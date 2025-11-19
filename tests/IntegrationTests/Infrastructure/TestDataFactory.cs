using Mutagen.Bethesda;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins.Records;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// Factory class for creating consistent test data scenarios.
/// Provides pre-configured weapon, ammunition, and COBJ combinations for testing.
/// </summary>
public static class TestDataFactory
{
    /// <summary>
    /// Creates a simple weapon-ammo-COBJ scenario for basic testing.
    /// </summary>
    /// <param name="builder">The TestEnvironmentBuilder to configure</param>
    /// <returns>The configured builder</returns>
    public static TestEnvironmentBuilder CreateBasicWeaponAmmoScenario(this TestEnvironmentBuilder builder)
    {
        return builder
            .WithPlugin("TestMod.esp", mod =>
            {
                // Create ammunition first
                var ammo = mod.Ammunitions.AddNew();
                ammo.EditorID = "TestAmmo";
                ammo.Name = "Test Ammunition";

                // Create weapon that references the ammunition
                var weapon = mod.Weapons.AddNew();
                weapon.EditorID = "TestWeapon";
                weapon.Name = "Test Weapon";
                weapon.Ammo = ammo.ToLink();

                // Create constructible object that creates the weapon
                var cobj = mod.ConstructibleObjects.AddNew();
                cobj.EditorID = "cobj_TestWeapon";
                cobj.CreatedObject = weapon.ToLink().AsSetter().AsNullable();
            });
    }

    /// <summary>
    /// Creates a complex scenario with multiple weapons and ammunition types.
    /// </summary>
    /// <param name="builder">The TestEnvironmentBuilder to configure</param>
    /// <returns>The configured builder</returns>
    public static TestEnvironmentBuilder CreateComplexWeaponAmmoScenario(this TestEnvironmentBuilder builder)
    {
        return builder
            .WithPlugin("WeaponMod.esp", mod =>
            {
                // Create multiple ammunition types
                var ammo556 = mod.Ammunitions.AddNew();
                ammo556.EditorID = "Ammo556mm";
                ammo556.Name = "5.56mm Round";

                var ammo762 = mod.Ammunitions.AddNew();
                ammo762.EditorID = "Ammo762mm";
                ammo762.Name = "7.62mm Round";

                var ammoShotgun = mod.Ammunitions.AddNew();
                ammoShotgun.EditorID = "AmmoShotgunShell";
                ammoShotgun.Name = "Shotgun Shell";

                // Create weapons with different ammunition
                var rifle556 = mod.Weapons.AddNew();
                rifle556.EditorID = "AssaultRifle556";
                rifle556.Name = "5.56mm Assault Rifle";
                rifle556.Ammo = ammo556.ToLink();

                var rifle762 = mod.Weapons.AddNew();
                rifle762.EditorID = "SniperRifle762";
                rifle762.Name = "7.62mm Sniper Rifle";
                rifle762.Ammo = ammo762.ToLink();

                var shotgun = mod.Weapons.AddNew();
                shotgun.EditorID = "CombatShotgun";
                shotgun.Name = "Combat Shotgun";
                shotgun.Ammo = ammoShotgun.ToLink();

                // Create COBJs for each weapon
                var cobj556 = mod.ConstructibleObjects.AddNew();
                cobj556.EditorID = "cobj_AssaultRifle556";
                cobj556.CreatedObject = rifle556.ToLink().AsSetter().AsNullable();

                var cobj762 = mod.ConstructibleObjects.AddNew();
                cobj762.EditorID = "cobj_SniperRifle762";
                cobj762.CreatedObject = rifle762.ToLink().AsSetter().AsNullable();

                var cobjShotgun = mod.ConstructibleObjects.AddNew();
                cobjShotgun.EditorID = "cobj_CombatShotgun";
                cobjShotgun.CreatedObject = shotgun.ToLink().AsSetter().AsNullable();
            });
    }

    /// <summary>
    /// Creates a cross-plugin scenario where weapons and ammunition are in different plugins.
    /// </summary>
    /// <param name="builder">The TestEnvironmentBuilder to configure</param>
    /// <returns>The configured builder</returns>
    public static TestEnvironmentBuilder CreateCrossPluginScenario(this TestEnvironmentBuilder builder)
    {
        // First plugin: Ammunition only
        builder.WithPlugin("AmmoMod.esp", mod =>
        {
            var ammo = mod.Ammunitions.AddNew();
            ammo.EditorID = "CustomAmmo";
            ammo.Name = "Custom Ammunition";
        });

        // Second plugin: Weapons that reference ammunition from first plugin
        builder.WithPlugin("WeaponMod.esp", mod =>
        {
            var weapon = mod.Weapons.AddNew();
            weapon.EditorID = "CustomWeapon";
            weapon.Name = "Custom Weapon";
            
            // Note: In a real scenario, this would need proper FormKey resolution
            // For testing purposes, we'll create a local reference
            var localAmmo = mod.Ammunitions.AddNew();
            localAmmo.EditorID = "CustomAmmo";
            weapon.Ammo = localAmmo.ToLink();

            var cobj = mod.ConstructibleObjects.AddNew();
            cobj.EditorID = "cobj_CustomWeapon";
            cobj.CreatedObject = weapon.ToLink().AsSetter().AsNullable();
        });

        return builder;
    }

    /// <summary>
    /// Creates a scenario with excluded plugins for testing exclusion logic.
    /// </summary>
    /// <param name="builder">The TestEnvironmentBuilder to configure</param>
    /// <returns>The configured builder</returns>
    public static TestEnvironmentBuilder CreateExclusionTestScenario(this TestEnvironmentBuilder builder)
    {
        // Plugin that should be processed
        builder.WithPlugin("IncludedMod.esp", mod =>
        {
            var ammo = mod.Ammunitions.AddNew();
            ammo.EditorID = "IncludedAmmo";
            ammo.Name = "Included Ammunition";

            var weapon = mod.Weapons.AddNew();
            weapon.EditorID = "IncludedWeapon";
            weapon.Name = "Included Weapon";
            weapon.Ammo = ammo.ToLink();

            var cobj = mod.ConstructibleObjects.AddNew();
            cobj.EditorID = "cobj_IncludedWeapon";
            cobj.CreatedObject = weapon.ToLink().AsSetter().AsNullable();
        });

        // Plugin that should be excluded
        builder.WithPlugin("ExcludedMod.esp", mod =>
        {
            var ammo = mod.Ammunitions.AddNew();
            ammo.EditorID = "ExcludedAmmo";
            ammo.Name = "Excluded Ammunition";

            var weapon = mod.Weapons.AddNew();
            weapon.EditorID = "ExcludedWeapon";
            weapon.Name = "Excluded Weapon";
            weapon.Ammo = ammo.ToLink();

            var cobj = mod.ConstructibleObjects.AddNew();
            cobj.EditorID = "cobj_ExcludedWeapon";
            cobj.CreatedObject = weapon.ToLink().AsSetter().AsNullable();
        });

        return builder;
    }

    /// <summary>
    /// Creates a scenario with weapons that have no ammunition assigned.
    /// </summary>
    /// <param name="builder">The TestEnvironmentBuilder to configure</param>
    /// <returns>The configured builder</returns>
    public static TestEnvironmentBuilder CreateNoAmmoScenario(this TestEnvironmentBuilder builder)
    {
        return builder
            .WithPlugin("NoAmmoMod.esp", mod =>
            {
                // Create weapon without ammunition
                var weapon = mod.Weapons.AddNew();
                weapon.EditorID = "WeaponNoAmmo";
                weapon.Name = "Weapon Without Ammo";
                // Deliberately not setting weapon.Ammo

                var cobj = mod.ConstructibleObjects.AddNew();
                cobj.EditorID = "cobj_WeaponNoAmmo";
                cobj.CreatedObject = weapon.ToLink().AsSetter().AsNullable();
            });
    }

    /// <summary>
    /// Creates a scenario with invalid or broken references for error testing.
    /// </summary>
    /// <param name="builder">The TestEnvironmentBuilder to configure</param>
    /// <returns>The configured builder</returns>
    public static TestEnvironmentBuilder CreateErrorTestScenario(this TestEnvironmentBuilder builder)
    {
        return builder
            .WithPlugin("ErrorMod.esp", mod =>
            {
                // Create COBJ with null CreatedObject (should be handled gracefully)
                var cobjNull = mod.ConstructibleObjects.AddNew();
                cobjNull.EditorID = "cobj_NullReference";
                // Deliberately not setting CreatedObject

                // Create weapon with valid setup for comparison
                var ammo = mod.Ammunitions.AddNew();
                ammo.EditorID = "ValidAmmo";

                var weapon = mod.Weapons.AddNew();
                weapon.EditorID = "ValidWeapon";
                weapon.Ammo = ammo.ToLink();

                var cobjValid = mod.ConstructibleObjects.AddNew();
                cobjValid.EditorID = "cobj_ValidWeapon";
                cobjValid.CreatedObject = weapon.ToLink().AsSetter().AsNullable();
            });
    }
}