namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// A disposable environment returned by the factory. Combines the IMutagenEnvironment
/// surface with IDisposable so callers can use `using` to ensure resources are released.
/// </summary>
public interface IResourcedMutagenEnvironment : IMutagenEnvironment, System.IDisposable
{
}
