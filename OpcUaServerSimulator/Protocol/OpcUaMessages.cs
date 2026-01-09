using System.Text;

namespace OpcUaServerSimulator.Protocol;

/// <summary>
/// OPC UA 메시지 헤더
/// </summary>
public class OpcUaMessageHeader
{
    public string MessageType { get; set; } = "";
    public byte ChunkType { get; set; }
    public uint MessageSize { get; set; }

    public static OpcUaMessageHeader Parse(byte[] buffer)
    {
        return new OpcUaMessageHeader
        {
            MessageType = Encoding.ASCII.GetString(buffer, 0, 3),
            ChunkType = buffer[3],
            MessageSize = BitConverter.ToUInt32(buffer, 4)
        };
    }

    public byte[] ToBytes()
    {
        var result = new byte[8];
        Encoding.ASCII.GetBytes(MessageType).CopyTo(result, 0);
        result[3] = ChunkType;
        BitConverter.GetBytes(MessageSize).CopyTo(result, 4);
        return result;
    }
}

/// <summary>
/// Hello 메시지
/// </summary>
public class HelloMessage
{
    public uint ProtocolVersion { get; set; }
    public uint ReceiveBufferSize { get; set; }
    public uint SendBufferSize { get; set; }
    public uint MaxMessageSize { get; set; }
    public uint MaxChunkCount { get; set; }
    public string? EndpointUrl { get; set; }

    public static HelloMessage Parse(byte[] buffer, int offset)
    {
        var decoder = new OpcUaBinaryDecoder(buffer, offset);
        return new HelloMessage
        {
            ProtocolVersion = decoder.ReadUInt32(),
            ReceiveBufferSize = decoder.ReadUInt32(),
            SendBufferSize = decoder.ReadUInt32(),
            MaxMessageSize = decoder.ReadUInt32(),
            MaxChunkCount = decoder.ReadUInt32(),
            EndpointUrl = decoder.ReadString()
        };
    }
}

/// <summary>
/// Acknowledge 메시지
/// </summary>
public class AcknowledgeMessage
{
    public uint ProtocolVersion { get; set; } = 0;
    public uint ReceiveBufferSize { get; set; } = 65536;
    public uint SendBufferSize { get; set; } = 65536;
    public uint MaxMessageSize { get; set; } = 16777216;
    public uint MaxChunkCount { get; set; } = 0;

    public byte[] ToBytes()
    {
        var encoder = new OpcUaBinaryEncoder();

        // Header
        encoder.WriteBytes(Encoding.ASCII.GetBytes("ACK"));
        encoder.WriteByte((byte)'F');
        encoder.WriteUInt32(28);  // Message size

        // Body
        encoder.WriteUInt32(ProtocolVersion);
        encoder.WriteUInt32(ReceiveBufferSize);
        encoder.WriteUInt32(SendBufferSize);
        encoder.WriteUInt32(MaxMessageSize);
        encoder.WriteUInt32(MaxChunkCount);

        return encoder.ToArray();
    }
}

/// <summary>
/// OpenSecureChannel 요청
/// </summary>
public class OpenSecureChannelRequest
{
    public uint SecureChannelId { get; set; }
    public string? SecurityPolicyUri { get; set; }
    public byte[]? SenderCertificate { get; set; }
    public byte[]? ReceiverCertificateThumbprint { get; set; }
    public uint SequenceNumber { get; set; }
    public uint RequestId { get; set; }
    public uint RequestType { get; set; }
    public uint SecurityMode { get; set; }
    public byte[]? ClientNonce { get; set; }
    public uint RequestedLifetime { get; set; }

    public static OpenSecureChannelRequest Parse(byte[] buffer, int offset)
    {
        var request = new OpenSecureChannelRequest();
        var decoder = new OpcUaBinaryDecoder(buffer, offset);

        // Security Header
        request.SecureChannelId = decoder.ReadUInt32();
        request.SecurityPolicyUri = decoder.ReadString();
        request.SenderCertificate = decoder.ReadByteString();
        request.ReceiverCertificateThumbprint = decoder.ReadByteString();

        // Sequence Header
        request.SequenceNumber = decoder.ReadUInt32();
        request.RequestId = decoder.ReadUInt32();

        // Request Body (ExtensionObject)
        decoder.ReadNodeId();  // TypeId
        decoder.ReadByte();    // Encoding

        // OpenSecureChannelRequest
        decoder.ReadNodeId();      // RequestHeader.AuthenticationToken
        decoder.ReadDateTime();    // RequestHeader.Timestamp
        decoder.ReadUInt32();      // RequestHeader.RequestHandle
        decoder.ReadUInt32();      // RequestHeader.ReturnDiagnostics
        decoder.ReadString();      // RequestHeader.AuditEntryId
        decoder.ReadUInt32();      // RequestHeader.TimeoutHint
        decoder.ReadByte();        // RequestHeader.AdditionalHeader (null)

        decoder.ReadUInt32();      // ClientProtocolVersion
        request.RequestType = decoder.ReadUInt32();
        request.SecurityMode = decoder.ReadUInt32();
        request.ClientNonce = decoder.ReadByteString();
        request.RequestedLifetime = decoder.ReadUInt32();

        return request;
    }
}

/// <summary>
/// 서비스 요청 헤더
/// </summary>
public class ServiceRequestHeader
{
    public string AuthenticationToken { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public uint RequestHandle { get; set; }
    public uint ReturnDiagnostics { get; set; }
    public string? AuditEntryId { get; set; }
    public uint TimeoutHint { get; set; }

    public static ServiceRequestHeader Parse(OpcUaBinaryDecoder decoder)
    {
        return new ServiceRequestHeader
        {
            AuthenticationToken = decoder.ReadNodeId(),
            Timestamp = decoder.ReadDateTime(),
            RequestHandle = decoder.ReadUInt32(),
            ReturnDiagnostics = decoder.ReadUInt32(),
            AuditEntryId = decoder.ReadString(),
            TimeoutHint = decoder.ReadUInt32()
        };
    }

    public void Write(OpcUaBinaryEncoder encoder)
    {
        encoder.WriteNodeId(AuthenticationToken);
        encoder.WriteDateTime(Timestamp);
        encoder.WriteUInt32(RequestHandle);
        encoder.WriteUInt32(ReturnDiagnostics);
        encoder.WriteString(AuditEntryId);
        encoder.WriteUInt32(TimeoutHint);
        encoder.WriteByte(0);  // AdditionalHeader (null)
    }
}

/// <summary>
/// 서비스 응답 헤더
/// </summary>
public class ServiceResponseHeader
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public uint RequestHandle { get; set; }
    public uint ServiceResult { get; set; } = OpcUaConstants.StatusCodeGood;

    public void Write(OpcUaBinaryEncoder encoder)
    {
        encoder.WriteDateTime(Timestamp);
        encoder.WriteUInt32(RequestHandle);
        encoder.WriteStatusCode(ServiceResult);
        encoder.WriteByte(0);  // DiagnosticInfo (null)
        encoder.WriteInt32(-1);  // StringTable (null array)
        encoder.WriteByte(0);  // AdditionalHeader (null)
    }
}

/// <summary>
/// Read 요청 파라미터
/// </summary>
public class ReadValueId
{
    public string NodeId { get; set; } = "";
    public uint AttributeId { get; set; }
    public string? IndexRange { get; set; }
    public string? DataEncoding { get; set; }

    public static ReadValueId Parse(OpcUaBinaryDecoder decoder)
    {
        return new ReadValueId
        {
            NodeId = decoder.ReadNodeId(),
            AttributeId = decoder.ReadUInt32(),
            IndexRange = decoder.ReadString(),
            DataEncoding = decoder.ReadNodeId()
        };
    }
}

/// <summary>
/// Write 요청 파라미터
/// </summary>
public class WriteValue
{
    public string NodeId { get; set; } = "";
    public uint AttributeId { get; set; }
    public string? IndexRange { get; set; }
    public object? Value { get; set; }
    public OpcUaDataType DataType { get; set; }

    public static WriteValue Parse(OpcUaBinaryDecoder decoder)
    {
        var wv = new WriteValue
        {
            NodeId = decoder.ReadNodeId(),
            AttributeId = decoder.ReadUInt32(),
            IndexRange = decoder.ReadString()
        };

        // DataValue
        byte encoding = decoder.ReadByte();
        if ((encoding & 0x01) != 0)
        {
            // Variant
            byte typeId = decoder.ReadByte();
            wv.DataType = (OpcUaDataType)typeId;
            wv.Value = ReadVariantValue(decoder, wv.DataType);
        }

        return wv;
    }

    private static object? ReadVariantValue(OpcUaBinaryDecoder decoder, OpcUaDataType dataType)
    {
        return dataType switch
        {
            OpcUaDataType.Boolean => decoder.ReadBoolean(),
            OpcUaDataType.SByte => (sbyte)decoder.ReadByte(),
            OpcUaDataType.Byte => decoder.ReadByte(),
            OpcUaDataType.Int16 => decoder.ReadInt16(),
            OpcUaDataType.UInt16 => decoder.ReadUInt16(),
            OpcUaDataType.Int32 => decoder.ReadInt32(),
            OpcUaDataType.UInt32 => decoder.ReadUInt32(),
            OpcUaDataType.Int64 => decoder.ReadInt64(),
            OpcUaDataType.UInt64 => decoder.ReadUInt64(),
            OpcUaDataType.Float => decoder.ReadFloat(),
            OpcUaDataType.Double => decoder.ReadDouble(),
            OpcUaDataType.String => decoder.ReadString(),
            OpcUaDataType.DateTime => decoder.ReadDateTime(),
            _ => null
        };
    }
}

/// <summary>
/// Browse 요청 파라미터
/// </summary>
public class BrowseDescription
{
    public string NodeId { get; set; } = "";
    public uint BrowseDirection { get; set; }
    public string ReferenceTypeId { get; set; } = "";
    public bool IncludeSubtypes { get; set; }
    public uint NodeClassMask { get; set; }
    public uint ResultMask { get; set; }

    public static BrowseDescription Parse(OpcUaBinaryDecoder decoder)
    {
        return new BrowseDescription
        {
            NodeId = decoder.ReadNodeId(),
            BrowseDirection = decoder.ReadUInt32(),
            ReferenceTypeId = decoder.ReadNodeId(),
            IncludeSubtypes = decoder.ReadBoolean(),
            NodeClassMask = decoder.ReadUInt32(),
            ResultMask = decoder.ReadUInt32()
        };
    }
}
