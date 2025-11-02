using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Interfaces;
using MunitionAutoPatcher.Services.Implementations;
using Xunit;

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

        [Fact]
        public void ConfirmCandidatesThroughReverseMap_CancelsDuringProcessing_ThrowsOperationCanceledException()
        {
            // Arrange: prepare a candidate and a reverseMap with many slow entries so cancellation can occur while iterating
            var candidate = new OmodCandidate
            {
                BaseWeapon = new FormKey { PluginName = "mod.esp", FormId = 1 }
            };
            var results = new List<OmodCandidate> { candidate };

            var slow = new SlowRecord();
            var refs = new List<(object Record, string PropName, object PropValue)>();
            for (int i = 0; i < 500; i++) refs.Add((slow, "SlowProp", new object()));

            var reverseMap = new Dictionary<string, List<(object Record, string PropName, object PropValue)>>(StringComparer.OrdinalIgnoreCase)
            {
                { "mod.esp:00000001", refs }
            };

            var cts = new CancellationTokenSource();
            // Cancel immediately so the helper will observe the cancellation token early in the pass
            cts.Cancel();

            var detector = new NoopDetector();

            // Use reflection to invoke the private static helper
            var method = typeof(WeaponOmodExtractor).GetMethod("ConfirmCandidatesThroughReverseMap", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            // Act & Assert: invoking should result in a TargetInvocationException whose InnerException is OperationCanceledException
            var ex = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, new object[] { results, reverseMap, new HashSet<string>(), new List<object>(), null, detector, null, null, cts.Token }));
            Assert.IsType<OperationCanceledException>(ex.InnerException);
        }
    }
}
