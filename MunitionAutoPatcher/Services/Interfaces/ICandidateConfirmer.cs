using MunitionAutoPatcher.Models;

namespace MunitionAutoPatcher.Services.Interfaces;

/// <summary>
/// Service responsible for confirming candidates through reverse-reference analysis.
/// </summary>
public interface ICandidateConfirmer
{
    /// <summary>
    /// Confirms candidates by checking if they modify weapon ammo via reverse-reference map.
    /// Updates candidates in-place with confirmation status and reasons.
    /// </summary>
    void Confirm(IEnumerable<OmodCandidate> candidates, ConfirmationContext context);
}
