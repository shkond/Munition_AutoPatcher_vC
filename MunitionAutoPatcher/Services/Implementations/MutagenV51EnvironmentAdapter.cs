using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Environments;

namespace MunitionAutoPatcher.Services.Implementations;

public class MutagenV51EnvironmentAdapter : IMutagenEnvironment
{
    private readonly IGameEnvironment<IFallout4Mod, IFallout4ModGetter> _env;

    public MutagenV51EnvironmentAdapter(IGameEnvironment<IFallout4Mod, IFallout4ModGetter> env)
    {
        _env = env;
    }

    public IEnumerable<object> GetWinningWeaponOverrides()
    {
        try { return _env.LoadOrder.PriorityOrder.Weapon().WinningOverrides().Cast<object>(); }
        catch { return Enumerable.Empty<object>(); }
    }

    public IEnumerable<object> GetWinningConstructibleObjectOverrides()
    {
        try { return _env.LoadOrder.PriorityOrder.ConstructibleObject().WinningOverrides().Cast<object>(); }
        catch { return Enumerable.Empty<object>(); }
    }

    public IEnumerable<(string Name, IEnumerable<object> Items)> EnumerateRecordCollections()
    {
        var priority = _env.LoadOrder.PriorityOrder;
        var methods = priority.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetParameters().Length == 0 && typeof(System.Collections.IEnumerable).IsAssignableFrom(m.ReturnType));

        foreach (var m in methods)
        {
            object? collection = null;
            try
            {
                collection = m.Invoke(priority, null);
            }
            catch (Exception ex)
            {
                AppLogger.Log($"MutagenV51EnvironmentAdapter: failed to invoke {m.Name}", ex);
                continue;
            }

            if (collection == null) continue;

            // Prefer WinningOverrides() where present
            var winMethod = collection.GetType().GetMethod("WinningOverrides");
            IEnumerable<object>? items = null;
            try
            {
                if (winMethod != null)
                    items = (winMethod.Invoke(collection, null) as System.Collections.IEnumerable)?.Cast<object>();
                else if (collection is System.Collections.IEnumerable en)
                    items = en.Cast<object>();
            }
            catch (Exception ex)
            {
                AppLogger.Log($"MutagenV51EnvironmentAdapter: failed to obtain items from {m.Name}", ex);
                items = null;
            }

            if (items != null)
                yield return (m.Name, items);
        }
    }

    public object? GetLinkCache()
    {
        try { return _env.GetType().GetProperty("LinkCache")?.GetValue(_env); }
        catch (Exception ex) { AppLogger.Log("MutagenV51EnvironmentAdapter: failed to obtain LinkCache", ex); return null; }
    }
}
