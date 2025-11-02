using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using MunitionAutoPatcher.Services.Interfaces;
using MunitionAutoPatcher.Services.Implementations;
using MunitionAutoPatcher.Models;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins.Order;

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
                new DummyWeaponDataExtractor()
            );

            using var cts = new CancellationTokenSource();
            cts.Cancel(); // cancel immediately

            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await extractor.ExtractCandidatesAsync(progress: null, cancellationToken: cts.Token);
            });
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
        }

        private sealed class DummyMutagenEnvironmentFactory : IMutagenEnvironmentFactory
        {
            public IResourcedMutagenEnvironment Create() => new DummyEnv();

            private sealed class DummyEnv : IResourcedMutagenEnvironment
            {
                public IEnumerable<object> GetWinningWeaponOverrides() => Array.Empty<object>();
                public IEnumerable<object> GetWinningConstructibleObjectOverrides() => Array.Empty<object>();
                public IEnumerable<(string Name, IEnumerable<object> Items)> EnumerateRecordCollections() => Array.Empty<(string, IEnumerable<object>)>();
                public object? GetLinkCache() => null;
                public Noggog.DirectoryPath? GetDataFolderPath() => null;
                public void Dispose() { }
            }
        }

        private sealed class DummyWeaponDataExtractor : IWeaponDataExtractor
        {
            public Task<List<OmodCandidate>> ExtractAsync(IResourcedMutagenEnvironment env, HashSet<string> excluded, IProgress<string>? progress = null)
                => Task.FromResult(new List<OmodCandidate>());
        }
    }
}
