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
    // Exclusion flags for UI filtering
    bool GetExcludeFallout4Esm();
    void SetExcludeFallout4Esm(bool v);
    bool GetExcludeDlcEsms();
    void SetExcludeDlcEsms(bool v);
    bool GetExcludeCcEsl();
    void SetExcludeCcEsl(bool v);
    bool GetPreferEditorIdForDisplay();
    void SetPreferEditorIdForDisplay(bool v);
    // Excluded plugins list (configurable blacklist)
    System.Collections.Generic.IEnumerable<string> GetExcludedPlugins();
    void SetExcludedPlugins(System.Collections.Generic.IEnumerable<string> plugins);
}
