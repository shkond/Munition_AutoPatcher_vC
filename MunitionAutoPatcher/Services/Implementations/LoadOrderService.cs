using MunitionAutoPatcher.Services.Interfaces;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Installs;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Order;
using Mutagen.Bethesda.Environments;
using Noggog;
using Microsoft.Extensions.Logging;

namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// Implementation of the load order service using Mutagen
/// </summary>
public class LoadOrderService : ILoadOrderService
{
    private readonly IConfigService _configService;
    private readonly IMutagenEnvironmentFactory _mutagenEnvironmentFactory;
    private ILoadOrder<IModListing<IFallout4ModGetter>>? _loadOrder;
    private readonly ILogger<LoadOrderService> _logger;

    public LoadOrderService(IConfigService configService, IMutagenEnvironmentFactory mutagenEnvironmentFactory, ILogger<LoadOrderService> logger)
    {
        _configService = configService;
        _mutagenEnvironmentFactory = mutagenEnvironmentFactory ?? throw new ArgumentNullException(nameof(mutagenEnvironmentFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                // Attempt to create a Mutagen-backed environment via the factory. When launched via MO2
                // this will observe the virtualized Data folder and provide the merged LoadOrder getters.
                using var envRes = _mutagenEnvironmentFactory.Create();
                var envDataOpt = envRes.GetDataFolderPath();
                if (envDataOpt != null)
                {
                    var envData = envDataOpt.Value;
                    var envListings = PluginListings.LoadOrderListings(GameRelease.Fallout4, envData, throwOnMissingMods: false);
                    var imported = LoadOrder.Import<IFallout4ModGetter>(envData, envListings, GameRelease.Fallout4);
                    if (imported != null)
                    {
                        _loadOrder = imported;
                        Console.WriteLine($"Using GameEnvironment-derived load order (DataFolder={envData})");
                        return _loadOrder;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LoadOrderService: GameEnvironment detection failed, falling back to data-folder import");
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
            // Record the full exception into the centralized logger so troubleshooting data is persisted.
            try
            {
                _logger.LogError(ex, "LoadOrderService: failed to load plugin load order");
            }
            catch { }
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
        catch (Exception ex)
        {
            try
            {
                _logger.LogError(ex, "LoadOrderService.ValidateLoadOrderAsync error: {Message}", ex.Message);
            }
            catch (Exception ex2)
            {
                _logger.LogError(ex2, "LoadOrderService: failed to log ValidateLoadOrderAsync error");
            }
            return false;
        }
    }

    public string GetGameDataPath()
    {
        return _configService.GetGameDataPath();
    }
}
