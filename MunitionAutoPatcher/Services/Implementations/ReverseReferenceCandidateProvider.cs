using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// Provider that discovers candidates through reverse-reference scanning of all record collections.
/// </summary>
public class ReverseReferenceCandidateProvider : ICandidateProvider
{
    private readonly IMutagenAccessor _mutagenAccessor;
    private readonly ILogger<ReverseReferenceCandidateProvider> _logger;

    public ReverseReferenceCandidateProvider(
        IMutagenAccessor mutagenAccessor,
        ILogger<ReverseReferenceCandidateProvider> logger)
    {
        _mutagenAccessor = mutagenAccessor ?? throw new ArgumentNullException(nameof(mutagenAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public IEnumerable<OmodCandidate> GetCandidates(ExtractionContext context)
    {
        var results = new List<OmodCandidate>();

        try
        {
            if (context.Environment == null)
            {
                _logger.LogWarning("Environment is null, cannot perform reverse-reference scan");
                return results;
            }

            _logger.LogInformation("Starting reverse-reference scan");
            context.Progress?.Report("逆参照スキャンを実行しています...");

            var weaponKeys = context.WeaponKeySet;
            var typedCollections = context.Environment.EnumerateRecordCollectionsTyped();

            foreach (var col in typedCollections)
            {
                var collectionName = col.Name;
                var items = col.Items;
                if (items == null) continue;

                int scanned = 0;

                foreach (var rec in items)
                {
                    try
                    {
                        context.CancellationToken.ThrowIfCancellationRequested();

                        if (rec == null) continue;

                        // Get record FormKey (plugin + id) from typed getter
                        var recPlugin = rec.FormKey.ModKey.FileName.ToString();
                        var recId = (uint)rec.FormKey.ID;

                        // Skip excluded plugins
                        if (context.ExcludedPlugins.Contains(recPlugin))
                            continue;

                        // Inspect public properties that have a FormKey property on the TYPE (avoid materializing values unnecessarily)
                        // Use reflection only for property iteration; rec is typed IMajorRecordGetter
                        var props = rec.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                        foreach (var prop in props)
                        {
                            try
                            {
                                // Skip indexers
                                if (prop.GetIndexParameters().Length > 0) continue;

                                var propType = prop.PropertyType;
                                var propValue = prop.GetValue(rec);
                                if (propValue == null) continue;

                                // Case 1: Direct FormLink-like property (inspect runtime value for FormKey)
                                var formKeyPropOnValue = propValue.GetType().GetProperty("FormKey");
                                if (formKeyPropOnValue != null)
                                {
                                    var formKey = formKeyPropOnValue.GetValue(propValue);
                                    if (formKey != null && TryExtractFormKeyInfo(formKey, out var plugin, out var id))
                                    {
                                        var pluginKey = plugin.ToLowerInvariant();
                                        if (weaponKeys.Contains((pluginKey, id)))
                                        {
                                            var recEditorId = _mutagenAccessor.GetEditorId(rec);
                                            var detectedAmmoKey = TryDetectAmmoReference(rec, prop, plugin, id);
                                            var weaponEditorId = FindWeaponEditorId(context.AllWeapons, plugin, id);
                                            var candidate = new OmodCandidate
                                            {
                                                CandidateType = collectionName,
                                                CandidateFormKey = new FormKey { PluginName = recPlugin, FormId = recId },
                                                CandidateEditorId = recEditorId,
                                                BaseWeapon = new FormKey { PluginName = plugin, FormId = id },
                                                BaseWeaponEditorId = weaponEditorId,
                                                CandidateAmmo = detectedAmmoKey,
                                                CandidateAmmoName = string.Empty,
                                                SourcePlugin = recPlugin,
                                                Notes = $"Reference found in {collectionName}.{prop.Name} -> {plugin}:{id:X8}" +
                                                       (detectedAmmoKey != null ? $";DetectedAmmo={detectedAmmoKey.PluginName}:{detectedAmmoKey.FormId:X8}" : string.Empty),
                                                SuggestedTarget = "Reference"
                                            };
                                            results.Add(candidate);
                                            continue;
                                        }
                                    }
                                }

                                // Case 2: Enumerable of FormLink-like values
                                if (propValue is System.Collections.IEnumerable enumerable)
                                {
                                    int inspected = 0;
                                    foreach (var elem in enumerable)
                                    {
                                        if (elem == null) continue;
                                        inspected++;
                                        if (inspected > 16) break; // cap for performance

                                        var elemType = elem.GetType();
                                        var fkProp = elemType.GetProperty("FormKey");
                                        if (fkProp == null) continue;
                                        var fk = fkProp.GetValue(elem);
                                        if (fk == null) continue;
                                        if (!TryExtractFormKeyInfo(fk, out var plugin2, out var id2)) continue;

                                        var pluginKey2 = plugin2.ToLowerInvariant();
                                        if (!weaponKeys.Contains((pluginKey2, id2))) continue;

                                        var recEditorId2 = _mutagenAccessor.GetEditorId(rec);
                                        var detectedAmmoKey2 = TryDetectAmmoReference(rec, prop, plugin2, id2);
                                        var weaponEditorId2 = FindWeaponEditorId(context.AllWeapons, plugin2, id2);
                                        var candidate2 = new OmodCandidate
                                        {
                                            CandidateType = collectionName,
                                            CandidateFormKey = new FormKey { PluginName = recPlugin, FormId = recId },
                                            CandidateEditorId = recEditorId2,
                                            BaseWeapon = new FormKey { PluginName = plugin2, FormId = id2 },
                                            BaseWeaponEditorId = weaponEditorId2,
                                            CandidateAmmo = detectedAmmoKey2,
                                            CandidateAmmoName = string.Empty,
                                            SourcePlugin = recPlugin,
                                            Notes = $"Reference found in {collectionName}.{prop.Name} (enumerable) -> {plugin2}:{id2:X8}" +
                                                   (detectedAmmoKey2 != null ? $";DetectedAmmo={detectedAmmoKey2.PluginName}:{detectedAmmoKey2.FormId:X8}" : string.Empty),
                                            SuggestedTarget = "Reference"
                                        };
                                        results.Add(candidate2);
                                        break; // one hit per enumerable is enough
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug(ex, "Error scanning property {PropName} on record", prop.Name);
                            }
                        }

                        scanned++;
                        if (scanned % 2000 == 0)
                        {
                            _logger.LogDebug("Reverse-scan progress {Collection}: {Count} records processed", collectionName, scanned);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error scanning record in collection {Collection}", collectionName);
                    }
                }
            }

            _logger.LogInformation("Reverse-reference scan found {Count} candidates", results.Count);
            context.Progress?.Report($"逆参照スキャンで {results.Count} 件の候補を検出しました");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Reverse-reference scan was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reverse-reference scan encountered an error (non-fatal)");
            context.Progress?.Report($"注意: 逆参照スキャン中に例外が発生しました（無視します）: {ex.Message}");
        }

        return results;
    }

    private bool TryExtractFormKeyInfo(object formKey, out string plugin, out uint id)
    {
        plugin = string.Empty;
        id = 0;

        try
        {
            var modKey = formKey.GetType().GetProperty("ModKey")?.GetValue(formKey);
            if (modKey == null) return false;

            var idObj = formKey.GetType().GetProperty("ID")?.GetValue(formKey);
            if (idObj == null) return false;

            var fileNameObj = modKey.GetType().GetProperty("FileName")?.GetValue(modKey);
            plugin = (fileNameObj?.ToString() ?? modKey.ToString()) ?? string.Empty;

            if (idObj is uint ui)
                id = ui;
            else
                id = Convert.ToUInt32(idObj);

            bool success = !string.IsNullOrEmpty(plugin) && !plugin.Equals("Null", StringComparison.OrdinalIgnoreCase) && id != 0;

            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("TryExtractFormKeyInfo: fk={fk}, plugin={p}, id={id:X8}, success={s}", formKey.ToString(), plugin, id, success);
            }

            return success;
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Trace))
                _logger.LogTrace(ex, "Failed to extract FormKey info");
            return false;
        }
    }

    private FormKey? TryDetectAmmoReference(object record, System.Reflection.PropertyInfo weaponProp, string weaponPlugin, uint weaponId)
    {
        try
        {
            var allProps = record.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in allProps)
            {
                try
                {
                    // Skip the weapon property itself
                    if (prop.Name == weaponProp.Name) continue;

                    var propValue = prop.GetValue(record);
                    if (propValue == null) continue;

                    var formKeyProp = propValue.GetType().GetProperty("FormKey");
                    if (formKeyProp == null) continue;

                    var formKey = formKeyProp.GetValue(propValue);
                    if (formKey == null) continue;

                    if (TryExtractFormKeyInfo(formKey, out var plugin, out var id))
                    {
                        // If it's not the weapon itself, consider it as potential ammo
                        if (!(string.Equals(plugin, weaponPlugin, StringComparison.OrdinalIgnoreCase) && id == weaponId))
                        {
                            return new FormKey { PluginName = plugin, FormId = id };
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error detecting ammo reference in property {PropName}", prop.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error scanning for ammo references");
        }

        return null;
    }

    private string FindWeaponEditorId(List<object> allWeapons, string plugin, uint id)
    {
        try
        {
            var weapon = allWeapons.FirstOrDefault(w =>
            {
                try
                {
                    if (_mutagenAccessor.TryGetPluginAndIdFromRecord(w, out var wPlugin, out var wId))
                    {
                        return string.Equals(wPlugin, plugin, StringComparison.OrdinalIgnoreCase) && wId == id;
                    }
                }
                catch { }
                return false;
            });

            return weapon != null ? _mutagenAccessor.GetEditorId(weapon) : string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error finding weapon EditorID for {Plugin}:{Id:X8}", plugin, id);
            return string.Empty;
        }
    }
}
