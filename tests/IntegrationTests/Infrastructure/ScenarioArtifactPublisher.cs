// Copyright (c) Munition AutoPatcher contributors. Licensed under the MIT License.

using System.Text.Json;
using IntegrationTests.Infrastructure.Models;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// T022: Publishes test artifacts (ESP, diagnostics, metadata) to CI-accessible location.
/// Used for artifact archival and baseline comparison in GitHub Actions.
/// </summary>
public class ScenarioArtifactPublisher
{
    private readonly string _outputRoot;
    private readonly List<PublishResult> _publishedArtifacts = [];

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ScenarioArtifactPublisher(string outputRoot)
    {
        _outputRoot = outputRoot ?? throw new ArgumentNullException(nameof(outputRoot));
    }

    /// <summary>
    /// Publishes a single artifact to the output directory.
    /// </summary>
    /// <param name="artifact">The artifact to publish.</param>
    /// <returns>Result of the publish operation.</returns>
    public PublishResult Publish(ScenarioRunArtifact artifact)
    {
        if (artifact == null)
        {
            return new PublishResult(false, "Artifact is null");
        }

        try
        {
            // Create output directory structure
            Directory.CreateDirectory(_outputRoot);
            var scenarioDir = Path.Combine(_outputRoot, artifact.ScenarioId);
            Directory.CreateDirectory(scenarioDir);

            var result = new PublishResult(true)
            {
                ScenarioId = artifact.ScenarioId,
                OutputPath = scenarioDir
            };

            // Write metadata
            WriteMetadata(artifact, scenarioDir);

            // Copy ESP if present
            if (!string.IsNullOrEmpty(artifact.GeneratedEspPath) && File.Exists(artifact.GeneratedEspPath))
            {
                var espFileName = Path.GetFileName(artifact.GeneratedEspPath);
                var destPath = Path.Combine(scenarioDir, espFileName);
                File.Copy(artifact.GeneratedEspPath, destPath, overwrite: true);
                result.EspCopied = true;
            }
            else
            {
                result.Notes = "No ESP to copy";
            }

            // Write diagnostics
            WriteDiagnostics(artifact.Diagnostics, scenarioDir);

            // Copy additional artifacts from temp paths if they exist
            CopyAdditionalArtifacts(artifact, scenarioDir);

            _publishedArtifacts.Add(result);
            return result;
        }
        catch (Exception ex)
        {
            return new PublishResult(false, ex.Message)
            {
                ScenarioId = artifact.ScenarioId
            };
        }
    }

    /// <summary>
    /// Publishes multiple artifacts and generates a summary report.
    /// </summary>
    /// <param name="artifacts">The artifacts to publish.</param>
    /// <returns>Results of all publish operations.</returns>
    public IReadOnlyList<PublishResult> PublishAll(IEnumerable<ScenarioRunArtifact> artifacts)
    {
        var results = new List<PublishResult>();

        foreach (var artifact in artifacts)
        {
            results.Add(Publish(artifact));
        }

        // Generate summary report
        WriteSummaryReport(results);

        return results;
    }

    private void WriteMetadata(ScenarioRunArtifact artifact, string scenarioDir)
    {
        var metadata = new
        {
            scenarioId = artifact.ScenarioId,
            state = artifact.State.ToString(),
            duration = artifact.Duration.TotalSeconds,
            generatedEspPath = artifact.GeneratedEspPath,
            errorMessage = artifact.ErrorMessage,
            timestamp = DateTime.UtcNow.ToString("O"),
            validationResult = artifact.ValidationResult != null ? new
            {
                isValid = artifact.ValidationResult.IsValid,
                errorsCount = artifact.ValidationResult.Errors.Count,
                warningsCount = artifact.ValidationResult.Warnings.Count
            } : null
        };

        var metadataPath = Path.Combine(scenarioDir, "metadata.json");
        File.WriteAllText(metadataPath, JsonSerializer.Serialize(metadata, s_jsonOptions));
    }

    private void WriteDiagnostics(DiagnosticBundle diagnostics, string scenarioDir)
    {
        if (diagnostics == null) return;

        var diagnosticsData = new
        {
            statusMessages = diagnostics.StatusMessages,
            logFilePaths = diagnostics.LogFilePaths,
            diagnosticWriterOutputs = diagnostics.DiagnosticWriterOutputs,
            validationReports = diagnostics.ValidationReports,
            ciArtifactRoot = diagnostics.CIArtifactRoot
        };

        var diagnosticsPath = Path.Combine(scenarioDir, "diagnostics.json");
        File.WriteAllText(diagnosticsPath, JsonSerializer.Serialize(diagnosticsData, s_jsonOptions));
    }

    private void CopyAdditionalArtifacts(ScenarioRunArtifact artifact, string scenarioDir)
    {
        // Copy CSV files from temp output if present
        if (!string.IsNullOrEmpty(artifact.TempOutputPath) && Directory.Exists(artifact.TempOutputPath))
        {
            var csvFiles = Directory.GetFiles(artifact.TempOutputPath, "*.csv");
            foreach (var csv in csvFiles)
            {
                var destPath = Path.Combine(scenarioDir, Path.GetFileName(csv));
                File.Copy(csv, destPath, overwrite: true);
            }

            // Copy any log files
            var logFiles = Directory.GetFiles(artifact.TempOutputPath, "*.log");
            foreach (var log in logFiles)
            {
                var destPath = Path.Combine(scenarioDir, Path.GetFileName(log));
                File.Copy(log, destPath, overwrite: true);
            }
        }
    }

    private void WriteSummaryReport(List<PublishResult> results)
    {
        var summary = new
        {
            timestamp = DateTime.UtcNow.ToString("O"),
            totalScenarios = results.Count,
            successfulPublishes = results.Count(r => r.Success),
            failedPublishes = results.Count(r => !r.Success),
            scenarios = results.Select(r => new
            {
                scenarioId = r.ScenarioId,
                success = r.Success,
                espCopied = r.EspCopied,
                notes = r.Notes,
                outputPath = r.OutputPath
            }).ToList()
        };

        var summaryPath = Path.Combine(_outputRoot, "summary.json");
        File.WriteAllText(summaryPath, JsonSerializer.Serialize(summary, s_jsonOptions));
    }
}

/// <summary>
/// Result of a publish operation.
/// </summary>
public class PublishResult
{
    public bool Success { get; }
    public string? ErrorMessage { get; }
    public string? ScenarioId { get; set; }
    public string? OutputPath { get; set; }
    public bool EspCopied { get; set; }
    public string? Notes { get; set; }

    public PublishResult(bool success, string? errorMessage = null)
    {
        Success = success;
        ErrorMessage = errorMessage;
    }
}
