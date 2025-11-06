using System;
using System.Collections.Generic;
using MunitionAutoPatcher.Services.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using MunitionAutoPatcher.Models;
using Xunit;

namespace LinkCacheHelperTests
{
    public class CandidateEnumeratorTests
    {
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

        [Fact]
        public void CandidateEnumerator_Includes_COBJ_CreatedWeapon()
        {
            var weapon = new FakeWeapon();
            weapon.FormKey.ModKey.FileName = "TestPlugin";
            weapon.FormKey.ID = 0x1234;
            weapon.Ammo.FormKey.ModKey.FileName = "AmmoPlugin";
            weapon.Ammo.FormKey.ID = 0xAAAA;

            var cobj = new FakeCOBJ();
            cobj.CreatedObject.FormKey.ModKey.FileName = "TestPlugin";
            cobj.CreatedObject.FormKey.ID = 0x1234;
            cobj.FormKey.ModKey.FileName = "SourcePlugin";
            cobj.FormKey.ID = 0x1111;

            var priority = new FakePriorityOrder(new[] { cobj }, new[] { weapon });
            var env = new FakeEnv(new FakeLoadOrder(priority));

            var results = CandidateEnumerator.EnumerateCandidates(env, new HashSet<string>(StringComparer.OrdinalIgnoreCase), null, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

            Assert.Contains(results, r => r.CandidateType == "COBJ" && r.SourcePlugin == "SourcePlugin" && r.CandidateFormKey.PluginName == "TestPlugin");
        }

        [Fact]
        public void CandidateEnumerator_Skips_ExcludedPlugin()
        {
            var weapon = new FakeWeapon();
            weapon.FormKey.ModKey.FileName = "TestPlugin";
            weapon.FormKey.ID = 0x1234;

            var cobj = new FakeCOBJ();
            cobj.CreatedObject.FormKey.ModKey.FileName = "TestPlugin";
            cobj.CreatedObject.FormKey.ID = 0x1234;
            cobj.FormKey.ModKey.FileName = "SourcePlugin";
            cobj.FormKey.ID = 0x1111;

            var priority = new FakePriorityOrder(new[] { cobj }, new[] { weapon });
            var env = new FakeEnv(new FakeLoadOrder(priority));

            var excluded = new HashSet<string>(new[] { "SourcePlugin" }, StringComparer.OrdinalIgnoreCase);
            var results = CandidateEnumerator.EnumerateCandidates(env, excluded, null, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

            Assert.DoesNotContain(results, r => r.SourcePlugin == "SourcePlugin");
        }
    }
}
