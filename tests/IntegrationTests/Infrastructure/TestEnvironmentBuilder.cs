using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Order;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins.Binary;
using Mutagen.Bethesda.Plugins.Records;
using Noggog;

namespace IntegrationTests.Infrastructure;

/// Builder for creating virtual Mutagen environments for integration testing.
/// Provides in-memory plugin environments without requiring actual game files.
/// </summary>
public class TestEnvironmentBuilder
{
    private readonly MockFileSystem _mockFileSystem;
    private readonly string _testDataPath;
    private readonly List<ModKey> _modKeys;
    private readonly Dictionary<ModKey, Fallout4Mod> _mods;

    /// <summary>
    /// Initializes a new TestEnvironmentBuilder with a mock file system.
    /// </summary>
    public TestEnvironmentBuilder()
    {
        _mockFileSystem = new MockFileSystem();
        _testDataPath = @"C:\Games\Fallout4\Data";
        _modKeys = new List<ModKey>();
        _mods = new Dictionary<ModKey, Fallout4Mod>();
        
        // Create the base directory structure
        _mockFileSystem.AddDirectory(_testDataPath);
        
        // Add Fallout4.esm as the master file (required for proper environment setup)
        AddMasterFile();
    }

    /// <summary>
    /// Adds a plugin to the virtual environment with optional modification action.
    /// </summary>
    /// <param name="pluginName">Name of the plugin file (e.g., "MyMod.esp")</param>
    /// <param name="modifyAction">Optional action to modify the plugin before adding</param>
    /// <returns>This builder instance for method chaining</returns>
    public TestEnvironmentBuilder WithPlugin(string pluginName, Action<Fallout4Mod>? modifyAction = null)
    {
        var modKey = ModKey.FromNameAndExtension(pluginName);
        _modKeys.Add(modKey);
        
        var mod = new Fallout4Mod(modKey, Fallout4Release.Fallout4);
        
        // Apply modifications if provided
        modifyAction?.Invoke(mod);
        
        // Store the mod for potential future reference
        _mods[modKey] = mod;
        
        // Write the mod to the mock file system
        var filePath = Path.Combine(_testDataPath, pluginName);
        using var memStream = new MemoryStream();
        mod.WriteToBinary(memStream);
        _mockFileSystem.AddFile(filePath, new MockFileData(memStream.ToArray()));
        
        return this;
    }

    /// <summary>
    /// Creates a weapon with specified properties and optional ammunition reference.
    /// </summary>
    /// <param name="pluginName">Plugin to add the weapon to</param>
    /// <param name="editorId">Editor ID for the weapon</param>
    /// <param name="ammoEditorId">Optional ammunition editor ID to reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public TestEnvironmentBuilder WithWeapon(string pluginName, string editorId, string? ammoEditorId = null)
    {
        return WithPlugin(pluginName, mod =>
        {
            var weapon = mod.Weapons.AddNew();
            weapon.EditorID = editorId;
            
            // If ammunition is specified, try to find it and create a reference
            if (!string.IsNullOrEmpty(ammoEditorId))
            {
                // Look for existing ammo in this mod or create a placeholder reference
                var existingAmmo = mod.Ammunitions.FirstOrDefault(a => a.EditorID == ammoEditorId);
                if (existingAmmo != null)
                {
                    weapon.Ammo = existingAmmo.ToLink();
                }
                else
                {
                    // Create a new ammo record if it doesn't exist
                    var ammo = mod.Ammunitions.AddNew();
                    ammo.EditorID = ammoEditorId;
                    weapon.Ammo = ammo.ToLink();
                }
            }
        });
    }

    /// <summary>
    /// Creates ammunition with specified properties.
    /// </summary>
    /// <param name="pluginName">Plugin to add the ammunition to</param>
    /// <param name="editorId">Editor ID for the ammunition</param>
    /// <returns>This builder instance for method chaining</returns>
    public TestEnvironmentBuilder WithAmmunition(string pluginName, string editorId)
    {
        return WithPlugin(pluginName, mod =>
        {
            var ammo = mod.Ammunitions.AddNew();
            ammo.EditorID = editorId;
        });
    }

    /// <summary>
    /// Creates a constructible object (COBJ) that references a weapon.
    /// </summary>
    /// <param name="pluginName">Plugin to add the COBJ to</param>
    /// <param name="editorId">Editor ID for the COBJ</param>
    /// <param name="weaponEditorId">Editor ID of the weapon to reference</param>
    /// <returns>This builder instance for method chaining</returns>
    public TestEnvironmentBuilder WithConstructibleObject(string pluginName, string editorId, string weaponEditorId)
    {
        return WithPlugin(pluginName, mod =>
        {
            var cobj = mod.ConstructibleObjects.AddNew();
            cobj.EditorID = editorId;
            
            // Find the weapon to reference
            var weapon = mod.Weapons.FirstOrDefault(w => w.EditorID == weaponEditorId);
            if (weapon != null)
            {
                cobj.CreatedObject = weapon.ToLink().AsSetter().AsNullable();
            }
            else
            {
                // Create a placeholder weapon if it doesn't exist
                var newWeapon = mod.Weapons.AddNew();
                newWeapon.EditorID = weaponEditorId;
                cobj.CreatedObject = newWeapon.ToLink().AsSetter().AsNullable();
            }
        });
    }

    /// <summary>
    /// Builds the virtual game environment with all configured plugins.
    /// </summary>
    /// <returns>A configured game environment for testing</returns>
    public IGameEnvironment<IFallout4Mod, IFallout4ModGetter> Build()
    {
        // Create the load order file
        CreateLoadOrderFile();
        
        // Build the game environment using Mutagen's builder pattern
        return GameEnvironment.Typical.Builder<IFallout4Mod, IFallout4ModGetter>(GameRelease.Fallout4)
            .WithResolver(type =>
            {
                if (type == typeof(IFileSystem)) 
                    return _mockFileSystem;
                if (type == typeof(IDataDirectoryProvider)) 
                    return new MockDataDirectoryProvider(_testDataPath);
                if (type == typeof(IPluginListingsPathContext)) 
                    return new MockPluginListingsPathContext(GetLoadOrderPath());
                return null;
            })
            .WithTargetDataFolder(_testDataPath)
            .Build();
    }

    /// <summary>
    /// Gets a reference to a mod that was added to this builder.
    /// </summary>
    /// <param name="pluginName">Name of the plugin</param>
    /// <returns>The mod instance, or null if not found</returns>
    public Fallout4Mod? GetMod(string pluginName)
    {
        var modKey = ModKey.FromNameAndExtension(pluginName);
        return _mods.TryGetValue(modKey, out var mod) ? mod : null;
    }

    private void AddMasterFile()
    {
        // Add Fallout4.esm as a minimal master file
        var masterKey = ModKey.FromNameAndExtension("Fallout4.esm");
        _modKeys.Insert(0, masterKey); // Master files go first
        
        var masterMod = new Fallout4Mod(masterKey, Fallout4Release.Fallout4);
        _mods[masterKey] = masterMod;
        
        var masterPath = Path.Combine(_testDataPath, "Fallout4.esm");
        using var memStream = new MemoryStream();
        masterMod.WriteToBinary(memStream);
        _mockFileSystem.AddFile(masterPath, new MockFileData(memStream.ToArray()));
    }

    private void CreateLoadOrderFile()
    {
        var loadOrderPath = GetLoadOrderPath();
        _mockFileSystem.AddDirectory(Path.GetDirectoryName(loadOrderPath)!);
        
        // Create load order content with master files first, then plugins
        var loadOrderContent = string.Join(Environment.NewLine, 
            _modKeys.Select(m => m.Type == ModType.Master ? m.FileName.ToString() : $"*{m.FileName}"));
        
        _mockFileSystem.AddFile(loadOrderPath, new MockFileData(loadOrderContent));
    }

    private string GetLoadOrderPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            @"Fallout4\Plugins.txt");
    }
}
        
public interface IDataDirectoryProvider { string Path { get; } }
public interface IPluginListingsPathContext { string Path { get; } }

/// <summary>
/// Mock implementation of IDataDirectoryProvider for testing.
/// </summary>
public class MockDataDirectoryProvider : IDataDirectoryProvider
{
    public string Path { get; }

    public MockDataDirectoryProvider(string path) => Path = path;
}

/// <summary>
/// Mock implementation of IPluginListingsPathContext for testing.
/// </summary>
public class MockPluginListingsPathContext : IPluginListingsPathContext
{
    public string Path { get; }

    public MockPluginListingsPathContext(string path) => Path = path;
}