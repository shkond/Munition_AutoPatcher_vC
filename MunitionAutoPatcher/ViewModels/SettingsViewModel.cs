using System.Windows.Input;
using MunitionAutoPatcher.Commands;
using MunitionAutoPatcher.Services.Interfaces;
using Microsoft.Win32;

namespace MunitionAutoPatcher.ViewModels;

/// <summary>
/// ViewModel for the settings view
/// </summary>
public class SettingsViewModel : ViewModelBase
{
    private readonly IConfigService _configService;
    private readonly IOrchestrator _orchestrator;
    private string _gameDataPath = @"C:\Games\Fallout4\Data";
    private string _outputPath = @"C:\Games\Fallout4\Data\RobCoPatcher.ini";
    private bool _autoMapByName = true;
    private bool _autoMapByType = true;
    private bool _excludeFallout4Esm = true;
    private bool _excludeDlcEsms = true;
    private bool _excludeCcEsl = true;
    private bool _preferEditorIdForDisplay = false;
    private bool _isProcessing;

    public SettingsViewModel(IConfigService configService, IOrchestrator orchestrator)
    {
        _configService = configService;
        _orchestrator = orchestrator;

        BrowseGameDataCommand = new RelayCommand(BrowseGameData);
        BrowseOutputPathCommand = new RelayCommand(BrowseOutputPath);
        StartExtractionCommand = new AsyncRelayCommand(StartExtraction, () => !IsProcessing);
        
        LoadSettings();
    }

    public string GameDataPath
    {
        get => _gameDataPath;
        set => SetProperty(ref _gameDataPath, value);
    }

    public string OutputPath
    {
        get => _outputPath;
        set => SetProperty(ref _outputPath, value);
    }

    public bool AutoMapByName
    {
        get => _autoMapByName;
        set => SetProperty(ref _autoMapByName, value);
    }

    public bool AutoMapByType
    {
        get => _autoMapByType;
        set => SetProperty(ref _autoMapByType, value);
    }

    public bool ExcludeFallout4Esm
    {
        get => _excludeFallout4Esm;
        set
        {
            if (SetProperty(ref _excludeFallout4Esm, value))
            {
                _configService.SetExcludeFallout4Esm(value);
            }
        }
    }

    public bool ExcludeDlcEsms
    {
        get => _excludeDlcEsms;
        set
        {
            if (SetProperty(ref _excludeDlcEsms, value))
            {
                _configService.SetExcludeDlcEsms(value);
            }
        }
    }

    public bool ExcludeCcEsl
    {
        get => _excludeCcEsl;
        set
        {
            if (SetProperty(ref _excludeCcEsl, value))
            {
                _configService.SetExcludeCcEsl(value);
            }
        }
    }

    public bool PreferEditorIdForDisplay
    {
        get => _preferEditorIdForDisplay;
        set
        {
            if (SetProperty(ref _preferEditorIdForDisplay, value))
            {
                _configService.SetPreferEditorIdForDisplay(value);
            }
        }
    }

    public bool IsProcessing
    {
        get => _isProcessing;
        set => SetProperty(ref _isProcessing, value);
    }

    public ICommand BrowseGameDataCommand { get; }
    public ICommand BrowseOutputPathCommand { get; }
    public ICommand StartExtractionCommand { get; }

    private void LoadSettings()
    {
        GameDataPath = _configService.GetGameDataPath();
        OutputPath = _configService.GetOutputPath();
        ExcludeFallout4Esm = _configService.GetExcludeFallout4Esm();
        ExcludeDlcEsms = _configService.GetExcludeDlcEsms();
        ExcludeCcEsl = _configService.GetExcludeCcEsl();
        PreferEditorIdForDisplay = _configService.GetPreferEditorIdForDisplay();
    }

    private void BrowseGameData()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Fallout4.exeを選択してください",
            Filter = "実行ファイル (*.exe)|*.exe"
        };

        if (dialog.ShowDialog() == true)
        {
            var dataPath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(dialog.FileName) ?? "",
                "Data");
            GameDataPath = dataPath;
            _configService.SetGameDataPath(dataPath);
        }
    }

    private void BrowseOutputPath()
    {
        var dialog = new SaveFileDialog
        {
            Title = "出力INIファイルを選択してください",
            Filter = "INIファイル (*.ini)|*.ini",
            FileName = "RobCoPatcher.ini"
        };

        if (dialog.ShowDialog() == true)
        {
            OutputPath = dialog.FileName;
            _configService.SetOutputPath(dialog.FileName);
        }
    }

    private async Task StartExtraction()
    {
        IsProcessing = true;
        try
        {
            var progress = new Progress<string>(msg =>
            {
                if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel mainVm)
                {
                    mainVm.AddLog(msg);
                }
            });

            await _orchestrator.ExtractWeaponsAsync(progress);
        }
        finally
        {
            IsProcessing = false;
        }
    }
}
