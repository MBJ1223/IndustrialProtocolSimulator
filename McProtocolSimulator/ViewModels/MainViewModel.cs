using McProtocolSimulator.Protocol;
using McProtocolSimulator.Simulator;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace McProtocolSimulator.ViewModels;

/// <summary>
/// 메인 뷰 모델
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    private readonly PlcMemory _memory;
    private readonly McProtocolHandler _handler;
    private readonly McTcpServer _server;

    private int _port = 5000;
    private bool _isRunning;
    private string _statusText = "서버 중지됨";
    private int _selectedDeviceIndex;
    private int _viewStartAddress;
    private int _viewCount = 100;

    public event PropertyChangedEventHandler? PropertyChanged;

    public PlcMemory Memory => _memory;

    public ObservableCollection<string> LogEntries { get; } = new();
    public ObservableCollection<ClientInfo> ConnectedClients { get; } = new();
    public ObservableCollection<MemoryItem> MemoryItems { get; } = new();

    public int Port
    {
        get => _port;
        set { _port = value; OnPropertyChanged(); }
    }

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            _isRunning = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanStart));
            OnPropertyChanged(nameof(CanStop));
        }
    }

    public bool CanStart => !IsRunning;
    public bool CanStop => IsRunning;

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public string[] DeviceTypes => new[] { "D (데이터 레지스터)", "M (내부 릴레이)", "X (입력)", "Y (출력)" };

    public int SelectedDeviceIndex
    {
        get => _selectedDeviceIndex;
        set
        {
            _selectedDeviceIndex = value;
            OnPropertyChanged();
            RefreshMemoryView();
        }
    }

    public int ViewStartAddress
    {
        get => _viewStartAddress;
        set
        {
            _viewStartAddress = Math.Max(0, value);
            OnPropertyChanged();
            RefreshMemoryView();
        }
    }

    public int ViewCount
    {
        get => _viewCount;
        set
        {
            _viewCount = Math.Clamp(value, 1, 1000);
            OnPropertyChanged();
            RefreshMemoryView();
        }
    }

    public ICommand StartServerCommand { get; }
    public ICommand StopServerCommand { get; }
    public ICommand ClearLogCommand { get; }
    public ICommand SaveLogCommand { get; }
    public ICommand ClearMemoryCommand { get; }
    public ICommand RefreshMemoryCommand { get; }

    public MainViewModel()
    {
        _memory = new PlcMemory();
        _handler = new McProtocolHandler(_memory);
        _server = new McTcpServer(_handler);

        // 이벤트 연결
        _server.LogMessage += OnLogMessage;
        _server.ClientConnected += OnClientConnected;
        _server.ClientDisconnected += OnClientDisconnected;
        _memory.MemoryChanged += OnMemoryChanged;

        // 명령 초기화
        StartServerCommand = new RelayCommand(async () => await StartServerAsync(), () => CanStart);
        StopServerCommand = new RelayCommand(StopServer, () => CanStop);
        ClearLogCommand = new RelayCommand(ClearLog);
        SaveLogCommand = new RelayCommand(SaveLog);
        ClearMemoryCommand = new RelayCommand(ClearMemory);
        RefreshMemoryCommand = new RelayCommand(RefreshMemoryView);

        // 초기 메모리 뷰
        RefreshMemoryView();
    }

    private async Task StartServerAsync()
    {
        try
        {
            await _server.StartAsync(Port);
            IsRunning = true;
            StatusText = $"서버 실행 중 - 포트: {Port}";
        }
        catch (Exception ex)
        {
            AddLog($"서버 시작 실패: {ex.Message}");
        }
    }

    private void StopServer()
    {
        _server.Stop();
        IsRunning = false;
        StatusText = "서버 중지됨";
    }

    private void ClearLog()
    {
        LogEntries.Clear();
    }

    private void SaveLog()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "텍스트 파일 (*.txt)|*.txt|모든 파일 (*.*)|*.*",
            DefaultExt = ".txt",
            FileName = $"McProtocol_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
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

    private void ClearMemory()
    {
        _memory.ClearAll();
        RefreshMemoryView();
        AddLog("메모리 초기화됨");
    }

    private void RefreshMemoryView()
    {
        MemoryItems.Clear();

        var deviceType = SelectedDeviceIndex switch
        {
            0 => DeviceType.D,
            1 => DeviceType.M,
            2 => DeviceType.X,
            3 => DeviceType.Y,
            _ => DeviceType.D
        };

        bool isBitDevice = deviceType is DeviceType.M or DeviceType.X or DeviceType.Y;

        if (isBitDevice)
        {
            var bits = _memory.ReadBits(deviceType, ViewStartAddress, ViewCount);
            for (int i = 0; i < bits.Length; i++)
            {
                MemoryItems.Add(new MemoryItem
                {
                    Address = $"{deviceType}{ViewStartAddress + i}",
                    DecimalValue = bits[i] ? "1" : "0",
                    HexValue = bits[i] ? "0x01" : "0x00",
                    BinaryValue = bits[i] ? "1" : "0",
                    IsBit = true
                });
            }
        }
        else
        {
            var words = _memory.ReadWords(deviceType, ViewStartAddress, ViewCount);
            for (int i = 0; i < words.Length; i++)
            {
                MemoryItems.Add(new MemoryItem
                {
                    Address = $"{deviceType}{ViewStartAddress + i}",
                    DecimalValue = words[i].ToString(),
                    HexValue = $"0x{words[i]:X4}",
                    BinaryValue = Convert.ToString(words[i], 2).PadLeft(16, '0'),
                    IsBit = false,
                    RawValue = words[i]
                });
            }
        }
    }

    private void OnLogMessage(object? sender, string message)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            AddLog(message);
        });
    }

    private void OnClientConnected(object? sender, ClientInfo client)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            ConnectedClients.Add(client);
            StatusText = $"서버 실행 중 - 포트: {Port} - 클라이언트: {ConnectedClients.Count}";
        });
    }

    private void OnClientDisconnected(object? sender, ClientInfo client)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var existing = ConnectedClients.FirstOrDefault(c => c.Id == client.Id);
            if (existing != null)
            {
                ConnectedClients.Remove(existing);
            }
            StatusText = $"서버 실행 중 - 포트: {Port} - 클라이언트: {ConnectedClients.Count}";
        });
    }

    private void OnMemoryChanged(object? sender, MemoryChangedEventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            RefreshMemoryView();
        });
    }

    private void AddLog(string message)
    {
        LogEntries.Add(message);

        // 로그 최대 1000개 유지
        while (LogEntries.Count > 1000)
        {
            LogEntries.RemoveAt(0);
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// 메모리 아이템 (UI 표시용)
/// </summary>
public class MemoryItem : INotifyPropertyChanged
{
    private string _address = "";
    private string _decimalValue = "";
    private string _hexValue = "";
    private string _binaryValue = "";
    private bool _isBit;
    private ushort _rawValue;

    public string Address
    {
        get => _address;
        set { _address = value; OnPropertyChanged(); }
    }

    public string DecimalValue
    {
        get => _decimalValue;
        set { _decimalValue = value; OnPropertyChanged(); }
    }

    public string HexValue
    {
        get => _hexValue;
        set { _hexValue = value; OnPropertyChanged(); }
    }

    public string BinaryValue
    {
        get => _binaryValue;
        set { _binaryValue = value; OnPropertyChanged(); }
    }

    public bool IsBit
    {
        get => _isBit;
        set { _isBit = value; OnPropertyChanged(); }
    }

    public ushort RawValue
    {
        get => _rawValue;
        set { _rawValue = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// 간단한 RelayCommand 구현
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Func<Task>? _executeAsync;
    private readonly Action? _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public RelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
    {
        _executeAsync = executeAsync;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public async void Execute(object? parameter)
    {
        if (_executeAsync != null)
        {
            await _executeAsync();
        }
        else
        {
            _execute?.Invoke();
        }
    }
}
