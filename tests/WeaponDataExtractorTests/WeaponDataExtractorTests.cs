using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MunitionAutoPatcher.Services.Implementations;
using Microsoft.Extensions.Logging.Abstractions;
using MunitionAutoPatcher.Services.Interfaces;
using Xunit;
using Moq;

namespace WeaponDataExtractorTestsProject
{
    public class WeaponDataExtractorTests
    {
        // Test data classes - keeping these as they represent domain objects rather than services
        private class FakeModKey { public string FileName { get; set; } = string.Empty; }
        private class FakeFormKey { public FakeModKey ModKey { get; set; } = new FakeModKey(); public uint ID { get; set; } }
        private class FakeAmmoLink { public FakeFormKey FormKey { get; set; } = new FakeFormKey(); public bool IsNull => false; }
        private class FakeWeapon { public FakeFormKey FormKey { get; set; } = new FakeFormKey(); public FakeAmmoLink Ammo { get; set; } = new FakeAmmoLink(); }
        private class FakeConstructibleObject { public object CreatedObject { get; set; } = new FakeFormKey(); public string EditorID { get; set; } = string.Empty; public FakeFormKey? FormKey { get; set; } }

        [Theory]
        [MemberData(nameof(GetValidExtractionTestData))]
        public async Task ExtractAsync_WithValidWeaponAndCobj_ReturnsCorrectCandidateData(
            FakeWeapon weapon,
            FakeConstructibleObject cobj,
            string expectedCandidateType,
            string expectedEditorId,
            string expectedSuggestedTarget,
            string expectedSourcePlugin,
            string expectedWeaponPlugin,
            uint expectedWeaponFormId,
            string expectedAmmoPlugin,
            uint expectedAmmoFormId)
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
            var candidate = Assert.Single(results); // Assert.Single は要素が1つであることの検証と、その要素の取得を同時に行います。

            // --- 検証の強化 ---
            Assert.Equal(expectedCandidateType, candidate.CandidateType);
            Assert.Equal(expectedEditorId, candidate.CandidateEditorId);
            Assert.Equal(expectedSuggestedTarget, candidate.SuggestedTarget);
            Assert.Equal(expectedSourcePlugin, candidate.SourcePlugin);

            // 作成されたオブジェクト（武器）のFormKeyが正しいか
            Assert.NotNull(candidate.CandidateFormKey);
            Assert.Equal(expectedWeaponPlugin, candidate.CandidateFormKey.PluginName);
            Assert.Equal(expectedWeaponFormId, candidate.CandidateFormKey.FormId);

            // 武器から検出された弾薬のFormKeyが正しいか
            Assert.NotNull(candidate.CandidateAmmo);
            Assert.Equal(expectedAmmoPlugin, candidate.CandidateAmmo.PluginName);
            Assert.Equal(expectedAmmoFormId, candidate.CandidateAmmo.FormId);
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

        [Theory]
        [MemberData(nameof(GetExcludedPluginTestData))]
        public async Task ExtractAsync_WithExcludedPlugins_SkipsExcludedCandidates(
            FakeWeapon weapon,
            FakeConstructibleObject cobj,
            HashSet<string> excludedPlugins,
            bool shouldBeExcluded)
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
            if (shouldBeExcluded)
            {
                Assert.Empty(results);
            }
            else
            {
                Assert.Single(results);
            }
        }

        public static IEnumerable<object[]> GetValidExtractionTestData()
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
                    EditorID = "COBJ_Editor",
                    FormKey = new FakeFormKey { ModKey = new FakeModKey { FileName = "SourceMod.esp" }, ID = 0x9999 }
                },
                "COBJ", // expectedCandidateType
                "COBJ_Editor", // expectedEditorId
                "CreatedWeapon", // expectedSuggestedTarget
                "SourceMod.esp", // expectedSourcePlugin
                "TestMod.esp", // expectedWeaponPlugin
                0x1234u, // expectedWeaponFormId
                "AmmoMod.esp", // expectedAmmoPlugin
                0x2222u // expectedAmmoFormId
            };
        }

        public static IEnumerable<object[]> GetExcludedPluginTestData()
        {
            // Test case: Plugin is excluded
            yield return new object[]
            {
                new FakeWeapon
                {
                    FormKey = new FakeFormKey { ModKey = new FakeModKey { FileName = "ExcludedMod.esp" }, ID = 0x1111 },
                    Ammo = new FakeAmmoLink { FormKey = new FakeFormKey { ModKey = new FakeModKey { FileName = "Ammo.esp" }, ID = 0x2222 } }
                },
                new FakeConstructibleObject
                {
                    CreatedObject = new FakeFormKey { ModKey = new FakeModKey { FileName = "ExcludedMod.esp" }, ID = 0x1111 },
                    EditorID = "COBJ_Editor",
                    FormKey = new FakeFormKey { ModKey = new FakeModKey { FileName = "ExcludedMod.esp" }, ID = 0x9999 }
                },
                new HashSet<string> { "ExcludedMod.esp" },
                true // shouldBeExcluded
            };

            // Test case: Plugin is not excluded
            yield return new object[]
            {
                new FakeWeapon
                {
                    FormKey = new FakeFormKey { ModKey = new FakeModKey { FileName = "AllowedMod.esp" }, ID = 0x1111 },
                    Ammo = new FakeAmmoLink { FormKey = new FakeFormKey { ModKey = new FakeModKey { FileName = "Ammo.esp" }, ID = 0x2222 } }
                },
                new FakeConstructibleObject
                {
                    CreatedObject = new FakeFormKey { ModKey = new FakeModKey { FileName = "AllowedMod.esp" }, ID = 0x1111 },
                    EditorID = "COBJ_Editor",
                    FormKey = new FakeFormKey { ModKey = new FakeModKey { FileName = "AllowedMod.esp" }, ID = 0x9999 }
                },
                new HashSet<string> { "DifferentMod.esp" },
                false // shouldBeExcluded
            };
        }

        public static IEnumerable<object[]> GetNullOrEmptyCollectionTestData()
        {
            yield return new object[] { null, null };
            yield return new object[] { new object[0], new object[0] };
        }
    }
}
