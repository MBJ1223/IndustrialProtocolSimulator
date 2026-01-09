using Microsoft.Win32;
using ModbusProtocolSimulator.Protocol;
using ModbusProtocolSimulator.Simulator;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace ModbusProtocolSimulator.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly ModbusMemory _memory;
    private readonly ModbusProtocolHandler _handler;
    private readonly ModbusTcpServer _server;

    private int _port = 502;
    private bool _isRunning;
    private string _statusText = "서버 중지됨";
    private int _selectedAreaIndex = 2; // 기본값: Holding Registers (PC 통신에서 주로 사용)
    private int _viewStartAddress;
    private int _viewCount = 100;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ModbusMemory Memory => _memory;
    public ObservableCollection<string> LogEntries { get; } = new();
    public ObservableCollection<ModbusClientInfo> ConnectedClients { get; } = new();
    public ObservableCollection<ModbusMemoryItem> MemoryItems { get; } = new();

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

    public string[] AreaTypes => new[]
    {
        "Coils (0x) - DO",
        "Discrete Inputs (1x) - DI",
        "Holding Registers (4x) - AO",
        "Input Registers (3x) - AI"
    };

    public int SelectedAreaIndex
    {
        get => _selectedAreaIndex;
        set { _selectedAreaIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsBitArea)); RefreshMemoryView(); }
    }

    public bool IsBitArea => SelectedAreaIndex <= 1;  // Coils, Discrete Inputs

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

    public MainViewModel()
    {
        _memory = new ModbusMemory();
        _handler = new ModbusProtocolHandler(_memory);
        _server = new ModbusTcpServer(_handler);

        _server.LogMessage += OnLogMessage;
        _server.ClientConnected += OnClientConnected;
        _server.ClientDisconnected += OnClientDisconnected;
        _server.ClientUnitIdUpdated += OnClientUnitIdUpdated;
        _memory.MemoryChanged += OnMemoryChanged;

        StartServerCommand = new RelayCommand(async () => await StartServerAsync(), () => CanStart);
        StopServerCommand = new RelayCommand(StopServer, () => CanStop);
        ClearLogCommand = new RelayCommand(ClearLog);
        SaveLogCommand = new RelayCommand(SaveLog);
        ClearMemoryCommand = new RelayCommand(ClearMemory);
        RefreshMemoryCommand = new RelayCommand(RefreshMemoryView);

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
            FileName = $"Modbus_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
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

    public void RefreshMemoryView()
    {
        MemoryItems.Clear();

        if (IsBitArea)
        {
            // Coils 또는 Discrete Inputs (비트 단위)
            bool[] bits = SelectedAreaIndex == 0
                ? _memory.ReadCoils(ViewStartAddress, ViewCount)
                : _memory.ReadDiscreteInputs(ViewStartAddress, ViewCount);

            string prefix = SelectedAreaIndex == 0 ? "0" : "1";

            for (int i = 0; i < bits.Length; i++)
            {
                MemoryItems.Add(new ModbusMemoryItem
                {
                    Address = $"{prefix}{ViewStartAddress + i:D5}",
                    Value = bits[i] ? "1 (ON)" : "0 (OFF)",
                    HexValue = bits[i] ? "0x0001" : "0x0000",
                    RawValue = bits[i] ? (ushort)1 : (ushort)0
                });
            }
        }
        else
        {
            // Holding Registers 또는 Input Registers (워드 단위)
            ushort[] registers = SelectedAreaIndex == 2
                ? _memory.ReadHoldingRegisters(ViewStartAddress, ViewCount)
                : _memory.ReadInputRegisters(ViewStartAddress, ViewCount);

            string prefix = SelectedAreaIndex == 2 ? "4" : "3";

            for (int i = 0; i < registers.Length; i++)
            {
                MemoryItems.Add(new ModbusMemoryItem
                {
                    Address = $"{prefix}{ViewStartAddress + i:D5}",
                    Value = registers[i].ToString(),
                    HexValue = $"0x{registers[i]:X4}",
                    RawValue = registers[i]
                });
            }
        }
    }

    public void WriteMemoryValue(int address, ushort value)
    {
        switch (SelectedAreaIndex)
        {
            case 0: // Coils
                _memory.WriteSingleCoil(address, value != 0);
                break;
            case 1: // Discrete Inputs (시뮬레이터에서만 쓰기 가능)
                _memory.SetDiscreteInput(address, value != 0);
                break;
            case 2: // Holding Registers
                _memory.WriteSingleRegister(address, value);
                break;
            case 3: // Input Registers (시뮬레이터에서만 쓰기 가능)
                _memory.SetInputRegister(address, value);
                break;
        }
        RefreshMemoryView();
    }

    private void OnLogMessage(object? sender, string message)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() => AddLog(message));
    }

    private void OnClientConnected(object? sender, ModbusClientInfo client)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            ConnectedClients.Add(client);
            StatusText = $"서버 실행 중 - 포트: {Port} - 클라이언트: {ConnectedClients.Count}";
        });
    }

    private void OnClientDisconnected(object? sender, ModbusClientInfo client)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var existing = ConnectedClients.FirstOrDefault(c => c.Id == client.Id);
            if (existing != null) ConnectedClients.Remove(existing);
            StatusText = $"서버 실행 중 - 포트: {Port} - 클라이언트: {ConnectedClients.Count}";
        });
    }

    private void OnClientUnitIdUpdated(object? sender, ModbusClientInfo client)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var index = ConnectedClients.IndexOf(ConnectedClients.FirstOrDefault(c => c.Id == client.Id)!);
            if (index >= 0)
            {
                var item = ConnectedClients[index];
                ConnectedClients.RemoveAt(index);
                ConnectedClients.Insert(index, item);
            }
        });
    }

    private void OnMemoryChanged(object? sender, ModbusMemoryChangedEventArgs e)
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

public class ModbusMemoryItem : INotifyPropertyChanged
{
    private string _address = "";
    private string _value = "";
    private string _hexValue = "";
    private ushort _rawValue;

    public string Address { get => _address; set { _address = value; OnPropertyChanged(); } }
    public string Value { get => _value; set { _value = value; OnPropertyChanged(); } }
    public string HexValue { get => _hexValue; set { _hexValue = value; OnPropertyChanged(); } }
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
