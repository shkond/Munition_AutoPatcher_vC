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

        [Fact]
        public void ResolveByKey_WithNullPluginName_ReturnsNull()
        {
            var mockCache = new Mock<ILinkCache>();
            var resolver = new LinkResolver(mockCache.Object, NullLogger<LinkResolver>.Instance);

            // FormKey with null PluginName should return null
            var result = resolver.ResolveByKey(new MunitionAutoPatcher.Models.FormKey { PluginName = null!, FormId = 0x10 });
            Assert.Null(result);

            // Verify no TryResolve calls made
            mockCache.Verify(x => x.TryResolve<IObjectModificationGetter>(
                It.IsAny<Mutagen.Bethesda.Plugins.FormKey>(), 
                out It.Ref<IObjectModificationGetter?>.IsAny, 
                It.IsAny<ResolveTarget>()), Times.Never);
        }

        [Fact]
        public void ResolveByKey_WithEmptyPluginName_ReturnsNull()
        {
            var mockCache = new Mock<ILinkCache>();
            var resolver = new LinkResolver(mockCache.Object, NullLogger<LinkResolver>.Instance);

            var result = resolver.ResolveByKey(new MunitionAutoPatcher.Models.FormKey { PluginName = "", FormId = 0x10 });
            Assert.Null(result);
        }

        [Fact]
        public void TryResolve_WithCustomFormKey_ResolvesCorrectly()
        {
            var mockCache = new Mock<ILinkCache>();
            var mockWeapon = new Mock<IWeaponGetter>();
            IWeaponGetter? outWeapon = mockWeapon.Object;

            // Setup IWeaponGetter resolution (checked after OMOD, COBJ)
            IObjectModificationGetter? nullOmod = null;
            IConstructibleObjectGetter? nullCobj = null;
            mockCache.Setup(x => x.TryResolve<IObjectModificationGetter>(
                It.IsAny<Mutagen.Bethesda.Plugins.FormKey>(), out nullOmod, It.IsAny<ResolveTarget>()))
                .Returns(false);
            mockCache.Setup(x => x.TryResolve<IConstructibleObjectGetter>(
                It.IsAny<Mutagen.Bethesda.Plugins.FormKey>(), out nullCobj, It.IsAny<ResolveTarget>()))
                .Returns(false);
            mockCache.Setup(x => x.TryResolve<IWeaponGetter>(
                It.IsAny<Mutagen.Bethesda.Plugins.FormKey>(), out outWeapon, It.IsAny<ResolveTarget>()))
                .Returns(true);

            var resolver = new LinkResolver(mockCache.Object, NullLogger<LinkResolver>.Instance);
            
            // Use custom FormKey (MunitionAutoPatcher.Models.FormKey)
            var customFk = new MunitionAutoPatcher.Models.FormKey { PluginName = "Weapons.esp", FormId = 0x1234 };
            
            Assert.True(resolver.TryResolve(customFk, out var result));
            Assert.NotNull(result);
            Assert.IsAssignableFrom<IWeaponGetter>(result);
        }

        [Fact]
        public void TryResolve_WhenAllTypedResolutionsFail_TriesGenericFallback()
        {
            var mockCache = new Mock<ILinkCache>();
            
            // Setup all typed resolutions to fail
            IObjectModificationGetter? nullOmod = null;
            IConstructibleObjectGetter? nullCobj = null;
            IWeaponGetter? nullWeap = null;
            IAmmunitionGetter? nullAmmo = null;
            IMajorRecordGetter? nullRecord = null;

            mockCache.Setup(x => x.TryResolve<IObjectModificationGetter>(
                It.IsAny<Mutagen.Bethesda.Plugins.FormKey>(), out nullOmod, It.IsAny<ResolveTarget>()))
                .Returns(false);
            mockCache.Setup(x => x.TryResolve<IConstructibleObjectGetter>(
                It.IsAny<Mutagen.Bethesda.Plugins.FormKey>(), out nullCobj, It.IsAny<ResolveTarget>()))
                .Returns(false);
            mockCache.Setup(x => x.TryResolve<IWeaponGetter>(
                It.IsAny<Mutagen.Bethesda.Plugins.FormKey>(), out nullWeap, It.IsAny<ResolveTarget>()))
                .Returns(false);
            mockCache.Setup(x => x.TryResolve<IAmmunitionGetter>(
                It.IsAny<Mutagen.Bethesda.Plugins.FormKey>(), out nullAmmo, It.IsAny<ResolveTarget>()))
                .Returns(false);
            // Generic fallback also fails in this test
            mockCache.Setup(x => x.TryResolve<IMajorRecordGetter>(
                It.IsAny<Mutagen.Bethesda.Plugins.FormKey>(), out nullRecord, It.IsAny<ResolveTarget>()))
                .Returns(false);

            var resolver = new LinkResolver(mockCache.Object, NullLogger<LinkResolver>.Instance);
            var fk = new Mutagen.Bethesda.Plugins.FormKey("Test.esp", 0x999);
            
            // All resolutions fail, so result should be false
            Assert.False(resolver.TryResolve(fk, out var result));
            Assert.Null(result);
            
            // Verify generic fallback was attempted (at least once - Moq counts interface calls regardless of generic type)
            mockCache.Verify(x => x.TryResolve<IMajorRecordGetter>(
                It.IsAny<Mutagen.Bethesda.Plugins.FormKey>(), 
                out It.Ref<IMajorRecordGetter?>.IsAny, 
                It.IsAny<ResolveTarget>()), Times.AtLeastOnce);
        }

        [Fact]
        public void TryResolve_WhenCacheMissesAll_ReturnsFalse()
        {
            var mockCache = new Mock<ILinkCache>();
            
            // Setup all resolutions to fail
            IObjectModificationGetter? nullOmod = null;
            IConstructibleObjectGetter? nullCobj = null;
            IWeaponGetter? nullWeap = null;
            IAmmunitionGetter? nullAmmo = null;
            IMajorRecordGetter? nullRecord = null;

            mockCache.Setup(x => x.TryResolve<IObjectModificationGetter>(
                It.IsAny<Mutagen.Bethesda.Plugins.FormKey>(), out nullOmod, It.IsAny<ResolveTarget>()))
                .Returns(false);
            mockCache.Setup(x => x.TryResolve<IConstructibleObjectGetter>(
                It.IsAny<Mutagen.Bethesda.Plugins.FormKey>(), out nullCobj, It.IsAny<ResolveTarget>()))
                .Returns(false);
            mockCache.Setup(x => x.TryResolve<IWeaponGetter>(
                It.IsAny<Mutagen.Bethesda.Plugins.FormKey>(), out nullWeap, It.IsAny<ResolveTarget>()))
                .Returns(false);
            mockCache.Setup(x => x.TryResolve<IAmmunitionGetter>(
                It.IsAny<Mutagen.Bethesda.Plugins.FormKey>(), out nullAmmo, It.IsAny<ResolveTarget>()))
                .Returns(false);
            mockCache.Setup(x => x.TryResolve<IMajorRecordGetter>(
                It.IsAny<Mutagen.Bethesda.Plugins.FormKey>(), out nullRecord, It.IsAny<ResolveTarget>()))
                .Returns(false);

            var resolver = new LinkResolver(mockCache.Object, NullLogger<LinkResolver>.Instance);
            var fk = new Mutagen.Bethesda.Plugins.FormKey("Missing.esp", 0xDEAD);
            
            Assert.False(resolver.TryResolve(fk, out var result));
            Assert.Null(result);
        }

        [Fact]
        public void LinkCache_Property_ReturnsUnderlyingCache()
        {
            var mockCache = new Mock<ILinkCache>();
            var resolver = new LinkResolver(mockCache.Object, NullLogger<LinkResolver>.Instance);

            Assert.Same(mockCache.Object, resolver.LinkCache);
        }

        [Fact]
        public void TryResolve_WithFormLinkProperty_ExtractsFormKeyAndResolves()
        {
            var mockCache = new Mock<ILinkCache>();
            var mockOmod = new Mock<IObjectModificationGetter>();
            IObjectModificationGetter? outOmod = mockOmod.Object;

            mockCache.Setup(x => x.TryResolve<IObjectModificationGetter>(
                It.IsAny<Mutagen.Bethesda.Plugins.FormKey>(), out outOmod, It.IsAny<ResolveTarget>()))
                .Returns(true);

            var resolver = new LinkResolver(mockCache.Object, NullLogger<LinkResolver>.Instance);

            // Create a mock object with FormKey property (simulating FormLink behavior)
            var fk = new Mutagen.Bethesda.Plugins.FormKey("Link.esp", 0x100);
            var mockLinkLike = new Mock<IFormLinkGetter>();
            mockLinkLike.Setup(x => x.FormKey).Returns(fk);

            Assert.True(resolver.TryResolve(mockLinkLike.Object, out var result));
            Assert.NotNull(result);
        }

        [Theory]
        [InlineData("Fallout4.esm", 0x0001F278)]  // Vanilla 10mm ammo
        [InlineData("DLCNukaWorld.esm", 0x00001234)]
        [InlineData("MyMod.esp", 0xABCDEF)]
        public void ResolveByKey_VariousPluginFormats_AttemptsResolution(string pluginName, uint formId)
        {
            var mockCache = new Mock<ILinkCache>();
            var mockOmod = new Mock<IObjectModificationGetter>();
            IObjectModificationGetter? outOmod = mockOmod.Object;

            mockCache.Setup(x => x.TryResolve<IObjectModificationGetter>(
                It.IsAny<Mutagen.Bethesda.Plugins.FormKey>(), out outOmod, It.IsAny<ResolveTarget>()))
                .Returns(true);

            var resolver = new LinkResolver(mockCache.Object, NullLogger<LinkResolver>.Instance);
            var customFk = new MunitionAutoPatcher.Models.FormKey { PluginName = pluginName, FormId = formId };
            
            var result = resolver.ResolveByKey(customFk);
            
            // Verify resolution was attempted
            mockCache.Verify(x => x.TryResolve<IObjectModificationGetter>(
                It.IsAny<Mutagen.Bethesda.Plugins.FormKey>(), 
                out It.Ref<IObjectModificationGetter?>.IsAny, 
                It.IsAny<ResolveTarget>()), Times.Once);
            
            Assert.NotNull(result);
        }
    }
}
