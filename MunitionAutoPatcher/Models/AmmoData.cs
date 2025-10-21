namespace MunitionAutoPatcher.Models;

/// <summary>
/// Represents ammunition data
/// </summary>
public class AmmoData
{
    public FormKey FormKey { get; set; } = new();
    public string EditorId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public float Damage { get; set; }
    public string AmmoType { get; set; } = string.Empty;
}
