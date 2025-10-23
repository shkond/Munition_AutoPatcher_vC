using MunitionAutoPatcher.Models;

namespace MunitionAutoPatcher.Services.Interfaces;

/// <summary>
/// Service for extracting and managing weapon data
/// </summary>
public interface IWeaponsService
{
    Task<List<WeaponData>> ExtractWeaponsAsync(IProgress<string>? progress = null);
    Task<WeaponData?> GetWeaponAsync(FormKey formKey);
    List<WeaponData> GetAllWeapons();
    List<AmmoData> GetAllAmmo();
}
