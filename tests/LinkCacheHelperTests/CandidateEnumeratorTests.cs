using System;
using System.Collections.Generic;
using System.Linq;
using MunitionAutoPatcher.Services.Implementations;
using MunitionAutoPatcher.Services.Interfaces;
using MunitionAutoPatcher.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Moq;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Records;
using MutagenFormKey = Mutagen.Bethesda.Plugins.FormKey;

namespace LinkCacheHelperTests
{
    /// <summary>
    /// Unit tests for CobjCandidateProvider.
    /// Tests the type-safe COBJ candidate extraction via IMutagenAccessor.
    /// </summary>
    public class CobjCandidateProviderTests
    {
        private readonly Mock<IMutagenAccessor> _mockAccessor;
        private readonly Mock<ILogger<CobjCandidateProvider>> _mockLogger;
        private readonly CobjCandidateProvider _provider;

        public CobjCandidateProviderTests()
        {
            _mockAccessor = new Mock<IMutagenAccessor>();
            _mockLogger = new Mock<ILogger<CobjCandidateProvider>>();
            _provider = new CobjCandidateProvider(_mockAccessor.Object, _mockLogger.Object);
        }

        [Fact]
        public void GetCandidates_WithNullEnvironment_ReturnsEmptyList()
        {
            // Arrange
            var context = new ExtractionContext
            {
                Environment = null,
                ExcludedPlugins = new HashSet<string>()
            };

            // Act
            var results = _provider.GetCandidates(context).ToList();

            // Assert
            Assert.Empty(results);
            _mockLogger.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Environment is null")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void GetCandidates_WithValidWeaponCobj_ReturnsCandidateWithCorrectType()
        {
            // Arrange
            var weaponFormKey = new MutagenFormKey(new ModKey("TestPlugin", ModType.Plugin), 0x1234);
            var cobjFormKey = new MutagenFormKey(new ModKey("SourcePlugin", ModType.Plugin), 0x1111);
            var ammoFormKey = new MutagenFormKey(new ModKey("AmmoPlugin", ModType.Plugin), 0xAAAA);

            var mockWeapon = CreateMockWeapon(weaponFormKey, "WPN_Test", ammoFormKey);
            var mockCobj = CreateMockCobj(cobjFormKey, weaponFormKey, "COBJ_Test");

            var mockEnv = new Mock<IResourcedMutagenEnvironment>();

            _mockAccessor.Setup(a => a.GetWinningConstructibleObjectOverridesTyped(mockEnv.Object))
                .Returns(new[] { mockCobj.Object });
            _mockAccessor.Setup(a => a.GetWinningWeaponOverridesTyped(mockEnv.Object))
                .Returns(new[] { mockWeapon.Object });
            _mockAccessor.Setup(a => a.BuildConcreteLinkCache(mockEnv.Object))
                .Returns((ILinkCache?)null);

            var context = new ExtractionContext
            {
                Environment = mockEnv.Object,
                ExcludedPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            };

            // Act
            var results = _provider.GetCandidates(context).ToList();

            // Assert
            Assert.Single(results);
            var candidate = results[0];
            Assert.Equal("COBJ", candidate.CandidateType);
            Assert.Equal("SourcePlugin.esp", candidate.SourcePlugin);
            Assert.Equal("TestPlugin.esp", candidate.BaseWeapon?.PluginName);
            Assert.Equal(0x1234u, candidate.BaseWeapon?.FormId);
            Assert.Equal("WPN_Test", candidate.BaseWeaponEditorId);
            Assert.Equal("COBJ_Test", candidate.CandidateEditorId);
        }

        [Fact]
        public void GetCandidates_WithExcludedPlugin_SkipsCandidate()
        {
            // Arrange
            var weaponFormKey = new MutagenFormKey(new ModKey("TestPlugin", ModType.Plugin), 0x1234);
            var cobjFormKey = new MutagenFormKey(new ModKey("ExcludedPlugin", ModType.Plugin), 0x1111);

            var mockWeapon = CreateMockWeapon(weaponFormKey, "WPN_Test", null);
            var mockCobj = CreateMockCobj(cobjFormKey, weaponFormKey, "COBJ_Test");

            var mockEnv = new Mock<IResourcedMutagenEnvironment>();

            _mockAccessor.Setup(a => a.GetWinningConstructibleObjectOverridesTyped(mockEnv.Object))
                .Returns(new[] { mockCobj.Object });
            _mockAccessor.Setup(a => a.GetWinningWeaponOverridesTyped(mockEnv.Object))
                .Returns(new[] { mockWeapon.Object });

            var context = new ExtractionContext
            {
                Environment = mockEnv.Object,
                ExcludedPlugins = new HashSet<string>(new[] { "ExcludedPlugin.esp" }, StringComparer.OrdinalIgnoreCase)
            };

            // Act
            var results = _provider.GetCandidates(context).ToList();

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public void GetCandidates_WithNullCreatedObject_SkipsRecord()
        {
            // Arrange
            var cobjFormKey = new MutagenFormKey(new ModKey("SourcePlugin", ModType.Plugin), 0x1111);

            var mockCobj = new Mock<IConstructibleObjectGetter>();
            mockCobj.Setup(c => c.FormKey).Returns(cobjFormKey);
            mockCobj.Setup(c => c.EditorID).Returns("COBJ_NoWeapon");

            var mockCreatedObjectLink = new Mock<IFormLinkNullableGetter<IConstructibleObjectTargetGetter>>();
            mockCreatedObjectLink.Setup(l => l.IsNull).Returns(true);
            mockCobj.Setup(c => c.CreatedObject).Returns(mockCreatedObjectLink.Object);

            var mockEnv = new Mock<IResourcedMutagenEnvironment>();

            _mockAccessor.Setup(a => a.GetWinningConstructibleObjectOverridesTyped(mockEnv.Object))
                .Returns(new[] { mockCobj.Object });
            _mockAccessor.Setup(a => a.GetWinningWeaponOverridesTyped(mockEnv.Object))
                .Returns(Enumerable.Empty<IWeaponGetter>());

            var context = new ExtractionContext
            {
                Environment = mockEnv.Object,
                ExcludedPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            };

            // Act
            var results = _provider.GetCandidates(context).ToList();

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public void GetCandidates_WithNonWeaponCreatedObject_SkipsRecord()
        {
            // Arrange
            var nonWeaponFormKey = new MutagenFormKey(new ModKey("OtherPlugin", ModType.Plugin), 0x9999);
            var cobjFormKey = new MutagenFormKey(new ModKey("SourcePlugin", ModType.Plugin), 0x1111);

            // COBJ creates something that is NOT a weapon
            var mockCobj = CreateMockCobj(cobjFormKey, nonWeaponFormKey, "COBJ_NonWeapon");

            var mockEnv = new Mock<IResourcedMutagenEnvironment>();

            _mockAccessor.Setup(a => a.GetWinningConstructibleObjectOverridesTyped(mockEnv.Object))
                .Returns(new[] { mockCobj.Object });
            _mockAccessor.Setup(a => a.GetWinningWeaponOverridesTyped(mockEnv.Object))
                .Returns(Enumerable.Empty<IWeaponGetter>()); // No weapons in load order

            var context = new ExtractionContext
            {
                Environment = mockEnv.Object,
                ExcludedPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            };

            // Act
            var results = _provider.GetCandidates(context).ToList();

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public void GetCandidates_WithCancellationRequested_ThrowsOperationCanceledException()
        {
            // Arrange
            var cts = new System.Threading.CancellationTokenSource();
            cts.Cancel();

            var mockEnv = new Mock<IResourcedMutagenEnvironment>();

            _mockAccessor.Setup(a => a.GetWinningConstructibleObjectOverridesTyped(mockEnv.Object))
                .Returns(Enumerable.Empty<IConstructibleObjectGetter>());
            _mockAccessor.Setup(a => a.GetWinningWeaponOverridesTyped(mockEnv.Object))
                .Returns(Enumerable.Empty<IWeaponGetter>());

            var context = new ExtractionContext
            {
                Environment = mockEnv.Object,
                ExcludedPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                CancellationToken = cts.Token
            };

            // Act & Assert
            Assert.Throws<OperationCanceledException>(() => _provider.GetCandidates(context).ToList());
        }

        [Fact]
        public void GetCandidates_WithAmmo_IncludesAmmoInCandidate()
        {
            // Arrange
            var weaponFormKey = new MutagenFormKey(new ModKey("TestPlugin", ModType.Plugin), 0x1234);
            var cobjFormKey = new MutagenFormKey(new ModKey("SourcePlugin", ModType.Plugin), 0x1111);
            var ammoFormKey = new MutagenFormKey(new ModKey("AmmoPlugin", ModType.Plugin), 0xAAAA);

            var mockWeapon = CreateMockWeapon(weaponFormKey, "WPN_Test", ammoFormKey);
            var mockCobj = CreateMockCobj(cobjFormKey, weaponFormKey, "COBJ_Test");

            var mockEnv = new Mock<IResourcedMutagenEnvironment>();

            _mockAccessor.Setup(a => a.GetWinningConstructibleObjectOverridesTyped(mockEnv.Object))
                .Returns(new[] { mockCobj.Object });
            _mockAccessor.Setup(a => a.GetWinningWeaponOverridesTyped(mockEnv.Object))
                .Returns(new[] { mockWeapon.Object });
            _mockAccessor.Setup(a => a.BuildConcreteLinkCache(mockEnv.Object))
                .Returns((ILinkCache?)null);

            var context = new ExtractionContext
            {
                Environment = mockEnv.Object,
                ExcludedPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            };

            // Act
            var results = _provider.GetCandidates(context).ToList();

            // Assert
            Assert.Single(results);
            var candidate = results[0];
            Assert.NotNull(candidate.CandidateAmmo);
            Assert.Equal("AmmoPlugin.esp", candidate.CandidateAmmo!.PluginName);
            Assert.Equal(0xAAAAu, candidate.CandidateAmmo.FormId);
        }

        #region Helper Methods

        private Mock<IWeaponGetter> CreateMockWeapon(MutagenFormKey formKey, string editorId, MutagenFormKey? ammoFormKey)
        {
            var mockWeapon = new Mock<IWeaponGetter>();
            mockWeapon.Setup(w => w.FormKey).Returns(formKey);
            mockWeapon.Setup(w => w.EditorID).Returns(editorId);

            var mockAmmoLink = new Mock<IFormLinkGetter<IAmmunitionGetter>>();
            if (ammoFormKey.HasValue)
            {
                mockAmmoLink.Setup(a => a.FormKey).Returns(ammoFormKey.Value);
                mockAmmoLink.Setup(a => a.IsNull).Returns(false);
            }
            else
            {
                mockAmmoLink.Setup(a => a.IsNull).Returns(true);
            }
            mockWeapon.Setup(w => w.Ammo).Returns(mockAmmoLink.Object);

            return mockWeapon;
        }

        private Mock<IConstructibleObjectGetter> CreateMockCobj(MutagenFormKey cobjFormKey, MutagenFormKey createdObjectFormKey, string editorId)
        {
            var mockCobj = new Mock<IConstructibleObjectGetter>();
            mockCobj.Setup(c => c.FormKey).Returns(cobjFormKey);
            mockCobj.Setup(c => c.EditorID).Returns(editorId);

            var mockCreatedObjectLink = new Mock<IFormLinkNullableGetter<IConstructibleObjectTargetGetter>>();
            mockCreatedObjectLink.Setup(l => l.FormKey).Returns(createdObjectFormKey);
            mockCreatedObjectLink.Setup(l => l.IsNull).Returns(false);
            mockCobj.Setup(c => c.CreatedObject).Returns(mockCreatedObjectLink.Object);

            return mockCobj;
        }

        #endregion
    }
}
