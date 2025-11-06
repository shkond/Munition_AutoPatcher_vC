using MunitionAutoPatcher.Models;

namespace MunitionAutoPatcher.Services.Interfaces;

/// <summary>
/// Service for building ESPFE patch files that apply ammo mappings directly to WEAP records.
/// </summary>
public interface IEspPatchService
{
    /// <summary>
    /// Builds an ESPFE patch from confirmed weapon-ammo mappings.
    /// </summary>
    /// <param name="confirmedCandidates">List of confirmed OMOD candidates with ammo mappings.</param>
    /// <param name="extraction">Extraction context with weapons and ammo data.</param>
    /// <param name="ct">Cancellation token.</param>
    Task BuildAsync(IEnumerable<OmodCandidate> confirmedCandidates, ExtractionContext extraction, CancellationToken ct);
}
