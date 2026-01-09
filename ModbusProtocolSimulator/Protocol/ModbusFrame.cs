namespace ModbusProtocolSimulator.Protocol;

/// <summary>
/// MBAP (Modbus Application Protocol) 헤더
/// Modbus TCP에서 사용하는 7바이트 헤더
/// </summary>
public class MbapHeader
{
    /// <summary>트랜잭션 식별자 (요청-응답 매칭)</summary>
    public ushort TransactionId { get; set; }

    /// <summary>프로토콜 식별자 (Modbus TCP = 0x0000)</summary>
    public ushort ProtocolId { get; set; }

    /// <summary>이후 데이터 길이 (Unit ID + PDU)</summary>
    public ushort Length { get; set; }

    /// <summary>유닛 식별자 (슬레이브 주소, TCP에서는 보통 0xFF 또는 0x01)</summary>
    public byte UnitId { get; set; }

    public static MbapHeader Parse(byte[] buffer, int offset = 0)
    {
        return new MbapHeader
        {
            TransactionId = (ushort)((buffer[offset] << 8) | buffer[offset + 1]),
            ProtocolId = (ushort)((buffer[offset + 2] << 8) | buffer[offset + 3]),
            Length = (ushort)((buffer[offset + 4] << 8) | buffer[offset + 5]),
            UnitId = buffer[offset + 6]
        };
    }

    public byte[] ToBytes()
    {
        return new byte[]
        {
            (byte)(TransactionId >> 8), (byte)(TransactionId & 0xFF),
            (byte)(ProtocolId >> 8), (byte)(ProtocolId & 0xFF),
            (byte)(Length >> 8), (byte)(Length & 0xFF),
            UnitId
        };
    }
}

/// <summary>
/// Modbus 요청 PDU (Protocol Data Unit)
/// </summary>
public class ModbusRequest
{
    /// <summary>기능 코드</summary>
    public byte FunctionCode { get; set; }

    /// <summary>시작 주소</summary>
    public ushort StartAddress { get; set; }

    /// <summary>개수 (읽기/쓰기할 항목 수)</summary>
    public ushort Quantity { get; set; }

    /// <summary>쓰기 데이터 (쓰기 요청시)</summary>
    public byte[]? Data { get; set; }

    /// <summary>바이트 카운트 (다중 쓰기 요청시)</summary>
    public byte ByteCount { get; set; }

    public static ModbusRequest Parse(byte[] buffer, int offset = 0)
    {
        var request = new ModbusRequest
        {
            FunctionCode = buffer[offset]
        };

        switch (request.FunctionCode)
        {
            // 읽기 요청: FC(1) + StartAddr(2) + Quantity(2)
            case ModbusConstants.FuncReadCoils:
            case ModbusConstants.FuncReadDiscreteInputs:
            case ModbusConstants.FuncReadHoldingRegisters:
            case ModbusConstants.FuncReadInputRegisters:
                request.StartAddress = (ushort)((buffer[offset + 1] << 8) | buffer[offset + 2]);
                request.Quantity = (ushort)((buffer[offset + 3] << 8) | buffer[offset + 4]);
                break;

            // 단일 쓰기: FC(1) + Address(2) + Value(2)
            case ModbusConstants.FuncWriteSingleCoil:
            case ModbusConstants.FuncWriteSingleRegister:
                request.StartAddress = (ushort)((buffer[offset + 1] << 8) | buffer[offset + 2]);
                request.Quantity = 1;
                request.Data = new byte[] { buffer[offset + 3], buffer[offset + 4] };
                break;

            // 다중 Coil 쓰기: FC(1) + StartAddr(2) + Quantity(2) + ByteCount(1) + Data(n)
            case ModbusConstants.FuncWriteMultipleCoils:
                request.StartAddress = (ushort)((buffer[offset + 1] << 8) | buffer[offset + 2]);
                request.Quantity = (ushort)((buffer[offset + 3] << 8) | buffer[offset + 4]);
                request.ByteCount = buffer[offset + 5];
                request.Data = new byte[request.ByteCount];
                Array.Copy(buffer, offset + 6, request.Data, 0, request.ByteCount);
                break;

            // 다중 Register 쓰기: FC(1) + StartAddr(2) + Quantity(2) + ByteCount(1) + Data(n)
            case ModbusConstants.FuncWriteMultipleRegisters:
                request.StartAddress = (ushort)((buffer[offset + 1] << 8) | buffer[offset + 2]);
                request.Quantity = (ushort)((buffer[offset + 3] << 8) | buffer[offset + 4]);
                request.ByteCount = buffer[offset + 5];
                request.Data = new byte[request.ByteCount];
                Array.Copy(buffer, offset + 6, request.Data, 0, request.ByteCount);
                break;
        }

        return request;
    }
}

/// <summary>
/// Modbus 응답 PDU
/// </summary>
public class ModbusResponse
{
    /// <summary>기능 코드 (에러시 0x80 | FC)</summary>
    public byte FunctionCode { get; set; }

    /// <summary>바이트 카운트 (읽기 응답)</summary>
    public byte ByteCount { get; set; }

    /// <summary>응답 데이터</summary>
    public byte[]? Data { get; set; }

    /// <summary>예외 코드 (에러 응답시)</summary>
    public byte ExceptionCode { get; set; }

    /// <summary>에러 여부</summary>
    public bool IsError => (FunctionCode & 0x80) != 0;

    /// <summary>읽기 응답 생성</summary>
    public static ModbusResponse CreateReadResponse(byte functionCode, byte[] data)
    {
        return new ModbusResponse
        {
            FunctionCode = functionCode,
            ByteCount = (byte)data.Length,
            Data = data
        };
    }

    /// <summary>쓰기 응답 생성 (에코)</summary>
    public static ModbusResponse CreateWriteResponse(byte functionCode, ushort address, ushort value)
    {
        return new ModbusResponse
        {
            FunctionCode = functionCode,
            Data = new byte[]
            {
                (byte)(address >> 8), (byte)(address & 0xFF),
                (byte)(value >> 8), (byte)(value & 0xFF)
            }
        };
    }

    /// <summary>에러 응답 생성</summary>
    public static ModbusResponse CreateExceptionResponse(byte functionCode, byte exceptionCode)
    {
        return new ModbusResponse
        {
            FunctionCode = (byte)(functionCode | 0x80),
            ExceptionCode = exceptionCode
        };
    }

    public byte[] ToBytes()
    {
        if (IsError)
        {
            return new byte[] { FunctionCode, ExceptionCode };
        }

        // 읽기 응답: FC(1) + ByteCount(1) + Data(n)
        if (FunctionCode is ModbusConstants.FuncReadCoils or
            ModbusConstants.FuncReadDiscreteInputs or
            ModbusConstants.FuncReadHoldingRegisters or
            ModbusConstants.FuncReadInputRegisters)
        {
            var result = new byte[2 + Data!.Length];
            result[0] = FunctionCode;
            result[1] = ByteCount;
            Data.CopyTo(result, 2);
            return result;
        }

        // 쓰기 응답: FC(1) + Data(4) - 주소와 값/개수 에코
        var response = new byte[1 + Data!.Length];
        response[0] = FunctionCode;
        Data.CopyTo(response, 1);
        return response;
    }
}
