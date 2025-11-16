using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// Provider that extracts candidates from ConstructibleObject (COBJ) records.
/// </summary>
public class CobjCandidateProvider : ICandidateProvider
{
    private readonly IWeaponDataExtractor _weaponDataExtractor;
    private readonly ILogger<CobjCandidateProvider> _logger;

    public CobjCandidateProvider(
        IWeaponDataExtractor weaponDataExtractor,
        ILogger<CobjCandidateProvider> logger)
    {
        _weaponDataExtractor = weaponDataExtractor ?? throw new ArgumentNullException(nameof(weaponDataExtractor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public IEnumerable<OmodCandidate> GetCandidates(ExtractionContext context)
    {
        try
        {
            _logger.LogInformation("Extracting COBJ candidates");
            context.Progress?.Report("COBJ 候補を抽出しています...");

            if (context.Environment == null)
            {
                _logger.LogWarning("Environment is null, cannot extract COBJ candidates");
                return Enumerable.Empty<OmodCandidate>();
            }

            var results = _weaponDataExtractor.ExtractAsync(
                context.Environment,
                context.ExcludedPlugins,
                context.Progress).GetAwaiter().GetResult();

            _logger.LogInformation("Extracted {Count} COBJ candidates", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract COBJ candidates");
            context.Progress?.Report($"警告: COBJ 候補の抽出中にエラーが発生しました: {ex.Message}");
            return Enumerable.Empty<OmodCandidate>();
        }
    }
}
