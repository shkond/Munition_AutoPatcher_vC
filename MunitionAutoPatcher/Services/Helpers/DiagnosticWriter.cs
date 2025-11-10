using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MunitionAutoPatcher.Models;
using Microsoft.Extensions.Logging;

namespace MunitionAutoPatcher.Services.Helpers
{
    internal static class DiagnosticWriter
    {
        public static string WriteReverseMapMarker(Dictionary<string, List<(object Record, string PropName, object PropValue)>> reverseMap, string repoRoot, ILogger logger)
        {
            var artifactsDirRm = Path.Combine(repoRoot ?? AppContext.BaseDirectory, "artifacts", "RobCo_Patcher");
            if (!Directory.Exists(artifactsDirRm)) Directory.CreateDirectory(artifactsDirRm);
            var rmMarker = Path.Combine(artifactsDirRm, $"reverse_map_built_{DateTime.Now:yyyyMMdd_HHmmss_fff}.txt");
            try
            {
                using var rsm = new StreamWriter(rmMarker, false, Encoding.UTF8);
                rsm.WriteLine($"Reverse reference map build attempted at {DateTime.Now:O}");
                rsm.WriteLine($"ReverseMapKeys={reverseMap?.Count ?? 0}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "DiagnosticWriter: WriteReverseMapMarker failed");
            }
            return rmMarker;
        }

        private static string Escape(string s)
        {
            if (s == null) return string.Empty;
            return s.Replace("\"", "\"\"");
        }

        public static string WriteNoveskeDiagnostic(
            Dictionary<string, List<(object Record, string PropName, object PropValue)>> reverseMap,
            IEnumerable<object> weaponsList,
            IEnumerable<OmodCandidate> results,
            string[] diagPlugins,
            string repoRoot,
            ILogger logger)
        {
            var artifactsDirDiag = Path.Combine(repoRoot ?? AppContext.BaseDirectory, "artifacts", "RobCo_Patcher");
            if (!Directory.Exists(artifactsDirDiag)) Directory.CreateDirectory(artifactsDirDiag);

            var diagFile = Path.Combine(artifactsDirDiag, $"noveske_diagnostic_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            try
            {
                using var dsw = new StreamWriter(diagFile, false, Encoding.UTF8);
                dsw.WriteLine("WeaponFormKey,EditorId,ReverseRefCount,ReverseSourcePlugins,ConfirmedCandidatesCount");

                foreach (var wobj in weaponsList)
                {
                    try
                    {
                        // Reflect to get plugin and id
                        var fk = wobj.GetType().GetProperty("FormKey")?.GetValue(wobj);
                        if (fk == null) continue;
                        var mk = fk.GetType().GetProperty("ModKey")?.GetValue(fk);
                        var plugin = mk?.GetType().GetProperty("FileName")?.GetValue(mk)?.ToString() ?? string.Empty;
                        var idObj = fk.GetType().GetProperty("ID")?.GetValue(fk);
                        uint id = 0;
                        if (idObj is uint uu) id = uu;
                        else if (idObj != null) id = Convert.ToUInt32(idObj);
                        if (string.IsNullOrEmpty(plugin) || id == 0) continue;

                        if (diagPlugins != null && diagPlugins.Length > 0 && !diagPlugins.Any(dp => string.Equals(plugin, dp, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        var wk = $"{plugin}:{id:X8}";
                        var editor = wobj.GetType().GetProperty("EditorID")?.GetValue(wobj)?.ToString() ?? string.Empty;
                        int refCount = 0;
                        var srcPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        if (reverseMap != null && reverseMap.TryGetValue(wk, out var lst))
                        {
                            refCount = lst.Count;
                            foreach (var t in lst)
                            {
                                try
                                {
                                    var fkPropSrc = t.Record.GetType().GetProperty("FormKey");
                                    if (fkPropSrc != null)
                                    {
                                        var fkSrc = fkPropSrc.GetValue(t.Record);
                                        if (fkSrc != null)
                                        {
                                            var mkSrc = fkSrc.GetType().GetProperty("ModKey")?.GetValue(fkSrc);
                                            var srcPlugin = mkSrc?.GetType().GetProperty("FileName")?.GetValue(mkSrc)?.ToString() ?? string.Empty;
                                            if (!string.IsNullOrEmpty(srcPlugin)) srcPlugins.Add(srcPlugin);
                                        }
                                    }
                                }
                                catch (Exception innerEx)
                                {
                                    logger.LogError(innerEx, "DiagnosticWriter: failed to inspect reverseMap source record");
                                }
                            }
                        }

                        var confirmed = results?.Count(r => r.BaseWeapon != null && string.Equals(r.BaseWeapon.PluginName, plugin, StringComparison.OrdinalIgnoreCase) && r.BaseWeapon.FormId == id && r.ConfirmedAmmoChange) ?? 0;

                        dsw.WriteLine($"{wk},{Escape(editor)},{refCount},\"{Escape(string.Join(";", srcPlugins))}\",{confirmed}");
                    }
                    catch (Exception innerEx)
                    {
                        logger.LogError(innerEx, "DiagnosticWriter: failed to process weapon object for diagnostic");
                    }
                }

                dsw.Flush();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "DiagnosticWriter: WriteNoveskeDiagnostic failed");
            }

            return diagFile;
        }
    }
}
