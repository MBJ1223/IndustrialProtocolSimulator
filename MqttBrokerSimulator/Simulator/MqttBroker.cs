using MqttBrokerSimulator.Protocol;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MqttBrokerSimulator.Simulator;

/// <summary>
/// MQTT 클라이언트 정보
/// </summary>
public class MqttClientInfo
{
    public string Id { get; }
    public string ClientId { get; set; } = "";
    public TcpClient TcpClient { get; }
    public IPEndPoint? RemoteEndPoint { get; }
    public DateTime ConnectedAt { get; }
    public DateTime LastActivity { get; set; }
    public HashSet<string> Subscriptions { get; } = new();
    public long MessagesReceived { get; set; }
    public long MessagesSent { get; set; }

    // 세션 관리
    public bool CleanSession { get; set; } = true;
    public ushort KeepAlive { get; set; }

    // QoS 2 메시지 상태 관리
    public HashSet<ushort> PendingPubRec { get; } = new();   // PUBREC 대기 중인 PacketId
    public HashSet<ushort> PendingPubComp { get; } = new();  // PUBCOMP 대기 중인 PacketId

    public MqttClientInfo(TcpClient client)
    {
        Id = Guid.NewGuid().ToString("N")[..8];
        TcpClient = client;
        RemoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
        ConnectedAt = DateTime.Now;
        LastActivity = DateTime.Now;
    }
}

/// <summary>
/// 토픽 메시지
/// </summary>
public class TopicMessage
{
    public string Topic { get; set; } = "";
    public string Payload { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public bool Retained { get; set; }
}

/// <summary>
/// 저장된 세션 정보 (CleanSession=false용)
/// </summary>
public class MqttSessionState
{
    public string ClientId { get; set; } = "";
    public HashSet<string> Subscriptions { get; set; } = new();
    public List<PendingMessage> PendingMessages { get; set; } = new();
}

public class PendingMessage
{
    public string Topic { get; set; } = "";
    public byte[] Payload { get; set; } = Array.Empty<byte>();
    public QosLevel Qos { get; set; }
    public bool Retain { get; set; }
}

/// <summary>
/// MQTT 브로커 시뮬레이터
/// </summary>
public class MqttBroker
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly ConcurrentDictionary<string, MqttClientInfo> _clients = new();
    private readonly ConcurrentDictionary<string, TopicMessage> _retainedMessages = new();
    private readonly ConcurrentDictionary<string, MqttSessionState> _savedSessions = new();
    private ushort _packetIdCounter;
    private Timer? _keepAliveTimer;

    public int Port { get; private set; }
    public bool IsRunning { get; private set; }

    public event EventHandler<string>? LogMessage;
    public event EventHandler<MqttClientInfo>? ClientConnected;
    public event EventHandler<MqttClientInfo>? ClientDisconnected;
    public event EventHandler<TopicMessage>? MessageReceived;

    public IReadOnlyCollection<MqttClientInfo> ConnectedClients => _clients.Values.ToList();
    public IReadOnlyCollection<TopicMessage> RetainedMessages => _retainedMessages.Values.ToList();

    public async Task StartAsync(int port = 1883)
    {
        if (IsRunning) return;

        Port = port;
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, port);

        try
        {
            _listener.Start();
            IsRunning = true;
            Log($"MQTT 브로커 시작됨 - 포트: {port}");

            // Keep-Alive 체크 타이머 시작
            _keepAliveTimer = new Timer(CheckKeepAlive, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

            _ = AcceptClientsAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            Log($"브로커 시작 실패: {ex.Message}");
            throw;
        }
    }

    public void Stop()
    {
        if (!IsRunning) return;

        _keepAliveTimer?.Dispose();
        _keepAliveTimer = null;

        _cts?.Cancel();
        _listener?.Stop();

        foreach (var client in _clients.Values)
        {
            try { client.TcpClient.Close(); } catch { }
        }
        _clients.Clear();

        IsRunning = false;
        Log("MQTT 브로커 중지됨");
    }

    /// <summary>
    /// Keep-Alive 타임아웃 체크
    /// </summary>
    private void CheckKeepAlive(object? state)
    {
        var now = DateTime.Now;
        foreach (var client in _clients.Values.ToList())
        {
            if (client.KeepAlive > 0)
            {
                // Keep-Alive의 1.5배 시간이 지나면 연결 종료 (MQTT 스펙)
                var timeout = TimeSpan.FromSeconds(client.KeepAlive * 1.5);
                if (now - client.LastActivity > timeout)
                {
                    Log($"[{client.ClientId}] Keep-Alive 타임아웃 - 연결 종료");
                    try { client.TcpClient.Close(); } catch { }
                }
            }
        }
    }

    /// <summary>
    /// 특정 토픽에 메시지 발행 (시뮬레이터용)
    /// </summary>
    public void PublishMessage(string topic, string payload, bool retain = false)
    {
        var message = new TopicMessage
        {
            Topic = topic,
            Payload = payload,
            Timestamp = DateTime.Now,
            Retained = retain
        };

        if (retain)
        {
            _retainedMessages[topic] = message;
        }

        MessageReceived?.Invoke(this, message);
        BroadcastToSubscribers(topic, Encoding.UTF8.GetBytes(payload), retain);
        Log($"발행: {topic} = {payload}");
    }

    private async Task AcceptClientsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var tcpClient = await _listener!.AcceptTcpClientAsync(ct);
                var clientInfo = new MqttClientInfo(tcpClient);
                _clients.TryAdd(clientInfo.Id, clientInfo);

                Log($"TCP 연결됨: {clientInfo.RemoteEndPoint}");
                _ = HandleClientAsync(clientInfo, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { Log($"클라이언트 수락 오류: {ex.Message}"); }
        }
    }

    private async Task HandleClientAsync(MqttClientInfo clientInfo, CancellationToken ct)
    {
        var buffer = new byte[8192];
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

                ProcessPacket(clientInfo, data, stream);
            }
        }
        catch (Exception ex)
        {
            Log($"[{clientInfo.ClientId}] 오류: {ex.Message}");
        }
        finally
        {
            _clients.TryRemove(clientInfo.Id, out _);
            clientInfo.TcpClient.Close();
            Log($"클라이언트 연결 해제: {clientInfo.ClientId}");
            ClientDisconnected?.Invoke(this, clientInfo);
        }
    }

    private void ProcessPacket(MqttClientInfo client, byte[] data, NetworkStream stream)
    {
        var packetType = MqttPacketParser.GetPacketType(data[0]);
        client.MessagesReceived++;
        client.LastActivity = DateTime.Now;

        switch (packetType)
        {
            case MqttPacketType.Connect:
                HandleConnect(client, data, stream);
                break;
            case MqttPacketType.Publish:
                HandlePublish(client, data, stream);
                break;
            case MqttPacketType.Pubrec:
                HandlePubRec(client, data, stream);
                break;
            case MqttPacketType.Pubrel:
                HandlePubRel(client, data, stream);
                break;
            case MqttPacketType.Pubcomp:
                HandlePubComp(client, data);
                break;
            case MqttPacketType.Subscribe:
                HandleSubscribe(client, data, stream);
                break;
            case MqttPacketType.Unsubscribe:
                HandleUnsubscribe(client, data, stream);
                break;
            case MqttPacketType.Pingreq:
                HandlePingReq(client, stream);
                break;
            case MqttPacketType.Disconnect:
                HandleDisconnect(client);
                break;
            default:
                Log($"[{client.ClientId}] 알 수 없는 패킷: {packetType}");
                break;
        }
    }

    private void HandleConnect(MqttClientInfo client, byte[] data, NetworkStream stream)
    {
        var connect = ConnectPacket.Parse(data);
        if (connect == null) return;

        client.ClientId = connect.ClientId;
        client.CleanSession = connect.CleanSession;
        client.KeepAlive = connect.KeepAlive;

        Log($"CONNECT: ClientId={connect.ClientId}, CleanSession={connect.CleanSession}, KeepAlive={connect.KeepAlive}");

        // 세션 상태 확인
        byte sessionPresent = 0x00;

        if (!connect.CleanSession && _savedSessions.TryGetValue(connect.ClientId, out var savedSession))
        {
            // 저장된 세션 복원
            client.Subscriptions.UnionWith(savedSession.Subscriptions);
            sessionPresent = 0x01;
            Log($"[{client.ClientId}] 이전 세션 복원됨 - 구독: {savedSession.Subscriptions.Count}개");

            // 대기 중인 메시지 전송
            foreach (var pending in savedSession.PendingMessages)
            {
                SendPublish(client, stream, pending.Topic, pending.Payload, pending.Retain, pending.Qos);
            }
            savedSession.PendingMessages.Clear();
        }
        else if (connect.CleanSession)
        {
            // 이전 세션 삭제
            _savedSessions.TryRemove(connect.ClientId, out _);
        }

        // CONNACK 응답 (Session Present 플래그 포함)
        var connack = new byte[] { MqttConstants.CONNACK, 0x02, sessionPresent, MqttConstants.CONNACK_ACCEPTED };
        stream.Write(connack);
        client.MessagesSent++;

        ClientConnected?.Invoke(this, client);
    }

    private void HandlePublish(MqttClientInfo client, byte[] data, NetworkStream stream)
    {
        var publish = PublishPacket.Parse(data);
        if (publish == null) return;

        string payload = Encoding.UTF8.GetString(publish.Payload);
        Log($"[{client.ClientId}] PUBLISH: {publish.Topic} = {payload} (QoS {(int)publish.Qos})");

        var message = new TopicMessage
        {
            Topic = publish.Topic,
            Payload = payload,
            Timestamp = DateTime.Now,
            Retained = publish.Retain
        };

        if (publish.Retain)
        {
            if (string.IsNullOrEmpty(payload))
                _retainedMessages.TryRemove(publish.Topic, out _);
            else
                _retainedMessages[publish.Topic] = message;
        }

        // QoS에 따른 처리
        switch (publish.Qos)
        {
            case QosLevel.AtMostOnce:
                // QoS 0: 즉시 배포
                MessageReceived?.Invoke(this, message);
                BroadcastToSubscribers(publish.Topic, publish.Payload, publish.Retain, publish.Qos);
                break;

            case QosLevel.AtLeastOnce:
                // QoS 1: PUBACK 전송 후 배포
                MessageReceived?.Invoke(this, message);
                BroadcastToSubscribers(publish.Topic, publish.Payload, publish.Retain, publish.Qos);
                var puback = new byte[] { MqttConstants.PUBACK, 0x02, (byte)(publish.PacketId >> 8), (byte)(publish.PacketId & 0xFF) };
                stream.Write(puback);
                client.MessagesSent++;
                Log($"[{client.ClientId}] PUBACK 전송: PacketId={publish.PacketId}");
                break;

            case QosLevel.ExactlyOnce:
                // QoS 2: PUBREC 전송, PUBREL 대기
                client.PendingPubComp.Add(publish.PacketId);
                var pubrec = new byte[] { MqttConstants.PUBREC, 0x02, (byte)(publish.PacketId >> 8), (byte)(publish.PacketId & 0xFF) };
                stream.Write(pubrec);
                client.MessagesSent++;
                Log($"[{client.ClientId}] PUBREC 전송: PacketId={publish.PacketId}");
                // 메시지는 PUBREL 받은 후 배포
                break;
        }
    }

    /// <summary>
    /// PUBREC 처리 (QoS 2 - 클라이언트로부터)
    /// </summary>
    private void HandlePubRec(MqttClientInfo client, byte[] data, NetworkStream stream)
    {
        if (data.Length < 4) return;

        ushort packetId = (ushort)((data[2] << 8) | data[3]);
        Log($"[{client.ClientId}] PUBREC 수신: PacketId={packetId}");

        // PUBREL 전송
        var pubrel = new byte[] { (byte)(MqttConstants.PUBREL | 0x02), 0x02, (byte)(packetId >> 8), (byte)(packetId & 0xFF) };
        stream.Write(pubrel);
        client.MessagesSent++;
        client.PendingPubRec.Remove(packetId);
        Log($"[{client.ClientId}] PUBREL 전송: PacketId={packetId}");
    }

    /// <summary>
    /// PUBREL 처리 (QoS 2 - 클라이언트로부터)
    /// </summary>
    private void HandlePubRel(MqttClientInfo client, byte[] data, NetworkStream stream)
    {
        if (data.Length < 4) return;

        ushort packetId = (ushort)((data[2] << 8) | data[3]);
        Log($"[{client.ClientId}] PUBREL 수신: PacketId={packetId}");

        // PUBCOMP 전송
        var pubcomp = new byte[] { MqttConstants.PUBCOMP, 0x02, (byte)(packetId >> 8), (byte)(packetId & 0xFF) };
        stream.Write(pubcomp);
        client.MessagesSent++;
        client.PendingPubComp.Remove(packetId);
        Log($"[{client.ClientId}] PUBCOMP 전송: PacketId={packetId}");

        // 이제 메시지를 배포할 수 있음 (이미 PUBREC에서 저장한 경우)
    }

    /// <summary>
    /// PUBCOMP 처리 (QoS 2 - 클라이언트로부터)
    /// </summary>
    private void HandlePubComp(MqttClientInfo client, byte[] data)
    {
        if (data.Length < 4) return;

        ushort packetId = (ushort)((data[2] << 8) | data[3]);
        Log($"[{client.ClientId}] PUBCOMP 수신: PacketId={packetId}");
        // QoS 2 전송 완료
    }

    private void HandleSubscribe(MqttClientInfo client, byte[] data, NetworkStream stream)
    {
        var subscribe = SubscribePacket.Parse(data);
        if (subscribe == null) return;

        var returnCodes = new List<byte>();

        foreach (var (topic, qos) in subscribe.Subscriptions)
        {
            client.Subscriptions.Add(topic);
            returnCodes.Add((byte)qos);
            Log($"[{client.ClientId}] SUBSCRIBE: {topic} (QoS {(int)qos})");

            // 리테인 메시지 전송
            foreach (var retained in _retainedMessages.Values.Where(m => TopicMatches(topic, m.Topic)))
            {
                SendPublish(client, stream, retained.Topic, Encoding.UTF8.GetBytes(retained.Payload), true);
            }
        }

        // SUBACK
        var suback = new List<byte> { MqttConstants.SUBACK };
        suback.AddRange(MqttPacketParser.EncodeRemainingLength(2 + returnCodes.Count));
        suback.Add((byte)(subscribe.PacketId >> 8));
        suback.Add((byte)(subscribe.PacketId & 0xFF));
        suback.AddRange(returnCodes);
        stream.Write(suback.ToArray());
        client.MessagesSent++;
    }

    private void HandleUnsubscribe(MqttClientInfo client, byte[] data, NetworkStream stream)
    {
        var unsubscribe = UnsubscribePacket.Parse(data);
        if (unsubscribe == null) return;

        foreach (var topic in unsubscribe.Topics)
        {
            client.Subscriptions.Remove(topic);
            Log($"[{client.ClientId}] UNSUBSCRIBE: {topic}");
        }

        // UNSUBACK
        var unsuback = new byte[] { MqttConstants.UNSUBACK, 0x02, (byte)(unsubscribe.PacketId >> 8), (byte)(unsubscribe.PacketId & 0xFF) };
        stream.Write(unsuback);
        client.MessagesSent++;
    }

    private void HandlePingReq(MqttClientInfo client, NetworkStream stream)
    {
        var pingresp = new byte[] { MqttConstants.PINGRESP, 0x00 };
        stream.Write(pingresp);
        client.MessagesSent++;
    }

    private void HandleDisconnect(MqttClientInfo client)
    {
        Log($"[{client.ClientId}] DISCONNECT");

        // CleanSession=false인 경우 세션 저장
        if (!client.CleanSession && !string.IsNullOrEmpty(client.ClientId))
        {
            _savedSessions[client.ClientId] = new MqttSessionState
            {
                ClientId = client.ClientId,
                Subscriptions = new HashSet<string>(client.Subscriptions)
            };
            Log($"[{client.ClientId}] 세션 저장됨 - 구독: {client.Subscriptions.Count}개");
        }

        client.TcpClient.Close();
    }

    private void BroadcastToSubscribers(string topic, byte[] payload, bool retain, QosLevel publishQos = QosLevel.AtMostOnce)
    {
        foreach (var client in _clients.Values)
        {
            if (client.Subscriptions.Any(s => TopicMatches(s, topic)))
            {
                try
                {
                    var stream = client.TcpClient.GetStream();
                    // 구독자의 QoS와 발행 QoS 중 낮은 것 사용 (MQTT 스펙)
                    SendPublish(client, stream, topic, payload, retain, publishQos);
                }
                catch { }
            }
        }

        // 오프라인 클라이언트에게 저장 (CleanSession=false인 세션)
        if (publishQos > QosLevel.AtMostOnce)
        {
            foreach (var session in _savedSessions.Values)
            {
                if (!_clients.Values.Any(c => c.ClientId == session.ClientId) &&
                    session.Subscriptions.Any(s => TopicMatches(s, topic)))
                {
                    session.PendingMessages.Add(new PendingMessage
                    {
                        Topic = topic,
                        Payload = payload,
                        Qos = publishQos,
                        Retain = retain
                    });
                }
            }
        }
    }

    private void SendPublish(MqttClientInfo client, NetworkStream stream, string topic, byte[] payload, bool retain, QosLevel qos = QosLevel.AtMostOnce)
    {
        var packetId = ++_packetIdCounter;
        var publish = new PublishPacket
        {
            Topic = topic,
            Payload = payload,
            Qos = qos,
            Retain = retain,
            PacketId = packetId
        };

        stream.Write(publish.ToBytes());
        client.MessagesSent++;

        // QoS 2인 경우 PUBREC 대기 상태로 추가
        if (qos == QosLevel.ExactlyOnce)
        {
            client.PendingPubRec.Add(packetId);
        }
    }

    private static bool TopicMatches(string filter, string topic)
    {
        if (filter == "#") return true;
        if (filter == topic) return true;

        var filterParts = filter.Split('/');
        var topicParts = topic.Split('/');

        for (int i = 0; i < filterParts.Length; i++)
        {
            if (filterParts[i] == "#") return true;
            if (filterParts[i] == "+")
            {
                if (i >= topicParts.Length) return false;
                continue;
            }
            if (i >= topicParts.Length || filterParts[i] != topicParts[i]) return false;
        }

        return filterParts.Length == topicParts.Length;
    }

    private void Log(string message) => LogMessage?.Invoke(this, $"[{DateTime.Now:HH:mm:ss.fff}] {message}");
}
