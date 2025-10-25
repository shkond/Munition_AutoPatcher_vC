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
    private readonly IConfigService _configService;

    public WeaponOmodExtractor(ILoadOrderService loadOrderService, IConfigService configService)
    {
        _loadOrderService = loadOrderService;
        _configService = configService;
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

                // Load excluded plugins from config (blacklist). Candidates from these plugins will be skipped.
                var excluded = new System.Collections.Generic.HashSet<string>(_configService.GetExcludedPlugins() ?? System.Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
                int skippedByExcluded = 0;

            // 1) Enumerate all ConstructibleObject records and record those that create objects (CreatedObject will usually be a weapon/item)
            var cobjs = env.LoadOrder.PriorityOrder.ConstructibleObject().WinningOverrides();
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
                        if (excluded.Contains(srcPlugin))
                        {
                            skippedByExcluded++;
                            continue;
                        }
                    }
                catch (Exception ex) { AppLogger.Log("Suppressed exception (empty catch) in WeaponOmodExtractor: iterating COBJs", ex); }

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

                                        if (skippedByExcluded > 0)
                                            progress?.Report($"{skippedByExcluded} 件のレコードが除外プラグイン設定のためスキップされました。");
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
                            catch (Exception ex2) { AppLogger.Log("WeaponOmodExtractor: failed to add log to UI in COBJ created-object scan catch", ex2); }
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
                catch (Exception ex) { AppLogger.Log("Suppressed exception (empty catch) in WeaponOmodExtractor: processing COBJ candidate", ex); }
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
                        catch (Exception ex) { AppLogger.Log("Suppressed exception (empty catch) in WeaponOmodExtractor: invoking collection method", ex); continue; }

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
                        catch (Exception ex) { AppLogger.Log("Suppressed exception (empty catch) in WeaponOmodExtractor: obtaining items from collection", ex); items = null; }

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
                                                catch (Exception ex2) { AppLogger.Log("WeaponOmodExtractor: failed to add log to UI in reflection property scan catch", ex2); }
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
                                            try { recEditorId = rec.GetType().GetProperty("EditorID")?.GetValue(rec)?.ToString() ?? string.Empty; } catch (Exception ex) { AppLogger.Log("WeaponOmodExtractor: failed to read EditorID via reflection", ex); }

                                            // Try to detect whether this record modifies ammo: scan its other properties for Ammo/Projectile FormLinks
                                            FormKey? detectedAmmoKey = null;
                                            try
                                            {
                                                var allProps = rec.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                                                foreach (var q in allProps)
                                                {
                                                    try
                                                    {
                                                        // Skip the property that pointed to the weapon itself (p)
                                                        if (q.Name == p.Name) continue;

                                                        // If property name suggests ammo/ projectile, inspect deeper
                                                        var lname = q.Name.ToLowerInvariant();
                                                        if (!lname.Contains("ammo") && !lname.Contains("projectile") && !lname.Contains("bullet"))
                                                        {
                                                            // still inspect: some FormLink fields don't have obvious names
                                                        }

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
                                                                    var mkq = fkq.GetType().GetProperty("ModKey")?.GetValue(fkq);
                                                                    var idObjq = fkq.GetType().GetProperty("ID")?.GetValue(fkq);
                                                                    var pluginq = mkq?.GetType().GetProperty("FileName")?.GetValue(mkq)?.ToString() ?? string.Empty;
                                                                    uint idq = 0;
                                                                    if (idObjq is uint uuq) idq = uuq;
                                                                    else if (idObjq != null) idq = Convert.ToUInt32(idObjq);
                                                                    if (!string.IsNullOrEmpty(pluginq) && idq != 0)
                                                                    {
                                                                        // Heuristic: if the referenced Form is not the weapon itself, consider as ammo candidate
                                                                        if (!(string.Equals(pluginq, plugin, StringComparison.OrdinalIgnoreCase) && idq == id))
                                                                        {
                                                                            detectedAmmoKey = new FormKey { PluginName = pluginq, FormId = idq };
                                                                            break;
                                                                        }
                                                                    }
                                                                }
                                                                catch (Exception ex) { AppLogger.Log("Suppressed exception (empty catch) in WeaponOmodExtractor: inner property detection", ex); }
                                                            }
                                                        }
                                                    }
                                                    catch (Exception ex) { AppLogger.Log("Suppressed exception (empty catch) in WeaponOmodExtractor: iterating properties q", ex); }
                                                }
                                            }
                                            catch (Exception ex) { AppLogger.Log("Suppressed exception (empty catch) in WeaponOmodExtractor: scanning allProps", ex); }

                                            // If this record originates from an excluded plugin, skip adding it as a candidate.
                                            if (excluded.Contains(recPlugin ?? string.Empty))
                                            {
                                                skippedByExcluded++;
                                            }
                                            else
                                            {
                                                var candidate = new OmodCandidate
                                                {
                                                    CandidateType = m.Name, // method name (e.g. "ObjectMod", "ConstructibleObject", ...)
                                                    CandidateFormKey = new Models.FormKey { PluginName = recPlugin ?? string.Empty, FormId = recId },
                                                    CandidateEditorId = recEditorId,
                                                    BaseWeapon = new Models.FormKey { PluginName = plugin, FormId = id },
                                                    BaseWeaponEditorId = weapons.FirstOrDefault(w => w.FormKey.ModKey.FileName == plugin && w.FormKey.ID == id)?.EditorID ?? string.Empty,
                                                    CandidateAmmo = detectedAmmoKey != null ? new Models.FormKey { PluginName = detectedAmmoKey.PluginName, FormId = detectedAmmoKey.FormId } : null,
                                                    CandidateAmmoName = string.Empty,
                                                    SourcePlugin = recPlugin ?? string.Empty,
                                                    Notes = $"Reference found in {m.Name}.{p.Name} -> {plugin}:{id:X8}" + (detectedAmmoKey != null ? $";DetectedAmmo={detectedAmmoKey.PluginName}:{detectedAmmoKey.FormId:X8}" : string.Empty),
                                                    SuggestedTarget = "Reference"
                                                };

                                                results.Add(candidate);
                                            }
                                        }
                                    }
                                    catch (Exception ex) { AppLogger.Log("Suppressed exception (empty catch) in WeaponOmodExtractor: iterating rec items", ex); }
                                }
                            }
                            catch (Exception ex)
                            {
                                try
                                {
                                    if (System.Windows.Application.Current?.MainWindow?.DataContext is MunitionAutoPatcher.ViewModels.MainViewModel mainVm)
                                        mainVm.AddLog($"WeaponOmodExtractor: record enumeration error: {ex.Message}");
                                }
                                catch (Exception ex2) { AppLogger.Log("WeaponOmodExtractor: failed to add log to UI in record enumeration", ex2); }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Non-fatal; reflection scanning is best-effort
                    progress?.Report($"注意: 逆参照スキャン中に例外が発生しました（無視します）: {ex.Message}");
                }

                // Resolution pass: when running under GameEnvironment we can resolve names via PriorityOrder/LinkCache
                Dictionary<string, object>? ammoMap = null;
                try
                {
                    var priority = env.LoadOrder.PriorityOrder;
                    // Build weapon map
                    var weaponGetters = priority.Weapon().WinningOverrides().ToList();
                    var weaponMap = weaponGetters.ToDictionary(w => ($"{w.FormKey.ModKey.FileName}:{w.FormKey.ID:X8}"), w => w);
                    // Try to build ammo map if Ammo() extension exists
                    try
                    {
                        var ammoMethod = priority.GetType().GetMethod("Ammo");
                        if (ammoMethod != null)
                        {
                            var ammoCollection = ammoMethod.Invoke(priority, null);
                            var win = ammoCollection?.GetType().GetMethod("WinningOverrides");
                            if (win != null)
                            {
                                var ammoList = win.Invoke(ammoCollection, null) as System.Collections.IEnumerable;
                                if (ammoList != null)
                                {
                                    ammoMap = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                                    foreach (var a in ammoList)
                                    {
                                        try
                                        {
                                            var plugin = a.GetType().GetProperty("FormKey")?.GetValue(a)?.GetType().GetProperty("ModKey")?.GetValue(a.GetType().GetProperty("FormKey")?.GetValue(a))?.GetType().GetProperty("FileName")?.GetValue(a.GetType().GetProperty("FormKey")?.GetValue(a))?.ToString();
                                            var idObj = a.GetType().GetProperty("FormKey")?.GetValue(a)?.GetType().GetProperty("ID")?.GetValue(a.GetType().GetProperty("FormKey")?.GetValue(a));
                                            uint id = 0;
                                            if (idObj is uint uu) id = uu;
                                            else if (idObj != null) id = Convert.ToUInt32(idObj);
                                            if (!string.IsNullOrEmpty(plugin) && id != 0)
                                            {
                                                var key = $"{plugin}:{id:X8}";
                                                ammoMap[key] = a;
                                            }
                                        }
                                        catch (Exception ex) { AppLogger.Log("Suppressed exception (empty catch) in WeaponOmodExtractor: building ammoMap entries", ex); }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex) { AppLogger.Log("Suppressed exception (empty catch) in WeaponOmodExtractor: building ammoMap", ex); ammoMap = null; }

                    // Populate candidate names/ids
                    foreach (var c in results)
                    {
                        try
                        {
                            // Resolve BaseWeapon editor id
                            if (c.BaseWeapon != null && string.IsNullOrEmpty(c.BaseWeaponEditorId))
                            {
                                var bk = $"{c.BaseWeapon.PluginName}:{c.BaseWeapon.FormId:X8}";
                                if (weaponMap.TryGetValue(bk, out var wgetter))
                                {
                                    try { c.BaseWeaponEditorId = wgetter.EditorID ?? string.Empty; } catch (Exception ex) { AppLogger.Log("WeaponOmodExtractor: failed to read BaseWeapon.EditorID", ex); }
                                    // If candidate has no ammo, try to read from the weapon's Ammo link
                                    try
                                    {
                                        var ammoLink = wgetter.GetType().GetProperty("Ammo")?.GetValue(wgetter);
                                        if (ammoLink != null)
                                        {
                                            var fk = ammoLink.GetType().GetProperty("FormKey")?.GetValue(ammoLink);
                                            if (fk != null)
                                            {
                                                var plugin = fk.GetType().GetProperty("ModKey")?.GetValue(fk)?.GetType().GetProperty("FileName")?.GetValue(fk.GetType().GetProperty("ModKey")?.GetValue(fk))?.ToString() ?? string.Empty;
                                                var idObj = fk.GetType().GetProperty("ID")?.GetValue(fk);
                                                uint id = 0;
                                                if (idObj is uint uu) id = uu;
                                                else if (idObj != null) id = Convert.ToUInt32(idObj);
                                                if (!string.IsNullOrEmpty(plugin) && id != 0)
                                                {
                                                    c.CandidateAmmo = new Models.FormKey { PluginName = plugin, FormId = id };
                                                    var key = $"{plugin}:{id:X8}";
                                                    if (ammoMap != null && ammoMap.TryGetValue(key, out var ammoGetter))
                                                    {
                                                        try { c.CandidateAmmoName = ammoGetter.GetType().GetProperty("EditorID")?.GetValue(ammoGetter)?.ToString() ?? string.Empty; } catch (Exception ex) { AppLogger.Log("WeaponOmodExtractor: failed to read ammoGetter.EditorID", ex); }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception ex) { AppLogger.Log("WeaponOmodExtractor: error while inspecting ammo link on weapon getter", ex); }
                                }
                            }

                            // Resolve CandidateAmmoName
                            if (c.CandidateAmmo != null && string.IsNullOrEmpty(c.CandidateAmmoName) && ammoMap != null)
                            {
                                var ak = $"{c.CandidateAmmo.PluginName}:{c.CandidateAmmo.FormId:X8}";
                                if (ammoMap.TryGetValue(ak, out var ag))
                                {
                                    try { c.CandidateAmmoName = ag.GetType().GetProperty("EditorID")?.GetValue(ag)?.ToString() ?? string.Empty; } catch (Exception ex) { AppLogger.Log("WeaponOmodExtractor: failed to read ag.EditorID", ex); }
                                }
                            }
                        }
                        catch (Exception ex) { AppLogger.Log("Suppressed exception (empty catch) in WeaponOmodExtractor: resolving candidate names", ex); }
                    }
                }
                catch (Exception ex) { AppLogger.Log("Suppressed exception (empty catch) in WeaponOmodExtractor: resolution pass", ex); }
                // 2) ObjectMod enumeration is not performed here (API surface differs per Mutagen version); subject to future enhancement.

                // 3) Precise detection pass (TryResolve + reverse-reference map)
                // Build a reverse-reference map of FormKey -> list of (sourceRecord, propertyName, propertyValue)
                try
                {
                    var reverseMap = new Dictionary<string, List<(object Record, string PropName, object PropValue)>>(StringComparer.OrdinalIgnoreCase);
                    try
                    {
                        var priority2 = env.LoadOrder.PriorityOrder;
                        var methods2 = priority2.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                            .Where(m => m.GetParameters().Length == 0 && typeof(System.Collections.IEnumerable).IsAssignableFrom(m.ReturnType));

                        foreach (var m in methods2)
                        {
                            object? collection = null;
                            try { collection = m.Invoke(priority2, null); } catch (Exception ex) { AppLogger.Log("WeaponOmodExtractor: invoking collection method failed", ex); continue; }
                            if (collection == null) continue;

                            var winMethod = collection.GetType().GetMethod("WinningOverrides");
                            System.Collections.IEnumerable? items2 = null;
                            try
                            {
                                if (winMethod != null) items2 = winMethod.Invoke(collection, null) as System.Collections.IEnumerable;
                                else if (collection is System.Collections.IEnumerable en) items2 = en;
                            }
                            catch (Exception ex) { AppLogger.Log("WeaponOmodExtractor: obtaining items from collection failed", ex); items2 = null; }
                            if (items2 == null) continue;

                            foreach (var rec in items2)
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
                                            if (idObj is uint u2) id = u2;
                                            else if (idObj != null) id = Convert.ToUInt32(idObj);
                                            if (string.IsNullOrEmpty(plugin) || id == 0) continue;
                                            // Skip records originating from excluded plugins
                                            try
                                            {
                                                if (excluded.Contains(plugin))
                                                    continue;
                                            }
                                            catch (Exception ex) { AppLogger.Log("WeaponOmodExtractor: excluded plugin check failed", ex); }
                                            var key = $"{plugin}:{id:X8}";
                                            if (!reverseMap.TryGetValue(key, out var list))
                                            {
                                                list = new List<(object, string, object)>();
                                                reverseMap[key] = list;
                                            }
                                            list.Add((rec, p.Name, val));
                                        }
                                        catch (Exception ex) { AppLogger.Log("Suppressed exception in WeaponOmodExtractor: iterating sp properties", ex); }
                                    }
                                }
                                catch (Exception ex) { AppLogger.Log("Suppressed exception in WeaponOmodExtractor: iterating refs for base weapon", ex); }
                            }
                        }
                    }
                    catch (Exception ex) { AppLogger.Log("Suppressed exception in WeaponOmodExtractor: building reverse reference map", ex); }

                    // Try to get LinkCache if available for typed TryResolve
                    object? linkCache = null;
                    try { linkCache = env.GetType().GetProperty("LinkCache")?.GetValue(env); } catch (Exception ex) { AppLogger.Log("WeaponOmodExtractor: failed to obtain LinkCache via reflection", ex); linkCache = null; }

                    // Now, for each candidate that has a BaseWeapon, check reverseMap entries for that weapon.
                    foreach (var c in results)
                    {
                        try
                        {
                            if (c.BaseWeapon == null) continue;
                            var baseKey = $"{c.BaseWeapon.PluginName}:{c.BaseWeapon.FormId:X8}";
                            if (!reverseMap.TryGetValue(baseKey, out var refs)) continue;

                            foreach (var entry in refs)
                            {
                                try
                                {
                                    var sourceRec = entry.Record;
                                    // Skip source records from excluded plugins to keep exclusion consistent
                                    try
                                    {
                                        var fkPropSrc = sourceRec.GetType().GetProperty("FormKey");
                                        if (fkPropSrc != null)
                                        {
                                            var fkSrc = fkPropSrc.GetValue(sourceRec);
                                            if (fkSrc != null)
                                            {
                                                var mkSrc = fkSrc.GetType().GetProperty("ModKey")?.GetValue(fkSrc);
                                                var srcPlugin = mkSrc?.GetType().GetProperty("FileName")?.GetValue(mkSrc)?.ToString() ?? string.Empty;
                                                if (!string.IsNullOrEmpty(srcPlugin) && excluded.Contains(srcPlugin))
                                                    continue;
                                            }
                                        }
                                    }
                                    catch (Exception ex) { AppLogger.Log("WeaponOmodExtractor: failed while checking sourceRec.FormKey or excluded plugins", ex); }
                                    // Inspect properties of the source record to find ammo-like references
                                    var sprops = sourceRec.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                                    foreach (var sp in sprops)
                                    {
                                        try
                                        {
                                            var sval = sp.GetValue(sourceRec);
                                            if (sval == null) continue;
                                            var fkProp = sval.GetType().GetProperty("FormKey");
                                            if (fkProp == null) continue;
                                            var fk = fkProp.GetValue(sval);
                                            if (fk == null) continue;
                                            var mk = fk.GetType().GetProperty("ModKey")?.GetValue(fk);
                                            var idObj = fk.GetType().GetProperty("ID")?.GetValue(fk);
                                            var plugin = mk?.GetType().GetProperty("FileName")?.GetValue(mk)?.ToString() ?? string.Empty;
                                            uint id = 0;
                                            if (idObj is uint uu3) id = uu3;
                                            else if (idObj != null) id = Convert.ToUInt32(idObj);
                                            if (string.IsNullOrEmpty(plugin) || id == 0) continue;

                                            // First try LinkCache.TryResolve where available (use central helper)
                                            var resolved = MunitionAutoPatcher.Services.Implementations.LinkCacheHelper.TryResolveViaLinkCache(sval, linkCache);
                                            bool isAmmo = false;
                                            string resolvedTypeName = string.Empty;
                                            object? resolvedGetter = null;
                                            if (resolved != null)
                                            {
                                                resolvedGetter = resolved;
                                                resolvedTypeName = resolved.GetType().Name ?? string.Empty;
                                                var lname = resolvedTypeName.ToLowerInvariant();
                                                if (lname.Contains("ammo") || lname.Contains("projectile") || lname.Contains("projectilegetter"))
                                                    isAmmo = true;
                                            }

                                            // Fallback: check ammoMap built earlier
                                            var fkKey = $"{plugin}:{id:X8}";
                                            if (!isAmmo && fkKey != null && ammoMap != null && ammoMap.TryGetValue(fkKey, out var ammoGetterObj))
                                            {
                                                isAmmo = true;
                                                resolvedGetter = ammoGetterObj;
                                                try { resolvedTypeName = ammoGetterObj.GetType().Name ?? string.Empty; } catch (Exception ex) { AppLogger.Log("WeaponOmodExtractor: failed to read ammoGetter object type name", ex); }
                                            }

                                            if (isAmmo)
                                            {
                                                // Confirm candidate
                                                c.ConfirmedAmmoChange = true;
                                                c.ConfirmReason = $"Resolved {sp.Name} -> {resolvedTypeName} on {entry.Record.GetType().Name}";
                                                // Populate CandidateAmmo and CandidateAmmoName
                                                c.CandidateAmmo = new Models.FormKey { PluginName = plugin, FormId = id };
                                                if (resolvedGetter != null)
                                                {
                                                    try { c.CandidateAmmoName = resolvedGetter.GetType().GetProperty("EditorID")?.GetValue(resolvedGetter)?.ToString() ?? string.Empty; } catch (Exception ex) { AppLogger.Log("WeaponOmodExtractor: failed to read resolvedGetter.EditorID", ex); }
                                                }
                                                else
                                                {
                                                    // try to fill name from ammoMap key if available
                                                }

                                                // Stop on first confirmed ammo reference for performance
                                                break;
                                            }
                                        }
                                        catch (Exception ex) { AppLogger.Log("Suppressed exception in WeaponOmodExtractor: scanning properties for ammo refs", ex); }
                                    }
                                    if (c.ConfirmedAmmoChange) break;
                                }
                                catch (Exception ex) { AppLogger.Log("Suppressed exception in WeaponOmodExtractor: iterating reverse-reference entries for candidate", ex); }
                            }
                        }
                        catch (Exception ex) { AppLogger.Log("Suppressed exception in WeaponOmodExtractor: processing reverse-reference entries", ex); }
                    }
                }
                catch (Exception ex)
                {
                    try { if (System.Windows.Application.Current?.MainWindow?.DataContext is MunitionAutoPatcher.ViewModels.MainViewModel mainVm) mainVm.AddLog($"WeaponOmodExtractor: precise detection pass error: {ex.Message}"); } catch (Exception inner) { AppLogger.Log("WeaponOmodExtractor: failed to add log to UI in precise detection pass catch", inner); }
                }

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
                // Add ConfirmedAmmoChange and ConfirmReason and CandidateAmmoName to CSV
                sw.WriteLine("CandidateType,BaseWeapon,BaseEditorId,CandidateFormKey,CandidateEditorId,CandidateAmmo,CandidateAmmoName,SourcePlugin,Notes,SuggestedTarget,ConfirmedAmmoChange,ConfirmReason");
                foreach (var c in results)
                {
                    var baseKey = c.BaseWeapon != null ? $"{c.BaseWeapon.PluginName}:{c.BaseWeapon.FormId:X8}" : string.Empty;
                    var candKey = c.CandidateFormKey != null ? $"{c.CandidateFormKey.PluginName}:{c.CandidateFormKey.FormId:X8}" : string.Empty;
                    var ammoKey = c.CandidateAmmo != null ? $"{c.CandidateAmmo.PluginName}:{c.CandidateAmmo.FormId:X8}" : string.Empty;
                    var ammoName = c.CandidateAmmoName ?? string.Empty;
                    var confirmed = c.ConfirmedAmmoChange ? "true" : "false";
                    var reason = c.ConfirmReason ?? string.Empty;
                    sw.WriteLine($"{c.CandidateType},{baseKey},{Escape(c.BaseWeaponEditorId)},{candKey},{Escape(c.CandidateEditorId)},{ammoKey},{Escape(ammoName)},{c.SourcePlugin},{Escape(c.Notes)},{c.SuggestedTarget},{confirmed},{Escape(reason)}");
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

    // (Removed) use the shared LinkCacheHelper.TryResolveViaLinkCache from Services/Implementations/LinkCacheHelper.cs

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
            try { if (System.Windows.Application.Current?.MainWindow?.DataContext is MunitionAutoPatcher.ViewModels.MainViewModel mainVm) mainVm.AddLog($"WeaponOmodExtractor.FindRepoRoot error: {ex.Message}"); } catch (Exception inner) { AppLogger.Log("WeaponOmodExtractor.FindRepoRoot: failed to add log to UI", inner); }
        }
        return AppContext.BaseDirectory;
    }
}
