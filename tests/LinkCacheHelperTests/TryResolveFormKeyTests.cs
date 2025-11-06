using System;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinkCacheHelperTests
{
    public class TryResolveFormKeyTests
    {
        // Case 1: Resolve(string) returns non-null — should return that result.
        private class FakeLinkCacheResolve
        {
            public object Resolve(string key) => (object)$"resolved:{key}";
        }

        // Case 2: Resolve(string) returns null, but TryResolve(formKey, out object) returns true and sets out value.
        private class FK { public string Mod => "modx"; public uint ID => 0x1C8; }
        private class FakeLinkCacheTryResolve
        {
            public bool TryResolve(FK fk, out object? resolved)
            {
                resolved = 456;
                return true;
            }
            public object? Resolve(string key) => null;
        }

        // Case 3: Generic TryResolve<T> is invoked — create a cache with generic TryResolve
        private class GenericFormKey<T> { public T Value { get; set; } = default!; }
        private class FakeLinkCacheGeneric
        {
            public bool TryResolve<T>(GenericFormKey<T> fk, out object? resolved)
            {
                resolved = $"generic:{typeof(T).Name}";
                return true;
            }
        }

        [Fact]
        public void ResolveString_ReturnsResolveStringResult()
        {
            // When providing a string link-like, single-arg fallback should invoke Resolve(string)
            var linkLike = "editorOrIdentifier";
            var cache = new FakeLinkCacheResolve();

            var res = MunitionAutoPatcher.Services.Implementations.LinkCacheHelper.TryResolveViaLinkCache(linkLike, cache);
            Assert.NotNull(res);
            Assert.IsType<string>(res);
            Assert.StartsWith("resolved:", (string)res!);
        }

        [Fact]
        public void ResolveString_Null_Then_TryResolve_FormKey_Out_Works()
        {
            var fk = new FK();
            var linkLike = new { FormKey = fk };
            var cache = new FakeLinkCacheTryResolve();

            var res = MunitionAutoPatcher.Services.Implementations.LinkCacheHelper.TryResolveViaLinkCache(linkLike, cache);
            Assert.NotNull(res);
            Assert.Equal(456, res);
        }

        [Fact]
        public void Generic_TryResolve_T_Called()
        {
            var fk = new GenericFormKey<int> { Value = 7 };
            var linkLike = new { FormKey = fk };
            var cache = new FakeLinkCacheGeneric();

            var res = MunitionAutoPatcher.Services.Implementations.LinkCacheHelper.TryResolveViaLinkCache(linkLike, cache);
            Assert.NotNull(res);
            Assert.IsType<string>(res);
            Assert.Equal("generic:Int32", res);
        }
    }
}
