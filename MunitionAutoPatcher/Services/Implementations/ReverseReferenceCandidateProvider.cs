using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins.Records;
using System.Reflection;

namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// Provider that discovers candidates through reverse-reference scanning of all record collections.
/// Uses IMutagenAccessor where possible (constitution Section 2.1).
/// Note: Some reflection is unavoidable for generic property scanning across unknown record types.
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

            _logger.LogInformation("Starting reverse-reference scan via IMutagenAccessor");
            context.Progress?.Report("逆参照スキャンを実行しています...");

            var weaponKeys = context.WeaponKeySet;
            
            // Type-safe: Use typed collections from environment
            var typedCollections = context.Environment.EnumerateRecordCollectionsTyped();

            // Build weapon lookup for O(1) EditorID retrieval
            var weaponEditorIdLookup = BuildWeaponEditorIdLookup(context.AllWeapons);

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

                        // Type-safe: IMajorRecordGetter provides FormKey directly
                        var recPlugin = rec.FormKey.ModKey.FileName.ToString();
                        var recId = rec.FormKey.ID;

                        // Skip excluded plugins
                        if (context.ExcludedPlugins.Contains(recPlugin))
                            continue;

                        // Scan properties for weapon references
                        // Note: Property iteration requires reflection for generic record types
                        var candidates = ScanRecordForWeaponReferences(
                            rec, collectionName, recPlugin, recId,
                            weaponKeys, weaponEditorIdLookup);

                        results.AddRange(candidates);

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

    /// <summary>
    /// Builds a lookup dictionary for weapon EditorIDs by (plugin, formId).
    /// </summary>
    private Dictionary<(string Plugin, uint Id), string> BuildWeaponEditorIdLookup(List<object> allWeapons)
    {
        var lookup = new Dictionary<(string Plugin, uint Id), string>();

        foreach (var weapon in allWeapons)
        {
            try
            {
                if (_mutagenAccessor.TryGetPluginAndIdFromRecord(weapon, out var plugin, out var id))
                {
                    var key = (plugin.ToLowerInvariant(), id);
                    if (!lookup.ContainsKey(key))
                    {
                        lookup[key] = _mutagenAccessor.GetEditorId(weapon);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error building weapon EditorID lookup entry");
            }
        }

        return lookup;
    }

    /// <summary>
    /// Scans a record's properties for weapon references.
    /// Note: Uses reflection for property iteration (unavoidable for generic scanning).
    /// </summary>
    private IEnumerable<OmodCandidate> ScanRecordForWeaponReferences(
        IMajorRecordGetter rec,
        string collectionName,
        string recPlugin,
        uint recId,
        HashSet<(string Plugin, uint Id)> weaponKeys,
        Dictionary<(string Plugin, uint Id), string> weaponEditorIdLookup)
    {
        var results = new List<OmodCandidate>();

        try
        {
            // Note: Reflection is used here to iterate over unknown property types.
            // This is necessary for generic reverse-reference scanning.
            var props = rec.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in props)
            {
                try
                {
                    // Skip indexers
                    if (prop.GetIndexParameters().Length > 0) continue;

                    var propValue = prop.GetValue(rec);
                    if (propValue == null) continue;

                    // Case 1: Direct FormLink-like property
                    if (TryExtractWeaponReference(propValue, weaponKeys, out var plugin, out var id))
                    {
                        var candidate = CreateCandidate(
                            rec, collectionName, recPlugin, recId,
                            prop.Name, plugin, id, false,
                            weaponEditorIdLookup, prop);

                        if (candidate != null)
                        {
                            results.Add(candidate);
                            continue;
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
                            if (inspected > 16) break; // Performance cap

                            if (TryExtractWeaponReference(elem, weaponKeys, out var plugin2, out var id2))
                            {
                                var candidate = CreateCandidate(
                                    rec, collectionName, recPlugin, recId,
                                    prop.Name, plugin2, id2, true,
                                    weaponEditorIdLookup, prop);

                                if (candidate != null)
                                {
                                    results.Add(candidate);
                                    break; // One hit per enumerable
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error scanning property {PropName} on record", prop.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error scanning record properties");
        }

        return results;
    }

    /// <summary>
    /// Tries to extract a weapon reference from a property value.
    /// Uses IMutagenAccessor.TryGetPluginAndIdFromRecord for extraction.
    /// </summary>
    private bool TryExtractWeaponReference(
        object propValue,
        HashSet<(string Plugin, uint Id)> weaponKeys,
        out string plugin,
        out uint id)
    {
        plugin = string.Empty;
        id = 0;

        try
        {
            // Try to get FormKey from the value
            var formKeyProp = propValue.GetType().GetProperty("FormKey");
            if (formKeyProp == null) return false;

            var formKey = formKeyProp.GetValue(propValue);
            if (formKey == null) return false;

            // Use IMutagenAccessor for type-safe extraction
            if (!_mutagenAccessor.TryGetPluginAndIdFromRecord(formKey, out plugin, out id))
                return false;

            // Check if this is a weapon
            var lookupKey = (plugin.ToLowerInvariant(), id);
            return weaponKeys.Contains(lookupKey);
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Trace))
                _logger.LogTrace(ex, "Failed to extract weapon reference");
            return false;
        }
    }

    /// <summary>
    /// Creates an OmodCandidate from a detected weapon reference.
    /// </summary>
    private OmodCandidate? CreateCandidate(
        IMajorRecordGetter rec,
        string collectionName,
        string recPlugin,
        uint recId,
        string propName,
        string weaponPlugin,
        uint weaponId,
        bool isEnumerable,
        Dictionary<(string Plugin, uint Id), string> weaponEditorIdLookup,
        PropertyInfo weaponProp)
    {
        var recEditorId = _mutagenAccessor.GetEditorId(rec);
        var detectedAmmoKey = TryDetectAmmoReference(rec, weaponProp, weaponPlugin, weaponId);
        
        var lookupKey = (weaponPlugin.ToLowerInvariant(), weaponId);
        var weaponEditorId = weaponEditorIdLookup.TryGetValue(lookupKey, out var edId) ? edId : string.Empty;

        var enumerableNote = isEnumerable ? " (enumerable)" : string.Empty;

        return new OmodCandidate
        {
            CandidateType = collectionName,
            CandidateFormKey = new FormKey { PluginName = recPlugin, FormId = recId },
            CandidateEditorId = recEditorId,
            BaseWeapon = new FormKey { PluginName = weaponPlugin, FormId = weaponId },
            BaseWeaponEditorId = weaponEditorId,
            CandidateAmmo = detectedAmmoKey,
            CandidateAmmoName = string.Empty,
            SourcePlugin = recPlugin,
            Notes = $"Reference found in {collectionName}.{propName}{enumerableNote} -> {weaponPlugin}:{weaponId:X8}" +
                   (detectedAmmoKey != null ? $";DetectedAmmo={detectedAmmoKey.PluginName}:{detectedAmmoKey.FormId:X8}" : string.Empty),
            SuggestedTarget = "Reference"
        };
    }

    /// <summary>
    /// Tries to detect an ammo reference in the record (excluding the weapon property).
    /// </summary>
    private FormKey? TryDetectAmmoReference(object record, PropertyInfo weaponProp, string weaponPlugin, uint weaponId)
    {
        try
        {
            var allProps = record.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var prop in allProps)
            {
                try
                {
                    if (prop.Name == weaponProp.Name) continue;

                    var propValue = prop.GetValue(record);
                    if (propValue == null) continue;

                    var formKeyProp = propValue.GetType().GetProperty("FormKey");
                    if (formKeyProp == null) continue;

                    var formKey = formKeyProp.GetValue(propValue);
                    if (formKey == null) continue;

                    if (_mutagenAccessor.TryGetPluginAndIdFromRecord(formKey, out var plugin, out var id))
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
}
