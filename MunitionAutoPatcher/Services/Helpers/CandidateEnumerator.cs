using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using MunitionAutoPatcher.Models;
using MunitionAutoPatcher;
using Mutagen.Bethesda.Environments;

namespace MunitionAutoPatcher.Services.Helpers
{
    internal static class CandidateEnumerator
    {
        // Enumerate initial OMOD/COBJ candidates by scanning ConstructibleObject and reflecting over PriorityOrder collections.
    public static List<OmodCandidate> EnumerateCandidates(dynamic env, HashSet<string>? excluded, IProgress<string>? progress)
        {
            var results = new List<OmodCandidate>();
            try
            {
                // 1) ConstructibleObject (COBJ) CreatedObject scan
                try
                {
                    var cobjs = env.LoadOrder.PriorityOrder.ConstructibleObject().WinningOverrides();
                    int skippedByExcluded = 0;
                    foreach (var cobj in cobjs)
                    {
                        try
                        {
                            var created = cobj.CreatedObject;
                            if (created.IsNull) continue;

                            // Skip candidates coming from excluded plugins
                            try
                            {
                                var srcPlugin = cobj.FormKey.ModKey.FileName;
                                if ((excluded?.Contains(srcPlugin) ?? false))
                                {
                                    skippedByExcluded++;
                                    continue;
                                }
                            }
                            catch (Exception ex)
                            {
                                AppLogger.Log("CandidateEnumerator: failed checking excluded plugin for COBJ", ex);
                            }

                            FormKey? createdAmmoKey = null;
                            string createdAmmoName = string.Empty;
                            try
                            {
                                var plugin = created.FormKey.ModKey.FileName;
                                var id = created.FormKey.ID;
                                try
                                {
                                    var possibleWeapon = default(object);
                                    try
                                    {
                                        var seq = env.LoadOrder.PriorityOrder.Weapon().WinningOverrides();
                                        foreach (var w in seq)
                                        {
                                            try
                                            {
                                                if (w.FormKey.ModKey.FileName == plugin && w.FormKey.ID == id)
                                                {
                                                    possibleWeapon = w;
                                                    break;
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                AppLogger.Log("CandidateEnumerator: error iterating weapons sequence", ex);
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        AppLogger.Log("CandidateEnumerator: failed to enumerate weapons sequence", ex);
                                    }

                                    if (possibleWeapon != null)
                                    {
                                        try
                                        {
                                            if (TryExtractAmmoKeyFromWeaponObject(possibleWeapon, out var ammoKey) && ammoKey != null)
                                            {
                                                createdAmmoKey = ammoKey;
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            AppLogger.Log("CandidateEnumerator: failed to read Ammo/FormKey from possibleWeapon", ex);
                                        }
                                    }

                                    if (createdAmmoKey != null) createdAmmoName = string.Empty;
                                }
                                catch (Exception ex)
                                {
                                    AppLogger.Log("CandidateEnumerator: failed while detecting created ammo key", ex);
                                }
                            }
                            catch (Exception ex)
                            {
                                AppLogger.Log("CandidateEnumerator: failed processing individual COBJ candidate", ex);
                            }

                            results.Add(new OmodCandidate
                            {
                                CandidateType = "COBJ",
                                CandidateFormKey = new Models.FormKey { PluginName = created.FormKey.ModKey.FileName, FormId = created.FormKey.ID },
                                CandidateEditorId = cobj.EditorID ?? string.Empty,
                                CandidateAmmo = createdAmmoKey != null ? new Models.FormKey { PluginName = createdAmmoKey.PluginName, FormId = createdAmmoKey.FormId } : null,
                                CandidateAmmoName = createdAmmoName ?? string.Empty,
                                SourcePlugin = cobj.FormKey.ModKey.FileName,
                                Notes = $"COBJ source: {cobj.FormKey.ModKey.FileName}:{cobj.FormKey.ID:X8}",
                                SuggestedTarget = "CreatedWeapon"
                            });
                        }
                        catch (Exception ex)
                        {
                            AppLogger.Log("CandidateEnumerator: failed processing COBJ loop item", ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Log("CandidateEnumerator: COBJ CreatedObject scan failed", ex);
                }

                // 2) Reflection-based scan over PriorityOrder collections to find records that reference weapons
                try
                {
                    var weapons = new List<dynamic>();
                    try
                    {
                        var weaponsSeq = env.LoadOrder.PriorityOrder.Weapon().WinningOverrides();
                        foreach (var w in weaponsSeq) weapons.Add(w);
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Log("CandidateEnumerator: failed to add weapons from PriorityOrder.Weapon() sequence", ex);
                    }

                    // If direct WinningOverrides retrieval failed (e.g. test fakes), try reflecting the PriorityOrder to collect Weapon() items.
                    if (weapons.Count == 0)
                    {
                        try
                        {
                            var priorityForWeapons = env.LoadOrder.PriorityOrder;
                            var pt = priorityForWeapons.GetType();
                            var pmethods = pt.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                            foreach (var mm in pmethods)
                            {
                                try
                                {
                                    if (mm.GetParameters().Length == 0 && string.Equals(mm.Name, "Weapon", StringComparison.OrdinalIgnoreCase) && typeof(System.Collections.IEnumerable).IsAssignableFrom(mm.ReturnType))
                                    {
                                        var coll = mm.Invoke(priorityForWeapons, null);
                                        if (coll == null) continue;
                                        var winm = coll.GetType().GetMethod("WinningOverrides");
                                        System.Collections.IEnumerable? its = null;
                                        if (winm != null) its = winm.Invoke(coll, null) as System.Collections.IEnumerable;
                                        else if (coll is System.Collections.IEnumerable en2) its = en2;
                                        if (its != null)
                                        {
                                            foreach (var w in its)
                                            {
                                                try { weapons.Add(w); } catch (Exception ex) { AppLogger.Log("CandidateEnumerator: failed adding reflected weapon item", ex); }
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    AppLogger.Log("CandidateEnumerator: failed collecting weapons via reflection", ex);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            AppLogger.Log("CandidateEnumerator: failed reflecting PriorityOrder for weapons", ex);
                        }
                    }

                    var weaponKeys = new HashSet<(string Plugin, uint Id)>();
                    foreach (var w in weapons)
                    {
                        try
                        {
                            var pName = w.FormKey.ModKey.FileName?.ToString() ?? string.Empty;
                            var fid = (uint)(w.FormKey.ID);
                            weaponKeys.Add((pName, fid));
                        }
                        catch (Exception ex)
                        {
                            AppLogger.Log("CandidateEnumerator: failed to read FormKey from weapon record", ex);
                        }
                    }

                    var priority = env.LoadOrder.PriorityOrder;
                    var type = priority.GetType();
                    var allMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                    var methods = new List<MethodInfo>();
                    foreach (var mm in allMethods)
                    {
                        try
                        {
                            if (mm.GetParameters().Length == 0 && typeof(System.Collections.IEnumerable).IsAssignableFrom(mm.ReturnType))
                                methods.Add(mm);
                        }
                        catch (Exception ex)
                        {
                            AppLogger.Log("CandidateEnumerator: failed inspecting PriorityOrder method", ex);
                        }
                    }

                    foreach (var m in methods)
                    {
                        object? collection = null;
                        try { collection = m.Invoke(priority, null); } catch (Exception ex) { AppLogger.Log("CandidateEnumerator: failed to invoke collection method on PriorityOrder", ex); continue; }
                        if (collection == null) continue;

                        var winMethod = collection.GetType().GetMethod("WinningOverrides");
                        System.Collections.IEnumerable? items = null;
                        try
                        {
                            if (winMethod != null) items = winMethod.Invoke(collection, null) as System.Collections.IEnumerable;
                            else if (collection is System.Collections.IEnumerable en) items = en;
                        }
                        catch (Exception ex)
                        {
                            AppLogger.Log("CandidateEnumerator: failed to obtain WinningOverrides or enumerate collection", ex);
                            items = null;
                        }
                        if (items == null) continue;

                        // If this collection is the 'Weapon' collection, ensure we capture its items into the weapons list
                        try
                        {
                            if (string.Equals(m.Name, "Weapon", StringComparison.OrdinalIgnoreCase))
                            {
                                var materialized = new List<object>();
                                foreach (var it in items) materialized.Add(it);
                                foreach (var it in materialized) weapons.Add(it);
                                items = materialized; // use materialized list for further iteration
                            }
                        }
                        catch (Exception ex)
                        {
                            AppLogger.Log("CandidateEnumerator: failed materializing Weapon collection", ex);
                        }

                        foreach (var rec in items)
                        {
                            if (rec == null) continue;
                            try
                            {
                                // If the record itself comes from an excluded plugin, skip it early.
                                try
                                {
                                    string recPlugin = string.Empty;
                                    try { if (MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPluginAndIdFromRecord(rec, out var pluginTmp, out var _)) recPlugin = pluginTmp; }
                                    catch (Exception ex) { AppLogger.Log("CandidateEnumerator: failed to read rec plugin via helper", ex); }
                                    if (!string.IsNullOrEmpty(recPlugin))
                                    {
                                        try { if ((excluded?.Contains(recPlugin) ?? false)) continue; } catch (Exception ex) { AppLogger.Log("CandidateEnumerator: failed checking excluded plugin for reflected record", ex); }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    AppLogger.Log("CandidateEnumerator: failed obtaining record source plugin", ex);
                                }

                                
                                var props = rec.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                                foreach (var p in props)
                                {
                                    try
                                    {
                                        var val = p.GetValue(rec);
                                        if (val == null) continue;
                                        var nestedFkProp = val.GetType().GetProperty("FormKey");
                                        if (nestedFkProp == null) continue;
                                        var nestedFk = nestedFkProp.GetValue(val);
                                        if (nestedFk == null) continue;
                                        string plugin = string.Empty; uint id = 0;
                                        try { MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPluginAndIdFromRecord(nestedFk, out plugin, out id); }
                                        catch (Exception ex) { AppLogger.Log("CandidateEnumerator: failed to read nested FormKey via helper", ex); }
                                        if (string.IsNullOrEmpty(plugin) || id == 0) continue;
                                        try { if ((excluded?.Contains(plugin) ?? false)) continue; } catch (Exception ex) { AppLogger.Log("CandidateEnumerator: failed checking excluded plugin in reflection scan", ex); }
                                        if (!weaponKeys.Contains((plugin, id))) continue;

                                        var recEditorId = string.Empty;
                                        try { if (!MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPropertyValue<string>(rec, "EditorID", out recEditorId)) recEditorId = string.Empty; }
                                        catch (Exception ex) { AppLogger.Log("CandidateEnumerator: failed to read EditorID via helper", ex); }

                                        // Detect ammo-like references in other properties
                                        Models.FormKey? detectedAmmoKey = null;
                                        try
                                        {
                                            var allProps = rec.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                                            foreach (var q in allProps)
                                            {
                                                try
                                                {
                                                    if (q.Name == p.Name) continue;
                                                    var qval = q.GetValue(rec);
                                                    if (qval == null) continue;
                                                    var fkPropQ = qval.GetType().GetProperty("FormKey");
                                                    if (fkPropQ != null)
                                                    {
                                                        var fkq = fkPropQ.GetValue(qval);
                                                        if (fkq != null)
                                                        {
                                                            try
                                                            {
                                                                if (MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPluginAndIdFromRecord(fkq, out var pluginq, out var idq))
                                                                {
                                                                    if (!(string.Equals(pluginq, plugin, StringComparison.OrdinalIgnoreCase) && idq == id))
                                                                    {
                                                                        detectedAmmoKey = new Models.FormKey { PluginName = pluginq, FormId = idq };
                                                                        break;
                                                                    }
                                                                }
                                                            }
                                                            catch (Exception ex)
                                                            {
                                                                AppLogger.Log("CandidateEnumerator: failed inspecting nested property for ammo detection", ex);
                                                            }
                                                        }
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    AppLogger.Log("CandidateEnumerator: error iterating record properties for ammo detection", ex);
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            AppLogger.Log("CandidateEnumerator: failed during nested property scan", ex);
                                        }

                                        // Compose candidate
                                        try
                                        {
                                            // determine BaseWeaponEditorId by scanning collected weapons
                                            string baseWeaponEditorId = string.Empty;
                                            try
                                            {
                                                foreach (var ww in weapons)
                                                {
                                                    try
                                                    {
                                                        if (ww.FormKey.ModKey.FileName == plugin && ww.FormKey.ID == id)
                                                        {
                                                            baseWeaponEditorId = ww.EditorID ?? string.Empty;
                                                            break;
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        AppLogger.Log("CandidateEnumerator: error scanning weapons for BaseWeaponEditorId", ex);
                                                    }
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                AppLogger.Log("CandidateEnumerator: failed while searching for base weapon editor id", ex);
                                            }

                                            // Extract the record's own FormKey (source) safely
                                            object? recFormKeyObj = null;
                                            string recSourcePlugin = string.Empty;
                                            uint recSourceId = 0;
                                            try
                                            {
                                                try { MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPluginAndIdFromRecord(rec, out recSourcePlugin, out recSourceId); }
                                                catch (Exception ex) { AppLogger.Log("CandidateEnumerator: failed to read rec source plugin/id via helper", ex); }
                                            }
                                            catch (Exception ex)
                                            {
                                                AppLogger.Log("CandidateEnumerator: failed reading record FormKey for candidate composition", ex);
                                            }

                                            OmodCandidate candidate;
                                            if (string.Equals(m.Name, "ConstructibleObject", StringComparison.OrdinalIgnoreCase) && string.Equals(p.Name, "CreatedObject", StringComparison.OrdinalIgnoreCase))
                                            {
                                                // This is a COBJ -> CreatedObject reference: treat as a COBJ candidate
                                                candidate = new OmodCandidate
                                                {
                                                    CandidateType = "COBJ",
                                                    CandidateFormKey = new Models.FormKey { PluginName = plugin, FormId = id },
                                                    CandidateEditorId = recEditorId,
                                                    BaseWeapon = new Models.FormKey { PluginName = plugin, FormId = id },
                                                    BaseWeaponEditorId = baseWeaponEditorId,
                                                    CandidateAmmo = detectedAmmoKey != null ? new Models.FormKey { PluginName = detectedAmmoKey.PluginName, FormId = detectedAmmoKey.FormId } : null,
                                                    CandidateAmmoName = string.Empty,
                                                    SourcePlugin = recSourcePlugin ?? string.Empty,
                                                    Notes = $"COBJ source: {recSourcePlugin}:{recSourceId:X8};Reference in {m.Name}.{p.Name} -> {plugin}:{id:X8}" + (detectedAmmoKey != null ? $";DetectedAmmo={detectedAmmoKey.PluginName}:{detectedAmmoKey.FormId:X8}" : string.Empty),
                                                    SuggestedTarget = "CreatedWeapon"
                                                };
                                            }
                                            else
                                            {
                                                candidate = new OmodCandidate
                                                {
                                                    CandidateType = m.Name,
                                                    CandidateFormKey = new Models.FormKey { PluginName = recSourcePlugin ?? string.Empty, FormId = recSourceId },
                                                    CandidateEditorId = recEditorId,
                                                    BaseWeapon = new Models.FormKey { PluginName = plugin, FormId = id },
                                                    BaseWeaponEditorId = baseWeaponEditorId,
                                                    CandidateAmmo = detectedAmmoKey != null ? new Models.FormKey { PluginName = detectedAmmoKey.PluginName, FormId = detectedAmmoKey.FormId } : null,
                                                    CandidateAmmoName = string.Empty,
                                                    SourcePlugin = recSourcePlugin ?? string.Empty,
                                                    Notes = $"Reference found in {m.Name}.{p.Name} -> {plugin}:{id:X8}" + (detectedAmmoKey != null ? $";DetectedAmmo={detectedAmmoKey.PluginName}:{detectedAmmoKey.FormId:X8}" : string.Empty),
                                                    SuggestedTarget = "Reference"
                                                };
                                            }
                                            results.Add(candidate);
                                        }
                                        catch (Exception ex)
                                        {
                                            AppLogger.Log("CandidateEnumerator: failed to compose/add candidate", ex);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        AppLogger.Log("CandidateEnumerator: failed processing property on record", ex);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                AppLogger.Log("CandidateEnumerator: failed processing record in method scan", ex);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Log("CandidateEnumerator: reflection-based scan failed", ex);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log("CandidateEnumerator: EnumerateCandidates failed", ex);
            }

            return results;
        }

        // Helper: extract Ammo FormKey from a weapon-like object safely via reflection helpers
        private static bool TryExtractAmmoKeyFromWeaponObject(object possibleWeapon, out Models.FormKey? ammoKey)
        {
            ammoKey = null;
            try
            {
                if (MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPropertyValue<object>(possibleWeapon, "Ammo", out var ammoLink) && ammoLink != null)
                {
                    if (MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPropertyValue<object>(ammoLink, "FormKey", out var fk) && fk != null)
                    {
                        if (MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPluginAndIdFromRecord(fk, out var plugin, out var id))
                        {
                            ammoKey = new Models.FormKey { PluginName = plugin, FormId = id };
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log("CandidateEnumerator: TryExtractAmmoKeyFromWeaponObject failed", ex);
            }
            return false;
        }
    }
}
