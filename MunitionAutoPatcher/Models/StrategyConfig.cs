namespace MunitionAutoPatcher.Models;

/// <summary>
/// Configuration for mapping strategies
/// </summary>
public class StrategyConfig
{
    public string StrategyName { get; set; } = string.Empty;
    public bool AutoMapByName { get; set; } = true;
    public bool AutoMapByType { get; set; } = true;
    public bool AllowManualOverride { get; set; } = true;
    public Dictionary<string, string> CustomRules { get; set; } = new();
}
