using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Implementations;
using MunitionAutoPatcher.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace WeaponDataExtractorTestsProject
{
    public class EspPatchServiceTests
    {
        private class FakePathService : IPathService
        {
            private readonly string _outputDir;

            public FakePathService(string outputDir)
            {
                _outputDir = outputDir;
            }

            public string GetRepoRoot() => Path.GetTempPath();
            public string GetArtifactsDirectory() => Path.Combine(Path.GetTempPath(), "artifacts");
            public string GetOutputDirectory() => _outputDir;
        }

        private class FakeDiagnosticWriter : IDiagnosticWriter
        {
            public void WriteStartMarker(ExtractionContext ctx) { }
            public void WriteDetectorSelected(string name, ExtractionContext ctx) { }
            public void WriteReverseMapMarker(ExtractionContext ctx) { }
            public void WriteDetectionPassMarker(ExtractionContext ctx) { }
            public void WriteResultsCsv(IEnumerable<OmodCandidate> confirmed, ExtractionContext ctx) { }
            public void WriteZeroReferenceReport(IEnumerable<OmodCandidate> candidates, ExtractionContext ctx) { }
            public void WriteCompletionMarker(ExtractionContext ctx) { }
        }

        [Fact]
        public async Task BuildAsync_NoConfirmedCandidates_SkipsGeneration()
        {
            // Arrange
            var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(outputDir);

            try
            {
                var pathService = new FakePathService(outputDir);
                var diagnosticWriter = new FakeDiagnosticWriter();
                var logger = NullLogger<EspPatchService>.Instance;
                var service = new EspPatchService(pathService, diagnosticWriter, logger);

                var candidates = new List<OmodCandidate>
                {
                    new OmodCandidate { ConfirmedAmmoChange = false }
                };

                var extraction = new ExtractionContext();
                var ct = CancellationToken.None;

                // Act
                await service.BuildAsync(candidates, extraction, ct);

                // Assert
                var patchPath = Path.Combine(outputDir, "MunitionAutoPatcher_Patch.esp");
                Assert.False(File.Exists(patchPath), "Patch file should not be created when no candidates are confirmed");
            }
            finally
            {
                if (Directory.Exists(outputDir))
                {
                    try { Directory.Delete(outputDir, true); } catch { }
                }
            }
        }

        [Fact]
        public async Task BuildAsync_NullEnvironment_ThrowsException()
        {
            // Arrange
            var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(outputDir);

            try
            {
                var pathService = new FakePathService(outputDir);
                var diagnosticWriter = new FakeDiagnosticWriter();
                var logger = NullLogger<EspPatchService>.Instance;
                var service = new EspPatchService(pathService, diagnosticWriter, logger);

                var candidates = new List<OmodCandidate>
                {
                    new OmodCandidate
                    {
                        ConfirmedAmmoChange = true,
                        CandidateFormKey = new FormKey { PluginName = "Test.esp", FormId = 0x1234 },
                        CandidateAmmo = new FormKey { PluginName = "Ammo.esp", FormId = 0x5678 }
                    }
                };

                var extraction = new ExtractionContext { Environment = null };
                var ct = CancellationToken.None;

                // Act & Assert
                await Assert.ThrowsAsync<InvalidOperationException>(
                    async () => await service.BuildAsync(candidates, extraction, ct));
            }
            finally
            {
                if (Directory.Exists(outputDir))
                {
                    try { Directory.Delete(outputDir, true); } catch { }
                }
            }
        }

        // NOTE: Full integration test with Mutagen LinkCache would require actual game data files
        // and is best run manually in a Windows environment with Fallout 4 installed.
        // The above tests validate the basic logic without requiring game files.
    }
}
