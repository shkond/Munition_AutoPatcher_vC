using Microsoft.Extensions.Logging;
using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Interfaces;
using Mutagen.Bethesda.Environments;
using MunitionAutoPatcher.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using MutagenFormKey = Mutagen.Bethesda.Plugins.FormKey;

namespace MunitionAutoPatcher.Services.Implementations
{
    public class WeaponDataExtractor : IWeaponDataExtractor
    {
        private readonly ILogger<WeaponDataExtractor> _logger;

        public WeaponDataExtractor(ILogger<WeaponDataExtractor> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<List<OmodCandidate>> ExtractAsync(IResourcedMutagenEnvironment env, HashSet<string> excluded, IProgress<string>? progress = null)
        {
            var resultsLocal = new List<OmodCandidate>();
            try
            {
                var cobjs = env.GetWinningConstructibleObjectOverrides();
                var allWeapons = env.GetWinningWeaponOverridesTyped().Cast<object>().ToList();

                resultsLocal.AddRange(
                    cobjs.Select(cobj =>
                    {
                        try { return ProcessCobj(cobj, allWeapons, excluded, env); }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Suppressed exception in WeaponDataExtractor: processing COBJ candidate");
                            return null;
                        }
                    })
                    .Where(x => x != null)!
                    .Cast<OmodCandidate>()
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WeaponDataExtractor: failed while extracting from ConstructibleObjects");
            }

            return Task.FromResult(resultsLocal);
        }

        private OmodCandidate? ProcessCobj(object cobj, List<object> allWeapons, HashSet<string> excluded, IResourcedMutagenEnvironment env)
        {
            try
            {
                if (!TryGetCreatedObject(cobj, out var created) || created == null)
                    return null;

                try
                {
                    if (MutagenReflectionHelpers.TryGetPropertyValue<bool>(created, "IsNull", out var isNull) && isNull)
                        return null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Suppressed exception in WeaponDataExtractor: inspecting CreatedObject.IsNull");
                }

                string createdPlugin = string.Empty;
                uint createdId = 0u;
                try
                {
                    MutagenReflectionHelpers.TryGetPluginAndIdFromRecord(created, out createdPlugin, out createdId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Suppressed exception in WeaponDataExtractor: reading created object's plugin/id");
                }

                // Exclusion: prefer COBJ source plugin, else created plugin
                try
                {
                    var srcPlugin = string.Empty;
                    if (MutagenReflectionHelpers.TryGetPluginAndIdFromRecord(cobj, out var sp, out _))
                        srcPlugin = sp;

                    var checkPlugin = !string.IsNullOrEmpty(srcPlugin) ? srcPlugin : createdPlugin;
                    if (!string.IsNullOrEmpty(checkPlugin) && excluded.Contains(checkPlugin))
                        return null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Suppressed exception in WeaponDataExtractor: skip-by-excluded check");
                }

                var possibleWeapon = TryFindMatchingWeapon(allWeapons, createdPlugin, createdId);

                string baseWeaponEditorId = string.Empty;
                MutagenFormKey? createdAmmoKey = null;
                if (possibleWeapon != null)
                {
                    MutagenReflectionHelpers.TryGetPropertyValue<string>(possibleWeapon, "EditorID", out var wEdid);
                    baseWeaponEditorId = wEdid ?? string.Empty;
                    TryExtractAmmoKey(possibleWeapon, out createdAmmoKey);
                }

                string candidateAmmoEditorId = string.Empty;
                string candidateAmmoName = string.Empty;

                if (createdAmmoKey != null)
                {
                    var linkCache = env.GetLinkCache()?.LinkCache;
                    if (linkCache != null)
                    {
                        if (linkCache.TryResolve<IAmmunitionGetter>(createdAmmoKey.Value, out var ammoRecord))
                        {
                            candidateAmmoEditorId = ammoRecord.EditorID ?? string.Empty;
                            candidateAmmoName = ammoRecord.Name?.ToString() ?? string.Empty;
                        }
                    }
                }

                MutagenReflectionHelpers.TryGetPropertyValue<string>(cobj, "EditorID", out var edid);
                var candEditorId = edid ?? string.Empty;

                // Get COBJ's own FormKey (plugin and ID)
                MutagenReflectionHelpers.TryGetPluginAndIdFromRecord(cobj, out var cobjPlugin, out var cobjId);
                cobjPlugin = cobjPlugin ?? string.Empty;

                return new OmodCandidate
                {
                    CandidateType = "COBJ",
                    // Use COBJ's FormKey as the candidate key (this is what AttachPointConfirmer will resolve)
                    CandidateFormKey = new Models.FormKey { PluginName = cobjPlugin, FormId = cobjId },
                    CandidateEditorId = candEditorId,
                    BaseWeaponEditorId = baseWeaponEditorId,
                    // Store the CreatedObject (weapon) FormKey in BaseWeapon for reference
                    BaseWeapon = new Models.FormKey { PluginName = createdPlugin, FormId = createdId },
                    CandidateAmmo = createdAmmoKey != null ? new Models.FormKey { PluginName = createdAmmoKey.Value.ModKey.FileName, FormId = createdAmmoKey.Value.ID } : null,
                    CandidateAmmoEditorId = candidateAmmoEditorId,
                    CandidateAmmoName = candidateAmmoName,
                    SourcePlugin = cobjPlugin,
                    Notes = $"COBJ source: {cobjPlugin}:{(cobjId != 0 ? cobjId.ToString("X8") : "00000000")} -> Weapon: {createdPlugin}:{createdId:X8}",
                    SuggestedTarget = "CreatedWeapon"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Suppressed exception in WeaponDataExtractor: processing COBJ candidate");
                return null;
            }
        }
        // Helper: get CreatedObject via reflection safely
        private bool TryGetCreatedObject(object? cobj, out object? created)
        {
            created = null;
            try
            {
                return MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPropertyValue<object>(cobj, "CreatedObject", out created);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TryGetCreatedObject failed");
                created = null;
                return false;
            }
        }

        // Helper: find a matching weapon in the weapon list by plugin and id
        private object? TryFindMatchingWeapon(IEnumerable<object> allWeapons, string plugin, uint id)
        {
            try
            {
                foreach (var w in allWeapons)
                {
                    try
                    {
                        if (MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPluginAndIdFromRecord(w, out var wfPlugin, out var wfId) && wfPlugin == plugin && wfId == id)
                            return w;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "TryFindMatchingWeapon: error inspecting weapon");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TryFindMatchingWeapon failed");
            }
            return null;
        }

        // Helper: extract Ammo FormKey from a weapon-like object
        private bool TryExtractAmmoKey(object possibleWeapon, out MutagenFormKey? ammoKey)
        {
            ammoKey = null;
            try
            {
                if (MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPropertyValue<object>(possibleWeapon, "Ammo", out var ammoLink) && ammoLink != null)
                {
                    if (MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPropertyValue<object>(ammoLink, "FormKey", out var fk) && fk != null)
                    {
                        if (MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPluginAndIdFromRecord(fk, out var fileName, out var id2))
                        {
                            ammoKey = new MutagenFormKey(ModKey.FromNameAndExtension(fileName), id2);
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TryExtractAmmoKey failed");
            }
            return false;
        }
    }
}

