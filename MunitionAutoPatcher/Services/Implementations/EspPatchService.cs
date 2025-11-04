using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using System.IO;
using System.Linq;

namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// Service for generating ESL-flagged ESP patch files that apply ammo mappings directly to WEAP records.
/// </summary>
public class EspPatchService : IEspPatchService
{
    private readonly IPathService _pathService;
    private readonly IConfigService _configService;
    private readonly IDiagnosticWriter _diagnosticWriter;
    private readonly IMutagenAccessor _mutagenAccessor;
    private readonly ILogger<EspPatchService> _logger;

    public EspPatchService(
        IPathService pathService,
        IConfigService configService,
        IDiagnosticWriter diagnosticWriter,
        IMutagenAccessor mutagenAccessor,
        ILogger<EspPatchService> logger)
    {
        _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _diagnosticWriter = diagnosticWriter ?? throw new ArgumentNullException(nameof(diagnosticWriter));
        _mutagenAccessor = mutagenAccessor ?? throw new ArgumentNullException(nameof(mutagenAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task BuildAsync(ExtractionContext extraction, ConfirmationContext confirmation, List<OmodCandidate> candidates, CancellationToken ct)
    {
        _logger.LogInformation("Starting ESP patch generation");

        try
        {
            // Filter to confirmed candidates only
            var confirmedCandidates = candidates.Where(c => c.ConfirmedAmmoChange).ToList();
            _logger.LogInformation("Processing {Count} confirmed candidates out of {Total} total", 
                confirmedCandidates.Count, candidates.Count);

            if (confirmedCandidates.Count == 0)
            {
                _logger.LogWarning("No confirmed candidates to process, creating empty patch");
            }

            // Create a new Fallout4Mod with ESL flag
            var modKey = new ModKey("MunitionAutoPatcher_Patch", ModType.Plugin);
            var patchMod = new Fallout4Mod(modKey, Fallout4Release.Fallout4);

            // Set the ESP to ESL-flagged (ESPFE)
            patchMod.ModHeader.Flags |= (int)Fallout4Mod.HeaderFlag.Light;

            _logger.LogInformation("Created patch mod with ModKey: {ModKey}, ESL-flagged", modKey);

            // Get LinkCache from extraction context
            var linkCache = extraction.LinkCache;
            if (linkCache == null)
            {
                _logger.LogError("LinkCache is not available in extraction context");
                throw new InvalidOperationException("LinkCache is required for ESP patch generation");
            }

            int successCount = 0;
            int skipCount = 0;

            foreach (var candidate in confirmedCandidates)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    // Determine the weapon FormKey to patch
                    var weaponFormKey = GetWeaponFormKey(candidate);
                    if (weaponFormKey == null)
                    {
                        _logger.LogWarning("Skipping candidate: unable to determine weapon FormKey");
                        skipCount++;
                        continue;
                    }

                    // Resolve the winning override for the weapon
                    var weaponRecord = ResolveWeaponRecord(linkCache, weaponFormKey);
                    if (weaponRecord == null)
                    {
                        _logger.LogWarning("Skipping candidate: weapon record not found for FormKey {FormKey}", weaponFormKey);
                        skipCount++;
                        continue;
                    }

                    // Get the target ammo FormKey
                    var ammoFormKey = candidate.CandidateAmmo;
                    if (ammoFormKey == null)
                    {
                        _logger.LogWarning("Skipping candidate: no ammo FormKey specified");
                        skipCount++;
                        continue;
                    }

                    // Convert to Mutagen FormKey
                    var mutagenAmmoFormKey = ConvertToMutagenFormKey(ammoFormKey);
                    if (mutagenAmmoFormKey == null)
                    {
                        _logger.LogWarning("Skipping candidate: unable to convert ammo FormKey {AmmoFormKey}", ammoFormKey);
                        skipCount++;
                        continue;
                    }

                    // Add an override of the weapon to the patch
                    var weaponOverride = CreateWeaponOverride(patchMod, weaponRecord, mutagenAmmoFormKey.Value);
                    if (weaponOverride != null)
                    {
                        _logger.LogDebug("Added weapon override for {EditorId} with ammo {AmmoFormKey}",
                            candidate.CandidateEditorId, ammoFormKey);
                        successCount++;
                    }
                    else
                    {
                        _logger.LogWarning("Failed to create weapon override for {EditorId}", candidate.CandidateEditorId);
                        skipCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error processing candidate {EditorId}, skipping", candidate.CandidateEditorId);
                    skipCount++;
                }
            }

            _logger.LogInformation("Processed candidates: {Success} succeeded, {Skipped} skipped", successCount, skipCount);

            // Ensure output directory exists
            var repoRoot = _pathService.GetRepoRoot();
            var outputDirConfig = _configService.GetOutputDirectory();
            var outputDir = Path.IsPathRooted(outputDirConfig)
                ? outputDirConfig
                : Path.Combine(repoRoot, outputDirConfig);

            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
                _logger.LogInformation("Created output directory: {OutputDir}", outputDir);
            }

            // Write the patch to file
            var outputPath = Path.Combine(outputDir, "MunitionAutoPatcher_Patch.esp");
            patchMod.WriteToBinaryParallel(outputPath);

            _logger.LogInformation("ESP patch written to: {OutputPath}", outputPath);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ESP patch generation was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during ESP patch generation");
            throw;
        }

        await Task.CompletedTask;
    }

    private FormKey? GetWeaponFormKey(OmodCandidate candidate)
    {
        // Determine the weapon to patch based on candidate type
        // For most candidates, we want to patch the base weapon or the created weapon
        if (candidate.CandidateFormKey != null && !string.IsNullOrEmpty(candidate.CandidateFormKey.PluginName))
        {
            return candidate.CandidateFormKey;
        }

        if (candidate.BaseWeapon != null)
        {
            return candidate.BaseWeapon;
        }

        return null;
    }

    private object? ResolveWeaponRecord(ILinkResolver linkCache, FormKey formKey)
    {
        try
        {
            // Use the LinkResolver abstraction to resolve the weapon record
            var formKeyStr = $"{formKey.PluginName}:{formKey.FormId:X8}";
            return linkCache.TryResolve(formKeyStr, "Weapon");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve weapon record for {FormKey}", formKey);
            return null;
        }
    }

    private Mutagen.Bethesda.Plugins.FormKey? ConvertToMutagenFormKey(FormKey formKey)
    {
        try
        {
            // Convert our internal FormKey to Mutagen's FormKey
            var modKey = new ModKey(formKey.PluginName, ModType.Plugin);
            return new Mutagen.Bethesda.Plugins.FormKey(modKey, formKey.FormId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to convert FormKey {PluginName}:{FormId}", formKey.PluginName, formKey.FormId);
            return null;
        }
    }

    private Weapon? CreateWeaponOverride(Fallout4Mod patchMod, object weaponRecord, Mutagen.Bethesda.Plugins.FormKey ammoFormKey)
    {
        try
        {
            // Use reflection to get the weapon's FormKey
            var formKeyProp = weaponRecord.GetType().GetProperty("FormKey");
            if (formKeyProp == null)
            {
                _logger.LogWarning("Unable to find FormKey property on weapon record");
                return null;
            }

            var weaponFormKey = formKeyProp.GetValue(weaponRecord) as Mutagen.Bethesda.Plugins.FormKey?;
            if (weaponFormKey == null)
            {
                _logger.LogWarning("Unable to extract FormKey from weapon record");
                return null;
            }

            // Cast to IWeaponGetter if possible
            if (weaponRecord is not IWeaponGetter weaponGetter)
            {
                _logger.LogWarning("Weapon record does not implement IWeaponGetter");
                return null;
            }

            // Create an override in the patch mod
            var weaponOverride = patchMod.Weapons.GetOrAddAsOverride(weaponGetter);

            // Set the ammo on the weapon
            if (weaponOverride.AttackAnimation != null)
            {
                weaponOverride.AttackAnimation.Ammo.SetTo(ammoFormKey);
            }

            return weaponOverride;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create weapon override");
            return null;
        }
    }
}
