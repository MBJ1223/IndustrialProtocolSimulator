using McProtocolSimulator.Protocol;

namespace McProtocolSimulator.Simulator;

/// <summary>
/// MC 프로토콜 핸들러
/// 수신된 요청을 처리하고 응답을 생성
/// </summary>
public class McProtocolHandler
{
    private readonly PlcMemory _memory;

    public event EventHandler<string>? LogMessage;

    public McProtocolHandler(PlcMemory memory)
    {
        _memory = memory;
    }

    /// <summary>
    /// 요청 처리
    /// </summary>
    public byte[]? ProcessRequest(byte[] buffer)
    {
        // 수신 데이터 로그 (디버깅용)
        Log($"수신 패킷 ({buffer.Length} bytes): {BitConverter.ToString(buffer)}");

        // 프로토콜 형식 감지 및 파싱
        var request = DetectAndParse(buffer);
        if (request == null)
        {
            Log("요청 파싱 실패");
            return null;
        }

        Log($"요청 수신: Command=0x{request.Command:X4}, SubCommand=0x{request.SubCommand:X4}, Format={request.Format}, DataLength={request.DataLength}, Data.Length={request.Data.Length}");

        // 데이터 부분 상세 로그
        if (request.Data.Length > 0)
        {
            Log($"데이터 부분: {BitConverter.ToString(request.Data)}");
        }

        McResponseFrame response;

        try
        {
            response = request.Command switch
            {
                McProtocolConstants.CommandBatchRead => HandleBatchRead(request),
                McProtocolConstants.CommandBatchWrite => HandleBatchWrite(request),
                McProtocolConstants.CommandRandomRead => HandleRandomRead(request),
                McProtocolConstants.CommandRandomWrite => HandleRandomWrite(request),
                _ => McResponseFrame.CreateError(request, McProtocolConstants.ErrorCode.CommandError)
            };
        }
        catch (Exception ex)
        {
            Log($"처리 중 오류: {ex.Message}");
            response = McResponseFrame.CreateError(request, McProtocolConstants.ErrorCode.DeviceError);
        }

        return response.ToBytes();
    }

    /// <summary>
    /// 프로토콜 형식 감지 및 파싱
    /// </summary>
    private McRequestFrame? DetectAndParse(byte[] buffer)
    {
        if (buffer.Length < 2) return null;

        // Binary 형식 확인 (서브헤더: 0x50 0x00 - 미쓰비시 사양)
        if (buffer[0] == 0x50 && buffer[1] == 0x00)
        {
            Log($"Binary 형식 감지 (서브헤더: 0x{buffer[0]:X2} 0x{buffer[1]:X2})");
            return McRequestFrame.ParseBinary(buffer);
        }

        // ASCII 형식 확인 ("5000")
        if (buffer[0] == '5' && buffer[1] == '0')
        {
            Log("ASCII 형식 감지");
            return McRequestFrame.ParseAscii(buffer);
        }

        // 기본적으로 Binary 시도
        Log($"기본 Binary 파싱 시도 (헤더: 0x{buffer[0]:X2} 0x{buffer[1]:X2})");
        return McRequestFrame.ParseBinary(buffer);
    }

    /// <summary>
    /// 일괄 읽기 처리
    /// </summary>
    private McResponseFrame HandleBatchRead(McRequestFrame request)
    {
        McCommandInfo? cmdInfo;

        if (request.Format == ProtocolFormat.Binary)
        {
            cmdInfo = McCommandParser.ParseBatchReadBinary(request);
        }
        else
        {
            cmdInfo = McCommandParser.ParseBatchReadAscii(request);
        }

        if (cmdInfo == null)
        {
            Log($"읽기 명령 파싱 실패: cmdInfo is null, Data.Length={request.Data.Length}");
            return McResponseFrame.CreateError(request, McProtocolConstants.ErrorCode.DeviceError);
        }

        if (cmdInfo.DeviceType == DeviceType.Unknown)
        {
            // 디바이스 타입을 알 수 없는 경우, 데이터 부분의 디바이스 코드 바이트 로그
            if (request.Data.Length > 3)
            {
                Log($"읽기 명령 파싱 실패: Unknown DeviceType, DeviceCode=0x{request.Data[3]:X2}");
            }
            else
            {
                Log($"읽기 명령 파싱 실패: Unknown DeviceType, Data.Length={request.Data.Length}");
            }
            return McResponseFrame.CreateError(request, McProtocolConstants.ErrorCode.DeviceError);
        }

        Log($"일괄 읽기: Device={cmdInfo.DeviceType}, Address={cmdInfo.StartAddress}, Count={cmdInfo.PointCount}, BitAccess={cmdInfo.IsBitAccess}");

        byte[] responseData;

        if (cmdInfo.IsBitAccess || IsBitDevice(cmdInfo.DeviceType))
        {
            // 비트 읽기
            var bits = _memory.ReadBits(cmdInfo.DeviceType, cmdInfo.StartAddress, cmdInfo.PointCount);
            responseData = ConvertBitsToBytes(bits, request.Format);
        }
        else
        {
            // 워드 읽기
            var words = _memory.ReadWords(cmdInfo.DeviceType, cmdInfo.StartAddress, cmdInfo.PointCount);
            responseData = ConvertWordsToBytes(words, request.Format);
        }

        Log($"응답 데이터: {responseData.Length} bytes");
        return McResponseFrame.CreateSuccess(request, responseData);
    }

    /// <summary>
    /// 일괄 쓰기 처리
    /// </summary>
    private McResponseFrame HandleBatchWrite(McRequestFrame request)
    {
        McCommandInfo? cmdInfo;

        if (request.Format == ProtocolFormat.Binary)
        {
            cmdInfo = McCommandParser.ParseBatchWriteBinary(request);
        }
        else
        {
            cmdInfo = McCommandParser.ParseBatchWriteAscii(request);
        }

        if (cmdInfo == null || cmdInfo.DeviceType == DeviceType.Unknown || cmdInfo.WriteData == null)
        {
            Log("쓰기 명령 파싱 실패");
            return McResponseFrame.CreateError(request, McProtocolConstants.ErrorCode.DeviceError);
        }

        Log($"일괄 쓰기: Device={cmdInfo.DeviceType}, Address={cmdInfo.StartAddress}, Count={cmdInfo.PointCount}, BitAccess={cmdInfo.IsBitAccess}");

        if (cmdInfo.IsBitAccess || IsBitDevice(cmdInfo.DeviceType))
        {
            // 비트 쓰기
            var bits = ConvertBytesToBits(cmdInfo.WriteData, cmdInfo.PointCount, request.Format);
            _memory.WriteBits(cmdInfo.DeviceType, cmdInfo.StartAddress, bits);
        }
        else
        {
            // 워드 쓰기
            var words = ConvertBytesToWords(cmdInfo.WriteData, request.Format);
            _memory.WriteWords(cmdInfo.DeviceType, cmdInfo.StartAddress, words);
        }

        return McResponseFrame.CreateSuccess(request, Array.Empty<byte>());
    }

    /// <summary>
    /// 랜덤 읽기 처리
    /// </summary>
    private McResponseFrame HandleRandomRead(McRequestFrame request)
    {
        if (request.Format != ProtocolFormat.Binary)
        {
            Log("랜덤 읽기: ASCII 형식은 현재 지원하지 않음");
            return McResponseFrame.CreateError(request, McProtocolConstants.ErrorCode.CommandError);
        }

        var cmdInfo = McCommandParser.ParseRandomReadBinary(request);
        if (cmdInfo == null || cmdInfo.Points.Count == 0)
        {
            Log($"랜덤 읽기 명령 파싱 실패: Data.Length={request.Data.Length}");
            return McResponseFrame.CreateError(request, McProtocolConstants.ErrorCode.DeviceError);
        }

        Log($"랜덤 읽기: {cmdInfo.Points.Count}개 포인트");

        // 각 포인트의 값을 읽어서 응답 데이터 생성
        var responseData = new List<byte>();
        foreach (var point in cmdInfo.Points)
        {
            if (point.DeviceType == DeviceType.Unknown)
            {
                Log($"랜덤 읽기: Unknown DeviceType at Address={point.Address}");
                // Unknown 디바이스는 0으로 응답
                responseData.Add(0);
                responseData.Add(0);
                continue;
            }

            var words = _memory.ReadWords(point.DeviceType, point.Address, 1);
            responseData.Add((byte)(words[0] & 0xFF));
            responseData.Add((byte)((words[0] >> 8) & 0xFF));

            Log($"랜덤 읽기: {point.DeviceType}{point.Address} = {words[0]}");
        }

        Log($"랜덤 읽기 응답: {responseData.Count} bytes");
        return McResponseFrame.CreateSuccess(request, responseData.ToArray());
    }

    /// <summary>
    /// 랜덤 쓰기 처리
    /// </summary>
    private McResponseFrame HandleRandomWrite(McRequestFrame request)
    {
        if (request.Format != ProtocolFormat.Binary)
        {
            Log("랜덤 쓰기: ASCII 형식은 현재 지원하지 않음");
            return McResponseFrame.CreateError(request, McProtocolConstants.ErrorCode.CommandError);
        }

        var cmdInfo = McCommandParser.ParseRandomWriteBinary(request);
        if (cmdInfo == null || cmdInfo.Points.Count == 0)
        {
            Log($"랜덤 쓰기 명령 파싱 실패: Data.Length={request.Data.Length}");
            return McResponseFrame.CreateError(request, McProtocolConstants.ErrorCode.DeviceError);
        }

        Log($"랜덤 쓰기: {cmdInfo.Points.Count}개 포인트");

        // 각 포인트에 값 쓰기
        foreach (var point in cmdInfo.Points)
        {
            if (point.DeviceType == DeviceType.Unknown)
            {
                Log($"랜덤 쓰기: Unknown DeviceType at Address={point.Address}, 건너뜀");
                continue;
            }

            _memory.WriteWords(point.DeviceType, point.Address, new[] { point.Value });
            Log($"랜덤 쓰기: {point.DeviceType}{point.Address} = {point.Value}");
        }

        return McResponseFrame.CreateSuccess(request, Array.Empty<byte>());
    }

    #region 변환 헬퍼

    private static bool IsBitDevice(DeviceType deviceType)
    {
        return deviceType switch
        {
            DeviceType.M or DeviceType.X or DeviceType.Y or DeviceType.B or DeviceType.L => true,
            _ => false
        };
    }

    private static byte[] ConvertWordsToBytes(ushort[] words, ProtocolFormat format)
    {
        if (format == ProtocolFormat.Binary)
        {
            var bytes = new byte[words.Length * 2];
            for (int i = 0; i < words.Length; i++)
            {
                BitConverter.GetBytes(words[i]).CopyTo(bytes, i * 2);
            }
            return bytes;
        }
        else
        {
            // ASCII: 각 워드를 4자리 16진수로
            var result = new List<byte>();
            foreach (var word in words)
            {
                var hex = word.ToString("X4");
                result.AddRange(System.Text.Encoding.ASCII.GetBytes(hex));
            }
            return result.ToArray();
        }
    }

    private static ushort[] ConvertBytesToWords(byte[] bytes, ProtocolFormat format)
    {
        if (format == ProtocolFormat.Binary)
        {
            var words = new ushort[bytes.Length / 2];
            for (int i = 0; i < words.Length; i++)
            {
                words[i] = BitConverter.ToUInt16(bytes, i * 2);
            }
            return words;
        }
        else
        {
            // ASCII: 4자리 16진수씩 파싱
            string ascii = System.Text.Encoding.ASCII.GetString(bytes);
            var words = new List<ushort>();
            for (int i = 0; i < ascii.Length - 3; i += 4)
            {
                words.Add(ushort.Parse(ascii.Substring(i, 4), System.Globalization.NumberStyles.HexNumber));
            }
            return words.ToArray();
        }
    }

    private static byte[] ConvertBitsToBytes(bool[] bits, ProtocolFormat format)
    {
        if (format == ProtocolFormat.Binary)
        {
            // Binary: 2비트당 1바이트 (각 비트가 0x00 또는 0x01)
            var bytes = new byte[(bits.Length + 1) / 2];
            for (int i = 0; i < bits.Length; i++)
            {
                if (bits[i])
                {
                    if (i % 2 == 0)
                        bytes[i / 2] |= 0x10;
                    else
                        bytes[i / 2] |= 0x01;
                }
            }
            return bytes;
        }
        else
        {
            // ASCII: 각 비트를 '0' 또는 '1'로
            var result = new byte[bits.Length];
            for (int i = 0; i < bits.Length; i++)
            {
                result[i] = (byte)(bits[i] ? '1' : '0');
            }
            return result;
        }
    }

    private static bool[] ConvertBytesToBits(byte[] bytes, int count, ProtocolFormat format)
    {
        var bits = new bool[count];

        if (format == ProtocolFormat.Binary)
        {
            // Binary: 2비트당 1바이트
            for (int i = 0; i < count && i / 2 < bytes.Length; i++)
            {
                if (i % 2 == 0)
                    bits[i] = (bytes[i / 2] & 0x10) != 0;
                else
                    bits[i] = (bytes[i / 2] & 0x01) != 0;
            }
        }
        else
        {
            // ASCII: '0' 또는 '1'
            for (int i = 0; i < count && i < bytes.Length; i++)
            {
                bits[i] = bytes[i] == '1';
            }
        }

        return bits;
    }

    #endregion

    private void Log(string message)
    {
        LogMessage?.Invoke(this, $"[{DateTime.Now:HH:mm:ss.fff}] {message}");
    }
}
