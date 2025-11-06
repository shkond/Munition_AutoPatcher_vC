using System;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinkCacheHelperTests
{
    public class LinkCacheHelper_AdditionalTests
    {
        // Case A: Instance-level TryResolve on the link-like object
        private class CacheA { }
        private class LinkA
        {
            public bool TryResolve(CacheA cache, out object? resolved)
            {
                resolved = 123; return true;
            }
        }

        // Case B: FormKey property + cache.Resolve(formKey)
        private class FK { }
        private class LinkB { public FK FormKey { get; set; } = new FK(); }
        private class CacheB { public object Resolve(FK key) => "RES"; }

        // Case C: FormKey property + cache.TryResolve(formKey, out)
        private class LinkC { public FK FormKey { get; set; } = new FK(); }
        private class CacheC { public bool TryResolve(FK key, out object? resolved) { resolved = 456; return true; } }

        // Case D: Fallback single-arg method that returns object
        private class LinkD { public FK FormKey { get; set; } = new FK(); }
        private class CacheD { public object Get(FK key) => "X"; }

        [Fact]
        public void TryResolve_InstanceMethod_Works()
        {
            var link = new LinkA();
            var cache = new CacheA();
            var r = MunitionAutoPatcher.Services.Implementations.LinkCacheHelper.TryResolveViaLinkCache(link, cache);
            Assert.Equal(123, r);
        }

        [Fact]
        public void TryResolve_FormKey_Resolve_Works()
        {
            var link = new LinkB();
            var cache = new CacheB();
            var r = MunitionAutoPatcher.Services.Implementations.LinkCacheHelper.TryResolveViaLinkCache(link, cache);
            Assert.Equal("RES", r);
        }

        [Fact]
        public void TryResolve_FormKey_TryResolve_Works()
        {
            var link = new LinkC();
            var cache = new CacheC();
            var r = MunitionAutoPatcher.Services.Implementations.LinkCacheHelper.TryResolveViaLinkCache(link, cache);
            Assert.Equal(456, r);
        }

        [Fact]
        public void TryResolve_Fallback_SingleArg_Works()
        {
            var link = new LinkD();
            var cache = new CacheD();
            var r = MunitionAutoPatcher.Services.Implementations.LinkCacheHelper.TryResolveViaLinkCache(link, cache);
            Assert.Equal("X", r);
        }
    }
}
