using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Interfaces;
using MunitionAutoPatcher.Services.Implementations;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Mutagen.Bethesda.Plugins.Cache;

namespace LinkCacheHelperTests
{
    public class ConfirmReverseMapCancellationTests
    {
        private class SlowRecord
        {
            // A property getter that delays to simulate work during reflection-based scanning
            public object SlowProp { get { System.Threading.Thread.Sleep(5); return new object(); } }
            public object? FormKey { get; set; } = null;
        }

        private class NoopDetector : IAmmunitionChangeDetector
        {
            public string Name => "Noop";
            public bool DoesOmodChangeAmmo(object sourceRec, object? originalAmmoLinkObj, out object? newAmmoLinkObj)
            {
                newAmmoLinkObj = null;
                return false;
            }
        }

        private class MockMutagenAccessor : IMutagenAccessor
        {
            public ILinkResolver? GetLinkCache(IResourcedMutagenEnvironment env) => null;
            public ILinkCache? BuildConcreteLinkCache(IResourcedMutagenEnvironment env) => null;
            public IEnumerable<object> EnumerateRecordCollections(IResourcedMutagenEnvironment env, string collectionName) => Array.Empty<object>();
            public IEnumerable<object> GetWinningWeaponOverrides(IResourcedMutagenEnvironment env) => Array.Empty<object>();
            public IEnumerable<object> GetWinningConstructibleObjectOverrides(IResourcedMutagenEnvironment env) => Array.Empty<object>();
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

        [Fact]
        public async Task ReverseMapConfirmer_CancelsDuringProcessing_ThrowsOperationCanceledException()
        {
            // Arrange: prepare a candidate and a reverseMap with many slow entries so cancellation can occur while iterating
            var candidate = new OmodCandidate
            {
                BaseWeapon = new FormKey { PluginName = "mod.esp", FormId = 1 }
            };
            var candidates = new List<OmodCandidate> { candidate };

            var slow = new SlowRecord();
            var refs = new List<(object Record, string PropName, object PropValue)>();
            for (int i = 0; i < 500; i++) refs.Add((slow, "SlowProp", new object()));

            var reverseMap = new Dictionary<string, List<(object Record, string PropName, object PropValue)>>(StringComparer.OrdinalIgnoreCase)
            {
                { "mod.esp:00000001", refs }
            };

            var cts = new CancellationTokenSource();
            // Cancel immediately so the confirmer will observe the cancellation token early in the pass
            cts.Cancel();

            var detector = new NoopDetector();
            var mutagenAccessor = new MockMutagenAccessor();
            var logger = NullLogger<ReverseMapConfirmer>.Instance;
            var confirmer = new ReverseMapConfirmer(mutagenAccessor, logger, NullLoggerFactory.Instance);

            var context = new ConfirmationContext
            {
                ReverseMap = reverseMap,
                ExcludedPlugins = new HashSet<string>(),
                AllWeapons = new List<object>(),
                AmmoMap = null,
                Detector = detector,
                Resolver = null,
                LinkCache = null,
                CancellationToken = cts.Token
            };

            // Act & Assert: invoking should throw OperationCanceledException
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await confirmer.ConfirmAsync(candidates, context, cts.Token));
        }
    }
}
