namespace S7ProtocolSimulator.Protocol;

/// <summary>
/// TPKT 헤더 (RFC 1006)
/// </summary>
public class TpktHeader
{
    public byte Version { get; set; } = S7Constants.TpktVersion;
    public byte Reserved { get; set; } = S7Constants.TpktReserved;
    public ushort Length { get; set; }

    public static TpktHeader Parse(byte[] buffer)
    {
        return new TpktHeader
        {
            Version = buffer[0],
            Reserved = buffer[1],
            Length = (ushort)((buffer[2] << 8) | buffer[3])
        };
    }

    public byte[] ToBytes()
    {
        return new byte[]
        {
            Version,
            Reserved,
            (byte)(Length >> 8),
            (byte)(Length & 0xFF)
        };
    }
}

/// <summary>
/// COTP 연결 요청/확인 (ISO 8073)
/// </summary>
public class CotpConnection
{
    public byte Length { get; set; }
    public byte PduType { get; set; }
    public ushort DstRef { get; set; }
    public ushort SrcRef { get; set; }
    public byte Class { get; set; }

    // TSAP 파라미터
    public byte[]? SrcTsap { get; set; }
    public byte[]? DstTsap { get; set; }
    public byte TpduSize { get; set; } = 0x0A;  // 1024 bytes

    public static CotpConnection ParseRequest(byte[] buffer, int offset)
    {
        var conn = new CotpConnection
        {
            Length = buffer[offset],
            PduType = buffer[offset + 1],
            DstRef = (ushort)((buffer[offset + 2] << 8) | buffer[offset + 3]),
            SrcRef = (ushort)((buffer[offset + 4] << 8) | buffer[offset + 5]),
            Class = buffer[offset + 6]
        };

        // 파라미터 파싱 (옵션)
        int paramOffset = offset + 7;
        int endOffset = offset + conn.Length + 1;

        while (paramOffset < endOffset && paramOffset < buffer.Length - 1)
        {
            byte paramCode = buffer[paramOffset];
            byte paramLen = buffer[paramOffset + 1];

            if (paramOffset + 2 + paramLen > buffer.Length) break;

            switch (paramCode)
            {
                case 0xC0:  // TPDU Size
                    if (paramLen >= 1) conn.TpduSize = buffer[paramOffset + 2];
                    break;
                case 0xC1:  // Source TSAP
                    conn.SrcTsap = new byte[paramLen];
                    Array.Copy(buffer, paramOffset + 2, conn.SrcTsap, 0, paramLen);
                    break;
                case 0xC2:  // Destination TSAP
                    conn.DstTsap = new byte[paramLen];
                    Array.Copy(buffer, paramOffset + 2, conn.DstTsap, 0, paramLen);
                    break;
            }

            paramOffset += 2 + paramLen;
        }

        return conn;
    }

    /// <summary>
    /// TSAP에서 Rack/Slot 정보 추출
    /// TSAP 형식: [ConnectionType][Rack*32 + Slot]
    /// </summary>
    public (int Rack, int Slot) GetRackSlot()
    {
        if (DstTsap == null || DstTsap.Length < 2)
            return (0, 1);  // 기본값

        byte rackSlotByte = DstTsap[1];
        int rack = (rackSlotByte >> 5) & 0x07;
        int slot = rackSlotByte & 0x1F;
        return (rack, slot);
    }

    public byte[] ToConfirmBytes()
    {
        var response = new List<byte>();

        // COTP CR 기본 헤더 (Length는 나중에 설정)
        response.Add(0x00);  // Length (placeholder)
        response.Add(S7Constants.CotpConnectionConfirm);
        response.Add((byte)(SrcRef >> 8));  // Dst Ref (원래 Src)
        response.Add((byte)(SrcRef & 0xFF));
        response.Add((byte)(DstRef >> 8));  // Src Ref (원래 Dst)
        response.Add((byte)(DstRef & 0xFF));
        response.Add(0x00);  // Class 0

        // TPDU Size 파라미터
        response.Add(0xC0);
        response.Add(0x01);
        response.Add(TpduSize);

        // Source TSAP (클라이언트가 보낸 Dst TSAP를 반환)
        if (DstTsap != null && DstTsap.Length > 0)
        {
            response.Add(0xC1);
            response.Add((byte)DstTsap.Length);
            response.AddRange(DstTsap);
        }
        else
        {
            response.Add(0xC1);
            response.Add(0x02);
            response.Add(0x01);
            response.Add(0x00);
        }

        // Destination TSAP (클라이언트가 보낸 Src TSAP를 반환)
        if (SrcTsap != null && SrcTsap.Length > 0)
        {
            response.Add(0xC2);
            response.Add((byte)SrcTsap.Length);
            response.AddRange(SrcTsap);
        }
        else
        {
            response.Add(0xC2);
            response.Add(0x02);
            response.Add(0x01);
            response.Add(0x02);
        }

        // Length 설정 (첫 바이트 제외한 길이)
        response[0] = (byte)(response.Count - 1);

        return response.ToArray();
    }
}

/// <summary>
/// COTP 데이터
/// </summary>
public class CotpData
{
    public byte Length { get; set; } = 0x02;
    public byte PduType { get; set; } = S7Constants.CotpData;
    public byte TpduNumber { get; set; } = 0x80;

    public static CotpData Parse(byte[] buffer, int offset)
    {
        return new CotpData
        {
            Length = buffer[offset],
            PduType = buffer[offset + 1],
            TpduNumber = buffer[offset + 2]
        };
    }

    public byte[] ToBytes()
    {
        return new byte[] { Length, PduType, TpduNumber };
    }
}

/// <summary>
/// S7 프로토콜 헤더
/// </summary>
public class S7Header
{
    public byte ProtocolId { get; set; } = S7Constants.ProtocolId;
    public byte MsgType { get; set; }
    public ushort Reserved { get; set; }
    public ushort PduRef { get; set; }
    public ushort ParamLength { get; set; }
    public ushort DataLength { get; set; }
    public byte ErrorClass { get; set; }
    public byte ErrorCode { get; set; }

    public static S7Header Parse(byte[] buffer, int offset)
    {
        var header = new S7Header
        {
            ProtocolId = buffer[offset],
            MsgType = buffer[offset + 1],
            Reserved = (ushort)((buffer[offset + 2] << 8) | buffer[offset + 3]),
            PduRef = (ushort)((buffer[offset + 4] << 8) | buffer[offset + 5]),
            ParamLength = (ushort)((buffer[offset + 6] << 8) | buffer[offset + 7]),
            DataLength = (ushort)((buffer[offset + 8] << 8) | buffer[offset + 9])
        };

        if (header.MsgType == S7Constants.MsgTypeAckData && buffer.Length > offset + 11)
        {
            header.ErrorClass = buffer[offset + 10];
            header.ErrorCode = buffer[offset + 11];
        }

        return header;
    }

    public byte[] ToBytes(bool includeError = false)
    {
        if (includeError)
        {
            return new byte[]
            {
                ProtocolId, MsgType,
                (byte)(Reserved >> 8), (byte)(Reserved & 0xFF),
                (byte)(PduRef >> 8), (byte)(PduRef & 0xFF),
                (byte)(ParamLength >> 8), (byte)(ParamLength & 0xFF),
                (byte)(DataLength >> 8), (byte)(DataLength & 0xFF),
                ErrorClass, ErrorCode
            };
        }

        return new byte[]
        {
            ProtocolId, MsgType,
            (byte)(Reserved >> 8), (byte)(Reserved & 0xFF),
            (byte)(PduRef >> 8), (byte)(PduRef & 0xFF),
            (byte)(ParamLength >> 8), (byte)(ParamLength & 0xFF),
            (byte)(DataLength >> 8), (byte)(DataLength & 0xFF)
        };
    }
}

/// <summary>
/// S7 읽기/쓰기 요청 아이템
/// </summary>
public class S7RequestItem
{
    public byte SpecType { get; set; } = 0x12;
    public byte Length { get; set; } = 0x0A;
    public byte SyntaxId { get; set; } = 0x10;
    public byte TransportSize { get; set; }
    public ushort Count { get; set; }
    public ushort DbNumber { get; set; }
    public byte Area { get; set; }
    public int Address { get; set; }  // 비트 주소 (바이트주소 * 8 + 비트)

    public int ByteAddress => Address / 8;
    public int BitAddress => Address % 8;

    public static S7RequestItem Parse(byte[] buffer, int offset)
    {
        return new S7RequestItem
        {
            SpecType = buffer[offset],
            Length = buffer[offset + 1],
            SyntaxId = buffer[offset + 2],
            TransportSize = buffer[offset + 3],
            Count = (ushort)((buffer[offset + 4] << 8) | buffer[offset + 5]),
            DbNumber = (ushort)((buffer[offset + 6] << 8) | buffer[offset + 7]),
            Area = buffer[offset + 8],
            Address = (buffer[offset + 9] << 16) | (buffer[offset + 10] << 8) | buffer[offset + 11]
        };
    }
}
