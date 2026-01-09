using OpcUaServerSimulator.Protocol;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Timers;
using Timer = System.Timers.Timer;

namespace OpcUaServerSimulator.Simulator;

/// <summary>
/// OPC UA 클라이언트 정보
/// </summary>
public class OpcUaClientInfo
{
    public string Id { get; }
    public TcpClient TcpClient { get; }
    public IPEndPoint? RemoteEndPoint { get; }
    public DateTime ConnectedAt { get; }
    public string? SessionId { get; set; }
    public bool IsActivated { get; set; }
    public long RequestCount { get; set; }
    public uint SecureChannelId { get; set; }
    public uint TokenId { get; set; }
    public uint SequenceNumber { get; set; }

    public OpcUaClientInfo(TcpClient client)
    {
        Id = Guid.NewGuid().ToString("N")[..8];
        TcpClient = client;
        RemoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
        ConnectedAt = DateTime.Now;
    }
}

/// <summary>
/// OPC UA 서버 시뮬레이터
/// </summary>
public class OpcUaServer
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly ConcurrentDictionary<string, OpcUaClientInfo> _clients = new();
    private readonly OpcUaNodeStore _nodeStore;
    private Timer? _simulationTimer;
    private int _counter;
    private readonly Random _random = new();
    private uint _secureChannelIdCounter;
    private uint _tokenIdCounter;

    public int Port { get; private set; }
    public bool IsRunning { get; private set; }
    public bool SimulationEnabled { get; set; } = true;
    public OpcUaNodeStore NodeStore => _nodeStore;

    public event EventHandler<string>? LogMessage;
    public event EventHandler<OpcUaClientInfo>? ClientConnected;
    public event EventHandler<OpcUaClientInfo>? ClientDisconnected;

    public IReadOnlyCollection<OpcUaClientInfo> ConnectedClients => _clients.Values.ToList();

    public OpcUaServer()
    {
        _nodeStore = new OpcUaNodeStore();
    }

    public async Task StartAsync(int port = 4840)
    {
        if (IsRunning) return;

        Port = port;
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, port);

        try
        {
            _listener.Start();
            IsRunning = true;
            Log($"OPC UA 서버 시작됨 - 포트: {port}");
            Log($"Endpoint: opc.tcp://localhost:{port}");

            StartSimulation();
            _ = AcceptClientsAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            Log($"서버 시작 실패: {ex.Message}");
            throw;
        }
    }

    public void Stop()
    {
        if (!IsRunning) return;

        StopSimulation();
        _cts?.Cancel();
        _listener?.Stop();

        foreach (var client in _clients.Values)
        {
            try { client.TcpClient.Close(); } catch { }
        }
        _clients.Clear();

        IsRunning = false;
        Log("OPC UA 서버 중지됨");
    }

    private void StartSimulation()
    {
        _simulationTimer = new Timer(1000);
        _simulationTimer.Elapsed += OnSimulationTick;
        _simulationTimer.Start();
    }

    private void StopSimulation()
    {
        _simulationTimer?.Stop();
        _simulationTimer?.Dispose();
        _simulationTimer = null;
    }

    private void OnSimulationTick(object? sender, ElapsedEventArgs e)
    {
        if (!SimulationEnabled) return;

        _counter++;
        double time = _counter * 0.1;

        // 시뮬레이션 값 업데이트
        _nodeStore.WriteValue("ns=2;s=Simulation.Counter", _counter);
        _nodeStore.WriteValue("ns=2;s=Simulation.Random", _random.NextDouble() * 100);
        _nodeStore.WriteValue("ns=2;s=Simulation.SineWave", Math.Sin(time) * 50 + 50);
        _nodeStore.WriteValue("ns=2;s=Simulation.Boolean", _counter % 2 == 0);

        // 디바이스 값 업데이트
        var temp = 25.0 + Math.Sin(time * 0.5) * 5 + _random.NextDouble() * 0.5;
        var pressure = 1013.25 + Math.Cos(time * 0.3) * 10 + _random.NextDouble() * 2;
        _nodeStore.WriteValue("ns=2;s=Device.Temperature", Math.Round(temp, 2));
        _nodeStore.WriteValue("ns=2;s=Device.Pressure", Math.Round(pressure, 2));
    }

    private async Task AcceptClientsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var tcpClient = await _listener!.AcceptTcpClientAsync(ct);
                var clientInfo = new OpcUaClientInfo(tcpClient);
                _clients.TryAdd(clientInfo.Id, clientInfo);

                Log($"클라이언트 연결됨: {clientInfo.RemoteEndPoint}");
                ClientConnected?.Invoke(this, clientInfo);

                _ = HandleClientAsync(clientInfo, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { Log($"클라이언트 수락 오류: {ex.Message}"); }
        }
    }

    private async Task HandleClientAsync(OpcUaClientInfo clientInfo, CancellationToken ct)
    {
        var buffer = new byte[65536];
        var stream = clientInfo.TcpClient.GetStream();

        try
        {
            while (!ct.IsCancellationRequested && clientInfo.TcpClient.Connected)
            {
                int bytesRead;
                try
                {
                    bytesRead = await stream.ReadAsync(buffer, ct);
                }
                catch { break; }

                if (bytesRead == 0) break;

                var data = new byte[bytesRead];
                Array.Copy(buffer, data, bytesRead);

                clientInfo.RequestCount++;
                var response = ProcessMessage(clientInfo, data);

                if (response != null && response.Length > 0)
                {
                    await stream.WriteAsync(response, ct);
                }
            }
        }
        catch (Exception ex)
        {
            Log($"[{clientInfo.SessionId ?? clientInfo.Id}] 오류: {ex.Message}");
        }
        finally
        {
            _clients.TryRemove(clientInfo.Id, out _);
            clientInfo.TcpClient.Close();
            Log($"클라이언트 연결 해제: {clientInfo.SessionId ?? clientInfo.Id}");
            ClientDisconnected?.Invoke(this, clientInfo);
        }
    }

    private byte[]? ProcessMessage(OpcUaClientInfo client, byte[] data)
    {
        if (data.Length < 8) return null;

        var header = OpcUaMessageHeader.Parse(data);
        Log($"[{client.Id}] 메시지 수신: {header.MessageType}, Size: {header.MessageSize}");

        return header.MessageType switch
        {
            "HEL" => HandleHello(client, data),
            "OPN" => HandleOpenSecureChannel(client, data),
            "MSG" => HandleMessage(client, data),
            "CLO" => HandleCloseSecureChannel(client, data),
            _ => CreateErrorResponse("Bad_UnexpectedError")
        };
    }

    private byte[] HandleHello(OpcUaClientInfo client, byte[] data)
    {
        var hello = HelloMessage.Parse(data, 8);
        Log($"[{client.Id}] Hello: EndpointUrl={hello.EndpointUrl}");

        var ack = new AcknowledgeMessage
        {
            ReceiveBufferSize = Math.Min(hello.ReceiveBufferSize, 65536),
            SendBufferSize = Math.Min(hello.SendBufferSize, 65536),
            MaxMessageSize = Math.Min(hello.MaxMessageSize, 16777216)
        };

        return ack.ToBytes();
    }

    private byte[] HandleOpenSecureChannel(OpcUaClientInfo client, byte[] data)
    {
        Log($"[{client.Id}] OpenSecureChannel 요청");

        try
        {
            var request = OpenSecureChannelRequest.Parse(data, 8);

            // 새로운 SecureChannel 할당
            client.SecureChannelId = ++_secureChannelIdCounter;
            client.TokenId = ++_tokenIdCounter;
            client.SequenceNumber = request.SequenceNumber;

            Log($"[{client.Id}] SecureChannel 생성: ChannelId={client.SecureChannelId}, TokenId={client.TokenId}");

            return CreateOpenSecureChannelResponse(client, request);
        }
        catch (Exception ex)
        {
            Log($"[{client.Id}] OpenSecureChannel 오류: {ex.Message}");
            return CreateErrorResponse("Bad_SecurityChecksFailed");
        }
    }

    private byte[] CreateOpenSecureChannelResponse(OpcUaClientInfo client, OpenSecureChannelRequest request)
    {
        var encoder = new OpcUaBinaryEncoder();

        // Security Header
        encoder.WriteUInt32(client.SecureChannelId);

        // 간소화된 Security Policy (SecurityMode.None)
        encoder.WriteString("http://opcfoundation.org/UA/SecurityPolicy#None");
        encoder.WriteByteString(null);  // SenderCertificate
        encoder.WriteByteString(null);  // ReceiverCertificateThumbprint

        // Sequence Header
        encoder.WriteUInt32(++client.SequenceNumber);
        encoder.WriteUInt32(request.RequestId);

        // Response TypeId
        encoder.WriteNodeId("ns=0;i=449");  // OpenSecureChannelResponse
        encoder.WriteByte(0x01);  // Binary encoding

        // Response Header
        var responseHeader = new ServiceResponseHeader { RequestHandle = 0 };
        responseHeader.Write(encoder);

        // SecurityToken
        encoder.WriteUInt32(client.SecureChannelId);
        encoder.WriteUInt32(client.TokenId);
        encoder.WriteDateTime(DateTime.UtcNow);
        encoder.WriteUInt32(request.RequestedLifetime);

        // ServerNonce
        encoder.WriteByteString(new byte[32]);  // 32-byte nonce

        var body = encoder.ToArray();

        // Message Header
        var header = new OpcUaMessageHeader
        {
            MessageType = "OPN",
            ChunkType = (byte)'F',
            MessageSize = (uint)(8 + body.Length)
        };

        var result = new byte[header.MessageSize];
        header.ToBytes().CopyTo(result, 0);
        body.CopyTo(result, 8);

        return result;
    }

    private byte[] HandleMessage(OpcUaClientInfo client, byte[] data)
    {
        if (data.Length < 24) return CreateErrorResponse("Bad_DecodingError");

        try
        {
            var decoder = new OpcUaBinaryDecoder(data, 8);

            // Secure Channel Id
            uint channelId = decoder.ReadUInt32();
            uint tokenId = decoder.ReadUInt32();

            // Sequence Header
            uint sequenceNumber = decoder.ReadUInt32();
            uint requestId = decoder.ReadUInt32();

            // Request TypeId
            string typeId = decoder.ReadNodeId();
            byte encoding = decoder.ReadByte();

            Log($"[{client.Id}] 서비스 요청: TypeId={typeId}");

            // Service Request 처리
            return typeId switch
            {
                "ns=0;i=426" => HandleGetEndpoints(client, decoder, requestId),
                "ns=0;i=461" => HandleCreateSession(client, decoder, requestId),
                "ns=0;i=467" => HandleActivateSession(client, decoder, requestId),
                "ns=0;i=473" => HandleCloseSession(client, decoder, requestId),
                "ns=0;i=527" => HandleBrowse(client, decoder, requestId),
                "ns=0;i=631" => HandleRead(client, decoder, requestId),
                "ns=0;i=673" => HandleWrite(client, decoder, requestId),
                _ => CreateServiceFault(client, requestId, OpcUaConstants.StatusCodeBadNodeIdUnknown)
            };
        }
        catch (Exception ex)
        {
            Log($"[{client.Id}] 메시지 처리 오류: {ex.Message}");
            return CreateErrorResponse("Bad_DecodingError");
        }
    }

    private byte[] HandleGetEndpoints(OpcUaClientInfo client, OpcUaBinaryDecoder decoder, uint requestId)
    {
        // Skip request header
        var reqHeader = ServiceRequestHeader.Parse(decoder);
        decoder.ReadByte();  // AdditionalHeader

        string? endpointUrl = decoder.ReadString();
        Log($"[{client.Id}] GetEndpoints: Url={endpointUrl}");

        var encoder = new OpcUaBinaryEncoder();
        WriteMessageHeader(encoder, client, requestId, "ns=0;i=429");  // GetEndpointsResponse

        // Response Header
        var responseHeader = new ServiceResponseHeader { RequestHandle = reqHeader.RequestHandle };
        responseHeader.Write(encoder);

        // Endpoints array (1개의 None 보안 엔드포인트)
        encoder.WriteInt32(1);  // Array length

        // EndpointDescription
        encoder.WriteString($"opc.tcp://localhost:{Port}");  // EndpointUrl

        // Server ApplicationDescription
        encoder.WriteString($"urn:OpcUaSimulator:{Port}");  // ApplicationUri
        encoder.WriteString("");  // ProductUri
        encoder.WriteByte(0x02);  // LocalizedText encoding (Text only)
        encoder.WriteString("OPC UA Simulator");  // ApplicationName
        encoder.WriteUInt32(0);  // ApplicationType: Server
        encoder.WriteString(null);  // GatewayServerUri
        encoder.WriteString(null);  // DiscoveryProfileUri
        encoder.WriteInt32(0);  // DiscoveryUrls array (empty)

        encoder.WriteByteString(null);  // ServerCertificate
        encoder.WriteUInt32(1);  // SecurityMode: None
        encoder.WriteString("http://opcfoundation.org/UA/SecurityPolicy#None");
        encoder.WriteInt32(1);  // UserIdentityTokens count

        // UserTokenPolicy (Anonymous)
        encoder.WriteString("anonymous");  // PolicyId
        encoder.WriteUInt32(0);  // TokenType: Anonymous
        encoder.WriteString(null);  // IssuedTokenType
        encoder.WriteString(null);  // IssuerEndpointUrl
        encoder.WriteString(null);  // SecurityPolicyUri

        encoder.WriteString($"opc.tcp://localhost:{Port}");  // TransportProfileUri
        encoder.WriteByte(0);  // SecurityLevel

        return BuildMessage(client, encoder.ToArray());
    }

    private byte[] HandleCreateSession(OpcUaClientInfo client, OpcUaBinaryDecoder decoder, uint requestId)
    {
        var reqHeader = ServiceRequestHeader.Parse(decoder);
        decoder.ReadByte();  // AdditionalHeader

        // CreateSessionRequest 파싱
        decoder.ReadByte();  // ClientDescription.ApplicationUri encoding
        decoder.ReadString();  // ApplicationUri
        // ... 간소화를 위해 나머지 필드 스킵

        client.SessionId = Guid.NewGuid().ToString();
        Log($"[{client.Id}] CreateSession: SessionId={client.SessionId}");

        var encoder = new OpcUaBinaryEncoder();
        WriteMessageHeader(encoder, client, requestId, "ns=0;i=464");  // CreateSessionResponse

        var responseHeader = new ServiceResponseHeader { RequestHandle = reqHeader.RequestHandle };
        responseHeader.Write(encoder);

        // SessionId & AuthenticationToken
        encoder.WriteNodeId($"ns=1;g={client.SessionId}");
        encoder.WriteNodeId($"ns=1;g={client.SessionId}");

        encoder.WriteDouble(120000);  // RevisedSessionTimeout
        encoder.WriteByteString(new byte[32]);  // ServerNonce
        encoder.WriteByteString(null);  // ServerCertificate
        encoder.WriteInt32(0);  // ServerEndpoints (empty)
        encoder.WriteInt32(0);  // ServerSoftwareCertificates (empty)

        // ServerSignature
        encoder.WriteByteString(null);
        encoder.WriteString(null);

        encoder.WriteUInt32(8192);  // MaxRequestMessageSize

        return BuildMessage(client, encoder.ToArray());
    }

    private byte[] HandleActivateSession(OpcUaClientInfo client, OpcUaBinaryDecoder decoder, uint requestId)
    {
        var reqHeader = ServiceRequestHeader.Parse(decoder);
        Log($"[{client.Id}] ActivateSession");

        client.IsActivated = true;

        var encoder = new OpcUaBinaryEncoder();
        WriteMessageHeader(encoder, client, requestId, "ns=0;i=470");  // ActivateSessionResponse

        var responseHeader = new ServiceResponseHeader { RequestHandle = reqHeader.RequestHandle };
        responseHeader.Write(encoder);

        encoder.WriteByteString(new byte[32]);  // ServerNonce
        encoder.WriteInt32(0);  // Results (empty)
        encoder.WriteInt32(0);  // DiagnosticInfos (empty)

        return BuildMessage(client, encoder.ToArray());
    }

    private byte[] HandleCloseSession(OpcUaClientInfo client, OpcUaBinaryDecoder decoder, uint requestId)
    {
        var reqHeader = ServiceRequestHeader.Parse(decoder);
        Log($"[{client.Id}] CloseSession");

        client.IsActivated = false;
        client.SessionId = null;

        var encoder = new OpcUaBinaryEncoder();
        WriteMessageHeader(encoder, client, requestId, "ns=0;i=476");  // CloseSessionResponse

        var responseHeader = new ServiceResponseHeader { RequestHandle = reqHeader.RequestHandle };
        responseHeader.Write(encoder);

        return BuildMessage(client, encoder.ToArray());
    }

    private byte[] HandleBrowse(OpcUaClientInfo client, OpcUaBinaryDecoder decoder, uint requestId)
    {
        var reqHeader = ServiceRequestHeader.Parse(decoder);
        decoder.ReadByte();  // AdditionalHeader

        // View
        decoder.ReadNodeId();  // ViewId
        decoder.ReadDateTime();  // Timestamp
        decoder.ReadUInt32();  // ViewVersion

        uint maxReferences = decoder.ReadUInt32();
        int nodeCount = decoder.ReadInt32();

        Log($"[{client.Id}] Browse: {nodeCount}개 노드");

        var encoder = new OpcUaBinaryEncoder();
        WriteMessageHeader(encoder, client, requestId, "ns=0;i=530");  // BrowseResponse

        var responseHeader = new ServiceResponseHeader { RequestHandle = reqHeader.RequestHandle };
        responseHeader.Write(encoder);

        // Results array
        encoder.WriteInt32(nodeCount);

        for (int i = 0; i < nodeCount; i++)
        {
            var browse = BrowseDescription.Parse(decoder);
            var node = _nodeStore.GetNode(browse.NodeId);
            var children = _nodeStore.GetChildNodes(browse.NodeId).ToList();

            // BrowseResult
            encoder.WriteStatusCode(OpcUaConstants.StatusCodeGood);
            encoder.WriteByteString(null);  // ContinuationPoint
            encoder.WriteInt32(children.Count);  // References count

            foreach (var child in children)
            {
                // ReferenceDescription
                encoder.WriteNodeId("ns=0;i=35");  // Organizes reference
                encoder.WriteBoolean(true);  // IsForward
                encoder.WriteNodeId(child.NodeId);  // NodeId
                encoder.WriteByte(0x03);  // BrowseName encoding
                encoder.WriteUInt16(2);  // NamespaceIndex
                encoder.WriteString(child.BrowseName);
                encoder.WriteByte(0x02);  // DisplayName encoding
                encoder.WriteString(child.DisplayName);
                encoder.WriteUInt32((uint)child.NodeClass);

                // TypeDefinition
                if (child.NodeClass == OpcUaNodeClass.Variable)
                    encoder.WriteNodeId("ns=0;i=63");  // BaseDataVariableType
                else
                    encoder.WriteNodeId("ns=0;i=61");  // FolderType
            }
        }

        encoder.WriteInt32(0);  // DiagnosticInfos

        return BuildMessage(client, encoder.ToArray());
    }

    private byte[] HandleRead(OpcUaClientInfo client, OpcUaBinaryDecoder decoder, uint requestId)
    {
        var reqHeader = ServiceRequestHeader.Parse(decoder);
        decoder.ReadByte();  // AdditionalHeader

        double maxAge = decoder.ReadDouble();
        uint timestampToReturn = decoder.ReadUInt32();
        int nodeCount = decoder.ReadInt32();

        Log($"[{client.Id}] Read: {nodeCount}개 노드");

        var encoder = new OpcUaBinaryEncoder();
        WriteMessageHeader(encoder, client, requestId, "ns=0;i=634");  // ReadResponse

        var responseHeader = new ServiceResponseHeader { RequestHandle = reqHeader.RequestHandle };
        responseHeader.Write(encoder);

        // Results array
        encoder.WriteInt32(nodeCount);

        for (int i = 0; i < nodeCount; i++)
        {
            var readValue = ReadValueId.Parse(decoder);
            var node = _nodeStore.GetNode(readValue.NodeId);

            if (node != null && readValue.AttributeId == 13)  // Value attribute
            {
                encoder.WriteDataValue(node.Value, node.DataType, OpcUaConstants.StatusCodeGood, node.Timestamp);
            }
            else if (node == null)
            {
                // Bad_NodeIdUnknown
                encoder.WriteByte(0x02);  // StatusCode only
                encoder.WriteStatusCode(OpcUaConstants.StatusCodeBadNodeIdUnknown);
            }
            else
            {
                // Bad_AttributeIdInvalid
                encoder.WriteByte(0x02);
                encoder.WriteStatusCode(OpcUaConstants.StatusCodeBadAttributeIdInvalid);
            }
        }

        encoder.WriteInt32(0);  // DiagnosticInfos

        return BuildMessage(client, encoder.ToArray());
    }

    private byte[] HandleWrite(OpcUaClientInfo client, OpcUaBinaryDecoder decoder, uint requestId)
    {
        var reqHeader = ServiceRequestHeader.Parse(decoder);
        decoder.ReadByte();  // AdditionalHeader

        int nodeCount = decoder.ReadInt32();
        Log($"[{client.Id}] Write: {nodeCount}개 노드");

        var results = new List<uint>();

        for (int i = 0; i < nodeCount; i++)
        {
            var writeValue = WriteValue.Parse(decoder);
            var node = _nodeStore.GetNode(writeValue.NodeId);

            if (node == null)
            {
                results.Add(OpcUaConstants.StatusCodeBadNodeIdUnknown);
            }
            else if (!node.IsWritable)
            {
                results.Add(OpcUaConstants.StatusCodeBadNotWritable);
            }
            else if (writeValue.Value != null)
            {
                _nodeStore.WriteValue(writeValue.NodeId, writeValue.Value);
                results.Add(OpcUaConstants.StatusCodeGood);
                Log($"[{client.Id}] Write: {writeValue.NodeId} = {writeValue.Value}");
            }
            else
            {
                results.Add(OpcUaConstants.StatusCodeGood);
            }
        }

        var encoder = new OpcUaBinaryEncoder();
        WriteMessageHeader(encoder, client, requestId, "ns=0;i=676");  // WriteResponse

        var responseHeader = new ServiceResponseHeader { RequestHandle = reqHeader.RequestHandle };
        responseHeader.Write(encoder);

        // Results array
        encoder.WriteInt32(results.Count);
        foreach (var result in results)
            encoder.WriteStatusCode(result);

        encoder.WriteInt32(0);  // DiagnosticInfos

        return BuildMessage(client, encoder.ToArray());
    }

    private byte[] HandleCloseSecureChannel(OpcUaClientInfo client, byte[] data)
    {
        Log($"[{client.Id}] CloseSecureChannel");
        return Array.Empty<byte>();
    }

    private void WriteMessageHeader(OpcUaBinaryEncoder encoder, OpcUaClientInfo client, uint requestId, string typeId)
    {
        // Security Header (None 모드)
        encoder.WriteUInt32(client.SecureChannelId);
        encoder.WriteUInt32(client.TokenId);

        // Sequence Header
        encoder.WriteUInt32(++client.SequenceNumber);
        encoder.WriteUInt32(requestId);

        // Response TypeId
        encoder.WriteNodeId(typeId);
        encoder.WriteByte(0x01);  // Binary encoding
    }

    private byte[] BuildMessage(OpcUaClientInfo client, byte[] body)
    {
        var header = new OpcUaMessageHeader
        {
            MessageType = "MSG",
            ChunkType = (byte)'F',
            MessageSize = (uint)(8 + body.Length)
        };

        var result = new byte[header.MessageSize];
        header.ToBytes().CopyTo(result, 0);
        body.CopyTo(result, 8);

        return result;
    }

    private byte[] CreateServiceFault(OpcUaClientInfo client, uint requestId, uint statusCode)
    {
        var encoder = new OpcUaBinaryEncoder();
        WriteMessageHeader(encoder, client, requestId, "ns=0;i=397");  // ServiceFault

        var responseHeader = new ServiceResponseHeader
        {
            RequestHandle = 0,
            ServiceResult = statusCode
        };
        responseHeader.Write(encoder);

        return BuildMessage(client, encoder.ToArray());
    }

    private byte[] CreateErrorResponse(string error)
    {
        var encoder = new OpcUaBinaryEncoder();
        encoder.WriteBytes(Encoding.ASCII.GetBytes("ERR"));
        encoder.WriteByte((byte)'F');

        // 임시 크기 (나중에 수정)
        int sizePos = encoder.Length;
        encoder.WriteUInt32(0);

        encoder.WriteUInt32(0x80010000);  // Bad_UnexpectedError
        encoder.WriteString(error);

        var result = encoder.ToArray();
        BitConverter.GetBytes((uint)result.Length).CopyTo(result, 4);

        return result;
    }

    private void Log(string message) => LogMessage?.Invoke(this, $"[{DateTime.Now:HH:mm:ss.fff}] {message}");
}
