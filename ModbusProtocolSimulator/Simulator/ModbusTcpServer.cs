using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;

namespace ModbusProtocolSimulator.Simulator;

/// <summary>
/// 클라이언트 연결 정보
/// </summary>
public class ModbusClientInfo
{
    public string Id { get; }
    public TcpClient Client { get; }
    public IPEndPoint? RemoteEndPoint { get; }
    public DateTime ConnectedAt { get; }
    public long BytesReceived { get; set; }
    public long BytesSent { get; set; }
    public byte UnitId { get; set; }

    public ModbusClientInfo(TcpClient client)
    {
        Id = Guid.NewGuid().ToString("N")[..8];
        Client = client;
        RemoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
        ConnectedAt = DateTime.Now;
    }
}

/// <summary>
/// Modbus TCP 서버
/// </summary>
public class ModbusTcpServer
{
    private TcpListener? _listener;
    private readonly ModbusMemory _memory;
    private CancellationTokenSource? _cts;
    private readonly ConcurrentDictionary<string, ModbusClientInfo> _clients = new();
    private readonly ConcurrentDictionary<string, ModbusProtocolHandler> _handlers = new();

    public int Port { get; private set; }
    public bool IsRunning { get; private set; }

    public event EventHandler<string>? LogMessage;
    public event EventHandler<ModbusClientInfo>? ClientConnected;
    public event EventHandler<ModbusClientInfo>? ClientDisconnected;
    public event EventHandler<ModbusClientInfo>? ClientUnitIdUpdated;

    public IReadOnlyCollection<ModbusClientInfo> ConnectedClients => _clients.Values.ToList();

    public ModbusTcpServer(ModbusProtocolHandler handler)
    {
        _memory = handler.Memory;
        handler.LogMessage += (s, msg) => Log(msg);
    }

    public async Task StartAsync(int port = 502)
    {
        if (IsRunning) return;

        Port = port;
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, port);

        try
        {
            _listener.Start();
            IsRunning = true;
            Log($"Modbus TCP 서버 시작됨 - 포트: {port}");

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

        _cts?.Cancel();
        _listener?.Stop();

        foreach (var client in _clients.Values)
        {
            try { client.Client.Close(); } catch { }
        }
        _clients.Clear();
        _handlers.Clear();

        IsRunning = false;
        Log("Modbus TCP 서버 중지됨");
    }

    private async Task AcceptClientsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var tcpClient = await _listener!.AcceptTcpClientAsync(ct);
                var clientInfo = new ModbusClientInfo(tcpClient);
                _clients.TryAdd(clientInfo.Id, clientInfo);

                Log($"클라이언트 연결됨: {clientInfo.RemoteEndPoint}");
                ClientConnected?.Invoke(this, clientInfo);

                _ = HandleClientAsync(clientInfo, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { Log($"클라이언트 수락 오류: {ex.Message}"); }
        }
    }

    private async Task HandleClientAsync(ModbusClientInfo clientInfo, CancellationToken ct)
    {
        var client = clientInfo.Client;
        var buffer = new byte[4096];

        // 클라이언트별 핸들러 생성
        var handler = new ModbusProtocolHandler(_memory);
        handler.LogMessage += (s, msg) => Log(msg);
        _handlers.TryAdd(clientInfo.Id, handler);

        try
        {
            var stream = client.GetStream();

            while (!ct.IsCancellationRequested && client.Connected)
            {
                int bytesRead;
                try
                {
                    bytesRead = await stream.ReadAsync(buffer, ct);
                }
                catch (OperationCanceledException) { break; }

                if (bytesRead == 0) break;

                clientInfo.BytesReceived += bytesRead;

                var requestData = new byte[bytesRead];
                Array.Copy(buffer, requestData, bytesRead);

                Log($"[{clientInfo.RemoteEndPoint}] 수신: {bytesRead} bytes");

                var responseData = handler.ProcessRequest(requestData);

                // UnitId 업데이트
                if (handler.UnitId != clientInfo.UnitId)
                {
                    clientInfo.UnitId = handler.UnitId;
                    ClientUnitIdUpdated?.Invoke(this, clientInfo);
                }

                if (responseData != null && responseData.Length > 0)
                {
                    await stream.WriteAsync(responseData, ct);
                    clientInfo.BytesSent += responseData.Length;
                    Log($"[{clientInfo.RemoteEndPoint}] 송신: {responseData.Length} bytes");
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
            _handlers.TryRemove(clientInfo.Id, out _);
            client.Close();
            Log($"클라이언트 연결 해제됨: {clientInfo.RemoteEndPoint}");
            ClientDisconnected?.Invoke(this, clientInfo);
        }
    }

    private void Log(string message) => LogMessage?.Invoke(this, message);
}
