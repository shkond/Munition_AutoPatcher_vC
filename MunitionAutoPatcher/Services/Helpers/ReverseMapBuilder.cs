using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MunitionAutoPatcher.Services.Helpers
{
    internal static class ReverseMapBuilder
    {
        // Build a reverse-reference map: "Plugin:ID" -> list of (record, propName, propValue)
        public static Dictionary<string, List<(object Record, string PropName, object PropValue)>> Build(object priorityRoot, HashSet<string> excluded)
        {
            var reverseMap = new Dictionary<string, List<(object, string, object)>>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var methods = priorityRoot.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.GetParameters().Length == 0 && typeof(System.Collections.IEnumerable).IsAssignableFrom(m.ReturnType));

                foreach (var m in methods)
                {
                    object? collection = null;
                    try { collection = m.Invoke(priorityRoot, null); } catch (Exception ex) { AppLogger.Log("ReverseMapBuilder: failed to invoke collection method", ex); continue; }
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
                        AppLogger.Log("ReverseMapBuilder: failed to obtain WinningOverrides or enumerate collection", ex);
                        items = null;
                    }
                    if (items == null) continue;

                    foreach (var rec in items)
                    {
                        if (rec == null) continue;
                        try
                        {
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
                                    var mk = nestedFk.GetType().GetProperty("ModKey")?.GetValue(nestedFk);
                                    var idObj = nestedFk.GetType().GetProperty("ID")?.GetValue(nestedFk);
                                    var plugin = mk?.GetType().GetProperty("FileName")?.GetValue(mk)?.ToString() ?? string.Empty;
                                    uint id = 0;
                                    if (idObj is uint u) id = u;
                                    else if (idObj != null) id = Convert.ToUInt32(idObj);
                                    if (string.IsNullOrEmpty(plugin) || id == 0) continue;
                                    try { if (excluded != null && excluded.Contains(plugin)) continue; } catch (Exception ex) { AppLogger.Log("ReverseMapBuilder: failed checking excluded plugin", ex); }
                                    var key = $"{plugin}:{id:X8}";
                                    if (!reverseMap.TryGetValue(key, out var list))
                                    {
                                        list = new List<(object, string, object)>();
                                        reverseMap[key] = list;
                                    }
                                    list.Add((rec, p.Name, val));
                                }
                                catch (Exception ex)
                                {
                                    AppLogger.Log("ReverseMapBuilder: failed processing property on record", ex);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            AppLogger.Log("ReverseMapBuilder: failed processing record in items", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log("ReverseMapBuilder: Build failed", ex);
            }

            return reverseMap;
        }
    }
}
