using System.Diagnostics.CodeAnalysis;
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
    public IEnumerable<Mutagen.Bethesda.Fallout4.IConstructibleObjectGetter> GetWinningConstructibleObjectOverridesTyped(IResourcedMutagenEnvironment env)
    {
        try
        {
            return env.GetWinningConstructibleObjectOverridesTyped();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MutagenAccessor: GetWinningConstructibleObjectOverridesTyped failed");
            return Enumerable.Empty<Mutagen.Bethesda.Fallout4.IConstructibleObjectGetter>();
        }
    }

    /// <inheritdoc/>
    public IEnumerable<Mutagen.Bethesda.Fallout4.IWeaponGetter> GetWinningWeaponOverridesTyped(IResourcedMutagenEnvironment env)
    {
        try
        {
            return env.GetWinningWeaponOverridesTyped();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MutagenAccessor: GetWinningWeaponOverridesTyped failed");
            return Enumerable.Empty<Mutagen.Bethesda.Fallout4.IWeaponGetter>();
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
    public bool TryResolveRecord<T>(
        IResourcedMutagenEnvironment env,
        Models.FormKey formKey,
        [NotNullWhen(true)] out T? record) 
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

    #region 型安全な Weapon プロパティアクセサ

    /// <inheritdoc/>
    public string? GetWeaponName(Mutagen.Bethesda.Fallout4.IWeaponGetter weapon)
    {
        if (weapon == null) return null;
        try
        {
            var nameObj = weapon.Name;
            if (nameObj == null) return null;
            
            // ITranslatedStringGetter の場合は String プロパティを使用
            if (nameObj is Mutagen.Bethesda.Strings.ITranslatedStringGetter translated)
            {
                return translated.String;
            }
            return nameObj.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MutagenAccessor.GetWeaponName: failed to read Name");
            return null;
        }
    }

    /// <inheritdoc/>
    public string? GetWeaponDescription(Mutagen.Bethesda.Fallout4.IWeaponGetter weapon)
    {
        if (weapon == null) return null;
        try
        {
            var descObj = weapon.Description;
            if (descObj == null) return null;
            
            if (descObj is Mutagen.Bethesda.Strings.ITranslatedStringGetter translated)
            {
                return translated.String;
            }
            return descObj.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MutagenAccessor.GetWeaponDescription: failed to read Description");
            return null;
        }
    }

    /// <inheritdoc/>
    public float GetWeaponBaseDamage(Mutagen.Bethesda.Fallout4.IWeaponGetter weapon)
    {
        if (weapon == null) return 0f;
        try
        {
            // Fallout 4 の IWeaponGetter は BaseDamage を直接プロパティとして持つ
            return weapon.BaseDamage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MutagenAccessor.GetWeaponBaseDamage: failed to read BaseDamage");
            return 0f;
        }
    }

    /// <inheritdoc/>
    public float GetWeaponFireRate(Mutagen.Bethesda.Fallout4.IWeaponGetter weapon)
    {
        if (weapon == null) return 0f;
        try
        {
            // AnimationAttackSeconds から RPM を計算 (直接プロパティとして存在)
            var attackSeconds = weapon.AnimationAttackSeconds;
            if (attackSeconds > 0)
            {
                return 60f / attackSeconds;
            }
            return 0f;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MutagenAccessor.GetWeaponFireRate: failed to read FireRate");
            return 0f;
        }
    }

    /// <inheritdoc/>
    public object? GetWeaponAmmoLink(Mutagen.Bethesda.Fallout4.IWeaponGetter weapon)
    {
        if (weapon == null) return null;
        try
        {
            return weapon.Ammo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MutagenAccessor.GetWeaponAmmoLink: failed to read Ammo");
            return null;
        }
    }

    #endregion

    #region FormKey / プロパティアクセサ

    /// <inheritdoc/>
    public bool TryGetFormKey(object? record, out Mutagen.Bethesda.Plugins.FormKey? formKey)
    {
        formKey = null;
        if (record == null) return false;

        try
        {
            // Fast path: typed record
            if (record is Mutagen.Bethesda.Plugins.Records.IMajorRecordGetter mrFast)
            {
                formKey = mrFast.FormKey;
                return true;
            }

            // Fast path: FormKey itself
            if (record is Mutagen.Bethesda.Plugins.FormKey fkDirect)
            {
                formKey = fkDirect;
                return true;
            }

            // Fallback: reflection (delegated to internal helper)
            if (MutagenReflectionHelpers.TryGetFormKey(record, out var fkObj) && fkObj is Mutagen.Bethesda.Plugins.FormKey fk)
            {
                formKey = fk;
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MutagenAccessor.TryGetFormKey: failed to extract FormKey");
            return false;
        }
    }

    /// <inheritdoc/>
    public bool TryGetPropertyValue<T>(object? obj, string propertyName, out T? value)
    {
        value = default;
        if (obj == null || string.IsNullOrEmpty(propertyName)) return false;

        try
        {
            return MutagenReflectionHelpers.TryGetPropertyValue(obj, propertyName, out value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MutagenAccessor.TryGetPropertyValue: failed for property {PropertyName}", propertyName);
            return false;
        }
    }

    #endregion
}
