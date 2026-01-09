using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;

namespace McProtocolSimulator.Simulator;

/// <summary>
/// 클라이언트 연결 정보
/// </summary>
public class ClientInfo
{
    public string Id { get; }
    public TcpClient Client { get; }
    public IPEndPoint? RemoteEndPoint { get; }
    public DateTime ConnectedAt { get; }
    public long BytesReceived { get; set; }
    public long BytesSent { get; set; }

    public ClientInfo(TcpClient client)
    {
        Id = Guid.NewGuid().ToString("N")[..8];
        Client = client;
        RemoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
        ConnectedAt = DateTime.Now;
    }
}

/// <summary>
/// MC 프로토콜 TCP 서버
/// </summary>
public class McTcpServer
{
    private TcpListener? _listener;
    private readonly McProtocolHandler _handler;
    private CancellationTokenSource? _cts;
    private readonly ConcurrentDictionary<string, ClientInfo> _clients = new();

    public int Port { get; private set; }
    public bool IsRunning { get; private set; }

    public event EventHandler<string>? LogMessage;
    public event EventHandler<ClientInfo>? ClientConnected;
    public event EventHandler<ClientInfo>? ClientDisconnected;

    public IReadOnlyCollection<ClientInfo> ConnectedClients => _clients.Values.ToList();

    public McTcpServer(McProtocolHandler handler)
    {
        _handler = handler;
        _handler.LogMessage += (s, msg) => Log(msg);
    }

    /// <summary>
    /// 서버 시작
    /// </summary>
    public async Task StartAsync(int port)
    {
        if (IsRunning) return;

        Port = port;
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, port);

        try
        {
            _listener.Start();
            IsRunning = true;
            Log($"서버 시작됨 - 포트: {port}");

            // 클라이언트 연결 수락 루프
            _ = AcceptClientsAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            Log($"서버 시작 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 서버 중지
    /// </summary>
    public void Stop()
    {
        if (!IsRunning) return;

        _cts?.Cancel();
        _listener?.Stop();

        // 모든 클라이언트 연결 종료
        foreach (var client in _clients.Values)
        {
            try
            {
                client.Client.Close();
            }
            catch { }
        }
        _clients.Clear();

        IsRunning = false;
        Log("서버 중지됨");
    }

    /// <summary>
    /// 클라이언트 연결 수락
    /// </summary>
    private async Task AcceptClientsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var tcpClient = await _listener!.AcceptTcpClientAsync(ct);
                var clientInfo = new ClientInfo(tcpClient);
                _clients.TryAdd(clientInfo.Id, clientInfo);

                Log($"클라이언트 연결됨: {clientInfo.RemoteEndPoint}");
                ClientConnected?.Invoke(this, clientInfo);

                // 클라이언트 처리 시작
                _ = HandleClientAsync(clientInfo, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log($"클라이언트 수락 오류: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 클라이언트 요청 처리
    /// </summary>
    private async Task HandleClientAsync(ClientInfo clientInfo, CancellationToken ct)
    {
        var client = clientInfo.Client;
        var buffer = new byte[4096];

        try
        {
            var stream = client.GetStream();

            while (!ct.IsCancellationRequested && client.Connected)
            {
                // 데이터 수신
                int bytesRead;
                try
                {
                    bytesRead = await stream.ReadAsync(buffer, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (bytesRead == 0) break;

                clientInfo.BytesReceived += bytesRead;

                // 수신 데이터 복사
                var requestData = new byte[bytesRead];
                Array.Copy(buffer, requestData, bytesRead);

                Log($"[{clientInfo.RemoteEndPoint}] 수신: {bytesRead} bytes - {BitConverter.ToString(requestData).Replace("-", " ")}");

                // 요청 처리
                var responseData = _handler.ProcessRequest(requestData);

                if (responseData != null && responseData.Length > 0)
                {
                    await stream.WriteAsync(responseData, ct);
                    clientInfo.BytesSent += responseData.Length;

                    Log($"[{clientInfo.RemoteEndPoint}] 송신: {responseData.Length} bytes - {BitConverter.ToString(responseData).Replace("-", " ")}");
                }
            }
        }
        catch (Exception ex)
        {
            Log($"[{clientInfo.RemoteEndPoint}] 처리 오류: {ex.Message}");
        }
        finally
        {
            _clients.TryRemove(clientInfo.Id, out _);
            client.Close();
            Log($"클라이언트 연결 해제됨: {clientInfo.RemoteEndPoint}");
            ClientDisconnected?.Invoke(this, clientInfo);
        }
    }

    private void Log(string message)
    {
        LogMessage?.Invoke(this, message);
    }
}
