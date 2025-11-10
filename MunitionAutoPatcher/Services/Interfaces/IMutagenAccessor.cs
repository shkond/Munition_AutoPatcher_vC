using MunitionAutoPatcher.Services.Implementations;
using Mutagen.Bethesda.Plugins.Cache;

namespace MunitionAutoPatcher.Services.Interfaces;

/// <summary>
/// Abstraction layer for Mutagen API access, isolating version-specific reflection calls.
/// </summary>
public interface IMutagenAccessor
{
    /// <summary>
    /// Gets an `ILinkResolver` (wrapper over LinkCache) from the environment if available.
    /// </summary>
    ILinkResolver? GetLinkCache(IResourcedMutagenEnvironment env);

    /// <summary>
    /// Builds a concrete Mutagen <see cref="ILinkCache"/> for the supplied environment when possible.
    /// Returns <c>null</c> if the cache cannot be materialized.
    /// </summary>
    ILinkCache? BuildConcreteLinkCache(IResourcedMutagenEnvironment env);

    /// <summary>
    /// Enumerates record collections by name (e.g., "Weapon", "ObjectMod").
    /// </summary>
    IEnumerable<object> EnumerateRecordCollections(IResourcedMutagenEnvironment env, string collectionName);

    /// <summary>
    /// Gets winning weapon overrides from the environment.
    /// </summary>
    IEnumerable<object> GetWinningWeaponOverrides(IResourcedMutagenEnvironment env);

    /// <summary>
    /// Gets winning ConstructibleObject overrides from the environment.
    /// </summary>
    IEnumerable<object> GetWinningConstructibleObjectOverrides(IResourcedMutagenEnvironment env);

    /// <summary>
    /// Tries to extract plugin name and FormID from a record object using reflection.
    /// </summary>
    bool TryGetPluginAndIdFromRecord(object record, out string pluginName, out uint formId);

    /// <summary>
    /// Tries to get the EditorID from a record object using reflection.
    /// </summary>
    string GetEditorId(object? record);
}
