using MunitionAutoPatcher.Models;

namespace MunitionAutoPatcher.Services.Interfaces;

/// <summary>
/// Service responsible for writing diagnostic markers, CSV outputs, and extraction reports.
/// </summary>
public interface IDiagnosticWriter
{
    /// <summary>
    /// Writes a marker file indicating extraction has started.
    /// </summary>
    void WriteStartMarker(ExtractionContext ctx);

    /// <summary>
    /// Writes a marker file indicating which detector was selected.
    /// </summary>
    void WriteDetectorSelected(string name, ExtractionContext ctx);

    /// <summary>
    /// Writes a marker file indicating reverse map has been built.
    /// </summary>
    void WriteReverseMapMarker(ExtractionContext ctx);

    /// <summary>
    /// Writes a marker file indicating detection pass is complete.
    /// </summary>
    void WriteDetectionPassMarker(ExtractionContext ctx);

    /// <summary>
    /// Writes the main results CSV with all confirmed candidates.
    /// </summary>
    void WriteResultsCsv(IEnumerable<OmodCandidate> confirmed, ExtractionContext ctx);

    /// <summary>
    /// Writes the zero-reference diagnostic report for candidates with no reverse references.
    /// </summary>
    void WriteZeroReferenceReport(IEnumerable<OmodCandidate> candidates, ExtractionContext ctx);

    /// <summary>
    /// Writes a marker file indicating extraction has completed.
    /// </summary>
    void WriteCompletionMarker(ExtractionContext ctx);
}
