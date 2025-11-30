// Copyright (c) Munition AutoPatcher contributors. Licensed under the MIT License.

using IntegrationTests.Infrastructure;
using IntegrationTests.Infrastructure.Models;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins.Records;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// T017: Extension methods for registering standard builder actions with ScenarioCatalog.
/// These actions are used by scenario manifests via BuilderActionName references.
/// </summary>
public static class TestDataFactoryScenarioExtensions
{
    /// <summary>
    /// Registers all standard builder actions with the scenario catalog.
    /// </summary>
    /// <param name="catalog">The catalog to register actions with.</param>
    public static void RegisterAllActions(ScenarioCatalog catalog)
    {
        // Basic scenarios
        catalog.RegisterBuilderAction("CreateBasicWeapon", CreateBasicWeaponAction);
        catalog.RegisterBuilderAction("CreateBasicWeaponAmmoScenario", CreateBasicWeaponAmmoScenarioAction);
        catalog.RegisterBuilderAction("CreateWeaponWithOmod", CreateWeaponWithOmodAction);
        catalog.RegisterBuilderAction("CreateWeaponWithMultipleOmods", CreateWeaponWithMultipleOmodsAction);
        
        // DLC scenarios
        catalog.RegisterBuilderAction("CreateDlcWeaponRemap", CreateDlcWeaponRemapAction);
        catalog.RegisterBuilderAction("CreateDlcAmmoRemap", CreateDlcAmmoRemapAction);
        
        // Edge case scenarios
        catalog.RegisterBuilderAction("CreateEmptyPlugin", CreateEmptyPluginAction);
        catalog.RegisterBuilderAction("CreatePluginWithNoWeapons", CreatePluginWithNoWeaponsAction);
        catalog.RegisterBuilderAction("CreatePluginWithInvalidRecords", CreatePluginWithInvalidRecordsAction);
        
        // Multi-plugin scenarios
        catalog.RegisterBuilderAction("CreateMasterPlugin", CreateMasterPluginAction);
        catalog.RegisterBuilderAction("CreateDependentPlugin", CreateDependentPluginAction);
    }

    #region Basic Scenarios

    /// <summary>
    /// Creates a basic weapon record in the test environment.
    /// </summary>
    private static void CreateBasicWeaponAction(TestEnvironmentBuilder builder)
    {
        builder.WithWeapon("TestMod.esp", "TestWeapon");
    }

    /// <summary>
    /// Creates a weapon with associated ammo for a complete mapping scenario.
    /// </summary>
    private static void CreateBasicWeaponAmmoScenarioAction(TestEnvironmentBuilder builder)
    {
        builder
            .WithAmmunition("TestMod.esp", "TestAmmo")
            .WithWeapon("TestMod.esp", "TestWeapon", "TestAmmo");
    }

    /// <summary>
    /// Creates a weapon with a single OMOD attachment.
    /// Note: ObjectModifications in FO4 is a polymorphic group and requires 
    /// concrete type creation via WeaponModification. For E2E harness testing,
    /// we simplify to just weapons to test the harness plumbing.
    /// </summary>
    private static void CreateWeaponWithOmodAction(TestEnvironmentBuilder builder)
    {
        // Simplified: Just create weapon with ammo for harness testing
        // Full OMOD creation would require WeaponModification concrete instances
        builder
            .WithAmmunition("TestMod.esp", "OmodTestAmmo")
            .WithWeapon("TestMod.esp", "TestWeaponWithOmod", "OmodTestAmmo");
    }

    /// <summary>
    /// Creates a weapon with multiple OMOD attachments.
    /// Note: Simplified for harness testing - real OMOD testing requires integration tests
    /// with WeaponModification concrete type instantiation.
    /// </summary>
    private static void CreateWeaponWithMultipleOmodsAction(TestEnvironmentBuilder builder)
    {
        // Simplified: Multiple weapons to represent multi-omod scenario structure
        builder
            .WithAmmunition("TestMod.esp", "MultiOmodAmmo")
            .WithWeapon("TestMod.esp", "TestWeaponWithMultipleOmods", "MultiOmodAmmo");
    }

    #endregion

    #region DLC Scenarios

    /// <summary>
    /// Creates a DLC weapon that needs remapping.
    /// </summary>
    private static void CreateDlcWeaponRemapAction(TestEnvironmentBuilder builder)
    {
        builder.WithWeapon("DLCMod.esp", "DLC01_WeaponAssaultRifle");
    }

    /// <summary>
    /// Creates DLC ammo that needs remapping.
    /// </summary>
    private static void CreateDlcAmmoRemapAction(TestEnvironmentBuilder builder)
    {
        builder.WithAmmunition("DLCMod.esp", "DLC01_Ammo556");
    }

    #endregion

    #region Edge Case Scenarios

    /// <summary>
    /// Creates an empty plugin with no records.
    /// </summary>
    private static void CreateEmptyPluginAction(TestEnvironmentBuilder builder)
    {
        // Add an empty plugin with no records
        builder.WithPlugin("EmptyMod.esp");
    }

    /// <summary>
    /// Creates a plugin with ammo but no weapons.
    /// </summary>
    private static void CreatePluginWithNoWeaponsAction(TestEnvironmentBuilder builder)
    {
        builder.WithAmmunition("AmmoOnlyMod.esp", "AmmoOnly");
    }

    /// <summary>
    /// Creates a plugin with intentionally invalid/problematic records.
    /// </summary>
    private static void CreatePluginWithInvalidRecordsAction(TestEnvironmentBuilder builder)
    {
        // Create a plugin with minimal weapon (EditorID set to empty string to simulate problematic data)
        builder.WithPlugin("InvalidMod.esp", mod =>
        {
            var weapon = mod.Weapons.AddNew();
            weapon.EditorID = ""; // Empty EditorID for testing edge cases
        });
    }

    #endregion

    #region Multi-Plugin Scenarios

    /// <summary>
    /// Creates a master plugin that other plugins depend on.
    /// </summary>
    private static void CreateMasterPluginAction(TestEnvironmentBuilder builder)
    {
        builder
            .WithAmmunition("MasterMod.esp", "MasterAmmo")
            .WithWeapon("MasterMod.esp", "MasterWeapon", "MasterAmmo");
    }

    /// <summary>
    /// Creates a plugin that depends on a master plugin's records.
    /// </summary>
    private static void CreateDependentPluginAction(TestEnvironmentBuilder builder)
    {
        builder.WithWeapon("DependentMod.esp", "DependentWeapon");
    }

    #endregion
}
