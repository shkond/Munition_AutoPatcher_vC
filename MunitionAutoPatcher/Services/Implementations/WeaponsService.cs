using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Interfaces;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Strings;

using System.Linq;
using System.Text;
using System.IO;
using System.Globalization;

namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// Implementation of the weapons service using Mutagen
/// </summary>
public class WeaponsService : IWeaponsService
{
    private readonly ILoadOrderService _loadOrderService;
    private readonly IMutagenEnvironmentFactory _mutagenEnvironmentFactory;
    private readonly List<WeaponData> _weapons = new();
    private readonly List<AmmoData> _ammo = new();

    public WeaponsService(ILoadOrderService loadOrderService, IMutagenEnvironmentFactory mutagenEnvironmentFactory)
    {
        _loadOrderService = loadOrderService;
        _mutagenEnvironmentFactory = mutagenEnvironmentFactory ?? throw new ArgumentNullException(nameof(mutagenEnvironmentFactory));
    }

    // Mojibake repair was implemented here but is disabled per user request.
    // The original implementation is preserved below in a comment for reference.
    private static string FixMojibake(string s)
    {
        // Function intentionally disabled — return the original string unchanged.
        return s;

        /*
        // Try to repair common UTF-8 <-> Latin1 mojibake by reinterpreting the string
        // If the string contains suspicious characters (e.g. Ã, Â, å) attempt ISO-8859-1 -> UTF8 conversion.
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
        */
    }

    // Safely obtain a display string from a Mutagen translated string (ITranslatedStringGetter).
    // Preference order:
    // 1) TargetLanguage (.String)
    // 2) Japanese
    // 3) English
    // 4) First non-empty entry found
    private static string GetBestTranslatedString(ITranslatedStringGetter? t)
    {
        if (t == null)
            return string.Empty;

        // Prefer explicit language lookups before trusting the TargetLanguage (.String),
        // because TargetLanguage may be set to a codepage that results in mojibake.
        if (t.TryLookup(Language.Japanese, out var jap) && !string.IsNullOrEmpty(jap))
            return jap;

        if (t.TryLookup(Language.English, out var eng) && !string.IsNullOrEmpty(eng))
            return eng;

        // If no explicit JP/EN entry, fall back to TargetLanguage (.String)
        if (!string.IsNullOrEmpty(t.String))
            return t.String;

        // Finally, fall back to the first available non-empty translation
        foreach (var kv in t)
        {
            if (!string.IsNullOrEmpty(kv.Value))
                return kv.Value;
        }

        return string.Empty;
    }

    // Heuristic to detect strings that look like mojibake (common patterns: sequences like 'Ã', 'ã', 'å', etc.)
    private static bool IsLikelyMojibake(string s)
    {
        if (string.IsNullOrEmpty(s))
            return false;
        return s.IndexOf('Ã') >= 0 || s.IndexOf('ã') >= 0 || s.IndexOf('å') >= 0 || s.IndexOf('Â') >= 0 || s.IndexOf('�') >= 0;
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
                using var envRes = _mutagenEnvironmentFactory.Create();
                var weaponGetters = envRes.GetWinningWeaponOverrides();

                foreach (var weaponGetter in weaponGetters)
                {
                    try
                    {
                        dynamic wg = weaponGetter;
                        var weaponData = new WeaponData
                        {
                            FormKey = new Models.FormKey
                            {
                                PluginName = wg.FormKey.ModKey.FileName,
                                FormId = wg.FormKey.ID
                            },
                            EditorId = wg.EditorID ?? string.Empty,
                            Name = GetBestTranslatedString(wg.Name),
                            Description = GetBestTranslatedString(wg.Description),
                            WeaponType = "Unknown",
                            Damage = wg.BaseDamage,
                            FireRate = wg.AnimationAttackSeconds > 0
                                ? 60f / wg.AnimationAttackSeconds
                                : 0f
                        };

                        // If the chosen name looks like mojibake, dump all available translations for diagnosis.
                        try
                        {
                            if (IsLikelyMojibake(weaponData.Name) && wg.Name != null)
                                AppendTranslationsDump(wg.Name, weaponData.FormKey, "Weapon");
                            if (IsLikelyMojibake(weaponData.Description) && wg.Description != null)
                                AppendTranslationsDump(wg.Description, weaponData.FormKey, "WeaponDesc");
                        }
                        catch (Exception ex)
                        {
                            // Log via centralized logger rather than touching UI directly
                            AppLogger.Log($"WeaponsService: translation dump error: {ex.Message}", ex);
                        }

                        // Try to resolve ammunition via the record's FormLink using adapter-provided LinkCache
                        try
                        {
                            var linkCache = envRes.GetLinkCache();
                            var ammoResolved = MunitionAutoPatcher.Services.Implementations.LinkCacheHelper.TryResolveViaLinkCache(wg.Ammo, linkCache);
                            if (ammoResolved != null)
                            {
                                var ammoRecord = ammoResolved;
                                try
                                {
                                    var fk = ammoRecord.GetType().GetProperty("FormKey")?.GetValue(ammoRecord);
                                    var mk = fk?.GetType().GetProperty("ModKey")?.GetValue(fk);
                                    var pluginName = mk?.GetType().GetProperty("FileName")?.GetValue(mk)?.ToString() ?? string.Empty;
                                    var idObj = fk?.GetType().GetProperty("ID")?.GetValue(fk);
                                    uint id = 0;
                                    if (idObj is uint uu) id = uu;
                                    else if (idObj != null) id = Convert.ToUInt32(idObj);
                                    if (!string.IsNullOrEmpty(pluginName) && id != 0)
                                    {
                                        weaponData.DefaultAmmo = new Models.FormKey { PluginName = pluginName, FormId = id };
                                        try { weaponData.DefaultAmmoName = ammoRecord.GetType().GetProperty("EditorID")?.GetValue(ammoRecord)?.ToString() ?? string.Empty; } catch { }

                                        var key = $"{weaponData.DefaultAmmo.PluginName}:{weaponData.DefaultAmmo.FormId:X8}";
                                        if (!seen.Contains(key))
                                        {
                                            seen.Add(key);
                                            _ammo.Add(new AmmoData
                                            {
                                                FormKey = new Models.FormKey { PluginName = weaponData.DefaultAmmo.PluginName, FormId = weaponData.DefaultAmmo.FormId },
                                                Name = weaponData.DefaultAmmoName ?? string.Empty,
                                                EditorId = ammoRecord.GetType().GetProperty("EditorID")?.GetValue(ammoRecord)?.ToString() ?? string.Empty,
                                                Damage = 0,
                                                AmmoType = string.Empty
                                            });
                                        }
                                    }
                                }
                                catch (Exception ex) { AppLogger.Log($"WeaponsService: ammo resolved but processing failed: {ex.Message}", ex); }
                            }
                            else
                            {
                                // Fallback: try to read FormKey from the weaponGetter via reflection
                                try
                                {
                                    var ammoObj = wg.Ammo;
                                    var fk = ammoObj?.GetType().GetProperty("FormKey")?.GetValue(ammoObj);
                                    if (fk != null)
                                    {
                                        var mk = fk.GetType().GetProperty("ModKey")?.GetValue(fk);
                                        var pluginName = mk?.GetType().GetProperty("FileName")?.GetValue(mk)?.ToString() ?? string.Empty;
                                        var idObj = fk.GetType().GetProperty("ID")?.GetValue(fk);
                                        uint id = 0;
                                        if (idObj is uint uu) id = uu;
                                        else if (idObj != null) id = Convert.ToUInt32(idObj);
                                        if (!string.IsNullOrEmpty(pluginName) && id != 0)
                                        {
                                            weaponData.DefaultAmmo = new Models.FormKey { PluginName = pluginName, FormId = id };
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                        catch (Exception ex)
                        {
                            AppLogger.Log($"WeaponsService: ammo resolve error: {ex.Message}", ex);
                        }

                        _weapons.Add(weaponData);
                        weaponCount++;

                        if (weaponCount % 50 == 0)
                        {
                            progress?.Report($"{weaponCount}個の武器を処理中...");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Record details so the root cause is available in artifacts/ logs and Debug output
                        AppLogger.Log("WeaponsService: failed while parsing a weapon record", ex);
                        progress?.Report($"警告: 武器の解析に失敗しました: {ex.Message}");
                    }
                }

                progress?.Report($"抽出完了: {_weapons.Count}個の武器データを抽出しました");
                progress?.Report($"弾薬抽出(MO2 LinkCache 経由)完了: {_ammo.Count}個の弾薬を収集しました");
                try { WriteRecordsLog(); } catch (Exception ex) { AppLogger.Log("WeaponsService: WriteRecordsLog failed", ex); /* non-fatal for extraction */ }
                return _weapons;
                }
                catch (Exception ex)
                {
                    // GameEnvironment not available or initialization failed; log and fall back to data-folder based enumeration.
                    AppLogger.Log("WeaponsService: GameEnvironment detection failed, falling back to non-MO2 enumeration", ex);
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
                catch (Exception ex)
                {
                    AppLogger.Log($"WeaponsService: ammo fallback scan error: {ex.Message}", ex);
                }
            }

            progress?.Report($"弾薬抽出(武器参照から)完了: {_ammo.Count}個の弾薬を収集しました");
            try { WriteRecordsLog(); } catch (Exception ex) { AppLogger.Log("Suppressed exception (empty catch) in WeaponsService.WriteRecordsLog", ex); }
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

    // Write extracted weapon and ammo records to a timestamped log file inside the project (artifacts/)
    private void WriteRecordsLog()
    {
        try
        {
            var repoRoot = MunitionAutoPatcher.Utilities.RepoUtils.FindRepoRoot();
            var artifactsDir = Path.Combine(repoRoot, "artifacts");
            if (!Directory.Exists(artifactsDir))
                Directory.CreateDirectory(artifactsDir);

            var fileName = $"munition_records_{DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}.log";
            var path = Path.Combine(artifactsDir, fileName);

            using var sw = new StreamWriter(path, false, Encoding.UTF8);
            sw.WriteLine($"Generated: {DateTime.Now.ToString("u", CultureInfo.InvariantCulture)}");
            sw.WriteLine("# Weapons");
            sw.WriteLine("WeaponName\tEditorID\tFormKey\tDefaultAmmoName\tDefaultAmmoFormKey\tDamage\tFireRate");
            foreach (var w in _weapons)
            {
                try
                {
                    var formKeyStr = w.FormKey != null ? $"{w.FormKey.PluginName}:{w.FormKey.FormId:X8}" : string.Empty;
                    var defaultAmmoName = w.DefaultAmmoName ?? string.Empty;
                    var defaultAmmoKey = w.DefaultAmmo != null ? $"{w.DefaultAmmo.PluginName}:{w.DefaultAmmo.FormId:X8}" : string.Empty;
                    sw.WriteLine($"{w.Name}\t{w.EditorId}\t{formKeyStr}\t{defaultAmmoName}\t{defaultAmmoKey}\t{w.Damage.ToString(CultureInfo.InvariantCulture)}\t{w.FireRate.ToString(CultureInfo.InvariantCulture)}");
                }
                catch (Exception ex) { AppLogger.Log("WeaponsService: failed to write weapon row to records log", ex); }
            }

            sw.WriteLine();
            sw.WriteLine("# Ammo");
            sw.WriteLine("AmmoName\tEditorID\tFormKey\tDamage\tAmmoType");
            foreach (var a in _ammo)
            {
                try
                {
                    var aKey = a.FormKey != null ? $"{a.FormKey.PluginName}:{a.FormKey.FormId:X8}" : string.Empty;
                    sw.WriteLine($"{a.Name}\t{a.EditorId}\t{aKey}\t{a.Damage.ToString(CultureInfo.InvariantCulture)}\t{a.AmmoType}");
                }
                catch (Exception ex) { AppLogger.Log("WeaponsService: failed to write ammo row to records log", ex); }
            }

            sw.Flush();
            AppLogger.Log($"WeaponsService: records log written to: {path}");
        }
        catch (Exception ex)
        {
            // Do not throw — logging is only for diagnostics. Persist details for investigation.
            AppLogger.Log("WeaponsService: failed to write records log", ex);
        }
    }

        // RepoUtils.FindRepoRoot provides repository root lookup

    // Append all translation entries from an ITranslatedStringGetter to a diagnostic file.
    private void AppendTranslationsDump(ITranslatedStringGetter t, Models.FormKey? formKey, string tag)
    {
        try
        {
            var repoRoot = MunitionAutoPatcher.Utilities.RepoUtils.FindRepoRoot();
            var artifactsDir = Path.Combine(repoRoot, "artifacts");
            if (!Directory.Exists(artifactsDir))
                Directory.CreateDirectory(artifactsDir);

            var fileName = $"munition_translations_{DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}.log";
            var path = Path.Combine(artifactsDir, fileName);

            // Use append so multiple calls accumulate into the same file during a single run.
            using var sw = new StreamWriter(path, true, Encoding.UTF8);
            sw.WriteLine($"--- {DateTime.Now.ToString("u", CultureInfo.InvariantCulture)} [{tag}] {formKey?.PluginName}:{formKey?.FormId:X8}");
            foreach (var kv in t)
            {
                sw.WriteLine($"{kv.Key}\t{kv.Value}");
            }
            sw.WriteLine();
            sw.Flush();
            AppLogger.Log($"WeaponsService: translations dump appended to: {path}");
        }
        catch (Exception ex)
        {
            AppLogger.Log("WeaponsService: failed to append translations dump", ex);
        }
    }
}

    