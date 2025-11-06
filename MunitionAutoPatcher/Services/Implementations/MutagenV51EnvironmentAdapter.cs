using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Environments;
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
                _logger.LogWarning(ex, "MutagenV51EnvironmentAdapter: failed to invoke {Method}", m.Name);
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
                _logger.LogWarning(ex, "MutagenV51EnvironmentAdapter: failed to obtain items from {Method}", m.Name);
                items = null;
            }

            if (items != null)
                yield return (m.Name, items);
        }
    }

    public ILinkResolver? GetLinkCache()
    {
        try
        {
            var real = _env.GetType().GetProperty("LinkCache")?.GetValue(_env);
            if (real == null) return null;
            return new LinkResolver(real);
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
