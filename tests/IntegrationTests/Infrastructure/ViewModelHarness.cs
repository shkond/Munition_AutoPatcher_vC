// Copyright (c) Munition AutoPatcher contributors. Licensed under the MIT License.

using IntegrationTests.Infrastructure.Models;
using MunitionAutoPatcher.Services.Implementations;
using MunitionAutoPatcher.Services.Interfaces;
using MunitionAutoPatcher.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Records;
using Xunit.Abstractions;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// T012: ViewModelHarness orchestrates E2E test scenarios by:
/// 1. Building TestEnvironmentBuilder with plugin seeds
/// 2. Resolving MapperViewModel via TestServiceProvider
/// 3. Running ViewModel commands (ExtractOmods, GenerateMappings, GenerateIni)
/// 4. Returning ScenarioRunArtifact with ESP paths and validation results
/// 
/// Per research.md:
/// - TestEnvironmentBuilder owns the GameEnvironment, disposed via await using at test boundary
/// - ViewModel receives exact production wiring via TestServiceProvider
/// - AsyncTestHarness handles timeout/cancellation coordination
/// </summary>
public sealed class ViewModelHarness : IAsyncDisposable
{
    private readonly E2EScenarioDefinition _scenario;
    private readonly ITestOutputHelper? _testOutput;
    private readonly TestServiceProvider _serviceProvider;
    private readonly TestEnvironmentBuilder _envBuilder;
    private readonly AsyncTestHarness _asyncHarness;
    private readonly ScenarioRunArtifact _artifact;
    private readonly IResourcedMutagenEnvironment _resourcedEnv;
    private readonly List<string> _statusMessages = [];
    private bool _disposed;

    private ViewModelHarness(
        E2EScenarioDefinition scenario,
        ITestOutputHelper? testOutput,
        TestServiceProvider serviceProvider,
        TestEnvironmentBuilder envBuilder,
        AsyncTestHarness asyncHarness,
        ScenarioRunArtifact artifact,
        IResourcedMutagenEnvironment resourcedEnv)
    {
        _scenario = scenario;
        _testOutput = testOutput;
        _serviceProvider = serviceProvider;
        _envBuilder = envBuilder;
        _asyncHarness = asyncHarness;
        _artifact = artifact;
        _resourcedEnv = resourcedEnv;
    }

    /// <summary>
    /// Gets the scenario run artifact containing results and diagnostics.
    /// </summary>
    public ScenarioRunArtifact Artifact => _artifact;

    /// <summary>
    /// Gets the test service provider for accessing services.
    /// </summary>
    public TestServiceProvider Services => _serviceProvider;

    /// <summary>
    /// Gets the status messages captured during execution.
    /// </summary>
    public IReadOnlyList<string> StatusMessages => _statusMessages;

    /// <summary>
    /// Gets the cancellation token from the async harness.
    /// </summary>
    public CancellationToken CancellationToken => _asyncHarness.CancellationToken;

    /// <summary>
    /// Executes the full E2E scenario: ExtractOmods → GenerateMappings → GenerateIni.
    /// </summary>
    /// <returns>The scenario run artifact with validation results.</returns>
    public async Task<ScenarioRunArtifact> ExecuteAsync()
    {
        var startTime = DateTime.UtcNow;
        _artifact.State = RunState.Initialized;

        try
        {
            // Phase 1: Run ExtractOmods command
            Log("Phase 1: Executing ExtractOmods...");
            var mapperViewModel = _serviceProvider.GetRequiredService<MapperViewModel>();
            
            await ExecuteCommandAsync(mapperViewModel.ExtractOmodsCommand);
            Log($"  ExtractOmods complete. OmodCandidates: {mapperViewModel.OmodCandidates.Count}");

            // Phase 2: Run GenerateMappings command
            Log("Phase 2: Executing GenerateMappings...");
            await ExecuteCommandAsync(mapperViewModel.GenerateMappingsCommand);
            Log($"  GenerateMappings complete. WeaponMappings: {mapperViewModel.WeaponMappings.Count}");

            // Phase 3: Run GenerateIni command (produces ESP via EspPatchService)
            Log("Phase 3: Executing GenerateIni...");
            await ExecuteCommandAsync(mapperViewModel.GenerateIniCommand);
            Log("  GenerateIni complete.");

            _artifact.State = RunState.ViewModelExecuted;

            // Phase 4: Locate generated ESP
            var expectedEspPath = Path.Combine(_artifact.TempOutputPath, _scenario.ExpectedEspName);
            if (File.Exists(expectedEspPath))
            {
                _artifact.GeneratedEspPath = expectedEspPath;
                Log($"  Generated ESP found: {expectedEspPath}");
            }
            else
            {
                Log($"  WARNING: Expected ESP not found at {expectedEspPath}");
                _artifact.ErrorMessage = $"Expected ESP not found: {expectedEspPath}";
            }

            // Phase 5: Validate ESP
            if (_artifact.GeneratedEspPath != null)
            {
                Log("Phase 4: Validating ESP...");
                var validator = new EspFileValidator();
                var validationResult = validator.Validate(_artifact.GeneratedEspPath, _scenario.ValidationProfile);
                _artifact.ValidationResult = validationResult;
                _artifact.State = RunState.EspValidated;
                Log($"  Validation: IsValid={validationResult.IsValid}, WEAP={validationResult.WeaponCount}, AMMO={validationResult.AmmoCount}, COBJ={validationResult.CobjCount}");
                
                if (validationResult.Errors.Count > 0)
                {
                    foreach (var error in validationResult.Errors)
                        Log($"    ERROR: {error}");
                }
                if (validationResult.Warnings.Count > 0)
                {
                    foreach (var warning in validationResult.Warnings)
                        Log($"    WARN: {warning}");
                }
            }

            // Collect diagnostics
            _artifact.Diagnostics.StatusMessages.AddRange(_statusMessages);
            foreach (var file in _serviceProvider.DiagnosticWriter.WrittenFiles)
            {
                _artifact.Diagnostics.AddDiagnosticOutput(file);
            }
        }
        catch (OperationCanceledException)
        {
            _artifact.State = RunState.Failed;
            _artifact.ErrorMessage = "Scenario execution was cancelled due to timeout";
            Log($"ERROR: {_artifact.ErrorMessage}");
        }
        catch (Exception ex)
        {
            _artifact.State = RunState.Failed;
            _artifact.ErrorMessage = $"Scenario execution failed: {ex.Message}";
            Log($"ERROR: {_artifact.ErrorMessage}");
        }
        finally
        {
            _artifact.Duration = DateTime.UtcNow - startTime;
        }

        return _artifact;
    }

    /// <summary>
    /// Executes a single ViewModel command with timeout enforcement.
    /// </summary>
    private async Task ExecuteCommandAsync(System.Windows.Input.ICommand command)
    {
        if (!command.CanExecute(null))
        {
            Log("  Command cannot execute (CanExecute=false)");
            return;
        }

        // AsyncRelayCommand executes asynchronously
        if (command is MunitionAutoPatcher.Commands.AsyncRelayCommand)
        {
            var result = await _asyncHarness.ExecuteWithTimeoutAsync(async ct =>
            {
                // Execute the command - it returns a Task
                command.Execute(null);
                
                // Wait for IsProcessing to become false
                var mapper = _serviceProvider.GetRequiredService<MapperViewModel>();
                while (mapper.IsProcessing)
                {
                    await Task.Delay(50, ct);
                    ct.ThrowIfCancellationRequested();
                }
            }, "ExecuteCommand");

            if (!result.IsSuccess && result.Exception != null)
            {
                throw result.Exception;
            }
        }
        else
        {
            // Synchronous command
            command.Execute(null);
        }
    }

    /// <summary>
    /// Logs a message to the test output and status messages.
    /// </summary>
    private void Log(string message)
    {
        _statusMessages.Add(message);
        _testOutput?.WriteLine(message);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await _asyncHarness.DisposeAsync();
        _serviceProvider.Dispose();
        _resourcedEnv.Dispose();
    }

    /// <summary>
    /// Builder for creating ViewModelHarness instances.
    /// </summary>
    public sealed class Builder
    {
        private E2EScenarioDefinition? _scenario;
        private ITestOutputHelper? _testOutput;
        private string? _tempRoot;
        private IResourcedMutagenEnvironment? _environment;

        /// <summary>
        /// Sets the scenario definition.
        /// </summary>
        public Builder WithScenario(E2EScenarioDefinition scenario)
        {
            _scenario = scenario;
            return this;
        }

        /// <summary>
        /// Sets the xUnit test output helper for logging.
        /// </summary>
        public Builder WithTestOutput(ITestOutputHelper output)
        {
            _testOutput = output;
            return this;
        }

        /// <summary>
        /// Sets the temp root directory for test artifacts.
        /// </summary>
        public Builder WithTempRoot(string path)
        {
            _tempRoot = path;
            return this;
        }

        /// <summary>
        /// Sets the Mutagen environment (for real plugin testing).
        /// </summary>
        public Builder WithMutagenEnvironment(IResourcedMutagenEnvironment environment)
        {
            _environment = environment;
            return this;
        }

        /// <summary>
        /// Builds the ViewModelHarness with configured options.
        /// </summary>
        public async Task<ViewModelHarness> BuildAsync()
        {
            if (_scenario == null)
                throw new InvalidOperationException("Scenario definition is required");

            // Determine temp root
            var tempRoot = _tempRoot ?? Path.Combine(
                Path.GetTempPath(),
                "MunitionAutoPatcher_E2E_Tests",
                _scenario.Id);

            // Create directories
            var dataPath = Path.Combine(tempRoot, "Data");
            var outputPath = Path.Combine(tempRoot, "Output");

            Directory.CreateDirectory(dataPath);
            Directory.CreateDirectory(outputPath);

            // Build TestEnvironmentBuilder with plugin seeds
            var envBuilder = new TestEnvironmentBuilder();
            
            foreach (var seed in _scenario.PluginSeeds)
            {
                if (seed.BuilderAction != null)
                {
                    seed.BuilderAction(envBuilder);
                }
                else
                {
                    // Default: create an empty plugin
                    envBuilder.WithPlugin(seed.Name);
                }
            }

            // Build the game environment (for compatibility with existing code)
            var gameEnv = envBuilder.Build();
            
            // Build an in-memory LinkCache that contains all test records.
            // This is critical because GameEnvironment.Typical.Builder may not properly
            // resolve records from MockFileSystem. The in-memory cache ensures all
            // test-created records (weapons, COBJs, ammo, etc.) are resolvable.
            var inMemoryLinkCache = envBuilder.BuildInMemoryLinkCache();

            // Create async harness with scenario timeout
            var timeoutSeconds = _scenario.GetEffectiveTimeoutSeconds();
            var asyncHarness = new AsyncTestHarness(timeoutSeconds, _testOutput);

            // Wrap game environment in ResourcedMutagenEnvironment with in-memory LinkCache
            var mutagenAdapter = new MutagenV51EnvironmentAdapter(gameEnv, inMemoryLinkCache);
            var resourcedEnv = _environment ?? new ResourcedMutagenEnvironment(
                mutagenAdapter,
                mutagenAdapter, // Adapter is also disposable
                NullLogger<ResourcedMutagenEnvironment>.Instance);

            // Build service provider with test overrides
            // IMPORTANT: Use WithMutagenEnvironment to inject the test environment via IMutagenEnvironmentFactory
            // This ensures WeaponOmodExtractor.ExtractCandidatesAsync() uses our test environment
            // instead of trying to auto-detect Fallout 4 installation
            var serviceProvider = TestServiceProvider.CreateBuilder()
                .WithGameDataPath(dataPath)
                .WithOutputPath(outputPath)
                .WithTempRoot(tempRoot)
                .WithScenarioId(_scenario.Id)
                .WithTestOutput(_testOutput)
                .WithMutagenEnvironment(() => 
                {
                    // Create a new adapter for each factory call
                    // The adapter wraps the same gameEnv but each call gets its own wrapper
                    // Use the in-memory LinkCache so test records are resolvable
                    var adapter = new MutagenV51EnvironmentAdapter(gameEnv, inMemoryLinkCache);
                    return new ResourcedMutagenEnvironment(
                        adapter,
                        new NoOpDisposable(), // Factory-created envs use NoOpDisposable to avoid double-dispose
                        NullLogger<ResourcedMutagenEnvironment>.Instance);
                })
                .Build();

            // Create scenario run artifact
            var artifact = new ScenarioRunArtifact
            {
                ScenarioId = _scenario.Id,
                ExecutionTimestampUtc = DateTime.UtcNow,
                TempDataPath = dataPath,
                TempOutputPath = outputPath
            };

            return new ViewModelHarness(
                _scenario,
                _testOutput,
                serviceProvider,
                envBuilder,
                asyncHarness,
                artifact,
                resourcedEnv);
        }
    }

    /// <summary>
    /// Creates a new builder for ViewModelHarness.
    /// </summary>
    public static Builder CreateBuilder() => new();
}

/// <summary>
/// Adapter that wraps IGameEnvironment to implement IMutagenEnvironment for test compatibility.
/// This is a minimal implementation for E2E tests.
/// Note: Exposes InnerGameEnvironment for MutagenAccessor.BuildConcreteLinkCache() to capture the LinkCache.
/// </summary>
file sealed class MutagenV51EnvironmentAdapter : IMutagenEnvironment, IDisposable
{
    private readonly IGameEnvironment<IFallout4Mod, IFallout4ModGetter> _gameEnv;
    private readonly ILinkCache<IFallout4Mod, IFallout4ModGetter>? _inMemoryLinkCache;

    public MutagenV51EnvironmentAdapter(
        IGameEnvironment<IFallout4Mod, IFallout4ModGetter> gameEnv,
        ILinkCache<IFallout4Mod, IFallout4ModGetter>? inMemoryLinkCache = null)
    {
        _gameEnv = gameEnv;
        _inMemoryLinkCache = inMemoryLinkCache;
    }

    /// <summary>
    /// Exposes the inner IGameEnvironment for MutagenAccessor to extract the LinkCache.
    /// This property must exist and match the naming expected by MutagenAccessor.BuildConcreteLinkCache().
    /// </summary>
    public IGameEnvironment<IFallout4Mod, IFallout4ModGetter> InnerGameEnvironment => _gameEnv;

    /// <summary>
    /// Gets the effective LinkCache (either in-memory test cache or game environment cache).
    /// </summary>
    public ILinkCache<IFallout4Mod, IFallout4ModGetter> EffectiveLinkCache 
        => _inMemoryLinkCache ?? _gameEnv.LinkCache;

    public void Dispose()
    {
        if (_gameEnv is IDisposable disposable)
            disposable.Dispose();
    }

    public IEnumerable<object> GetWinningWeaponOverrides()
    {
        return GetWinningWeaponOverridesTyped().Cast<object>();
    }

    public IEnumerable<object> GetWinningConstructibleObjectOverrides()
    {
        return GetWinningConstructibleObjectOverridesTyped().Cast<object>();
    }

    public IEnumerable<(string Name, IEnumerable<object> Items)> EnumerateRecordCollections()
    {
        yield return ("Weapons", GetWinningWeaponOverrides());
        yield return ("Ammunitions", EnumerateAmmunitions().Cast<object>());
        yield return ("ConstructibleObjects", GetWinningConstructibleObjectOverrides());
        yield return ("ObjectModifications", GetWinningObjectModificationsTyped().Cast<object>());
    }

    public IEnumerable<(string Name, IEnumerable<IMajorRecordGetter> Items)> EnumerateRecordCollectionsTyped()
    {
        yield return ("Weapons", GetWinningWeaponOverridesTyped().Cast<IMajorRecordGetter>());
        yield return ("Ammunitions", EnumerateAmmunitions().Cast<IMajorRecordGetter>());
        yield return ("ConstructibleObjects", GetWinningConstructibleObjectOverridesTyped().Cast<IMajorRecordGetter>());
        yield return ("ObjectModifications", GetWinningObjectModificationsTyped().Cast<IMajorRecordGetter>());
    }

    public ILinkResolver? GetLinkCache()
    {
        // Return a production LinkResolver so that MutagenAccessor.BuildConcreteLinkCache()
        // can extract the underlying ILinkCache via LinkResolver.LinkCache property.
        // This is critical for AttachPointConfirmer and other services that need direct LinkCache access.
        // Use EffectiveLinkCache which prefers the in-memory test cache over game environment cache.
        return new LinkResolver(EffectiveLinkCache, NullLogger<LinkResolver>.Instance);
    }

    public Noggog.DirectoryPath? GetDataFolderPath()
    {
        return _gameEnv.DataFolderPath;
    }

    public IEnumerable<IWeaponGetter> GetWinningWeaponOverridesTyped()
    {
        foreach (var listing in _gameEnv.LoadOrder.PriorityOrder)
        {
            if (listing.Mod is null) continue;
            foreach (var weap in listing.Mod.Weapons)
                yield return weap;
        }
    }

    public IEnumerable<IConstructibleObjectGetter> GetWinningConstructibleObjectOverridesTyped()
    {
        foreach (var listing in _gameEnv.LoadOrder.PriorityOrder)
        {
            if (listing.Mod is null) continue;
            foreach (var cobj in listing.Mod.ConstructibleObjects)
                yield return cobj;
        }
    }

    public IEnumerable<IObjectModificationGetter> GetWinningObjectModificationsTyped()
    {
        foreach (var listing in _gameEnv.LoadOrder.PriorityOrder)
        {
            if (listing.Mod is null) continue;
            // ObjectModifications contains IAObjectModificationGetter (abstract base), 
            // filter to IObjectModificationGetter (concrete weapon mods)
            foreach (var omod in listing.Mod.ObjectModifications.OfType<IObjectModificationGetter>())
                yield return omod;
        }
    }

    private IEnumerable<IAmmunitionGetter> EnumerateAmmunitions()
    {
        foreach (var listing in _gameEnv.LoadOrder.PriorityOrder)
        {
            if (listing.Mod is null) continue;
            foreach (var ammo in listing.Mod.Ammunitions)
                yield return ammo;
        }
    }
}

/// <summary>
/// Wraps Mutagen's ILinkCache for test use with type-safe resolution.
/// </summary>
file sealed class LinkResolverAdapter : ILinkResolver
{
    public ILinkCache? LinkCache { get; }

    public LinkResolverAdapter(ILinkCache? cache)
    {
        LinkCache = cache;
    }

    public bool TryResolve(object linkLike, out object? result)
    {
        result = null;
        if (LinkCache == null || linkLike == null) return false;

        try
        {
            // Extract FormKey from linkLike object
            Mutagen.Bethesda.Plugins.FormKey? fk = linkLike switch
            {
                Mutagen.Bethesda.Plugins.FormKey directFk => directFk,
                MunitionAutoPatcher.Models.FormKey customFk => MunitionAutoPatcher.Services.Implementations.FormKeyNormalizer.ToMutagenFormKey(customFk),
                _ => TryExtractFormKey(linkLike)
            };

            if (fk.HasValue)
            {
                result = ResolveFormKeyTyped(fk.Value);
                return result != null;
            }
        }
        catch
        {
            // Ignore resolution failures
        }
        return false;
    }

    public bool TryResolve<TGetter>(object linkLike, out TGetter? result) where TGetter : class?
    {
        result = null;
        if (TryResolve(linkLike, out var resolved) && resolved is TGetter typed)
        {
            result = typed;
            return true;
        }
        return false;
    }

    public object? ResolveByKey(MunitionAutoPatcher.Models.FormKey key)
    {
        if (LinkCache == null) return null;

        try
        {
            var mutagenKey = MunitionAutoPatcher.Services.Implementations.FormKeyNormalizer.ToMutagenFormKey(key);
            if (mutagenKey.HasValue)
            {
                return ResolveFormKeyTyped(mutagenKey.Value);
            }
        }
        catch
        {
            // Ignore resolution failures
        }
        return null;
    }

    private Mutagen.Bethesda.Plugins.FormKey? TryExtractFormKey(object linkLike)
    {
        var prop = linkLike.GetType().GetProperty("FormKey");
        var raw = prop?.GetValue(linkLike);
        return raw as Mutagen.Bethesda.Plugins.FormKey?;
    }

    private object? ResolveFormKeyTyped(Mutagen.Bethesda.Plugins.FormKey fk)
    {
        if (LinkCache == null) return null;

        // Try typed paths first (same as production LinkResolver)
        if (LinkCache.TryResolve<IObjectModificationGetter>(fk, out var omod) && omod != null)
            return omod;
        if (LinkCache.TryResolve<IConstructibleObjectGetter>(fk, out var cobj) && cobj != null)
            return cobj;
        if (LinkCache.TryResolve<IWeaponGetter>(fk, out var weap) && weap != null)
            return weap;
        if (LinkCache.TryResolve<IAmmunitionGetter>(fk, out var ammo) && ammo != null)
            return ammo;
        if (LinkCache.TryResolve<IMajorRecordGetter>(fk, out var any) && any != null)
            return any;

        return null;
    }
}

/// <summary>
/// A no-op disposable for use when we don't need actual disposal.
/// </summary>
file sealed class NoOpDisposable : IDisposable
{
    public void Dispose() { }
}
