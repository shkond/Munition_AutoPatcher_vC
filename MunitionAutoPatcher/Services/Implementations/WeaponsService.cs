using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Interfaces;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;

using System.Linq;
using System.Text;

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

    // Try to repair common UTF-8 <-> Latin1 mojibake by reinterpreting the string
    // If the string contains suspicious characters (e.g. Ã, Â, å) attempt ISO-8859-1 -> UTF8 conversion.
    private static string FixMojibake(string s)
    {
        if (string.IsNullOrEmpty(s))
            return s;

        if (s.IndexOf('Ã') >= 0 || s.IndexOf('Â') >= 0 || s.IndexOf('å') >= 0 || s.IndexOf('æ') >= 0)
        {
            try
            {
                var bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(s);
                var fixedStr = Encoding.UTF8.GetString(bytes);
                // If it produced something readable, return it; otherwise fall through
                if (!string.IsNullOrWhiteSpace(fixedStr))
                    return fixedStr;
            }
            catch
            {
                // ignore and return original
            }
        }

        return s;
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

            // Try to use Mutagen's GameEnvironment (MO2) so we can resolve FormLinks via the env.LinkCache.
            // If that fails, fall back to the provided loadOrder's PriorityOrder enumeration.
            progress?.Report("プラグインから武器レコードを読み込んでいます...");

            // Track seen ammo keys while building _ammo so we don't duplicate entries.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using var env = GameEnvironment.Typical.Fallout4(Fallout4Release.Fallout4);
                var weaponGetters = env.LoadOrder.PriorityOrder.Weapon().WinningOverrides();

                foreach (var weaponGetter in weaponGetters)
                {
                    try
                    {
                        var weaponData = new WeaponData
                        {
                            FormKey = new Models.FormKey
                            {
                                PluginName = weaponGetter.FormKey.ModKey.FileName,
                                FormId = weaponGetter.FormKey.ID
                            },
                            EditorId = weaponGetter.EditorID ?? string.Empty,
                            Name = FixMojibake(weaponGetter.Name?.String ?? string.Empty),
                            Description = FixMojibake(weaponGetter.Description?.String ?? string.Empty),
                            WeaponType = "Unknown",
                            Damage = weaponGetter.BaseDamage,
                            FireRate = weaponGetter.AnimationAttackSeconds > 0
                                ? 60f / weaponGetter.AnimationAttackSeconds
                                : 0f
                        };

                        // Try to resolve ammunition via the record's FormLink using env.LinkCache
                        try
                        {
                            // Many weapon records expose their ammo as .Ammo (FormLink) - try to resolve it.
                            var ammoLink = weaponGetter.Ammo;
                            if (!ammoLink.IsNull && ammoLink.TryResolve(env.LinkCache, out var ammoRecord))
                            {
                                weaponData.DefaultAmmo = new Models.FormKey
                                {
                                    PluginName = ammoRecord.FormKey.ModKey.FileName,
                                    FormId = ammoRecord.FormKey.ID
                                };
                                weaponData.DefaultAmmoName = FixMojibake(ammoRecord.Name?.String ?? string.Empty);

                                var key = $"{weaponData.DefaultAmmo.PluginName}:{weaponData.DefaultAmmo.FormId:X8}";
                                if (!seen.Contains(key))
                                {
                                    seen.Add(key);
                                    _ammo.Add(new AmmoData
                                    {
                                        FormKey = new Models.FormKey
                                        {
                                            PluginName = weaponData.DefaultAmmo.PluginName,
                                            FormId = weaponData.DefaultAmmo.FormId
                                        },
                                        Name = FixMojibake(weaponData.DefaultAmmoName),
                                        EditorId = ammoRecord.EditorID ?? string.Empty,
                                        Damage = 0,
                                        AmmoType = string.Empty
                                    });
                                }
                            }
                            else
                            {
                                // No resolvable ammo record; if a FormKey is present on the link, preserve it as a fallback
                                if (weaponGetter.Ammo.FormKey != null)
                                {
                                    weaponData.DefaultAmmo = new Models.FormKey
                                    {
                                        PluginName = weaponGetter.Ammo.FormKey.ModKey.FileName,
                                        FormId = weaponGetter.Ammo.FormKey.ID
                                    };
                                }
                            }
                        }
                        catch { }

                        _weapons.Add(weaponData);
                        weaponCount++;

                        if (weaponCount % 50 == 0)
                        {
                            progress?.Report($"{weaponCount}個の武器を処理中...");
                        }
                    }
                    catch (Exception ex)
                    {
                        progress?.Report($"警告: 武器の解析に失敗しました: {ex.Message}");
                    }
                }

                progress?.Report($"抽出完了: {_weapons.Count}個の武器データを抽出しました");
                progress?.Report($"弾薬抽出(MO2 LinkCache 経由)完了: {_ammo.Count}個の弾薬を収集しました");
                return _weapons;
            }
            catch
            {
                // Could not use GameEnvironment (not running under MO2 or API unavailable) - fall back to provided loadOrder
            }

            progress?.Report($"抽出完了: {_weapons.Count}個の武器データを抽出しました");
            // Build an ammo list by scanning the weapons' DefaultAmmo entries (fallback)
            _ammo.Clear();
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
                                Name = w.DefaultAmmoName ?? string.Empty,
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
    

    // 既存の GetWeaponAsync / GetAllWeapons / GetAllAmmo など...
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

    