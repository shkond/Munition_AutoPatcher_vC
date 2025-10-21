using MunitionAutoPatcher.Models;

namespace MunitionAutoPatcher.Services.Interfaces;

/// <summary>
/// Main orchestrator service for coordinating the patching workflow
/// </summary>
public interface IOrchestrator
{
    Task<bool> InitializeAsync();
    Task<bool> ExtractWeaponsAsync(IProgress<string>? progress = null);
    Task<bool> GenerateMappingsAsync(IProgress<string>? progress = null);
    Task<bool> GenerateIniAsync(string outputPath, IProgress<string>? progress = null);
    bool IsInitialized { get; }
}
