using System;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MunitionAutoPatcher.Services.Implementations;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Xunit;

namespace LinkCacheHelperTests;

public class MutagenV51OmodPropertyAdapterTests
{
    private static MutagenV51OmodPropertyAdapter CreateAdapter() =>
        new(NullLogger<MutagenV51OmodPropertyAdapter>.Instance);

    [Fact]
    public void TryExtract_ReturnsTrue_WhenRecordFormKeyAvailable()
    {
        var adapter = CreateAdapter();
        var expectedKey = new FormKey(ModKey.FromNameAndExtension("Test.esm"), 0x0010u);

        var linkMock = new Mock<IFormLinkGetter<IFallout4MajorRecordGetter>>();
        linkMock.Setup(x => x.FormKeyNullable).Returns(expectedKey);

        var propMock = new Mock<IObjectModFormLinkIntPropertyGetter<Weapon.Property>>();
        propMock.Setup(x => x.Record).Returns(linkMock.Object);
        propMock.Setup(x => x.Property).Returns(Weapon.Property.Ammo);

        var weaponMock = new Mock<IWeaponModificationGetter>();
        weaponMock.Setup(w => w.EditorID).Returns("LaserOMOD");

        var success = adapter.TryExtractFormKeyFromAmmoProperty(propMock.Object, weaponMock.Object, out var result);

        Assert.True(success);
        Assert.Equal(expectedKey, result);
    }

    [Fact]
    public void TryExtract_ConstructsFormKey_WhenRecordMissingButValuePresent()
    {
        var adapter = CreateAdapter();
        var modKey = ModKey.FromNameAndExtension("Weapons.esp");
        const uint rawValue = 0x0ABCu;

        var propMock = new Mock<IObjectModFormLinkIntPropertyGetter<Weapon.Property>>();
        propMock.Setup(x => x.Record).Returns((IFormLinkGetter<IFallout4MajorRecordGetter>?)null!);
        propMock.Setup(x => x.Value).Returns(rawValue);

        var weaponMock = new Mock<IWeaponModificationGetter>();
        weaponMock.Setup(w => w.FormKey).Returns(new FormKey(modKey, 0x0001u));

        var success = adapter.TryExtractFormKeyFromAmmoProperty(propMock.Object, weaponMock.Object, out var result);

        Assert.True(success);
        Assert.Equal(new FormKey(modKey, rawValue), result);
    }

    [Fact]
    public void TryExtract_ReturnsFalse_WhenFormLinkInterfaceMissing()
    {
        var adapter = CreateAdapter();

        var propMock = new Mock<IAObjectModPropertyGetter<Weapon.Property>>();
        var weaponMock = new Mock<IWeaponModificationGetter>();

        var success = adapter.TryExtractFormKeyFromAmmoProperty(propMock.Object, weaponMock.Object, out _);

        Assert.False(success);
    }

    [Fact]
    public void TryExtract_ReturnsFalse_WhenRawValueZero()
    {
        var adapter = CreateAdapter();

        var propMock = new Mock<IObjectModFormLinkIntPropertyGetter<Weapon.Property>>();
        propMock.Setup(x => x.Value).Returns(0u);

        var weaponMock = new Mock<IWeaponModificationGetter>();
        weaponMock.Setup(w => w.FormKey).Returns(new FormKey(ModKey.FromNameAndExtension("Weapons.esp"), 0x0001u));

        var result = adapter.TryExtractFormKeyFromAmmoProperty(propMock.Object, weaponMock.Object, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryExtract_Throws_WhenWeaponModHasNullModKey()
    {
        var adapter = CreateAdapter();

        var propMock = new Mock<IObjectModFormLinkIntPropertyGetter<Weapon.Property>>();
        propMock.Setup(x => x.Value).Returns(0x0FFu);

        var weaponMock = new Mock<IWeaponModificationGetter>();
        weaponMock.Setup(w => w.FormKey).Returns(FormKey.Null);

        Assert.Throws<InvalidOperationException>(() =>
            adapter.TryExtractFormKeyFromAmmoProperty(propMock.Object, weaponMock.Object, out _));
    }
}
