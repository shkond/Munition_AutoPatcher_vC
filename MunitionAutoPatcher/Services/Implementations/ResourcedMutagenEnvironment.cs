using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Noggog;
using MunitionAutoPatcher.Services.Interfaces;

namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// Lightweight wrapper that composes an <see cref="IMutagenEnvironment"/> and an
/// <see cref="IDisposable"/> resource. This allows factories to return a single
/// object that both exposes the adapter surface and disposes underlying resources
/// when disposed by callers.
/// </summary>
public sealed class ResourcedMutagenEnvironment : IResourcedMutagenEnvironment
{
    private readonly IMutagenEnvironment _env;
    private readonly IDisposable _resource;
    private readonly ILogger _logger;
    private bool _disposed;

    public ResourcedMutagenEnvironment(IMutagenEnvironment env, IDisposable resource, ILogger logger)
    {
        _env = env ?? throw new ArgumentNullException(nameof(env));
        _resource = resource ?? throw new ArgumentNullException(nameof(resource));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IEnumerable<object> GetWinningWeaponOverrides() => _env.GetWinningWeaponOverrides();

    public IEnumerable<object> GetWinningConstructibleObjectOverrides() => _env.GetWinningConstructibleObjectOverrides();

    public IEnumerable<(string Name, IEnumerable<object> Items)> EnumerateRecordCollections()
        => _env.EnumerateRecordCollections();

    public ILinkResolver? GetLinkCache() => _env.GetLinkCache();

    public DirectoryPath? GetDataFolderPath() => _env.GetDataFolderPath();

    public void Dispose()
    {
        if (_disposed) return;
        try
        {
            _resource.Dispose();
        }
        catch (Exception ex)
        {
            // Disposal should not throw to callers; log and swallow.
            _logger.LogError(ex, "ResourcedMutagenEnvironment: exception while disposing resource");
        }
        _disposed = true;
    }
}
