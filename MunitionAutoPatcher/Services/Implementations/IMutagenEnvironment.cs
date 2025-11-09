using System.Collections.Generic;
using Mutagen.Bethesda.Installs;
using MunitionAutoPatcher.Services.Interfaces;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins.Records;

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
    /// If available, return a link resolver (wrapper over LinkCache) else null.
    /// </summary>
    ILinkResolver? GetLinkCache();

    /// <summary>
    /// If available, return the environment's data folder path (DirectoryPath) used by Mutagen.
    /// Returns null when not available.
    /// </summary>
    Noggog.DirectoryPath? GetDataFolderPath();

    // Typed accessors (additive, non-breaking)
    IEnumerable<IWeaponGetter> GetWinningWeaponOverridesTyped();
    IEnumerable<IConstructibleObjectGetter> GetWinningConstructibleObjectOverridesTyped();
    IEnumerable<IObjectModificationGetter> GetWinningObjectModificationsTyped();
    IEnumerable<(string Name, IEnumerable<IMajorRecordGetter> Items)> EnumerateRecordCollectionsTyped();
}
