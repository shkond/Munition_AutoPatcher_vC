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

    public IMutagenEnvironment Create()
    {
        try
        {
            var env = _environmentCreator();
            return new MutagenV51EnvironmentAdapter(env);
        }
        catch (Exception ex)
        {
            AppLogger.Log("MutagenEnvironmentFactory: failed to create GameEnvironment; returning NoOpMutagenEnvironment", ex);
            return new NoOpMutagenEnvironment();
        }
    }
}
