using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LinkCacheHelperTests
{
    public class DiagnosticWriterTests : IDisposable
    {
        private readonly string _tmpRoot;

        public DiagnosticWriterTests()
        {
            _tmpRoot = Path.Combine(Path.GetTempPath(), "DiagWriterTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tmpRoot);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_tmpRoot)) Directory.Delete(_tmpRoot, true); } catch { }
        }

        public class FakeModKey { public string FileName { get; set; } = string.Empty; }
        public class FakeFormKey { public FakeModKey ModKey { get; set; } = new FakeModKey(); public uint ID { get; set; } }
        private class FakeSourceRecord { public FakeFormKey FormKey { get; set; } = new FakeFormKey(); }
        public class FakeWeapon { public FakeFormKey FormKey { get; set; } = new FakeFormKey(); public string EditorID { get; set; } = string.Empty; }

        [Fact]
        public void WriteReverseMapMarker_CreatesMarkerFile()
        {
            var reverseMap = new Dictionary<string, List<(object Record, string PropName, object PropValue)>>(StringComparer.OrdinalIgnoreCase);
            var marker = DiagnosticWriter.WriteReverseMapMarker(reverseMap, _tmpRoot, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

            Assert.True(File.Exists(marker));
            var content = File.ReadAllText(marker);
            Assert.Contains("Reverse reference map build attempted", content);
            Assert.Contains("ReverseMapKeys=0", content);
        }

        [Fact]
        public void WriteNoveskeDiagnostic_WritesCsvWithExpectedRow()
        {
            // Arrange: craft a fake weapon and a reverseMap entry that should match
            var weapon = new FakeWeapon();
            weapon.FormKey.ModKey.FileName = "TestPlugin";
            weapon.FormKey.ID = 0x1234;
            weapon.EditorID = "WPN_EDID";

            var weaponsList = new[] { (object)weapon };

            // reverse map key matches plugin:ID format
            var key = $"TestPlugin:{weapon.FormKey.ID:X8}";
            var srcRec = new FakeSourceRecord();
            srcRec.FormKey.ModKey.FileName = "SourcePlugin";
            srcRec.FormKey.ID = 0xAAAA;

            var reverseMap = new Dictionary<string, List<(object, string, object)>>(StringComparer.OrdinalIgnoreCase)
            {
                [key] = new List<(object, string, object)> { (srcRec, "SomeProp", new object()) }
            };

            // results: include one confirmed candidate for the base weapon
            var candidate = new OmodCandidate { BaseWeapon = new FormKey { PluginName = "TestPlugin", FormId = weapon.FormKey.ID }, ConfirmedAmmoChange = true };
            var results = new[] { candidate };

            // Act
            var diagFile = DiagnosticWriter.WriteNoveskeDiagnostic(reverseMap, weaponsList, results, Array.Empty<string>(), _tmpRoot, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

            // Assert
            Assert.True(File.Exists(diagFile));
            var lines = File.ReadAllLines(diagFile).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            // header + one data row expected
            Assert.True(lines.Length >= 2, "Expected at least header and one data row");
            Assert.Contains("WeaponFormKey,EditorId,ReverseRefCount", lines[0]);
            // find row that starts with our key
            var dataRow = lines.FirstOrDefault(l => l.StartsWith(key, StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(dataRow);
            var fields = dataRow.Split(',');
            Assert.Equal(5, fields.Length); // Expecting 5 fields: WeaponFormKey,EditorId,ReverseRefCount,ReverseSourcePlugins,ConfirmedCandidatesCount
            Assert.Equal(key, fields[0]);
            Assert.Equal("WPN_EDID", fields[1]);
            Assert.Equal("1", fields[2]); // ReverseRefCount
            Assert.Equal("\"SourcePlugin\"", fields[3]); // ReverseSourcePlugins (quoted)
            Assert.Equal("1", fields[4]); // ConfirmedCandidatesCount
        }
    }
}
