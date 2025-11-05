using System.Linq;
using Xunit;
using MunitionAutoPatcher.Services.Implementations;
using MunitionAutoPatcher.ViewModels;
using MunitionAutoPatcher.Services.Interfaces;
using MunitionAutoPatcher.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LinkCacheHelperTests
{
    public class SettingsAndMapperTests
    {
        [Fact]
        public void ConfigService_ExcludedPlugins_PersistAndRestore()
        {
            var cfg = new ConfigService();
            var orig = cfg.GetExcludedPlugins().ToList();
            var testList = new List<string> { "UT_Test_Plugin.esp" };
            try
            {
                cfg.SetExcludedPlugins(testList);
                var got = cfg.GetExcludedPlugins().ToList();
                Assert.Contains("UT_Test_Plugin.esp", got);
            }
            finally
            {
                // restore original
                cfg.SetExcludedPlugins(orig);
            }
        }

        private class DummyOrchestrator : IOrchestrator
        {
            public bool IsInitialized => true;
            public Task<bool> InitializeAsync() => Task.FromResult(true);
            public Task<List<WeaponData>> ExtractWeaponsAsync(IProgress<string>? progress = null) => Task.FromResult(new List<WeaponData>());
            public Task<bool> GenerateMappingsAsync(List<WeaponData> weapons, IProgress<string>? progress = null) => Task.FromResult(true);
            public Task<bool> GenerateIniAsync(string outputPath, List<WeaponMapping> mappings, IProgress<string>? progress = null) => Task.FromResult(true);
            public Task<bool> GeneratePatchAsync(string outputPath, List<WeaponData> weapons, IProgress<string>? progress = null) => Task.FromResult(true);
        }

        private class DummyWeaponsService : IWeaponsService
        {
            public Task<List<WeaponData>> ExtractWeaponsAsync(IProgress<string>? progress = null) => Task.FromResult(new List<WeaponData>());
            public Task<WeaponData?> GetWeaponAsync(FormKey formKey) => Task.FromResult<WeaponData?>(null);
            public List<WeaponData> GetAllWeapons() => new List<WeaponData>();
            public List<AmmoData> GetAllAmmo() => new List<AmmoData>();
        }

        private class DummyOmodExtractor : MunitionAutoPatcher.Services.Interfaces.IWeaponOmodExtractor
        {
            public Task<List<OmodCandidate>> ExtractCandidatesAsync(IProgress<string>? progress = null) => Task.FromResult(new List<OmodCandidate>());
            public Task<List<OmodCandidate>> ExtractCandidatesAsync(IProgress<string>? progress, CancellationToken cancellationToken) => Task.FromResult(new List<OmodCandidate>());
        }

        [Fact]
        public void Mapper_FilteredOmods_ShowAllWhenNoSelection_And_FilterWhenSelected()
        {
            var orchestrator = new DummyOrchestrator();
            var weapons = new DummyWeaponsService();
            var config = new ConfigService();
            var omodExtractor = new DummyOmodExtractor();

            var vm = new MapperViewModel(orchestrator, weapons, config, omodExtractor);

            // Create two candidates, one matching target weapon
            var matching = new OmodCandidate
            {
                BaseWeapon = new FormKey { PluginName = "TestMod.esp", FormId = 0x123 },
                CandidateFormKey = new FormKey { PluginName = "TestMod.esp", FormId = 0x123 },
                CandidateEditorId = "M_WEAPON_TEST",
                CandidateType = "COBJ",
                SourcePlugin = "TestMod.esp"
            };

            var other = new OmodCandidate
            {
                BaseWeapon = new FormKey { PluginName = "OtherMod.esp", FormId = 0x222 },
                CandidateFormKey = new FormKey { PluginName = "OtherMod.esp", FormId = 0x222 },
                CandidateEditorId = "M_OTHER",
                CandidateType = "COBJ",
                SourcePlugin = "OtherMod.esp"
            };

            vm.OmodCandidates.Add(matching);
            vm.OmodCandidates.Add(other);

            // Force update by toggling selection: set a non-null then back to null
            vm.SelectedMapping = new WeaponMappingViewModel { WeaponFormKey = "NoMatch:00000000" };
            vm.SelectedMapping = null;

            // No selection => filtered should include both
            Assert.Contains(matching, vm.FilteredOmodCandidates);
            Assert.Contains(other, vm.FilteredOmodCandidates);

            // Select mapping matching the 'matching' candidate
            vm.SelectedMapping = new WeaponMappingViewModel { WeaponFormKey = matching.BaseWeapon.ToString() };

            // Now filtered should contain only the matching candidate
            Assert.Contains(matching, vm.FilteredOmodCandidates);
            Assert.DoesNotContain(other, vm.FilteredOmodCandidates);
        }
    }
}
