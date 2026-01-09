namespace OpcUaServerSimulator.Protocol;

/// <summary>
/// OPC UA 상수 정의
/// </summary>
public static class OpcUaConstants
{
    // 메시지 타입
    public const string MessageTypeHello = "HEL";
    public const string MessageTypeAck = "ACK";
    public const string MessageTypeError = "ERR";
    public const string MessageTypeOpen = "OPN";
    public const string MessageTypeClose = "CLO";
    public const string MessageTypeMessage = "MSG";

    // 서비스 ID
    public const uint ServiceIdGetEndpoints = 428;
    public const uint ServiceIdCreateSession = 461;
    public const uint ServiceIdActivateSession = 467;
    public const uint ServiceIdCloseSession = 473;
    public const uint ServiceIdBrowse = 527;
    public const uint ServiceIdRead = 631;
    public const uint ServiceIdWrite = 673;
    public const uint ServiceIdCreateSubscription = 787;
    public const uint ServiceIdPublish = 826;

    // 노드 클래스
    public const uint NodeClassObject = 1;
    public const uint NodeClassVariable = 2;
    public const uint NodeClassMethod = 4;
    public const uint NodeClassObjectType = 8;
    public const uint NodeClassVariableType = 16;
    public const uint NodeClassReferenceType = 32;
    public const uint NodeClassDataType = 64;
    public const uint NodeClassView = 128;

    // 데이터 타입
    public const uint DataTypeBoolean = 1;
    public const uint DataTypeSByte = 2;
    public const uint DataTypeByte = 3;
    public const uint DataTypeInt16 = 4;
    public const uint DataTypeUInt16 = 5;
    public const uint DataTypeInt32 = 6;
    public const uint DataTypeUInt32 = 7;
    public const uint DataTypeInt64 = 8;
    public const uint DataTypeUInt64 = 9;
    public const uint DataTypeFloat = 10;
    public const uint DataTypeDouble = 11;
    public const uint DataTypeString = 12;
    public const uint DataTypeDateTime = 13;

    // 기본 설정
    public const int DefaultPort = 4840;
    public const string DefaultEndpoint = "opc.tcp://localhost:4840";

    // 상태 코드
    public const uint StatusCodeGood = 0x00000000;
    public const uint StatusCodeBadNodeIdUnknown = 0x80340000;
    public const uint StatusCodeBadAttributeIdInvalid = 0x80350000;
    public const uint StatusCodeBadNotWritable = 0x803B0000;
}

/// <summary>
/// OPC UA 데이터 타입
/// </summary>
public enum OpcUaDataType
{
    Boolean = 1,
    SByte = 2,
    Byte = 3,
    Int16 = 4,
    UInt16 = 5,
    Int32 = 6,
    UInt32 = 7,
    Int64 = 8,
    UInt64 = 9,
    Float = 10,
    Double = 11,
    String = 12,
    DateTime = 13
}

/// <summary>
/// 노드 클래스
/// </summary>
public enum OpcUaNodeClass
{
    Object = 1,
    Variable = 2,
    Method = 4,
    ObjectType = 8,
    VariableType = 16,
    ReferenceType = 32,
    DataType = 64,
    View = 128
}
