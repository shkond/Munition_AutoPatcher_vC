using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MunitionAutoPatcher.Services.Implementations;
using Microsoft.Extensions.Logging.Abstractions;
using MunitionAutoPatcher.Services.Interfaces;
using MunitionAutoPatcher.Models;
using Xunit;
using Moq;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using MutagenFormKey = Mutagen.Bethesda.Plugins.FormKey;

namespace LinkCacheHelperTests
{
    public class WeaponDataExtractorTests
    {
        [Theory]
        [MemberData(nameof(GetValidWeaponAndCobjTestData))]
        public async Task ExtractAsync_WithValidWeaponAndCobj_ReturnsExpectedCandidate(
            Mock<IWeaponGetter> mockWeapon,
            Mock<IConstructibleObjectGetter> mockCobj,
            string expectedCandidateType,
            string expectedEditorId,
            string expectedSuggestedTarget)
        {
            // Arrange
            var mockEnvironment = new Mock<IResourcedMutagenEnvironment>();
            mockEnvironment.Setup(x => x.GetWinningWeaponOverridesTyped()).Returns(new[] { mockWeapon.Object });
            mockEnvironment.Setup(x => x.GetWinningConstructibleObjectOverridesTyped()).Returns(new[] { mockCobj.Object });

            // Also setup untyped for fallback if needed, though Typed should be preferred
            mockEnvironment.Setup(x => x.GetWinningWeaponOverrides()).Returns(new object[] { mockWeapon.Object });
            mockEnvironment.Setup(x => x.GetWinningConstructibleObjectOverrides()).Returns(new object[] { mockCobj.Object });

            var extractor = new WeaponDataExtractor(NullLogger<WeaponDataExtractor>.Instance);

            // Act
            var results = await extractor.ExtractAsync(mockEnvironment.Object, new HashSet<string>());

            // Assert
            Assert.NotNull(results);
            Assert.Single(results);
            var candidate = results.First();
            Assert.Equal(expectedCandidateType, candidate.CandidateType);
            Assert.Equal(expectedEditorId, candidate.CandidateEditorId);
            Assert.Equal(expectedSuggestedTarget, candidate.SuggestedTarget);
        }

        [Theory]
        [MemberData(nameof(GetExcludedPluginTestData))]
        public async Task ExtractAsync_WithExcludedPlugin_SkipsCandidate(
            Mock<IWeaponGetter> mockWeapon,
            Mock<IConstructibleObjectGetter> mockCobj,
            HashSet<string> excludedPlugins)
        {
            // Arrange
            var mockEnvironment = new Mock<IResourcedMutagenEnvironment>();
            mockEnvironment.Setup(x => x.GetWinningWeaponOverridesTyped()).Returns(new[] { mockWeapon.Object });
            mockEnvironment.Setup(x => x.GetWinningConstructibleObjectOverridesTyped()).Returns(new[] { mockCobj.Object });
            
            mockEnvironment.Setup(x => x.GetWinningWeaponOverrides()).Returns(new object[] { mockWeapon.Object });
            mockEnvironment.Setup(x => x.GetWinningConstructibleObjectOverrides()).Returns(new object[] { mockCobj.Object });

            var extractor = new WeaponDataExtractor(NullLogger<WeaponDataExtractor>.Instance);

            // Act
            var results = await extractor.ExtractAsync(mockEnvironment.Object, excludedPlugins);

            // Assert
            Assert.NotNull(results);
            Assert.Empty(results);
        }

        [Theory]
        [MemberData(nameof(GetNullOrEmptyCollectionTestData))]
        public async Task ExtractAsync_WithNullOrEmptyCollections_ReturnsEmptyResults(
            IEnumerable<IWeaponGetter>? weapons, 
            IEnumerable<IConstructibleObjectGetter>? cobjs)
        {
            // Arrange
            var mockEnvironment = new Mock<IResourcedMutagenEnvironment>();
            mockEnvironment.Setup(x => x.GetWinningWeaponOverridesTyped()).Returns(weapons ?? Enumerable.Empty<IWeaponGetter>());
            mockEnvironment.Setup(x => x.GetWinningConstructibleObjectOverridesTyped()).Returns(cobjs ?? Enumerable.Empty<IConstructibleObjectGetter>());

            mockEnvironment.Setup(x => x.GetWinningWeaponOverrides()).Returns(weapons?.Cast<object>() ?? Enumerable.Empty<object>());
            mockEnvironment.Setup(x => x.GetWinningConstructibleObjectOverrides()).Returns(cobjs?.Cast<object>() ?? Enumerable.Empty<object>());

            var extractor = new WeaponDataExtractor(NullLogger<WeaponDataExtractor>.Instance);

            // Act
            var results = await extractor.ExtractAsync(mockEnvironment.Object, new HashSet<string>());

            // Assert
            Assert.NotNull(results);
            Assert.Empty(results);
        }

        public static IEnumerable<object[]> GetValidWeaponAndCobjTestData()
        {
            var weaponFormKey = new MutagenFormKey(new ModKey("TestMod", ModType.Plugin), 0x1234);
            var ammoFormKey = new MutagenFormKey(new ModKey("AmmoMod", ModType.Plugin), 0x2222);

            var mockWeapon = new Mock<IWeaponGetter>();
            mockWeapon.Setup(w => w.FormKey).Returns(weaponFormKey);
            mockWeapon.Setup(w => w.EditorID).Returns("TestWeapon");
            
            var mockAmmoLink = new Mock<IFormLinkGetter<IAmmunitionGetter>>();
            mockAmmoLink.Setup(a => a.FormKey).Returns(ammoFormKey);
            mockAmmoLink.Setup(a => a.IsNull).Returns(false);
            mockWeapon.Setup(w => w.Ammo).Returns(mockAmmoLink.Object);

            var mockCobj = new Mock<IConstructibleObjectGetter>();
            mockCobj.Setup(c => c.FormKey).Returns(new MutagenFormKey(new ModKey("TestMod", ModType.Plugin), 0x9999));
            mockCobj.Setup(c => c.EditorID).Returns("COBJ_Editor");
            
            var mockCreatedObjectLink = new Mock<IFormLinkNullableGetter<IConstructibleObjectTargetGetter>>();
            mockCreatedObjectLink.Setup(l => l.FormKey).Returns(weaponFormKey);
            mockCreatedObjectLink.Setup(l => l.IsNull).Returns(false);
            mockCobj.Setup(c => c.CreatedObject).Returns(mockCreatedObjectLink.Object);

            yield return new object[]
            {
                mockWeapon,
                mockCobj,
                "COBJ",
                "COBJ_Editor",
                "CreatedWeapon"
            };
        }

        public static IEnumerable<object[]> GetExcludedPluginTestData()
        {
            var weaponFormKey = new MutagenFormKey(new ModKey("Excluded", ModType.Plugin), 0x1111);
            var ammoFormKey = new MutagenFormKey(new ModKey("Ammo", ModType.Plugin), 0x2222);

            var mockWeapon = new Mock<IWeaponGetter>();
            mockWeapon.Setup(w => w.FormKey).Returns(weaponFormKey);
            
            var mockAmmoLink = new Mock<IFormLinkGetter<IAmmunitionGetter>>();
            mockAmmoLink.Setup(a => a.FormKey).Returns(ammoFormKey);
            mockWeapon.Setup(w => w.Ammo).Returns(mockAmmoLink.Object);

            var mockCobj = new Mock<IConstructibleObjectGetter>();
            mockCobj.Setup(c => c.FormKey).Returns(new MutagenFormKey(new ModKey("Excluded", ModType.Plugin), 0x8888));
            
            var mockCreatedObjectLink = new Mock<IFormLinkNullableGetter<IConstructibleObjectTargetGetter>>();
            mockCreatedObjectLink.Setup(l => l.FormKey).Returns(weaponFormKey);
            mockCobj.Setup(c => c.CreatedObject).Returns(mockCreatedObjectLink.Object);

            yield return new object[]
            {
                mockWeapon,
                mockCobj,
                new HashSet<string> { "Excluded.esp" }
            };
        }

        public static IEnumerable<object?[]> GetNullOrEmptyCollectionTestData()
        {
            yield return new object?[] { null, null };
            yield return new object?[] { new IWeaponGetter[0], new IConstructibleObjectGetter[0] };
        }
    }
}
