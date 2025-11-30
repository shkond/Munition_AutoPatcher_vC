// Copyright (c) Munition AutoPatcher contributors. Licensed under the MIT License.

using IntegrationTests.Infrastructure;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using Xunit;
using Xunit.Abstractions;

namespace IntegrationTests;

/// <summary>
/// Tests to verify in-memory LinkCache functionality.
/// These tests diagnose whether TestEnvironmentBuilder's in-memory LinkCache
/// can properly resolve records created during test setup.
/// </summary>
public class InMemoryLinkCacheTests
{
    private readonly ITestOutputHelper _output;

    public InMemoryLinkCacheTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void InMemoryLinkCache_CanResolveCobj_WhenBuiltWithTestEnvironmentBuilder()
    {
        // Arrange - Create test environment with COBJ and Weapon
        var envBuilder = new TestEnvironmentBuilder();
        envBuilder
            .WithAmmunition("TestMod.esp", "TestAmmo001")
            .WithWeapon("TestMod.esp", "TestWeapon001", "TestAmmo001")
            .WithConstructibleObject("TestMod.esp", "co_TestWeapon001", "TestWeapon001");

        // Build in-memory LinkCache
        var linkCache = envBuilder.BuildInMemoryLinkCache();

        // Diagnostic: List all mods in the cache
        _output.WriteLine("=== Mods in LinkCache ===");
        var mods = envBuilder.GetModsInOrder();
        foreach (var mod in mods)
        {
            _output.WriteLine($"Mod: {mod.ModKey.FileName}");
            _output.WriteLine($"  Weapons: {mod.Weapons.Count}");
            _output.WriteLine($"  Ammunitions: {mod.Ammunitions.Count}");
            _output.WriteLine($"  ConstructibleObjects: {mod.ConstructibleObjects.Count}");
            
            foreach (var cobj in mod.ConstructibleObjects)
            {
                _output.WriteLine($"    COBJ: {cobj.EditorID} FK={cobj.FormKey.ModKey.FileName}:{cobj.FormKey.ID:X8}");
                _output.WriteLine($"      CreatedObject: {cobj.CreatedObject.FormKey.ModKey.FileName}:{cobj.CreatedObject.FormKey.ID:X8}");
            }
        }

        // Try to resolve COBJ by FormKey
        var testModKey = ModKey.FromNameAndExtension("TestMod.esp");
        
        // Find the COBJ's FormKey from the test mod
        var testMod = envBuilder.GetMod("TestMod.esp");
        Assert.NotNull(testMod);
        
        var cobjFromMod = testMod.ConstructibleObjects.FirstOrDefault(c => c.EditorID == "co_TestWeapon001");
        Assert.NotNull(cobjFromMod);
        
        var cobjFormKey = cobjFromMod.FormKey;
        _output.WriteLine($"\n=== Resolving COBJ ===");
        _output.WriteLine($"COBJ FormKey: {cobjFormKey.ModKey.FileName}:{cobjFormKey.ID:X8}");

        // Act - Try to resolve via LinkCache
        var resolved = linkCache.TryResolve<IConstructibleObjectGetter>(cobjFormKey, out var resolvedCobj);

        // Assert
        _output.WriteLine($"Resolution result: {resolved}");
        if (resolvedCobj != null)
        {
            _output.WriteLine($"Resolved COBJ EditorID: {resolvedCobj.EditorID}");
        }

        Assert.True(resolved, "LinkCache should resolve COBJ by FormKey");
        Assert.NotNull(resolvedCobj);
        Assert.Equal("co_TestWeapon001", resolvedCobj.EditorID);
    }

    [Fact]
    public void InMemoryLinkCache_CanResolveWeapon_WhenBuiltWithTestEnvironmentBuilder()
    {
        // Arrange
        var envBuilder = new TestEnvironmentBuilder();
        envBuilder
            .WithAmmunition("TestMod.esp", "TestAmmo001")
            .WithWeapon("TestMod.esp", "TestWeapon001", "TestAmmo001");

        var linkCache = envBuilder.BuildInMemoryLinkCache();

        var testMod = envBuilder.GetMod("TestMod.esp");
        Assert.NotNull(testMod);
        
        var weaponFromMod = testMod.Weapons.FirstOrDefault(w => w.EditorID == "TestWeapon001");
        Assert.NotNull(weaponFromMod);
        
        var weaponFormKey = weaponFromMod.FormKey;
        _output.WriteLine($"Weapon FormKey: {weaponFormKey.ModKey.FileName}:{weaponFormKey.ID:X8}");

        // Act
        var resolved = linkCache.TryResolve<IWeaponGetter>(weaponFormKey, out var resolvedWeapon);

        // Assert
        _output.WriteLine($"Resolution result: {resolved}");
        Assert.True(resolved, "LinkCache should resolve Weapon by FormKey");
        Assert.NotNull(resolvedWeapon);
        Assert.Equal("TestWeapon001", resolvedWeapon.EditorID);
    }

    [Fact]
    public void InMemoryLinkCache_CanResolveAmmo_WhenBuiltWithTestEnvironmentBuilder()
    {
        // Arrange
        var envBuilder = new TestEnvironmentBuilder();
        envBuilder.WithAmmunition("TestMod.esp", "TestAmmo001");

        var linkCache = envBuilder.BuildInMemoryLinkCache();

        var testMod = envBuilder.GetMod("TestMod.esp");
        Assert.NotNull(testMod);
        
        var ammoFromMod = testMod.Ammunitions.FirstOrDefault(a => a.EditorID == "TestAmmo001");
        Assert.NotNull(ammoFromMod);
        
        var ammoFormKey = ammoFromMod.FormKey;
        _output.WriteLine($"Ammo FormKey: {ammoFormKey.ModKey.FileName}:{ammoFormKey.ID:X8}");

        // Act
        var resolved = linkCache.TryResolve<IAmmunitionGetter>(ammoFormKey, out var resolvedAmmo);

        // Assert
        _output.WriteLine($"Resolution result: {resolved}");
        Assert.True(resolved, "LinkCache should resolve Ammunition by FormKey");
        Assert.NotNull(resolvedAmmo);
        Assert.Equal("TestAmmo001", resolvedAmmo.EditorID);
    }
}
