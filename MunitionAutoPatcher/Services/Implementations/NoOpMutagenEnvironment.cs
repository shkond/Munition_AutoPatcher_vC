using System.Collections.Generic;
using MunitionAutoPatcher.Services.Interfaces;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins.Records;

namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// A safe no-op implementation of IMutagenEnvironment used when a real Mutagen
/// environment cannot be created. Returns empty enumerables and null LinkCache.
/// </summary>
public class NoOpMutagenEnvironment : IMutagenEnvironment, IDisposable
{
    public IEnumerable<object> GetWinningWeaponOverrides() => System.Linq.Enumerable.Empty<object>();

    public IEnumerable<object> GetWinningConstructibleObjectOverrides() => System.Linq.Enumerable.Empty<object>();

    public IEnumerable<(string Name, IEnumerable<object> Items)> EnumerateRecordCollections()
    {
        yield break;
    }

    // Typed additions (empty implementations)
    public IEnumerable<IWeaponGetter> GetWinningWeaponOverridesTyped() => System.Linq.Enumerable.Empty<IWeaponGetter>();
    public IEnumerable<IConstructibleObjectGetter> GetWinningConstructibleObjectOverridesTyped() => System.Linq.Enumerable.Empty<IConstructibleObjectGetter>();
    public IEnumerable<IObjectModificationGetter> GetWinningObjectModificationsTyped() => System.Linq.Enumerable.Empty<IObjectModificationGetter>();
    public IEnumerable<(string Name, IEnumerable<IMajorRecordGetter> Items)> EnumerateRecordCollectionsTyped()
    {
        yield break;
    }

    public ILinkResolver? GetLinkCache() => null;

    public Noggog.DirectoryPath? GetDataFolderPath() => null;

    public void Dispose()
    {
        // no-op
    }
}
