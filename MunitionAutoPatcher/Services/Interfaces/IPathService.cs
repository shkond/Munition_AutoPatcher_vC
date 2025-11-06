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
    /// Gets the configured output directory for patches and INI files.
    /// Creates the directory if it doesn't exist.
    /// </summary>
    string GetOutputDirectory();
}
