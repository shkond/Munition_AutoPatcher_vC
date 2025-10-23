using MunitionAutoPatcher.Services.Interfaces;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Installs;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Order;
using Mutagen.Bethesda.Environments;
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

            // First, prefer Mutagen's GameEnvironment which will represent the VFS when
            // this process is launched via Mod Organizer 2. This gives the merged Data view.
            try
            {
                // Attempt to create a typical GameEnvironment for Fallout 4. When launched via MO2
                // this will observe the virtualized Data folder and provide the merged LoadOrder getters.
                using var env = GameEnvironment.Typical.Fallout4(Fallout4Release.Fallout4);
                var envData = env.DataFolderPath;

                // env.LoadOrder is a getter-based view; convert it to a concrete ILoadOrder by
                // importing from the same data folder using the environment's listings.
                var envListings = PluginListings.LoadOrderListings(GameRelease.Fallout4, envData, throwOnMissingMods: false);
                var imported = LoadOrder.Import<IFallout4ModGetter>(envData, envListings, GameRelease.Fallout4);
                if (imported != null)
                {
                    _loadOrder = imported;
                    Console.WriteLine($"Using GameEnvironment-derived load order (DataFolder={envData})");
                    return _loadOrder;
                }
            }
            catch
            {
                // If GameEnvironment is not available (not running under MO2 or detection failed),
                // fall back to the explicit data-folder based approach below.
            }

            // Determine data folder path (fallback)
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

            // Get the load order listings from the explicit data folder
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
