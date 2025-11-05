using System;
using System.Collections.Generic;
using System.Linq;
using Noggog;
using MunitionAutoPatcher.Services.Implementations;
using Xunit;

namespace LinkCacheHelperTests
{
    public class NoOpMutagenEnvironmentTests
    {
        [Fact]
        public void NoOp_ReturnsEmptyAndNull()
        {
            using var env = new NoOpMutagenEnvironment();

            Assert.False(env.GetWinningWeaponOverrides().Any());
            Assert.False(env.GetWinningConstructibleObjectOverrides().Any());
            Assert.False(env.EnumerateRecordCollections().Any());
            Assert.Null(env.GetLinkCache());
            Assert.Null(env.GetDataFolderPath());
        }
    }

    public class ResourcedMutagenEnvironmentTests
    {
        private class FakeEnv : IMutagenEnvironment
        {
            public IEnumerable<object> GetWinningWeaponOverrides() => new object[] { "W" };
            public IEnumerable<object> GetWinningConstructibleObjectOverrides() => new object[] { "C" };
            public IEnumerable<(string Name, IEnumerable<object> Items)> EnumerateRecordCollections()
            {
                yield return ("PriorityWeapons", new object[] { "W" });
            }
            private sealed class FakeResolver : MunitionAutoPatcher.Services.Interfaces.ILinkResolver
            {
                public bool TryResolve(object linkLike, out object? result) { result = null; return false; }
                public bool TryResolve<TGetter>(object linkLike, out TGetter? result) where TGetter : class? { result = null; return false; }
                public object? ResolveByKey(MunitionAutoPatcher.Models.FormKey key) => null;
            }
            private readonly FakeResolver _resolver = new FakeResolver();
            public MunitionAutoPatcher.Services.Interfaces.ILinkResolver? GetLinkCache() => _resolver;
            public DirectoryPath? GetDataFolderPath() => null;
        }

        private class FakeResource : IDisposable
        {
            public bool Disposed { get; private set; }
            public void Dispose() => Disposed = true;
        }

        [Fact]
        public void Resourced_ForwardsCallsAndDisposesResource()
        {
            var fakeEnv = new FakeEnv();
            var resource = new FakeResource();

            var r = new ResourcedMutagenEnvironment(fakeEnv, resource);

            var weapons = r.GetWinningWeaponOverrides().ToList();
            Assert.Single(weapons);
            Assert.Equal("W", weapons[0]);

            var cobjs = r.GetWinningConstructibleObjectOverrides().ToList();
            Assert.Single(cobjs);
            Assert.Equal("C", cobjs[0]);

            var collections = r.EnumerateRecordCollections().ToList();
            Assert.Single(collections);
            Assert.Equal("PriorityWeapons", collections[0].Name);

            var lc = r.GetLinkCache();
            Assert.NotNull(lc);
            Assert.Same(((FakeEnv)fakeEnv).GetLinkCache(), lc);
            Assert.Null(r.GetDataFolderPath());

            // Dispose explicitly and assert resource disposed
            r.Dispose();
            Assert.True(resource.Disposed);
        }

        [Fact]
        public void Dispose_IsIdempotent()
        {
            var fakeEnv = new FakeEnv();
            var resource = new FakeResource();
            var r = new ResourcedMutagenEnvironment(fakeEnv, resource);

            r.Dispose();
            Assert.True(resource.Disposed);

            // second dispose should not throw and resource stays disposed
            r.Dispose();
            Assert.True(resource.Disposed);
        }
    }
}
