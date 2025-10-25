using System.Windows.Input;
using System.Windows;
using System.Collections.ObjectModel;
using MunitionAutoPatcher.Models;
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
    private readonly MunitionAutoPatcher.Services.Interfaces.IWeaponOmodExtractor _omodExtractor;
    private readonly MunitionAutoPatcher.Services.Interfaces.IRobCoIniGenerator _iniGenerator;
    private string _gameDataPath = @"C:\Games\Fallout4\Data";
    private string _outputPath = @"C:\Games\Fallout4\Data\RobCoPatcher.ini";
    private bool _autoMapByName = true;
    private bool _autoMapByType = true;
    private bool _excludeFallout4Esm = true;
    private bool _excludeDlcEsms = true;
    private bool _excludeCcEsl = true;
    private bool _preferEditorIdForDisplay = false;
    private bool _isProcessing;
    private OmodCandidate? _selectedOmodCandidate;

    public SettingsViewModel(IConfigService configService, IOrchestrator orchestrator, MunitionAutoPatcher.Services.Interfaces.IWeaponOmodExtractor omodExtractor, MunitionAutoPatcher.Services.Interfaces.IRobCoIniGenerator iniGenerator)
    {
        _configService = configService;
        _orchestrator = orchestrator;
        _omodExtractor = omodExtractor;
        _iniGenerator = iniGenerator;

        BrowseGameDataCommand = new RelayCommand(BrowseGameData);
        BrowseOutputPathCommand = new RelayCommand(BrowseOutputPath);
    StartExtractionCommand = new AsyncRelayCommand(StartExtraction, () => !IsProcessing);
    ExtractOmodsCommand = new AsyncRelayCommand(StartOmodExtraction, () => !IsProcessing);
    GenerateIniFromSelectedCommand = new AsyncRelayCommand(GenerateIniFromSelected, () => !IsProcessing && SelectedOmodCandidate != null);
        
        LoadSettings();
        OmodCandidates = new ObservableCollection<OmodCandidate>();
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
    public ICommand ExtractOmodsCommand { get; }

    public ObservableCollection<OmodCandidate> OmodCandidates { get; }

    public OmodCandidate? SelectedOmodCandidate
    {
        get => _selectedOmodCandidate;
        set
        {
            if (SetProperty(ref _selectedOmodCandidate, value))
            {
                // Notify command availability changed
                (GenerateIniFromSelectedCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand GenerateIniFromSelectedCommand { get; }

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

    private async Task StartOmodExtraction()
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

            var results = await _omodExtractor.ExtractCandidatesAsync(progress);
            // Populate UI collection
            OmodCandidates.Clear();
            foreach (var r in results)
            {
                OmodCandidates.Add(r);
            }
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private async Task GenerateIniFromSelected()
    {
        if (SelectedOmodCandidate == null)
            return;

        // Ask user for ammo FormKey via dialog on UI thread
        string initial = SelectedOmodCandidate.CandidateAmmo != null ? SelectedOmodCandidate.CandidateAmmo.ToString() : string.Empty;
        string prompt = "生成する INI に設定する弾薬の FormKey を入力してください (形式: PluginName:FormID(hex))";

        string? input = null;
        var dlgResult = false;
        Application.Current.Dispatcher.Invoke(() =>
        {
            var dlg = new MunitionAutoPatcher.Views.InputDialog(prompt, initial)
            {
                Owner = Application.Current.MainWindow
            };
            var res = dlg.ShowDialog();
            if (res == true)
            {
                dlgResult = true;
                input = dlg.ResponseText?.Trim();
            }
        });

        if (!dlgResult || string.IsNullOrEmpty(input))
        {
            if (Application.Current.MainWindow?.DataContext is MainViewModel mainVm)
                mainVm.AddLog("INI 生成がキャンセルされました。弾薬 FormKey が指定されていません。");
            return;
        }

        // Parse input into FormKey
        MunitionAutoPatcher.Models.FormKey ammoFk;
        try
        {
            ammoFk = MunitionAutoPatcher.Models.FormKey.Parse(input);
        }
        catch (Exception ex)
        {
            if (Application.Current.MainWindow?.DataContext is MainViewModel mainVm2)
            {
                mainVm2.AddLog($"無効な FormKey: {ex.Message}");
            }
            return;
        }

        // Build mapping
        var mapping = new MunitionAutoPatcher.Models.WeaponMapping
        {
            WeaponFormKey = SelectedOmodCandidate.CandidateFormKey,
            WeaponName = SelectedOmodCandidate.CandidateEditorId,
            AmmoFormKey = ammoFk,
            AmmoName = string.Empty,
            Strategy = SelectedOmodCandidate.SuggestedTarget,
            IsManualMapping = true
        };

        // Choose output path under artifacts/RobCo_Patcher by default
        var repoRoot = FindRepoRoot();
        var artifactsDir = System.IO.Path.Combine(repoRoot, "artifacts", "RobCo_Patcher", SelectedOmodCandidate.SourcePlugin ?? "");
        if (!System.IO.Directory.Exists(artifactsDir))
            System.IO.Directory.CreateDirectory(artifactsDir);
        var defaultFile = System.IO.Path.Combine(artifactsDir, (SelectedOmodCandidate.SourcePlugin ?? "generated") + ".esp.ini");

        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Title = "RobCo INI を保存",
            Filter = "INI ファイル (*.ini)|*.ini",
            FileName = System.IO.Path.GetFileName(defaultFile),
            InitialDirectory = System.IO.Path.GetDirectoryName(defaultFile)
        };

        var saveOk = sfd.ShowDialog() == true;
        if (!saveOk)
            return;

        var outputPath = sfd.FileName;
        var progress = new Progress<string>(msg =>
        {
            if (Application.Current.MainWindow?.DataContext is MainViewModel mainVm3)
                mainVm3.AddLog(msg);
        });

        // Generate INI asynchronously
        var mappings = new List<MunitionAutoPatcher.Models.WeaponMapping> { mapping };
        await _iniGenerator.GenerateIniAsync(outputPath, mappings, progress);
    }

    private string FindRepoRoot()
    {
        try
        {
            var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var solutionPath = System.IO.Path.Combine(dir.FullName, "MunitionAutoPatcher.sln");
                if (System.IO.File.Exists(solutionPath))
                    return dir.FullName;
                dir = dir.Parent;
            }
        }
        catch (Exception ex)
        {
            try { if (Application.Current.MainWindow?.DataContext is MainViewModel mainVm) mainVm.AddLog($"SettingsViewModel.FindRepoRoot error: {ex.Message}"); } catch { }
        }
        return AppContext.BaseDirectory;
    }
}
