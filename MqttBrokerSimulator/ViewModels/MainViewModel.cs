using Microsoft.Win32;
using MqttBrokerSimulator.Simulator;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace MqttBrokerSimulator.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly MqttBroker _broker;
    private int _port = 1883;
    private bool _isRunning;
    private string _statusText = "브로커 중지됨";
    private string _publishTopic = "test/topic";
    private string _publishPayload = "Hello MQTT!";
    private bool _publishRetain;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MqttBroker Broker => _broker;
    public ObservableCollection<string> LogEntries { get; } = new();
    public ObservableCollection<MqttClientInfo> ConnectedClients { get; } = new();
    public ObservableCollection<TopicMessage> Messages { get; } = new();

    public int Port { get => _port; set { _port = value; OnPropertyChanged(); } }
    public bool IsRunning { get => _isRunning; set { _isRunning = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanStart)); OnPropertyChanged(nameof(CanStop)); } }
    public bool CanStart => !IsRunning;
    public bool CanStop => IsRunning;
    public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }

    public string PublishTopic { get => _publishTopic; set { _publishTopic = value; OnPropertyChanged(); } }
    public string PublishPayload { get => _publishPayload; set { _publishPayload = value; OnPropertyChanged(); } }
    public bool PublishRetain { get => _publishRetain; set { _publishRetain = value; OnPropertyChanged(); } }

    public ICommand StartBrokerCommand { get; }
    public ICommand StopBrokerCommand { get; }
    public ICommand ClearLogCommand { get; }
    public ICommand SaveLogCommand { get; }
    public ICommand ClearMessagesCommand { get; }
    public ICommand PublishCommand { get; }

    public MainViewModel()
    {
        _broker = new MqttBroker();
        _broker.LogMessage += OnLogMessage;
        _broker.ClientConnected += OnClientConnected;
        _broker.ClientDisconnected += OnClientDisconnected;
        _broker.MessageReceived += OnMessageReceived;

        StartBrokerCommand = new RelayCommand(async () => await StartBrokerAsync(), () => CanStart);
        StopBrokerCommand = new RelayCommand(StopBroker, () => CanStop);
        ClearLogCommand = new RelayCommand(() => LogEntries.Clear());
        SaveLogCommand = new RelayCommand(SaveLog);
        ClearMessagesCommand = new RelayCommand(() => Messages.Clear());
        PublishCommand = new RelayCommand(PublishMessage, () => IsRunning);
    }

    private async Task StartBrokerAsync()
    {
        try
        {
            await _broker.StartAsync(Port);
            IsRunning = true;
            StatusText = $"브로커 실행 중 - 포트: {Port}";
        }
        catch (Exception ex) { AddLog($"브로커 시작 실패: {ex.Message}"); }
    }

    private void StopBroker()
    {
        _broker.Stop();
        IsRunning = false;
        StatusText = "브로커 중지됨";
        ConnectedClients.Clear();
    }

    private void SaveLog()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "텍스트 파일 (*.txt)|*.txt|모든 파일 (*.*)|*.*",
            DefaultExt = ".txt",
            FileName = $"MqttBroker_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
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

    private void PublishMessage()
    {
        if (!string.IsNullOrWhiteSpace(PublishTopic))
        {
            _broker.PublishMessage(PublishTopic, PublishPayload, PublishRetain);
        }
    }

    private void OnLogMessage(object? sender, string message)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() => AddLog(message));
    }

    private void OnClientConnected(object? sender, MqttClientInfo client)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            ConnectedClients.Add(client);
            StatusText = $"브로커 실행 중 - 포트: {Port} - 클라이언트: {ConnectedClients.Count}";
        });
    }

    private void OnClientDisconnected(object? sender, MqttClientInfo client)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var existing = ConnectedClients.FirstOrDefault(c => c.Id == client.Id);
            if (existing != null) ConnectedClients.Remove(existing);
            StatusText = $"브로커 실행 중 - 포트: {Port} - 클라이언트: {ConnectedClients.Count}";
        });
    }

    private void OnMessageReceived(object? sender, TopicMessage message)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            Messages.Insert(0, message);
            while (Messages.Count > 100) Messages.RemoveAt(Messages.Count - 1);
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
