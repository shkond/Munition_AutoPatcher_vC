// Copyright (c) Munition AutoPatcher contributors. Licensed under the MIT License.

using IntegrationTests.Infrastructure;
using IntegrationTests.Infrastructure.Models;
using Xunit;
using Xunit.Abstractions;

namespace IntegrationTests.Tests;

/// <summary>
/// T020: Tests for ScenarioArtifactPublisher - publishes test artifacts to CI-accessible location.
/// </summary>
public class ScenarioArtifactPublisherTests
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempRoot;

    public ScenarioArtifactPublisherTests(ITestOutputHelper output)
    {
        _output = output;
        _tempRoot = Path.Combine(
            Path.GetTempPath(),
            "MunitionAutoPatcher_Publisher_Tests",
            Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempRoot);
    }

    /// <summary>
    /// Tests that publisher creates output directory structure correctly.
    /// </summary>
    [Fact]
    public void Publish_CreatesOutputDirectory_WhenNotExists()
    {
        // Arrange
        var outputPath = Path.Combine(_tempRoot, "nonexistent", "artifacts");
        var publisher = new ScenarioArtifactPublisher(outputPath);
        var artifact = CreateTestArtifact("test-scenario-1");

        // Act
        var result = publisher.Publish(artifact);

        // Assert
        Assert.True(result.Success);
        Assert.True(Directory.Exists(outputPath));
        _output.WriteLine($"Output directory created: {outputPath}");
    }

    /// <summary>
    /// Tests that publisher creates scenario-specific subdirectory.
    /// </summary>
    [Fact]
    public void Publish_CreatesScenarioSubdirectory_WithScenarioId()
    {
        // Arrange
        var outputPath = Path.Combine(_tempRoot, "artifacts");
        var publisher = new ScenarioArtifactPublisher(outputPath);
        var artifact = CreateTestArtifact("test-scenario-subdirectory");

        // Act
        var result = publisher.Publish(artifact);

        // Assert
        Assert.True(result.Success);
        var expectedSubdir = Path.Combine(outputPath, artifact.ScenarioId);
        Assert.True(Directory.Exists(expectedSubdir));
        _output.WriteLine($"Scenario subdirectory: {expectedSubdir}");
    }

    /// <summary>
    /// Tests that publisher writes artifact metadata file.
    /// </summary>
    [Fact]
    public void Publish_WritesMetadataFile_WithArtifactDetails()
    {
        // Arrange
        var outputPath = Path.Combine(_tempRoot, "artifacts");
        var publisher = new ScenarioArtifactPublisher(outputPath);
        var artifact = CreateTestArtifact("test-metadata");

        // Act
        var result = publisher.Publish(artifact);

        // Assert
        Assert.True(result.Success);
        var metadataPath = Path.Combine(outputPath, artifact.ScenarioId, "metadata.json");
        Assert.True(File.Exists(metadataPath));
        
        var content = File.ReadAllText(metadataPath);
        Assert.Contains(artifact.ScenarioId, content);
        _output.WriteLine($"Metadata file: {metadataPath}");
    }

    /// <summary>
    /// Tests that publisher copies ESP file if exists.
    /// </summary>
    [Fact]
    public void Publish_CopiesEspFile_WhenPresent()
    {
        // Arrange
        var outputPath = Path.Combine(_tempRoot, "artifacts");
        var espSourceDir = Path.Combine(_tempRoot, "source");
        Directory.CreateDirectory(espSourceDir);
        
        var espSourcePath = Path.Combine(espSourceDir, "test.esp");
        File.WriteAllText(espSourcePath, "ESP content placeholder");

        var publisher = new ScenarioArtifactPublisher(outputPath);
        var artifact = CreateTestArtifact("test-esp-copy");
        artifact.GeneratedEspPath = espSourcePath;

        // Act
        var result = publisher.Publish(artifact);

        // Assert
        Assert.True(result.Success);
        var copiedEspPath = Path.Combine(outputPath, artifact.ScenarioId, "test.esp");
        Assert.True(File.Exists(copiedEspPath));
        _output.WriteLine($"ESP copied to: {copiedEspPath}");
    }

    /// <summary>
    /// Tests that publisher handles missing ESP gracefully.
    /// </summary>
    [Fact]
    public void Publish_HandlesGracefully_WhenEspNotExists()
    {
        // Arrange
        var outputPath = Path.Combine(_tempRoot, "artifacts");
        var publisher = new ScenarioArtifactPublisher(outputPath);
        var artifact = CreateTestArtifact("test-no-esp");
        artifact.GeneratedEspPath = null;

        // Act
        var result = publisher.Publish(artifact);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Notes);
        Assert.Contains("No ESP", result.Notes);
        _output.WriteLine($"Handled gracefully: {result.Notes}");
    }

    /// <summary>
    /// Tests that publisher copies diagnostics bundle if present.
    /// </summary>
    [Fact]
    public void Publish_CopiesDiagnostics_WhenPresent()
    {
        // Arrange
        var outputPath = Path.Combine(_tempRoot, "artifacts");
        var publisher = new ScenarioArtifactPublisher(outputPath);
        var artifact = CreateTestArtifact("test-diagnostics");
        artifact.Diagnostics.StatusMessages.Add("Test diagnostic message");

        // Act
        var result = publisher.Publish(artifact);

        // Assert
        Assert.True(result.Success);
        var diagnosticsPath = Path.Combine(outputPath, artifact.ScenarioId, "diagnostics.json");
        Assert.True(File.Exists(diagnosticsPath));
        
        var content = File.ReadAllText(diagnosticsPath);
        Assert.Contains("Test diagnostic message", content);
        _output.WriteLine($"Diagnostics written: {diagnosticsPath}");
    }

    /// <summary>
    /// Tests that publisher generates summary report.
    /// </summary>
    [Fact]
    public void PublishAll_GeneratesSummaryReport_ForMultipleArtifacts()
    {
        // Arrange
        var outputPath = Path.Combine(_tempRoot, "artifacts");
        var publisher = new ScenarioArtifactPublisher(outputPath);
        var artifacts = new[]
        {
            CreateTestArtifact("scenario-1"),
            CreateTestArtifact("scenario-2"),
            CreateTestArtifact("scenario-3")
        };

        // Act
        var results = publisher.PublishAll(artifacts);

        // Assert
        Assert.All(results, r => Assert.True(r.Success));
        var summaryPath = Path.Combine(outputPath, "summary.json");
        Assert.True(File.Exists(summaryPath));
        
        var content = File.ReadAllText(summaryPath);
        Assert.Contains("scenario-1", content);
        Assert.Contains("scenario-2", content);
        Assert.Contains("scenario-3", content);
        _output.WriteLine($"Summary report: {summaryPath}");
    }

    /// <summary>
    /// Tests that publisher respects artifact state.
    /// </summary>
    [Fact]
    public void Publish_IncludesStateInMetadata_ForDifferentStates()
    {
        // Arrange
        var outputPath = Path.Combine(_tempRoot, "artifacts");
        var publisher = new ScenarioArtifactPublisher(outputPath);
        var artifact = CreateTestArtifact("test-state");
        artifact.State = RunState.EspValidated;

        // Act
        var result = publisher.Publish(artifact);

        // Assert
        Assert.True(result.Success);
        var metadataPath = Path.Combine(outputPath, artifact.ScenarioId, "metadata.json");
        var content = File.ReadAllText(metadataPath);
        Assert.Contains("EspValidated", content);
        _output.WriteLine($"State included: EspValidated");
    }

    private static ScenarioRunArtifact CreateTestArtifact(string scenarioId)
    {
        return new ScenarioRunArtifact
        {
            ScenarioId = scenarioId,
            ExecutionTimestampUtc = DateTime.UtcNow,
            State = RunState.Initialized,
            Duration = TimeSpan.FromSeconds(1.5),
            TempDataPath = Path.GetTempPath(),
            TempOutputPath = Path.GetTempPath(),
            Diagnostics = new DiagnosticBundle()
        };
    }
}
