// Copyright (c) Munition AutoPatcher contributors. Licensed under the MIT License.

using IntegrationTests.Infrastructure;
using IntegrationTests.Infrastructure.Models;
using Xunit;
using Xunit.Abstractions;

namespace IntegrationTests;

/// <summary>
/// End-to-end tests that exercise MapperViewModel through the test harness,
/// producing real ESP artifacts and validating them without WPF shell involvement.
/// </summary>
public class ViewModelE2ETests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _testOutputRoot;

    public ViewModelE2ETests(ITestOutputHelper output)
    {
        _output = output;
        _testOutputRoot = Path.Combine(
            Path.GetTempPath(),
            "MunitionAutoPatcher_E2E_Tests",
            Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testOutputRoot);
    }

    public void Dispose()
    {
        // Cleanup temp directories after test run (optional - can keep for debugging)
        // Directory.Delete(_testOutputRoot, recursive: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// T007: Failing skeleton test that provisions a baseline scenario and asserts ESP path.
    /// This test is expected to FAIL until ViewModelHarness and TestServiceProvider are implemented.
    /// </summary>
    [Fact]
    public async Task BaselineScenario_GeneratesEsp_WhenViewModelExecutes()
    {
        // Arrange - Create a basic scenario definition
        var scenario = new E2EScenarioDefinition
        {
            Id = "baseline-basic-mapping",
            DisplayName = "Baseline Basic Weapon-Ammo Mapping",
            Description = "Tests basic weapon-ammunition extraction and ESP generation",
            PluginSeeds =
            [
                new PluginSeed
                {
                    Name = "TestMod.esp",
                    BuilderActionName = "CreateBasicWeaponAmmoScenario",
                    OwnsEnvironment = true
                }
            ],
            ExpectedEspName = "MunitionPatch.esp",
            ValidationProfile = new ESPValidationProfile
            {
                ProfileId = "basic-validation",
                StructuralExpectations = new StructuralExpectation
                {
                    WeaponCount = CountRange.AtLeast(1),
                    AmmoCount = CountRange.AtLeast(1),
                    CobjCount = CountRange.AtLeast(1)
                }
            },
            TimeoutSeconds = 60
        };

        // TODO: Once ViewModelHarness is implemented, replace this with actual harness execution
        // var harness = new ViewModelHarness(_testOutputRoot, _output);
        // var artifact = await harness.ExecuteScenarioAsync(scenario);

        // Assert - Placeholder assertion that will fail until harness is ready
        // This ensures the test skeleton is recognized and tracked
        var expectedEspPath = Path.Combine(_testOutputRoot, scenario.GetEffectiveOutputPath(), scenario.ExpectedEspName);
        
        _output.WriteLine($"Test output root: {_testOutputRoot}");
        _output.WriteLine($"Expected ESP path: {expectedEspPath}");
        _output.WriteLine("PLACEHOLDER: ViewModelHarness not yet implemented");

        // This assertion will FAIL - intentionally marking test as incomplete
        Assert.True(File.Exists(expectedEspPath), 
            $"ESP file should exist at {expectedEspPath}. " +
            "This test will pass once ViewModelHarness is implemented (T012).");
    }

    /// <summary>
    /// Tests that MapperViewModel status messages are captured during execution.
    /// Placeholder for ViewModel-level assertion testing.
    /// </summary>
    [Fact]
    public async Task BaselineScenario_CapturesStatusMessages_WhenViewModelExecutes()
    {
        // Arrange
        var scenario = new E2EScenarioDefinition
        {
            Id = "baseline-status-capture",
            DisplayName = "Status Message Capture Test",
            PluginSeeds =
            [
                new PluginSeed { Name = "TestMod.esp", BuilderActionName = "CreateBasicWeaponAmmoScenario" }
            ],
            ExpectedEspName = "MunitionPatch.esp",
            ValidationProfile = new ESPValidationProfile
            {
                ProfileId = "status-capture",
                StructuralExpectations = new StructuralExpectation()
            },
            ScenarioAssertions =
            [
                new ScenarioAssertion
                {
                    Target = "StatusMessage",
                    ExpectedValue = "Extraction complete",
                    Description = "ViewModel should report extraction completion"
                }
            ]
        };

        // TODO: Implement with ViewModelHarness
        _output.WriteLine("PLACEHOLDER: ViewModelHarness not yet implemented");

        // Placeholder assertion - will fail until harness captures status messages
        Assert.Fail("Status message capture not yet implemented. Requires ViewModelHarness (T012).");

        await Task.CompletedTask; // Suppress CS1998 warning
    }

    /// <summary>
    /// Tests that validation result is properly computed after ESP generation.
    /// </summary>
    [Fact]
    public async Task BaselineScenario_ComputesValidationResult_AfterEspGeneration()
    {
        // Arrange
        var scenario = new E2EScenarioDefinition
        {
            Id = "baseline-validation",
            DisplayName = "Validation Result Test",
            PluginSeeds =
            [
                new PluginSeed { Name = "TestMod.esp", BuilderActionName = "CreateBasicWeaponAmmoScenario" }
            ],
            ExpectedEspName = "MunitionPatch.esp",
            ValidationProfile = new ESPValidationProfile
            {
                ProfileId = "full-validation",
                StructuralExpectations = new StructuralExpectation
                {
                    WeaponCount = CountRange.Exact(1),
                    AmmoCount = CountRange.Exact(1),
                    CobjCount = CountRange.Exact(1)
                }
            }
        };

        // TODO: Implement with ViewModelHarness + EspFileValidator
        _output.WriteLine("PLACEHOLDER: EspFileValidator not yet implemented");

        // Placeholder assertion - will fail until validator is ready
        Assert.Fail("Validation result computation not yet implemented. Requires EspFileValidator (T011).");

        await Task.CompletedTask;
    }
}
