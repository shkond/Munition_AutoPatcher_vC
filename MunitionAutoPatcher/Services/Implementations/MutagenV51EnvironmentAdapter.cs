using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Environments;
// WinningOverrides<T>() extension methods are in Mutagen.Bethesda.Plugins.Records / Mutagen.Bethesda.Plugins order; no Core.Extensions in this version.
using MunitionAutoPatcher.Services.Interfaces;

namespace MunitionAutoPatcher.Services.Implementations;

public class MutagenV51EnvironmentAdapter : IMutagenEnvironment, IDisposable
{
    private readonly IGameEnvironment<IFallout4Mod, IFallout4ModGetter> _env;
    private readonly ILogger<MutagenV51EnvironmentAdapter> _logger;

    public MutagenV51EnvironmentAdapter(IGameEnvironment<IFallout4Mod, IFallout4ModGetter> env, ILogger<MutagenV51EnvironmentAdapter> logger)
    {
        _env = env;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    internal IGameEnvironment<IFallout4Mod, IFallout4ModGetter> InnerGameEnvironment => _env;

    public void Dispose()
    {
        try
        {
            (_env as IDisposable)?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MutagenV51EnvironmentAdapter: failed while disposing inner GameEnvironment");
        }
    }

    public IEnumerable<object> GetWinningWeaponOverrides()
    {
        try { return _env.LoadOrder.PriorityOrder.Weapon().WinningOverrides().Cast<object>(); }
        catch (Exception ex) { _logger?.LogWarning(ex, "MutagenV51EnvironmentAdapter: GetWinningWeaponOverrides failed"); return Enumerable.Empty<object>(); }
    }

    public IEnumerable<object> GetWinningConstructibleObjectOverrides()
    {
        try { return _env.LoadOrder.PriorityOrder.ConstructibleObject().WinningOverrides().Cast<object>(); }
        catch (Exception ex) { _logger?.LogWarning(ex, "MutagenV51EnvironmentAdapter: GetWinningConstructibleObjectOverrides failed"); return Enumerable.Empty<object>(); }
    }

    public IEnumerable<(string Name, IEnumerable<object> Items)> EnumerateRecordCollections()
    {
        var priority = _env.LoadOrder.PriorityOrder;

        // Curated, typed collections first (avoid reflection on extension methods)
        IEnumerable<(string Name, Func<IEnumerable<object>> Getter)> candidates = new (string, Func<IEnumerable<object>>)[]
        {
            ("Weapon", () => priority.Weapon().WinningOverrides().Cast<object>()),
            ("ObjectModification", () => GetObjectModificationWinningOverrides().Cast<object>()),
            ("ConstructibleObject", () => priority.ConstructibleObject().WinningOverrides().Cast<object>()),
            ("Armor", () => priority.Armor().WinningOverrides().Cast<object>()),
            ("Ammo", () => priority.Ammunition().WinningOverrides().Cast<object>())
        };

        foreach (var (name, getter) in candidates)
        {
            IEnumerable<object>? items = null;
            try
            {
                items = getter();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "EnumerateRecordCollections: failed to load curated collection {Name}", name);
                items = null;
            }

            if (items == null)
            {
                continue;
            }

            // Quick preview to ensure there are major records present
            bool hasMajor = false;
            int preview = 0;
            try
            {
                foreach (var obj in items.Take(500))
                {
                    preview++;
                    if (obj is IMajorRecordGetter) hasMajor = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "EnumerateRecordCollections: preview failure for {Name}", name);
            }

            if (!hasMajor)
            {
                _logger.LogDebug("EnumerateRecordCollections: curated {Name} has no IMajorRecordGetter elements (preview={Count})", name, preview);
                continue;
            }

            _logger.LogDebug("EnumerateRecordCollections: curated accept {Name} (preview~{Count})", name, preview);
            yield return (name, items);
        }

        // Fallback: very conservative reflection-based scan in case new collections are added in future versions.
        List<(string Name, IEnumerable<object> Items)> fallbackResults = new();
        try
        {
            var type = priority.GetType();
            var allMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            var candidateMethods = allMethods
                .Where(m => m.GetParameters().Length == 0 && typeof(System.Collections.IEnumerable).IsAssignableFrom(m.ReturnType))
                .ToList();

            var excludedMethods = new HashSet<string>(StringComparer.Ordinal)
            {
                "ToArray", "ToList", "Clone", "ToString", "GetEnumerator", "GetType"
            };

            _logger.LogDebug("EnumerateRecordCollections (fallback): scanning {MethodCount} methods on {Type}", candidateMethods.Count, type.FullName);

            foreach (var m in candidateMethods)
            {
                try
                {
                    if (excludedMethods.Contains(m.Name))
                    {
                        _logger.LogDebug("EnumerateRecordCollections: excluded by name: {Method} (return={Return})", m.Name, m.ReturnType.FullName);
                        continue;
                    }
                    if (m.ReturnType.Namespace != null && m.ReturnType.Namespace.StartsWith("System", StringComparison.Ordinal))
                    {
                        _logger.LogDebug("EnumerateRecordCollections: excluded by namespace: {Method} (return={Return})", m.Name, m.ReturnType.FullName);
                        continue;
                    }

                    object? collection = null;
                    try { collection = m.Invoke(priority, null); }
                    catch (Exception ex) { _logger.LogDebug(ex, "EnumerateRecordCollections: invoke failed for {Method}", m.Name); }
                    if (collection == null) continue;

                    var winMethod = collection.GetType().GetMethod("WinningOverrides");
                    IEnumerable<object>? items2 = null;
                    try
                    {
                        if (winMethod != null) items2 = (winMethod.Invoke(collection, null) as System.Collections.IEnumerable)?.Cast<object>();
                        else if (collection is System.Collections.IEnumerable en) items2 = en.Cast<object>();
                    }
                    catch (Exception ex) { _logger.LogDebug(ex, "EnumerateRecordCollections: materialize failed for {Method}", m.Name); }
                    if (items2 == null) continue;

                    bool hasMajorRecord = false;
                    int previewCount = 0;
                    try
                    {
                        foreach (var obj in items2.Take(500))
                        {
                            previewCount++;
                            if (obj is IMajorRecordGetter) hasMajorRecord = true;
                        }
                    }
                    catch (Exception ex) { _logger.LogDebug(ex, "EnumerateRecordCollections: preview failed for {Method}", m.Name); }

                    if (!hasMajorRecord)
                    {
                        _logger.LogDebug("EnumerateRecordCollections: skipped {Method} (no IMajorRecordGetter elements, preview={Count})", m.Name, previewCount);
                        continue;
                    }

                    _logger.LogDebug("EnumerateRecordCollections: accepted {Method} (fallback) with ~{Count} items", m.Name, previewCount);
                    fallbackResults.Add((m.Name, items2));
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "EnumerateRecordCollections: unexpected failure during fallback processing for {Method}", m.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "EnumerateRecordCollections: fallback reflection scan failed");
        }

        foreach (var t in fallbackResults)
            yield return t;
    }

    // Typed methods implementation
    public IEnumerable<IWeaponGetter> GetWinningWeaponOverridesTyped()
    {
        try
        {
            // Try direct accessor first
            try { return _env.LoadOrder.PriorityOrder.Weapon().WinningOverrides(); } catch { }
            return Enumerable.Empty<IWeaponGetter>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetWinningWeaponOverridesTyped failed");
            return Enumerable.Empty<IWeaponGetter>();
        }
    }

    public IEnumerable<IConstructibleObjectGetter> GetWinningConstructibleObjectOverridesTyped()
    {
        try
        {
            try { return _env.LoadOrder.PriorityOrder.ConstructibleObject().WinningOverrides(); } catch { }
            return Enumerable.Empty<IConstructibleObjectGetter>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetWinningConstructibleObjectOverridesTyped failed");
            return Enumerable.Empty<IConstructibleObjectGetter>();
        }
    }

    public IEnumerable<IObjectModificationGetter> GetWinningObjectModificationsTyped()
    {
        try
        {
            // Generic WinningOverrides<T>() not available on this runtime; use robust per-mod winners and cast
            return GetObjectModificationWinningOverrides().OfType<IObjectModificationGetter>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetWinningObjectModificationsTyped failed");
            return Enumerable.Empty<IObjectModificationGetter>();
        }
    }

    public IEnumerable<(string Name, IEnumerable<IMajorRecordGetter> Items)> EnumerateRecordCollectionsTyped()
    {
        // Curated typed collections (materialize lists for logging counts)
        var curated = new List<(string Name, List<IMajorRecordGetter> Items)>();
        void AddSafe(string name, Func<IEnumerable<IMajorRecordGetter>> getter)
        {
            try
            {
                var data = getter()?.ToList() ?? new List<IMajorRecordGetter>();
                curated.Add((name, data));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "EnumerateRecordCollectionsTyped: failed to populate {Name}", name);
                curated.Add((name, new List<IMajorRecordGetter>()));
            }
        }

        AddSafe("Weapon", () => GetWinningWeaponOverridesTyped().Cast<IMajorRecordGetter>());
        AddSafe("ObjectModification", () => GetWinningObjectModificationsTyped().Cast<IMajorRecordGetter>());
        AddSafe("ConstructibleObject", () => GetWinningConstructibleObjectOverridesTyped().Cast<IMajorRecordGetter>());
        AddSafe("Armor", () => _env.LoadOrder.PriorityOrder.Armor().WinningOverrides().Cast<IMajorRecordGetter>());
        AddSafe("Ammo", () => _env.LoadOrder.PriorityOrder.Ammunition().WinningOverrides().Cast<IMajorRecordGetter>());

        foreach (var (name, items) in curated)
        {
            var count = items.Count;
            if (name == "ObjectModification" && count == 0)
            {
                _logger.LogWarning("EnumerateRecordCollectionsTyped: ObjectModification count=0 (OMOD winners missing or unsupported in this version)");
            }
            else
            {
                _logger.LogInformation("EnumerateRecordCollectionsTyped: {Name} count={Count}", name, count);
            }

            if (count > 0)
            {
                yield return (name, (IEnumerable<IMajorRecordGetter>)items);
            }
        }
    }

    private IEnumerable<object> GetObjectModificationWinningOverrides()
    {
        // Prefer PriorityOrder extension if present (ObjectModification/ObjectMod) → WinningOverrides()
        try
        {
            var priority = _env.LoadOrder.PriorityOrder;
            var pType = priority.GetType();
            var methodNames = new[] { "ObjectModification", "ObjectMod" };
            foreach (var name in methodNames)
            {
                var m = pType.GetMethod(name, BindingFlags.Public | BindingFlags.Instance, Type.DefaultBinder, Type.EmptyTypes, null);
                if (m == null) continue;
                object? coll = null;
                try { coll = m.Invoke(priority, null); } catch { coll = null; }
                if (coll == null) continue;
                var win = coll.GetType().GetMethod("WinningOverrides", BindingFlags.Public | BindingFlags.Instance);
                if (win == null) continue;
                try
                {
                    if (win.Invoke(coll, null) is System.Collections.IEnumerable seq)
                        return seq.Cast<object>();
                }
                catch { /* try fallback */ }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "GetObjectModificationWinningOverrides: extension path failed, will try per-mod fallback");
        }

        // Fallback: walk each mod's ObjectModifications and dedupe by (plugin,id) to get winners
        var winners = new Dictionary<(string Plugin, uint Id), object>();
        try
        {
            foreach (var listing in _env.LoadOrder.PriorityOrder)
            {
                object? mod = null;
                try { mod = listing?.Mod; } catch { mod = null; }
                if (mod == null) continue;

                var prop = mod.GetType().GetProperty("ObjectModifications", BindingFlags.Public | BindingFlags.Instance);
                if (prop == null) continue;
                if (prop.GetValue(mod) is not System.Collections.IEnumerable seq) continue;

                foreach (var rec in seq)
                {
                    if (rec is IMajorRecordGetter mr)
                    {
                        try
                        {
                            var fk = mr.FormKey;
                            var plugin = fk.ModKey.FileName.ToString().ToLowerInvariant();
                            var id = (uint)fk.ID;
                            if (!string.IsNullOrEmpty(plugin) && id != 0)
                            {
                                // Later entries in PriorityOrder overwrite earlier → winner ends up stored
                                winners[(plugin, id)] = rec;
                            }
                        }
                        catch { /* skip malformed */ }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "GetObjectModificationWinningOverrides: per-mod fallback failed");
        }

        return winners.Values;
    }

    public ILinkResolver? GetLinkCache()
    {
        try
        {
            var cache = _env.LinkCache;
            if (cache == null) return null;
            // Create logger with correct type
            var loggerFactory = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
            var linkResolverLogger = loggerFactory.CreateLogger<LinkResolver>();
            return new LinkResolver(cache, linkResolverLogger);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MutagenV51EnvironmentAdapter: failed to obtain LinkCache");
            return null;
        }
    }

    public Noggog.DirectoryPath? GetDataFolderPath()
    {
        try { return _env.DataFolderPath; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MutagenV51EnvironmentAdapter: failed to obtain DataFolderPath");
            return null;
        }
    }
}
