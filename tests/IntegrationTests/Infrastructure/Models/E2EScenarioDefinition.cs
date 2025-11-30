// Copyright (c) Munition AutoPatcher contributors. Licensed under the MIT License.

using Mutagen.Bethesda.Fallout4;

namespace IntegrationTests.Infrastructure.Models;

/// <summary>
/// Declarative description of an E2E test scenario including inputs, expected outputs,
/// and validation rules. Stored alongside integration test assets.
/// </summary>
public sealed class E2EScenarioDefinition
{
    /// <summary>
    /// Unique scenario identifier used for folder names and logging.
    /// Must match pattern [a-z0-9_-]+.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Human-readable title for reports (max 80 chars).
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Optional markdown summary of what the scenario covers.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Ordered collection describing each plugin to materialize via TestEnvironmentBuilder.
    /// Must contain at least one entry.
    /// </summary>
    public required IReadOnlyList<PluginSeed> PluginSeeds { get; init; }

    /// <summary>
    /// Optional override for game data root; defaults to temp root from TestServiceProvider.
    /// Must be absolute if set.
    /// </summary>
    public string? GameDataRoot { get; init; }

    /// <summary>
    /// Relative subfolder inside test output root where generated ESPs/logs land.
    /// Defaults to "scenario-{Id}".
    /// </summary>
    public string? OutputRelativePath { get; init; }

    /// <summary>
    /// File name (including extension) expected from IEspPatchService.
    /// </summary>
    public required string ExpectedEspName { get; init; }

    /// <summary>
    /// Structural + binary validation instructions.
    /// </summary>
    public required ESPValidationProfile ValidationProfile { get; init; }

    /// <summary>
    /// Additional ViewModel-level assertions (e.g., status text, log markers).
    /// </summary>
    public IReadOnlyList<ScenarioAssertion>? ScenarioAssertions { get; init; }

    /// <summary>
    /// Key/value overrides passed into IConfigService.
    /// Keys must match existing config service setters.
    /// </summary>
    public IReadOnlyDictionary<string, string>? ConfigOverrides { get; init; }

    /// <summary>
    /// Optional scenario timeout in seconds. Uses CancellationTokenSource.
    /// Defaults to 300 seconds if not specified.
    /// </summary>
    public int? TimeoutSeconds { get; init; }

    /// <summary>
    /// Gets the effective output path, defaulting to "scenario-{Id}" if not specified.
    /// </summary>
    public string GetEffectiveOutputPath() => OutputRelativePath ?? $"scenario-{Id}";

    /// <summary>
    /// Gets the effective timeout, defaulting to 300 seconds.
    /// </summary>
    public int GetEffectiveTimeoutSeconds() => TimeoutSeconds ?? 300;
}

/// <summary>
/// Describes a plugin to materialize via TestEnvironmentBuilder.
/// </summary>
public sealed class PluginSeed
{
    /// <summary>
    /// Plugin filename (e.g., "TestMod.esp").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Name of the registered TestDataFactory extension method to invoke.
    /// Used for JSON serialization; the actual delegate is resolved at runtime.
    /// </summary>
    public string? BuilderActionName { get; init; }

    /// <summary>
    /// Delegate that mutates the builder; typically points to TestDataFactory extension.
    /// This is set at runtime when loading from JSON manifests.
    /// </summary>
    public Action<TestEnvironmentBuilder>? BuilderAction { get; set; }

    /// <summary>
    /// Optional path to an existing ESP to copy into test data directory before running.
    /// </summary>
    public string? BaselineCopySource { get; init; }

    /// <summary>
    /// If true, this PluginSeed creates a dedicated GameEnvironment (disposed at test end).
    /// If false, uses shared environment.
    /// </summary>
    public bool OwnsEnvironment { get; init; }
}

/// <summary>
/// ViewModel-level assertion for scenario validation.
/// </summary>
public sealed class ScenarioAssertion
{
    /// <summary>
    /// Target property or log marker to check.
    /// </summary>
    public required string Target { get; init; }

    /// <summary>
    /// Expected value or pattern to match.
    /// </summary>
    public required string ExpectedValue { get; init; }

    /// <summary>
    /// Optional description for failure messages.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Describes how to inspect generated ESPs (structural checks, ignore regions, baseline references).
/// </summary>
public sealed class ESPValidationProfile
{
    /// <summary>
    /// Identifier so multiple scenarios can reuse the same validation rules.
    /// </summary>
    public required string ProfileId { get; init; }

    /// <summary>
    /// Path to the golden ESP used for byte comparison.
    /// If empty, suite only performs structural checks.
    /// </summary>
    public string? BaselineArtifacts { get; init; }

    /// <summary>
    /// Enumerates mod-header fields to zero/skip during byte diff.
    /// Defaults to Timestamp + NextFormId.
    /// </summary>
    public IReadOnlyList<HeaderField> IgnoreHeaderFields { get; init; } =
        [HeaderField.Timestamp, HeaderField.NextFormId];

    /// <summary>
    /// Set of warning substrings that are tolerated for this scenario.
    /// </summary>
    public IReadOnlyList<string>? AllowedWarnings { get; init; }

    /// <summary>
    /// Error message patterns that trigger test failure.
    /// </summary>
    public IReadOnlyList<string>? FatalErrorPatterns { get; init; }

    /// <summary>
    /// Aggregated counts and predicates for structural validation.
    /// </summary>
    public required StructuralExpectation StructuralExpectations { get; init; }
}

/// <summary>
/// Header fields that can be ignored during byte comparison.
/// </summary>
public enum HeaderField
{
    Timestamp,
    NextFormId,
    Author,
    Description
}

/// <summary>
/// Aggregated record counts and custom predicates for ESP validation.
/// </summary>
public sealed class StructuralExpectation
{
    /// <summary>
    /// Expected WEAP records that should appear in the generated ESP.
    /// </summary>
    public CountRange? WeaponCount { get; init; }

    /// <summary>
    /// Expected AMMO records or overrides touched.
    /// </summary>
    public CountRange? AmmoCount { get; init; }

    /// <summary>
    /// Expected number of COBJ overrides written.
    /// </summary>
    public CountRange? CobjCount { get; init; }

    /// <summary>
    /// Extra inspectors (e.g., confirm that a specific form key appears).
    /// </summary>
    public IReadOnlyList<CustomCheck>? CustomChecks { get; init; }
}

/// <summary>
/// Simple min/max range for record count validation.
/// Named CountRange to avoid collision with System.Range.
/// </summary>
public readonly record struct CountRange(int Min, int Max)
{
    /// <summary>
    /// Checks if a value falls within the range (inclusive).
    /// </summary>
    public bool Contains(int value) => value >= Min && value <= Max;

    /// <summary>
    /// Creates a range expecting an exact count.
    /// </summary>
    public static CountRange Exact(int count) => new(count, count);

    /// <summary>
    /// Creates a range with at least the specified minimum.
    /// </summary>
    public static CountRange AtLeast(int min) => new(min, int.MaxValue);

    public override string ToString() => Min == Max ? $"{Min}" : $"{Min}-{Max}";
}

/// <summary>
/// Custom validation check executed against a parsed Fallout4Mod.
/// </summary>
public sealed class CustomCheck
{
    /// <summary>
    /// Human-readable label reported when failing.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Validation function receiving the parsed mod and returning pass/fail.
    /// </summary>
    public required Func<IFallout4ModDisposableGetter, CustomCheckResult> Execute { get; init; }
}

/// <summary>
/// Result of a custom check execution.
/// </summary>
public readonly record struct CustomCheckResult(bool Passed, string? ErrorMessage = null)
{
    public static CustomCheckResult Pass() => new(true);
    public static CustomCheckResult Fail(string message) => new(false, message);
}
