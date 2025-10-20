namespace MunitionAutoPatcher.Models;

/// <summary>
/// Represents weapon data extracted from plugins
/// </summary>
public class WeaponData
{
    public FormKey FormKey { get; set; } = new();
    public string EditorId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public FormKey? DefaultAmmo { get; set; }
    public float Damage { get; set; }
    public float FireRate { get; set; }
    public string WeaponType { get; set; } = string.Empty;
}
