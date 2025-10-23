using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Interfaces;
using Mutagen.Bethesda.Fallout4;

namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// Implementation of the weapons service using Mutagen
/// </summary>
public class WeaponsService : IWeaponsService
{
    private readonly ILoadOrderService _loadOrderService;
    private readonly List<WeaponData> _weapons = new();
    private readonly List<AmmoData> _ammo = new();

    public WeaponsService(ILoadOrderService loadOrderService)
    {
        _loadOrderService = loadOrderService;
    }

    public async Task<List<WeaponData>> ExtractWeaponsAsync(IProgress<string>? progress = null)
    {
        progress?.Report("Mutagenを使用して武器データを抽出しています...");
        
        try
        {
            // Get the load order from the load order service
            var loadOrder = await _loadOrderService.GetLoadOrderAsync();
            
            if (loadOrder == null)
            {
                progress?.Report("エラー: ロードオーダーの取得に失敗しました");
                return _weapons;
            }

            _weapons.Clear();
            int weaponCount = 0;

            // Use WinningOverrides to get the final, winning version of each weapon record
            progress?.Report("プラグインから武器レコードを読み込んでいます...");
            
            foreach (var weaponGetter in loadOrder.PriorityOrder.Weapon().WinningOverrides())
            {
                try
                {
                    // Convert Mutagen weapon to our WeaponData model
                    var weaponData = new WeaponData
                    {
                        FormKey = new Models.FormKey 
                        { 
                            PluginName = weaponGetter.FormKey.ModKey.FileName,
                            FormId = weaponGetter.FormKey.ID
                        },
                        EditorId = weaponGetter.EditorID ?? string.Empty,
                        Name = weaponGetter.Name?.String ?? string.Empty,
                        Description = weaponGetter.Description?.String ?? string.Empty,
                        WeaponType = "Unknown", // AnimationType is not directly available in this version
                        Damage = weaponGetter.BaseDamage,
                        FireRate = weaponGetter.AnimationAttackSeconds > 0 
                            ? 60f / weaponGetter.AnimationAttackSeconds 
                            : 0f
                    };

                    // Extract default ammo if available
                    if (weaponGetter.Ammo.FormKey != null)
                    {
                        weaponData.DefaultAmmo = new Models.FormKey
                        {
                            PluginName = weaponGetter.Ammo.FormKey.ModKey.FileName,
                            FormId = weaponGetter.Ammo.FormKey.ID
                        };
                    }

                    _weapons.Add(weaponData);
                    weaponCount++;
                    
                    // Report progress every 50 weapons
                    if (weaponCount % 50 == 0)
                    {
                        progress?.Report($"{weaponCount}個の武器を処理中...");
                    }
                }
                catch (Exception ex)
                {
                    // Skip weapons that fail to parse
                    progress?.Report($"警告: 武器の解析に失敗しました: {ex.Message}");
                }
            }

            progress?.Report($"抽出完了: {_weapons.Count}個の武器データを抽出しました");
            // Build an ammo list by scanning the weapons' DefaultAmmo entries (fallback)
            _ammo.Clear();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var w in _weapons)
            {
                try
                {
                    if (w.DefaultAmmo != null && !string.IsNullOrEmpty(w.DefaultAmmo.PluginName) && w.DefaultAmmo.FormId != 0)
                    {
                        var key = $"{w.DefaultAmmo.PluginName}:{w.DefaultAmmo.FormId:X8}";
                        if (!seen.Contains(key))
                        {
                            seen.Add(key);
                            _ammo.Add(new AmmoData
                            {
                                FormKey = new Models.FormKey
                                {
                                    PluginName = w.DefaultAmmo.PluginName,
                                    FormId = w.DefaultAmmo.FormId
                                },
                                Name = string.Empty,
                                EditorId = string.Empty,
                                Damage = 0,
                                AmmoType = string.Empty
                            });
                        }
                    }
                }
                catch { }
            }

            progress?.Report($"弾薬抽出(武器参照から)完了: {_ammo.Count}個の弾薬を収集しました");
            return _weapons;
        }
        catch (Exception ex)
        {
            progress?.Report($"エラー: 武器データの抽出中にエラーが発生しました: {ex.Message}");
            return _weapons;
        }
    }

    public Task<WeaponData?> GetWeaponAsync(Models.FormKey formKey)
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

    public List<AmmoData> GetAllAmmo()
    {
        return _ammo.ToList();
    }
}
