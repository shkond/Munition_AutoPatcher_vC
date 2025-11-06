using MunitionAutoPatcher.Services.Interfaces;
using MunitionAutoPatcher.Utilities;
using Microsoft.Extensions.Logging;

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
    public IEnumerable<object> EnumerateRecordCollections(IResourcedMutagenEnvironment env, string collectionName)
    {
        try
        {
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
            var wins = env.GetWinningWeaponOverrides();
            if (wins != null)
            {
                return wins.Cast<object>();
            }
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
}
