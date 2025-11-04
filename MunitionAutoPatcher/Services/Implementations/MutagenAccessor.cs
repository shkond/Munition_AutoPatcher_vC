using MunitionAutoPatcher.Services.Interfaces;
using MunitionAutoPatcher.Utilities;

namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// Abstraction layer for Mutagen API access, isolating version-specific reflection calls.
/// </summary>
public class MutagenAccessor : IMutagenAccessor
{
    /// <inheritdoc/>
    public ILinkResolver? GetLinkCache(IResourcedMutagenEnvironment env)
    {
        try
        {
            return env.GetLinkCache();
        }
        catch (Exception ex)
        {
            AppLogger.Log("MutagenAccessor: failed to obtain LinkCache", ex);
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
            AppLogger.Log($"MutagenAccessor: failed to enumerate {collectionName} collection", ex);
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
            AppLogger.Log("MutagenAccessor: GetWinningWeaponOverrides failed, trying EnumerateRecordCollections fallback", ex);
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
            AppLogger.Log("MutagenAccessor: GetWinningConstructibleObjectOverrides failed", ex);
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
            AppLogger.Log("MutagenAccessor: failed to read EditorID", ex);
            return string.Empty;
        }
    }
}
