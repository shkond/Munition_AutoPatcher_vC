using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins.Order;

namespace MunitionAutoPatcher.Services.Interfaces;

/// <summary>
/// Service for managing plugin load order
/// </summary>
public interface ILoadOrderService
{
    Task<ILoadOrder<IModListing<IFallout4ModGetter>>?> GetLoadOrderAsync();
    Task<bool> ValidateLoadOrderAsync();
    string GetGameDataPath();
}
