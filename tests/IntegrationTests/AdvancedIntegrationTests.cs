using Xunit;
using MunitionAutoPatcher.Services.Implementations;
using Microsoft.Extensions.Logging.Abstractions;
using System.Threading.Tasks;
using IntegrationTests.Infrastructure;
using MunitionAutoPatcher.Models;

namespace IntegrationTests;

/// <summary>
/// Advanced integration tests that demonstrate complex scenarios and edge cases
/// for WeaponDataExtractor with virtual Mutagen environments.
/// </summary>
public class AdvancedIntegrationTests
{
    /// <summary>
    /// Tests performance with a large number of weapons and ammunition.
    /// Verifies that WeaponDataExtractor can handle realistic mod sizes
    /// without performance degradation.
    /// </summary>
    [Fact]
    public async Task ExtractAsync_WithLargeDataSet_HandlesPerformanceGracefully()
    {
        // Arrange
        var builder = new TestEnvironmentBuilder();
        
        // Create a plugin with many weapons and ammunition types
        builder.WithPlugin("LargeMod.esp", mod =>
        {
            // Create 50 different ammunition types
            for (int i = 0; i < 50; i++)
            {
                var ammo = mod.Ammunitions.AddNew();
                ammo.EditorID = $"Ammo_{i:D3}";
                ammo.Name = $"Ammunition Type {i}";
            }

            // Create 100 weapons, each referencing different ammunition
            for (int i = 0; i < 100; i++)
            {
                var weapon = mod.Weapons.AddNew();
                weapon.EditorID = $"Weapon_{i:D3}";
                weapon.Name = $"Weapon {i}";
                
                // Reference ammunition (cycling through available ammo)
                var ammoIndex = i % 50;
                var targetAmmo = mod.Ammunitions.Skip(ammoIndex).First();
                weapon.Ammo = targetAmmo.ToLink();

                // Create COBJ for each weapon
                var cobj = mod.ConstructibleObjects.AddNew();
                cobj.EditorID = $"cobj_Weapon_{i:D3}";
                cobj.CreatedObject = weapon.ToLink();
            }
        });

        var gameEnv = builder.Build();
        var loggerAdapter = NullLogger<MutagenV51EnvironmentAdapter>.Instance;
        var mutagenEnv = new MutagenV51EnvironmentAdapter(gameEnv, loggerAdapter);
        var loggerResourced = NullLogger<ResourcedMutagenEnvironment>.Instance;
        
        using var resourcedEnv = new ResourcedMutagenEnvironment(mutagenEnv, gameEnv, loggerResourced);
        var extractorLogger = NullLogger<WeaponDataExtractor>.Instance;
        var extractor = new WeaponDataExtractor(extractorLogger);
        var excludedPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var results = await extractor.ExtractAsync(resourcedEnv, excludedPlugins);
        stopwatch.Stop();

        // Assert
        Assert.NotNull(results);
        Assert.Equal(100, results.Count); // Should find all 100 COBJ records
        Assert.True(stopwatch.ElapsedMilliseconds < 5000, $"Extraction took too long: {stopwatch.ElapsedMilliseconds}ms");
        
        // Verify a few random samples
        var sample1 = results.FirstOrDefault(r => r.CandidateEditorId == "cobj_Weapon_025");
        Assert.NotNull(sample1);
        Assert.Equal("Weapon_025", sample1.BaseWeaponEditorId);
        Assert.Equal("Ammo_025", sample1.CandidateAmmoEditorId);
    }

    /// <summary>
    /// Tests cross-plugin references and load order handling.
    /// Verifies that WeaponDataExtractor correctly handles scenarios where
    /// weapons in one plugin reference ammunition from another plugin.
    /// </summary>
    [Fact]
    public async Task ExtractAsync_WithCrossPluginReferences_ResolvesCorrectly()
    {
        // Arrange
        var gameEnv = new TestEnvironmentBuilder()
            .CreateCrossPluginScenario()
            .Build();

        var loggerAdapter = NullLogger<MutagenV51EnvironmentAdapter>.Instance;
        var mutagenEnv = new MutagenV51EnvironmentAdapter(gameEnv, loggerAdapter);
        var loggerResourced = NullLogger<ResourcedMutagenEnvironment>.Instance;
        
        using var resourcedEnv = new ResourcedMutagenEnvironment(mutagenEnv, gameEnv, loggerResourced);
        var extractorLogger = NullLogger<WeaponDataExtractor>.Instance;
        var extractor = new WeaponDataExtractor(extractorLogger);
        var excludedPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Act
        var results = await extractor.ExtractAsync(resourcedEnv, excludedPlugins);

        // Assert
        Assert.NotNull(results);
        var candidate = Assert.Single(results);
        
        Assert.Equal("cobj_CustomWeapon", candidate.CandidateEditorId);
        Assert.Equal("CustomWeapon", candidate.BaseWeaponEditorId);
        Assert.Equal("CustomAmmo", candidate.CandidateAmmoEditorId);
        Assert.Contains("WeaponMod.esp", candidate.SourcePlugin);
    }
}