using MunitionAutoPatcher.Services.Interfaces;
using MunitionAutoPatcher.Utilities;

namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// Service for resolving repository and artifact paths.
/// </summary>
public class PathService : IPathService
{
    private readonly IConfigService? _configService;

    public PathService() : this(null)
    {
    }

    public PathService(IConfigService? configService)
    {
        _configService = configService;
    }

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

    /// <summary>
    /// Gets the configured output directory for patches and INI files.
    /// Creates the directory if it doesn't exist.
    /// </summary>
    public string GetOutputDirectory()
    {
        var repoRoot = GetRepoRoot();
        var outputDir = _configService?.GetOutputDirectory() ?? "artifacts";
        
        // Resolve relative paths against repository root
        var resolvedPath = System.IO.Path.IsPathRooted(outputDir)
            ? outputDir
            : System.IO.Path.Combine(repoRoot, outputDir);
        
        if (!System.IO.Directory.Exists(resolvedPath))
        {
            System.IO.Directory.CreateDirectory(resolvedPath);
        }
        
        return resolvedPath;
    }
}
