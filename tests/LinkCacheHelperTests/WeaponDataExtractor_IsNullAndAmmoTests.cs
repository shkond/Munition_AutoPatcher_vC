using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Implementations;
using MunitionAutoPatcher.Services.Interfaces;
using Mutagen.Bethesda.Plugins.Records;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Moq;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using MutagenFormKey = Mutagen.Bethesda.Plugins.FormKey;

namespace LinkCacheHelperTests
{
    public class WeaponDataExtractor_AdditionalTests
    {
        [Fact]
        public async Task ExtractAsync_Skips_WhenCreatedObjectIsNullLike()
        {
            // Arrange
            var mockCobj = new Mock<IConstructibleObjectGetter>();
            var mockCreatedObjectLink = new Mock<IFormLinkNullableGetter<IConstructibleObjectTargetGetter>>();
            mockCreatedObjectLink.Setup(l => l.IsNull).Returns(true);
            mockCobj.Setup(c => c.CreatedObject).Returns(mockCreatedObjectLink.Object);
            mockCobj.Setup(c => c.EditorID).Returns("COBJ_NULL");

            var mockEnvironment = new Mock<IResourcedMutagenEnvironment>();
            mockEnvironment.Setup(x => x.GetWinningWeaponOverridesTyped()).Returns(Enumerable.Empty<IWeaponGetter>());
            mockEnvironment.Setup(x => x.GetWinningConstructibleObjectOverridesTyped()).Returns(new[] { mockCobj.Object });
            
            mockEnvironment.Setup(x => x.GetWinningWeaponOverrides()).Returns(Enumerable.Empty<object>());
            mockEnvironment.Setup(x => x.GetWinningConstructibleObjectOverrides()).Returns(new object[] { mockCobj.Object });

            var extractor = new WeaponDataExtractor(new Mock<IMutagenAccessor>().Object, NullLogger<WeaponDataExtractor>.Instance);

            // Act
            var result = await extractor.ExtractAsync(mockEnvironment.Object, new HashSet<string>());

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task ExtractAsync_Extracts_AmmoKey_WhenWeaponHasAmmoFormKey()
        {
            // Arrange
            var weaponFormKey = new MutagenFormKey(new ModKey("Base", ModType.Plugin), 0x00000111);
            var ammoFormKey = new MutagenFormKey(new ModKey("Ammo", ModType.Plugin), 0x000000A1);

            var mockWeapon = new Mock<IWeaponGetter>();
            mockWeapon.Setup(w => w.FormKey).Returns(weaponFormKey);
            mockWeapon.Setup(w => w.EditorID).Returns("W_BASE");
            
            var mockAmmoLink = new Mock<IFormLinkGetter<IAmmunitionGetter>>();
            mockAmmoLink.Setup(a => a.FormKey).Returns(ammoFormKey);
            mockAmmoLink.Setup(a => a.IsNull).Returns(false);
            mockWeapon.Setup(w => w.Ammo).Returns(mockAmmoLink.Object);

            var mockCobj = new Mock<IConstructibleObjectGetter>();
            mockCobj.Setup(c => c.FormKey).Returns(new MutagenFormKey(new ModKey("Src", ModType.Plugin), 0x0000F001));
            mockCobj.Setup(c => c.EditorID).Returns("COBJ_CREATE_BASE");
            
            var mockCreatedObjectLink = new Mock<IFormLinkNullableGetter<IConstructibleObjectTargetGetter>>();
            mockCreatedObjectLink.Setup(l => l.FormKey).Returns(weaponFormKey);
            mockCreatedObjectLink.Setup(l => l.IsNull).Returns(false);
            mockCobj.Setup(c => c.CreatedObject).Returns(mockCreatedObjectLink.Object);

            var mockEnvironment = new Mock<IResourcedMutagenEnvironment>();
            mockEnvironment.Setup(x => x.GetWinningWeaponOverridesTyped()).Returns(new[] { mockWeapon.Object });
            mockEnvironment.Setup(x => x.GetWinningConstructibleObjectOverridesTyped()).Returns(new[] { mockCobj.Object });

            mockEnvironment.Setup(x => x.GetWinningWeaponOverrides()).Returns(new object[] { mockWeapon.Object });
            mockEnvironment.Setup(x => x.GetWinningConstructibleObjectOverrides()).Returns(new object[] { mockCobj.Object });

            var extractor = new WeaponDataExtractor(new Mock<IMutagenAccessor>().Object, NullLogger<WeaponDataExtractor>.Instance);

            // Act
            var result = await extractor.ExtractAsync(mockEnvironment.Object, new HashSet<string>());

            // Assert
            Assert.Single(result);
            var cand = result[0];
            Assert.Equal("COBJ", cand.CandidateType);
            // CandidateFormKey should now be the COBJ's FormKey (not the CreatedObject/Weapon's FormKey)
            Assert.Equal("Src.esp", cand.CandidateFormKey.PluginName);
            Assert.Equal((uint)0x0000F001, cand.CandidateFormKey.FormId);
            Assert.NotNull(cand.CandidateAmmo);
            Assert.Equal("Ammo.esp", cand.CandidateAmmo!.PluginName);
            Assert.Equal((uint)0x000000A1, cand.CandidateAmmo!.FormId);
        }
    }
}
