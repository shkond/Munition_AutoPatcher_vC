using Mutagen.Bethesda;
using System;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Environments;
using Microsoft.Extensions.Logging;

namespace MunitionAutoPatcher.Services.Implementations;

public class MutagenEnvironmentFactory : IMutagenEnvironmentFactory
{
    private readonly Func<IGameEnvironment<IFallout4Mod, IFallout4ModGetter>> _environmentCreator;
    private readonly ILogger<MutagenEnvironmentFactory> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public MutagenEnvironmentFactory(ILogger<MutagenEnvironmentFactory> logger, ILoggerFactory loggerFactory)
        : this(() => GameEnvironment.Typical.Fallout4(Fallout4Release.Fallout4), logger, loggerFactory)
    {
    }

    internal MutagenEnvironmentFactory(Func<IGameEnvironment<IFallout4Mod, IFallout4ModGetter>> environmentCreator, ILogger<MutagenEnvironmentFactory> logger, ILoggerFactory loggerFactory)
    {
        _environmentCreator = environmentCreator;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    public IResourcedMutagenEnvironment Create()
    {
        try
        {
            var env = _environmentCreator();
            var adapterLogger = _loggerFactory.CreateLogger<MutagenV51EnvironmentAdapter>();
            var adapter = new MutagenV51EnvironmentAdapter(env, adapterLogger);
            // Pass the actual GameEnvironment as the disposable resource so Dispose will clean it up.
            var disposableResource = env as IDisposable ?? adapter as IDisposable;
            var resourcedLogger = _loggerFactory.CreateLogger<ResourcedMutagenEnvironment>();
            return new ResourcedMutagenEnvironment(adapter, disposableResource!, resourcedLogger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MutagenEnvironmentFactory: failed to create GameEnvironment; returning NoOpMutagenEnvironment");
            var noop = new NoOpMutagenEnvironment();
            var resourcedLogger = _loggerFactory.CreateLogger<ResourcedMutagenEnvironment>();
            return new ResourcedMutagenEnvironment(noop, noop, resourcedLogger);
        }
    }
}
