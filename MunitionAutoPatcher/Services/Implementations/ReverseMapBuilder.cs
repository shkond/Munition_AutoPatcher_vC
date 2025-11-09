using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// Builds a reverse-reference map (FormKey -> list of (sourceRecord, propertyName, propertyValue)).
/// This implementation uses IMutagenEnvironment so it can be written in a statically-typed
/// and testable way while remaining version-adaptive via adapters.
/// </summary>
public class ReverseMapBuilder
{
    private readonly IMutagenEnvironment _env;
    private readonly ILogger<ReverseMapBuilder> _logger;

    public ReverseMapBuilder(IMutagenEnvironment env, ILogger<ReverseMapBuilder> logger)
    {
        _env = env;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Dictionary<string, List<(object Record, string PropName, object PropValue)>> Build(HashSet<string> excluded)
    {
        var reverseMap = new Dictionary<string, List<(object, string, object)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var col in _env.EnumerateRecordCollectionsTyped())
        {
            try
            {
                foreach (var rec in col.Items)
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

                                try { if (excluded != null && excluded.Contains(plugin)) continue; } catch { }

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
                                _logger.LogError(ex, "ReverseMapBuilder: exception while scanning properties");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "ReverseMapBuilder: exception while processing record");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReverseMapBuilder: failed while enumerating collection {Name}", col.Name);
            }
        }

        return reverseMap;
    }
}
