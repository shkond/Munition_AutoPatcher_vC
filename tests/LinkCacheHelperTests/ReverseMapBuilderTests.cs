using System;
using System.Collections.Generic;
using System.Linq;
using MunitionAutoPatcher.Services.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LinkCacheHelperTests
{
    public class ReverseMapBuilderTests
    {
        private class FakeModKey { public string FileName { get; set; } = string.Empty; }
        private class FakeFormKey { public FakeModKey ModKey { get; set; } = new FakeModKey(); public uint ID { get; set; } }
        private class FakeNested { public FakeFormKey FormKey { get; set; } = new FakeFormKey(); }
        private class FakeRecord
        {
            public FakeFormKey FormKey { get; set; } = new FakeFormKey();
            public FakeNested SomeProp { get; set; } = new FakeNested();
            public string EditorID { get; set; } = "EDID";
        }

        private class FakePriorityRoot
        {
            private readonly IEnumerable<object> _items;
            public FakePriorityRoot(IEnumerable<object> items) => _items = items;
            // Method matches the pattern: public, no-arg, returns IEnumerable
            public IEnumerable<object> SomeCollection() => _items;
        }

        [Fact]
        public void Build_IncludesEntries_ForNestedFormKey()
        {
            var rec = new FakeRecord();
            rec.SomeProp.FormKey.ModKey.FileName = "TestPlugin";
            rec.SomeProp.FormKey.ID = 0x1234;

            var root = new FakePriorityRoot(new[] { rec });
            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var map = ReverseMapBuilder.Build(root, excluded, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

            var key = $"TestPlugin:{rec.SomeProp.FormKey.ID:X8}";
            Assert.True(map.ContainsKey(key));
            var list = map[key];
            Assert.Contains(list, t => t.Record == rec && t.PropName == "SomeProp");
        }

        [Fact]
        public void Build_Skips_ExcludedPlugin()
        {
            var rec = new FakeRecord();
            rec.SomeProp.FormKey.ModKey.FileName = "ExcludedPlugin";
            rec.SomeProp.FormKey.ID = 0x9999;

            var root = new FakePriorityRoot(new[] { rec });
            var excluded = new HashSet<string>(new[] { "ExcludedPlugin" }, StringComparer.OrdinalIgnoreCase);

            var map = ReverseMapBuilder.Build(root, excluded, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

            var key = $"ExcludedPlugin:{rec.SomeProp.FormKey.ID:X8}";
            Assert.False(map.ContainsKey(key));
        }
    }
}
