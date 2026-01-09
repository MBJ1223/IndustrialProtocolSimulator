namespace ModbusProtocolSimulator.Protocol;

/// <summary>
/// Modbus 프로토콜 상수
/// </summary>
public static class ModbusConstants
{
    // Modbus TCP 기본 포트
    public const int DefaultPort = 502;

    // MBAP 헤더 크기
    public const int MbapHeaderSize = 7;

    // 프로토콜 식별자 (Modbus TCP는 항상 0)
    public const ushort ProtocolId = 0x0000;

    #region Function Codes (기능 코드)

    // 비트 읽기
    public const byte FuncReadCoils = 0x01;              // Coils 읽기 (DO)
    public const byte FuncReadDiscreteInputs = 0x02;     // Discrete Inputs 읽기 (DI)

    // 레지스터 읽기
    public const byte FuncReadHoldingRegisters = 0x03;   // Holding Registers 읽기 (AO)
    public const byte FuncReadInputRegisters = 0x04;     // Input Registers 읽기 (AI)

    // 단일 쓰기
    public const byte FuncWriteSingleCoil = 0x05;        // 단일 Coil 쓰기
    public const byte FuncWriteSingleRegister = 0x06;    // 단일 Holding Register 쓰기

    // 다중 쓰기
    public const byte FuncWriteMultipleCoils = 0x0F;     // 다중 Coils 쓰기
    public const byte FuncWriteMultipleRegisters = 0x10; // 다중 Holding Registers 쓰기

    // 진단
    public const byte FuncReadExceptionStatus = 0x07;
    public const byte FuncDiagnostic = 0x08;
    public const byte FuncGetCommEventCounter = 0x0B;
    public const byte FuncGetCommEventLog = 0x0C;
    public const byte FuncReportSlaveId = 0x11;

    // 파일/레코드
    public const byte FuncReadFileRecord = 0x14;
    public const byte FuncWriteFileRecord = 0x15;

    // 마스크 쓰기
    public const byte FuncMaskWriteRegister = 0x16;
    public const byte FuncReadWriteMultipleRegisters = 0x17;

    #endregion

    #region Exception Codes (예외 코드)

    public const byte ExceptionIllegalFunction = 0x01;     // 잘못된 기능 코드
    public const byte ExceptionIllegalDataAddress = 0x02;  // 잘못된 데이터 주소
    public const byte ExceptionIllegalDataValue = 0x03;    // 잘못된 데이터 값
    public const byte ExceptionSlaveDeviceFailure = 0x04;  // 슬레이브 장치 오류
    public const byte ExceptionAcknowledge = 0x05;         // 확인 (처리 중)
    public const byte ExceptionSlaveDeviceBusy = 0x06;     // 슬레이브 장치 바쁨
    public const byte ExceptionMemoryParityError = 0x08;   // 메모리 패리티 오류
    public const byte ExceptionGatewayPathUnavailable = 0x0A;
    public const byte ExceptionGatewayTargetFailed = 0x0B;

    #endregion

    #region Coil Values

    public const ushort CoilOn = 0xFF00;
    public const ushort CoilOff = 0x0000;

    #endregion

    #region Memory Limits

    public const int MaxCoils = 65536;             // 0x0000 ~ 0xFFFF
    public const int MaxDiscreteInputs = 65536;
    public const int MaxHoldingRegisters = 65536;
    public const int MaxInputRegisters = 65536;

    public const int MaxReadCoilsCount = 2000;     // 최대 읽기 개수
    public const int MaxReadRegistersCount = 125;
    public const int MaxWriteCoilsCount = 1968;    // 최대 쓰기 개수
    public const int MaxWriteRegistersCount = 123;

    #endregion
}

/// <summary>
/// Modbus 메모리 영역 타입
/// </summary>
public enum ModbusAreaType
{
    Coils,              // DO - Discrete Output (읽기/쓰기)
    DiscreteInputs,     // DI - Discrete Input (읽기 전용)
    HoldingRegisters,   // AO - Analog Output (읽기/쓰기)
    InputRegisters      // AI - Analog Input (읽기 전용)
}
