namespace MunitionAutoPatcher.Models;

/// <summary>
/// Represents an ammunition category
/// </summary>
public class AmmoCategory
{
    public string CategoryName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<AmmoData> Ammo { get; set; } = new();
}
