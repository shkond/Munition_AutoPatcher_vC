using System.Reflection;
using Microsoft.Extensions.Logging;
using MunitionAutoPatcher.Services.Interfaces;
using MunitionAutoPatcher.Utilities;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Order;

namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// Abstraction layer for Mutagen API access, isolating version-specific reflection calls.
/// </summary>
public class MutagenAccessor : IMutagenAccessor
{
    private readonly ILogger<MutagenAccessor> _logger;

    public MutagenAccessor(ILogger<MutagenAccessor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    /// <inheritdoc/>
    public ILinkResolver? GetLinkCache(IResourcedMutagenEnvironment env)
    {
        try
        {
            return env.GetLinkCache();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MutagenAccessor: failed to obtain LinkCache");
            return null;
        }
    }

    /// <inheritdoc/>
    public ILinkCache? BuildConcreteLinkCache(IResourcedMutagenEnvironment env)
    {
        if (env == null) return null;

        try
        {
            var resolver = env.GetLinkCache();
            if (resolver is LinkResolver typedResolver && typedResolver.LinkCache != null)
            {
                return typedResolver.LinkCache;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "MutagenAccessor: resolver-backed LinkCache capture failed");
        }

        try
        {
            if (env is ResourcedMutagenEnvironment resourced)
            {
                var inner = resourced.InnerEnvironment;
                if (inner is MutagenV51EnvironmentAdapter adapter)
                {
                    var gameEnv = adapter.InnerGameEnvironment;
                    if (gameEnv?.LinkCache != null)
                    {
                        _logger.LogInformation("MutagenAccessor: reusing GameEnvironment LinkCache (type={Type})", gameEnv.LinkCache.GetType().FullName);
                        return gameEnv.LinkCache;
                    }

                    var loadOrder = gameEnv?.LoadOrder;
                    if (loadOrder != null)
                    {
                        try
                        {
                            var cache = TryInvokeLinkCacheBuilder(loadOrder, "ToImmutableLinkCache");
                            if (cache != null)
                            {
                                _logger.LogInformation("MutagenAccessor: built LinkCache via GameEnvironment load order (type={Type})", cache.GetType().FullName);
                                return cache;
                            }
                        }
                        catch (MissingMethodException)
                        {
                            try
                            {
                                var cache = TryInvokeLinkCacheBuilder(loadOrder, "ToLinkCache");
                                if (cache != null)
                                {
                                    _logger.LogInformation("MutagenAccessor: built LinkCache via GameEnvironment ToLinkCache (type={Type})", cache.GetType().FullName);
                                    return cache;
                                }
                            }
                            catch (Exception secondary)
                            {
                                _logger.LogDebug(secondary, "MutagenAccessor: LoadOrder.ToLinkCache fallback failed");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "MutagenAccessor: LoadOrder.ToImmutableLinkCache failed");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MutagenAccessor: failed to materialize concrete LinkCache from environment");
        }

        return null;
    }

    private static ILinkCache? TryInvokeLinkCacheBuilder(object loadOrder, string methodName)
    {
        if (loadOrder == null) return null;

        try
        {
            var method = loadOrder.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance, Type.DefaultBinder, Type.EmptyTypes, null);
            if (method == null) return null;
            return method.Invoke(loadOrder, null) as ILinkCache;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public IEnumerable<object> EnumerateRecordCollections(IResourcedMutagenEnvironment env, string collectionName)
    {
        try
        {
            // Prefer typed collections first
            try
            {
                var typed = env.EnumerateRecordCollectionsTyped();
                var targetTyped = typed.FirstOrDefault(t => string.Equals(t.Name, collectionName, StringComparison.OrdinalIgnoreCase));
                if (!targetTyped.Equals(default((string, IEnumerable<Mutagen.Bethesda.Plugins.Records.IMajorRecordGetter>))) && targetTyped.Items != null)
                {
                    return targetTyped.Items.Cast<object>();
                }
            }
            catch { /* fall back to untyped */ }

            var collections = env.EnumerateRecordCollections();
            var targetCollection = collections.FirstOrDefault(t =>
                string.Equals(t.Name, collectionName, StringComparison.OrdinalIgnoreCase));

            if (!targetCollection.Equals(default((string, IEnumerable<object>))) && targetCollection.Items != null)
            {
                return targetCollection.Items;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MutagenAccessor: failed to enumerate {CollectionName} collection", collectionName);
        }

        return Enumerable.Empty<object>();
    }

    /// <inheritdoc/>
    public IEnumerable<object> GetWinningWeaponOverrides(IResourcedMutagenEnvironment env)
    {
        try
        {
            // Prefer typed
            try
            {
                var typed = env.GetWinningWeaponOverridesTyped();
                if (typed != null) return typed.Cast<object>();
            }
            catch { /* fallback below */ }

            var wins = env.GetWinningWeaponOverrides();
            if (wins != null) return wins.Cast<object>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MutagenAccessor: GetWinningWeaponOverrides failed, trying EnumerateRecordCollections fallback");
        }

        // Fallback to enumerating Weapon collection
        return EnumerateRecordCollections(env, "Weapon");
    }

    /// <inheritdoc/>
    public IEnumerable<object> GetWinningConstructibleObjectOverrides(IResourcedMutagenEnvironment env)
    {
        try
        {
            return env.GetWinningConstructibleObjectOverrides();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MutagenAccessor: GetWinningConstructibleObjectOverrides failed");
            return Enumerable.Empty<object>();
        }
    }

    /// <inheritdoc/>
    public bool TryGetPluginAndIdFromRecord(object record, out string pluginName, out uint formId)
    {
        return MutagenReflectionHelpers.TryGetPluginAndIdFromRecord(record, out pluginName, out formId);
    }

    /// <inheritdoc/>
    public string GetEditorId(object? record)
    {
        if (record == null) return string.Empty;

        try
        {
            return record.GetType().GetProperty("EditorID")?.GetValue(record)?.ToString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MutagenAccessor: failed to read EditorID");
            return string.Empty;
        }
    }

    /// <inheritdoc/>
    public bool TryResolveRecord<T>(IResourcedMutagenEnvironment env, Models.FormKey formKey, out T? record) 
        where T : class, Mutagen.Bethesda.Plugins.Records.IMajorRecordGetter
    {
        record = null;
        
        try
        {
            var mfk = FormKeyNormalizer.ToMutagenFormKey(formKey);
            if (!mfk.HasValue)
            {
                _logger.LogDebug("MutagenAccessor.TryResolveRecord<{Type}>: Invalid FormKey {Plugin}:{Id:X8}", 
                    typeof(T).Name, formKey.PluginName, formKey.FormId);
                return false;
            }

            var linkCache = BuildConcreteLinkCache(env);
            if (linkCache == null)
            {
                _logger.LogWarning("MutagenAccessor.TryResolveRecord<{Type}>: LinkCache unavailable for {Plugin}:{Id:X8}", 
                    typeof(T).Name, formKey.PluginName, formKey.FormId);
                return false;
            }

            if (linkCache.TryResolve<T>(mfk.Value, out record))
            {
                _logger.LogDebug("MutagenAccessor.TryResolveRecord<{Type}>: Resolved {Plugin}:{Id:X8}", 
                    typeof(T).Name, formKey.PluginName, formKey.FormId);
                return true;
            }

            _logger.LogDebug("MutagenAccessor.TryResolveRecord<{Type}>: Resolution failed for {Plugin}:{Id:X8}", 
                typeof(T).Name, formKey.PluginName, formKey.FormId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MutagenAccessor.TryResolveRecord<{Type}>: Exception resolving {Plugin}:{Id:X8}", 
                typeof(T).Name, formKey?.PluginName ?? "NULL", formKey?.FormId ?? 0);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<(bool Success, T? Record)> TryResolveRecordAsync<T>(
        IResourcedMutagenEnvironment env, 
        Models.FormKey formKey, 
        CancellationToken ct) 
        where T : class, Mutagen.Bethesda.Plugins.Records.IMajorRecordGetter
    {
        // 現時点では同期実装を呼び出し（将来的な拡張ポイント）
        await Task.CompletedTask;
        ct.ThrowIfCancellationRequested();
        
        var success = TryResolveRecord<T>(env, formKey, out var record);
        return (success, record);
    }
}
