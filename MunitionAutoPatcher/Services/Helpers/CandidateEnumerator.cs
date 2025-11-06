using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using MunitionAutoPatcher.Models;
using MunitionAutoPatcher;
using Microsoft.Extensions.Logging;
using Mutagen.Bethesda.Environments;

namespace MunitionAutoPatcher.Services.Helpers
{
    /// <summary>
    /// Enumerates OMOD/COBJ candidates by scanning game records and reflecting over collections.
    /// </summary>
    internal static class CandidateEnumerator
    {
        // Constants for record type names
        private const string CobjTypeName = "ConstructibleObject";
        private const string WeaponTypeName = "Weapon";
        private const string CreatedObjectPropertyName = "CreatedObject";

        /// <summary>
        /// Enumerates all OMOD/COBJ candidates from the game environment.
        /// </summary>
        /// <param name="env">Game environment with LoadOrder access</param>
        /// <param name="excluded">Set of plugin names to exclude from enumeration</param>
        /// <param name="progress">Optional progress reporter</param>
        /// <returns>List of discovered candidates</returns>
        public static List<OmodCandidate> EnumerateCandidates(dynamic env, HashSet<string>? excluded, IProgress<string>? progress, ILogger logger)
        {
            var results = new List<OmodCandidate>();

            try
            {
                // Enumerate COBJ candidates
                var cobjCandidates = EnumerateCobjCandidates(env, excluded, logger);
                results.AddRange(cobjCandidates);

                // Enumerate reflection-based candidates
                var reflectedCandidates = EnumerateReflectedCandidates(env, excluded, logger);
                results.AddRange(reflectedCandidates);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "CandidateEnumerator: EnumerateCandidates failed");
            }

            return results;
        }

        /// <summary>
        /// Enumerates candidates from ConstructibleObject records (COBJ).
        /// </summary>
        private static List<OmodCandidate> EnumerateCobjCandidates(dynamic env, HashSet<string>? excluded, ILogger? logger = null)
        {
            var results = new List<OmodCandidate>();

            try
            {
                var cobjs = env.LoadOrder.PriorityOrder.ConstructibleObject().WinningOverrides();

                foreach (var cobj in cobjs)
                {
                    try
                    {
                        var candidate = TryCreateCobjCandidate(env, cobj, excluded, logger);
                        if (candidate != null)
                        {
                            results.Add(candidate);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "CandidateEnumerator: failed processing COBJ loop item");
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "CandidateEnumerator: COBJ CreatedObject scan failed");
            }

            return results;
        }

        /// <summary>
        /// Attempts to create a candidate from a ConstructibleObject record.
        /// </summary>
        private static OmodCandidate? TryCreateCobjCandidate(dynamic env, dynamic cobj, HashSet<string>? excluded, ILogger? logger = null)
        {
            var created = cobj.CreatedObject;
            if (created.IsNull) return null;

            // Check exclusion
            var srcPlugin = cobj.FormKey?.ModKey?.FileName;
            if (IsExcluded(srcPlugin, excluded)) return null;

            // Extract created object details
            var createdPlugin = created.FormKey?.ModKey?.FileName ?? string.Empty;
            var createdId = created.FormKey?.ID ?? 0u;

            // Try to detect ammo for the created weapon
            var createdAmmoKey = TryDetectAmmoForWeapon(env, createdPlugin, createdId, logger);

            return new OmodCandidate
            {
                CandidateType = "COBJ",
                CandidateFormKey = new Models.FormKey { PluginName = createdPlugin, FormId = createdId },
                CandidateEditorId = cobj.EditorID ?? string.Empty,
                CandidateAmmo = createdAmmoKey != null
                    ? new Models.FormKey { PluginName = createdAmmoKey.PluginName ?? string.Empty, FormId = createdAmmoKey.FormId }
                    : null,
                CandidateAmmoName = string.Empty,
                SourcePlugin = srcPlugin ?? string.Empty,
                Notes = $"COBJ source: {srcPlugin ?? "Unknown"}:{cobj.FormKey?.ID ?? 0u:X8}",
                SuggestedTarget = "CreatedWeapon"
            };
        }

        /// <summary>
        /// Attempts to detect ammo key for a weapon identified by plugin and ID.
        /// </summary>
        private static Models.FormKey? TryDetectAmmoForWeapon(dynamic env, string plugin, uint id, ILogger? logger = null)
        {
            if (string.IsNullOrEmpty(plugin) || id == 0) return null;

            try
            {
                var weaponsSeq = env.LoadOrder.PriorityOrder.Weapon().WinningOverrides();
                foreach (var w in weaponsSeq)
                {
                    try
                    {
                        if (w.FormKey.ModKey.FileName == plugin && w.FormKey.ID == id)
                        {
                            if (TryExtractAmmoKeyFromWeaponObject(w, out Models.FormKey? ammoKey))
                            {
                                return ammoKey;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "CandidateEnumerator: error iterating weapons for ammo detection");
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "CandidateEnumerator: failed to detect ammo for weapon");
            }

            return null;
        }

        /// <summary>
        /// Enumerates candidates via reflection over PriorityOrder collections.
        /// </summary>
        private static List<OmodCandidate> EnumerateReflectedCandidates(dynamic env, HashSet<string>? excluded, ILogger? logger = null)
        {
            var results = new List<OmodCandidate>();

            try
            {
                var weapons = CollectWeapons(env, logger);
                var weaponKeys = BuildWeaponKeySet(weapons, logger);
                var methods = GetPriorityOrderMethods(env, logger);

                foreach (var method in methods)
                {
                    var candidates = ProcessCollectionMethod(env, method, weapons, weaponKeys, excluded, logger);
                    results.AddRange(candidates);
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "CandidateEnumerator: reflection-based scan failed");
            }

            return results;
        }

        /// <summary>
        /// Collects all weapon records from the environment.
        /// </summary>
        private static List<dynamic> CollectWeapons(dynamic env, ILogger? logger = null)
        {
            var weapons = new List<dynamic>();

            try
            {
                var weaponsSeq = env.LoadOrder.PriorityOrder.Weapon().WinningOverrides();
                foreach (var w in weaponsSeq) weapons.Add(w);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "CandidateEnumerator: failed to add weapons from PriorityOrder.Weapon() sequence");
            }

            // Fallback: reflect if direct access failed
            if (weapons.Count == 0)
            {
                weapons = CollectWeaponsViaReflection(env, logger);
            }

            return weapons;
        }

        /// <summary>
        /// Collects weapons via reflection (fallback method).
        /// </summary>
        private static List<dynamic> CollectWeaponsViaReflection(dynamic env, ILogger? logger = null)
        {
            var weapons = new List<dynamic>();

            try
            {
                var priorityForWeapons = env.LoadOrder.PriorityOrder;
                var pt = priorityForWeapons.GetType();
                var pmethods = pt.GetMethods(BindingFlags.Public | BindingFlags.Instance);

                foreach (var mm in pmethods)
                {
                    try
                    {
                        if (mm.GetParameters().Length == 0 &&
                            string.Equals(mm.Name, WeaponTypeName, StringComparison.OrdinalIgnoreCase) &&
                            typeof(System.Collections.IEnumerable).IsAssignableFrom(mm.ReturnType))
                        {
                            var items = InvokeAndGetWinningOverrides(mm, priorityForWeapons, logger);
                            if (items != null)
                            {
                                foreach (var w in items)
                                {
                                    weapons.Add(w);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "CandidateEnumerator: failed collecting weapons via reflection");
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "CandidateEnumerator: failed reflecting PriorityOrder for weapons");
            }

            return weapons;
        }

        /// <summary>
        /// Builds a set of weapon keys for quick lookup.
        /// </summary>
        private static HashSet<(string Plugin, uint Id)> BuildWeaponKeySet(List<dynamic> weapons, ILogger? logger = null)
        {
            var weaponKeys = new HashSet<(string Plugin, uint Id)>();

            foreach (var w in weapons)
            {
                try
                {
                    var pName = w.FormKey?.ModKey?.FileName?.ToString() ?? string.Empty;
                    var fid = (uint)(w.FormKey?.ID ?? 0u);
                    if (!string.IsNullOrEmpty(pName) && fid != 0)
                    {
                        weaponKeys.Add((pName, fid));
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "CandidateEnumerator: failed to read FormKey from weapon record");
                }
            }

            return weaponKeys;
        }

        /// <summary>
        /// Gets collection methods from PriorityOrder via reflection.
        /// </summary>
        private static List<MethodInfo> GetPriorityOrderMethods(dynamic env, ILogger? logger = null)
        {
            var methods = new List<MethodInfo>();

            try
            {
                var priority = env.LoadOrder.PriorityOrder;
                var type = priority.GetType();
                var allMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);

                foreach (var mm in allMethods)
                {
                    try
                    {
                        if (mm.GetParameters().Length == 0 &&
                            typeof(System.Collections.IEnumerable).IsAssignableFrom(mm.ReturnType))
                        {
                            methods.Add(mm);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "CandidateEnumerator: failed inspecting PriorityOrder method");
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "CandidateEnumerator: failed to get PriorityOrder methods");
            }

            return methods;
        }

        /// <summary>
        /// Processes a single collection method and extracts candidates.
        /// </summary>
        private static List<OmodCandidate> ProcessCollectionMethod(
            dynamic env,
            MethodInfo method,
            List<dynamic> weapons,
            HashSet<(string Plugin, uint Id)> weaponKeys,
            HashSet<string>? excluded,
            ILogger? logger = null)
        {
            var results = new List<OmodCandidate>();

            try
            {
                var priority = env.LoadOrder.PriorityOrder;
                var items = InvokeAndGetWinningOverrides(method, priority, logger);
                if (items == null) return results;

                // If this is the Weapon collection, ensure we have it in our weapons list
                if (string.Equals(method.Name, WeaponTypeName, StringComparison.OrdinalIgnoreCase))
                {
                    var materialized = new List<object>();
                    foreach (var it in items)
                    {
                        materialized.Add(it);
                        weapons.Add(it);
                    }
                    items = materialized;
                }

                foreach (var rec in items)
                {
                    if (rec == null) continue;

                    var recCandidates = ProcessRecord(method, rec, weapons, weaponKeys, excluded, logger);
                    results.AddRange(recCandidates);
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "CandidateEnumerator: failed processing collection method {Method}", method.Name);
            }

            return results;
        }

        /// <summary>
        /// Processes a single record and extracts candidates from its properties.
        /// </summary>
        private static List<OmodCandidate> ProcessRecord(
            MethodInfo collectionMethod,
            dynamic rec,
            List<dynamic> weapons,
            HashSet<(string Plugin, uint Id)> weaponKeys,
            HashSet<string>? excluded,
            ILogger? logger = null)
        {
            var results = new List<OmodCandidate>();

            try
            {
                // Check if record itself is excluded
                if (MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPluginAndIdFromRecord(rec, out string? recPlugin, out uint _))
                {
                    if (IsExcluded(recPlugin, excluded)) return results;
                }

                var props = rec.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

                foreach (var prop in props)
                {
                    try
                    {
                        var candidate = TryExtractCandidateFromProperty(
                            collectionMethod,
                            rec,
                            prop,
                            weapons,
                            weaponKeys,
                            excluded,
                            logger);

                        if (candidate != null)
                        {
                            results.Add(candidate);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "CandidateEnumerator: failed processing property on record");
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "CandidateEnumerator: failed processing record in method scan");
            }

            return results;
        }

        /// <summary>
        /// Attempts to extract a candidate from a property value if it references a weapon.
        /// </summary>
        private static OmodCandidate? TryExtractCandidateFromProperty(
            MethodInfo collectionMethod,
            dynamic rec,
            PropertyInfo prop,
            List<dynamic> weapons,
            HashSet<(string Plugin, uint Id)> weaponKeys,
            HashSet<string>? excluded,
            ILogger? logger = null)
        {
            var val = prop.GetValue(rec);
            if (val == null) return null;

            // Check if this property has a FormKey
            var nestedFkProp = val.GetType().GetProperty("FormKey");
            if (nestedFkProp == null) return null;

            var nestedFk = nestedFkProp.GetValue(val);
            if (nestedFk == null) return null;

            // Extract plugin and ID from the FormKey
            if (!MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPluginAndIdFromRecord(nestedFk, out string? plugin, out uint id))
                return null;

            if (string.IsNullOrEmpty(plugin) || id == 0) return null;
            if (IsExcluded(plugin, excluded)) return null;

            var pluginSafe = plugin ?? string.Empty;
            if (!weaponKeys.Contains((pluginSafe, id))) return null;

            // We found a weapon reference - build the candidate
            return BuildCandidateFromWeaponReference(
                collectionMethod,
                rec,
                prop,
                pluginSafe,
                id,
                weapons,
                logger);
        }

        /// <summary>
        /// Builds a candidate from a detected weapon reference.
        /// </summary>
        private static OmodCandidate BuildCandidateFromWeaponReference(
            MethodInfo collectionMethod,
            dynamic rec,
            PropertyInfo prop,
            string weaponPlugin,
            uint weaponId,
            List<dynamic> weapons,
            ILogger? logger = null)
        {
            var recEditorId = string.Empty;
            MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPropertyValue<string>(rec, "EditorID", out recEditorId);

            // Detect ammo references in other properties
            var detectedAmmoKey = DetectAmmoKeyInRecord(rec, prop, weaponPlugin, weaponId, logger);
            var detectedAmmoNotes = detectedAmmoKey != null
                ? $";DetectedAmmo={(detectedAmmoKey.PluginName ?? string.Empty)}:{detectedAmmoKey.FormId:X8}"
                : string.Empty;

            // Find base weapon editor ID
            var baseWeaponEditorId = FindWeaponEditorId(weapons, weaponPlugin, weaponId, logger);

            // Extract record source FormKey
            MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPluginAndIdFromRecord(rec, out string? recSourcePlugin, out uint recSourceId);
            var recSourcePluginSafe = recSourcePlugin ?? string.Empty;

            // Prepare candidate ammo
            Models.FormKey? candidateAmmoLocal = null;
            if (detectedAmmoKey != null)
            {
                candidateAmmoLocal = new Models.FormKey
                {
                    PluginName = detectedAmmoKey.PluginName ?? string.Empty,
                    FormId = detectedAmmoKey.FormId
                };
            }

            var pluginForCandidate = weaponPlugin;

#pragma warning disable CS8601 // pluginForCandidate is guaranteed non-null by prior checks
            if (string.Equals(collectionMethod.Name, CobjTypeName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(prop.Name, CreatedObjectPropertyName, StringComparison.OrdinalIgnoreCase))
            {
                // This is a COBJ -> CreatedObject reference
                return new OmodCandidate
                {
                    CandidateType = "COBJ",
                    CandidateFormKey = new Models.FormKey { PluginName = pluginForCandidate, FormId = weaponId },
                    CandidateEditorId = recEditorId,
                    BaseWeapon = new Models.FormKey { PluginName = pluginForCandidate, FormId = weaponId },
                    BaseWeaponEditorId = baseWeaponEditorId,
                    CandidateAmmo = candidateAmmoLocal,
                    CandidateAmmoName = string.Empty,
                    SourcePlugin = recSourcePluginSafe,
                    Notes = $"COBJ source: {recSourcePluginSafe}:{recSourceId:X8};Reference in {collectionMethod.Name}.{prop.Name} -> {pluginForCandidate}:{weaponId:X8}" + detectedAmmoNotes,
                    SuggestedTarget = "CreatedWeapon"
                };
            }
            else
            {
                // Generic reference
                return new OmodCandidate
                {
                    CandidateType = collectionMethod.Name,
                    CandidateFormKey = new Models.FormKey { PluginName = recSourcePluginSafe, FormId = recSourceId },
                    CandidateEditorId = recEditorId,
                    BaseWeapon = new Models.FormKey { PluginName = pluginForCandidate, FormId = weaponId },
                    BaseWeaponEditorId = baseWeaponEditorId,
                    CandidateAmmo = candidateAmmoLocal,
                    CandidateAmmoName = string.Empty,
                    SourcePlugin = recSourcePluginSafe,
                    Notes = $"Reference found in {collectionMethod.Name}.{prop.Name} -> {pluginForCandidate}:{weaponId:X8}" + detectedAmmoNotes,
                    SuggestedTarget = "Reference"
                };
            }
#pragma warning restore CS8601
        }

        /// <summary>
        /// Detects ammo-like references in a record's properties (excluding the weapon property itself).
        /// </summary>
        private static Models.FormKey? DetectAmmoKeyInRecord(dynamic rec, PropertyInfo excludeProperty, string weaponPlugin, uint weaponId, ILogger? logger = null)
        {
            try
            {
                var allProps = rec.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

                foreach (var q in allProps)
                {
                    if (q.Name == excludeProperty.Name) continue;

                    try
                    {
                        var qval = q.GetValue(rec);
                        if (qval == null) continue;

                        var fkPropQ = qval.GetType().GetProperty("FormKey");
                        if (fkPropQ == null) continue;

                        var fkq = fkPropQ.GetValue(qval);
                        if (fkq == null) continue;

                        if (MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPluginAndIdFromRecord(fkq, out string? pluginq, out uint idq))
                        {
                            // Found a different FormKey - likely ammo
                            if (!(string.Equals(pluginq, weaponPlugin, StringComparison.OrdinalIgnoreCase) && idq == weaponId))
                            {
                                return new Models.FormKey { PluginName = pluginq ?? string.Empty, FormId = idq };
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "CandidateEnumerator: error iterating record properties for ammo detection");
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "CandidateEnumerator: failed during nested property scan");
            }

            return null;
        }

        /// <summary>
        /// Finds the EditorID of a weapon from the weapons collection.
        /// </summary>
        private static string FindWeaponEditorId(List<dynamic> weapons, string plugin, uint id, ILogger? logger = null)
        {
            try
            {
                foreach (var ww in weapons)
                {
                    try
                    {
                        if (ww.FormKey?.ModKey?.FileName == plugin && ww.FormKey?.ID == id)
                        {
                            return ww.EditorID ?? string.Empty;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "CandidateEnumerator: error scanning weapons for EditorId");
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "CandidateEnumerator: failed while searching for weapon editor id");
            }

            return string.Empty;
        }

        /// <summary>
        /// Helper: invokes a method on an object and attempts to get WinningOverrides.
        /// </summary>
        private static System.Collections.IEnumerable? InvokeAndGetWinningOverrides(MethodInfo method, object target, ILogger? logger = null)
        {
            try
            {
                var collection = method.Invoke(target, null);
                if (collection == null) return null;

                var winMethod = collection.GetType().GetMethod("WinningOverrides");
                if (winMethod != null)
                {
                    return winMethod.Invoke(collection, null) as System.Collections.IEnumerable;
                }
                else if (collection is System.Collections.IEnumerable en)
                {
                    return en;
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "CandidateEnumerator: failed to invoke method and get WinningOverrides");
            }

            return null;
        }

        /// <summary>
        /// Checks if a plugin name is in the exclusion set.
        /// </summary>
        private static bool IsExcluded(string? plugin, HashSet<string>? excluded)
        {
            if (string.IsNullOrEmpty(plugin) || excluded == null || excluded.Count == 0)
                return false;

            try
            {
                return excluded.Contains(plugin);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Helper: extract Ammo FormKey from a weapon-like object safely via reflection helpers.
        /// </summary>
        private static bool TryExtractAmmoKeyFromWeaponObject(object possibleWeapon, out Models.FormKey? ammoKey, ILogger? logger = null)
        {
            ammoKey = null;
            try
            {
                if (MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPropertyValue<object>(possibleWeapon, "Ammo", out var ammoLink) && ammoLink != null)
                {
                    if (MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPropertyValue<object>(ammoLink, "FormKey", out var fk) && fk != null)
                    {
                        if (MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPluginAndIdFromRecord(fk, out string? plugin, out uint id))
                        {
                            ammoKey = new Models.FormKey { PluginName = plugin ?? string.Empty, FormId = id };
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "CandidateEnumerator: TryExtractAmmoKeyFromWeaponObject failed");
            }
            return false;
        }
    }
}
