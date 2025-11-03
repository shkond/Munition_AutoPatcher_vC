using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MunitionAutoPatcher.Services.Helpers
{
    /// <summary>
    /// Builds a reverse-reference map from FormKeys to records that reference them.
    /// </summary>
    internal static class ReverseMapBuilder
    {
        /// <summary>
        /// Builds a reverse-reference map: "Plugin:ID" -> list of (record, propName, propValue).
        /// </summary>
        /// <param name="priorityRoot">The PriorityOrder root object to scan</param>
        /// <param name="excluded">Set of plugin names to exclude</param>
        /// <returns>Dictionary mapping FormKey strings to lists of referencing records</returns>
        public static Dictionary<string, List<(object Record, string PropName, object PropValue)>> Build(object priorityRoot, HashSet<string> excluded)
        {
            var reverseMap = new Dictionary<string, List<(object, string, object)>>(StringComparer.OrdinalIgnoreCase);
            
            try
            {
                var methods = GetCollectionMethods(priorityRoot);
                
                foreach (var method in methods)
                {
                    var records = GetRecordsFromMethod(method, priorityRoot);
                    ProcessRecords(records, excluded, reverseMap);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log("ReverseMapBuilder: Build failed", ex);
            }

            return reverseMap;
        }

        /// <summary>
        /// Gets collection methods from the priority root object.
        /// </summary>
        private static IEnumerable<MethodInfo> GetCollectionMethods(object priorityRoot)
        {
            try
            {
                return priorityRoot.GetType()
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.GetParameters().Length == 0 && 
                                typeof(System.Collections.IEnumerable).IsAssignableFrom(m.ReturnType));
            }
            catch (Exception ex)
            {
                AppLogger.Log("ReverseMapBuilder: failed to get collection methods", ex);
                return Enumerable.Empty<MethodInfo>();
            }
        }

        /// <summary>
        /// Gets records from a collection method by invoking it and obtaining WinningOverrides.
        /// </summary>
        private static System.Collections.IEnumerable GetRecordsFromMethod(MethodInfo method, object priorityRoot)
        {
            try
            {
                var collection = method.Invoke(priorityRoot, null);
                if (collection == null) return Enumerable.Empty<object>();

                var winMethod = collection.GetType().GetMethod("WinningOverrides");
                if (winMethod != null)
                {
                    var items = winMethod.Invoke(collection, null);
                    return items as System.Collections.IEnumerable ?? Enumerable.Empty<object>();
                }
                else if (collection is System.Collections.IEnumerable enumerable)
                {
                    return enumerable;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log($"ReverseMapBuilder: failed to get records from method {method.Name}", ex);
            }
            
            return Enumerable.Empty<object>();
        }

        /// <summary>
        /// Processes a collection of records and adds their references to the reverse map.
        /// </summary>
        private static void ProcessRecords(
            System.Collections.IEnumerable records, 
            HashSet<string> excluded, 
            Dictionary<string, List<(object, string, object)>> reverseMap)
        {
            foreach (var rec in records)
            {
                if (rec == null) continue;
                
                try
                {
                    ProcessRecord(rec, excluded, reverseMap);
                }
                catch (Exception ex)
                {
                    AppLogger.Log("ReverseMapBuilder: failed processing record", ex);
                }
            }
        }

        /// <summary>
        /// Processes a single record and extracts FormKey references from its properties.
        /// </summary>
        private static void ProcessRecord(
            object rec, 
            HashSet<string> excluded, 
            Dictionary<string, List<(object, string, object)>> reverseMap)
        {
            var props = rec.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var prop in props)
            {
                try
                {
                    ProcessProperty(rec, prop, excluded, reverseMap);
                }
                catch (Exception ex)
                {
                    AppLogger.Log($"ReverseMapBuilder: failed processing property {prop.Name}", ex);
                }
            }
        }

        /// <summary>
        /// Processes a single property and adds it to the reverse map if it contains a FormKey reference.
        /// </summary>
        private static void ProcessProperty(
            object rec, 
            PropertyInfo prop, 
            HashSet<string> excluded, 
            Dictionary<string, List<(object, string, object)>> reverseMap)
        {
            var val = prop.GetValue(rec);
            if (val == null) return;

            // Try to extract FormKey from the property value
            var formKeyRef = TryExtractFormKeyReference(val);
            if (formKeyRef == null) return;

            var (plugin, id) = formKeyRef.Value;
            
            // Check exclusion
            if (IsExcluded(plugin, excluded)) return;

            // Add to reverse map
            var key = $"{plugin}:{id:X8}";
            if (!reverseMap.TryGetValue(key, out var list))
            {
                list = new List<(object, string, object)>();
                reverseMap[key] = list;
            }
            list.Add((rec, prop.Name, val));
        }

        /// <summary>
        /// Attempts to extract a FormKey reference (plugin, ID) from a property value.
        /// </summary>
        /// <returns>Tuple of (plugin, id) if extraction succeeded, null otherwise</returns>
        private static (string Plugin, uint Id)? TryExtractFormKeyReference(object val)
        {
            try
            {
                // Check if this value has a FormKey property
                var nestedFkProp = val.GetType().GetProperty("FormKey");
                if (nestedFkProp == null) return null;

                var nestedFk = nestedFkProp.GetValue(val);
                if (nestedFk == null) return null;

                // Use helper to extract plugin and ID
                if (MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPluginAndIdFromRecord(nestedFk, out string? plugin, out uint id))
                {
                    return (plugin, id);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log("ReverseMapBuilder: failed extracting FormKey reference", ex);
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
            catch (Exception ex)
            {
                AppLogger.Log("ReverseMapBuilder: failed checking excluded plugin", ex);
                return false;
            }
        }
    }
}
