using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Interfaces;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Strings;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Order;

using MunitionAutoPatcher.Utilities;

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
            var loadOrder = await _loadOrderService.GetLoadOrderAsync();

            if (loadOrder == null)
            {
                progress?.Report("エラー: ロードオーダーの取得に失敗しました");
                return _weapons;
            }

            return await Task.Run(() => ExtractWeaponsInternal(loadOrder, progress));
        }
        catch (Exception ex)
        {
            progress?.Report($"エラー: 武器データの抽出中にエラーが発生しました: {ex.Message}");
            return _weapons;
        }
    }

    private List<WeaponData> ExtractWeaponsInternal(ILoadOrder<IModListing<IFallout4ModGetter>> loadOrder, IProgress<string>? progress)
    {
        if (loadOrder.Count == 0)
        {
            progress?.Report("警告: ロードオーダーが空です。Mod Organizer 2 から起動するか、config/config.json の GameDataPath を正しい Fallout 4 の Data フォルダへ設定してください。");
        }

        try
        {
            _weapons.Clear();
            _ammo.Clear();
            int weaponCount = 0;

            // Try to use Mutagen's GameEnvironment (MO2) so we can resolve FormLinks via the env.LinkCache.
            // If that fails, fall back to the provided loadOrder's PriorityOrder enumeration.
            progress?.Report("プラグインから武器レコードを読み込んでいます...");

            // Track seen ammo keys while building _ammo so we don't duplicate entries.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Write an early snapshot so a log file exists even if the app terminates unexpectedly
            try { WriteRecordsSnapshot(); } catch (Exception ex) { AppLogger.Log("WeaponsService: initial snapshot write failed", ex); }

            try
            {
                using var envRes = _mutagenEnvironmentFactory.Create();
                try
                {
                    var dataPath = envRes.GetDataFolderPath()?.ToString() ?? "(null)";
                    var hasLinkCache = envRes.GetLinkCache() != null;
                    AppLogger.Log($"WeaponsService: Env DataFolderPath={dataPath}, LinkCache={(hasLinkCache ? "available" : "null")}");
                }
                catch { /* diagnostics only */ }

                var weaponGetters = envRes.GetWinningWeaponOverrides();

                foreach (var weaponGetter in weaponGetters)
                {
                    try
                    {
                        // Use reflection-safe helpers to extract FormKey and common properties.
                        // Dynamic binding can throw when the object doesn't expose expected members
                        // (e.g., in test/no-op environments), so prefer guarded extraction.
                        string pluginName = string.Empty;
                        uint formId = 0;
                        if (!MutagenReflectionHelpers.TryGetPluginAndIdFromRecord(weaponGetter, out pluginName, out formId))
                        {
                            // Skip weapons with invalid FormKeys instead of throwing
                            AppLogger.Log($"WeaponsService: skipping weapon record with missing or invalid FormKey (Type: {weaponGetter?.GetType().Name ?? "null"})");
                            continue;
                        }

                        string? editorId = null;
                        if (weaponGetter is IWeaponGetter typedWeapon)
                        {
                            try
                            {
                                editorId = typedWeapon.EditorID;
                            }
                            catch (Exception ex)
                            {
                                AppLogger.Log($"WeaponsService: typed EditorID access failed for {pluginName}:{formId:X8}", ex);
                            }
                        }

                        if (string.IsNullOrEmpty(editorId))
                        {
                            try
                            {
                                MutagenReflectionHelpers.TryGetPropertyValue<string>(weaponGetter, "EditorID", out editorId);
                            }
                            catch (Exception ex)
                            {
                                AppLogger.Log($"WeaponsService: reflection EditorID access failed for {pluginName}:{formId:X8}", ex);
                                editorId = null;
                            }
                        }

                        // Name / Description may be ITranslatedStringGetter or simple strings
                        object? nameObj = null;
                        object? descObj = null;
                        object? ammoObj = null;
                        string name = string.Empty;
                        string description = string.Empty;
                        try
                        {
                            MutagenReflectionHelpers.TryGetPropertyValue<object>(weaponGetter, "Name", out nameObj);
                            if (nameObj != null)
                            {
                                if (nameObj is ITranslatedStringGetter tname) name = GetBestTranslatedString(tname);
                                else name = nameObj.ToString() ?? string.Empty;
                            }
                        }
                        catch { name = string.Empty; }

                        try
                        {
                            MutagenReflectionHelpers.TryGetPropertyValue<object>(weaponGetter, "Description", out descObj);
                            if (descObj != null)
                            {
                                if (descObj is ITranslatedStringGetter tdesc) description = GetBestTranslatedString(tdesc);
                                else description = descObj.ToString() ?? string.Empty;
                            }
                        }
                        catch { description = string.Empty; }

                        try { MutagenReflectionHelpers.TryGetPropertyValue<object>(weaponGetter, "Ammo", out ammoObj); } catch { ammoObj = null; }

                        float damage = 0f;
                        try
                        {
                            if (MutagenReflectionHelpers.TryGetPropertyValue<object>(weaponGetter, "BaseDamage", out var bd) && bd != null)
                            {
                                damage = ToSingleFlexible(bd);
                            }
                            else if (MutagenReflectionHelpers.TryGetPropertyValue<object>(weaponGetter, "Data", out var wdata) && wdata != null)
                            {
                                if (MutagenReflectionHelpers.TryGetPropertyValue<object>(wdata, "BaseDamage", out var bd2) && bd2 != null)
                                {
                                    damage = ToSingleFlexible(bd2);
                                }
                                else if (MutagenReflectionHelpers.TryGetPropertyValue<object>(wdata, "Damage", out var bd3) && bd3 != null)
                                {
                                    damage = ToSingleFlexible(bd3);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            AppLogger.Log($"WeaponsService: damage extraction failed for {pluginName}:{formId:X8}", ex);
                            damage = 0f;
                        }

                        float fireRate = 0f;
                        try
                        {
                            float secs = 0f;
                            if (MutagenReflectionHelpers.TryGetPropertyValue<object>(weaponGetter, "AnimationAttackSeconds", out var aas) && aas != null)
                            {
                                secs = ToSingleFlexible(aas);
                            }
                            else if (MutagenReflectionHelpers.TryGetPropertyValue<object>(weaponGetter, "Data", out var wdata2) && wdata2 != null)
                            {
                                if (MutagenReflectionHelpers.TryGetPropertyValue<object>(wdata2, "AnimationAttackSeconds", out var aas2) && aas2 != null)
                                {
                                    secs = ToSingleFlexible(aas2);
                                }
                                else if (MutagenReflectionHelpers.TryGetPropertyValue<object>(wdata2, "AttackDelaySec", out var delay) && delay != null)
                                {
                                    secs = ToSingleFlexible(delay);
                                }
                            }
                            fireRate = secs > 0 ? 60f / secs : 0f;
                        }
                        catch (Exception ex)
                        {
                            AppLogger.Log($"WeaponsService: fire rate extraction failed for {pluginName}:{formId:X8}", ex);
                            fireRate = 0f;
                        }

                        var weaponData = new WeaponData
                        {
                            FormKey = new Models.FormKey
                            {
                                PluginName = pluginName,
                                FormId = formId
                            },
                            EditorId = editorId ?? string.Empty,
                            Name = name,
                            Description = description,
                            WeaponType = "Unknown",
                            Damage = damage,
                            FireRate = fireRate
                        };

                        // If the chosen name looks like mojibake, dump all available translations for diagnosis.
                        try
                        {
                            if (IsLikelyMojibake(weaponData.Name) && nameObj is ITranslatedStringGetter tnameObj)
                                AppendTranslationsDump(tnameObj, weaponData.FormKey, "Weapon");
                            if (IsLikelyMojibake(weaponData.Description) && descObj is ITranslatedStringGetter tdescObj)
                                AppendTranslationsDump(tdescObj, weaponData.FormKey, "WeaponDesc");
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
                            MunitionAutoPatcher.Services.Implementations.LinkResolver? resolver = linkCache != null ? new MunitionAutoPatcher.Services.Implementations.LinkResolver(linkCache) : null;
                            object? ammoResolved = null;
                            if (resolver != null)
                            {
                                try { if (ammoObj != null) resolver.TryResolve(ammoObj, out ammoResolved); else ammoResolved = null; } catch (Exception ex) { AppLogger.Log($"WeaponsService: resolver.TryResolve failed: {ex.Message}", ex); ammoResolved = null; }
                            }
                            else
                            {
                                try
                                {
                                    if (linkCache != null)
                                    {
                                        try
                                        {
                                            var tmpResolver = new MunitionAutoPatcher.Services.Implementations.LinkResolver(linkCache);
                                            try { if (ammoObj != null) tmpResolver.TryResolve(ammoObj, out ammoResolved); else ammoResolved = null; } catch (Exception ex) { AppLogger.Log($"WeaponsService: tmpResolver.TryResolve failed: {ex.Message}", ex); ammoResolved = null; }
                                        }
                                        catch (Exception ex) { AppLogger.Log($"WeaponsService: failed to create temporary LinkResolver: {ex.Message}", ex); ammoResolved = null; }
                                    }
                                    else
                                    {
                                        try { ammoResolved = MunitionAutoPatcher.Services.Implementations.LinkCacheHelper.TryResolveViaLinkCache(ammoObj, linkCache); } catch (Exception ex) { AppLogger.Log($"WeaponsService: LinkCache fallback failed: {ex.Message}", ex); ammoResolved = null; }
                                    }
                                }
                                catch (Exception ex) { AppLogger.Log($"WeaponsService: unexpected resolution error: {ex.Message}", ex); ammoResolved = null; }
                            }

                            if (ammoResolved != null)
                            {
                                var ammoRecord = ammoResolved;
                                try
                                {
                                    if (MutagenReflectionHelpers.TryGetPluginAndIdFromRecord(ammoRecord, out string ammoPlugin, out uint id))
                                    {
                                        weaponData.DefaultAmmo = new Models.FormKey { PluginName = ammoPlugin, FormId = id };

                                        if (MutagenReflectionHelpers.TryGetPropertyValue<string>(ammoRecord, "EditorID", out string? ammoEditorId) && !string.IsNullOrEmpty(ammoEditorId))
                                        {
                                            weaponData.DefaultAmmoName = ammoEditorId!;
                                        }

                                        var key = $"{ammoPlugin}:{id:X8}";
                                        if (!seen.Contains(key))
                                        {
                                            seen.Add(key);

                                            MutagenReflectionHelpers.TryGetPropertyValue<string>(ammoRecord, "EditorID", out string? ammoEditorId2);
                                            _ammo.Add(new AmmoData
                                            {
                                                FormKey = new Models.FormKey { PluginName = ammoPlugin, FormId = id },
                                                Name = weaponData.DefaultAmmoName ?? string.Empty,
                                                EditorId = ammoEditorId2 ?? string.Empty,
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
                                // Fallback: try to read FormKey directly from the weapon's Ammo property via reflection.
                                // Even if we can't resolve the full record through LinkCache, we can still surface the ammo FormKey
                                // in outputs and collect a minimal ammo entry so the Ammo table isn't empty.
                                try
                                {
                                    if (ammoObj != null && MutagenReflectionHelpers.TryGetPluginAndIdFromRecord(ammoObj, out string ammoPluginFallback, out uint ammoFormId))
                                    {
                                        weaponData.DefaultAmmo = new Models.FormKey { PluginName = ammoPluginFallback, FormId = ammoFormId };

                                        // Try to read an EditorID directly off the object (works if it's already a record/overlay)
                                        try
                                        {
                                            if (MutagenReflectionHelpers.TryGetPropertyValue<string>(ammoObj, "EditorID", out string? eid) && !string.IsNullOrWhiteSpace(eid))
                                            {
                                                weaponData.DefaultAmmoName = eid;
                                            }
                                        }
                                        catch { /* best-effort only */ }

                                        // Also add to the _ammo set minimally so records log has entries even when LinkCache resolution isn't available
                                        var key = $"{ammoPluginFallback}:{ammoFormId:X8}";
                                        if (!seen.Contains(key))
                                        {
                                            seen.Add(key);
                                            _ammo.Add(new AmmoData
                                            {
                                                FormKey = new Models.FormKey { PluginName = ammoPluginFallback, FormId = ammoFormId },
                                                Name = weaponData.DefaultAmmoName ?? string.Empty,
                                                EditorId = weaponData.DefaultAmmoName ?? string.Empty,
                                                Damage = 0,
                                                AmmoType = string.Empty
                                            });
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

                        // Periodically write a snapshot to avoid losing diagnostics on unexpected exit
                        if (weaponCount % 200 == 0)
                        {
                            try { WriteRecordsSnapshot(); } catch (Exception ex) { AppLogger.Log("WeaponsService: periodic snapshot write failed", ex); }
                        }

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

    // Write a snapshot of current records to a stable filename so a log exists even if the app exits early.
    // This overwrites the same file each time to avoid clutter.
    private void WriteRecordsSnapshot()
    {
        try
        {
            var repoRoot = MunitionAutoPatcher.Utilities.RepoUtils.FindRepoRoot();
            var artifactsDir = Path.Combine(repoRoot, "artifacts");
            if (!Directory.Exists(artifactsDir))
                Directory.CreateDirectory(artifactsDir);

            var path = Path.Combine(artifactsDir, "munition_records_latest.log");
            using var sw = new StreamWriter(path, false, Encoding.UTF8);
            sw.WriteLine($"Generated (snapshot): {DateTime.Now.ToString("u", CultureInfo.InvariantCulture)}");
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
                catch (Exception ex) { AppLogger.Log("WeaponsService: failed to write weapon row to snapshot log", ex); }
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
                catch (Exception ex) { AppLogger.Log("WeaponsService: failed to write ammo row to snapshot log", ex); }
            }

            sw.Flush();
            AppLogger.Log($"WeaponsService: snapshot records log written to: {path}");
        }
        catch (Exception ex)
        {
            AppLogger.Log("WeaponsService: failed to write snapshot records log", ex);
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

    // Flexible numeric conversion: supports common numeric primitives, boxed types, strings, and value wrappers exposing Value
    private static float ToSingleFlexible(object? v)
    {
        if (v == null) return 0f;
        try
        {
            switch (v)
            {
                case float f: return f;
                case double d: return (float)d;
                case decimal m: return (float)m;
                case byte b: return b;
                case sbyte sb: return sb;
                case short s: return s;
                case ushort us: return us;
                case int i: return i;
                case uint ui: return ui;
                case long l: return l;
                case ulong ul:
                    {
                        var d = (double)ul;
                        return d > float.MaxValue ? float.MaxValue : (float)d;
                    }
                case string ss:
                    if (float.TryParse(ss, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fp)) return fp;
                    if (float.TryParse(ss, out fp)) return fp;
                    break;
            }

            // Try Value property commonly used by wrapper structs
            try
            {
                if (MutagenReflectionHelpers.TryGetPropertyValue<object>(v, "Value", out var inner) && inner != null)
                    return ToSingleFlexible(inner);
            }
            catch { /* ignore */ }

            return Convert.ToSingle(v, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch
        {
            try { return Convert.ToSingle(v); } catch { return 0f; }
        }
    }
}

