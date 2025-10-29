using System.Collections.Generic;

namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// Minimal adapter interface that exposes the small subset of Mutagen functionality
/// that the extractor and helpers need. Implementations adapt a concrete Mutagen
/// IGameEnvironment (version-specific) into a stable, testable surface.
/// </summary>
public interface IMutagenEnvironment
{
    IEnumerable<object> GetWinningWeaponOverrides();
    IEnumerable<object> GetWinningConstructibleObjectOverrides();

    /// <summary>
    /// Enumerate named record-collection accessors on the priority order. Each tuple
    /// contains the method name and an IEnumerable of the collection items (usually
    /// the WinningOverrides() result or the raw enumerable).
    /// </summary>
    IEnumerable<(string Name, IEnumerable<object> Items)> EnumerateRecordCollections();

    /// <summary>
    /// If available, return the LinkCache instance (unknown/dynamic type) else null.
    /// </summary>
    object? GetLinkCache();
}
