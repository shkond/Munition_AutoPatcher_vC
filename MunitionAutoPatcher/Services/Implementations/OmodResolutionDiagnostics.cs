using Microsoft.Extensions.Logging;

namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// AttachPointConfirmer の処理診断カウンター
/// Phase 5: 詳細カウンタとLogSummaryメソッドを追加
/// </summary>
internal struct OmodResolutionDiagnostics
{
    public int TotalCandidates { get; init; }
    public int OmodResolved { get; init; }
    public int AttachPointMatched { get; init; }
    public int MatchedWeapons { get; init; }
    public int AmmoReferenceDetected { get; init; }
    public int Confirmed { get; init; }
    
    // 詳細な失敗カウンタ
    public int RootNull { get; init; }
    public int CreatedObjMissing { get; init; }
    public int CreatedObjResolveFail { get; init; }
    public int CreatedObjNotOmod { get; init; }

    public readonly void LogSummary(ILogger logger)
    {
        logger.LogInformation(
            "AttachPointConfirmer: inspected={Inspected}, resolvedToOmod={Resolved}, hadAttachPoint={AttachPts}, matchedWeapons={Matched}, foundAmmo={Ammo}, confirmed={Confirmed}",
            TotalCandidates, OmodResolved, AttachPointMatched, MatchedWeapons, AmmoReferenceDetected, Confirmed);
        
        logger.LogInformation(
            "AttachPointConfirmer: failures rootNull={RootNull}, createdObjMissing={CreatedMissing}, createdObjResolveFail={ResolveFail}, createdObjNotOmod={NotOmod}",
            RootNull, CreatedObjMissing, CreatedObjResolveFail, CreatedObjNotOmod);
    }
}
