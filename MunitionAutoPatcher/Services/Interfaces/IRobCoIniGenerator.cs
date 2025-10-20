using MunitionAutoPatcher.Models;

namespace MunitionAutoPatcher.Services.Interfaces;

/// <summary>
/// Service for generating RobCo ini files
/// </summary>
public interface IRobCoIniGenerator
{
    Task<bool> GenerateIniAsync(string outputPath, List<WeaponMapping> mappings, IProgress<string>? progress = null);
    string PreviewIni(List<WeaponMapping> mappings);
}
