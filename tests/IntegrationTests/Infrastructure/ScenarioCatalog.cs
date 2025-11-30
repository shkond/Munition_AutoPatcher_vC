// Copyright (c) Munition AutoPatcher contributors. Licensed under the MIT License.

using System.Text.Json;
using System.Text.RegularExpressions;
using IntegrationTests.Infrastructure.Models;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// T015: Loads scenario manifests from Scenarios/*.json, materializes 
/// E2EScenarioDefinition objects, and exposes them to the harness.
/// </summary>
public sealed partial class ScenarioCatalog
{
    private readonly string _scenariosDirectory;
    private readonly Dictionary<string, E2EScenarioDefinition> _scenarios = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _loadErrors = [];
    private readonly Dictionary<string, Action<TestEnvironmentBuilder>> _builderActions = new(StringComparer.OrdinalIgnoreCase);
    private bool _loaded;

    // Pattern for valid scenario IDs: lowercase letters, numbers, hyphens, underscores
    [GeneratedRegex(@"^[a-z0-9_-]+$", RegexOptions.Compiled)]
    private static partial Regex IdPatternRegex();

    /// <summary>
    /// Creates a new ScenarioCatalog that will load from the specified directory.
    /// </summary>
    /// <param name="scenariosDirectory">Path to the Scenarios directory.</param>
    public ScenarioCatalog(string scenariosDirectory)
    {
        _scenariosDirectory = scenariosDirectory;
    }

    /// <summary>
    /// Gets the default scenarios directory relative to the test assembly.
    /// </summary>
    public static string GetDefaultScenariosDirectory()
    {
        var assemblyLocation = typeof(ScenarioCatalog).Assembly.Location;
        var assemblyDir = Path.GetDirectoryName(assemblyLocation) ?? ".";
        return Path.Combine(assemblyDir, "Scenarios");
    }

    /// <summary>
    /// Registers a builder action that can be referenced by name in scenario manifests.
    /// </summary>
    /// <param name="name">The action name to register.</param>
    /// <param name="action">The builder action delegate.</param>
    public void RegisterBuilderAction(string name, Action<TestEnvironmentBuilder> action)
    {
        _builderActions[name] = action;
    }

    /// <summary>
    /// Registers all standard builder actions from TestDataFactoryScenarioExtensions.
    /// </summary>
    public void RegisterStandardBuilderActions()
    {
        TestDataFactoryScenarioExtensions.RegisterAllActions(this);
    }

    /// <summary>
    /// Loads all scenario definitions from the scenarios directory.
    /// </summary>
    /// <returns>Enumerable of loaded scenarios.</returns>
    public IEnumerable<E2EScenarioDefinition> LoadScenarios()
    {
        if (_loaded)
        {
            return _scenarios.Values;
        }

        _scenarios.Clear();
        _loadErrors.Clear();

        if (!Directory.Exists(_scenariosDirectory))
        {
            _loaded = true;
            return _scenarios.Values;
        }

        var jsonFiles = Directory.GetFiles(_scenariosDirectory, "*.json", SearchOption.TopDirectoryOnly);

        foreach (var jsonFile in jsonFiles)
        {
            try
            {
                var json = File.ReadAllText(jsonFile);
                var scenario = ScenarioManifestSerializer.Deserialize(json);
                
                if (scenario == null)
                {
                    _loadErrors.Add($"Failed to deserialize scenario from {Path.GetFileName(jsonFile)}: null result");
                    continue;
                }

                // Validate ID pattern
                if (!IdPatternRegex().IsMatch(scenario.Id))
                {
                    _loadErrors.Add($"Invalid scenario id '{scenario.Id}' in {Path.GetFileName(jsonFile)}: must match pattern [a-z0-9_-]+");
                    continue;
                }

                // Check for duplicate IDs
                if (_scenarios.ContainsKey(scenario.Id))
                {
                    _loadErrors.Add($"Duplicate scenario id '{scenario.Id}' found in {Path.GetFileName(jsonFile)}");
                    continue;
                }

                // Resolve builder actions
                ResolveBuilderActions(scenario);

                _scenarios[scenario.Id] = scenario;
            }
            catch (JsonException ex)
            {
                _loadErrors.Add($"JSON parse error in {Path.GetFileName(jsonFile)}: {ex.Message}");
            }
            catch (Exception ex)
            {
                _loadErrors.Add($"Error loading {Path.GetFileName(jsonFile)}: {ex.Message}");
            }
        }

        _loaded = true;
        return _scenarios.Values;
    }

    /// <summary>
    /// Gets a scenario by its ID. Must call LoadScenarios first.
    /// </summary>
    /// <param name="id">The scenario ID to find.</param>
    /// <returns>The scenario definition, or null if not found.</returns>
    public E2EScenarioDefinition? GetScenarioById(string id)
    {
        if (!_loaded)
        {
            LoadScenarios();
        }

        return _scenarios.GetValueOrDefault(id);
    }

    /// <summary>
    /// Gets any errors that occurred during scenario loading.
    /// </summary>
    /// <returns>List of error messages.</returns>
    public IReadOnlyList<string> GetLoadErrors() => _loadErrors;

    /// <summary>
    /// Gets the count of successfully loaded scenarios.
    /// </summary>
    public int Count => _scenarios.Count;

    /// <summary>
    /// Resolves builder action names to actual delegates for each plugin seed.
    /// </summary>
    private void ResolveBuilderActions(E2EScenarioDefinition scenario)
    {
        foreach (var seed in scenario.PluginSeeds)
        {
            if (!string.IsNullOrEmpty(seed.BuilderActionName) && seed.BuilderAction == null)
            {
                if (_builderActions.TryGetValue(seed.BuilderActionName, out var action))
                {
                    seed.BuilderAction = action;
                }
                else
                {
                    _loadErrors.Add($"Scenario '{scenario.Id}': Unknown builder action '{seed.BuilderActionName}' for plugin '{seed.Name}'");
                }
            }
        }
    }
}
