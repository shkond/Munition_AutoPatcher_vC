// Copyright (c) Munition AutoPatcher contributors. Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Implementations;
using MunitionAutoPatcher.Services.Interfaces;
using MunitionAutoPatcher.ViewModels;
using Xunit.Abstractions;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// T009: Test service provider that mirrors App.xaml.cs DI registrations
/// while swapping test-safe implementations for IConfigService, IPathService, and IDiagnosticWriter.
/// </summary>
public sealed class TestServiceProvider : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly TestConfigService _configService;
    private readonly TestPathService _pathService;
    private readonly TestDiagnosticWriter _diagnosticWriter;

    private TestServiceProvider(
        ServiceProvider provider,
        TestConfigService configService,
        TestPathService pathService,
        TestDiagnosticWriter diagnosticWriter)
    {
        _provider = provider;
        _configService = configService;
        _pathService = pathService;
        _diagnosticWriter = diagnosticWriter;
    }

    /// <summary>
    /// Gets the underlying service provider for resolving services.
    /// </summary>
    public IServiceProvider Services => _provider;

    /// <summary>
    /// Gets the test config service for pre-populating game/output paths.
    /// </summary>
    public TestConfigService ConfigService => _configService;

    /// <summary>
    /// Gets the test path service for isolated temp directories.
    /// </summary>
    public TestPathService PathService => _pathService;

    /// <summary>
    /// Gets the test diagnostic writer for capturing diagnostics.
    /// </summary>
    public TestDiagnosticWriter DiagnosticWriter => _diagnosticWriter;

    /// <summary>
    /// Resolves a service from the container.
    /// </summary>
    public T GetRequiredService<T>() where T : notnull => _provider.GetRequiredService<T>();

    /// <summary>
    /// Resolves a service from the container or returns null if not found.
    /// </summary>
    public T? GetService<T>() => _provider.GetService<T>();

    public void Dispose() => _provider.Dispose();

    /// <summary>
    /// Builder for creating TestServiceProvider instances with customizable options.
    /// </summary>
    public sealed class Builder
    {
        private string _gameDataPath = @"C:\Games\Fallout4\Data";
        private string _outputPath = string.Empty;
        private string _tempRoot = string.Empty;
        private string _scenarioId = "default";
        private ITestOutputHelper? _testOutput;
        private ILoggerFactory? _loggerFactory;
        private Action<IServiceCollection>? _configureServices;

        /// <summary>
        /// Sets the game data path for IConfigService.
        /// </summary>
        public Builder WithGameDataPath(string path)
        {
            _gameDataPath = path;
            return this;
        }

        /// <summary>
        /// Sets the output path for IConfigService.
        /// </summary>
        public Builder WithOutputPath(string path)
        {
            _outputPath = path;
            return this;
        }

        /// <summary>
        /// Sets the temp root directory for IPathService.
        /// If not set, uses Path.GetTempPath() + scenario ID.
        /// </summary>
        public Builder WithTempRoot(string path)
        {
            _tempRoot = path;
            return this;
        }

        /// <summary>
        /// Sets the scenario ID for path isolation.
        /// </summary>
        public Builder WithScenarioId(string id)
        {
            _scenarioId = id;
            return this;
        }

        /// <summary>
        /// Enables xUnit test output logging.
        /// </summary>
        public Builder WithTestOutput(ITestOutputHelper output)
        {
            _testOutput = output;
            return this;
        }

        /// <summary>
        /// Sets a custom logger factory (overrides test output).
        /// </summary>
        public Builder WithLoggerFactory(ILoggerFactory factory)
        {
            _loggerFactory = factory;
            return this;
        }

        /// <summary>
        /// Allows additional service configuration.
        /// </summary>
        public Builder ConfigureServices(Action<IServiceCollection> configure)
        {
            _configureServices = configure;
            return this;
        }

        /// <summary>
        /// Builds the TestServiceProvider with configured options.
        /// </summary>
        public TestServiceProvider Build()
        {
            // Determine temp root
            var tempRoot = string.IsNullOrEmpty(_tempRoot)
                ? Path.Combine(Path.GetTempPath(), "MunitionAutoPatcher_E2E_Tests", _scenarioId)
                : _tempRoot;

            // Create test directories
            var dataPath = Path.Combine(tempRoot, "Data");
            var outputDir = string.IsNullOrEmpty(_outputPath)
                ? Path.Combine(tempRoot, "Output")
                : _outputPath;
            var artifactsDir = Path.Combine(tempRoot, "Artifacts");
            var diagnosticsDir = Path.Combine(tempRoot, "Diagnostics");

            Directory.CreateDirectory(dataPath);
            Directory.CreateDirectory(outputDir);
            Directory.CreateDirectory(artifactsDir);
            Directory.CreateDirectory(diagnosticsDir);

            // Create test service instances
            var configService = new TestConfigService(_gameDataPath, outputDir);
            var pathService = new TestPathService(tempRoot, artifactsDir, outputDir);
            var diagnosticWriter = new TestDiagnosticWriter(diagnosticsDir);

            // Determine logger factory
            var loggerFactory = _loggerFactory ?? (_testOutput != null
                ? new XunitLoggerFactory(_testOutput)
                : NullLoggerFactory.Instance);

            // Build service collection mirroring App.xaml.cs
            var services = new ServiceCollection();

            // Logging
            services.AddSingleton(loggerFactory);
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

            // Test service overrides
            services.AddSingleton<IConfigService>(configService);
            services.AddSingleton<IPathService>(pathService);
            services.AddSingleton<IDiagnosticWriter>(diagnosticWriter);

            // Core services (from App.xaml.cs)
            services.AddSingleton<ILoadOrderService, LoadOrderService>();
            services.AddSingleton<IWeaponsService, WeaponsService>();
            services.AddSingleton<IMutagenEnvironmentFactory, MutagenEnvironmentFactory>();

            // Mutagen environment - will be set externally via test harness
            // For now, register as null - tests must configure this
            services.AddSingleton<IResourcedMutagenEnvironment>(sp =>
            {
                throw new InvalidOperationException(
                    "IResourcedMutagenEnvironment must be provided by the test harness. " +
                    "Use ConfigureServices() to register a test environment.");
            });

            services.AddSingleton<IOmodPropertyAdapter, MutagenV51OmodPropertyAdapter>();
            services.AddSingleton<IAmmunitionChangeDetector, MutagenV51Detector>();

            // Extraction infrastructure
            services.AddSingleton<IMutagenAccessor, MutagenAccessor>();

            // Candidate providers
            services.AddSingleton<ICandidateProvider, CobjCandidateProvider>();
            services.AddSingleton<ICandidateProvider, ReverseReferenceCandidateProvider>();

            // Candidate confirmers
            services.AddSingleton<ICandidateConfirmer, ReverseMapConfirmer>();
            services.AddSingleton<ICandidateConfirmer, AttachPointConfirmer>();

            services.AddSingleton<IWeaponOmodExtractor, WeaponOmodExtractor>();
            services.AddTransient<IWeaponDataExtractor, WeaponDataExtractor>();
            services.AddSingleton<IRobCoIniGenerator, RobCoIniGenerator>();
            services.AddSingleton<IEspPatchService, EspPatchService>();
            services.AddSingleton<IOrchestrator, OrchestratorService>();

            // ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<MapperViewModel>();

            // Allow additional customization
            _configureServices?.Invoke(services);

            var provider = services.BuildServiceProvider();
            return new TestServiceProvider(provider, configService, pathService, diagnosticWriter);
        }
    }

    /// <summary>
    /// Creates a new builder for TestServiceProvider.
    /// </summary>
    public static Builder CreateBuilder() => new();
}

/// <summary>
/// Test implementation of IConfigService with in-memory configuration.
/// </summary>
public sealed class TestConfigService : IConfigService
{
    private string _gameDataPath;
    private string _outputPath;
    private bool _excludeFallout4Esm;
    private bool _excludeDlcEsms;
    private bool _excludeCcEsl;
    private bool _preferEditorIdForDisplay = true;
    private IEnumerable<string> _excludedPlugins = [];
    private string _outputMode = "ini";
    private string _outputDirectory;

    public TestConfigService(string gameDataPath, string outputPath)
    {
        _gameDataPath = gameDataPath;
        _outputPath = outputPath;
        _outputDirectory = outputPath;
    }

    public Task<StrategyConfig> LoadConfigAsync() => Task.FromResult(new StrategyConfig());
    public Task SaveConfigAsync(StrategyConfig config) => Task.CompletedTask;

    public string GetGameDataPath() => _gameDataPath;
    public void SetGameDataPath(string path) => _gameDataPath = path;

    public string GetOutputPath() => _outputPath;
    public void SetOutputPath(string path) => _outputPath = path;

    public bool GetExcludeFallout4Esm() => _excludeFallout4Esm;
    public void SetExcludeFallout4Esm(bool v) => _excludeFallout4Esm = v;

    public bool GetExcludeDlcEsms() => _excludeDlcEsms;
    public void SetExcludeDlcEsms(bool v) => _excludeDlcEsms = v;

    public bool GetExcludeCcEsl() => _excludeCcEsl;
    public void SetExcludeCcEsl(bool v) => _excludeCcEsl = v;

    public bool GetPreferEditorIdForDisplay() => _preferEditorIdForDisplay;
    public void SetPreferEditorIdForDisplay(bool v) => _preferEditorIdForDisplay = v;

    public IEnumerable<string> GetExcludedPlugins() => _excludedPlugins;
    public void SetExcludedPlugins(IEnumerable<string> plugins) => _excludedPlugins = plugins;

    public string GetOutputMode() => _outputMode;
    public void SetOutputMode(string mode) => _outputMode = mode;

    public string GetOutputDirectory() => _outputDirectory;
    public void SetOutputDirectory(string directory) => _outputDirectory = directory;
}

/// <summary>
/// Test implementation of IPathService with isolated temp directories.
/// </summary>
public sealed class TestPathService : IPathService
{
    private readonly string _repoRoot;
    private readonly string _artifactsDirectory;
    private readonly string _outputDirectory;

    public TestPathService(string repoRoot, string artifactsDirectory, string outputDirectory)
    {
        _repoRoot = repoRoot;
        _artifactsDirectory = artifactsDirectory;
        _outputDirectory = outputDirectory;
    }

    public string GetRepoRoot() => _repoRoot;
    public string GetArtifactsDirectory() => _artifactsDirectory;
    public string GetOutputDirectory() => _outputDirectory;
}

/// <summary>
/// Test implementation of IDiagnosticWriter that captures outputs to a test directory.
/// </summary>
public sealed class TestDiagnosticWriter : IDiagnosticWriter
{
    private readonly string _outputDirectory;
    private readonly List<string> _writtenFiles = [];

    public TestDiagnosticWriter(string outputDirectory)
    {
        _outputDirectory = outputDirectory;
        Directory.CreateDirectory(_outputDirectory);
    }

    /// <summary>
    /// Gets the list of files written by this writer.
    /// </summary>
    public IReadOnlyList<string> WrittenFiles => _writtenFiles;

    public void WriteStartMarker(ExtractionContext ctx)
    {
        var path = Path.Combine(_outputDirectory, "start.marker");
        File.WriteAllText(path, $"Started: {DateTime.Now:O}");
        _writtenFiles.Add(path);
    }

    public void WriteDetectorSelected(string name, ExtractionContext ctx)
    {
        var path = Path.Combine(_outputDirectory, "detector.marker");
        File.WriteAllText(path, $"Detector: {name}");
        _writtenFiles.Add(path);
    }

    public void WriteReverseMapMarker(ExtractionContext ctx)
    {
        var path = Path.Combine(_outputDirectory, "reversemap.marker");
        File.WriteAllText(path, $"ReverseMap built: {DateTime.Now:O}");
        _writtenFiles.Add(path);
    }

    public void WriteDetectionPassMarker(ExtractionContext ctx)
    {
        var path = Path.Combine(_outputDirectory, "detection.marker");
        File.WriteAllText(path, $"Detection pass complete: {DateTime.Now:O}");
        _writtenFiles.Add(path);
    }

    public void WriteResultsCsv(IEnumerable<OmodCandidate> confirmed, ExtractionContext ctx)
    {
        var path = Path.Combine(_outputDirectory, "results.csv");
        var lines = new List<string> { "EditorId,BaseWeapon,Ammo,Source" };
        foreach (var c in confirmed)
        {
            lines.Add($"{c.CandidateEditorId},{c.BaseWeaponEditorId},{c.CandidateAmmoEditorId},{c.SourcePlugin}");
        }
        File.WriteAllLines(path, lines);
        _writtenFiles.Add(path);
    }

    public void WriteZeroReferenceReport(IEnumerable<OmodCandidate> candidates, ExtractionContext ctx)
    {
        var path = Path.Combine(_outputDirectory, "zero_reference.txt");
        var lines = candidates.Select(c => $"{c.CandidateEditorId}: {c.BaseWeaponEditorId}");
        File.WriteAllLines(path, lines);
        _writtenFiles.Add(path);
    }

    public void WriteCompletionMarker(ExtractionContext ctx)
    {
        var path = Path.Combine(_outputDirectory, "complete.marker");
        File.WriteAllText(path, $"Completed: {DateTime.Now:O}");
        _writtenFiles.Add(path);
    }
}

/// <summary>
/// Logger factory that bridges to xUnit ITestOutputHelper.
/// </summary>
public sealed class XunitLoggerFactory : ILoggerFactory
{
    private readonly ITestOutputHelper _output;

    public XunitLoggerFactory(ITestOutputHelper output)
    {
        _output = output;
    }

    public ILogger CreateLogger(string categoryName) => new XunitLogger(_output, categoryName);

    public void AddProvider(ILoggerProvider provider) { }

    public void Dispose() { }
}

/// <summary>
/// Logger that writes to xUnit ITestOutputHelper.
/// </summary>
public sealed class XunitLogger : ILogger
{
    private readonly ITestOutputHelper _output;
    private readonly string _categoryName;

    public XunitLogger(ITestOutputHelper output, string categoryName)
    {
        _output = output;
        _categoryName = categoryName;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        try
        {
            var message = formatter(state, exception);
            var shortCategory = _categoryName.Contains('.')
                ? _categoryName[((_categoryName.LastIndexOf('.') + 1))..]
                : _categoryName;
            _output.WriteLine($"[{logLevel}] {shortCategory}: {message}");
            if (exception != null)
            {
                _output.WriteLine($"  Exception: {exception.GetType().Name}: {exception.Message}");
            }
        }
        catch
        {
            // Swallow any xUnit output errors
        }
    }
}
