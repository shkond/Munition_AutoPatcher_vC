using MunitionAutoPatcher.Models;

namespace MunitionAutoPatcher.Services.Interfaces;

/// <summary>
/// Extractor service that finds OMOD/COBJ/CreatedWeapon candidates related to weapon ammo.
/// </summary>
public interface IWeaponOmodExtractor
{
    /// <summary>
    /// Extracts candidate OMOD/COBJ/CreatedWeapon entries that may affect weapon ammo.
    /// Returns a list of OmodCandidate for inspection or CSV export.
    /// </summary>
    Task<List<OmodCandidate>> ExtractCandidatesAsync(IProgress<string>? progress = null);

    /// <summary>
    /// Extracts candidate OMOD/COBJ/CreatedWeapon entries with cancellation token support.
    /// </summary>
    Task<List<OmodCandidate>> ExtractCandidatesAsync(IProgress<string>? progress, CancellationToken cancellationToken);
}
