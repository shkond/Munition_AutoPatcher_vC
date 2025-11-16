using Xunit;
using MunitionAutoPatcher.Services.Implementations;
using Microsoft.Extensions.Logging.Abstractions;
using System.Threading.Tasks;
using IntegrationTests.Infrastructure;

namespace IntegrationTests;

/// <summary>
/// Example integration test that demonstrates the exact scenario requested:
/// Create weapon A that references ammo B, then verify WeaponDataExtractor
/// correctly resolves the relationship in a virtual Mutagen environment.
/// 
/// This test serves as a reference implementation for the integration testing approach.
/// </summary>
public class ExampleIntegrationTest
{
    /// <summary>
    /// Creates a virtual environment with weapon A referencing ammo B,
    /// then verifies WeaponDataExtractor correctly extracts and resolves the relationship.
    /// 
    /// This test demonstrates:
    /// 1. Creating in-memory plugin with TestEnvironmentBuilder
    /// 2. Setting up weapon-ammo relationships
    /// 3. Creating COBJ that references the weapon
    /// 4. Using WeaponDataExtractor with virtual environment
    /// 5. Verifying correct relationship resolution
    /// </summary>
    [Fact]
    public async Task WeaponDataExtractor_WithVirtualEnvironment_ResolvesWeaponAmmoRelationship()
    {
        // Arrange: Create virtual environment with weapon A and ammo B
        var gameEnv = new TestEnvironmentBuilder()
            .WithPlugin("MyMod.esp", mod =>
            {
                // Create ammo B
                var ammoB = mod.Ammunitions.AddNew();
                ammoB.EditorID = "AmmoB";
                ammoB.Name = "Ammunition B";

                // Create weapon A that references ammo B
                var weaponA = mod.Weapons.AddNew();
                weaponA.EditorID = "WeaponA";
                weaponA.Name = "Weapon A";
                weaponA.Ammo = ammoB.ToLink(); // Weapon A references Ammo B

                // Create constructible object that creates weapon A
                var cobj = mod.ConstructibleObjects.AddNew();
                cobj.EditorID = "cobj_WeaponA";
                cobj.CreatedObject = weaponA.ToLink();
            })
            .Build();

        // Create the Mutagen environment adapter
        var loggerAdapter = NullLogger<MutagenV51EnvironmentAdapter>.Instance;
        var mutagenEnv = new MutagenV51EnvironmentAdapter(gameEnv, loggerAdapter);

        // Create the resourced environment for proper disposal
        var loggerResourced = NullLogger<ResourcedMutagenEnvironment>.Instance;
        using var resourcedEnv = new ResourcedMutagenEnvironment(mutagenEnv, gameEnv, loggerResourced);

        // Create WeaponDataExtractor instance
        var extractorLogger = NullLogger<WeaponDataExtractor>.Instance;
        var extractor = new WeaponDataExtractor(extractorLogger);

        // No excluded plugins for this test
        var excludedPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Act: Extract weapon data from virtual environment
        var results = await extractor.ExtractAsync(resourcedEnv, excludedPlugins);

        // Assert: Verify WeaponDataExtractor correctly resolved weapon A and ammo B
        Assert.NotNull(results);
        var candidate = Assert.Single(results);

        // Verify COBJ information
        Assert.Equal("COBJ", candidate.CandidateType);
        Assert.Equal("cobj_WeaponA", candidate.CandidateEditorId);
        Assert.Equal("CreatedWeapon", candidate.SuggestedTarget);

        // Verify weapon A information
        Assert.Equal("WeaponA", candidate.BaseWeaponEditorId);

        // Verify ammo B information - this is the key test!
        Assert.NotNull(candidate.CandidateAmmo);
        Assert.Equal("AmmoB", candidate.CandidateAmmoEditorId);

        // Verify source plugin information
        Assert.Contains("MyMod.esp", candidate.SourcePlugin);

        // Additional verification: Ensure the FormKey information is correct
        Assert.NotNull(candidate.CandidateFormKey);
        Assert.Equal("MyMod.esp", candidate.CandidateFormKey.PluginName);
        Assert.True(candidate.CandidateFormKey.FormId > 0);

        // Verify ammo FormKey information
        Assert.NotNull(candidate.CandidateAmmo);
        Assert.Equal("MyMod.esp", candidate.CandidateAmmo.PluginName);
        Assert.True(candidate.CandidateAmmo.FormId > 0);
    }

    /// <summary>
    /// Demonstrates a more complex scenario with multiple weapons and ammunition types
    /// to show the scalability of the virtual environment approach.
    /// </summary>
    [Fact]
    public async Task WeaponDataExtractor_WithMultipleWeaponsAndAmmo_ResolvesAllRelationships()
    {
        // Arrange: Create virtual environment with multiple weapon-ammo pairs
        var gameEnv = new TestEnvironmentBuilder()
            .WithPlugin("WeaponPack.esp", mod =>
            {
                // Create multiple ammunition types
                var ammo556 = mod.Ammunitions.AddNew();
                ammo556.EditorID = "Ammo556";
                ammo556.Name = "5.56mm Ammunition";

                var ammo762 = mod.Ammunitions.AddNew();
                ammo762.EditorID = "Ammo762";
                ammo762.Name = "7.62mm Ammunition";

                var ammoShotgun = mod.Ammunitions.AddNew();
                ammoShotgun.EditorID = "AmmoShotgun";
                ammoShotgun.Name = "Shotgun Shell";

                // Create weapons that reference different ammunition
                var rifle556 = mod.Weapons.AddNew();
                rifle556.EditorID = "Rifle556";
                rifle556.Name = "5.56mm Rifle";
                rifle556.Ammo = ammo556.ToLink();

                var rifle762 = mod.Weapons.AddNew();
                rifle762.EditorID = "Rifle762";
                rifle762.Name = "7.62mm Rifle";
                rifle762.Ammo = ammo762.ToLink();

                var shotgun = mod.Weapons.AddNew();
                shotgun.EditorID = "Shotgun";
                shotgun.Name = "Combat Shotgun";
                shotgun.Ammo = ammoShotgun.ToLink();

                // Create COBJs for each weapon
                var cobj556 = mod.ConstructibleObjects.AddNew();
                cobj556.EditorID = "cobj_Rifle556";
                cobj556.CreatedObject = rifle556.ToLink();

                var cobj762 = mod.ConstructibleObjects.AddNew();
                cobj762.EditorID = "cobj_Rifle762";
                cobj762.CreatedObject = rifle762.ToLink();

                var cobjShotgun = mod.ConstructibleObjects.AddNew();
                cobjShotgun.EditorID = "cobj_Shotgun";
                cobjShotgun.CreatedObject = shotgun.ToLink();
            })
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

        // Assert: Verify all three weapon-ammo relationships are correctly resolved
        Assert.NotNull(results);
        Assert.Equal(3, results.Count);

        // Verify 5.56mm rifle
        var rifle556Result = results.FirstOrDefault(r => r.BaseWeaponEditorId == "Rifle556");
        Assert.NotNull(rifle556Result);
        Assert.Equal("cobj_Rifle556", rifle556Result.CandidateEditorId);
        Assert.Equal("Ammo556", rifle556Result.CandidateAmmoEditorId);

        // Verify 7.62mm rifle
        var rifle762Result = results.FirstOrDefault(r => r.BaseWeaponEditorId == "Rifle762");
        Assert.NotNull(rifle762Result);
        Assert.Equal("cobj_Rifle762", rifle762Result.CandidateEditorId);
        Assert.Equal("Ammo762", rifle762Result.CandidateAmmoEditorId);

        // Verify shotgun
        var shotgunResult = results.FirstOrDefault(r => r.BaseWeaponEditorId == "Shotgun");
        Assert.NotNull(shotgunResult);
        Assert.Equal("cobj_Shotgun", shotgunResult.CandidateEditorId);
        Assert.Equal("AmmoShotgun", shotgunResult.CandidateAmmoEditorId);
    }
}