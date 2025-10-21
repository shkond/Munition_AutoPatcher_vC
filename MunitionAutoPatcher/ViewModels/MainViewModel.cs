using System.Collections.ObjectModel;
using System.Windows.Input;
using MunitionAutoPatcher.Commands;
using MunitionAutoPatcher.Services.Interfaces;

namespace MunitionAutoPatcher.ViewModels;

/// <summary>
/// Main ViewModel managing the overall application state
/// </summary>
public class MainViewModel : ViewModelBase
{
    private readonly IOrchestrator _orchestrator;
    private ViewModelBase? _currentView;
    private string _statusMessage = "準備完了";
    private ObservableCollection<string> _logMessages = new();

    public MainViewModel(
        IOrchestrator orchestrator,
        SettingsViewModel settingsViewModel,
        MapperViewModel mapperViewModel)
    {
        _orchestrator = orchestrator;
        SettingsViewModel = settingsViewModel;
        MapperViewModel = mapperViewModel;

        // Set initial view
        CurrentView = SettingsViewModel;

        // Commands
        ShowSettingsCommand = new RelayCommand(() => CurrentView = SettingsViewModel);
        ShowMapperCommand = new RelayCommand(() => CurrentView = MapperViewModel);
        ExitCommand = new RelayCommand(() => System.Windows.Application.Current.Shutdown());

        // Initialize
        _ = InitializeAsync();
    }

    public SettingsViewModel SettingsViewModel { get; }
    public MapperViewModel MapperViewModel { get; }

    public ViewModelBase? CurrentView
    {
        get => _currentView;
        set => SetProperty(ref _currentView, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ObservableCollection<string> LogMessages
    {
        get => _logMessages;
        set => SetProperty(ref _logMessages, value);
    }

    public ICommand ShowSettingsCommand { get; }
    public ICommand ShowMapperCommand { get; }
    public ICommand ExitCommand { get; }

    private async Task InitializeAsync()
    {
        AddLog("アプリケーションを初期化しています...");
        var result = await _orchestrator.InitializeAsync();
        if (result)
        {
            StatusMessage = "初期化完了";
            AddLog("初期化が完了しました");
        }
        else
        {
            StatusMessage = "初期化エラー";
            AddLog("初期化に失敗しました");
        }
    }

    public void AddLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        LogMessages.Add($"[{timestamp}] {message}");
        StatusMessage = message;
    }
}
