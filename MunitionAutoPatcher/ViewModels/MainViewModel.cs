using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using MunitionAutoPatcher.Commands;
using MunitionAutoPatcher.Services.Interfaces;
using MunitionAutoPatcher;

namespace MunitionAutoPatcher.ViewModels;

/// <summary>
/// Main ViewModel managing the overall application state
/// </summary>
public class MainViewModel : ViewModelBase
{
    private readonly IOrchestrator _orchestrator;
    private readonly ILogger<MainViewModel> _logger;
    private ViewModelBase? _currentView;
    private string _statusMessage = "準備完了";
    private ObservableCollection<string> _logMessages = new();

    public MainViewModel(
        IOrchestrator orchestrator,
        SettingsViewModel settingsViewModel,
        MapperViewModel mapperViewModel,
        ILogger<MainViewModel> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        SettingsViewModel = settingsViewModel;
        MapperViewModel = mapperViewModel;

        // Set initial view
        CurrentView = SettingsViewModel;

        // Commands
        ShowSettingsCommand = new RelayCommand(() => CurrentView = SettingsViewModel);
        ShowMapperCommand = new RelayCommand(() => CurrentView = MapperViewModel);
        ExitCommand = new RelayCommand(() => System.Windows.Application.Current.Shutdown());

        // Subscribe to central logger events for UI updates
        AppLogger.LogMessagePublished += OnAppLogPublished;

        // Initialize
        _ = InitializeAsync();
    }

    private void OnAppLogPublished(string line)
    {
        // Event may be raised on a thread-pool thread; marshal to UI and avoid re-persisting
        var app = System.Windows.Application.Current;
        if (app != null && app.Dispatcher != null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.BeginInvoke(new Action(() =>
            {
                try { AddLog(line, persist: false); } catch { /* swallow */ }
            }));
        }
        else
        {
            try { AddLog(line, persist: false); } catch { /* swallow */ }
        }
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

    public void AddLog(string message, bool persist = true)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        // Ensure collection mutation happens on the UI thread. Some callers invoke AddLog
        // from background threads (e.g. extraction tasks). Use BeginInvoke to avoid
        // blocking the caller and to marshal the update to the Dispatcher thread.
        try
        {
            var payload = $"[{timestamp}] {message}";
            var app = System.Windows.Application.Current;
            if (app != null && app.Dispatcher != null && !app.Dispatcher.CheckAccess())
            {
                app.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        LogMessages.Add(payload);
                        StatusMessage = message;
                    }
                    catch (Exception ex)
                    {
                        // Swallow to avoid any UI logging failure affecting background work
                        AppLogger.Log($"AddLog: UI update failed: {ex.Message}");
                    }
                }));
            }
            else
            {
                // We're already on the UI thread or dispatcher unavailable
                LogMessages.Add(payload);
                StatusMessage = message;
            }

            // Persist UI-visible logs to the central AppLogger so they are written to disk
            if (persist)
            {
                try
                {
                    AppLogger.Log(message);
                }
                catch (Exception ex)
                {
                    // Never allow logging failures to affect UI; AppLogger itself is best-effort.
                    _logger.LogDebug(ex, "AddLog: AppLogger failed");
                }
            }
        }
        catch (Exception ex)
        {
            // Defensive: do not let logging errors propagate
            AppLogger.Log($"AddLog: top-level failure: {ex.Message}");
        }
    }
}