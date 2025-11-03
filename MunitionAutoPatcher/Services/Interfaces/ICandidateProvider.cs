using MunitionAutoPatcher.Models;

namespace MunitionAutoPatcher.Services.Interfaces;

/// <summary>
/// Strategy for discovering OMOD/COBJ candidates.
/// </summary>
public interface ICandidateProvider
{
    /// <summary>
    /// Gets candidates from a specific source (COBJ, reflection scan, etc.).
    /// </summary>
    IEnumerable<OmodCandidate> GetCandidates(ExtractionContext context);
}
