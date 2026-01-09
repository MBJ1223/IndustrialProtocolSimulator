namespace McProtocolSimulator.Protocol;

/// <summary>
/// MC 프로토콜 3E 프레임 요청 구조
/// </summary>
public class McRequestFrame
{
    public ushort Subheader { get; set; }        // 서브헤더 (0x5000)
    public byte NetworkNo { get; set; }           // 네트워크 번호
    public byte PcNo { get; set; }                // PC 번호
    public ushort RequestDestModuleIoNo { get; set; }  // 요청 대상 모듈 I/O 번호
    public byte RequestDestModuleStationNo { get; set; } // 요청 대상 모듈 국번호
    public ushort DataLength { get; set; }        // 데이터 길이
    public ushort MonitoringTimer { get; set; }   // 감시 타이머
    public ushort Command { get; set; }           // 명령
    public ushort SubCommand { get; set; }        // 서브 명령
    public byte[] Data { get; set; } = Array.Empty<byte>();  // 요청 데이터

    public ProtocolFormat Format { get; set; } = ProtocolFormat.Binary;

    /// <summary>
    /// 바이너리 데이터에서 프레임 파싱
    /// </summary>
    public static McRequestFrame? ParseBinary(byte[] buffer)
    {
        if (buffer.Length < 21) return null;

        var frame = new McRequestFrame
        {
            Format = ProtocolFormat.Binary,
            Subheader = BitConverter.ToUInt16(buffer, 0),
            NetworkNo = buffer[2],
            PcNo = buffer[3],
            RequestDestModuleIoNo = BitConverter.ToUInt16(buffer, 4),
            RequestDestModuleStationNo = buffer[6],
            DataLength = BitConverter.ToUInt16(buffer, 7),
            MonitoringTimer = BitConverter.ToUInt16(buffer, 9),
            Command = BitConverter.ToUInt16(buffer, 11),
            SubCommand = BitConverter.ToUInt16(buffer, 13)
        };

        // 데이터 부분 추출 (15바이트부터)
        int dataStart = 15;
        int dataLen = frame.DataLength - 6; // DataLength에서 타이머, 명령, 서브명령 제외
        if (dataLen > 0 && buffer.Length >= dataStart + dataLen)
        {
            frame.Data = new byte[dataLen];
            Array.Copy(buffer, dataStart, frame.Data, 0, dataLen);
        }

        return frame;
    }

    /// <summary>
    /// ASCII 데이터에서 프레임 파싱
    /// </summary>
    public static McRequestFrame? ParseAscii(byte[] buffer)
    {
        if (buffer.Length < 42) return null;

        try
        {
            string ascii = System.Text.Encoding.ASCII.GetString(buffer);

            var frame = new McRequestFrame
            {
                Format = ProtocolFormat.Ascii,
                Subheader = ushort.Parse(ascii.Substring(0, 4), System.Globalization.NumberStyles.HexNumber),
                NetworkNo = byte.Parse(ascii.Substring(4, 2), System.Globalization.NumberStyles.HexNumber),
                PcNo = byte.Parse(ascii.Substring(6, 2), System.Globalization.NumberStyles.HexNumber),
                RequestDestModuleIoNo = ushort.Parse(ascii.Substring(8, 4), System.Globalization.NumberStyles.HexNumber),
                RequestDestModuleStationNo = byte.Parse(ascii.Substring(12, 2), System.Globalization.NumberStyles.HexNumber),
                DataLength = ushort.Parse(ascii.Substring(14, 4), System.Globalization.NumberStyles.HexNumber),
                MonitoringTimer = ushort.Parse(ascii.Substring(18, 4), System.Globalization.NumberStyles.HexNumber),
                Command = ushort.Parse(ascii.Substring(22, 4), System.Globalization.NumberStyles.HexNumber),
                SubCommand = ushort.Parse(ascii.Substring(26, 4), System.Globalization.NumberStyles.HexNumber)
            };

            // 데이터 부분 추출
            if (ascii.Length > 30)
            {
                string dataHex = ascii.Substring(30);
                frame.Data = ConvertHexStringToBytes(dataHex);
            }

            return frame;
        }
        catch
        {
            return null;
        }
    }

    private static byte[] ConvertHexStringToBytes(string hex)
    {
        var bytes = new List<byte>();
        for (int i = 0; i < hex.Length - 1; i += 2)
        {
            bytes.Add(byte.Parse(hex.Substring(i, 2), System.Globalization.NumberStyles.HexNumber));
        }
        return bytes.ToArray();
    }
}

/// <summary>
/// MC 프로토콜 3E 프레임 응답 구조
/// </summary>
public class McResponseFrame
{
    public ushort Subheader { get; set; }        // 서브헤더 (0xD000)
    public byte NetworkNo { get; set; }           // 네트워크 번호
    public byte PcNo { get; set; }                // PC 번호
    public ushort RequestDestModuleIoNo { get; set; }  // 요청 대상 모듈 I/O 번호
    public byte RequestDestModuleStationNo { get; set; } // 요청 대상 모듈 국번호
    public ushort DataLength { get; set; }        // 데이터 길이
    public ushort EndCode { get; set; }           // 종료 코드
    public byte[] Data { get; set; } = Array.Empty<byte>();  // 응답 데이터

    public ProtocolFormat Format { get; set; } = ProtocolFormat.Binary;

    /// <summary>
    /// 바이너리 응답 프레임 생성
    /// </summary>
    public byte[] ToBinaryBytes()
    {
        DataLength = (ushort)(2 + Data.Length); // EndCode + Data

        var buffer = new byte[11 + Data.Length];
        BitConverter.GetBytes(McProtocolConstants.SubheaderBinaryResponse).CopyTo(buffer, 0);
        buffer[2] = NetworkNo;
        buffer[3] = PcNo;
        BitConverter.GetBytes(RequestDestModuleIoNo).CopyTo(buffer, 4);
        buffer[6] = RequestDestModuleStationNo;
        BitConverter.GetBytes(DataLength).CopyTo(buffer, 7);
        BitConverter.GetBytes(EndCode).CopyTo(buffer, 9);
        Data.CopyTo(buffer, 11);

        return buffer;
    }

    /// <summary>
    /// ASCII 응답 프레임 생성
    /// </summary>
    public byte[] ToAsciiBytes()
    {
        DataLength = (ushort)(4 + Data.Length * 2); // EndCode(4) + Data(hex)

        var sb = new System.Text.StringBuilder();
        sb.Append(McProtocolConstants.SubheaderAsciiResponse);
        sb.Append(NetworkNo.ToString("X2"));
        sb.Append(PcNo.ToString("X2"));
        sb.Append(RequestDestModuleIoNo.ToString("X4"));
        sb.Append(RequestDestModuleStationNo.ToString("X2"));
        sb.Append(DataLength.ToString("X4"));
        sb.Append(EndCode.ToString("X4"));

        foreach (byte b in Data)
        {
            sb.Append(b.ToString("X2"));
        }

        return System.Text.Encoding.ASCII.GetBytes(sb.ToString());
    }

    /// <summary>
    /// 응답 프레임 생성
    /// </summary>
    public byte[] ToBytes()
    {
        return Format == ProtocolFormat.Binary ? ToBinaryBytes() : ToAsciiBytes();
    }

    /// <summary>
    /// 성공 응답 생성
    /// </summary>
    public static McResponseFrame CreateSuccess(McRequestFrame request, byte[] data)
    {
        return new McResponseFrame
        {
            Format = request.Format,
            NetworkNo = request.NetworkNo,
            PcNo = request.PcNo,
            RequestDestModuleIoNo = request.RequestDestModuleIoNo,
            RequestDestModuleStationNo = request.RequestDestModuleStationNo,
            EndCode = McProtocolConstants.ErrorCode.Success,
            Data = data
        };
    }

    /// <summary>
    /// 에러 응답 생성
    /// </summary>
    public static McResponseFrame CreateError(McRequestFrame request, ushort errorCode)
    {
        return new McResponseFrame
        {
            Format = request.Format,
            NetworkNo = request.NetworkNo,
            PcNo = request.PcNo,
            RequestDestModuleIoNo = request.RequestDestModuleIoNo,
            RequestDestModuleStationNo = request.RequestDestModuleStationNo,
            EndCode = errorCode,
            Data = Array.Empty<byte>()
        };
    }
}
