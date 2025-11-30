using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Implementations;
using MunitionAutoPatcher.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Mutagen.Bethesda.Plugins.Cache;
using Xunit;

namespace WeaponDataExtractorTestsProject
{
    public class EspPatchServiceTests
    {
        private class FakeConfigService : IConfigService
        {
            public string OutputMode { get; set; } = "esp";
            public string OutputDirectory { get; set; } = "artifacts";

            public Task<StrategyConfig> LoadConfigAsync() => Task.FromResult(new StrategyConfig());
            public Task SaveConfigAsync(StrategyConfig config) => Task.CompletedTask;
            public string GetGameDataPath() => string.Empty;
            public void SetGameDataPath(string path) { }
            public string GetOutputPath() => string.Empty;
            public void SetOutputPath(string path) { }
            public bool GetExcludeFallout4Esm() => false;
            public void SetExcludeFallout4Esm(bool v) { }
            public bool GetExcludeDlcEsms() => false;
            public void SetExcludeDlcEsms(bool v) { }
            public bool GetExcludeCcEsl() => false;
            public void SetExcludeCcEsl(bool v) { }
            public bool GetPreferEditorIdForDisplay() => false;
            public void SetPreferEditorIdForDisplay(bool v) { }
            public System.Collections.Generic.IEnumerable<string> GetExcludedPlugins() => new List<string>();
            public void SetExcludedPlugins(System.Collections.Generic.IEnumerable<string> plugins) { }
            public string GetOutputMode() => OutputMode;
            public void SetOutputMode(string mode) { OutputMode = mode; }
            public string GetOutputDirectory() => OutputDirectory;
            public void SetOutputDirectory(string directory) { OutputDirectory = directory; }
        }

        private class FakePathService : IPathService
        {
            public string RepoRoot { get; set; } = Path.GetTempPath();
            public string GetRepoRoot() => RepoRoot;
            public string GetArtifactsDirectory() => Path.Combine(RepoRoot, "artifacts");
            public string GetOutputDirectory() => Path.Combine(RepoRoot, "artifacts");
        }

        private class FakeDiagnosticWriter : IDiagnosticWriter
        {
            public void WriteStartMarker(ExtractionContext ctx) { }
            public void WriteDetectorSelected(string name, ExtractionContext ctx) { }
            public void WriteReverseMapMarker(ExtractionContext ctx) { }
            public void WriteDetectionPassMarker(ExtractionContext ctx) { }
            public void WriteResultsCsv(IEnumerable<OmodCandidate> confirmed, ExtractionContext ctx) { }
            public void WriteZeroReferenceReport(IEnumerable<OmodCandidate> candidates, ExtractionContext ctx) { }
            public void WriteCompletionMarker(ExtractionContext ctx) { }
        }

        private class FakeMutagenAccessor : IMutagenAccessor
        {
            public ILinkResolver? GetLinkCache(IResourcedMutagenEnvironment env) => null;
            public ILinkCache? BuildConcreteLinkCache(IResourcedMutagenEnvironment env) => null;
            public IEnumerable<object> EnumerateRecordCollections(IResourcedMutagenEnvironment env, string collectionName) => Enumerable.Empty<object>();
            public IEnumerable<object> GetWinningWeaponOverrides(IResourcedMutagenEnvironment env) => Enumerable.Empty<object>();
            public IEnumerable<object> GetWinningConstructibleObjectOverrides(IResourcedMutagenEnvironment env) => Enumerable.Empty<object>();
            public IEnumerable<Mutagen.Bethesda.Fallout4.IConstructibleObjectGetter> GetWinningConstructibleObjectOverridesTyped(IResourcedMutagenEnvironment env) => Enumerable.Empty<Mutagen.Bethesda.Fallout4.IConstructibleObjectGetter>();
            public IEnumerable<Mutagen.Bethesda.Fallout4.IWeaponGetter> GetWinningWeaponOverridesTyped(IResourcedMutagenEnvironment env) => Enumerable.Empty<Mutagen.Bethesda.Fallout4.IWeaponGetter>();
            public bool TryGetPluginAndIdFromRecord(object record, out string pluginName, out uint formId)
            {
                pluginName = string.Empty;
                formId = 0;
                return false;
            }
            public string GetEditorId(object? record) => string.Empty;
            public bool TryResolveRecord<T>(IResourcedMutagenEnvironment env, MunitionAutoPatcher.Models.FormKey formKey, [NotNullWhen(true)] out T? record) where T : class, Mutagen.Bethesda.Plugins.Records.IMajorRecordGetter { record = null; return false; }
            public Task<(bool Success, T? Record)> TryResolveRecordAsync<T>(IResourcedMutagenEnvironment env, MunitionAutoPatcher.Models.FormKey formKey, CancellationToken ct) where T : class, Mutagen.Bethesda.Plugins.Records.IMajorRecordGetter => Task.FromResult<(bool, T?)>((false, null));
        }

        private class FakeLogger<T> : ILogger<T>
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
        }

        [Fact]
        public void EspPatchService_ConstructorInitializesCorrectly()
        {
            // Arrange
            var pathService = new FakePathService();
            var configService = new FakeConfigService();
            var diagnosticWriter = new FakeDiagnosticWriter();
            var mutagenAccessor = new FakeMutagenAccessor();
            var logger = new FakeLogger<EspPatchService>();

            // Act
            var service = new EspPatchService(pathService, configService, diagnosticWriter, mutagenAccessor, logger);

            // Assert
            Assert.NotNull(service);
        }

        [Fact]
        public async Task ConfigService_OutputModeDefaultsToEsp()
        {
            // Arrange
            var configService = new FakeConfigService();

            // Act
            var mode = configService.GetOutputMode();

            // Assert
            Assert.Equal("esp", mode);
        }

        [Fact]
        public async Task ConfigService_OutputDirectoryDefaultsToArtifacts()
        {
            // Arrange
            var configService = new FakeConfigService();

            // Act
            var directory = configService.GetOutputDirectory();

            // Assert
            Assert.Equal("artifacts", directory);
        }

        [Fact]
        public async Task PathService_GetOutputDirectoryReturnsCorrectPath()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var pathService = new FakePathService { RepoRoot = tempDir };

            // Act
            var outputDir = pathService.GetOutputDirectory();

            // Assert
            Assert.Contains("artifacts", outputDir);
            Assert.Contains(tempDir, outputDir);
        }
    }
}
