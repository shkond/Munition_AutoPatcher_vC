using System.Collections.Generic;
using MunitionAutoPatcher.Services.Interfaces;

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

    public ILinkResolver? GetLinkCache() => null;

    public Noggog.DirectoryPath? GetDataFolderPath() => null;

    public void Dispose()
    {
        // no-op
    }
}
