// Copyright (c) Munition AutoPatcher contributors. Licensed under the MIT License.

using IntegrationTests.Infrastructure.Models;
using Xunit;
using Xunit.Abstractions;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// T014: Tests for ScenarioCatalog covering JSON schema validation, 
/// duplicate detection, and config override enforcement.
/// </summary>
public class ScenarioCatalogTests
{
    private readonly ITestOutputHelper _output;
    private readonly string _testRoot;

    public ScenarioCatalogTests(ITestOutputHelper output)
    {
        _output = output;
        _testRoot = Path.Combine(Path.GetTempPath(), "ScenarioCatalogTests", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testRoot);
    }

    [Fact]
    public void LoadScenarios_ReturnsEmpty_WhenDirectoryDoesNotExist()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testRoot, "nonexistent");
        var catalog = new ScenarioCatalog(nonExistentPath);

        // Act
        var scenarios = catalog.LoadScenarios();

        // Assert
        Assert.Empty(scenarios);
    }

    [Fact]
    public void LoadScenarios_ReturnsEmpty_WhenNoJsonFiles()
    {
        // Arrange
        var emptyDir = Path.Combine(_testRoot, "empty");
        Directory.CreateDirectory(emptyDir);
        var catalog = new ScenarioCatalog(emptyDir);

        // Act
        var scenarios = catalog.LoadScenarios();

        // Assert
        Assert.Empty(scenarios);
    }

    [Fact]
    public void LoadScenarios_LoadsValidScenario_WhenJsonIsValid()
    {
        // Arrange
        var scenarioDir = Path.Combine(_testRoot, "valid");
        Directory.CreateDirectory(scenarioDir);
        
        var json = """
        {
            "id": "test-scenario",
            "displayName": "Test Scenario",
            "pluginSeeds": [
                { "name": "TestMod.esp", "builderActionName": "CreateBasicWeapon" }
            ],
            "expectedEspName": "Output.esp",
            "validationProfile": {
                "profileId": "basic",
                "structuralExpectations": {}
            }
        }
        """;
        File.WriteAllText(Path.Combine(scenarioDir, "test-scenario.json"), json);
        
        var catalog = new ScenarioCatalog(scenarioDir);

        // Act
        var scenarios = catalog.LoadScenarios().ToList();

        // Assert
        Assert.Single(scenarios);
        Assert.Equal("test-scenario", scenarios[0].Id);
        Assert.Equal("Test Scenario", scenarios[0].DisplayName);
    }

    [Fact]
    public void LoadScenarios_DetectsDuplicateIds_AndReportsError()
    {
        // Arrange
        var scenarioDir = Path.Combine(_testRoot, "duplicates");
        Directory.CreateDirectory(scenarioDir);
        
        var json1 = """
        {
            "id": "duplicate-id",
            "displayName": "First Scenario",
            "pluginSeeds": [{ "name": "Mod1.esp" }],
            "expectedEspName": "Output.esp",
            "validationProfile": { "profileId": "basic", "structuralExpectations": {} }
        }
        """;
        var json2 = """
        {
            "id": "duplicate-id",
            "displayName": "Second Scenario",
            "pluginSeeds": [{ "name": "Mod2.esp" }],
            "expectedEspName": "Output.esp",
            "validationProfile": { "profileId": "basic", "structuralExpectations": {} }
        }
        """;
        File.WriteAllText(Path.Combine(scenarioDir, "scenario1.json"), json1);
        File.WriteAllText(Path.Combine(scenarioDir, "scenario2.json"), json2);
        
        var catalog = new ScenarioCatalog(scenarioDir);

        // Act
        var scenarios = catalog.LoadScenarios().ToList();
        var errors = catalog.GetLoadErrors();

        // Assert
        Assert.Single(scenarios); // Only first one loaded
        Assert.Contains(errors, e => e.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LoadScenarios_SkipsInvalidJson_AndRecordsError()
    {
        // Arrange
        var scenarioDir = Path.Combine(_testRoot, "invalid");
        Directory.CreateDirectory(scenarioDir);
        
        var invalidJson = "{ invalid json }";
        File.WriteAllText(Path.Combine(scenarioDir, "bad.json"), invalidJson);
        
        var catalog = new ScenarioCatalog(scenarioDir);

        // Act
        var scenarios = catalog.LoadScenarios().ToList();
        var errors = catalog.GetLoadErrors();

        // Assert
        Assert.Empty(scenarios);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void LoadScenarios_AppliesConfigOverrides_WhenPresent()
    {
        // Arrange
        var scenarioDir = Path.Combine(_testRoot, "overrides");
        Directory.CreateDirectory(scenarioDir);
        
        var json = """
        {
            "id": "override-test",
            "displayName": "Config Override Test",
            "pluginSeeds": [{ "name": "TestMod.esp" }],
            "expectedEspName": "Output.esp",
            "configOverrides": {
                "OutputPath": "custom/output",
                "EnableDiagnostics": "true"
            },
            "validationProfile": { "profileId": "basic", "structuralExpectations": {} }
        }
        """;
        File.WriteAllText(Path.Combine(scenarioDir, "override-test.json"), json);
        
        var catalog = new ScenarioCatalog(scenarioDir);

        // Act
        var scenarios = catalog.LoadScenarios().ToList();

        // Assert
        Assert.Single(scenarios);
        Assert.NotNull(scenarios[0].ConfigOverrides);
        Assert.Equal("custom/output", scenarios[0].ConfigOverrides!["OutputPath"]);
        Assert.Equal("true", scenarios[0].ConfigOverrides!["EnableDiagnostics"]);
    }

    [Fact]
    public void LoadScenarios_ValidatesIdPattern_RejectsInvalidCharacters()
    {
        // Arrange
        var scenarioDir = Path.Combine(_testRoot, "badid");
        Directory.CreateDirectory(scenarioDir);
        
        var json = """
        {
            "id": "Invalid ID With Spaces!",
            "displayName": "Invalid ID Test",
            "pluginSeeds": [{ "name": "TestMod.esp" }],
            "expectedEspName": "Output.esp",
            "validationProfile": { "profileId": "basic", "structuralExpectations": {} }
        }
        """;
        File.WriteAllText(Path.Combine(scenarioDir, "badid.json"), json);
        
        var catalog = new ScenarioCatalog(scenarioDir);

        // Act
        var scenarios = catalog.LoadScenarios().ToList();
        var errors = catalog.GetLoadErrors();

        // Assert
        Assert.Empty(scenarios);
        Assert.Contains(errors, e => e.Contains("id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LoadScenarios_ResolvesBuilderActions_WhenRegistered()
    {
        // Arrange
        var scenarioDir = Path.Combine(_testRoot, "actions");
        Directory.CreateDirectory(scenarioDir);
        
        var json = """
        {
            "id": "action-test",
            "displayName": "Builder Action Test",
            "pluginSeeds": [{ "name": "TestMod.esp", "builderActionName": "CreateBasicWeapon" }],
            "expectedEspName": "Output.esp",
            "validationProfile": { "profileId": "basic", "structuralExpectations": {} }
        }
        """;
        File.WriteAllText(Path.Combine(scenarioDir, "action-test.json"), json);
        
        var catalog = new ScenarioCatalog(scenarioDir);
        
        // Register a builder action
        var actionCalled = false;
        catalog.RegisterBuilderAction("CreateBasicWeapon", _ => actionCalled = true);

        // Act
        var scenarios = catalog.LoadScenarios().ToList();

        // Assert
        Assert.Single(scenarios);
        Assert.NotNull(scenarios[0].PluginSeeds[0].BuilderAction);
        
        // Verify action was wired correctly
        scenarios[0].PluginSeeds[0].BuilderAction!(null!);
        Assert.True(actionCalled);
    }

    [Fact]
    public void GetScenarioById_ReturnsNull_WhenNotFound()
    {
        // Arrange
        var catalog = new ScenarioCatalog(Path.Combine(_testRoot, "empty2"));

        // Act
        var scenario = catalog.GetScenarioById("nonexistent");

        // Assert
        Assert.Null(scenario);
    }

    [Fact]
    public void GetScenarioById_ReturnsScenario_WhenFound()
    {
        // Arrange
        var scenarioDir = Path.Combine(_testRoot, "findby");
        Directory.CreateDirectory(scenarioDir);
        
        var json = """
        {
            "id": "findme",
            "displayName": "Find Me",
            "pluginSeeds": [{ "name": "TestMod.esp" }],
            "expectedEspName": "Output.esp",
            "validationProfile": { "profileId": "basic", "structuralExpectations": {} }
        }
        """;
        File.WriteAllText(Path.Combine(scenarioDir, "findme.json"), json);
        
        var catalog = new ScenarioCatalog(scenarioDir);
        catalog.LoadScenarios(); // Must load first

        // Act
        var scenario = catalog.GetScenarioById("findme");

        // Assert
        Assert.NotNull(scenario);
        Assert.Equal("findme", scenario.Id);
    }
}
