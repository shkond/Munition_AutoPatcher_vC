namespace MunitionAutoPatcher.Models;

/// <summary>
/// Represents a mapping between a weapon and ammunition
/// </summary>
public class WeaponMapping
{
    public FormKey WeaponFormKey { get; set; } = new();
    public string WeaponName { get; set; } = string.Empty;
    public FormKey AmmoFormKey { get; set; } = new();
    public string AmmoName { get; set; } = string.Empty;
    public string Strategy { get; set; } = string.Empty;
    public bool IsManualMapping { get; set; }
}
