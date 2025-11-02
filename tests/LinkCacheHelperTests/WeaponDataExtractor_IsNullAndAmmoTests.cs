using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Implementations;
using Xunit;

namespace LinkCacheHelperTests
{
    public class WeaponDataExtractor_AdditionalTests
    {
        private class FakeModKey { public string FileName { get; set; } = string.Empty; }
        private class FakeFormKey { public FakeModKey ModKey { get; set; } = new FakeModKey(); public uint ID { get; set; } }
        private class FakeAmmoLink { public FakeFormKey FormKey { get; set; } = new FakeFormKey(); public bool IsNull => false; }
        private class FakeWeapon { public FakeFormKey FormKey { get; set; } = new FakeFormKey(); public FakeAmmoLink Ammo { get; set; } = new FakeAmmoLink(); public string? EditorID { get; set; } }
        private class FakeNullLink { public bool IsNull => true; }
        private class FakeConstructibleObject { public object CreatedObject { get; set; } = new FakeFormKey(); public string? EditorID { get; set; } = string.Empty; public FakeFormKey? FormKey { get; set; } }

        private class NoOpResourcedMutagenEnvironment : IResourcedMutagenEnvironment
        {
            private readonly IEnumerable<object> _weapons;
            private readonly IEnumerable<object> _cobjs;

            public NoOpResourcedMutagenEnvironment(IEnumerable<object> weapons, IEnumerable<object> cobjs)
            {
                _weapons = weapons;
                _cobjs = cobjs;
            }

            public Noggog.DirectoryPath? GetDataFolderPath() => null;
            public IEnumerable<object> GetWinningConstructibleObjectOverrides() => _cobjs;
            public IEnumerable<object> GetWinningWeaponOverrides() => _weapons;
            public IEnumerable<(string Name, IEnumerable<object> Items)> EnumerateRecordCollections()
                => new[] { ("ConstructibleObject", _cobjs), ("Weapon", _weapons) };
            public object? GetLinkCache() => null;
            public void Dispose() { }
        }

        [Fact]
        public async Task ExtractAsync_Skips_WhenCreatedObjectIsNullLike()
        {
            var cobj = new FakeConstructibleObject
            {
                CreatedObject = new FakeNullLink(),
                EditorID = "COBJ_NULL"
            };

            var env = new NoOpResourcedMutagenEnvironment(Enumerable.Empty<object>(), new[] { cobj });
            var extractor = new WeaponDataExtractor();

            var result = await extractor.ExtractAsync(env, new HashSet<string>());

            Assert.Empty(result);
        }

        [Fact]
        public async Task ExtractAsync_Extracts_AmmoKey_WhenWeaponHasAmmoFormKey()
        {
            // Arrange weapon with matching FormKey and ammo link
            var weapon = new FakeWeapon
            {
                FormKey = new FakeFormKey { ModKey = new FakeModKey { FileName = "Base.esp" }, ID = 0x00000111 },
                Ammo = new FakeAmmoLink { FormKey = new FakeFormKey { ModKey = new FakeModKey { FileName = "Ammo.esp" }, ID = 0x000000A1 } },
                EditorID = "W_BASE"
            };

            // COBJ that creates the weapon above
            var cobj = new FakeConstructibleObject
            {
                CreatedObject = new FakeFormKey { ModKey = new FakeModKey { FileName = "Base.esp" }, ID = 0x00000111 },
                EditorID = "COBJ_CREATE_BASE",
                FormKey = new FakeFormKey { ModKey = new FakeModKey { FileName = "Src.esp" }, ID = 0x0000F001 }
            };

            var env = new NoOpResourcedMutagenEnvironment(new object[] { weapon }, new object[] { cobj });
            var extractor = new WeaponDataExtractor();

            // Act
            var result = await extractor.ExtractAsync(env, new HashSet<string>());

            // Assert
            Assert.Single(result);
            var cand = result[0];
            Assert.Equal("COBJ", cand.CandidateType);
            Assert.Equal("Base.esp", cand.CandidateFormKey.PluginName);
            Assert.Equal((uint)0x00000111, cand.CandidateFormKey.FormId);
            Assert.NotNull(cand.CandidateAmmo);
            Assert.Equal("Ammo.esp", cand.CandidateAmmo!.PluginName);
            Assert.Equal((uint)0x000000A1, cand.CandidateAmmo!.FormId);
        }
    }
}
