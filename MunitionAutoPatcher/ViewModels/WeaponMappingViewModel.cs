namespace MunitionAutoPatcher.ViewModels;

/// <summary>
/// ViewModel for a single weapon mapping
/// </summary>
public class WeaponMappingViewModel : ViewModelBase
{
    private string _weaponName = string.Empty;
    private string _weaponFormKey = string.Empty;
    private string _ammoName = string.Empty;
    private string _ammoFormKey = string.Empty;
    private string _strategy = string.Empty;
    private bool _isManualMapping;

    public string WeaponName
    {
        get => _weaponName;
        set => SetProperty(ref _weaponName, value);
    }

    public string WeaponFormKey
    {
        get => _weaponFormKey;
        set => SetProperty(ref _weaponFormKey, value);
    }

    public string AmmoName
    {
        get => _ammoName;
        set => SetProperty(ref _ammoName, value);
    }

    public string AmmoFormKey
    {
        get => _ammoFormKey;
        set => SetProperty(ref _ammoFormKey, value);
    }

    public string Strategy
    {
        get => _strategy;
        set => SetProperty(ref _strategy, value);
    }

    public bool IsManualMapping
    {
        get => _isManualMapping;
        set => SetProperty(ref _isManualMapping, value);
    }
}
