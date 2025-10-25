using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Interfaces;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using System.Text;
using System.Reflection;

namespace MunitionAutoPatcher.Services.Implementations;

public class WeaponOmodExtractor : IWeaponOmodExtractor
{
    private readonly ILoadOrderService _loadOrderService;

    public WeaponOmodExtractor(ILoadOrderService loadOrderService)
    {
        _loadOrderService = loadOrderService;
    }

    public async Task<List<OmodCandidate>> ExtractCandidatesAsync(IProgress<string>? progress = null)
    {
        progress?.Report("OMOD/COBJ の候補を抽出しています...");
        var results = new List<OmodCandidate>();

        var loadOrder = await _loadOrderService.GetLoadOrderAsync();
        if (loadOrder == null)
        {
            progress?.Report("エラー: ロードオーダーが取得できませんでした");
            return results;
        }

            try
            {
                using var env = GameEnvironment.Typical.Fallout4(Fallout4Release.Fallout4);

            // 1) Enumerate all ConstructibleObject records and record those that create objects (CreatedObject will usually be a weapon/item)
            var cobjs = env.LoadOrder.PriorityOrder.ConstructibleObject().WinningOverrides();
            foreach (var cobj in cobjs)
            {
                try
                {
                    var created = cobj.CreatedObject;
                    if (created.IsNull) continue;

                    // Record the created object's FormKey as a candidate. Resolution to a Weapon record (to inspect Ammo) may be done later.
                        // Try to resolve the created object to a weapon or ammo record by scanning the env PriorityOrder collections.
                        FormKey? createdAmmoKey = null;
                        string createdAmmoName = string.Empty;
                        try
                        {
                            // created.FormKey gives us the plugin + id
                            var plugin = created.FormKey.ModKey.FileName;
                            var id = created.FormKey.ID;

                            // Try to find a weapon record that matches
                            var possibleWeapon = env.LoadOrder.PriorityOrder.Weapon().WinningOverrides().FirstOrDefault(w => w.FormKey.ModKey.FileName == plugin && w.FormKey.ID == id);
                            if (possibleWeapon != null)
                            {
                                // If the created weapon references ammo, try to capture it
                                var ammoLink = possibleWeapon.Ammo;
                                if (!ammoLink.IsNull)
                                {
                                    if (ammoLink.FormKey != null)
                                    {
                                        createdAmmoKey = new FormKey { PluginName = ammoLink.FormKey.ModKey.FileName, FormId = ammoLink.FormKey.ID };
                                    }
                                }
                                if (createdAmmoKey != null)
                                {
                                    // We have an ammo FormKey but resolving the actual Ammo record via PriorityOrder.Ammo()
                                    // is not always available across Mutagen versions. Leave the name blank; the UI
                                    // can later resolve names via LinkCache when running inside MO2.
                                    createdAmmoName = string.Empty;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            try
                            {
                                if (System.Windows.Application.Current?.MainWindow?.DataContext is MunitionAutoPatcher.ViewModels.MainViewModel mainVm)
                                    mainVm.AddLog($"WeaponOmodExtractor: COBJ created-object scan error: {ex.Message}");
                            }
                            catch { }
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
                catch { }
            }

                // 2) ObjectMod record enumeration omitted: Mutagen's API varies across versions and
                //    ObjectMod-specific extension helpers may not be present. We currently focus on
                //    ConstructibleObject (COBJ) CreatedObject discovery. ObjectMod support can be
                //    added later by scanning the load order or using LinkCache reverse-reference methods
                //    when available in the runtime environment.

                // 3) When running inside GameEnvironment (MO2) we can attempt a generic reverse-reference
                // scan: reflect over the PriorityOrder object to enumerate available record collections
                // (Weapon/ConstructibleObject/ObjectMod/etc.) and scan each record's public properties
                // for FormLink/FormKey-like fields that reference a weapon FormKey. This approach avoids
                // calling unavailable typed extension methods directly and remains resilient across
                // Mutagen versions. All reflection calls are wrapped in try/catch to preserve stability.
                try
                {
                    // Build a quick weapon-set for lookup
                    var weapons = env.LoadOrder.PriorityOrder.Weapon().WinningOverrides().ToList();
                    var weaponKeys = new HashSet<(string Plugin, uint Id)>(weapons.Select(w => (w.FormKey.ModKey.FileName.ToString(), w.FormKey.ID)));

                    var priority = env.LoadOrder.PriorityOrder;
                    var methods = priority.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .Where(m => m.GetParameters().Length == 0 && typeof(System.Collections.IEnumerable).IsAssignableFrom(m.ReturnType));

                    foreach (var m in methods)
                    {
                        object? collection = null;
                        try
                        {
                            collection = m.Invoke(priority, null);
                        }
                        catch { continue; }

                        if (collection == null) continue;

                        // Some collection objects expose a WinningOverrides() that yields the concrete record getters
                        var winMethod = collection.GetType().GetMethod("WinningOverrides");
                        System.Collections.IEnumerable? items = null;
                        try
                        {
                            if (winMethod != null)
                                items = winMethod.Invoke(collection, null) as System.Collections.IEnumerable;
                            else if (collection is System.Collections.IEnumerable en)
                                items = en;
                        }
                        catch { items = null; }

                        if (items == null) continue;

                                foreach (var rec in items)
                                {
                                    try
                                    {
                                if (rec == null) continue;

                                // Try to get record FormKey if present
                                string recPlugin = string.Empty;
                                uint recId = 0;
                                try
                                {
                                    var fkProp = rec.GetType().GetProperty("FormKey");
                                    if (fkProp != null)
                                    {
                                        var fk = fkProp.GetValue(rec);
                                        if (fk != null)
                                        {
                                            var mk = fk.GetType().GetProperty("ModKey")?.GetValue(fk);
                                            var idObj = fk.GetType().GetProperty("ID")?.GetValue(fk);
                                            recPlugin = mk?.GetType().GetProperty("FileName")?.GetValue(mk)?.ToString() ?? string.Empty;
                                            if (idObj is uint u) recId = u;
                                            else if (idObj != null) recId = Convert.ToUInt32(idObj);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    try
                                    {
                                        if (System.Windows.Application.Current?.MainWindow?.DataContext is MunitionAutoPatcher.ViewModels.MainViewModel mainVm)
                                            mainVm.AddLog($"WeaponOmodExtractor: reflection property scan error: {ex.Message}");
                                    }
                                    catch { }
                                }

                                // Inspect public properties for nested FormKey/FormLink fields
                                var props = rec.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                                foreach (var p in props)
                                {
                                    try
                                    {
                                        var val = p.GetValue(rec);
                                        if (val == null) continue;

                                        // Common pattern: a FormLink struct exposes a FormKey property
                                        var nestedFkProp = val.GetType().GetProperty("FormKey");
                                        if (nestedFkProp == null) continue;

                                        var nestedFk = nestedFkProp.GetValue(val);
                                        if (nestedFk == null) continue;

                                        var mk = nestedFk.GetType().GetProperty("ModKey")?.GetValue(nestedFk);
                                        var idObj = nestedFk.GetType().GetProperty("ID")?.GetValue(nestedFk);
                                        string plugin = mk?.GetType().GetProperty("FileName")?.GetValue(mk)?.ToString() ?? string.Empty;
                                        uint id = 0;
                                        if (idObj is uint uu) id = uu;
                                        else if (idObj != null) id = Convert.ToUInt32(idObj);

                                        if (string.IsNullOrEmpty(plugin) || id == 0) continue;

                                        if (weaponKeys.Contains((plugin, id)))
                                        {
                                            // We found a record that references a weapon. Record as a candidate.
                                            var recEditorId = string.Empty;
                                            try { recEditorId = rec.GetType().GetProperty("EditorID")?.GetValue(rec)?.ToString() ?? string.Empty; } catch { }

                                            var candidate = new OmodCandidate
                                            {
                                                CandidateType = m.Name, // method name (e.g. "ObjectMod", "ConstructibleObject", ...)
                                                CandidateFormKey = new Models.FormKey { PluginName = recPlugin ?? string.Empty, FormId = recId },
                                                CandidateEditorId = recEditorId,
                                                BaseWeapon = new Models.FormKey { PluginName = plugin, FormId = id },
                                                BaseWeaponEditorId = weapons.FirstOrDefault(w => w.FormKey.ModKey.FileName == plugin && w.FormKey.ID == id)?.EditorID ?? string.Empty,
                                                CandidateAmmo = null,
                                                CandidateAmmoName = string.Empty,
                                                SourcePlugin = recPlugin ?? string.Empty,
                                                Notes = $"Reference found in {m.Name}.{p.Name} -> {plugin}:{id:X8}",
                                                SuggestedTarget = "Reference"
                                            };

                                            results.Add(candidate);
                                        }
                                    }
                                    catch { }
                                }
                            }
                            catch (Exception ex)
                            {
                                try
                                {
                                    if (System.Windows.Application.Current?.MainWindow?.DataContext is MunitionAutoPatcher.ViewModels.MainViewModel mainVm)
                                        mainVm.AddLog($"WeaponOmodExtractor: record enumeration error: {ex.Message}");
                                }
                                catch { }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Non-fatal; reflection scanning is best-effort
                    progress?.Report($"注意: 逆参照スキャン中に例外が発生しました（無視します）: {ex.Message}");
                }
            // 2) ObjectMod enumeration is not performed here (API surface differs per Mutagen version); subject to future enhancement.

            // 3) (Reference scanning via LinkCache was skipped to avoid version-specific API complexity.)

            // 4) Write CSV for debugging into artifacts
            try
            {
                var repoRoot = FindRepoRoot();
                var artifactsDir = System.IO.Path.Combine(repoRoot, "artifacts", "RobCo_Patcher");
                if (!System.IO.Directory.Exists(artifactsDir))
                    System.IO.Directory.CreateDirectory(artifactsDir);

                var fileName = $"weapon_omods_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var path = System.IO.Path.Combine(artifactsDir, fileName);
                using var sw = new System.IO.StreamWriter(path, false, Encoding.UTF8);
                sw.WriteLine("CandidateType,BaseWeapon,BaseEditorId,CandidateFormKey,CandidateEditorId,CandidateAmmo,SourcePlugin,Notes,SuggestedTarget");
                foreach (var c in results)
                {
                    var baseKey = c.BaseWeapon != null ? $"{c.BaseWeapon.PluginName}:{c.BaseWeapon.FormId:X8}" : string.Empty;
                    var candKey = c.CandidateFormKey != null ? $"{c.CandidateFormKey.PluginName}:{c.CandidateFormKey.FormId:X8}" : string.Empty;
                    var ammoKey = c.CandidateAmmo != null ? $"{c.CandidateAmmo.PluginName}:{c.CandidateAmmo.FormId:X8}" : string.Empty;
                    sw.WriteLine($"{c.CandidateType},{baseKey},{Escape(c.BaseWeaponEditorId)},{candKey},{Escape(c.CandidateEditorId)},{ammoKey},{c.SourcePlugin},{Escape(c.Notes)},{c.SuggestedTarget}");
                }
                sw.Flush();
                progress?.Report($"OMOD 抽出 CSV を生成しました: {path}");
            }
            catch (Exception ex)
            {
                progress?.Report($"警告: CSV の出力に失敗しました: {ex.Message}");
            }

            progress?.Report($"抽出完了: {results.Count} 件の候補を検出しました");
            return results;
        }
        catch (Exception ex)
        {
            progress?.Report($"エラー: OMOD 抽出中に例外が発生しました: {ex.Message}");
            return results;
        }
    }

    private string Escape(string? s)
    {
        if (s == null) return string.Empty;
        return s.Replace("\"", "\\\"").Replace(',', ';');
    }

    private string FindRepoRoot()
    {
        try
        {
            var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var solutionPath = System.IO.Path.Combine(dir.FullName, "MunitionAutoPatcher.sln");
                if (System.IO.File.Exists(solutionPath))
                    return dir.FullName;
                dir = dir.Parent;
            }
        }
        catch (Exception ex)
        {
            try { if (System.Windows.Application.Current?.MainWindow?.DataContext is MunitionAutoPatcher.ViewModels.MainViewModel mainVm) mainVm.AddLog($"WeaponOmodExtractor.FindRepoRoot error: {ex.Message}"); } catch { }
        }
        return AppContext.BaseDirectory;
    }
}
