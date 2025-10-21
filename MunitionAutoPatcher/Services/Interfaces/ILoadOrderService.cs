namespace MunitionAutoPatcher.Services.Interfaces;

/// <summary>
/// Service for managing plugin load order
/// </summary>
public interface ILoadOrderService
{
    Task<List<string>> GetLoadOrderAsync();
    Task<bool> ValidateLoadOrderAsync();
    string GetGameDataPath();
}
