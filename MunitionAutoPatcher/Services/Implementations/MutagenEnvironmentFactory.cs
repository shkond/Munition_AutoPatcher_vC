using Mutagen.Bethesda;
using System;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Environments;

namespace MunitionAutoPatcher.Services.Implementations;

public class MutagenEnvironmentFactory : IMutagenEnvironmentFactory
{
    private readonly Func<IGameEnvironment<IFallout4Mod, IFallout4ModGetter>> _environmentCreator;

    public MutagenEnvironmentFactory()
        : this(() => GameEnvironment.Typical.Fallout4(Fallout4Release.Fallout4))
    {
    }

    internal MutagenEnvironmentFactory(Func<IGameEnvironment<IFallout4Mod, IFallout4ModGetter>> environmentCreator)
    {
        _environmentCreator = environmentCreator;
    }

    public IResourcedMutagenEnvironment Create()
    {
        try
        {
            var env = _environmentCreator();
            var adapter = new MutagenV51EnvironmentAdapter(env);
            // Pass the actual GameEnvironment as the disposable resource so Dispose will clean it up.
            var disposableResource = env as IDisposable ?? adapter as IDisposable;
            return new ResourcedMutagenEnvironment(adapter, disposableResource!);
        }
        catch (Exception ex)
        {
            AppLogger.Log("MutagenEnvironmentFactory: failed to create GameEnvironment; returning NoOpMutagenEnvironment", ex);
            var noop = new NoOpMutagenEnvironment();
            return new ResourcedMutagenEnvironment(noop, noop);
        }
    }
}
