using System.Linq;
using Xunit;
using MunitionAutoPatcher.Services.Implementations;
using MunitionAutoPatcher.ViewModels;
using MunitionAutoPatcher.Services.Interfaces;
using MunitionAutoPatcher.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinkCacheHelperTests
{
    public class SettingsAndMapperTests
    {
        [Fact]
        public void SetExcludedPlugins_WithValidPluginList_PersistsAndRestoresCorrectly()
        {
            // Arrange
            var configService = new ConfigService(NullLogger<ConfigService>.Instance);
            var originalPlugins = configService.GetExcludedPlugins().ToList();
            var testPlugins = new List<string> { "UT_Test_Plugin.esp" };

            try
            {
                // Act
                configService.SetExcludedPlugins(testPlugins);
                var retrievedPlugins = configService.GetExcludedPlugins().ToList();

                // Assert
                Assert.Contains("UT_Test_Plugin.esp", retrievedPlugins);
            }
            finally
            {
                // Cleanup - restore original state
                configService.SetExcludedPlugins(originalPlugins);
            }
        }

        [Theory]
        [MemberData(nameof(GetFilteredOmodTestData))]
        public void FilteredOmodCandidates_WithSelectedMapping_FiltersCorrectly(
            List<OmodCandidate> candidates,
            WeaponMappingViewModel? selectedMapping,
            int expectedFilteredCount,
            string[] expectedCandidateIds)
        {
            // Arrange
            var mockOrchestrator = new Mock<IOrchestrator>();
            mockOrchestrator.Setup(x => x.IsInitialized).Returns(true);
            mockOrchestrator.Setup(x => x.InitializeAsync()).Returns(Task.FromResult(true));
            mockOrchestrator.Setup(x => x.ExtractWeaponsAsync(It.IsAny<IProgress<string>?>())).Returns(Task.FromResult(new List<WeaponData>()));
            mockOrchestrator.Setup(x => x.GenerateMappingsAsync(It.IsAny<List<WeaponData>>(), It.IsAny<IProgress<string>?>())).Returns(Task.FromResult(true));
            mockOrchestrator.Setup(x => x.GenerateIniAsync(It.IsAny<string>(), It.IsAny<List<WeaponMapping>>(), It.IsAny<IProgress<string>?>())).Returns(Task.FromResult(true));
            mockOrchestrator.Setup(x => x.GeneratePatchAsync(It.IsAny<string>(), It.IsAny<List<WeaponData>>(), It.IsAny<IProgress<string>?>())).Returns(Task.FromResult(true));

            var mockWeaponsService = new Mock<IWeaponsService>();
            mockWeaponsService.Setup(x => x.ExtractWeaponsAsync(It.IsAny<IProgress<string>?>())).Returns(Task.FromResult(new List<WeaponData>()));
            mockWeaponsService.Setup(x => x.GetWeaponAsync(It.IsAny<FormKey>())).Returns(Task.FromResult<WeaponData?>(null));
            mockWeaponsService.Setup(x => x.GetAllWeapons()).Returns(new List<WeaponData>());
            mockWeaponsService.Setup(x => x.GetAllAmmo()).Returns(new List<AmmoData>());

            var configService = new ConfigService(NullLogger<ConfigService>.Instance);

            var mockOmodExtractor = new Mock<IWeaponOmodExtractor>();
            mockOmodExtractor.Setup(x => x.ExtractCandidatesAsync(It.IsAny<IProgress<string>?>())).Returns(Task.FromResult(new List<OmodCandidate>()));
            mockOmodExtractor.Setup(x => x.ExtractCandidatesAsync(It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(new List<OmodCandidate>()));

            var viewModel = new MapperViewModel(mockOrchestrator.Object, mockWeaponsService.Object, configService, mockOmodExtractor.Object);

            // Add test candidates
            foreach (var candidate in candidates)
            {
                viewModel.OmodCandidates.Add(candidate);
            }

            // Act
            if (selectedMapping != null)
            {
                // Force update by toggling selection: set a non-null then the target selection
                viewModel.SelectedMapping = new WeaponMappingViewModel { WeaponFormKey = "NoMatch:00000000" };
                viewModel.SelectedMapping = selectedMapping;
            }
            else
            {
                // Force update by toggling selection: set a non-null then back to null
                viewModel.SelectedMapping = new WeaponMappingViewModel { WeaponFormKey = "NoMatch:00000000" };
                viewModel.SelectedMapping = null;
            }

            // Assert
            Assert.Equal(expectedFilteredCount, viewModel.FilteredOmodCandidates.Count);
            foreach (var expectedId in expectedCandidateIds)
            {
                Assert.Contains(viewModel.FilteredOmodCandidates, c => c.CandidateEditorId == expectedId);
            }
        }

        [Theory]
        [MemberData(nameof(GetNullOrEmptyPluginListTestData))]
        public void SetExcludedPlugins_WithNullOrEmptyList_HandlesGracefully(List<string> pluginList)
        {
            // Arrange
            var configService = new ConfigService(NullLogger<ConfigService>.Instance);
            var originalPlugins = configService.GetExcludedPlugins().ToList();

            try
            {
                // Act & Assert - should not throw
                var exception = Record.Exception(() => configService.SetExcludedPlugins(pluginList));
                Assert.Null(exception);
            }
            finally
            {
                // Cleanup - restore original state
                configService.SetExcludedPlugins(originalPlugins);
            }
        }

        public static IEnumerable<object?[]> GetFilteredOmodTestData()
        {
            var matchingCandidate = new OmodCandidate
            {
                BaseWeapon = new FormKey { PluginName = "TestMod.esp", FormId = 0x123 },
                CandidateFormKey = new FormKey { PluginName = "TestMod.esp", FormId = 0x123 },
                CandidateEditorId = "M_WEAPON_TEST",
                CandidateType = "COBJ",
                SourcePlugin = "TestMod.esp"
            };

            var otherCandidate = new OmodCandidate
            {
                BaseWeapon = new FormKey { PluginName = "OtherMod.esp", FormId = 0x222 },
                CandidateFormKey = new FormKey { PluginName = "OtherMod.esp", FormId = 0x222 },
                CandidateEditorId = "M_OTHER",
                CandidateType = "COBJ",
                SourcePlugin = "OtherMod.esp"
            };

            // Test case: No selection - should show all candidates
            yield return new object?[]
            {
                new List<OmodCandidate> { matchingCandidate, otherCandidate },
                null, // no selection
                2, // expected count
                new string[] { "M_WEAPON_TEST", "M_OTHER" } // expected IDs
            };

            // Test case: Selection matches one candidate - should show only matching
            yield return new object?[]
            {
                new List<OmodCandidate> { matchingCandidate, otherCandidate },
                new WeaponMappingViewModel { WeaponFormKey = matchingCandidate.BaseWeapon.ToString() },
                1, // expected count
                new string[] { "M_WEAPON_TEST" } // expected IDs
            };
        }

        public static IEnumerable<object[]> GetNullOrEmptyPluginListTestData()
        {
            yield return new object[] { new List<string>() }; // empty list
            yield return new object[] { new List<string> { "" } }; // list with empty string
        }
    }
}
