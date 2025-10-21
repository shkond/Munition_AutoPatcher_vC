using MunitionAutoPatcher.Services.Interfaces;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Installs;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Order;
using Noggog;

namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// Implementation of the load order service using Mutagen
/// </summary>
public class LoadOrderService : ILoadOrderService
{
    private readonly IConfigService _configService;
    private ILoadOrder<IModListing<IFallout4ModGetter>>? _loadOrder;

    public LoadOrderService(IConfigService configService)
    {
        _configService = configService;
    }

    public async Task<ILoadOrder<IModListing<IFallout4ModGetter>>?> GetLoadOrderAsync()
    {
        if (_loadOrder != null)
            return _loadOrder;

        try
        {
            var gameDataPath = _configService.GetGameDataPath();
            
            // Determine data folder path
            DirectoryPath dataFolderPath;
            if (!string.IsNullOrEmpty(gameDataPath) && System.IO.Directory.Exists(gameDataPath))
            {
                dataFolderPath = gameDataPath;
            }
            else
            {
                // Try to auto-detect Fallout 4 installation
                var gameRelease = GameRelease.Fallout4;
                if (GameLocations.TryGetDataFolder(gameRelease, out var dataFolder))
                {
                    dataFolderPath = dataFolder;
                }
                else
                {
                    throw new System.IO.DirectoryNotFoundException($"Game data folder not found. Please configure the game data path.");
                }
            }

            // TODO: Detect and configure Mod Organizer 2 or Vortex integration
            // For now, load plugins from the data folder directly
            // MO2 detection: Check for ModOrganizer.ini or use environment variables
            // Vortex detection: Check for Vortex deployment manifests
            
            // Get the load order listings
            var listings = PluginListings.LoadOrderListings(
                GameRelease.Fallout4,
                dataFolderPath,
                throwOnMissingMods: false
            );

            // Import the load order with Fallout4 mod getters
            _loadOrder = await Task.Run(() => 
                LoadOrder.Import<IFallout4ModGetter>(
                    dataFolderPath,
                    listings,
                    GameRelease.Fallout4
                )
            );
            
            return _loadOrder;
        }
        catch (Exception ex)
        {
            // Log error - TODO: Add proper logging
            Console.WriteLine($"Failed to load plugin load order: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> ValidateLoadOrderAsync()
    {
        try
        {
            var loadOrder = await GetLoadOrderAsync();
            return loadOrder != null && loadOrder.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    public string GetGameDataPath()
    {
        return _configService.GetGameDataPath();
    }
}
