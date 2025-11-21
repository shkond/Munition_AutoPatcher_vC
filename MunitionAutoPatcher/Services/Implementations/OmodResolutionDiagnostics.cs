using Microsoft.Extensions.Logging;

namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// AttachPointConfirmer の処理診断カウンター
/// </summary>
internal struct OmodResolutionDiagnostics
{
    public int TotalCandidates { get; set; }
    public int OmodResolved { get; set; }
    public int AttachPointMatched { get; set; }
    public int AmmoReferenceDetected { get; set; }
    public int Confirmed { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }

    public readonly void LogSummary(ILogger logger)
    {
        logger.LogInformation(
            "AttachPointConfirmer Summary: Total={Total}, OmodResolved={OmodResolved}, AttachPointMatched={AttachPointMatched}, AmmoReferenceDetected={AmmoReferenceDetected}, Confirmed={Confirmed}, Skipped={Skipped}, Failed={Failed}",
            TotalCandidates, OmodResolved, AttachPointMatched, AmmoReferenceDetected, Confirmed, Skipped, Failed);
    }
}
