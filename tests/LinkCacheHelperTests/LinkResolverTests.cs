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
            
            // Create a mock for the return value that implements IObjectModificationGetter
            var mockResult = new Mock<IObjectModificationGetter>();
            IObjectModificationGetter? outResult = mockResult.Object;
            
            // Setup the first type checked by LinkResolver
            mockCache.Setup(x => x.TryResolve<IObjectModificationGetter>(It.IsAny<Mutagen.Bethesda.Plugins.FormKey>(), out outResult, It.IsAny<ResolveTarget>()))
                     .Returns((Mutagen.Bethesda.Plugins.FormKey k, out IObjectModificationGetter? r, ResolveTarget t) => {
                         r = outResult;
                         return true;
                     });

            var resolver = new LinkResolver(mockCache.Object, NullLogger<LinkResolver>.Instance);

            // 1st call
            Assert.True(resolver.TryResolve(fk, out var first));
            Assert.NotNull(first);
            
            // Verify mock called once
            mockCache.Verify(x => x.TryResolve<IObjectModificationGetter>(It.IsAny<Mutagen.Bethesda.Plugins.FormKey>(), out It.Ref<IObjectModificationGetter?>.IsAny, It.IsAny<ResolveTarget>()), Times.Once);

            // 2nd call (should be cached)
            Assert.True(resolver.TryResolve(fk, out var second));
            Assert.NotNull(second);
            
            // Verify mock still called only once
            mockCache.Verify(x => x.TryResolve<IObjectModificationGetter>(It.IsAny<Mutagen.Bethesda.Plugins.FormKey>(), out It.Ref<IObjectModificationGetter?>.IsAny, It.IsAny<ResolveTarget>()), Times.Once);
            
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
            // We need to setup the mock for IAmmunitionGetter because LinkResolver will try that specific type
            mockCache.Setup(x => x.TryResolve<IAmmunitionGetter>(It.IsAny<Mutagen.Bethesda.Plugins.FormKey>(), out outResult, It.IsAny<ResolveTarget>()))
                     .Returns(new TryResolveDelegate((Mutagen.Bethesda.Plugins.FormKey k, out IMajorRecordGetter? r, ResolveTarget t) => {
                         // Note: The delegate signature must match the generic type if we were strict, 
                         // but Moq handles the out param type covariance usually. 
                         // However, to be safe, we cast or use object.
                         // Actually, for generic methods, the delegate should match the generic type.
                         // Let's use a lambda that matches the specific type.
                         r = outResult;
                         return true;
                     }));
            
            // Wait, the delegate above uses IMajorRecordGetter, but we are setting up IAmmunitionGetter.
            // Let's define a specific delegate for this or just use a lambda with correct types.
            // Since IAmmunitionGetter inherits IMajorRecordGetter, it might work, but let's be precise.
            
            mockCache.Setup(x => x.TryResolve<IAmmunitionGetter>(It.IsAny<Mutagen.Bethesda.Plugins.FormKey>(), out outResult, It.IsAny<ResolveTarget>()))
                .Returns((Mutagen.Bethesda.Plugins.FormKey k, out IAmmunitionGetter? r, ResolveTarget t) => {
                    r = outResult;
                    return true;
                });

            var resolver = new LinkResolver(mockCache.Object, NullLogger<LinkResolver>.Instance);

            Assert.True(resolver.TryResolve<IAmmunitionGetter>(fk, out var ammo));
            Assert.NotNull(ammo);
            Assert.Equal("ammo1", ammo!.EditorID);

            // 2nd call
            Assert.True(resolver.TryResolve<IAmmunitionGetter>(fk, out var ammo2));
            // Verify called once
            mockCache.Verify(x => x.TryResolve<IAmmunitionGetter>(It.IsAny<Mutagen.Bethesda.Plugins.FormKey>(), out It.Ref<IAmmunitionGetter?>.IsAny, It.IsAny<ResolveTarget>()), Times.Once);
        }

        [Fact]
        public void ResolveByKey_UsesResolveAndCaches()
        {
            var fk = new Mutagen.Bethesda.Plugins.FormKey("m.esp", 0x10);
            var mockCache = new Mock<ILinkCache>();
            
            var mockResult = new Mock<IObjectModificationGetter>();
            IObjectModificationGetter? outResult = mockResult.Object;

            // ResolveByKey calls ResolveInternal -> ResolveFormKeyFast -> TryResolve
            mockCache.Setup(x => x.TryResolve<IObjectModificationGetter>(It.IsAny<Mutagen.Bethesda.Plugins.FormKey>(), out outResult, It.IsAny<ResolveTarget>()))
                     .Returns((Mutagen.Bethesda.Plugins.FormKey k, out IObjectModificationGetter? r, ResolveTarget t) => {
                         r = outResult;
                         return true;
                     });

            var resolver = new LinkResolver(mockCache.Object, NullLogger<LinkResolver>.Instance);
            
            var result = resolver.ResolveByKey(new MunitionAutoPatcher.Models.FormKey { PluginName = "m.esp", FormId = 0x10 });
            Assert.NotNull(result);
            
            // Verify TryResolve was called
            mockCache.Verify(x => x.TryResolve<IObjectModificationGetter>(It.IsAny<Mutagen.Bethesda.Plugins.FormKey>(), out It.Ref<IObjectModificationGetter?>.IsAny, It.IsAny<ResolveTarget>()), Times.Once);
        }
    }
}
