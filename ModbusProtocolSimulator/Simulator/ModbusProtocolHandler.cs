using ModbusProtocolSimulator.Protocol;

namespace ModbusProtocolSimulator.Simulator;

/// <summary>
/// Modbus TCP 프로토콜 핸들러
/// </summary>
public class ModbusProtocolHandler
{
    private readonly ModbusMemory _memory;

    public ModbusMemory Memory => _memory;
    public byte UnitId { get; private set; }

    public event EventHandler<string>? LogMessage;

    public ModbusProtocolHandler(ModbusMemory memory)
    {
        _memory = memory;
    }

    /// <summary>
    /// 요청 처리
    /// </summary>
    public byte[]? ProcessRequest(byte[] buffer)
    {
        if (buffer.Length < ModbusConstants.MbapHeaderSize + 1) return null;

        // MBAP 헤더 파싱
        var mbap = MbapHeader.Parse(buffer);

        if (mbap.ProtocolId != ModbusConstants.ProtocolId)
        {
            Log($"잘못된 프로토콜 ID: 0x{mbap.ProtocolId:X4}");
            return null;
        }

        UnitId = mbap.UnitId;

        // PDU 파싱
        var request = ModbusRequest.Parse(buffer, ModbusConstants.MbapHeaderSize);

        Log($"요청: TransId={mbap.TransactionId}, UnitId={mbap.UnitId}, FC=0x{request.FunctionCode:X2}, Addr={request.StartAddress}, Qty={request.Quantity}");

        // 요청 처리
        var response = HandleRequest(request);

        // 응답 생성
        return CreateResponse(mbap, response);
    }

    private ModbusResponse HandleRequest(ModbusRequest request)
    {
        return request.FunctionCode switch
        {
            ModbusConstants.FuncReadCoils => HandleReadCoils(request),
            ModbusConstants.FuncReadDiscreteInputs => HandleReadDiscreteInputs(request),
            ModbusConstants.FuncReadHoldingRegisters => HandleReadHoldingRegisters(request),
            ModbusConstants.FuncReadInputRegisters => HandleReadInputRegisters(request),
            ModbusConstants.FuncWriteSingleCoil => HandleWriteSingleCoil(request),
            ModbusConstants.FuncWriteSingleRegister => HandleWriteSingleRegister(request),
            ModbusConstants.FuncWriteMultipleCoils => HandleWriteMultipleCoils(request),
            ModbusConstants.FuncWriteMultipleRegisters => HandleWriteMultipleRegisters(request),
            _ => ModbusResponse.CreateExceptionResponse(request.FunctionCode, ModbusConstants.ExceptionIllegalFunction)
        };
    }

    #region Read Handlers

    private ModbusResponse HandleReadCoils(ModbusRequest request)
    {
        // 검증
        if (request.Quantity == 0 || request.Quantity > ModbusConstants.MaxReadCoilsCount)
        {
            return ModbusResponse.CreateExceptionResponse(request.FunctionCode, ModbusConstants.ExceptionIllegalDataValue);
        }

        if (request.StartAddress + request.Quantity > ModbusMemory.CoilsSize)
        {
            return ModbusResponse.CreateExceptionResponse(request.FunctionCode, ModbusConstants.ExceptionIllegalDataAddress);
        }

        // Coils 읽기
        var coils = _memory.ReadCoils(request.StartAddress, request.Quantity);
        var data = PackBits(coils);

        Log($"  Read Coils: Addr={request.StartAddress}, Count={request.Quantity}, Bytes={data.Length}");

        return ModbusResponse.CreateReadResponse(request.FunctionCode, data);
    }

    private ModbusResponse HandleReadDiscreteInputs(ModbusRequest request)
    {
        if (request.Quantity == 0 || request.Quantity > ModbusConstants.MaxReadCoilsCount)
        {
            return ModbusResponse.CreateExceptionResponse(request.FunctionCode, ModbusConstants.ExceptionIllegalDataValue);
        }

        if (request.StartAddress + request.Quantity > ModbusMemory.DiscreteInputsSize)
        {
            return ModbusResponse.CreateExceptionResponse(request.FunctionCode, ModbusConstants.ExceptionIllegalDataAddress);
        }

        var inputs = _memory.ReadDiscreteInputs(request.StartAddress, request.Quantity);
        var data = PackBits(inputs);

        Log($"  Read Discrete Inputs: Addr={request.StartAddress}, Count={request.Quantity}");

        return ModbusResponse.CreateReadResponse(request.FunctionCode, data);
    }

    private ModbusResponse HandleReadHoldingRegisters(ModbusRequest request)
    {
        if (request.Quantity == 0 || request.Quantity > ModbusConstants.MaxReadRegistersCount)
        {
            return ModbusResponse.CreateExceptionResponse(request.FunctionCode, ModbusConstants.ExceptionIllegalDataValue);
        }

        if (request.StartAddress + request.Quantity > ModbusMemory.HoldingRegistersSize)
        {
            return ModbusResponse.CreateExceptionResponse(request.FunctionCode, ModbusConstants.ExceptionIllegalDataAddress);
        }

        var registers = _memory.ReadHoldingRegisters(request.StartAddress, request.Quantity);
        var data = PackRegisters(registers);

        Log($"  Read Holding Registers: Addr={request.StartAddress}, Count={request.Quantity}");

        return ModbusResponse.CreateReadResponse(request.FunctionCode, data);
    }

    private ModbusResponse HandleReadInputRegisters(ModbusRequest request)
    {
        if (request.Quantity == 0 || request.Quantity > ModbusConstants.MaxReadRegistersCount)
        {
            return ModbusResponse.CreateExceptionResponse(request.FunctionCode, ModbusConstants.ExceptionIllegalDataValue);
        }

        if (request.StartAddress + request.Quantity > ModbusMemory.InputRegistersSize)
        {
            return ModbusResponse.CreateExceptionResponse(request.FunctionCode, ModbusConstants.ExceptionIllegalDataAddress);
        }

        var registers = _memory.ReadInputRegisters(request.StartAddress, request.Quantity);
        var data = PackRegisters(registers);

        Log($"  Read Input Registers: Addr={request.StartAddress}, Count={request.Quantity}");

        return ModbusResponse.CreateReadResponse(request.FunctionCode, data);
    }

    #endregion

    #region Write Handlers

    private ModbusResponse HandleWriteSingleCoil(ModbusRequest request)
    {
        if (request.StartAddress >= ModbusMemory.CoilsSize)
        {
            return ModbusResponse.CreateExceptionResponse(request.FunctionCode, ModbusConstants.ExceptionIllegalDataAddress);
        }

        // 값 검증: 0xFF00 = ON, 0x0000 = OFF
        ushort value = (ushort)((request.Data![0] << 8) | request.Data[1]);
        if (value != ModbusConstants.CoilOn && value != ModbusConstants.CoilOff)
        {
            return ModbusResponse.CreateExceptionResponse(request.FunctionCode, ModbusConstants.ExceptionIllegalDataValue);
        }

        bool coilValue = value == ModbusConstants.CoilOn;
        _memory.WriteSingleCoil(request.StartAddress, coilValue);

        Log($"  Write Single Coil: Addr={request.StartAddress}, Value={coilValue}");

        return ModbusResponse.CreateWriteResponse(request.FunctionCode, request.StartAddress, value);
    }

    private ModbusResponse HandleWriteSingleRegister(ModbusRequest request)
    {
        if (request.StartAddress >= ModbusMemory.HoldingRegistersSize)
        {
            return ModbusResponse.CreateExceptionResponse(request.FunctionCode, ModbusConstants.ExceptionIllegalDataAddress);
        }

        ushort value = (ushort)((request.Data![0] << 8) | request.Data[1]);
        _memory.WriteSingleRegister(request.StartAddress, value);

        Log($"  Write Single Register: Addr={request.StartAddress}, Value={value}");

        return ModbusResponse.CreateWriteResponse(request.FunctionCode, request.StartAddress, value);
    }

    private ModbusResponse HandleWriteMultipleCoils(ModbusRequest request)
    {
        if (request.Quantity == 0 || request.Quantity > ModbusConstants.MaxWriteCoilsCount)
        {
            return ModbusResponse.CreateExceptionResponse(request.FunctionCode, ModbusConstants.ExceptionIllegalDataValue);
        }

        if (request.StartAddress + request.Quantity > ModbusMemory.CoilsSize)
        {
            return ModbusResponse.CreateExceptionResponse(request.FunctionCode, ModbusConstants.ExceptionIllegalDataAddress);
        }

        // 비트 언패킹
        var coils = UnpackBits(request.Data!, request.Quantity);
        _memory.WriteMultipleCoils(request.StartAddress, coils);

        Log($"  Write Multiple Coils: Addr={request.StartAddress}, Count={request.Quantity}");

        return ModbusResponse.CreateWriteResponse(request.FunctionCode, request.StartAddress, request.Quantity);
    }

    private ModbusResponse HandleWriteMultipleRegisters(ModbusRequest request)
    {
        if (request.Quantity == 0 || request.Quantity > ModbusConstants.MaxWriteRegistersCount)
        {
            return ModbusResponse.CreateExceptionResponse(request.FunctionCode, ModbusConstants.ExceptionIllegalDataValue);
        }

        if (request.StartAddress + request.Quantity > ModbusMemory.HoldingRegistersSize)
        {
            return ModbusResponse.CreateExceptionResponse(request.FunctionCode, ModbusConstants.ExceptionIllegalDataAddress);
        }

        // 레지스터 언패킹
        var registers = UnpackRegisters(request.Data!, request.Quantity);
        _memory.WriteMultipleRegisters(request.StartAddress, registers);

        Log($"  Write Multiple Registers: Addr={request.StartAddress}, Count={request.Quantity}");

        return ModbusResponse.CreateWriteResponse(request.FunctionCode, request.StartAddress, request.Quantity);
    }

    #endregion

    #region Response Builder

    private byte[] CreateResponse(MbapHeader requestMbap, ModbusResponse response)
    {
        var pduBytes = response.ToBytes();

        var responseMbap = new MbapHeader
        {
            TransactionId = requestMbap.TransactionId,
            ProtocolId = ModbusConstants.ProtocolId,
            Length = (ushort)(1 + pduBytes.Length),  // UnitId + PDU
            UnitId = requestMbap.UnitId
        };

        var mbapBytes = responseMbap.ToBytes();
        var result = new byte[mbapBytes.Length + pduBytes.Length];

        mbapBytes.CopyTo(result, 0);
        pduBytes.CopyTo(result, mbapBytes.Length);

        Log($"응답: TransId={responseMbap.TransactionId}, Len={result.Length}, FC=0x{response.FunctionCode:X2}");

        return result;
    }

    #endregion

    #region Helper Methods

    /// <summary>비트 배열을 바이트 배열로 패킹</summary>
    private static byte[] PackBits(bool[] bits)
    {
        int byteCount = (bits.Length + 7) / 8;
        var result = new byte[byteCount];

        for (int i = 0; i < bits.Length; i++)
        {
            if (bits[i])
            {
                result[i / 8] |= (byte)(1 << (i % 8));
            }
        }

        return result;
    }

    /// <summary>바이트 배열을 비트 배열로 언패킹</summary>
    private static bool[] UnpackBits(byte[] bytes, int bitCount)
    {
        var result = new bool[bitCount];

        for (int i = 0; i < bitCount; i++)
        {
            result[i] = (bytes[i / 8] & (1 << (i % 8))) != 0;
        }

        return result;
    }

    /// <summary>레지스터 배열을 바이트 배열로 패킹 (Big Endian)</summary>
    private static byte[] PackRegisters(ushort[] registers)
    {
        var result = new byte[registers.Length * 2];

        for (int i = 0; i < registers.Length; i++)
        {
            result[i * 2] = (byte)(registers[i] >> 8);
            result[i * 2 + 1] = (byte)(registers[i] & 0xFF);
        }

        return result;
    }

    /// <summary>바이트 배열을 레지스터 배열로 언패킹 (Big Endian)</summary>
    private static ushort[] UnpackRegisters(byte[] bytes, int count)
    {
        var result = new ushort[count];

        for (int i = 0; i < count; i++)
        {
            result[i] = (ushort)((bytes[i * 2] << 8) | bytes[i * 2 + 1]);
        }

        return result;
    }

    private void Log(string message)
    {
        LogMessage?.Invoke(this, $"[{DateTime.Now:HH:mm:ss.fff}] {message}");
    }

    #endregion
}
