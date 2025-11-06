using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MunitionAutoPatcher.Services.Implementations;
using Microsoft.Extensions.Logging.Abstractions;
using MunitionAutoPatcher.Services.Interfaces;
using MunitionAutoPatcher.Models;
using Xunit;

namespace LinkCacheHelperTests
{
    public class WeaponDataExtractorTests
    {
        private class FakeModKey { public string FileName { get; set; } = string.Empty; }
        private class FakeFormKey { public FakeModKey ModKey { get; set; } = new FakeModKey(); public uint ID { get; set; } }
        private class FakeAmmoLink { public FakeFormKey FormKey { get; set; } = new FakeFormKey(); public bool IsNull => false; }
        private class FakeWeapon { public FakeFormKey FormKey { get; set; } = new FakeFormKey(); public FakeAmmoLink Ammo { get; set; } = new FakeAmmoLink(); }
        private class FakeConstructibleObject { public object CreatedObject { get; set; } = new FakeFormKey(); public string EditorID { get; set; } = string.Empty; }

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
        public async Task ExtractAsync_HappyPath_ReturnsCandidate()
        {
            // Arrange: create a weapon with ammo and a COBJ that creates that weapon
            var weaponForm = new FakeFormKey { ModKey = new FakeModKey { FileName = "TestMod.esp" }, ID = 0x1234 };
            var ammoForm = new FakeFormKey { ModKey = new FakeModKey { FileName = "AmmoMod.esp" }, ID = 0x2222 };
            var weapon = new FakeWeapon { FormKey = weaponForm, Ammo = new FakeAmmoLink { FormKey = ammoForm } };

            var createdFormKey = weaponForm; // the created object refers to the weapon's form key
            var cobj = new FakeConstructibleObject { CreatedObject = createdFormKey, EditorID = "COBJ_Editor" };

            using var env = new NoOpResourcedMutagenEnvironment(new object[] { weapon }, new object[] { cobj });
            var extractor = new WeaponDataExtractor(NullLogger<WeaponDataExtractor>.Instance);

            // Act
            var results = await extractor.ExtractAsync(env, new HashSet<string>());

            // Assert
            Assert.NotNull(results);
            Assert.Single(results);
            var cand = results.First();
            Assert.Equal("COBJ", cand.CandidateType);
            Assert.Equal("COBJ_Editor", cand.CandidateEditorId);
            Assert.Equal("CreatedWeapon", cand.SuggestedTarget);
        }

        [Fact]
        public async Task ExtractAsync_ExcludedPlugin_SkipsCandidate()
        {
            // Arrange
            var weaponForm = new FakeFormKey { ModKey = new FakeModKey { FileName = "Excluded.esp" }, ID = 0x1111 };
            var ammoForm = new FakeFormKey { ModKey = new FakeModKey { FileName = "Ammo.esp" }, ID = 0x2222 };
            var weapon = new FakeWeapon { FormKey = weaponForm, Ammo = new FakeAmmoLink { FormKey = ammoForm } };

            var cobj = new FakeConstructibleObject { CreatedObject = weaponForm, EditorID = "COBJ_Editor" };

            using var env = new NoOpResourcedMutagenEnvironment(new object[] { weapon }, new object[] { cobj });
            var extractor = new WeaponDataExtractor(NullLogger<WeaponDataExtractor>.Instance);

            // Act
            var results = await extractor.ExtractAsync(env, new HashSet<string> { "Excluded.esp" });

            // Assert
            Assert.NotNull(results);
            Assert.Empty(results);
        }
    }
}
