using Microsoft.Extensions.Logging;
using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Interfaces;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins.Cache;
using MutagenFormKey = Mutagen.Bethesda.Plugins.FormKey;

namespace MunitionAutoPatcher.Services.Implementations
{
    /// <summary>
    /// Type-safe weapon data extractor using IMutagenAccessor (constitution Section 2.1).
    /// Extracts COBJ candidates that reference weapons.
    /// </summary>
    public class WeaponDataExtractor : IWeaponDataExtractor
    {
        private readonly IMutagenAccessor _mutagenAccessor;
        private readonly ILogger<WeaponDataExtractor> _logger;

        public WeaponDataExtractor(
            IMutagenAccessor mutagenAccessor,
            ILogger<WeaponDataExtractor> logger)
        {
            _mutagenAccessor = mutagenAccessor ?? throw new ArgumentNullException(nameof(mutagenAccessor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<List<OmodCandidate>> ExtractAsync(IResourcedMutagenEnvironment env, HashSet<string> excluded, IProgress<string>? progress = null)
        {
            var results = new List<OmodCandidate>();

            try
            {
                _logger.LogInformation("WeaponDataExtractor: Starting extraction via IMutagenAccessor");
                progress?.Report("COBJ から武器データを抽出しています...");

                // Type-safe: Get strongly-typed collections via IMutagenAccessor
                var cobjs = _mutagenAccessor.GetWinningConstructibleObjectOverridesTyped(env);
                var allWeapons = _mutagenAccessor.GetWinningWeaponOverridesTyped(env).ToList();

                // Build weapon lookup for O(1) access
                var weaponLookup = BuildWeaponLookup(allWeapons);

                // Get LinkCache for ammo resolution
                var linkCache = _mutagenAccessor.BuildConcreteLinkCache(env);

                int processed = 0;
                foreach (var cobj in cobjs)
                {
                    try
                    {
                        var candidate = ProcessCobj(cobj, weaponLookup, linkCache, excluded);
                        if (candidate != null)
                        {
                            results.Add(candidate);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "WeaponDataExtractor: Error processing COBJ {EditorId}", cobj.EditorID ?? "unknown");
                    }

                    processed++;
                    if (processed % 500 == 0)
                    {
                        _logger.LogDebug("WeaponDataExtractor: Processed {Count} COBJs", processed);
                    }
                }

                _logger.LogInformation("WeaponDataExtractor: Extracted {Count} candidates from {Total} COBJs", results.Count, processed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WeaponDataExtractor: Failed to extract from ConstructibleObjects");
            }

            return Task.FromResult(results);
        }

        /// <summary>
        /// Builds a lookup dictionary for weapons by (plugin, formId).
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

                    if (!lookup.ContainsKey(key))
                    {
                        lookup[key] = weapon;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "WeaponDataExtractor: Error building weapon lookup entry");
                }
            }

            return lookup;
        }

        /// <summary>
        /// Processes a single COBJ and creates a candidate if it references a weapon.
        /// </summary>
        private OmodCandidate? ProcessCobj(
            IConstructibleObjectGetter cobj,
            Dictionary<(string Plugin, uint Id), IWeaponGetter> weaponLookup,
            ILinkCache? linkCache,
            HashSet<string> excluded)
        {
            // Type-safe: Check if CreatedObject is null
            if (cobj.CreatedObject.IsNull)
                return null;

            var createdFormKey = cobj.CreatedObject.FormKey;
            var createdPlugin = createdFormKey.ModKey.FileName.ToString();
            var createdId = createdFormKey.ID;

            // Check exclusion by COBJ source plugin
            var cobjPlugin = cobj.FormKey.ModKey.FileName.ToString();
            if (excluded.Contains(cobjPlugin))
                return null;

            // Check if this creates a weapon
            var lookupKey = (createdPlugin.ToLowerInvariant(), createdId);
            if (!weaponLookup.TryGetValue(lookupKey, out var matchingWeapon))
            {
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

                // Resolve ammo details via LinkCache
                if (linkCache != null)
                {
                    try
                    {
                        if (linkCache.TryResolve<IAmmunitionGetter>(ammoFormKey, out var ammoRecord))
                        {
                            candidateAmmoEditorId = ammoRecord.EditorID ?? string.Empty;
                            candidateAmmoName = ammoRecord.Name?.ToString() ?? string.Empty;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "WeaponDataExtractor: Failed to resolve ammo {FormKey}", ammoFormKey);
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
}

