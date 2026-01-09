using System.Text;

namespace MqttBrokerSimulator.Protocol;

/// <summary>
/// MQTT 패킷 파서
/// </summary>
public static class MqttPacketParser
{
    public static MqttPacketType GetPacketType(byte header) => (MqttPacketType)((header & 0xF0) >> 4);

    public static int DecodeRemainingLength(byte[] buffer, int offset, out int bytesUsed)
    {
        int multiplier = 1;
        int value = 0;
        bytesUsed = 0;

        byte encodedByte;
        do
        {
            encodedByte = buffer[offset + bytesUsed];
            value += (encodedByte & 127) * multiplier;
            multiplier *= 128;
            bytesUsed++;
        } while ((encodedByte & 128) != 0 && bytesUsed < 4);

        return value;
    }

    public static byte[] EncodeRemainingLength(int length)
    {
        var bytes = new List<byte>();
        do
        {
            byte encodedByte = (byte)(length % 128);
            length /= 128;
            if (length > 0) encodedByte |= 128;
            bytes.Add(encodedByte);
        } while (length > 0);

        return bytes.ToArray();
    }

    public static string ReadString(byte[] buffer, ref int offset)
    {
        int length = (buffer[offset] << 8) | buffer[offset + 1];
        offset += 2;
        string result = Encoding.UTF8.GetString(buffer, offset, length);
        offset += length;
        return result;
    }

    public static byte[] WriteString(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var result = new byte[2 + bytes.Length];
        result[0] = (byte)(bytes.Length >> 8);
        result[1] = (byte)(bytes.Length & 0xFF);
        bytes.CopyTo(result, 2);
        return result;
    }
}

/// <summary>
/// CONNECT 패킷
/// </summary>
public class ConnectPacket
{
    public string ProtocolName { get; set; } = "MQTT";
    public byte ProtocolLevel { get; set; } = 4;
    public byte ConnectFlags { get; set; }
    public ushort KeepAlive { get; set; }
    public string ClientId { get; set; } = "";
    public string? WillTopic { get; set; }
    public byte[]? WillMessage { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }

    public bool CleanSession => (ConnectFlags & 0x02) != 0;
    public bool HasWill => (ConnectFlags & 0x04) != 0;
    public QosLevel WillQos => (QosLevel)((ConnectFlags >> 3) & 0x03);
    public bool WillRetain => (ConnectFlags & 0x20) != 0;
    public bool HasPassword => (ConnectFlags & 0x40) != 0;
    public bool HasUsername => (ConnectFlags & 0x80) != 0;

    public static ConnectPacket? Parse(byte[] buffer)
    {
        try
        {
            int offset = 0;

            // Fixed header
            byte header = buffer[offset++];
            int remainingLength = MqttPacketParser.DecodeRemainingLength(buffer, offset, out int bytesUsed);
            offset += bytesUsed;

            var packet = new ConnectPacket();

            // Protocol Name
            packet.ProtocolName = MqttPacketParser.ReadString(buffer, ref offset);

            // Protocol Level
            packet.ProtocolLevel = buffer[offset++];

            // Connect Flags
            packet.ConnectFlags = buffer[offset++];

            // Keep Alive
            packet.KeepAlive = (ushort)((buffer[offset] << 8) | buffer[offset + 1]);
            offset += 2;

            // Payload
            packet.ClientId = MqttPacketParser.ReadString(buffer, ref offset);

            if (packet.HasWill)
            {
                packet.WillTopic = MqttPacketParser.ReadString(buffer, ref offset);
                int willMsgLen = (buffer[offset] << 8) | buffer[offset + 1];
                offset += 2;
                packet.WillMessage = new byte[willMsgLen];
                Array.Copy(buffer, offset, packet.WillMessage, 0, willMsgLen);
                offset += willMsgLen;
            }

            if (packet.HasUsername)
                packet.Username = MqttPacketParser.ReadString(buffer, ref offset);

            if (packet.HasPassword)
                packet.Password = MqttPacketParser.ReadString(buffer, ref offset);

            return packet;
        }
        catch { return null; }
    }
}

/// <summary>
/// PUBLISH 패킷
/// </summary>
public class PublishPacket
{
    public string Topic { get; set; } = "";
    public ushort PacketId { get; set; }
    public byte[] Payload { get; set; } = Array.Empty<byte>();
    public QosLevel Qos { get; set; }
    public bool Retain { get; set; }
    public bool Dup { get; set; }

    public static PublishPacket? Parse(byte[] buffer)
    {
        try
        {
            int offset = 0;

            byte header = buffer[offset++];
            var packet = new PublishPacket
            {
                Dup = (header & 0x08) != 0,
                Qos = (QosLevel)((header >> 1) & 0x03),
                Retain = (header & 0x01) != 0
            };

            int remainingLength = MqttPacketParser.DecodeRemainingLength(buffer, offset, out int bytesUsed);
            offset += bytesUsed;
            int endOffset = offset + remainingLength;

            // Topic
            packet.Topic = MqttPacketParser.ReadString(buffer, ref offset);

            // Packet ID (only for QoS > 0)
            if (packet.Qos > QosLevel.AtMostOnce)
            {
                packet.PacketId = (ushort)((buffer[offset] << 8) | buffer[offset + 1]);
                offset += 2;
            }

            // Payload
            int payloadLength = endOffset - offset;
            if (payloadLength > 0)
            {
                packet.Payload = new byte[payloadLength];
                Array.Copy(buffer, offset, packet.Payload, 0, payloadLength);
            }

            return packet;
        }
        catch { return null; }
    }

    public byte[] ToBytes()
    {
        var payload = new List<byte>();

        // Topic
        payload.AddRange(MqttPacketParser.WriteString(Topic));

        // Packet ID
        if (Qos > QosLevel.AtMostOnce)
        {
            payload.Add((byte)(PacketId >> 8));
            payload.Add((byte)(PacketId & 0xFF));
        }

        // Payload
        payload.AddRange(Payload);

        // Fixed header
        byte header = (byte)(MqttConstants.PUBLISH | ((byte)Qos << 1));
        if (Retain) header |= 0x01;
        if (Dup) header |= 0x08;

        var result = new List<byte> { header };
        result.AddRange(MqttPacketParser.EncodeRemainingLength(payload.Count));
        result.AddRange(payload);

        return result.ToArray();
    }
}

/// <summary>
/// SUBSCRIBE 패킷
/// </summary>
public class SubscribePacket
{
    public ushort PacketId { get; set; }
    public List<(string Topic, QosLevel Qos)> Subscriptions { get; } = new();

    public static SubscribePacket? Parse(byte[] buffer)
    {
        try
        {
            int offset = 0;

            byte header = buffer[offset++];
            int remainingLength = MqttPacketParser.DecodeRemainingLength(buffer, offset, out int bytesUsed);
            offset += bytesUsed;
            int endOffset = offset + remainingLength;

            var packet = new SubscribePacket
            {
                PacketId = (ushort)((buffer[offset] << 8) | buffer[offset + 1])
            };
            offset += 2;

            while (offset < endOffset)
            {
                string topic = MqttPacketParser.ReadString(buffer, ref offset);
                var qos = (QosLevel)buffer[offset++];
                packet.Subscriptions.Add((topic, qos));
            }

            return packet;
        }
        catch { return null; }
    }
}

/// <summary>
/// UNSUBSCRIBE 패킷
/// </summary>
public class UnsubscribePacket
{
    public ushort PacketId { get; set; }
    public List<string> Topics { get; } = new();

    public static UnsubscribePacket? Parse(byte[] buffer)
    {
        try
        {
            int offset = 0;

            byte header = buffer[offset++];
            int remainingLength = MqttPacketParser.DecodeRemainingLength(buffer, offset, out int bytesUsed);
            offset += bytesUsed;
            int endOffset = offset + remainingLength;

            var packet = new UnsubscribePacket
            {
                PacketId = (ushort)((buffer[offset] << 8) | buffer[offset + 1])
            };
            offset += 2;

            while (offset < endOffset)
            {
                packet.Topics.Add(MqttPacketParser.ReadString(buffer, ref offset));
            }

            return packet;
        }
        catch { return null; }
    }
}
