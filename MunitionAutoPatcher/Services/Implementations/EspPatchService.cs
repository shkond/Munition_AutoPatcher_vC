using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Records;
using InternalFormKey = MunitionAutoPatcher.Models.FormKey;
using MutagenFormKey = Mutagen.Bethesda.Plugins.FormKey;

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
        if (extraction == null) throw new ArgumentNullException(nameof(extraction));

        _logger.LogInformation("ESP generation: start");

        var baseCache = extraction.FormLinkCache;
        var resolver = extraction.LinkCache;
        if (baseCache == null && resolver == null)
        {
            _logger.LogError("EspPatchService: No resolver available. Both FormLinkCache and LinkCache are null.");
            throw new InvalidOperationException("A link resolver (ILinkCache or ILinkResolver) is required for ESP generation");
        }

        try
        {
            ct.ThrowIfCancellationRequested();

            // Create ESPFE patch mod
            var modKey = new ModKey("MunitionAutoPatcher_Patch", ModType.Plugin);
            var patchMod = new Fallout4Mod(modKey, Fallout4Release.Fallout4);
            // Mark as ESL-flagged (Small Master)
            patchMod.IsSmallMaster = true;

            int success = 0, skipped = 0;
            foreach (var c in (candidates ?? Enumerable.Empty<OmodCandidate>()))
            {
                if (c == null || !c.ConfirmedAmmoChange) continue;
                ct.ThrowIfCancellationRequested();

                var wKey = GetWeaponFormKey(c);
                if (wKey == null) { skipped++; continue; }
                var mwKey = ToMutagenFormKey(wKey);
                if (!TryResolve<IWeaponGetter>(baseCache, resolver, mwKey, out var weaponGetter))
                { skipped++; continue; }

                var aKey = c.CandidateAmmo;
                if (aKey == null) { skipped++; continue; }
                var maKey = ToMutagenFormKey(aKey);
                if (!TryResolve<IAmmunitionGetter>(baseCache, resolver, maKey, out var ammoGetter))
                { skipped++; continue; }

                var weapOverride = patchMod.Weapons.GetOrAddAsOverride(weaponGetter!);
                weapOverride.Ammo.SetTo(ammoGetter);

                EnsureMaster(patchMod, weaponGetter!.FormKey.ModKey);
                EnsureMaster(patchMod, ammoGetter!.FormKey.ModKey);
                success++;
            }

            _logger.LogInformation("ESP generation: success={Success}, skipped={Skipped}", success, skipped);

            var repoRoot = _pathService.GetRepoRoot();
            var outputDirConfig = _configService.GetOutputDirectory();
            var outputDir = Path.IsPathRooted(outputDirConfig) ? outputDirConfig : Path.Combine(repoRoot, outputDirConfig);
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
            var outputPath = Path.Combine(outputDir, "MunitionAutoPatcher_Patch.esp");
            patchMod.WriteToBinary(outputPath);
            _logger.LogInformation("ESP written: {Path}", outputPath);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ESP generation cancelled");
            throw;
        }

        await Task.CompletedTask;
    }

    private static void EnsureMaster(Fallout4Mod mod, ModKey master)
    {
        var masters = mod.ModHeader.MasterReferences;
        if (!masters.Any(m => m.Master == master))
        {
            masters.Add(new MasterReference() { Master = master });
        }
    }

    private static MutagenFormKey ToMutagenFormKey(InternalFormKey key)
    {
        var mk = new ModKey(key.PluginName, ModType.Plugin);
        return new MutagenFormKey(mk, key.FormId);
    }

    private InternalFormKey? GetWeaponFormKey(OmodCandidate candidate)
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

    private static bool TryResolve<TGetter>(ILinkCache? cache, ILinkResolver? resolver, MutagenFormKey key, out TGetter? result)
        where TGetter : class
    {
        result = null;
        try
        {
            if (cache != null)
            {
                if (cache.TryResolve(key, typeof(TGetter), out var major) && major is TGetter t)
                {
                    result = t;
                    return true;
                }
            }
        }
        catch
        {
            // Fall through to resolver
        }

        try
        {
            if (resolver != null)
            {
                return resolver.TryResolve<TGetter>(key, out result);
            }
        }
        catch
        {
            // Give up
        }

        return false;
    }
}
