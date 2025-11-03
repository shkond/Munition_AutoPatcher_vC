using MunitionAutoPatcher.Services.Interfaces;
using MunitionAutoPatcher.Utilities;

namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// Service for resolving repository and artifact paths.
/// </summary>
public class PathService : IPathService
{
    /// <inheritdoc/>
    public string GetRepoRoot()
    {
        return RepoUtils.FindRepoRoot();
    }

    /// <inheritdoc/>
    public string GetArtifactsDirectory()
    {
        var repoRoot = GetRepoRoot();
        var artifactsDir = System.IO.Path.Combine(repoRoot, "artifacts", "RobCo_Patcher");
        
        if (!System.IO.Directory.Exists(artifactsDir))
        {
            System.IO.Directory.CreateDirectory(artifactsDir);
        }
        
        return artifactsDir;
    }
}
