namespace S7ProtocolSimulator.Protocol;

/// <summary>
/// S7 프로토콜 상수 정의
/// </summary>
public static class S7Constants
{
    // TPKT Header
    public const byte TpktVersion = 0x03;
    public const byte TpktReserved = 0x00;

    // COTP
    public const byte CotpConnectionRequest = 0xE0;
    public const byte CotpConnectionConfirm = 0xD0;
    public const byte CotpData = 0xF0;

    // S7 Protocol ID
    public const byte ProtocolId = 0x32;

    // Message Types
    public const byte MsgTypeJobRequest = 0x01;
    public const byte MsgTypeAck = 0x02;
    public const byte MsgTypeAckData = 0x03;
    public const byte MsgTypeUserData = 0x07;

    // Function Codes
    public const byte FuncReadVar = 0x04;
    public const byte FuncWriteVar = 0x05;
    public const byte FuncSetupComm = 0xF0;

    // Area Codes
    public const byte AreaSysInfo = 0x03;
    public const byte AreaSysFlags = 0x05;
    public const byte AreaAnaIn = 0x06;
    public const byte AreaAnaOut = 0x07;
    public const byte AreaCounter = 0x1C;
    public const byte AreaTimer = 0x1D;
    public const byte AreaInput = 0x81;    // I (Inputs)
    public const byte AreaOutput = 0x82;   // Q (Outputs)
    public const byte AreaFlags = 0x83;    // M (Merkers/Flags)
    public const byte AreaDB = 0x84;       // DB (Data Blocks)

    // Transport Size
    public const byte TransportSizeBit = 0x01;
    public const byte TransportSizeByte = 0x02;
    public const byte TransportSizeWord = 0x04;
    public const byte TransportSizeDWord = 0x06;
    public const byte TransportSizeReal = 0x08;

    // Return Codes
    public const byte ReturnCodeSuccess = 0xFF;
    public const byte ReturnCodeHardwareError = 0x01;
    public const byte ReturnCodeAccessError = 0x03;
    public const byte ReturnCodeAddressError = 0x05;
    public const byte ReturnCodeDataTypeError = 0x06;
    public const byte ReturnCodeNotFound = 0x0A;

    // Default Settings
    public const int DefaultPort = 102;
    public const int DefaultRack = 0;
    public const int DefaultSlot = 1;
}

/// <summary>
/// S7 영역 타입
/// </summary>
public enum S7AreaType
{
    Input,      // I
    Output,     // Q
    Merker,     // M (Flags)
    DataBlock,  // DB
    Counter,    // C
    Timer       // T
}
