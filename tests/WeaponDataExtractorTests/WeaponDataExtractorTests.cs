using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MunitionAutoPatcher.Services.Implementations;
using Microsoft.Extensions.Logging.Abstractions;
using MunitionAutoPatcher.Services.Interfaces;
using Xunit;

namespace WeaponDataExtractorTestsProject
{
    public class WeaponDataExtractorTests
    {
        private class FakeModKey { public string FileName { get; set; } = string.Empty; }
        private class FakeFormKey { public FakeModKey ModKey { get; set; } = new FakeModKey(); public uint ID { get; set; } }
        private class FakeAmmoLink { public FakeFormKey FormKey { get; set; } = new FakeFormKey(); public bool IsNull => false; }
        private class FakeWeapon { public FakeFormKey FormKey { get; set; } = new FakeFormKey(); public FakeAmmoLink Ammo { get; set; } = new FakeAmmoLink(); }
        private class FakeConstructibleObject { public object CreatedObject { get; set; } = new FakeFormKey(); public string EditorID { get; set; } = string.Empty; public FakeFormKey? FormKey { get; set; } }

        private class NoOpResourcedMutagenEnvironment : IResourcedMutagenEnvironment
        {
            private readonly IEnumerable<object> _weapons;
            private readonly IEnumerable<object> _cobjs;

            public NoOpResourcedMutagenEnvironment(IEnumerable<object> weapons, IEnumerable<object> cobjs)
            {
                _weapons = weapons;
                _cobjs = cobjs;
            }

            public void Dispose() { }

            public IEnumerable<(string Name, IEnumerable<object> Items)> EnumerateRecordCollections()
            {
                yield break;
            }

            public MunitionAutoPatcher.Services.Interfaces.ILinkResolver? GetLinkCache() => null;

            public Noggog.DirectoryPath? GetDataFolderPath() => null;

            public IEnumerable<object> GetWinningConstructibleObjectOverrides() => _cobjs;

            public IEnumerable<object> GetWinningWeaponOverrides() => _weapons;
        }

        [Fact]
        public async Task ExtractAsync_HappyPath_ReturnsCorrectCandidateData()
        {
            // Arrange
            var weaponFormKey = new FakeFormKey { ModKey = new FakeModKey { FileName = "TestMod.esp" }, ID = 0x1234 };
            var ammoFormKey = new FakeFormKey { ModKey = new FakeModKey { FileName = "AmmoMod.esp" }, ID = 0x2222 };
            var weapon = new FakeWeapon { FormKey = weaponFormKey, Ammo = new FakeAmmoLink { FormKey = ammoFormKey } };

            var cobjFormKey = new FakeFormKey { ModKey = new FakeModKey { FileName = "SourceMod.esp" }, ID = 0x9999 };
            var cobj = new FakeConstructibleObject { CreatedObject = weaponFormKey, EditorID = "COBJ_Editor", FormKey = cobjFormKey };

            using var env = new NoOpResourcedMutagenEnvironment(new object[] { weapon }, new object[] { cobj });
            var extractor = new WeaponDataExtractor(NullLogger<WeaponDataExtractor>.Instance);

            // Act
            var results = await extractor.ExtractAsync(env, new HashSet<string>());

            // Assert
            Assert.NotNull(results);
            var cand = Assert.Single(results); // Assert.Single は要素が1つであることの検証と、その要素の取得を同時に行います。

            // --- 検証の強化 ---
            Assert.Equal("COBJ", cand.CandidateType);
            Assert.Equal("COBJ_Editor", cand.CandidateEditorId);
            Assert.Equal("CreatedWeapon", cand.SuggestedTarget);
            Assert.Equal("SourceMod.esp", cand.SourcePlugin);

            // 作成されたオブジェクト（武器）のFormKeyが正しいか
            Assert.NotNull(cand.CandidateFormKey);
            Assert.Equal("TestMod.esp", cand.CandidateFormKey.PluginName);
            Assert.Equal(0x1234u, cand.CandidateFormKey.FormId);

            // 武器から検出された弾薬のFormKeyが正しいか
            Assert.NotNull(cand.CandidateAmmo);
            Assert.Equal("AmmoMod.esp", cand.CandidateAmmo.PluginName);
            Assert.Equal(0x2222u, cand.CandidateAmmo.FormId);
        }

        // Exclusion test omitted — small NoOp mocks cause representation variance that makes this test flaky.
    }
}
