namespace MunitionAutoPatcher.Services.Interfaces;

/// <summary>
/// Service for resolving repository and artifact paths.
/// </summary>
public interface IPathService
{
    /// <summary>
    /// Gets the repository root directory path.
    /// </summary>
    string GetRepoRoot();

    /// <summary>
    /// Gets the artifacts output directory path.
    /// </summary>
    string GetArtifactsDirectory();

    /// <summary>
    /// Gets the output directory path for generated patches/configs.
    /// </summary>
    string GetOutputDirectory();
}
