using System;
using System.Collections.Generic;
using System.Linq;
using MunitionAutoPatcher.Services.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using MunitionAutoPatcher.Models;
using Xunit;

namespace LinkCacheHelperTests
{
    public class CandidateEnumeratorTests
    {
        // Test data classes - keeping these as they represent domain objects rather than services
        public class FakeModKey { public string FileName { get; set; } = string.Empty; }
        public class FakeFormKey { public FakeModKey ModKey { get; set; } = new FakeModKey(); public uint ID { get; set; } }
        public class FakeFormLink { public bool IsNull { get; set; } = false; public FakeFormKey FormKey { get; set; } = new FakeFormKey(); }
        public class FakeCOBJ { public FakeFormLink CreatedObject { get; set; } = new FakeFormLink(); public FakeFormKey FormKey { get; set; } = new FakeFormKey(); public string EditorID { get; set; } = "COBJ_ED"; }
        public class FakeWeapon { public FakeFormKey FormKey { get; set; } = new FakeFormKey(); public string EditorID { get; set; } = "WPN_ED"; public FakeFormLink Ammo { get; set; } = new FakeFormLink(); }

        public class FakePriorityOrder
        {
            private readonly IEnumerable<FakeCOBJ> _cobjs;
            private readonly IEnumerable<FakeWeapon> _weapons;
            public FakePriorityOrder(IEnumerable<FakeCOBJ> cobjs, IEnumerable<FakeWeapon> weapons) { _cobjs = cobjs; _weapons = weapons; }
            public IEnumerable<FakeCOBJ> ConstructibleObject() => _cobjs;
            public IEnumerable<FakeWeapon> Weapon() => _weapons;
            // Other collections omitted
        }
        public class FakeLoadOrder { public FakePriorityOrder PriorityOrder { get; set; } public FakeLoadOrder(FakePriorityOrder p) { PriorityOrder = p; } }
        public class FakeEnv { public FakeLoadOrder LoadOrder { get; set; } public FakeEnv(FakeLoadOrder l) { LoadOrder = l; } }

        [Theory]
        [MemberData(nameof(GetValidCandidateTestData))]
        public void EnumerateCandidates_WithValidWeaponAndCobj_IncludesExpectedCandidate(
            FakeWeapon weapon,
            FakeCOBJ cobj,
            string expectedCandidateType,
            string expectedSourcePlugin,
            string expectedWeaponPlugin)
        {
            // Arrange
            var priorityOrder = new FakePriorityOrder(new[] { cobj }, new[] { weapon });
            var environment = new FakeEnv(new FakeLoadOrder(priorityOrder));
            var excludedPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Act
            var results = CandidateEnumerator.EnumerateCandidates(
                environment, 
                excludedPlugins, 
                null, 
                NullLogger.Instance);

            // Assert
            Assert.Contains(results, r => 
                r.CandidateType == expectedCandidateType && 
                r.SourcePlugin == expectedSourcePlugin && 
                r.CandidateFormKey.PluginName == expectedWeaponPlugin);
        }

        [Theory]
        [MemberData(nameof(GetExcludedPluginTestData))]
        public void EnumerateCandidates_WithExcludedPlugin_SkipsExcludedCandidate(
            FakeWeapon weapon,
            FakeCOBJ cobj,
            HashSet<string> excludedPlugins,
            string excludedSourcePlugin)
        {
            // Arrange
            var priorityOrder = new FakePriorityOrder(new[] { cobj }, new[] { weapon });
            var environment = new FakeEnv(new FakeLoadOrder(priorityOrder));

            // Act
            var results = CandidateEnumerator.EnumerateCandidates(
                environment, 
                excludedPlugins, 
                null, 
                NullLogger.Instance);

            // Assert
            Assert.DoesNotContain(results, r => r.SourcePlugin == excludedSourcePlugin);
        }

        [Theory]
        [MemberData(nameof(GetNullOrEmptyCollectionTestData))]
        public void EnumerateCandidates_WithNullOrEmptyCollections_ReturnsEmptyResults(FakeCOBJ[] cobjs, FakeWeapon[] weapons)
        {
            // Arrange
            var priorityOrder = new FakePriorityOrder(cobjs ?? new FakeCOBJ[0], weapons ?? new FakeWeapon[0]);
            var environment = new FakeEnv(new FakeLoadOrder(priorityOrder));
            var excludedPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Act
            var results = CandidateEnumerator.EnumerateCandidates(
                environment, 
                excludedPlugins, 
                null, 
                NullLogger.Instance);

            // Assert
            Assert.Empty(results);
        }

        public static IEnumerable<object[]> GetValidCandidateTestData()
        {
            var weapon = new FakeWeapon
            {
                FormKey = new FakeFormKey 
                { 
                    ModKey = new FakeModKey { FileName = "TestPlugin" }, 
                    ID = 0x1234 
                },
                Ammo = new FakeFormLink 
                { 
                    FormKey = new FakeFormKey 
                    { 
                        ModKey = new FakeModKey { FileName = "AmmoPlugin" }, 
                        ID = 0xAAAA 
                    } 
                }
            };

            var cobj = new FakeCOBJ
            {
                CreatedObject = new FakeFormLink 
                { 
                    FormKey = new FakeFormKey 
                    { 
                        ModKey = new FakeModKey { FileName = "TestPlugin" }, 
                        ID = 0x1234 
                    } 
                },
                FormKey = new FakeFormKey 
                { 
                    ModKey = new FakeModKey { FileName = "SourcePlugin" }, 
                    ID = 0x1111 
                }
            };

            yield return new object[]
            {
                weapon,
                cobj,
                "COBJ", // expectedCandidateType
                "SourcePlugin", // expectedSourcePlugin
                "TestPlugin" // expectedWeaponPlugin
            };
        }

        public static IEnumerable<object[]> GetExcludedPluginTestData()
        {
            var weapon = new FakeWeapon
            {
                FormKey = new FakeFormKey 
                { 
                    ModKey = new FakeModKey { FileName = "TestPlugin" }, 
                    ID = 0x1234 
                }
            };

            var cobj = new FakeCOBJ
            {
                CreatedObject = new FakeFormLink 
                { 
                    FormKey = new FakeFormKey 
                    { 
                        ModKey = new FakeModKey { FileName = "TestPlugin" }, 
                        ID = 0x1234 
                    } 
                },
                FormKey = new FakeFormKey 
                { 
                    ModKey = new FakeModKey { FileName = "SourcePlugin" }, 
                    ID = 0x1111 
                }
            };

            yield return new object[]
            {
                weapon,
                cobj,
                new HashSet<string>(new[] { "SourcePlugin" }, StringComparer.OrdinalIgnoreCase),
                "SourcePlugin" // excludedSourcePlugin
            };
        }

        public static IEnumerable<object[]> GetNullOrEmptyCollectionTestData()
        {
            yield return new object[] { null, null };
            yield return new object[] { new FakeCOBJ[0], new FakeWeapon[0] };
        }
    }
}
