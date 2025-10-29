namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// Factory responsible for creating IMutagenEnvironment instances. Creation may fail
/// (e.g., when not running under MO2); callers should handle failure or rely on a
/// no-op implementation where appropriate.
/// </summary>
public interface IMutagenEnvironmentFactory
{
    IMutagenEnvironment Create();
}
