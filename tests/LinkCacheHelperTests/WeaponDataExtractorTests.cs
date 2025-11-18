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

namespace LinkCacheHelperTests
{
    public class WeaponDataExtractorTests
    {
        // Test data classes - keeping these as they represent domain objects rather than services
        public class FakeModKey { public string FileName { get; set; } = string.Empty; }
        public class FakeFormKey { public FakeModKey ModKey { get; set; } = new FakeModKey(); public uint ID { get; set; } }
        public class FakeAmmoLink { public FakeFormKey FormKey { get; set; } = new FakeFormKey(); public bool IsNull => false; }
        public class FakeWeapon { public FakeFormKey FormKey { get; set; } = new FakeFormKey(); public FakeAmmoLink Ammo { get; set; } = new FakeAmmoLink(); }
        public class FakeConstructibleObject { public object CreatedObject { get; set; } = new FakeFormKey(); public string EditorID { get; set; } = string.Empty; }

        [Theory]
        [MemberData(nameof(GetValidWeaponAndCobjTestData))]
        public async Task ExtractAsync_WithValidWeaponAndCobj_ReturnsExpectedCandidate(
            FakeWeapon weapon,
            FakeConstructibleObject cobj,
            string expectedCandidateType,
            string expectedEditorId,
            string expectedSuggestedTarget)
        {
            // Arrange
            var mockEnvironment = new Mock<IResourcedMutagenEnvironment>();
            mockEnvironment.Setup(x => x.GetWinningWeaponOverrides()).Returns(new object[] { weapon });
            mockEnvironment.Setup(x => x.GetWinningConstructibleObjectOverrides()).Returns(new object[] { cobj });

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
            FakeWeapon weapon,
            FakeConstructibleObject cobj,
            HashSet<string> excludedPlugins)
        {
            // Arrange
            var mockEnvironment = new Mock<IResourcedMutagenEnvironment>();
            mockEnvironment.Setup(x => x.GetWinningWeaponOverrides()).Returns(new object[] { weapon });
            mockEnvironment.Setup(x => x.GetWinningConstructibleObjectOverrides()).Returns(new object[] { cobj });

            var extractor = new WeaponDataExtractor(NullLogger<WeaponDataExtractor>.Instance);

            // Act
            var results = await extractor.ExtractAsync(mockEnvironment.Object, excludedPlugins);

            // Assert
            Assert.NotNull(results);
            Assert.Empty(results);
        }

        [Theory]
        [MemberData(nameof(GetNullOrEmptyCollectionTestData))]
        public async Task ExtractAsync_WithNullOrEmptyCollections_ReturnsEmptyResults(object[] weapons, object[] cobjs)
        {
            // Arrange
            var mockEnvironment = new Mock<IResourcedMutagenEnvironment>();
            mockEnvironment.Setup(x => x.GetWinningWeaponOverrides()).Returns(weapons ?? new object[0]);
            mockEnvironment.Setup(x => x.GetWinningConstructibleObjectOverrides()).Returns(cobjs ?? new object[0]);

            var extractor = new WeaponDataExtractor(NullLogger<WeaponDataExtractor>.Instance);

            // Act
            var results = await extractor.ExtractAsync(mockEnvironment.Object, new HashSet<string>());

            // Assert
            Assert.NotNull(results);
            Assert.Empty(results);
        }

        public static IEnumerable<object[]> GetValidWeaponAndCobjTestData()
        {
            yield return new object[]
            {
                new FakeWeapon
                {
                    FormKey = new FakeFormKey { ModKey = new FakeModKey { FileName = "TestMod.esp" }, ID = 0x1234 },
                    Ammo = new FakeAmmoLink { FormKey = new FakeFormKey { ModKey = new FakeModKey { FileName = "AmmoMod.esp" }, ID = 0x2222 } }
                },
                new FakeConstructibleObject
                {
                    CreatedObject = new FakeFormKey { ModKey = new FakeModKey { FileName = "TestMod.esp" }, ID = 0x1234 },
                    EditorID = "COBJ_Editor"
                },
                "COBJ",
                "COBJ_Editor",
                "CreatedWeapon"
            };
        }

        public static IEnumerable<object[]> GetExcludedPluginTestData()
        {
            yield return new object[]
            {
                new FakeWeapon
                {
                    FormKey = new FakeFormKey { ModKey = new FakeModKey { FileName = "Excluded.esp" }, ID = 0x1111 },
                    Ammo = new FakeAmmoLink { FormKey = new FakeFormKey { ModKey = new FakeModKey { FileName = "Ammo.esp" }, ID = 0x2222 } }
                },
                new FakeConstructibleObject
                {
                    CreatedObject = new FakeFormKey { ModKey = new FakeModKey { FileName = "Excluded.esp" }, ID = 0x1111 },
                    EditorID = "COBJ_Editor"
                },
                new HashSet<string> { "Excluded.esp" }
            };
        }

        public static IEnumerable<object[]> GetNullOrEmptyCollectionTestData()
        {
            yield return new object[] { null, null };
            yield return new object[] { new object[0], new object[0] };
        }
    }
}
