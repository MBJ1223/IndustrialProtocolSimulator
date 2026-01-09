using System.Text;

namespace OpcUaServerSimulator.Protocol;

/// <summary>
/// OPC UA 바이너리 인코딩 헬퍼
/// </summary>
public class OpcUaBinaryEncoder
{
    private readonly List<byte> _buffer = new();

    public byte[] ToArray() => _buffer.ToArray();
    public int Length => _buffer.Count;

    public void WriteByte(byte value) => _buffer.Add(value);

    public void WriteBoolean(bool value) => _buffer.Add(value ? (byte)1 : (byte)0);

    public void WriteInt16(short value)
    {
        _buffer.Add((byte)(value & 0xFF));
        _buffer.Add((byte)((value >> 8) & 0xFF));
    }

    public void WriteUInt16(ushort value)
    {
        _buffer.Add((byte)(value & 0xFF));
        _buffer.Add((byte)((value >> 8) & 0xFF));
    }

    public void WriteInt32(int value)
    {
        _buffer.Add((byte)(value & 0xFF));
        _buffer.Add((byte)((value >> 8) & 0xFF));
        _buffer.Add((byte)((value >> 16) & 0xFF));
        _buffer.Add((byte)((value >> 24) & 0xFF));
    }

    public void WriteUInt32(uint value)
    {
        _buffer.Add((byte)(value & 0xFF));
        _buffer.Add((byte)((value >> 8) & 0xFF));
        _buffer.Add((byte)((value >> 16) & 0xFF));
        _buffer.Add((byte)((value >> 24) & 0xFF));
    }

    public void WriteInt64(long value)
    {
        for (int i = 0; i < 8; i++)
            _buffer.Add((byte)((value >> (i * 8)) & 0xFF));
    }

    public void WriteUInt64(ulong value)
    {
        for (int i = 0; i < 8; i++)
            _buffer.Add((byte)((value >> (i * 8)) & 0xFF));
    }

    public void WriteFloat(float value)
    {
        var bytes = BitConverter.GetBytes(value);
        _buffer.AddRange(bytes);
    }

    public void WriteDouble(double value)
    {
        var bytes = BitConverter.GetBytes(value);
        _buffer.AddRange(bytes);
    }

    public void WriteString(string? value)
    {
        if (value == null)
        {
            WriteInt32(-1);
            return;
        }
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteInt32(bytes.Length);
        _buffer.AddRange(bytes);
    }

    public void WriteByteString(byte[]? value)
    {
        if (value == null)
        {
            WriteInt32(-1);
            return;
        }
        WriteInt32(value.Length);
        _buffer.AddRange(value);
    }

    public void WriteDateTime(DateTime value)
    {
        // OPC UA DateTime: 100-nanosecond intervals since January 1, 1601
        long filetime = value.ToFileTimeUtc();
        WriteInt64(filetime);
    }

    public void WriteGuid(Guid value)
    {
        _buffer.AddRange(value.ToByteArray());
    }

    /// <summary>
    /// NodeId 인코딩
    /// </summary>
    public void WriteNodeId(string nodeId)
    {
        // 파싱: "ns=2;s=Name" 또는 "ns=0;i=85"
        ushort ns = 0;
        string identifier = nodeId;

        if (nodeId.StartsWith("ns="))
        {
            var parts = nodeId.Split(';');
            if (parts.Length >= 2)
            {
                ns = ushort.Parse(parts[0].Substring(3));
                identifier = parts[1];
            }
        }

        if (identifier.StartsWith("i="))
        {
            // Numeric NodeId
            uint numId = uint.Parse(identifier.Substring(2));
            if (ns == 0 && numId <= 255)
            {
                // Two-byte encoding
                WriteByte(0x00);
                WriteByte((byte)numId);
            }
            else if (ns <= 255 && numId <= 65535)
            {
                // Four-byte encoding
                WriteByte(0x01);
                WriteByte((byte)ns);
                WriteUInt16((ushort)numId);
            }
            else
            {
                // Numeric encoding
                WriteByte(0x02);
                WriteUInt16(ns);
                WriteUInt32(numId);
            }
        }
        else if (identifier.StartsWith("s="))
        {
            // String NodeId
            WriteByte(0x03);
            WriteUInt16(ns);
            WriteString(identifier.Substring(2));
        }
        else
        {
            // Default: String encoding
            WriteByte(0x03);
            WriteUInt16(ns);
            WriteString(identifier);
        }
    }

    /// <summary>
    /// ExpandedNodeId 인코딩
    /// </summary>
    public void WriteExpandedNodeId(string nodeId)
    {
        // 간단한 구현: NodeId와 동일하게 처리
        WriteNodeId(nodeId);
    }

    /// <summary>
    /// StatusCode 인코딩
    /// </summary>
    public void WriteStatusCode(uint statusCode)
    {
        WriteUInt32(statusCode);
    }

    /// <summary>
    /// Variant 인코딩
    /// </summary>
    public void WriteVariant(object? value, OpcUaDataType dataType)
    {
        if (value == null)
        {
            WriteByte(0);  // Null
            return;
        }

        WriteByte((byte)dataType);

        switch (dataType)
        {
            case OpcUaDataType.Boolean:
                WriteBoolean(Convert.ToBoolean(value));
                break;
            case OpcUaDataType.SByte:
                WriteByte((byte)Convert.ToSByte(value));
                break;
            case OpcUaDataType.Byte:
                WriteByte(Convert.ToByte(value));
                break;
            case OpcUaDataType.Int16:
                WriteInt16(Convert.ToInt16(value));
                break;
            case OpcUaDataType.UInt16:
                WriteUInt16(Convert.ToUInt16(value));
                break;
            case OpcUaDataType.Int32:
                WriteInt32(Convert.ToInt32(value));
                break;
            case OpcUaDataType.UInt32:
                WriteUInt32(Convert.ToUInt32(value));
                break;
            case OpcUaDataType.Int64:
                WriteInt64(Convert.ToInt64(value));
                break;
            case OpcUaDataType.UInt64:
                WriteUInt64(Convert.ToUInt64(value));
                break;
            case OpcUaDataType.Float:
                WriteFloat(Convert.ToSingle(value));
                break;
            case OpcUaDataType.Double:
                WriteDouble(Convert.ToDouble(value));
                break;
            case OpcUaDataType.String:
                WriteString(value.ToString());
                break;
            case OpcUaDataType.DateTime:
                WriteDateTime((DateTime)value);
                break;
            default:
                WriteString(value.ToString());
                break;
        }
    }

    /// <summary>
    /// DataValue 인코딩
    /// </summary>
    public void WriteDataValue(object? value, OpcUaDataType dataType, uint statusCode, DateTime timestamp)
    {
        // Encoding mask
        byte mask = 0x01;  // Value present
        if (statusCode != 0) mask |= 0x02;  // StatusCode present
        mask |= 0x04;  // SourceTimestamp present

        WriteByte(mask);
        WriteVariant(value, dataType);

        if ((mask & 0x02) != 0)
            WriteStatusCode(statusCode);

        if ((mask & 0x04) != 0)
            WriteDateTime(timestamp);
    }

    public void WriteBytes(byte[] data)
    {
        _buffer.AddRange(data);
    }
}

/// <summary>
/// OPC UA 바이너리 디코딩 헬퍼
/// </summary>
public class OpcUaBinaryDecoder
{
    private readonly byte[] _buffer;
    private int _position;

    public OpcUaBinaryDecoder(byte[] buffer, int offset = 0)
    {
        _buffer = buffer;
        _position = offset;
    }

    public int Position => _position;
    public int Remaining => _buffer.Length - _position;

    public byte ReadByte() => _buffer[_position++];

    public bool ReadBoolean() => ReadByte() != 0;

    public short ReadInt16()
    {
        var value = (short)(_buffer[_position] | (_buffer[_position + 1] << 8));
        _position += 2;
        return value;
    }

    public ushort ReadUInt16()
    {
        var value = (ushort)(_buffer[_position] | (_buffer[_position + 1] << 8));
        _position += 2;
        return value;
    }

    public int ReadInt32()
    {
        var value = _buffer[_position] |
                    (_buffer[_position + 1] << 8) |
                    (_buffer[_position + 2] << 16) |
                    (_buffer[_position + 3] << 24);
        _position += 4;
        return value;
    }

    public uint ReadUInt32()
    {
        var value = (uint)(_buffer[_position] |
                    (_buffer[_position + 1] << 8) |
                    (_buffer[_position + 2] << 16) |
                    (_buffer[_position + 3] << 24));
        _position += 4;
        return value;
    }

    public long ReadInt64()
    {
        long value = 0;
        for (int i = 0; i < 8; i++)
            value |= (long)_buffer[_position++] << (i * 8);
        return value;
    }

    public ulong ReadUInt64()
    {
        ulong value = 0;
        for (int i = 0; i < 8; i++)
            value |= (ulong)_buffer[_position++] << (i * 8);
        return value;
    }

    public float ReadFloat()
    {
        var value = BitConverter.ToSingle(_buffer, _position);
        _position += 4;
        return value;
    }

    public double ReadDouble()
    {
        var value = BitConverter.ToDouble(_buffer, _position);
        _position += 8;
        return value;
    }

    public string? ReadString()
    {
        int length = ReadInt32();
        if (length < 0) return null;
        if (length == 0) return "";

        var value = Encoding.UTF8.GetString(_buffer, _position, length);
        _position += length;
        return value;
    }

    public byte[]? ReadByteString()
    {
        int length = ReadInt32();
        if (length < 0) return null;
        if (length == 0) return Array.Empty<byte>();

        var value = new byte[length];
        Array.Copy(_buffer, _position, value, 0, length);
        _position += length;
        return value;
    }

    public DateTime ReadDateTime()
    {
        long filetime = ReadInt64();
        if (filetime <= 0) return DateTime.MinValue;
        try
        {
            return DateTime.FromFileTimeUtc(filetime);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    public Guid ReadGuid()
    {
        var bytes = new byte[16];
        Array.Copy(_buffer, _position, bytes, 0, 16);
        _position += 16;
        return new Guid(bytes);
    }

    /// <summary>
    /// NodeId 디코딩
    /// </summary>
    public string ReadNodeId()
    {
        byte encoding = ReadByte();

        switch (encoding & 0x3F)
        {
            case 0x00:  // Two-byte
                return $"ns=0;i={ReadByte()}";

            case 0x01:  // Four-byte
                {
                    byte ns = ReadByte();
                    ushort id = ReadUInt16();
                    return $"ns={ns};i={id}";
                }

            case 0x02:  // Numeric
                {
                    ushort ns = ReadUInt16();
                    uint id = ReadUInt32();
                    return $"ns={ns};i={id}";
                }

            case 0x03:  // String
                {
                    ushort ns = ReadUInt16();
                    string? str = ReadString();
                    return $"ns={ns};s={str}";
                }

            case 0x04:  // Guid
                {
                    ushort ns = ReadUInt16();
                    Guid guid = ReadGuid();
                    return $"ns={ns};g={guid}";
                }

            case 0x05:  // ByteString
                {
                    ushort ns = ReadUInt16();
                    byte[]? bytes = ReadByteString();
                    return $"ns={ns};b={Convert.ToBase64String(bytes ?? Array.Empty<byte>())}";
                }

            default:
                return "ns=0;i=0";
        }
    }

    public void Skip(int count)
    {
        _position += count;
    }
}
