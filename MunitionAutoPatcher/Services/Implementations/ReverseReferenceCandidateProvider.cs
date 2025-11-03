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
            var collections = context.Environment.EnumerateRecordCollections();

            foreach (var col in collections)
            {
                var collectionName = col.Name;
                var items = col.Items;
                if (items == null) continue;

                foreach (var rec in items)
                {
                    try
                    {
                        context.CancellationToken.ThrowIfCancellationRequested();

                        if (rec == null) continue;

                        // Get record FormKey
                        if (!_mutagenAccessor.TryGetPluginAndIdFromRecord(rec, out var recPlugin, out var recId))
                            continue;

                        // Skip excluded plugins
                        if (context.ExcludedPlugins.Contains(recPlugin))
                            continue;

                        // Inspect public properties for FormLink fields that reference weapons
                        var props = rec.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                        foreach (var prop in props)
                        {
                            try
                            {
                                var propValue = prop.GetValue(rec);
                                if (propValue == null) continue;

                                // Check if this is a FormLink with a FormKey property
                                var formKeyProp = propValue.GetType().GetProperty("FormKey");
                                if (formKeyProp == null) continue;

                                var formKey = formKeyProp.GetValue(propValue);
                                if (formKey == null) continue;

                                // Extract ModKey and ID from the FormKey
                                if (!TryExtractFormKeyInfo(formKey, out var plugin, out var id))
                                    continue;

                                // Check if this references a weapon
                                if (!weaponKeys.Contains((plugin, id)))
                                    continue;

                                // Found a record that references a weapon
                                var recEditorId = _mutagenAccessor.GetEditorId(rec);

                                // Try to detect ammo references in other properties
                                var detectedAmmoKey = TryDetectAmmoReference(rec, prop, plugin, id);

                                // Find weapon EditorID
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
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug(ex, "Error scanning property {PropName} on record", prop.Name);
                            }
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
            var idObj = formKey.GetType().GetProperty("ID")?.GetValue(formKey);
            
            plugin = modKey?.GetType().GetProperty("FileName")?.GetValue(modKey)?.ToString() ?? string.Empty;
            
            if (idObj is uint ui)
                id = ui;
            else if (idObj != null)
                id = Convert.ToUInt32(idObj);

            return !string.IsNullOrEmpty(plugin) && id != 0;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to extract FormKey info");
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
