using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins.Cache;

namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// Provider that extracts candidates from ConstructibleObject (COBJ) records.
/// Uses IMutagenAccessor for type-safe Mutagen API access (constitution Section 2.1).
/// </summary>
public class CobjCandidateProvider : ICandidateProvider
{
    private readonly IMutagenAccessor _mutagenAccessor;
    private readonly ILogger<CobjCandidateProvider> _logger;

    public CobjCandidateProvider(
        IMutagenAccessor mutagenAccessor,
        ILogger<CobjCandidateProvider> logger)
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
            _logger.LogInformation("Extracting COBJ candidates via IMutagenAccessor");
            context.Progress?.Report("COBJ 候補を抽出しています...");

            if (context.Environment == null)
            {
                _logger.LogWarning("Environment is null, cannot extract COBJ candidates");
                return results;
            }

            context.CancellationToken.ThrowIfCancellationRequested();

            // Type-safe: Get strongly-typed COBJ and Weapon collections
            var cobjs = _mutagenAccessor.GetWinningConstructibleObjectOverridesTyped(context.Environment);
            var allWeapons = _mutagenAccessor.GetWinningWeaponOverridesTyped(context.Environment).ToList();

            // Build weapon key lookup for fast matching
            var weaponLookup = BuildWeaponLookup(allWeapons);

            int processed = 0;
            foreach (var cobj in cobjs)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var candidate = ProcessCobj(cobj, allWeapons, weaponLookup, context);
                    if (candidate != null)
                    {
                        results.Add(candidate);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error processing COBJ {EditorId}", cobj.EditorID ?? "unknown");
                }

                processed++;
                if (processed % 500 == 0)
                {
                    _logger.LogDebug("Processed {Count} COBJs", processed);
                }
            }

            _logger.LogInformation("Extracted {Count} COBJ candidates from {Total} records", results.Count, processed);
            context.Progress?.Report($"COBJ から {results.Count} 件の候補を抽出しました");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("COBJ extraction was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract COBJ candidates");
            context.Progress?.Report($"警告: COBJ 候補の抽出中にエラーが発生しました: {ex.Message}");
        }

        return results;
    }

    /// <summary>
    /// Builds a lookup dictionary for weapons by (plugin, formId) for O(1) access.
    /// </summary>
    private Dictionary<(string Plugin, uint Id), IWeaponGetter> BuildWeaponLookup(List<IWeaponGetter> weapons)
    {
        var lookup = new Dictionary<(string Plugin, uint Id), IWeaponGetter>();

        foreach (var weapon in weapons)
        {
            try
            {
                var plugin = weapon.FormKey.ModKey.FileName.ToString();
                var id = weapon.FormKey.ID;
                var key = (plugin.ToLowerInvariant(), id);

                // First wins (WinningOverrides order)
                if (!lookup.ContainsKey(key))
                {
                    lookup[key] = weapon;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error building weapon lookup entry");
            }
        }

        return lookup;
    }

    /// <summary>
    /// Processes a single COBJ record and creates a candidate if it references a weapon.
    /// </summary>
    private OmodCandidate? ProcessCobj(
        IConstructibleObjectGetter cobj,
        List<IWeaponGetter> allWeapons,
        Dictionary<(string Plugin, uint Id), IWeaponGetter> weaponLookup,
        ExtractionContext context)
    {
        // Type-safe: Check if CreatedObject is null
        if (cobj.CreatedObject.IsNull)
            return null;

        var createdFormKey = cobj.CreatedObject.FormKey;
        var createdPlugin = createdFormKey.ModKey.FileName.ToString();
        var createdId = createdFormKey.ID;

        // Check exclusion by COBJ source plugin
        var cobjPlugin = cobj.FormKey.ModKey.FileName.ToString();
        if (context.ExcludedPlugins.Contains(cobjPlugin))
            return null;

        // Check if this creates a weapon (type-safe lookup)
        var lookupKey = (createdPlugin.ToLowerInvariant(), createdId);
        if (!weaponLookup.TryGetValue(lookupKey, out var matchingWeapon))
        {
            // CreatedObject is not a weapon, skip
            return null;
        }

        // Type-safe: Extract weapon's ammo reference
        Models.FormKey? candidateAmmo = null;
        string candidateAmmoEditorId = string.Empty;
        string candidateAmmoName = string.Empty;

        if (!matchingWeapon.Ammo.IsNull)
        {
            var ammoFormKey = matchingWeapon.Ammo.FormKey;
            candidateAmmo = new Models.FormKey
            {
                PluginName = ammoFormKey.ModKey.FileName.ToString(),
                FormId = ammoFormKey.ID
            };

            // Try to resolve ammo name via LinkCache
            if (context.FormLinkCache != null)
            {
                try
                {
                    if (context.FormLinkCache.TryResolve<IAmmunitionGetter>(ammoFormKey, out var ammoRecord))
                    {
                        candidateAmmoEditorId = ammoRecord.EditorID ?? string.Empty;
                        candidateAmmoName = ammoRecord.Name?.ToString() ?? string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to resolve ammo record {FormKey}", ammoFormKey);
                }
            }
        }

        return new OmodCandidate
        {
            CandidateType = "COBJ",
            CandidateFormKey = new Models.FormKey
            {
                PluginName = cobjPlugin,
                FormId = cobj.FormKey.ID
            },
            CandidateEditorId = cobj.EditorID ?? string.Empty,
            BaseWeapon = new Models.FormKey
            {
                PluginName = createdPlugin,
                FormId = createdId
            },
            BaseWeaponEditorId = matchingWeapon.EditorID ?? string.Empty,
            CandidateAmmo = candidateAmmo,
            CandidateAmmoEditorId = candidateAmmoEditorId,
            CandidateAmmoName = candidateAmmoName,
            SourcePlugin = cobjPlugin,
            Notes = $"COBJ source: {cobjPlugin}:{cobj.FormKey.ID:X8} -> Weapon: {createdPlugin}:{createdId:X8}",
            SuggestedTarget = "CreatedWeapon"
        };
    }
}
