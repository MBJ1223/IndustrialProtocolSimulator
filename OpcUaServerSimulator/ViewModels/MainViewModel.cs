using Microsoft.Win32;
using OpcUaServerSimulator.Protocol;
using OpcUaServerSimulator.Simulator;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace OpcUaServerSimulator.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly OpcUaServer _server;
    private int _port = 4840;
    private bool _isRunning;
    private string _statusText = "서버 중지됨";
    private bool _simulationEnabled = true;
    private string _selectedNodeId = "";
    private string _writeValue = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    public OpcUaServer Server => _server;
    public ObservableCollection<string> LogEntries { get; } = new();
    public ObservableCollection<OpcUaClientInfo> ConnectedClients { get; } = new();
    public ObservableCollection<OpcUaNode> Nodes { get; } = new();

    public int Port { get => _port; set { _port = value; OnPropertyChanged(); } }
    public bool IsRunning { get => _isRunning; set { _isRunning = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanStart)); OnPropertyChanged(nameof(CanStop)); } }
    public bool CanStart => !IsRunning;
    public bool CanStop => IsRunning;
    public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }

    public bool SimulationEnabled
    {
        get => _simulationEnabled;
        set { _simulationEnabled = value; _server.SimulationEnabled = value; OnPropertyChanged(); }
    }

    public string SelectedNodeId { get => _selectedNodeId; set { _selectedNodeId = value; OnPropertyChanged(); } }
    public string WriteValue { get => _writeValue; set { _writeValue = value; OnPropertyChanged(); } }

    public string EndpointUrl => $"opc.tcp://localhost:{Port}";

    public ICommand StartServerCommand { get; }
    public ICommand StopServerCommand { get; }
    public ICommand ClearLogCommand { get; }
    public ICommand SaveLogCommand { get; }
    public ICommand WriteValueCommand { get; }
    public ICommand RefreshNodesCommand { get; }

    public MainViewModel()
    {
        _server = new OpcUaServer();
        _server.LogMessage += OnLogMessage;
        _server.ClientConnected += OnClientConnected;
        _server.ClientDisconnected += OnClientDisconnected;
        _server.NodeStore.NodeValueChanged += OnNodeValueChanged;

        StartServerCommand = new RelayCommand(async () => await StartServerAsync(), () => CanStart);
        StopServerCommand = new RelayCommand(StopServer, () => CanStop);
        ClearLogCommand = new RelayCommand(() => LogEntries.Clear());
        SaveLogCommand = new RelayCommand(SaveLog);
        WriteValueCommand = new RelayCommand(WriteNodeValue, () => !string.IsNullOrEmpty(SelectedNodeId));
        RefreshNodesCommand = new RelayCommand(RefreshNodes);

        RefreshNodes();
    }

    private async Task StartServerAsync()
    {
        try
        {
            await _server.StartAsync(Port);
            IsRunning = true;
            StatusText = $"서버 실행 중 - {EndpointUrl}";
            OnPropertyChanged(nameof(EndpointUrl));
        }
        catch (Exception ex) { AddLog($"서버 시작 실패: {ex.Message}"); }
    }

    private void StopServer()
    {
        _server.Stop();
        IsRunning = false;
        StatusText = "서버 중지됨";
        ConnectedClients.Clear();
    }

    private void SaveLog()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "텍스트 파일 (*.txt)|*.txt|모든 파일 (*.*)|*.*",
            DefaultExt = ".txt",
            FileName = $"OpcUaServer_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                File.WriteAllLines(dialog.FileName, LogEntries);
                AddLog($"로그 저장 완료: {dialog.FileName}");
            }
            catch (Exception ex)
            {
                AddLog($"로그 저장 실패: {ex.Message}");
            }
        }
    }

    private void WriteNodeValue()
    {
        if (string.IsNullOrEmpty(SelectedNodeId) || string.IsNullOrEmpty(WriteValue)) return;

        if (_server.NodeStore.WriteValue(SelectedNodeId, WriteValue))
        {
            AddLog($"값 쓰기 성공: {SelectedNodeId} = {WriteValue}");
            RefreshNodes();
        }
        else
        {
            AddLog($"값 쓰기 실패: {SelectedNodeId}");
        }
    }

    public void RefreshNodes()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            Nodes.Clear();
            foreach (var node in _server.NodeStore.GetVariables().OrderBy(n => n.NodeId))
            {
                Nodes.Add(node);
            }
        });
    }

    private void OnLogMessage(object? sender, string message)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() => AddLog(message));
    }

    private void OnClientConnected(object? sender, OpcUaClientInfo client)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            ConnectedClients.Add(client);
            StatusText = $"서버 실행 중 - {EndpointUrl} - 클라이언트: {ConnectedClients.Count}";
        });
    }

    private void OnClientDisconnected(object? sender, OpcUaClientInfo client)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var existing = ConnectedClients.FirstOrDefault(c => c.Id == client.Id);
            if (existing != null) ConnectedClients.Remove(existing);
            StatusText = $"서버 실행 중 - {EndpointUrl} - 클라이언트: {ConnectedClients.Count}";
        });
    }

    private void OnNodeValueChanged(object? sender, OpcUaNode node)
    {
        // 노드 목록 갱신 (Simulation 변수만)
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var existing = Nodes.FirstOrDefault(n => n.NodeId == node.NodeId);
            if (existing != null)
            {
                var index = Nodes.IndexOf(existing);
                Nodes[index] = node;
            }
        });
    }

    private void AddLog(string message)
    {
        LogEntries.Add(message);
        while (LogEntries.Count > 1000) LogEntries.RemoveAt(0);
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class RelayCommand : ICommand
{
    private readonly Func<Task>? _executeAsync;
    private readonly Action? _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null) { _execute = execute; _canExecute = canExecute; }
    public RelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null) { _executeAsync = executeAsync; _canExecute = canExecute; }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public async void Execute(object? parameter)
    {
        if (_executeAsync != null) await _executeAsync();
        else _execute?.Invoke();
    }
}
