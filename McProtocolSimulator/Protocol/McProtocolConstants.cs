namespace McProtocolSimulator.Protocol;

/// <summary>
/// MC 프로토콜 상수 정의
/// </summary>
public static class McProtocolConstants
{
    // 서브헤더 (3E 프레임)
    public const ushort SubheaderBinaryRequest = 0x5000;
    public const ushort SubheaderBinaryResponse = 0xD000;
    public const string SubheaderAsciiRequest = "5000";
    public const string SubheaderAsciiResponse = "D000";

    // 명령 코드
    public const ushort CommandBatchRead = 0x0401;
    public const ushort CommandBatchWrite = 0x1401;
    public const ushort CommandRandomRead = 0x0403;
    public const ushort CommandRandomWrite = 0x1402;

    // 서브 명령 (워드 단위 / 비트 단위)
    public const ushort SubCommandWord = 0x0000;
    public const ushort SubCommandBit = 0x0001;

    // 디바이스 코드 (Binary)
    public static class DeviceCodeBinary
    {
        public const byte D = 0xA8;  // 데이터 레지스터
        public const byte M = 0x90;  // 내부 릴레이
        public const byte X = 0x9C;  // 입력
        public const byte Y = 0x9D;  // 출력
        public const byte W = 0xB4;  // 링크 레지스터
        public const byte B = 0xA0;  // 링크 릴레이
        public const byte R = 0xAF;  // 파일 레지스터
        public const byte L = 0x92;  // 래치 릴레이
        public const byte F = 0x93;  // 아나운서
        public const byte V = 0x94;  // 에지 릴레이
        public const byte S = 0x98;  // 스텝 릴레이
        public const byte SM = 0x91; // 특수 릴레이
        public const byte SD = 0xA9; // 특수 레지스터
    }

    // 디바이스 코드 (ASCII)
    public static class DeviceCodeAscii
    {
        public const string D = "D*";   // 데이터 레지스터
        public const string M = "M*";   // 내부 릴레이
        public const string X = "X*";   // 입력
        public const string Y = "Y*";   // 출력
        public const string W = "W*";   // 링크 레지스터
        public const string B = "B*";   // 링크 릴레이
        public const string R = "R*";   // 파일 레지스터
        public const string L = "L*";   // 래치 릴레이
        public const string F = "F*";   // 아나운서
        public const string SM = "SM";  // 특수 릴레이
        public const string SD = "SD";  // 특수 레지스터
    }

    // 에러 코드
    public static class ErrorCode
    {
        public const ushort Success = 0x0000;
        public const ushort DeviceError = 0xC059;
        public const ushort AddressError = 0xC05B;
        public const ushort CommandError = 0xC061;
        public const ushort DataLengthError = 0xC050;
    }
}

/// <summary>
/// 디바이스 타입 열거형
/// </summary>
public enum DeviceType
{
    D,  // 데이터 레지스터 (Word)
    M,  // 내부 릴레이 (Bit)
    X,  // 입력 (Bit)
    Y,  // 출력 (Bit)
    W,  // 링크 레지스터 (Word)
    B,  // 링크 릴레이 (Bit)
    R,  // 파일 레지스터 (Word)
    L,  // 래치 릴레이 (Bit)
    Unknown
}

/// <summary>
/// 프로토콜 형식 열거형
/// </summary>
public enum ProtocolFormat
{
    Binary,
    Ascii
}
