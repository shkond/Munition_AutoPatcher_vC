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
                        var created = cobj?.GetType().GetProperty("CreatedObject")?.GetValue(cobj);
                        if (created == null) continue;

                        try
                        {
                            var isNullProp = created.GetType().GetProperty("IsNull");
                            if (isNullProp != null)
                            {
                                var isNullVal = isNullProp.GetValue(created);
                                if (isNullVal is bool b && b) continue;
                            }
                        }
                        catch { }

                        // Skip by excluded plugin
                        try
                        {
                            var srcPlugin = string.Empty;
                            if (MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPluginAndIdFromRecord(cobj, out var sp, out _))
                                srcPlugin = sp;
                            if (excluded.Contains(srcPlugin)) continue;
                        }
                        catch (Exception ex) { AppLogger.Log("Suppressed exception in WeaponDataExtractor: iterating COBJs", ex); }

                        // Try to resolve created object to a weapon to read its ammo link
                        string createdPlugin = string.Empty;
                        uint createdId = 0u;
                        MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPluginAndIdFromRecord(created, out createdPlugin, out createdId);

                        object? possibleWeapon = null;
                        foreach (var w in allWeapons)
                        {
                            try
                            {
                                if (MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPluginAndIdFromRecord(w, out var wfPlugin, out var wfId) && wfPlugin == createdPlugin && wfId == createdId)
                                {
                                    possibleWeapon = w;
                                    break;
                                }
                            }
                            catch { }
                        }

                        FormKey? createdAmmoKey = null;
                        string createdAmmoName = string.Empty;
                        if (possibleWeapon != null)
                        {
                            var ammoLink = possibleWeapon.GetType().GetProperty("Ammo")?.GetValue(possibleWeapon);
                            if (ammoLink != null)
                            {
                                try
                                {
                                    var fk = ammoLink.GetType().GetProperty("FormKey")?.GetValue(ammoLink);
                                    if (fk != null)
                                    {
                                        if (MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPluginAndIdFromRecord(fk, out var fileName, out var id2))
                                        {
                                            createdAmmoKey = new FormKey { PluginName = fileName, FormId = id2 };
                                        }
                                    }
                                }
                                catch { }
                            }
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
    }
}

