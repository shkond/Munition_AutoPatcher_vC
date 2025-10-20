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
    private ObservableCollection<WeaponMappingViewModel> _weaponMappings = new();
    private WeaponMappingViewModel? _selectedMapping;
    private bool _isProcessing;

    public MapperViewModel(IOrchestrator orchestrator, IWeaponsService weaponsService)
    {
        _orchestrator = orchestrator;
        _weaponsService = weaponsService;

        GenerateMappingsCommand = new AsyncRelayCommand(GenerateMappings, () => !IsProcessing);
        GenerateIniCommand = new AsyncRelayCommand(GenerateIni, () => !IsProcessing && WeaponMappings.Any());
    }

    public ObservableCollection<WeaponMappingViewModel> WeaponMappings
    {
        get => _weaponMappings;
        set => SetProperty(ref _weaponMappings, value);
    }

    public WeaponMappingViewModel? SelectedMapping
    {
        get => _selectedMapping;
        set => SetProperty(ref _selectedMapping, value);
    }

    public bool IsProcessing
    {
        get => _isProcessing;
        set => SetProperty(ref _isProcessing, value);
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

            // Get output path from config
            var outputPath = @"C:\Games\Fallout4\Data\RobCoPatcher.ini";
            await _orchestrator.GenerateIniAsync(outputPath, progress);
        }
        finally
        {
            IsProcessing = false;
        }
    }
}
