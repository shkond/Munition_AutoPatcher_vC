namespace MunitionAutoPatcher.ViewModels;

/// <summary>
/// ViewModel for ammunition data
/// </summary>
public class AmmoViewModel : ViewModelBase
{
    private string _name = string.Empty;
    private string _formKey = string.Empty;
    private string _categoryName = string.Empty;
    private float _damage;
    private string _ammoType = string.Empty;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string FormKey
    {
        get => _formKey;
        set => SetProperty(ref _formKey, value);
    }

    public string CategoryName
    {
        get => _categoryName;
        set => SetProperty(ref _categoryName, value);
    }

    public float Damage
    {
        get => _damage;
        set => SetProperty(ref _damage, value);
    }

    public string AmmoType
    {
        get => _ammoType;
        set => SetProperty(ref _ammoType, value);
    }

    public string DisplayName => string.IsNullOrEmpty(Name) ? FormKey : $"{Name} ({FormKey})";
}
