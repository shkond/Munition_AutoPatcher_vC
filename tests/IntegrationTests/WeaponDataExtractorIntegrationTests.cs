using Xunit;
using MunitionAutoPatcher.Services.Implementations;
using Microsoft.Extensions.Logging.Abstractions;
using System.Threading.Tasks;
using IntegrationTests.Infrastructure;
using MunitionAutoPatcher.Models;

namespace IntegrationTests;

/// <summary>
/// Integration tests for WeaponDataExtractor using virtual Mutagen environments.
/// These tests verify that WeaponDataExtractor correctly processes weapon-ammunition
/// relationships in realistic plugin environments without requiring actual game files.
/// </summary>
public class WeaponDataExtractorIntegrationTests
{
    /// <summary>
    /// Tests basic weapon-ammunition extraction with a simple virtual environment.
    /// Verifies that WeaponDataExtractor can extract a weapon and resolve its ammunition
    /// reference when both are in the same plugin.
    /// </summary>
    [Fact]
    public async Task ExtractAsync_WithBasicWeaponAmmoScenario_ExtractsWeaponAndAmmo()
    {
        // Arrange
        var gameEnv = new TestEnvironmentBuilder()
            .CreateBasicWeaponAmmoScenario()
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
        
        Assert.Equal("COBJ", candidate.CandidateType);
        Assert.Equal("cobj_TestWeapon", candidate.CandidateEditorId);
        Assert.Equal("TestWeapon", candidate.BaseWeaponEditorId);
        Assert.NotNull(candidate.CandidateAmmo);
        Assert.Equal("TestAmmo", candidate.CandidateAmmoEditorId);
        Assert.Equal("CreatedWeapon", candidate.SuggestedTarget);
        Assert.Contains("TestMod.esp", candidate.SourcePlugin);
    }

    /// <summary>
    /// Tests extraction with multiple weapons and ammunition types.
    /// Verifies that WeaponDataExtractor can handle complex scenarios with
    /// multiple weapon-ammunition combinations in a single plugin.
    /// </summary>
    [Fact]
    public async Task ExtractAsync_WithComplexWeaponAmmoScenario_ExtractsAllWeaponsAndAmmo()
    {
        // Arrange
        var gameEnv = new TestEnvironmentBuilder()
            .CreateComplexWeaponAmmoScenario()
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
        Assert.Equal(3, results.Count); // Should find 3 COBJ records

        // Verify each weapon-ammo combination
        var assaultRifle = results.FirstOrDefault(r => r.CandidateEditorId == "cobj_AssaultRifle556");
        Assert.NotNull(assaultRifle);
        Assert.Equal("AssaultRifle556", assaultRifle.BaseWeaponEditorId);
        Assert.Equal("Ammo556mm", assaultRifle.CandidateAmmoEditorId);

        var sniperRifle = results.FirstOrDefault(r => r.CandidateEditorId == "cobj_SniperRifle762");
        Assert.NotNull(sniperRifle);
        Assert.Equal("SniperRifle762", sniperRifle.BaseWeaponEditorId);
        Assert.Equal("Ammo762mm", sniperRifle.CandidateAmmoEditorId);

        var shotgun = results.FirstOrDefault(r => r.CandidateEditorId == "cobj_CombatShotgun");
        Assert.NotNull(shotgun);
        Assert.Equal("CombatShotgun", shotgun.BaseWeaponEditorId);
        Assert.Equal("AmmoShotgunShell", shotgun.CandidateAmmoEditorId);
    }

    /// <summary>
    /// Tests plugin exclusion functionality with virtual environments.
    /// Verifies that WeaponDataExtractor correctly excludes plugins specified
    /// in the exclusion list while processing others normally.
    /// </summary>
    [Fact]
    public async Task ExtractAsync_WithExcludedPlugin_SkipsExcludedPluginRecords()
    {
        // Arrange
        var gameEnv = new TestEnvironmentBuilder()
            .CreateExclusionTestScenario()
            .Build();

        var loggerAdapter = NullLogger<MutagenV51EnvironmentAdapter>.Instance;
        var mutagenEnv = new MutagenV51EnvironmentAdapter(gameEnv, loggerAdapter);

        var loggerResourced = NullLogger<ResourcedMutagenEnvironment>.Instance;
        using var resourcedEnv = new ResourcedMutagenEnvironment(mutagenEnv, gameEnv, loggerResourced);

        var extractorLogger = NullLogger<WeaponDataExtractor>.Instance;
        var extractor = new WeaponDataExtractor(extractorLogger);

        var excludedPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ExcludedMod.esp" };

        // Act
        var results = await extractor.ExtractAsync(resourcedEnv, excludedPlugins);

        // Assert
        Assert.NotNull(results);
        var candidate = Assert.Single(results); // Should only find the included plugin's COBJ
        
        Assert.Equal("cobj_IncludedWeapon", candidate.CandidateEditorId);
        Assert.Equal("IncludedWeapon", candidate.BaseWeaponEditorId);
        Assert.Equal("IncludedAmmo", candidate.CandidateAmmoEditorId);
        Assert.Contains("IncludedMod.esp", candidate.SourcePlugin);
    }

    /// <summary>
    /// Tests extraction with weapons that have no ammunition assigned.
    /// Verifies that WeaponDataExtractor handles weapons without ammunition
    /// gracefully and still extracts the COBJ information.
    /// </summary>
    [Fact]
    public async Task ExtractAsync_WithWeaponNoAmmo_ExtractsWeaponWithoutAmmo()
    {
        // Arrange
        var gameEnv = new TestEnvironmentBuilder()
            .CreateNoAmmoScenario()
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
        
        Assert.Equal("COBJ", candidate.CandidateType);
        Assert.Equal("cobj_WeaponNoAmmo", candidate.CandidateEditorId);
        Assert.Equal("WeaponNoAmmo", candidate.BaseWeaponEditorId);
        Assert.Null(candidate.CandidateAmmo); // Should be null since no ammo is assigned
        Assert.Equal(string.Empty, candidate.CandidateAmmoEditorId);
    }

    /// <summary>
    /// Tests error handling with invalid or broken references.
    /// Verifies that WeaponDataExtractor handles error conditions gracefully
    /// and continues processing valid records even when encountering invalid ones.
    /// </summary>
    [Fact]
    public async Task ExtractAsync_WithErrorScenario_HandlesErrorsGracefully()
    {
        // Arrange
        var gameEnv = new TestEnvironmentBuilder()
            .CreateErrorTestScenario()
            .Build();

        var loggerAdapter = NullLogger<MutagenV51EnvironmentAdapter>.Instance;
        var mutagenEnv = new MutagenV51EnvironmentAdapter(gameEnv, loggerAdapter);

        var loggerResourced = NullLogger<ResourcedMutagenEnvironment>.Instance;
        using var resourcedEnv = new ResourcedMutagenEnvironment(mutagenEnv, gameEnv, loggerResourced);

        var extractorLogger = NullLogger<WeaponDataExtractor>.Instance;
        var extractor = new WeaponDataExtractor(extractorLogger);

        var excludedPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Act & Assert - Should not throw exceptions
        var results = await extractor.ExtractAsync(resourcedEnv, excludedPlugins);

        Assert.NotNull(results);
        
        // Should find the valid COBJ record despite the invalid one
        var validCandidate = results.FirstOrDefault(r => r.CandidateEditorId == "cobj_ValidWeapon");
        Assert.NotNull(validCandidate);
        Assert.Equal("ValidWeapon", validCandidate.BaseWeaponEditorId);
        Assert.Equal("ValidAmmo", validCandidate.CandidateAmmoEditorId);
    }

    /// <summary>
    /// Tests extraction with empty environment.
    /// Verifies that WeaponDataExtractor handles empty environments gracefully
    /// and returns empty results without errors.
    /// </summary>
    [Fact]
    public async Task ExtractAsync_WithEmptyEnvironment_ReturnsEmptyResults()
    {
        // Arrange
        var gameEnv = new TestEnvironmentBuilder()
            .Build(); // Empty environment with just master file

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
        Assert.Empty(results);
    }

    /// <summary>
    /// Tests that the virtual environment properly supports LinkCache resolution.
    /// This is a foundational test to ensure the TestEnvironmentBuilder creates
    /// environments that work correctly with Mutagen's LinkCache system.
    /// </summary>
    [Fact]
    public void VirtualEnvironment_SupportsLinkCacheResolution()
    {
        // Arrange
        var gameEnv = new TestEnvironmentBuilder()
            .CreateBasicWeaponAmmoScenario()
            .Build();

        var loggerAdapter = NullLogger<MutagenV51EnvironmentAdapter>.Instance;
        var mutagenEnv = new MutagenV51EnvironmentAdapter(gameEnv, loggerAdapter);

        var loggerResourced = NullLogger<ResourcedMutagenEnvironment>.Instance;
        using var resourcedEnv = new ResourcedMutagenEnvironment(mutagenEnv, gameEnv, loggerResourced);

        // Act & Assert
        var linkCache = resourcedEnv.GetLinkCache();
        Assert.NotNull(linkCache);

        var weapons = resourcedEnv.GetWinningWeaponOverrides().ToList();
        Assert.NotEmpty(weapons);

        var cobjs = resourcedEnv.GetWinningConstructibleObjectOverrides().ToList();
        Assert.NotEmpty(cobjs);
    }
}