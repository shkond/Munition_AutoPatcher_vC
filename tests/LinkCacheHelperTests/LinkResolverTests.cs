using System;
using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Implementations;
using Xunit;
using Moq;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Fallout4;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinkCacheHelperTests
{
    public class LinkResolverTests
    {
        // Define delegate for the callback
        delegate bool TryResolveDelegate(Mutagen.Bethesda.Plugins.FormKey key, out IMajorRecordGetter? result, ResolveTarget target);

        [Fact]
        public void TryResolve_CachesResultAcrossCalls()
        {
            var fk = new Mutagen.Bethesda.Plugins.FormKey("Test.esp", 123);
            var mockCache = new Mock<ILinkCache>();
            
            // Create a mock for the return value that implements IMajorRecordGetter
            var mockResult = new Mock<IMajorRecordGetter>();
            IMajorRecordGetter? outResult = mockResult.Object;
            
            // Setup the generic fallback which LinkResolver uses
            mockCache.Setup(x => x.TryResolve<IMajorRecordGetter>(fk, out outResult, It.IsAny<ResolveTarget>()))
                     .Returns(new TryResolveDelegate((Mutagen.Bethesda.Plugins.FormKey k, out IMajorRecordGetter? r, ResolveTarget t) => {
                         r = outResult;
                         return true;
                     }));

            var resolver = new LinkResolver(mockCache.Object, NullLogger<LinkResolver>.Instance);

            // 1st call
            Assert.True(resolver.TryResolve(fk, out var first));
            Assert.NotNull(first);
            
            // Verify mock called once
            mockCache.Verify(x => x.TryResolve<IMajorRecordGetter>(fk, out It.Ref<IMajorRecordGetter?>.IsAny, It.IsAny<ResolveTarget>()), Times.Once);

            // 2nd call (should be cached)
            Assert.True(resolver.TryResolve(fk, out var second));
            Assert.NotNull(second);
            
            // Verify mock still called only once
            mockCache.Verify(x => x.TryResolve<IMajorRecordGetter>(fk, out It.Ref<IMajorRecordGetter?>.IsAny, It.IsAny<ResolveTarget>()), Times.Once);
            
            Assert.Same(first, second);
        }

        [Fact]
        public void TryResolve_Generic_ReturnsTypedGetter()
        {
            var fk = new Mutagen.Bethesda.Plugins.FormKey("Ammo.esp", 456);
            var mockCache = new Mock<ILinkCache>();

            var mockAmmo = new Mock<IAmmunitionGetter>();
            mockAmmo.Setup(x => x.EditorID).Returns("ammo1");
            IAmmunitionGetter? outResult = mockAmmo.Object;

            // LinkResolver.ResolveFormKeyFast checks IAmmunitionGetter explicitly
            mockCache.Setup(x => x.TryResolve<IAmmunitionGetter>(fk, out outResult, It.IsAny<ResolveTarget>()))
                     .Returns(true);

            var resolver = new LinkResolver(mockCache.Object, NullLogger<LinkResolver>.Instance);

            Assert.True(resolver.TryResolve<IAmmunitionGetter>(fk, out var ammo));
            Assert.NotNull(ammo);
            Assert.Equal("ammo1", ammo!.EditorID);

            // 2nd call
            Assert.True(resolver.TryResolve<IAmmunitionGetter>(fk, out var ammo2));
            // Verify called once
            mockCache.Verify(x => x.TryResolve<IAmmunitionGetter>(fk, out It.Ref<IAmmunitionGetter?>.IsAny, It.IsAny<ResolveTarget>()), Times.Once);
        }

        [Fact]
        public void ResolveByKey_UsesResolveAndCaches()
        {
            var fk = new Mutagen.Bethesda.Plugins.FormKey("m.esp", 0x10);
            var mockCache = new Mock<ILinkCache>();
            
            var mockResult = new Mock<IMajorRecordGetter>();
            IMajorRecordGetter? outResult = mockResult.Object;

            // ResolveByKey calls ResolveInternal -> ResolveFormKeyFast -> TryResolve
            mockCache.Setup(x => x.TryResolve<IMajorRecordGetter>(fk, out outResult, It.IsAny<ResolveTarget>()))
                     .Returns(new TryResolveDelegate((Mutagen.Bethesda.Plugins.FormKey k, out IMajorRecordGetter? r, ResolveTarget t) => {
                         r = outResult;
                         return true;
                     }));

            var resolver = new LinkResolver(mockCache.Object, NullLogger<LinkResolver>.Instance);
            
            var result = resolver.ResolveByKey(new MunitionAutoPatcher.Models.FormKey { PluginName = "m.esp", FormId = 0x10 });
            Assert.NotNull(result);
            
            // Verify TryResolve was called
            mockCache.Verify(x => x.TryResolve<IMajorRecordGetter>(fk, out It.Ref<IMajorRecordGetter?>.IsAny, It.IsAny<ResolveTarget>()), Times.Once);
        }
    }
}
