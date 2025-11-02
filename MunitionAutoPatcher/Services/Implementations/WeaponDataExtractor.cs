using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Interfaces;
using Mutagen.Bethesda.Environments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MunitionAutoPatcher.Services.Implementations
{
    public class WeaponDataExtractor : IWeaponDataExtractor
    {
        public WeaponDataExtractor()
        {
        }

        public Task<List<OmodCandidate>> ExtractAsync(IResourcedMutagenEnvironment env, HashSet<string> excluded, IProgress<string>? progress = null)
        {
            var resultsLocal = new List<OmodCandidate>();
            try
            {
                var cobjs = env.GetWinningConstructibleObjectOverrides();
                var allWeapons = env.GetWinningWeaponOverrides().ToList();

                foreach (var cobj in cobjs)
                {
                    try
                    {
                        if (!TryGetCreatedObject(cobj, out var created) || created == null) continue;

                        try
                        {
                            if (MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPropertyValue<bool>(created, "IsNull", out var isNull) && isNull)
                                continue;
                        }
                        catch (Exception ex)
                        {
                            AppLogger.Log("Suppressed exception in WeaponDataExtractor: inspecting CreatedObject.IsNull", ex);
                        }

                        // Determine created object's plugin/id early so exclusion checks can consider it when the COBJ has no source plugin
                        string createdPlugin = string.Empty;
                        uint createdId = 0u;
                        try
                        {
                            MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPluginAndIdFromRecord(created, out createdPlugin, out createdId);
                        }
                        catch (Exception ex)
                        {
                            AppLogger.Log("Suppressed exception in WeaponDataExtractor: reading created object's plugin/id", ex);
                        }

                        // Skip by excluded plugin. Prefer the COBJ's source plugin; if unavailable fall back to the created object's plugin.
                        try
                        {
                            var srcPlugin = string.Empty;
                            if (MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPluginAndIdFromRecord(cobj, out var sp, out _))
                                srcPlugin = sp;

                            if (!string.IsNullOrEmpty(srcPlugin))
                            {
                                if (excluded.Contains(srcPlugin)) continue;
                            }
                            else if (!string.IsNullOrEmpty(createdPlugin))
                            {
                                if (excluded.Contains(createdPlugin)) continue;
                            }
                        }
                        catch (Exception ex)
                        {
                            AppLogger.Log("Suppressed exception in WeaponDataExtractor: skip-by-excluded check", ex);
                        }

                        var possibleWeapon = TryFindMatchingWeapon(allWeapons, createdPlugin, createdId);

                        FormKey? createdAmmoKey = null;
                        string createdAmmoName = string.Empty;
                        if (possibleWeapon != null)
                        {
                            TryExtractAmmoKey(possibleWeapon, out createdAmmoKey);
                        }

                        var candEditorId = cobj?.GetType().GetProperty("EditorID")?.GetValue(cobj)?.ToString() ?? string.Empty;
                        var srcPluginVal = string.Empty;
                        MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPluginAndIdFromRecord(cobj, out var _srcPluginVal, out _);
                        srcPluginVal = _srcPluginVal;

                        resultsLocal.Add(new OmodCandidate
                        {
                            CandidateType = "COBJ",
                            CandidateFormKey = new Models.FormKey { PluginName = createdPlugin, FormId = createdId },
                            CandidateEditorId = candEditorId,
                            CandidateAmmo = createdAmmoKey != null ? new Models.FormKey { PluginName = createdAmmoKey.PluginName, FormId = createdAmmoKey.FormId } : null,
                            CandidateAmmoName = createdAmmoName ?? string.Empty,
                            SourcePlugin = srcPluginVal,
                            Notes = $"COBJ source: {srcPluginVal}:{(createdId != 0 ? createdId.ToString("X8") : "00000000")}",
                            SuggestedTarget = "CreatedWeapon"
                        });
                    }
                    catch (Exception ex) { AppLogger.Log("Suppressed exception in WeaponDataExtractor: processing COBJ candidate", ex); }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log("WeaponDataExtractor: failed while extracting from ConstructibleObjects", ex);
            }

            return Task.FromResult(resultsLocal);
        }

        // Helper: get CreatedObject via reflection safely
        private static bool TryGetCreatedObject(object? cobj, out object? created)
        {
            created = null;
            try
            {
                return MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPropertyValue<object>(cobj, "CreatedObject", out created);
            }
            catch (Exception ex)
            {
                AppLogger.Log("TryGetCreatedObject failed", ex);
                created = null;
                return false;
            }
        }

        // Helper: find a matching weapon in the weapon list by plugin and id
        private static object? TryFindMatchingWeapon(IEnumerable<object> allWeapons, string plugin, uint id)
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
                        AppLogger.Log("TryFindMatchingWeapon: error inspecting weapon", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log("TryFindMatchingWeapon failed", ex);
            }
            return null;
        }

        // Helper: extract Ammo FormKey from a weapon-like object
        private static bool TryExtractAmmoKey(object possibleWeapon, out FormKey? ammoKey)
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
                            ammoKey = new FormKey { PluginName = fileName, FormId = id2 };
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log("TryExtractAmmoKey failed", ex);
            }
            return false;
        }
    }
}

