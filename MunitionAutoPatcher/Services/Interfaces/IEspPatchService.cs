using MunitionAutoPatcher.Models;

namespace MunitionAutoPatcher.Services.Interfaces;

/// <summary>
/// Service for generating ESL-flagged ESP patch files that apply ammo mappings directly to WEAP records.
/// </summary>
public interface IEspPatchService
{
    /// <summary>
    /// Builds an ESPFE patch from confirmed weapon-to-ammo mappings.
    /// </summary>
    /// <param name="extraction">Extraction context containing environment and weapon data.</param>
    /// <param name="confirmation">Confirmation context containing confirmed candidates.</param>
    /// <param name="candidates">List of confirmed candidates to process.</param>
    /// <param name="ct">Cancellation token.</param>
    Task BuildAsync(ExtractionContext extraction, ConfirmationContext confirmation, List<OmodCandidate> candidates, CancellationToken ct);
}
