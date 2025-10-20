using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Interfaces;
using System.Text.Json;

namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// Stub implementation of the config service
/// </summary>
public class ConfigService : IConfigService
{
    private string _gameDataPath = @"C:\Games\Fallout4\Data";
    private string _outputPath = @"C:\Games\Fallout4\Data\RobCoPatcher.ini";
    private StrategyConfig _currentConfig = new();

    public Task<StrategyConfig> LoadConfigAsync()
    {
        // Stub: Return default config
        _currentConfig = new StrategyConfig
        {
            StrategyName = "Default",
            AutoMapByName = true,
            AutoMapByType = true,
            AllowManualOverride = true
        };
        return Task.FromResult(_currentConfig);
    }

    public Task SaveConfigAsync(StrategyConfig config)
    {
        // Stub: Just store in memory
        _currentConfig = config;
        return Task.CompletedTask;
    }

    public string GetGameDataPath() => _gameDataPath;

    public void SetGameDataPath(string path)
    {
        _gameDataPath = path;
    }

    public string GetOutputPath() => _outputPath;

    public void SetOutputPath(string path)
    {
        _outputPath = path;
    }
}
