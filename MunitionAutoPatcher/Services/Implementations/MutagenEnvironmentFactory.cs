using System;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Environments;

namespace MunitionAutoPatcher.Services.Implementations;

public class MutagenEnvironmentFactory : IMutagenEnvironmentFactory
{
    public IMutagenEnvironment Create()
    {
        try
        {
            var env = GameEnvironment.Typical.Fallout4(Fallout4Release.Fallout4);
            return new MutagenV51EnvironmentAdapter(env);
        }
        catch (Exception ex)
        {
            AppLogger.Log("MutagenEnvironmentFactory: failed to create GameEnvironment; returning NoOpMutagenEnvironment", ex);
            return new NoOpMutagenEnvironment();
        }
    }
}
