namespace MqttBrokerSimulator.Protocol;

/// <summary>
/// MQTT 프로토콜 상수
/// </summary>
public static class MqttConstants
{
    // Control Packet Types
    public const byte CONNECT = 0x10;
    public const byte CONNACK = 0x20;
    public const byte PUBLISH = 0x30;
    public const byte PUBACK = 0x40;
    public const byte PUBREC = 0x50;
    public const byte PUBREL = 0x60;
    public const byte PUBCOMP = 0x70;
    public const byte SUBSCRIBE = 0x80;
    public const byte SUBACK = 0x90;
    public const byte UNSUBSCRIBE = 0xA0;
    public const byte UNSUBACK = 0xB0;
    public const byte PINGREQ = 0xC0;
    public const byte PINGRESP = 0xD0;
    public const byte DISCONNECT = 0xE0;

    // CONNACK Return Codes
    public const byte CONNACK_ACCEPTED = 0x00;
    public const byte CONNACK_REFUSED_PROTOCOL = 0x01;
    public const byte CONNACK_REFUSED_IDENTIFIER = 0x02;
    public const byte CONNACK_REFUSED_SERVER = 0x03;
    public const byte CONNACK_REFUSED_CREDENTIALS = 0x04;
    public const byte CONNACK_REFUSED_NOT_AUTHORIZED = 0x05;

    // QoS Levels
    public const byte QOS_0 = 0x00;  // At most once
    public const byte QOS_1 = 0x01;  // At least once
    public const byte QOS_2 = 0x02;  // Exactly once

    // Default Settings
    public const int DefaultPort = 1883;
    public const string ProtocolName = "MQTT";
    public const byte ProtocolLevel = 0x04;  // MQTT 3.1.1
}

/// <summary>
/// MQTT 패킷 타입
/// </summary>
public enum MqttPacketType : byte
{
    Connect = 1,
    Connack = 2,
    Publish = 3,
    Puback = 4,
    Pubrec = 5,
    Pubrel = 6,
    Pubcomp = 7,
    Subscribe = 8,
    Suback = 9,
    Unsubscribe = 10,
    Unsuback = 11,
    Pingreq = 12,
    Pingresp = 13,
    Disconnect = 14
}

/// <summary>
/// QoS 레벨
/// </summary>
public enum QosLevel : byte
{
    AtMostOnce = 0,
    AtLeastOnce = 1,
    ExactlyOnce = 2
}
