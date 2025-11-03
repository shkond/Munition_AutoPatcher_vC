using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text;

namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// Service responsible for writing diagnostic markers, CSV outputs, and extraction reports.
/// </summary>
public class DiagnosticWriter : IDiagnosticWriter
{
    private readonly IPathService _pathService;
    private readonly ILogger<DiagnosticWriter> _logger;

    public DiagnosticWriter(IPathService pathService, ILogger<DiagnosticWriter> logger)
    {
        _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public void WriteStartMarker(ExtractionContext ctx)
    {
        try
        {
            var path = WriteDiagnosticsMarker(
                "extract_start_",
                new[] { $"ExtractCandidatesAsync started at {ctx.Timestamp:O}" },
                ctx);
            _logger.LogInformation("Wrote start marker: {Path}", path);
            ctx.Progress?.Report($"OMOD 抽出 開始マーカーを生成しました: {path}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write extract start marker");
        }
    }

    /// <inheritdoc/>
    public void WriteDetectorSelected(string name, ExtractionContext ctx)
    {
        try
        {
            var path = WriteDiagnosticsMarker(
                "detector_selected_",
                new[]
                {
                    $"Detector selected at {DateTime.Now:O}",
                    $"Detector={name}"
                },
                ctx);
            _logger.LogInformation("Wrote detector marker: {Path}, Detector={Detector}", path, name);
            ctx.Progress?.Report($"Detector 選択マーカーを生成しました: {path}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write detector marker");
        }
    }

    /// <inheritdoc/>
    public void WriteReverseMapMarker(ExtractionContext ctx)
    {
        try
        {
            var path = WriteDiagnosticsMarker(
                "reverse_map_built_",
                new[]
                {
                    $"Reverse reference map build attempted at {DateTime.Now:O}"
                },
                ctx);
            _logger.LogInformation("Wrote reverse-map marker: {Path}", path);
            ctx.Progress?.Report($"逆参照マップ マーカーを生成しました: {path}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write reverse-map marker");
        }
    }

    /// <inheritdoc/>
    public void WriteDetectionPassMarker(ExtractionContext ctx)
    {
        try
        {
            var path = WriteDiagnosticsMarker(
                "detection_pass_complete_",
                new[]
                {
                    $"Detection pass finished at {DateTime.Now:O}"
                },
                ctx);
            _logger.LogInformation("Wrote detection-pass marker: {Path}", path);
            ctx.Progress?.Report($"検出パス完了マーカーを生成しました: {path}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write detection-pass marker");
        }
    }

    /// <inheritdoc/>
    public void WriteResultsCsv(IEnumerable<OmodCandidate> candidates, ExtractionContext ctx)
    {
        try
        {
            var artifactsDir = _pathService.GetArtifactsDirectory();
            var fileName = $"weapon_omods_{ctx.Timestamp:yyyyMMdd_HHmmss}.csv";
            var path = System.IO.Path.Combine(artifactsDir, fileName);

            using var sw = new System.IO.StreamWriter(path, false, Encoding.UTF8);
            sw.WriteLine("CandidateType,BaseWeapon,BaseEditorId,CandidateFormKey,CandidateEditorId,CandidateAmmo,CandidateAmmoName,SourcePlugin,Notes,SuggestedTarget,ConfirmedAmmoChange,ConfirmReason");
            
            foreach (var c in candidates)
            {
                var baseKey = c.BaseWeapon != null ? $"{c.BaseWeapon.PluginName}:{c.BaseWeapon.FormId:X8}" : string.Empty;
                var candKey = c.CandidateFormKey != null ? $"{c.CandidateFormKey.PluginName}:{c.CandidateFormKey.FormId:X8}" : string.Empty;
                var ammoKey = c.CandidateAmmo != null ? $"{c.CandidateAmmo.PluginName}:{c.CandidateAmmo.FormId:X8}" : string.Empty;
                var ammoName = c.CandidateAmmoName ?? string.Empty;
                var confirmed = c.ConfirmedAmmoChange ? "true" : "false";
                var reason = c.ConfirmReason ?? string.Empty;
                sw.WriteLine($"{c.CandidateType},{baseKey},{Escape(c.BaseWeaponEditorId)},{candKey},{Escape(c.CandidateEditorId)},{ammoKey},{Escape(ammoName)},{c.SourcePlugin},{Escape(c.Notes)},{c.SuggestedTarget},{confirmed},{Escape(reason)}");
            }
            
            sw.Flush();
            _logger.LogInformation("Wrote results CSV: {Path}", path);
            ctx.Progress?.Report($"OMOD 抽出 CSV を生成しました: {path}");

            // Write filtered CSV for specific plugin if needed
            WriteFilteredCsv(candidates, "noveskeRecceL.esp", ctx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write results CSV");
            ctx.Progress?.Report($"警告: CSV の出力に失敗しました: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public void WriteZeroReferenceReport(IEnumerable<OmodCandidate> candidates, ExtractionContext ctx)
    {
        try
        {
            var zeroRefCandidates = candidates.Where(c => !c.ConfirmedAmmoChange && c.BaseWeapon == null).ToList();
            if (zeroRefCandidates.Count == 0)
                return;

            var artifactsDir = _pathService.GetArtifactsDirectory();
            var diagPath = System.IO.Path.Combine(artifactsDir, $"zero_ref_summary_{ctx.Timestamp:yyyyMMdd_HHmmss}.csv");
            
            using var sw = new System.IO.StreamWriter(diagPath, false, Encoding.UTF8);
            sw.WriteLine("CandidateType,SourcePlugin,CandidateFormKey,CandidateEditorId,ConfirmReason");
            
            foreach (var c in zeroRefCandidates)
            {
                var candKey = c.CandidateFormKey != null ? $"{c.CandidateFormKey.PluginName}:{c.CandidateFormKey.FormId:X8}" : string.Empty;
                sw.WriteLine($"{c.CandidateType},{c.SourcePlugin},{candKey},{Escape(c.CandidateEditorId)},{Escape(c.ConfirmReason)}");
            }
            
            sw.Flush();
            _logger.LogInformation("Wrote zero-ref diagnostics: {Path}", diagPath);
            ctx.Progress?.Report($"zero-ref diagnostics を生成しました: {diagPath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write zero-ref diagnostics");
        }
    }

    /// <inheritdoc/>
    public void WriteCompletionMarker(ExtractionContext ctx)
    {
        try
        {
            var path = WriteDiagnosticsMarker(
                "extract_complete_",
                new[]
                {
                    $"ExtractCandidatesAsync completed at {DateTime.Now:O}"
                },
                ctx);
            _logger.LogInformation("Wrote completion marker: {Path}", path);
            ctx.Progress?.Report($"OMOD 抽出 完了マーカーを生成しました: {path}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write extract completion marker");
        }
    }

    private void WriteFilteredCsv(IEnumerable<OmodCandidate> candidates, string pluginFilter, ExtractionContext ctx)
    {
        try
        {
            var filtered = candidates.Where(c => string.Equals(c.SourcePlugin, pluginFilter, StringComparison.OrdinalIgnoreCase)).ToList();
            if (filtered.Count == 0)
                return;

            var artifactsDir = _pathService.GetArtifactsDirectory();
            var fileName = $"weapon_omods_{pluginFilter.Replace(".esp", "")}_{ctx.Timestamp:yyyyMMdd_HHmmss}.csv";
            var path = System.IO.Path.Combine(artifactsDir, fileName);

            using var sw = new System.IO.StreamWriter(path, false, Encoding.UTF8);
            sw.WriteLine("CandidateType,BaseWeapon,BaseEditorId,CandidateFormKey,CandidateEditorId,CandidateAmmo,CandidateAmmoName,SourcePlugin,Notes,SuggestedTarget,ConfirmedAmmoChange,ConfirmReason");
            
            foreach (var c in filtered)
            {
                var baseKey = c.BaseWeapon != null ? $"{c.BaseWeapon.PluginName}:{c.BaseWeapon.FormId:X8}" : string.Empty;
                var candKey = c.CandidateFormKey != null ? $"{c.CandidateFormKey.PluginName}:{c.CandidateFormKey.FormId:X8}" : string.Empty;
                var ammoKey = c.CandidateAmmo != null ? $"{c.CandidateAmmo.PluginName}:{c.CandidateAmmo.FormId:X8}" : string.Empty;
                var ammoName = c.CandidateAmmoName ?? string.Empty;
                var confirmed = c.ConfirmedAmmoChange ? "true" : "false";
                var reason = c.ConfirmReason ?? string.Empty;
                sw.WriteLine($"{c.CandidateType},{baseKey},{Escape(c.BaseWeaponEditorId)},{candKey},{Escape(c.CandidateEditorId)},{ammoKey},{Escape(ammoName)},{c.SourcePlugin},{Escape(c.Notes)},{c.SuggestedTarget},{confirmed},{Escape(reason)}");
            }
            
            sw.Flush();
            _logger.LogInformation("Wrote filtered CSV for {Plugin}: {Path}", pluginFilter, path);
            ctx.Progress?.Report($"{pluginFilter} 向け候補CSV を生成しました: {path}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write filtered CSV for {Plugin}", pluginFilter);
        }
    }

    private string WriteDiagnosticsMarker(string filePrefix, IEnumerable<string> lines, ExtractionContext ctx)
    {
        var artifactsDir = _pathService.GetArtifactsDirectory();
        var path = System.IO.Path.Combine(artifactsDir, $"{filePrefix}{ctx.Timestamp:yyyyMMdd_HHmmss_fff}.txt");
        
        using (var sw = new System.IO.StreamWriter(path, false, Encoding.UTF8))
        {
            foreach (var line in lines)
            {
                sw.WriteLine(line);
            }
        }
        
        return path;
    }

    private static string Escape(string? s)
    {
        if (s == null) return string.Empty;
        return s.Replace("\"", "\\\"").Replace(',', ';');
    }
}
