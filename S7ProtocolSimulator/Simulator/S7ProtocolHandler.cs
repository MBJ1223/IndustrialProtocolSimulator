using S7ProtocolSimulator.Protocol;

namespace S7ProtocolSimulator.Simulator;

/// <summary>
/// S7 프로토콜 핸들러
/// </summary>
public class S7ProtocolHandler
{
    private readonly S7Memory _memory;

    // 연결 상태
    public bool IsConnected { get; private set; }
    public int NegotiatedPduSize { get; private set; } = 480;
    public int Rack { get; private set; }
    public int Slot { get; private set; }
    public S7Memory Memory => _memory;

    public event EventHandler<string>? LogMessage;
    public event EventHandler<(int Rack, int Slot)>? ConnectionEstablished;

    public S7ProtocolHandler(S7Memory memory)
    {
        _memory = memory;
    }

    /// <summary>
    /// 요청 처리
    /// </summary>
    public byte[]? ProcessRequest(byte[] buffer)
    {
        if (buffer.Length < 4) return null;

        // TPKT 헤더 파싱
        var tpkt = TpktHeader.Parse(buffer);
        if (tpkt.Version != S7Constants.TpktVersion)
        {
            Log("잘못된 TPKT 버전");
            return null;
        }

        int offset = 4;  // TPKT 이후

        // COTP 타입 확인
        byte cotpType = buffer[offset + 1];

        return cotpType switch
        {
            S7Constants.CotpConnectionRequest => HandleConnectionRequest(buffer, offset),
            S7Constants.CotpData => HandleDataRequest(buffer, offset),
            _ => null
        };
    }

    /// <summary>
    /// COTP 연결 요청 처리
    /// </summary>
    private byte[] HandleConnectionRequest(byte[] buffer, int offset)
    {
        var cotp = CotpConnection.ParseRequest(buffer, offset);
        IsConnected = true;

        // Rack/Slot 정보 추출
        var (rack, slot) = cotp.GetRackSlot();
        Rack = rack;
        Slot = slot;

        Log($"COTP 연결 요청 수신 - SrcRef: {cotp.SrcRef}, DstRef: {cotp.DstRef}, Rack: {rack}, Slot: {slot}");
        ConnectionEstablished?.Invoke(this, (rack, slot));

        if (cotp.SrcTsap != null)
            Log($"  Source TSAP: {BitConverter.ToString(cotp.SrcTsap)}");
        if (cotp.DstTsap != null)
            Log($"  Dest TSAP: {BitConverter.ToString(cotp.DstTsap)}");

        // 연결 확인 응답 생성
        var cotpConfirm = cotp.ToConfirmBytes();
        var response = new byte[4 + cotpConfirm.Length];

        // TPKT 헤더
        response[0] = S7Constants.TpktVersion;
        response[1] = S7Constants.TpktReserved;
        response[2] = (byte)(response.Length >> 8);
        response[3] = (byte)(response.Length & 0xFF);

        cotpConfirm.CopyTo(response, 4);

        Log("COTP 연결 확인 응답 전송");
        return response;
    }

    /// <summary>
    /// S7 데이터 요청 처리
    /// </summary>
    private byte[]? HandleDataRequest(byte[] buffer, int offset)
    {
        // COTP Data 파싱
        var cotpData = CotpData.Parse(buffer, offset);
        offset += cotpData.Length + 1;

        // S7 헤더 파싱
        if (buffer.Length < offset + 10) return null;

        var s7Header = S7Header.Parse(buffer, offset);
        if (s7Header.ProtocolId != S7Constants.ProtocolId)
        {
            Log("잘못된 S7 프로토콜 ID");
            return null;
        }

        offset += 10;  // S7 헤더 이후

        // 함수 코드 확인
        byte funcCode = buffer[offset];
        Log($"S7 요청: MsgType={s7Header.MsgType}, Function=0x{funcCode:X2}");

        return funcCode switch
        {
            S7Constants.FuncSetupComm => HandleSetupCommunication(buffer, offset, s7Header),
            S7Constants.FuncReadVar => HandleReadVariable(buffer, offset, s7Header),
            S7Constants.FuncWriteVar => HandleWriteVariable(buffer, offset, s7Header),
            _ => CreateErrorResponse(s7Header, S7Constants.ReturnCodeAccessError)
        };
    }

    /// <summary>
    /// 통신 설정 처리
    /// </summary>
    private byte[] HandleSetupCommunication(byte[] buffer, int offset, S7Header requestHeader)
    {
        // 클라이언트 요청 파라미터 파싱
        // Function(1) + Reserved(1) + MaxAmQCalling(2) + MaxAmQCalled(2) + PduLength(2)
        ushort requestedPdu = 480;
        ushort maxAmqCalling = 1;
        ushort maxAmqCalled = 1;

        if (buffer.Length >= offset + 8)
        {
            maxAmqCalling = (ushort)((buffer[offset + 2] << 8) | buffer[offset + 3]);
            maxAmqCalled = (ushort)((buffer[offset + 4] << 8) | buffer[offset + 5]);
            requestedPdu = (ushort)((buffer[offset + 6] << 8) | buffer[offset + 7]);
        }

        Log($"Setup Communication 요청 - MaxAmQ Calling: {maxAmqCalling}, Called: {maxAmqCalled}, PDU Size: {requestedPdu}");

        // PDU 크기 협상 (클라이언트 요청과 서버 최대값 중 작은 값)
        const ushort MaxServerPdu = 960;  // S7-300/400 최대값
        ushort negotiatedPdu = Math.Min(requestedPdu, MaxServerPdu);
        NegotiatedPduSize = negotiatedPdu;

        Log($"Setup Communication 응답 - 협상된 PDU Size: {negotiatedPdu}");

        // 응답 파라미터
        byte[] param = new byte[]
        {
            S7Constants.FuncSetupComm,
            0x00,  // Reserved
            (byte)(maxAmqCalling >> 8), (byte)(maxAmqCalling & 0xFF),  // Max AmQ calling
            (byte)(maxAmqCalled >> 8), (byte)(maxAmqCalled & 0xFF),    // Max AmQ called
            (byte)(negotiatedPdu >> 8), (byte)(negotiatedPdu & 0xFF)   // PDU length
        };

        return CreateResponse(requestHeader, param, Array.Empty<byte>());
    }

    /// <summary>
    /// 변수 읽기 처리
    /// </summary>
    private byte[] HandleReadVariable(byte[] buffer, int offset, S7Header requestHeader)
    {
        // 파라미터: Function(1) + ItemCount(1) + Items(12*n)
        byte itemCount = buffer[offset + 1];
        Log($"Read Variable 요청: {itemCount}개 아이템");

        var responseData = new List<byte>();
        int itemOffset = offset + 2;

        for (int i = 0; i < itemCount; i++)
        {
            var item = S7RequestItem.Parse(buffer, itemOffset);
            itemOffset += 12;

            Log($"  읽기: Area=0x{item.Area:X2}, DB={item.DbNumber}, Addr={item.ByteAddress}, Count={item.Count}");

            // 데이터 읽기
            int byteCount = GetByteCount(item.TransportSize, item.Count);
            var data = _memory.ReadBytes(item.Area, item.DbNumber, item.ByteAddress, byteCount);

            // 응답 데이터 아이템 추가
            responseData.Add(S7Constants.ReturnCodeSuccess);  // Return code
            responseData.Add(item.TransportSize == S7Constants.TransportSizeBit ? (byte)0x03 : (byte)0x04);  // Transport size
            responseData.Add((byte)((byteCount * 8) >> 8));  // Length in bits (high)
            responseData.Add((byte)((byteCount * 8) & 0xFF));  // Length in bits (low)
            responseData.AddRange(data);

            // 패딩 (짝수 바이트)
            if (data.Length % 2 != 0 && i < itemCount - 1)
            {
                responseData.Add(0x00);
            }
        }

        // 응답 파라미터
        byte[] param = new byte[] { S7Constants.FuncReadVar, (byte)itemCount };

        return CreateResponse(requestHeader, param, responseData.ToArray());
    }

    /// <summary>
    /// 변수 쓰기 처리
    /// </summary>
    private byte[] HandleWriteVariable(byte[] buffer, int offset, S7Header requestHeader)
    {
        byte itemCount = buffer[offset + 1];
        Log($"Write Variable 요청: {itemCount}개 아이템");

        var responseData = new List<byte>();
        int itemOffset = offset + 2;

        // 파라미터 영역의 아이템들 파싱
        var items = new List<S7RequestItem>();
        for (int i = 0; i < itemCount; i++)
        {
            items.Add(S7RequestItem.Parse(buffer, itemOffset));
            itemOffset += 12;
        }

        // 데이터 영역으로 이동 (S7 헤더의 DataLength 위치 이후)
        int dataOffset = offset + requestHeader.ParamLength;

        foreach (var item in items)
        {
            // 데이터 아이템 헤더: ReturnCode(1) + TransportSize(1) + Length(2) + Data(n)
            byte returnCode = buffer[dataOffset];
            byte transportSize = buffer[dataOffset + 1];
            int dataLength = (buffer[dataOffset + 2] << 8) | buffer[dataOffset + 3];
            int byteLength = transportSize == 0x03 ? (dataLength + 7) / 8 : dataLength / 8;

            dataOffset += 4;

            Log($"  쓰기: Area=0x{item.Area:X2}, DB={item.DbNumber}, Addr={item.ByteAddress}, Len={byteLength}");

            // 데이터 추출 및 쓰기
            var writeData = new byte[byteLength];
            Array.Copy(buffer, dataOffset, writeData, 0, byteLength);

            bool success = _memory.WriteBytes(item.Area, item.DbNumber, item.ByteAddress, writeData);
            responseData.Add(success ? S7Constants.ReturnCodeSuccess : S7Constants.ReturnCodeAccessError);

            dataOffset += byteLength;
            if (byteLength % 2 != 0) dataOffset++;  // 패딩 건너뛰기
        }

        // 응답 파라미터
        byte[] param = new byte[] { S7Constants.FuncWriteVar, (byte)itemCount };

        return CreateResponse(requestHeader, param, responseData.ToArray());
    }

    /// <summary>
    /// 응답 프레임 생성
    /// </summary>
    private byte[] CreateResponse(S7Header requestHeader, byte[] param, byte[] data)
    {
        // COTP Data (3 bytes) + S7 Header (12 bytes) + Param + Data
        int totalLength = 4 + 3 + 12 + param.Length + data.Length;

        var response = new byte[totalLength];
        int pos = 0;

        // TPKT
        response[pos++] = S7Constants.TpktVersion;
        response[pos++] = S7Constants.TpktReserved;
        response[pos++] = (byte)(totalLength >> 8);
        response[pos++] = (byte)(totalLength & 0xFF);

        // COTP Data
        response[pos++] = 0x02;
        response[pos++] = S7Constants.CotpData;
        response[pos++] = 0x80;

        // S7 Header
        response[pos++] = S7Constants.ProtocolId;
        response[pos++] = S7Constants.MsgTypeAckData;
        response[pos++] = 0x00; response[pos++] = 0x00;  // Reserved
        response[pos++] = (byte)(requestHeader.PduRef >> 8);
        response[pos++] = (byte)(requestHeader.PduRef & 0xFF);
        response[pos++] = (byte)(param.Length >> 8);
        response[pos++] = (byte)(param.Length & 0xFF);
        response[pos++] = (byte)(data.Length >> 8);
        response[pos++] = (byte)(data.Length & 0xFF);
        response[pos++] = 0x00;  // Error class
        response[pos++] = 0x00;  // Error code

        // Parameter
        param.CopyTo(response, pos);
        pos += param.Length;

        // Data
        data.CopyTo(response, pos);

        return response;
    }

    /// <summary>
    /// 에러 응답 생성
    /// </summary>
    private byte[] CreateErrorResponse(S7Header requestHeader, byte errorCode)
    {
        byte[] param = new byte[] { 0x00 };
        var response = CreateResponse(requestHeader, param, Array.Empty<byte>());
        response[17] = 0x81;  // Error class
        response[18] = errorCode;
        return response;
    }

    private static int GetByteCount(byte transportSize, int count) => transportSize switch
    {
        S7Constants.TransportSizeBit => (count + 7) / 8,
        S7Constants.TransportSizeByte => count,
        S7Constants.TransportSizeWord => count * 2,
        S7Constants.TransportSizeDWord or S7Constants.TransportSizeReal => count * 4,
        _ => count
    };

    private void Log(string message)
    {
        LogMessage?.Invoke(this, $"[{DateTime.Now:HH:mm:ss.fff}] {message}");
    }
}
