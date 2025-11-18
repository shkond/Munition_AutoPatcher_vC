using System;
using System.Collections.Generic;
using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Implementations;
using Xunit;

namespace LinkCacheHelperTests
{
    public class LinkResolverTests
    {
        private class ResolvedDummy { public string Name { get; set; } = string.Empty; }
        private class AmmoGetter { public string EditorID { get; set; } = "ammo"; }
        private class ResolvedByKey { public string Edit { get; set; } = "bykey"; }

        private class FakeLinkCacheWithTryResolve
        {
            public int Calls = 0;
            public bool TryResolve(object linkLike, out object? result)
            {
                Calls++;
                result = new ResolvedDummy { Name = "r" + Calls };
                return true;
            }
        }

        private class FakeLinkCacheWithTypedTryResolve
        {
            public int Calls = 0;
            public bool TryResolve(object linkLike, out object? result)
            {
                Calls++;
                result = new AmmoGetter { EditorID = "ammo" + Calls };
                return true;
            }
        }

        private class FakeLinkCacheWithResolve
        {
            public int Calls = 0;
            public object Resolve(FormKey key)
            {
                Calls++;
                return new ResolvedByKey { Edit = $"k{Calls}" };
            }
        }

        [Fact]
        public void TryResolve_CachesResultAcrossCalls()
        {
            var linkLike = new object();
            var linkCache = new FakeLinkCacheWithTryResolve();
            var resolver = new LinkResolver(linkCache, Microsoft.Extensions.Logging.Abstractions.NullLogger<LinkResolver>.Instance);

            Assert.True(resolver.TryResolve(linkLike, out var first));
            Assert.NotNull(first);
            Assert.Equal(1, linkCache.Calls);

            // Second call should hit cache and not increment underlying calls
            Assert.True(resolver.TryResolve(linkLike, out var second));
            Assert.NotNull(second);
            Assert.Equal(1, linkCache.Calls);
            // cached object should be same reference returned previously
            Assert.Equal(first.GetType(), second.GetType());
        }

        [Fact]
        public void TryResolve_Generic_ReturnsTypedGetter()
        {
            var linkLike = new object();
            var linkCache = new FakeLinkCacheWithTypedTryResolve();
            var resolver = new LinkResolver(linkCache, Microsoft.Extensions.Logging.Abstractions.NullLogger<LinkResolver>.Instance);

            Assert.True(resolver.TryResolve<AmmoGetter>(linkLike, out var ammo));
            Assert.NotNull(ammo);
            Assert.Equal("ammo1", ammo!.EditorID);

            // subsequent call returns cached typed value
            Assert.True(resolver.TryResolve<AmmoGetter>(linkLike, out var ammo2));
            Assert.NotNull(ammo2);
            Assert.Equal(1, linkCache.Calls);
        }

        [Fact]
        public void ResolveByKey_UsesResolveAndCaches()
        {
            var fk = new FormKey { PluginName = "m.esp", FormId = 0x10 };
            var linkCache = new FakeLinkCacheWithResolve();
            var resolver = new LinkResolver(linkCache, Microsoft.Extensions.Logging.Abstractions.NullLogger<LinkResolver>.Instance);

            var r1 = resolver.ResolveByKey(fk);
            Assert.NotNull(r1);
            Assert.Equal(1, linkCache.Calls);

            // second call should be cached
            var r2 = resolver.ResolveByKey(fk);
            Assert.NotNull(r2);
            Assert.Equal(1, linkCache.Calls);
        }
    }
}
