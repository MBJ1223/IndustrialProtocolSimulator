using Microsoft.Win32;
using S7ProtocolSimulator.Protocol;
using S7ProtocolSimulator.Simulator;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace S7ProtocolSimulator.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly S7Memory _memory;
    private readonly S7ProtocolHandler _handler;
    private readonly S7TcpServer _server;

    private int _port = 102;
    private bool _isRunning;
    private string _statusText = "서버 중지됨";
    private int _selectedAreaIndex = 3; // 기본값: DB (데이터블록) - PC 통신에서 주로 사용
    private int _selectedDbNumber = 1;
    private int _viewStartAddress;
    private int _viewCount = 100;

    public event PropertyChangedEventHandler? PropertyChanged;

    public S7Memory Memory => _memory;
    public ObservableCollection<string> LogEntries { get; } = new();
    public ObservableCollection<S7ClientInfo> ConnectedClients { get; } = new();
    public ObservableCollection<S7MemoryItem> MemoryItems { get; } = new();
    public ObservableCollection<int> DataBlockNumbers { get; } = new();

    public int Port
    {
        get => _port;
        set { _port = value; OnPropertyChanged(); }
    }

    public bool IsRunning
    {
        get => _isRunning;
        set { _isRunning = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanStart)); OnPropertyChanged(nameof(CanStop)); }
    }

    public bool CanStart => !IsRunning;
    public bool CanStop => IsRunning;
    public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }

    public string[] AreaTypes => new[] { "I (입력)", "Q (출력)", "M (머커)", "DB (데이터블록)" };

    public int SelectedAreaIndex
    {
        get => _selectedAreaIndex;
        set { _selectedAreaIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsDbSelected)); RefreshMemoryView(); }
    }

    public bool IsDbSelected => SelectedAreaIndex == 3;

    public int SelectedDbNumber
    {
        get => _selectedDbNumber;
        set { _selectedDbNumber = value; OnPropertyChanged(); RefreshMemoryView(); }
    }

    public int ViewStartAddress
    {
        get => _viewStartAddress;
        set { _viewStartAddress = Math.Max(0, value); OnPropertyChanged(); RefreshMemoryView(); }
    }

    public int ViewCount
    {
        get => _viewCount;
        set { _viewCount = Math.Clamp(value, 1, 500); OnPropertyChanged(); RefreshMemoryView(); }
    }

    public ICommand StartServerCommand { get; }
    public ICommand StopServerCommand { get; }
    public ICommand ClearLogCommand { get; }
    public ICommand SaveLogCommand { get; }
    public ICommand ClearMemoryCommand { get; }
    public ICommand RefreshMemoryCommand { get; }
    public ICommand CreateDbCommand { get; }

    public MainViewModel()
    {
        _memory = new S7Memory();
        _handler = new S7ProtocolHandler(_memory);
        _server = new S7TcpServer(_handler);

        _server.LogMessage += OnLogMessage;
        _server.ClientConnected += OnClientConnected;
        _server.ClientDisconnected += OnClientDisconnected;
        _server.ClientRackSlotUpdated += OnClientRackSlotUpdated;
        _memory.MemoryChanged += OnMemoryChanged;

        StartServerCommand = new RelayCommand(async () => await StartServerAsync(), () => CanStart);
        StopServerCommand = new RelayCommand(StopServer, () => CanStop);
        ClearLogCommand = new RelayCommand(ClearLog);
        SaveLogCommand = new RelayCommand(SaveLog);
        ClearMemoryCommand = new RelayCommand(ClearMemory);
        RefreshMemoryCommand = new RelayCommand(RefreshMemoryView);
        CreateDbCommand = new RelayCommand(CreateDataBlock);

        RefreshDbList();
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
        catch (Exception ex) { AddLog($"서버 시작 실패: {ex.Message}"); }
    }

    private void StopServer()
    {
        _server.Stop();
        IsRunning = false;
        StatusText = "서버 중지됨";
    }

    private void ClearLog() => LogEntries.Clear();

    private void SaveLog()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "텍스트 파일 (*.txt)|*.txt|모든 파일 (*.*)|*.*",
            DefaultExt = ".txt",
            FileName = $"S7Protocol_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
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

    private void CreateDataBlock()
    {
        int newDbNumber = DataBlockNumbers.Count > 0 ? DataBlockNumbers.Max() + 1 : 1;
        _memory.CreateDataBlock(newDbNumber, 1024);
        RefreshDbList();
        SelectedDbNumber = newDbNumber;
        AddLog($"DB{newDbNumber} 생성됨 (1024 bytes)");
    }

    private void RefreshDbList()
    {
        DataBlockNumbers.Clear();
        foreach (var db in _memory.DataBlockNumbers)
        {
            DataBlockNumbers.Add(db);
        }
    }

    public void RefreshMemoryView()
    {
        MemoryItems.Clear();

        byte area = SelectedAreaIndex switch
        {
            0 => S7Constants.AreaInput,
            1 => S7Constants.AreaOutput,
            2 => S7Constants.AreaFlags,
            3 => S7Constants.AreaDB,
            _ => S7Constants.AreaFlags
        };

        if (SelectedAreaIndex == 3)
        {
            // DB 영역: 워드(DBW) 단위로 표시
            for (int i = 0; i < ViewCount; i++)
            {
                int wordAddress = ViewStartAddress + (i * 2);
                ushort wordValue = _memory.ReadWord(area, SelectedDbNumber, wordAddress);
                MemoryItems.Add(new S7MemoryItem
                {
                    Address = $"DB{SelectedDbNumber}.DBW{wordAddress}",
                    DecimalValue = wordValue.ToString(),
                    HexValue = $"0x{wordValue:X4}",
                    BinaryValue = Convert.ToString(wordValue, 2).PadLeft(16, '0'),
                    RawValue = wordValue
                });
            }
        }
        else
        {
            // I, Q, M 영역: 바이트 단위로 표시
            string prefix = SelectedAreaIndex switch
            {
                0 => "IB",
                1 => "QB",
                2 => "MB",
                _ => "MB"
            };

            var bytes = _memory.ReadBytes(area, 0, ViewStartAddress, ViewCount);

            for (int i = 0; i < bytes.Length; i++)
            {
                MemoryItems.Add(new S7MemoryItem
                {
                    Address = $"{prefix}{ViewStartAddress + i}",
                    DecimalValue = bytes[i].ToString(),
                    HexValue = $"0x{bytes[i]:X2}",
                    BinaryValue = Convert.ToString(bytes[i], 2).PadLeft(8, '0'),
                    RawValue = bytes[i]
                });
            }
        }
    }

    private void OnLogMessage(object? sender, string message)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() => AddLog(message));
    }

    private void OnClientConnected(object? sender, S7ClientInfo client)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            ConnectedClients.Add(client);
            StatusText = $"서버 실행 중 - 포트: {Port} - 클라이언트: {ConnectedClients.Count}";
        });
    }

    private void OnClientDisconnected(object? sender, S7ClientInfo client)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var existing = ConnectedClients.FirstOrDefault(c => c.Id == client.Id);
            if (existing != null) ConnectedClients.Remove(existing);
            StatusText = $"서버 실행 중 - 포트: {Port} - 클라이언트: {ConnectedClients.Count}";
        });
    }

    private void OnClientRackSlotUpdated(object? sender, S7ClientInfo client)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            // ObservableCollection의 아이템이 변경되었음을 알림
            var index = ConnectedClients.IndexOf(ConnectedClients.FirstOrDefault(c => c.Id == client.Id)!);
            if (index >= 0)
            {
                var item = ConnectedClients[index];
                ConnectedClients.RemoveAt(index);
                ConnectedClients.Insert(index, item);
            }
        });
    }

    private void OnMemoryChanged(object? sender, S7MemoryChangedEventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(RefreshMemoryView);
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

public class S7MemoryItem : INotifyPropertyChanged
{
    private string _address = "";
    private string _decimalValue = "";
    private string _hexValue = "";
    private string _binaryValue = "";
    private ushort _rawValue;

    public string Address { get => _address; set { _address = value; OnPropertyChanged(); } }
    public string DecimalValue { get => _decimalValue; set { _decimalValue = value; OnPropertyChanged(); } }
    public string HexValue { get => _hexValue; set { _hexValue = value; OnPropertyChanged(); } }
    public string BinaryValue { get => _binaryValue; set { _binaryValue = value; OnPropertyChanged(); } }
    public ushort RawValue { get => _rawValue; set { _rawValue = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
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
