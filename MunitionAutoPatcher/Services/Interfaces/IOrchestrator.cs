using MunitionAutoPatcher.Models;

namespace MunitionAutoPatcher.Services.Interfaces;

/// <summary>
/// Main orchestrator service for coordinating the patching workflow
/// </summary>
public interface IOrchestrator
{
    Task<bool> InitializeAsync();
    Task<List<WeaponData>> ExtractWeaponsAsync(IProgress<string>? progress = null);
    Task<bool> GenerateMappingsAsync(List<WeaponData> weapons, IProgress<string>? progress = null);
    Task<bool> GenerateIniAsync(string outputPath, List<WeaponMapping> mappings, IProgress<string>? progress = null);
    Task<bool> GeneratePatchAsync(string outputPath, List<WeaponData> weapons, IProgress<string>? progress = null);
    bool IsInitialized { get; }
}
