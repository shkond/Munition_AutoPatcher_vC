using System.Reflection;
using Microsoft.Extensions.Logging;
using MunitionAutoPatcher.Services.Interfaces;

namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// Factory that returns a best-fit IAmmunitionChangeDetector implementation
/// based on the detected Mutagen assembly or configuration.
/// </summary>
public static class DetectorFactory
{
    /// <summary>
    /// Selects the appropriate detector given a Mutagen assembly name. Currently
    /// returns a reflection-based fallback detector for unknown versions. Logs
    /// selection via structured `ILogger` (created from the provided `ILoggerFactory`).
    /// </summary>
    public static IAmmunitionChangeDetector GetDetector(AssemblyName? mutagenAssembly, ILoggerFactory loggerFactory)
    {
        if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));
        var logger = loggerFactory.CreateLogger(nameof(DetectorFactory));
        try
        {
            var asmInfo = mutagenAssembly?.Name ?? "(unknown)";
            var version = mutagenAssembly?.Version?.ToString() ?? "(unknown)";
            logger.LogInformation("DetectorFactory: selecting detector for Mutagen assembly {Assembly} v{Version}", asmInfo, version);

            // Example logic: if later we add version-specific detectors, branch here
            if (mutagenAssembly != null && mutagenAssembly.Version != null)
            {
                var v = mutagenAssembly.Version;
                // If we detect Mutagen v0.51, return a tuned detector
                if (v.Major == 0 && v.Minor == 51)
                {
                    try
                    {
                        logger.LogInformation("DetectorFactory: selecting MutagenV51Detector for detected Mutagen v0.51 runtime");
                        return new MutagenV51Detector(loggerFactory.CreateLogger<MutagenV51Detector>(), loggerFactory);
                    }
                    catch (Exception ex)
                    {
                        // Persist a marker so it's obvious in artifacts that the optimized detector failed and a fallback was used.
                        try
                        {
                            var repoRoot = MunitionAutoPatcher.Utilities.RepoUtils.FindRepoRoot();
                            var artifactsDir = System.IO.Path.Combine(repoRoot ?? string.Empty, "artifacts");
                            try { System.IO.Directory.CreateDirectory(artifactsDir); } catch (Exception dirEx) { logger.LogWarning(dirEx, "DetectorFactory: failed to create artifacts directory"); }
                            var marker = System.IO.Path.Combine(artifactsDir, $"detector_fallback_marker_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                            try { System.IO.File.WriteAllText(marker, $"MutagenV51Detector construction failed: {ex}\n"); } catch (Exception fileEx) { logger.LogWarning(fileEx, "DetectorFactory: failed to write detector fallback marker"); }
                        }
                        catch (Exception innerEx)
                        {
                            logger.LogWarning(innerEx, "DetectorFactory: failed while attempting to write detector fallback marker");
                        }
                        logger.LogWarning(ex, "DetectorFactory: failed to construct MutagenV51Detector, falling back");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DetectorFactory: failed during selection, using fallback");
        }

        return new ReflectionFallbackDetector(loggerFactory.CreateLogger<ReflectionFallbackDetector>());
    }
}
