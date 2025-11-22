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
    /// <param name="candidates">候補のコレクション</param>
    /// <param name="context">確認に必要なコンテキスト</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    Task ConfirmAsync(IEnumerable<OmodCandidate> candidates, ConfirmationContext context, CancellationToken cancellationToken);
}
