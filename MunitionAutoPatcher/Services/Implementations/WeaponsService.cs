using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Interfaces;

namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// Stub implementation of the weapons service
/// </summary>
public class WeaponsService : IWeaponsService
{
    private readonly List<WeaponData> _weapons = new();

    public async Task<List<WeaponData>> ExtractWeaponsAsync(IProgress<string>? progress = null)
    {
        progress?.Report("Mutagenを使用して武器データを抽出しています... (スタブ)");
        await Task.Delay(1000); // Simulate extraction

        // Create some stub data
        _weapons.Clear();
        _weapons.AddRange(new[]
        {
            new WeaponData
            {
                FormKey = new FormKey { PluginName = "Fallout4.esm", FormId = 0x001234 },
                EditorId = "WeaponPistol10mm",
                Name = "10mmピストル",
                WeaponType = "Pistol",
                Damage = 18.0f,
                FireRate = 46.0f
            },
            new WeaponData
            {
                FormKey = new FormKey { PluginName = "Fallout4.esm", FormId = 0x005678 },
                EditorId = "WeaponCombatRifle",
                Name = "コンバットライフル",
                WeaponType = "Rifle",
                Damage = 33.0f,
                FireRate = 40.0f
            }
        });

        progress?.Report($"{_weapons.Count}個の武器データを抽出しました");
        return _weapons;
    }

    public Task<WeaponData?> GetWeaponAsync(FormKey formKey)
    {
        var weapon = _weapons.FirstOrDefault(w => 
            w.FormKey.PluginName == formKey.PluginName && 
            w.FormKey.FormId == formKey.FormId);
        return Task.FromResult(weapon);
    }

    public List<WeaponData> GetAllWeapons()
    {
        return _weapons.ToList();
    }
}
