namespace McProtocolSimulator.Protocol;

/// <summary>
/// MC 프로토콜 명령 파싱 결과 (일괄 읽기/쓰기용)
/// </summary>
public class McCommandInfo
{
    public DeviceType DeviceType { get; set; }
    public int StartAddress { get; set; }
    public int PointCount { get; set; }
    public bool IsBitAccess { get; set; }
    public byte[]? WriteData { get; set; }
}

/// <summary>
/// 랜덤 읽기/쓰기용 개별 포인트 정보
/// </summary>
public class McRandomPointInfo
{
    public DeviceType DeviceType { get; set; }
    public int Address { get; set; }
    public ushort Value { get; set; }  // 쓰기 시 사용
}

/// <summary>
/// 랜덤 읽기/쓰기 명령 파싱 결과
/// </summary>
public class McRandomCommandInfo
{
    public List<McRandomPointInfo> Points { get; set; } = new();
    public bool IsBitAccess { get; set; }
}

/// <summary>
/// MC 프로토콜 명령 파서
/// </summary>
public static class McCommandParser
{
    /// <summary>
    /// 바이너리 요청에서 읽기 명령 파싱
    /// MC 프로토콜 3E 프레임 데이터 구조 (인덱스 15부터):
    /// - 시작 주소 (3바이트, 리틀 엔디안) [Data[0-2]]
    /// - 디바이스 코드 (1바이트) [Data[3]]
    /// - 포인트 수 (2바이트, 리틀 엔디안) [Data[4-5]]
    /// </summary>
    public static McCommandInfo? ParseBatchReadBinary(McRequestFrame request)
    {
        // 최소 6바이트 필요: 주소(3) + 디바이스코드(1) + 포인트수(2)
        if (request.Data.Length < 6)
        {
            System.Diagnostics.Debug.WriteLine($"[ParseBatchReadBinary] 데이터 길이 부족: {request.Data.Length} < 6");
            return null;
        }

        // 디버그: 수신된 데이터 출력
        System.Diagnostics.Debug.WriteLine($"[ParseBatchReadBinary] Data: {BitConverter.ToString(request.Data)}");

        var info = new McCommandInfo
        {
            IsBitAccess = request.SubCommand == McProtocolConstants.SubCommandBit
        };

        // 시작 주소 (3바이트, 리틀 엔디안) - Data[0], Data[1], Data[2]
        info.StartAddress = request.Data[0] | (request.Data[1] << 8) | (request.Data[2] << 16);

        // 디바이스 코드 (1바이트) - Data[3]
        byte deviceCode = request.Data[3];
        info.DeviceType = GetDeviceTypeFromBinaryCode(deviceCode);

        // 포인트 수 (2바이트, 리틀 엔디안) - Data[4], Data[5]
        info.PointCount = request.Data[4] | (request.Data[5] << 8);

        System.Diagnostics.Debug.WriteLine($"[ParseBatchReadBinary] Address={info.StartAddress}, DeviceCode=0x{deviceCode:X2}, DeviceType={info.DeviceType}, PointCount={info.PointCount}");

        return info;
    }

    /// <summary>
    /// 바이너리 요청에서 쓰기 명령 파싱
    /// MC 프로토콜 3E 프레임 데이터 구조:
    /// - 시작 주소 (3바이트, 리틀 엔디안) [Data[0-2]]
    /// - 디바이스 코드 (1바이트) [Data[3]]
    /// - 포인트 수 (2바이트, 리틀 엔디안) [Data[4-5]]
    /// - 쓰기 데이터 [Data[6~]]
    /// </summary>
    public static McCommandInfo? ParseBatchWriteBinary(McRequestFrame request)
    {
        if (request.Data.Length < 6) return null;

        var info = new McCommandInfo
        {
            IsBitAccess = request.SubCommand == McProtocolConstants.SubCommandBit
        };

        // 시작 주소 (3바이트, 리틀 엔디안) - Data[0], Data[1], Data[2]
        info.StartAddress = request.Data[0] | (request.Data[1] << 8) | (request.Data[2] << 16);

        // 디바이스 코드 (1바이트) - Data[3]
        byte deviceCode = request.Data[3];
        info.DeviceType = GetDeviceTypeFromBinaryCode(deviceCode);

        // 포인트 수 (2바이트, 리틀 엔디안) - Data[4], Data[5]
        info.PointCount = request.Data[4] | (request.Data[5] << 8);

        // 쓰기 데이터
        if (request.Data.Length > 6)
        {
            info.WriteData = new byte[request.Data.Length - 6];
            Array.Copy(request.Data, 6, info.WriteData, 0, info.WriteData.Length);
        }

        return info;
    }

    /// <summary>
    /// ASCII 요청에서 읽기 명령 파싱
    /// </summary>
    public static McCommandInfo? ParseBatchReadAscii(McRequestFrame request)
    {
        if (request.Data.Length < 12) return null;

        try
        {
            string dataStr = System.Text.Encoding.ASCII.GetString(request.Data);

            var info = new McCommandInfo
            {
                IsBitAccess = request.SubCommand == McProtocolConstants.SubCommandBit
            };

            // 디바이스 코드 (4문자)
            string deviceCode = dataStr.Substring(0, 4).TrimEnd('*');
            info.DeviceType = GetDeviceTypeFromAsciiCode(deviceCode);

            // 시작 주소 (6문자, 16진수 또는 10진수)
            string addressStr = dataStr.Substring(4, 6);
            info.StartAddress = ParseAsciiAddress(addressStr, info.DeviceType);

            // 포인트 수 (4문자)
            if (dataStr.Length >= 14)
            {
                info.PointCount = int.Parse(dataStr.Substring(10, 4), System.Globalization.NumberStyles.HexNumber);
            }

            return info;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// ASCII 요청에서 쓰기 명령 파싱
    /// </summary>
    public static McCommandInfo? ParseBatchWriteAscii(McRequestFrame request)
    {
        if (request.Data.Length < 14) return null;

        try
        {
            string dataStr = System.Text.Encoding.ASCII.GetString(request.Data);

            var info = new McCommandInfo
            {
                IsBitAccess = request.SubCommand == McProtocolConstants.SubCommandBit
            };

            // 디바이스 코드 (4문자)
            string deviceCode = dataStr.Substring(0, 4).TrimEnd('*');
            info.DeviceType = GetDeviceTypeFromAsciiCode(deviceCode);

            // 시작 주소 (6문자)
            string addressStr = dataStr.Substring(4, 6);
            info.StartAddress = ParseAsciiAddress(addressStr, info.DeviceType);

            // 포인트 수 (4문자)
            info.PointCount = int.Parse(dataStr.Substring(10, 4), System.Globalization.NumberStyles.HexNumber);

            // 쓰기 데이터
            if (dataStr.Length > 14)
            {
                string dataHex = dataStr.Substring(14);
                info.WriteData = ConvertHexStringToBytes(dataHex);
            }

            return info;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 바이너리 요청에서 랜덤 읽기 명령 파싱
    /// MC 프로토콜 3E 프레임 랜덤 읽기 데이터 구조:
    /// - 워드 포인트 수 (1바이트) [Data[0]]
    /// - 더블워드 포인트 수 (1바이트) [Data[1]]
    /// - 각 포인트: 주소(3바이트) + 디바이스코드(1바이트) = 4바이트씩
    /// </summary>
    public static McRandomCommandInfo? ParseRandomReadBinary(McRequestFrame request)
    {
        if (request.Data.Length < 2) return null;

        var info = new McRandomCommandInfo
        {
            IsBitAccess = request.SubCommand == McProtocolConstants.SubCommandBit
        };

        int wordCount = request.Data[0];
        int dwordCount = request.Data[1];
        int totalCount = wordCount + dwordCount;

        System.Diagnostics.Debug.WriteLine($"[ParseRandomReadBinary] WordCount={wordCount}, DWordCount={dwordCount}");

        int offset = 2;
        for (int i = 0; i < totalCount && offset + 4 <= request.Data.Length; i++)
        {
            int address = request.Data[offset] | (request.Data[offset + 1] << 8) | (request.Data[offset + 2] << 16);
            byte deviceCode = request.Data[offset + 3];

            info.Points.Add(new McRandomPointInfo
            {
                Address = address,
                DeviceType = GetDeviceTypeFromBinaryCode(deviceCode)
            });

            System.Diagnostics.Debug.WriteLine($"[ParseRandomReadBinary] Point[{i}]: Address={address}, DeviceCode=0x{deviceCode:X2}");
            offset += 4;
        }

        return info;
    }

    /// <summary>
    /// 바이너리 요청에서 랜덤 쓰기 명령 파싱
    /// MC 프로토콜 3E 프레임 랜덤 쓰기 데이터 구조:
    /// - 워드 포인트 수 (1바이트) [Data[0]]
    /// - 더블워드 포인트 수 (1바이트) [Data[1]]
    /// - 각 포인트: 주소(3바이트) + 디바이스코드(1바이트) + 데이터(2바이트) = 6바이트씩
    /// </summary>
    public static McRandomCommandInfo? ParseRandomWriteBinary(McRequestFrame request)
    {
        if (request.Data.Length < 2) return null;

        var info = new McRandomCommandInfo
        {
            IsBitAccess = request.SubCommand == McProtocolConstants.SubCommandBit
        };

        int wordCount = request.Data[0];
        int dwordCount = request.Data[1];

        System.Diagnostics.Debug.WriteLine($"[ParseRandomWriteBinary] WordCount={wordCount}, DWordCount={dwordCount}");

        int offset = 2;

        // 워드 데이터 (각 6바이트: 주소3 + 디바이스1 + 데이터2)
        for (int i = 0; i < wordCount && offset + 6 <= request.Data.Length; i++)
        {
            int address = request.Data[offset] | (request.Data[offset + 1] << 8) | (request.Data[offset + 2] << 16);
            byte deviceCode = request.Data[offset + 3];
            ushort value = (ushort)(request.Data[offset + 4] | (request.Data[offset + 5] << 8));

            info.Points.Add(new McRandomPointInfo
            {
                Address = address,
                DeviceType = GetDeviceTypeFromBinaryCode(deviceCode),
                Value = value
            });

            System.Diagnostics.Debug.WriteLine($"[ParseRandomWriteBinary] Point[{i}]: Address={address}, DeviceCode=0x{deviceCode:X2}, Value={value}");
            offset += 6;
        }

        // 더블워드 데이터는 현재 지원하지 않음 (필요시 추가)

        return info;
    }

    public static DeviceType GetDeviceTypeFromBinaryCode(byte code) => code switch
    {
        McProtocolConstants.DeviceCodeBinary.D => DeviceType.D,
        McProtocolConstants.DeviceCodeBinary.M => DeviceType.M,
        McProtocolConstants.DeviceCodeBinary.X => DeviceType.X,
        McProtocolConstants.DeviceCodeBinary.Y => DeviceType.Y,
        McProtocolConstants.DeviceCodeBinary.W => DeviceType.W,
        McProtocolConstants.DeviceCodeBinary.B => DeviceType.B,
        McProtocolConstants.DeviceCodeBinary.R => DeviceType.R,
        McProtocolConstants.DeviceCodeBinary.L => DeviceType.L,
        _ => DeviceType.Unknown
    };

    private static DeviceType GetDeviceTypeFromAsciiCode(string code) => code.ToUpper() switch
    {
        "D" => DeviceType.D,
        "M" => DeviceType.M,
        "X" => DeviceType.X,
        "Y" => DeviceType.Y,
        "W" => DeviceType.W,
        "B" => DeviceType.B,
        "R" => DeviceType.R,
        "L" => DeviceType.L,
        _ => DeviceType.Unknown
    };

    private static int ParseAsciiAddress(string addressStr, DeviceType deviceType)
    {
        // X, Y는 8진수, 나머지는 10진수
        if (deviceType == DeviceType.X || deviceType == DeviceType.Y)
        {
            return Convert.ToInt32(addressStr, 8);
        }
        return int.Parse(addressStr);
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
