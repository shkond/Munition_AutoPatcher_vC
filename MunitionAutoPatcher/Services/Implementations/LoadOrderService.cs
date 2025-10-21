using MunitionAutoPatcher.Services.Interfaces;

namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// Stub implementation of the load order service
/// </summary>
public class LoadOrderService : ILoadOrderService
{
    private string _gameDataPath = @"C:\Games\Fallout4\Data";

    public Task<List<string>> GetLoadOrderAsync()
    {
        // Stub: Return some fake load order
        var loadOrder = new List<string>
        {
            "Fallout4.esm",
            "DLCRobot.esm",
            "DLCCoast.esm",
            "MyWeaponMod.esp"
        };
        return Task.FromResult(loadOrder);
    }

    public Task<bool> ValidateLoadOrderAsync()
    {
        // Stub: Always return true
        return Task.FromResult(true);
    }

    public string GetGameDataPath()
    {
        return _gameDataPath;
    }
}
