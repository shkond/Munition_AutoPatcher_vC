using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using MunitionAutoPatcher.Services.Interfaces;
using MunitionAutoPatcher.Services.Implementations;
using Microsoft.Extensions.Logging.Abstractions;
using MunitionAutoPatcher.Models;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Plugins.Order;
using Mutagen.Bethesda.Plugins.Cache;

namespace LinkCacheHelperTests
{
    public class WeaponOmodExtractorCancellationTests
    {
        [Fact]
        public async Task ExtractCandidatesAsync_Cancellation_ThrowsOperationCanceled()
        {
            var extractor = new WeaponOmodExtractor(
                new DummyLoadOrderService(),
                new DummyConfigService(),
                new DummyMutagenEnvironmentFactory(),
                new DummyDiagnosticWriter(),
                Array.Empty<ICandidateProvider>(),
                new[] { new DummyCandidateConfirmer() },
                new DummyMutagenAccessor(),
                new DummyPathService(),
                NullLogger<WeaponOmodExtractor>.Instance,
                NullLoggerFactory.Instance,
                new DummyAmmunitionChangeDetector()
            );

            using var cts = new CancellationTokenSource();
            cts.Cancel(); // cancel immediately

            // Current implementation swallows OperationCanceledException and returns an empty list.
            var result = await extractor.ExtractCandidatesAsync(progress: null, cancellationToken: cts.Token);
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        private sealed class DummyLoadOrderService : ILoadOrderService
        {
            public string GetGameDataPath() => string.Empty;
            public Task<ILoadOrder<IModListing<IFallout4ModGetter>>?> GetLoadOrderAsync() => Task.FromResult<ILoadOrder<IModListing<IFallout4ModGetter>>?>(null);
            public Task<bool> ValidateLoadOrderAsync() => Task.FromResult(true);
        }

        private sealed class DummyConfigService : IConfigService
        {
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
            public IEnumerable<string> GetExcludedPlugins() => Array.Empty<string>();
            public void SetExcludedPlugins(IEnumerable<string> plugins) { }
            public Task<StrategyConfig> LoadConfigAsync() => Task.FromResult(new StrategyConfig());
            public Task SaveConfigAsync(StrategyConfig config) => Task.CompletedTask;
            public string GetOutputMode() => string.Empty;
            public void SetOutputMode(string mode) { }
            public string GetOutputDirectory() => string.Empty;
            public void SetOutputDirectory(string directory) { }
        }

        private sealed class DummyMutagenEnvironmentFactory : IMutagenEnvironmentFactory
        {
            public IResourcedMutagenEnvironment Create() => new DummyEnv();

            private sealed class DummyEnv : IResourcedMutagenEnvironment
            {
                public IEnumerable<object> GetWinningWeaponOverrides() => Array.Empty<object>();
                public IEnumerable<object> GetWinningConstructibleObjectOverrides() => Array.Empty<object>();
                public IEnumerable<(string Name, IEnumerable<object> Items)> EnumerateRecordCollections() => Array.Empty<(string, IEnumerable<object>)>();
                public MunitionAutoPatcher.Services.Interfaces.ILinkResolver? GetLinkCache() => null;
                public Noggog.DirectoryPath? GetDataFolderPath() => null;
                public void Dispose() { }
                    // Typed accessors
                    public IEnumerable<IWeaponGetter> GetWinningWeaponOverridesTyped() => System.Linq.Enumerable.Empty<IWeaponGetter>();
                    public IEnumerable<IConstructibleObjectGetter> GetWinningConstructibleObjectOverridesTyped() => System.Linq.Enumerable.Empty<IConstructibleObjectGetter>();
                    public IEnumerable<IObjectModificationGetter> GetWinningObjectModificationsTyped() => System.Linq.Enumerable.Empty<IObjectModificationGetter>();
                    public IEnumerable<(string Name, IEnumerable<IMajorRecordGetter> Items)> EnumerateRecordCollectionsTyped() => System.Linq.Enumerable.Empty<(string, IEnumerable<IMajorRecordGetter>)>();
            }
        }

        private sealed class DummyWeaponDataExtractor : IWeaponDataExtractor
        {
            public Task<List<OmodCandidate>> ExtractAsync(IResourcedMutagenEnvironment env, HashSet<string> excluded, IProgress<string>? progress = null)
                => Task.FromResult(new List<OmodCandidate>());
        }

        // New test doubles for updated constructor
        private sealed class DummyDiagnosticWriter : IDiagnosticWriter
        {
            public void WriteCompletionMarker(ExtractionContext ctx) { }
            public void WriteDetectionPassMarker(ExtractionContext ctx) { }
            public void WriteDetectorSelected(string name, ExtractionContext ctx) { }
            public void WriteReverseMapMarker(ExtractionContext ctx) { }
            public void WriteResultsCsv(IEnumerable<OmodCandidate> confirmed, ExtractionContext ctx) { }
            public void WriteStartMarker(ExtractionContext ctx) { }
            public void WriteZeroReferenceReport(IEnumerable<OmodCandidate> candidates, ExtractionContext ctx) { }
        }

        private sealed class DummyCandidateConfirmer : ICandidateConfirmer
        {
            public Task ConfirmAsync(IEnumerable<OmodCandidate> candidates, ConfirmationContext context, CancellationToken cancellationToken) => Task.CompletedTask;
        }

        private sealed class DummyMutagenAccessor : IMutagenAccessor
        {
            public ILinkResolver? GetLinkCache(IResourcedMutagenEnvironment env) => null;
            public ILinkCache? BuildConcreteLinkCache(IResourcedMutagenEnvironment env) => null;
            public IEnumerable<object> EnumerateRecordCollections(IResourcedMutagenEnvironment env, string collectionName) => Array.Empty<object>();
            public IEnumerable<object> GetWinningConstructibleObjectOverrides(IResourcedMutagenEnvironment env) => Array.Empty<object>();
            public IEnumerable<object> GetWinningWeaponOverrides(IResourcedMutagenEnvironment env) => Array.Empty<object>();
            public IEnumerable<Mutagen.Bethesda.Fallout4.IConstructibleObjectGetter> GetWinningConstructibleObjectOverridesTyped(IResourcedMutagenEnvironment env) => Array.Empty<Mutagen.Bethesda.Fallout4.IConstructibleObjectGetter>();
            public IEnumerable<Mutagen.Bethesda.Fallout4.IWeaponGetter> GetWinningWeaponOverridesTyped(IResourcedMutagenEnvironment env) => Array.Empty<Mutagen.Bethesda.Fallout4.IWeaponGetter>();
            public bool TryGetPluginAndIdFromRecord(object record, out string pluginName, out uint formId) { pluginName = string.Empty; formId = 0; return false; }
            public string GetEditorId(object? record) => string.Empty;
            public bool TryResolveRecord<T>(IResourcedMutagenEnvironment env, MunitionAutoPatcher.Models.FormKey formKey, [NotNullWhen(true)] out T? record) where T : class, IMajorRecordGetter { record = null; return false; }
            public Task<(bool Success, T? Record)> TryResolveRecordAsync<T>(IResourcedMutagenEnvironment env, MunitionAutoPatcher.Models.FormKey formKey, CancellationToken ct) where T : class, IMajorRecordGetter => Task.FromResult<(bool, T?)>((false, null));
            
            // New Weapon API methods
            public string? GetWeaponName(Mutagen.Bethesda.Fallout4.IWeaponGetter weapon) => null;
            public string? GetWeaponDescription(Mutagen.Bethesda.Fallout4.IWeaponGetter weapon) => null;
            public float GetWeaponBaseDamage(Mutagen.Bethesda.Fallout4.IWeaponGetter weapon) => 0f;
            public float GetWeaponFireRate(Mutagen.Bethesda.Fallout4.IWeaponGetter weapon) => 0f;
            public object? GetWeaponAmmoLink(Mutagen.Bethesda.Fallout4.IWeaponGetter weapon) => null;
            
            // New FormKey/Property API methods
            public bool TryGetFormKey(object? record, out Mutagen.Bethesda.Plugins.FormKey? formKey) { formKey = null; return false; }
            public bool TryGetPropertyValue<TValue>(object? obj, string propertyName, out TValue? value) { value = default; return false; }
        }

        private sealed class DummyPathService : IPathService
        {
            public string GetArtifactsDirectory() => string.Empty;
            public string GetRepoRoot() => string.Empty;
            public string GetOutputDirectory() => string.Empty;
        }

        private sealed class DummyAmmunitionChangeDetector : IAmmunitionChangeDetector
        {
            public string Name => "Dummy";

            public bool DoesOmodChangeAmmo(object omod, object? originalAmmoLink, out object? newAmmoLink)
            {
                newAmmoLink = null;
                return false;
            }
        }
    }
}
