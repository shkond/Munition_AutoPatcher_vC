using MunitionAutoPatcher.Models;

namespace MunitionAutoPatcher.Services.Interfaces;

/// <summary>
/// Service for managing application configuration
/// </summary>
public interface IConfigService
{
    Task<StrategyConfig> LoadConfigAsync();
    Task SaveConfigAsync(StrategyConfig config);
    string GetGameDataPath();
    void SetGameDataPath(string path);
    string GetOutputPath();
    void SetOutputPath(string path);
}
