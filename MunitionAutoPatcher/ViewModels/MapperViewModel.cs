using System.Collections.ObjectModel;
using System.Windows.Input;
using MunitionAutoPatcher.Commands;
using MunitionAutoPatcher.Services.Interfaces;

namespace MunitionAutoPatcher.ViewModels;

/// <summary>
/// ViewModel for the mapper view
/// </summary>
public class MapperViewModel : ViewModelBase
{
    private readonly IOrchestrator _orchestrator;
    private readonly IWeaponsService _weaponsService;
    private readonly IRobCoIniGenerator _iniGenerator;
    private readonly IConfigService _configService;
    private ObservableCollection<WeaponMappingViewModel> _weaponMappings = new();
    private WeaponMappingViewModel? _selectedMapping;
    private bool _isProcessing;
    private ObservableCollection<AmmoViewModel> _ammoCandidates = new();
    private AmmoViewModel? _selectedAmmo;

    public MapperViewModel(IOrchestrator orchestrator, IWeaponsService weaponsService, IRobCoIniGenerator iniGenerator, IConfigService configService)
    {
        _orchestrator = orchestrator;
        _weaponsService = weaponsService;
        _iniGenerator = iniGenerator;
        _configService = configService;

        GenerateMappingsCommand = new AsyncRelayCommand(GenerateMappings, () => !IsProcessing);
        GenerateIniCommand = new AsyncRelayCommand(GenerateIni, () => !IsProcessing && WeaponMappings.Any());
    ApplyMappingCommand = new RelayCommand(() => ApplySelectedAmmo(), () => SelectedMapping != null && SelectedAmmo != null);

        // Ammo candidates will be populated when GenerateMappings runs (after extraction)
    }

    public ObservableCollection<WeaponMappingViewModel> WeaponMappings
    {
        get => _weaponMappings;
        set => SetProperty(ref _weaponMappings, value);
    }

    public WeaponMappingViewModel? SelectedMapping
    {
        get => _selectedMapping;
        set
        {
            SetProperty(ref _selectedMapping, value);
            // SelectedMapping changed -> reevaluate ApplyMappingCommand
            (ApplyMappingCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }


    public bool IsProcessing
    {
        get => _isProcessing;
        set => SetProperty(ref _isProcessing, value);
    }

    public ObservableCollection<AmmoViewModel> AmmoCandidates
    {
        get => _ammoCandidates;
        set => SetProperty(ref _ammoCandidates, value);
    }

    public AmmoViewModel? SelectedAmmo
    {
        get => _selectedAmmo;
        set
        {
            SetProperty(ref _selectedAmmo, value);
            // update ApplyMappingCommand can execute
            (ApplyMappingCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public RelayCommand? ApplyMappingCommand { get; }

    private void ApplySelectedAmmo()
    {
        if (SelectedMapping is null || SelectedAmmo is null) return;
        // Apply selected ammo FormKey to the selected mapping
        SelectedMapping.AmmoName = SelectedAmmo.Name ?? SelectedAmmo.FormKey;
        SelectedMapping.AmmoFormKey = SelectedAmmo.FormKey;
        SelectedMapping.IsManualMapping = true;
    }

    public ICommand GenerateMappingsCommand { get; }
    public ICommand GenerateIniCommand { get; }

    private async Task GenerateMappings()
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

            await _orchestrator.GenerateMappingsAsync(progress);

            // Create some stub mappings for demonstration
            WeaponMappings.Clear();
            var weapons = _weaponsService.GetAllWeapons();
            // Populate ammo candidates from extracted ammo records (preferred)
            AmmoCandidates.Clear();
            try
            {
                var pluginFilter = "Munitions - An Ammo Expansion"; // match with or without extension
                var allAmmo = _weaponsService.GetAllAmmo();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var a in allAmmo)
                {
                    if (a == null) continue;
                    if (string.IsNullOrEmpty(a.FormKey.PluginName)) continue;
                    var pn = a.FormKey.PluginName;
                    var pnNorm = pn.EndsWith(".esp", StringComparison.OrdinalIgnoreCase) || pn.EndsWith(".esl", StringComparison.OrdinalIgnoreCase)
                        ? pn[..pn.LastIndexOf('.')]
                        : pn;
                    if (!string.Equals(pnNorm, pluginFilter, StringComparison.OrdinalIgnoreCase)) continue;
                    var key = $"{a.FormKey.PluginName}:{a.FormKey.FormId:X8}";
                    if (seen.Add(key))
                    {
                        AmmoCandidates.Add(new AmmoViewModel
                        {
                            Name = a.Name ?? string.Empty,
                            FormKey = key,
                            Damage = a.Damage,
                            AmmoType = a.AmmoType ?? string.Empty
                        });
                    }
                }
                // If no ammo records found via GetAllAmmo, fall back to scanning weapons' DefaultAmmo
                if (AmmoCandidates.Count == 0)
                {
                    var seen2 = new HashSet<uint>();
                    foreach (var w in weapons)
                    {
                        var fa = w.DefaultAmmo;
                        if (fa == null) continue;
                        if (string.IsNullOrEmpty(fa.PluginName)) continue;
                        var pn = fa.PluginName;
                        var pnNorm = pn.EndsWith(".esp", StringComparison.OrdinalIgnoreCase) || pn.EndsWith(".esl", StringComparison.OrdinalIgnoreCase)
                            ? pn[..pn.LastIndexOf('.')]
                            : pn;
                        if (!string.Equals(pnNorm, pluginFilter, StringComparison.OrdinalIgnoreCase)) continue;
                        if (seen2.Add(fa.FormId))
                        {
                            AmmoCandidates.Add(new AmmoViewModel
                            {
                                Name = string.Empty,
                                FormKey = $"{fa.PluginName}:{fa.FormId:X8}",
                            });
                        }
                    }
                }
            }
            catch
            {
                // ignore
            }
            foreach (var weapon in weapons)
            {
                WeaponMappings.Add(new WeaponMappingViewModel
                {
                    WeaponName = weapon.Name,
                    WeaponFormKey = weapon.FormKey.ToString(),
                    AmmoName = "自動マッピング未実装",
                    AmmoFormKey = "N/A",
                    Strategy = "Default"
                });
            }
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private async Task GenerateIni()
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

            // Build mappings from current WeaponMappings and call INI generator directly
            var mappings = new List<MunitionAutoPatcher.Models.WeaponMapping>();
            foreach (var vm in WeaponMappings)
            {
                try
                {
                    var weaponFk = MunitionAutoPatcher.Models.FormKey.Parse(vm.WeaponFormKey);
                    var ammoFk = MunitionAutoPatcher.Models.FormKey.Parse(vm.AmmoFormKey);
                    mappings.Add(new MunitionAutoPatcher.Models.WeaponMapping
                    {
                        WeaponFormKey = weaponFk,
                        AmmoFormKey = ammoFk,
                        Strategy = vm.Strategy ?? "Default",
                        IsManualMapping = vm.IsManualMapping
                    });
                }
                catch { }
            }

            var outputPath = _configService.GetOutputPath();
            await _iniGenerator.GenerateIniAsync(outputPath, mappings, progress);
        }
        finally
        {
            IsProcessing = false;
        }
    }
}
