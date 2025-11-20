using System;
using System.Collections.Generic;
using System.Linq;
using MunitionAutoPatcher.Services.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MunitionAutoPatcher.Models;
using Xunit;
using Moq;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using MutagenFormKey = Mutagen.Bethesda.Plugins.FormKey;

namespace LinkCacheHelperTests
{
    public class CandidateEnumeratorTests
    {
        // Interfaces for mocking dynamic structure
        public interface IMockPriorityOrder
        {
            IEnumerable<IConstructibleObjectGetter> ConstructibleObject();
            IEnumerable<IWeaponGetter> Weapon();
        }

        public interface IMockLoadOrder
        {
            IMockPriorityOrder PriorityOrder { get; }
        }

        public interface IMockEnvironment
        {
            IMockLoadOrder LoadOrder { get; }
        }
        
        public interface IMockRecordCollection<T> : IEnumerable<T>
        {
            IEnumerable<T> WinningOverrides();
        }

        [Theory]
        [MemberData(nameof(GetValidCandidateTestData))]
        public void EnumerateCandidates_WithValidWeaponAndCobj_IncludesExpectedCandidate(
            Mock<IWeaponGetter> mockWeapon,
            Mock<IConstructibleObjectGetter> mockCobj,
            string expectedCandidateType,
            string expectedSourcePlugin,
            string expectedWeaponPlugin)
        {
            // Arrange
            var mockWeapons = new Mock<IMockRecordCollection<IWeaponGetter>>();
            mockWeapons.Setup(x => x.WinningOverrides()).Returns(new[] { mockWeapon.Object });
            mockWeapons.Setup(x => x.GetEnumerator()).Returns(() => new List<IWeaponGetter> { mockWeapon.Object }.GetEnumerator());

            var mockCobjs = new Mock<IMockRecordCollection<IConstructibleObjectGetter>>();
            mockCobjs.Setup(x => x.WinningOverrides()).Returns(new[] { mockCobj.Object });
            mockCobjs.Setup(x => x.GetEnumerator()).Returns(() => new List<IConstructibleObjectGetter> { mockCobj.Object }.GetEnumerator());

            var mockPriorityOrder = new Mock<IMockPriorityOrder>();
            mockPriorityOrder.Setup(x => x.Weapon()).Returns(mockWeapons.Object);
            mockPriorityOrder.Setup(x => x.ConstructibleObject()).Returns(mockCobjs.Object);

            var mockLoadOrder = new Mock<IMockLoadOrder>();
            mockLoadOrder.Setup(x => x.PriorityOrder).Returns(mockPriorityOrder.Object);

            var mockEnvironment = new Mock<IMockEnvironment>();
            mockEnvironment.Setup(x => x.LoadOrder).Returns(mockLoadOrder.Object);

            var excludedPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var mockLogger = new Mock<ILogger>();

            // Act
            var results = CandidateEnumerator.EnumerateCandidates(
                mockEnvironment.Object,
                excludedPlugins,
                null,
                mockLogger.Object);

            // Check for errors
            mockLogger.Verify(l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Never);

            // Verify mock access
            mockCobjs.Verify(x => x.WinningOverrides(), Times.AtLeastOnce(), "WinningOverrides not called on COBJs");
            mockCobj.Verify(c => c.CreatedObject, Times.AtLeastOnce(), "CreatedObject not accessed");
            mockCobj.Verify(c => c.FormKey, Times.AtLeastOnce(), "FormKey not accessed");

            // Assert
            var candidate = Assert.Single(results);
            Assert.True(candidate.CandidateType == expectedCandidateType, $"Type mismatch: Exp='{expectedCandidateType}', Act='{candidate.CandidateType}'");
            Assert.True(candidate.SourcePlugin == expectedSourcePlugin, $"Source mismatch: Exp='{expectedSourcePlugin}', Act='{candidate.SourcePlugin}'");
            Assert.True(candidate.CandidateFormKey.PluginName == expectedWeaponPlugin, $"WeaponPlugin mismatch: Exp='{expectedWeaponPlugin}', Act='{candidate.CandidateFormKey.PluginName}'");
        }

        [Theory]
        [MemberData(nameof(GetExcludedPluginTestData))]
        public void EnumerateCandidates_WithExcludedPlugin_SkipsExcludedCandidate(
            Mock<IWeaponGetter> mockWeapon,
            Mock<IConstructibleObjectGetter> mockCobj,
            HashSet<string> excludedPlugins,
            string excludedSourcePlugin)
        {
            // Arrange
            var mockWeapons = new Mock<IMockRecordCollection<IWeaponGetter>>();
            mockWeapons.Setup(x => x.WinningOverrides()).Returns(new[] { mockWeapon.Object });
            mockWeapons.Setup(x => x.GetEnumerator()).Returns(() => new List<IWeaponGetter> { mockWeapon.Object }.GetEnumerator());

            var mockCobjs = new Mock<IMockRecordCollection<IConstructibleObjectGetter>>();
            mockCobjs.Setup(x => x.WinningOverrides()).Returns(new[] { mockCobj.Object });
            mockCobjs.Setup(x => x.GetEnumerator()).Returns(() => new List<IConstructibleObjectGetter> { mockCobj.Object }.GetEnumerator());

            var mockPriorityOrder = new Mock<IMockPriorityOrder>();
            mockPriorityOrder.Setup(x => x.Weapon()).Returns(mockWeapons.Object);
            mockPriorityOrder.Setup(x => x.ConstructibleObject()).Returns(mockCobjs.Object);

            var mockLoadOrder = new Mock<IMockLoadOrder>();
            mockLoadOrder.Setup(x => x.PriorityOrder).Returns(mockPriorityOrder.Object);

            var mockEnvironment = new Mock<IMockEnvironment>();
            mockEnvironment.Setup(x => x.LoadOrder).Returns(mockLoadOrder.Object);

            // Act
            var results = CandidateEnumerator.EnumerateCandidates(
                mockEnvironment.Object,
                excludedPlugins,
                null,
                NullLogger.Instance);

            // Assert
            Assert.DoesNotContain(results, r => r.SourcePlugin == excludedSourcePlugin);
        }

        [Theory]
        [MemberData(nameof(GetNullOrEmptyCollectionTestData))]
        public void EnumerateCandidates_WithNullOrEmptyCollections_ReturnsEmptyResults(
            IEnumerable<IConstructibleObjectGetter>? cobjs, 
            IEnumerable<IWeaponGetter>? weapons)
        {
            // Arrange
            var mockWeapons = new Mock<IMockRecordCollection<IWeaponGetter>>();
            mockWeapons.Setup(x => x.WinningOverrides()).Returns(weapons ?? Enumerable.Empty<IWeaponGetter>());
            mockWeapons.Setup(x => x.GetEnumerator()).Returns(() => (weapons ?? Enumerable.Empty<IWeaponGetter>()).GetEnumerator());

            var mockCobjs = new Mock<IMockRecordCollection<IConstructibleObjectGetter>>();
            mockCobjs.Setup(x => x.WinningOverrides()).Returns(cobjs ?? Enumerable.Empty<IConstructibleObjectGetter>());
            mockCobjs.Setup(x => x.GetEnumerator()).Returns(() => (cobjs ?? Enumerable.Empty<IConstructibleObjectGetter>()).GetEnumerator());

            var mockPriorityOrder = new Mock<IMockPriorityOrder>();
            mockPriorityOrder.Setup(x => x.Weapon()).Returns(mockWeapons.Object);
            mockPriorityOrder.Setup(x => x.ConstructibleObject()).Returns(mockCobjs.Object);

            var mockLoadOrder = new Mock<IMockLoadOrder>();
            mockLoadOrder.Setup(x => x.PriorityOrder).Returns(mockPriorityOrder.Object);

            var mockEnvironment = new Mock<IMockEnvironment>();
            mockEnvironment.Setup(x => x.LoadOrder).Returns(mockLoadOrder.Object);

            var excludedPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Act
            var results = CandidateEnumerator.EnumerateCandidates(
                mockEnvironment.Object,
                excludedPlugins,
                null,
                NullLogger.Instance);

            // Assert
            Assert.Empty(results);
        }

        public static IEnumerable<object[]> GetValidCandidateTestData()
        {
            var weaponFormKey = new MutagenFormKey(new ModKey("TestPlugin", ModType.Plugin), 0x1234);
            var ammoFormKey = new MutagenFormKey(new ModKey("AmmoPlugin", ModType.Plugin), 0xAAAA);

            var mockWeapon = new Mock<IWeaponGetter>();
            mockWeapon.Setup(w => w.FormKey).Returns(weaponFormKey);
            mockWeapon.Setup(w => w.EditorID).Returns("WPN_ED");
            
            var mockAmmoLink = new Mock<IFormLinkGetter<IAmmunitionGetter>>();
            mockAmmoLink.Setup(a => a.FormKey).Returns(ammoFormKey);
            mockAmmoLink.Setup(a => a.IsNull).Returns(false);
            mockWeapon.Setup(w => w.Ammo).Returns(mockAmmoLink.Object);

            var mockCobj = new Mock<IConstructibleObjectGetter>();
            mockCobj.Setup(c => c.FormKey).Returns(new MutagenFormKey(new ModKey("SourcePlugin", ModType.Plugin), 0x1111));
            mockCobj.Setup(c => c.EditorID).Returns("COBJ_ED");
            
            var mockCreatedObjectLink = new Mock<IFormLinkNullableGetter<IConstructibleObjectTargetGetter>>();
            mockCreatedObjectLink.Setup(l => l.FormKey).Returns(weaponFormKey);
            mockCreatedObjectLink.Setup(l => l.IsNull).Returns(false);
            mockCobj.Setup(c => c.CreatedObject).Returns(mockCreatedObjectLink.Object);

            yield return new object[]
            {
                mockWeapon,
                mockCobj,
                "COBJ", // expectedCandidateType
                "SourcePlugin.esp", // expectedSourcePlugin
                "TestPlugin.esp" // expectedWeaponPlugin
            };
        }

        public static IEnumerable<object[]> GetExcludedPluginTestData()
        {
            var weaponFormKey = new MutagenFormKey(new ModKey("TestPlugin", ModType.Plugin), 0x1234);

            var mockWeapon = new Mock<IWeaponGetter>();
            mockWeapon.Setup(w => w.FormKey).Returns(weaponFormKey);
            
            var mockCobj = new Mock<IConstructibleObjectGetter>();
            mockCobj.Setup(c => c.FormKey).Returns(new MutagenFormKey(new ModKey("SourcePlugin", ModType.Plugin), 0x1111));
            
            var mockCreatedObjectLink = new Mock<IFormLinkNullableGetter<IConstructibleObjectTargetGetter>>();
            mockCreatedObjectLink.Setup(l => l.FormKey).Returns(weaponFormKey);
            mockCobj.Setup(c => c.CreatedObject).Returns(mockCreatedObjectLink.Object);

            yield return new object[]
            {
                mockWeapon,
                mockCobj,
                new HashSet<string>(new[] { "SourcePlugin.esp" }, StringComparer.OrdinalIgnoreCase),
                "SourcePlugin.esp" // excludedSourcePlugin
            };
        }

        public static IEnumerable<object?[]> GetNullOrEmptyCollectionTestData()
        {
            yield return new object?[] { null, null };
            yield return new object?[] { new IConstructibleObjectGetter[0], new IWeaponGetter[0] };
        }
    }
}
