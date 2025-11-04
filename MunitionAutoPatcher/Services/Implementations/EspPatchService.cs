using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Records;
using System.IO;

namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// Service for building ESPFE patches that apply ammo mappings directly to WEAP records.
/// </summary>
public class EspPatchService : IEspPatchService
{
    private readonly IPathService _pathService;
    private readonly ILogger<EspPatchService> _logger;

    public EspPatchService(
        IPathService pathService,
        IDiagnosticWriter diagnosticWriter,
        ILogger<EspPatchService> logger)
    {
        _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
        // diagnosticWriter is injected for potential future use (reserved for logging)
        _ = diagnosticWriter ?? throw new ArgumentNullException(nameof(diagnosticWriter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task BuildAsync(IEnumerable<OmodCandidate> confirmedCandidates, ExtractionContext extraction, CancellationToken ct)
    {
        _logger.LogInformation("Starting ESPFE patch build");

        try
        {
            var candidatesList = confirmedCandidates.Where(c => c.ConfirmedAmmoChange).ToList();
            if (!candidatesList.Any())
            {
                _logger.LogInformation("No confirmed weapon-ammo mappings found, skipping ESP patch generation");
                return;
            }

            _logger.LogInformation("Found {Count} confirmed weapon-ammo mappings", candidatesList.Count);

            // Get the Mutagen environment and LinkCache
            if (extraction.Environment == null)
            {
                _logger.LogError("Extraction environment is null, cannot build patch");
                throw new InvalidOperationException("Extraction environment is required for ESP patch generation");
            }

            var env = extraction.Environment;
            var linkCacheObj = env.GetLinkCache();
            if (linkCacheObj == null)
            {
                _logger.LogError("LinkCache is unavailable, cannot build patch");
                throw new InvalidOperationException("LinkCache is required for ESP patch generation");
            }

            // Try to get the typed ILinkCache from the object
            // The linkCacheObj should be convertible to ILinkCache<IFallout4Mod, IFallout4ModGetter>
            var linkCache = GetTypedLinkCache(linkCacheObj);

            // Create new Fallout4Mod for the patch
            var patchModKey = new ModKey("MunitionAutoPatcher_Patch", ModType.Plugin);
            var patch = new Fallout4Mod(patchModKey, Fallout4Release.Fallout4);

            // Set ESL flag (ESPFE)
            patch.ModHeader.Flags |= Fallout4ModHeader.HeaderFlag.LightMaster;

            _logger.LogInformation("Created patch with ModKey {ModKey}, ESL flag set", patchModKey);

            var successCount = 0;
            var skipCount = 0;

            // Process each confirmed mapping
            foreach (var candidate in candidatesList)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    if (!ProcessCandidate(candidate, linkCache, patch))
                    {
                        skipCount++;
                    }
                    else
                    {
                        successCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process candidate for weapon {WeaponKey}, skipping",
                        candidate.CandidateFormKey);
                    skipCount++;
                }
            }

            _logger.LogInformation("Processed {SuccessCount} weapon overrides, skipped {SkipCount}",
                successCount, skipCount);

            if (successCount == 0)
            {
                _logger.LogWarning("No weapons were successfully patched, skipping file output");
                return;
            }

            // Sync masters - this ensures all referenced plugins are in the master list
            patch.SyncMasters();

            // Write the patch to disk
            var outputDir = _pathService.GetOutputDirectory();
            var outputPath = Path.Combine(outputDir, "MunitionAutoPatcher_Patch.esp");

            _logger.LogInformation("Writing patch to {OutputPath}", outputPath);

            // WriteToBinaryParallel is already parallelized internally, no need for Task.Run wrapper
            patch.WriteToBinaryParallel(outputPath);

            _logger.LogInformation("ESPFE patch written successfully to {OutputPath}", outputPath);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ESP patch build was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build ESP patch");
            throw;
        }
    }

    /// <summary>
    /// Attempts to get a typed ILinkCache from the object returned by the environment.
    /// Note: This method uses reflection to access internal Mutagen LinkCache structure.
    /// It's brittle and may break with Mutagen library updates. Used as a fallback when
    /// the LinkResolver wrapper doesn't expose the typed interface directly.
    /// </summary>
    private ILinkCache<IFallout4Mod, IFallout4ModGetter> GetTypedLinkCache(object linkCacheObj)
    {
        // The LinkResolver wraps the actual LinkCache
        // We need to extract the underlying _linkCache field
        try
        {
            var linkCacheType = linkCacheObj.GetType();
            var linkCacheField = linkCacheType.GetField("_linkCache",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (linkCacheField != null)
            {
                var actualLinkCache = linkCacheField.GetValue(linkCacheObj);
                if (actualLinkCache is ILinkCache<IFallout4Mod, IFallout4ModGetter> typedCache)
                {
                    return typedCache;
                }
            }

            // Fallback: try direct cast
            if (linkCacheObj is ILinkCache<IFallout4Mod, IFallout4ModGetter> directCast)
            {
                return directCast;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract LinkCache via reflection");
        }

        throw new InvalidOperationException(
            "Could not extract typed LinkCache from environment. " +
            "This may indicate a Mutagen library version mismatch or API change.");
    }

    /// <summary>
    /// Processes a single candidate and adds a weapon override to the patch.
    /// </summary>
    private bool ProcessCandidate(
        OmodCandidate candidate,
        ILinkCache<IFallout4Mod, IFallout4ModGetter> linkCache,
        Fallout4Mod patch)
    {
        if (candidate.CandidateFormKey == null || candidate.CandidateAmmo == null)
        {
            _logger.LogWarning("Candidate has null FormKey or Ammo, skipping");
            return false;
        }

        // Build Mutagen FormKey for the weapon
        var weaponFormKey = new Mutagen.Bethesda.Plugins.FormKey(
            new ModKey(candidate.CandidateFormKey.PluginName, ModType.Plugin),
            candidate.CandidateFormKey.FormId);

        // Build Mutagen FormKey for the target ammo
        var ammoFormKey = new Mutagen.Bethesda.Plugins.FormKey(
            new ModKey(candidate.CandidateAmmo.PluginName, ModType.Plugin),
            candidate.CandidateAmmo.FormId);

        try
        {
            // Try to resolve the weapon from the link cache
            if (!linkCache.TryResolve<IWeaponGetter>(weaponFormKey, out var weaponGetter))
            {
                _logger.LogWarning("Could not resolve weapon {WeaponKey} from LinkCache, skipping", weaponFormKey);
                return false;
            }

            if (weaponGetter == null)
            {
                _logger.LogWarning("Weapon {WeaponKey} resolved to null, skipping", weaponFormKey);
                return false;
            }

            // Create a weapon override in the patch (copy-forward)
            var weaponOverride = patch.Weapons.GetOrAddAsOverride(weaponGetter);

            // Set the ammo to the target ammo FormKey
            if (weaponOverride.Data == null)
            {
                _logger.LogWarning("Weapon {WeaponKey} has no Data record, skipping", weaponFormKey);
                return false;
            }

            weaponOverride.Data.Ammo.SetTo(ammoFormKey);

            _logger.LogInformation("Created weapon override: {WeaponKey} -> ammo {AmmoKey}",
                weaponFormKey, ammoFormKey);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create weapon override for {WeaponKey}", weaponFormKey);
            return false;
        }
    }
}
