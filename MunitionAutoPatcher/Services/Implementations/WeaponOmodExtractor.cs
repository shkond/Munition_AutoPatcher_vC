using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// Orchestrator for OMOD/COBJ candidate extraction. Delegates to specialized providers, confirmer, and diagnostic writer.
/// </summary>
public class WeaponOmodExtractor : IWeaponOmodExtractor
{
    private readonly ILoadOrderService _loadOrderService;
    private readonly IConfigService _configService;
    private readonly IMutagenEnvironmentFactory _mutagenEnvironmentFactory;
    private readonly IDiagnosticWriter _diagnosticWriter;
    private readonly IEnumerable<ICandidateProvider> _providers;
    private readonly IEnumerable<ICandidateConfirmer> _confirmers;
    private readonly IMutagenAccessor _mutagenAccessor;
    private readonly IPathService _pathService;
    private readonly ILogger<WeaponOmodExtractor> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IEspPatchService? _espPatchService;

    public WeaponOmodExtractor(
        ILoadOrderService loadOrderService,
        IConfigService configService,
        IMutagenEnvironmentFactory mutagenEnvironmentFactory,
        IDiagnosticWriter diagnosticWriter,
        IEnumerable<ICandidateProvider> providers,
        IEnumerable<ICandidateConfirmer> confirmers,
        IMutagenAccessor mutagenAccessor,
        IPathService pathService,
        ILogger<WeaponOmodExtractor> logger,
        ILoggerFactory loggerFactory,
        IEspPatchService? espPatchService = null)
    {
        _loadOrderService = loadOrderService ?? throw new ArgumentNullException(nameof(loadOrderService));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _mutagenEnvironmentFactory = mutagenEnvironmentFactory ?? throw new ArgumentNullException(nameof(mutagenEnvironmentFactory));
        _diagnosticWriter = diagnosticWriter ?? throw new ArgumentNullException(nameof(diagnosticWriter));
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        _confirmers = confirmers ?? throw new ArgumentNullException(nameof(confirmers));
        _mutagenAccessor = mutagenAccessor ?? throw new ArgumentNullException(nameof(mutagenAccessor));
        _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _espPatchService = espPatchService; // optional for backward compatibility
    }

    /// <summary>
    /// Extracts OMOD/COBJ candidates by orchestrating providers, confirmation, and diagnostics.
    /// </summary>
    public async Task<List<OmodCandidate>> ExtractCandidatesAsync(IProgress<string>? progress = null)
    {
        return await ExtractCandidatesAsync(progress, CancellationToken.None);
    }

    /// <summary>
    /// Extracts OMOD/COBJ candidates with cancellation token support.
    /// </summary>
    public async Task<List<OmodCandidate>> ExtractCandidatesAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting OMOD/COBJ candidate extraction");
        progress?.Report("OMOD/COBJ の候補を抽出しています...");

        var candidates = new List<OmodCandidate>();

        try
        {
            // Verify load order
            var loadOrder = await _loadOrderService.GetLoadOrderAsync();
            if (loadOrder == null)
            {
                _logger.LogWarning("Load order unavailable, cannot extract candidates");
                progress?.Report("エラー: ロードオーダーが取得できませんでした");
                return candidates;
            }

            using var environment = _mutagenEnvironmentFactory.Create();

            // Build extraction context
            var context = await BuildExtractionContextAsync(environment, progress, cancellationToken);

            // Write start marker
            try
            {
                _diagnosticWriter.WriteStartMarker(context);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write start marker (non-fatal)");
            }

            // Aggregate candidates from all providers
            try
            {
                foreach (var provider in _providers)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var providerCandidates = provider.GetCandidates(context);
                        candidates.AddRange(providerCandidates);
                        _logger.LogInformation("Provider {ProviderType} found {Count} candidates",
                            provider.GetType().Name, providerCandidates.Count());
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Provider {ProviderType} failed (continuing with other providers)",
                            provider.GetType().Name);
                    }
                }

                _logger.LogInformation("Total candidates from providers: {Count}", candidates.Count);
                progress?.Report($"候補を {candidates.Count} 件発見しました");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Candidate extraction was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during candidate aggregation");
                progress?.Report($"エラー: 候補の集約中に例外が発生しました: {ex.Message}");
            }

            // Diagnostic: sample 5 OMODs and log attach-point EDID + matched weapon counts
            try
            {
                RunOmodAttachPointDiagnostics(context);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "OMOD attach-point diagnostics failed (non-fatal)");
            }

            // Build reverse-reference map for confirmation
            Dictionary<string, List<(object Record, string PropName, object PropValue)>> reverseMap;
            try
            {
                using var mapEnv = _mutagenEnvironmentFactory.Create();
                var builder = new ReverseMapBuilder(mapEnv, _loggerFactory.CreateLogger<ReverseMapBuilder>());
                reverseMap = builder.Build(context.ExcludedPlugins);
                _logger.LogInformation("Built reverse-reference map with {Count} keys", reverseMap.Count);

                try
                {
                    _diagnosticWriter.WriteReverseMapMarker(context);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to write reverse-map marker (non-fatal)");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to build reverse-reference map, confirmation will be limited");
                reverseMap = new Dictionary<string, List<(object Record, string PropName, object PropValue)>>(StringComparer.OrdinalIgnoreCase);
            }

            // Select detector
            IAmmunitionChangeDetector? detector = null;
            try
            {
                var mutAsm = typeof(Mutagen.Bethesda.Environments.GameEnvironment).Assembly.GetName();
                detector = DetectorFactory.GetDetector(mutAsm, _loggerFactory);
                _logger.LogInformation("Selected detector: {DetectorName}", detector?.Name ?? "None");

                if (detector != null)
                {
                    try
                    {
                        _diagnosticWriter.WriteDetectorSelected(detector.Name ?? "Unknown", context);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to write detector marker (non-fatal)");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to select detector, using fallback");
                detector = new ReflectionFallbackDetector(_loggerFactory.CreateLogger<ReflectionFallbackDetector>());
            }

            // Confirm candidates via reverse-reference analysis
            ConfirmationContext confirmationContext;
            try
            {
                confirmationContext = await BuildConfirmationContextAsync(
                    reverseMap,
                    context.ExcludedPlugins,
                    context.AllWeapons,
                    context.AmmoMap,
                    detector,
                    context.LinkCache,
                    context.FormLinkCache,
                    cancellationToken);

                foreach (var confirmer in _confirmers)
                {
                    try
                    {
                        confirmer.Confirm(candidates, confirmationContext);
                        _logger.LogInformation("Confirmer {Confirmer} complete", confirmer.GetType().Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Confirmer {Confirmer} failed (continuing)", confirmer.GetType().Name);
                    }
                }
                _logger.LogInformation("All confirmation passes complete");

                try
                {
                    _diagnosticWriter.WriteDetectionPassMarker(context);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to write detection-pass marker (non-fatal)");
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Candidate confirmation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during candidate confirmation");
                progress?.Report($"警告: 候補の確認中にエラーが発生しました: {ex.Message}");
                // Create empty confirmation context for fallback
                confirmationContext = new ConfirmationContext
                {
                    ReverseMap = reverseMap,
                    ExcludedPlugins = context.ExcludedPlugins,
                    AllWeapons = context.AllWeapons,
                    AmmoMap = context.AmmoMap,
                    Detector = detector,
                    LinkCache = context.LinkCache,
                    CancellationToken = cancellationToken
                };
            }

            // Post-process: fill ConfirmReason for unconfirmed candidates
            try
            {
                PostProcessUnconfirmedCandidates(candidates, reverseMap, detector?.Name ?? "Unknown");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during post-processing (non-fatal)");
            }

            // Generate output based on config.output.mode
            try
            {
                var outputMode = _configService.GetOutputMode();
                _logger.LogInformation("Output mode: {OutputMode}", outputMode);

                if (string.Equals(outputMode, "esp", StringComparison.OrdinalIgnoreCase))
                {
                    // Generate ESP patch
                    if (_espPatchService != null)
                    {
                        _logger.LogInformation("Generating ESP patch from confirmed candidates");
                        progress?.Report("ESP パッチを生成しています...");
                        try
                        {
                            var hasFormLinkCache = context.FormLinkCache != null;
                            var hasResolver = context.LinkCache != null;
                            _logger.LogInformation("ESP debug: FormLinkCachePresent={HasFormLinkCache}, ResolverPresent={HasResolver}, ResolverType={ResolverType}",
                                hasFormLinkCache,
                                hasResolver,
                                context.LinkCache?.GetType().FullName ?? "<null>");
                        }
                        catch { /* best effort */ }

                        await _espPatchService.BuildAsync(context, confirmationContext, candidates, cancellationToken);

                        progress?.Report("ESP パッチの生成が完了しました");
                    }
                    else
                    {
                        _logger.LogWarning("ESP patch service not available, skipping ESP generation");
                    }
                }
                // else: ini mode - no action here, INI generation handled separately
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate output patch");
                progress?.Report($"エラー: 出力の生成に失敗しました: {ex.Message}");
            }

            // Write diagnostic reports
            try
            {
                _diagnosticWriter.WriteResultsCsv(candidates, context);
                _diagnosticWriter.WriteZeroReferenceReport(candidates, context);
                WriteNoveskeDiagnostic(context, reverseMap, candidates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write diagnostic reports");
                progress?.Report($"警告: 診断レポートの出力に失敗しました: {ex.Message}");
            }

            // Write completion marker
            try
            {
                _diagnosticWriter.WriteCompletionMarker(context);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write completion marker (non-fatal)");
            }

            progress?.Report($"抽出完了: {candidates.Count} 件の候補を検出しました");
            _logger.LogInformation("Extraction complete with {Count} candidates", candidates.Count);

            return candidates;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Extraction was cancelled");
            progress?.Report("抽出がキャンセルされました");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error during extraction");
            progress?.Report($"エラー: OMOD 抽出中に例外が発生しました: {ex.Message}");
            return candidates;
        }
    }

    #region Helper Methods

    private void RunOmodAttachPointDiagnostics(ExtractionContext context)
    {
        if (context.Environment == null)
        {
            _logger.LogInformation("OMOD diag: Environment unavailable");
            return;
        }

        var resolver = context.LinkCache;
        if (resolver == null)
        {
            _logger.LogInformation("OMOD diag: LinkCache/Resolver unavailable");
            return;
        }

        // Gather weapons and their attach parent slot keyword FormKeys
        var weaponSlotKeys = new List<HashSet<(string Plugin, uint Id)>>();
        foreach (var weapon in context.AllWeapons)
        {
            try
            {
                var set = new HashSet<(string Plugin, uint Id)>();
                var apsProp = weapon.GetType().GetProperty("AttachParentSlots");
                var apsVal = apsProp?.GetValue(weapon) as System.Collections.IEnumerable;
                if (apsVal != null)
                {
                    foreach (var link in apsVal)
                    {
                        if (link == null) continue;
                        var fkProp = link.GetType().GetProperty("FormKey");
                        var fk = fkProp?.GetValue(link);
                        if (fk != null && TryExtractFormKeyInfo(fk, out var p, out var id))
                        {
                            set.Add((p.ToLowerInvariant(), id));
                        }
                    }
                }
                weaponSlotKeys.Add(set);
            }
            catch { weaponSlotKeys.Add(new HashSet<(string, uint)>()); }
        }

        // Take first 5 OMODs and log their attach point and match counts
        var omods = context.Environment.GetWinningObjectModificationsTyped().Take(5).ToList();
        int idx = 0;
        foreach (var omod in omods)
        {
            idx++;
            try
            {
                // Find an attach point-like property on the OMOD
                object? apLink = null;
                var props = omod.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                foreach (var prop in props)
                {
                    if (prop.GetIndexParameters().Length > 0) continue;
                    if (prop.Name.Equals("AttachPoint", StringComparison.OrdinalIgnoreCase) ||
                        prop.Name.Equals("AttachParentSlot", StringComparison.OrdinalIgnoreCase) ||
                        prop.Name.Contains("AttachPoint", StringComparison.OrdinalIgnoreCase))
                    {
                        apLink = prop.GetValue(omod);
                        if (apLink != null) break;
                    }
                }

                string apEdid = string.Empty;
                (string Plugin, uint Id) apKey = (string.Empty, 0);
                if (apLink != null)
                {
                    var fkProp = apLink.GetType().GetProperty("FormKey");
                    var fk = fkProp?.GetValue(apLink);
                    if (fk != null && TryExtractFormKeyInfo(fk, out var p, out var id))
                    {
                        apKey = (p.ToLowerInvariant(), id);
                        // Try resolve to keyword (prefer FormKey; fall back to link)
                        object? kw = null;
                        try
                        {
                            if (!(resolver.TryResolve(fk, out kw) && kw != null))
                            {
                                resolver.TryResolve(apLink, out kw);
                            }
                        }
                        catch { /* ignore resolution errors in diagnostics */ }
                        if (kw != null)
                        {
                            apEdid = _mutagenAccessor.GetEditorId(kw);
                        }
                    }
                }

                int matched = 0;
                if (!string.IsNullOrEmpty(apKey.Plugin) && apKey.Id != 0)
                {
                    foreach (var set in weaponSlotKeys)
                    {
                        if (set.Contains(apKey)) matched++;
                    }
                }

                var omodEdid = _mutagenAccessor.GetEditorId(omod);
                _logger.LogInformation("OMOD diag [{Idx}]: OMOD={OmodEdid}, AttachPointEDID={ApEdid}, MatchedWeapons={Matched}", idx, omodEdid, apEdid, matched);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "OMOD diag failed for sample {Idx}", idx);
            }
        }
    }

    private static bool TryExtractFormKeyInfo(object formKey, out string plugin, out uint id)
    {
        plugin = string.Empty;
        id = 0;
        try
        {
            var modKey = formKey.GetType().GetProperty("ModKey")?.GetValue(formKey);
            if (modKey == null) return false;
            var idObj = formKey.GetType().GetProperty("ID")?.GetValue(formKey);
            if (idObj == null) return false;
            var fileNameObj = modKey.GetType().GetProperty("FileName")?.GetValue(modKey);
            plugin = (fileNameObj?.ToString() ?? modKey.ToString()) ?? string.Empty;
            id = idObj is uint ui ? ui : Convert.ToUInt32(idObj);
            return !string.IsNullOrEmpty(plugin) && !plugin.Equals("Null", StringComparison.OrdinalIgnoreCase) && id != 0;
        }
        catch { return false; }
    }

    private Task<ExtractionContext> BuildExtractionContextAsync(
        IResourcedMutagenEnvironment environment,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var context = new ExtractionContext
        {
            Environment = environment,
            Progress = progress,
            CancellationToken = cancellationToken,
            Timestamp = DateTime.Now,
            RepoRoot = _pathService.GetRepoRoot()
        };

        // Get LinkCache (resolver wrapper) and underlying FormLinkCache if exposed
        context.LinkCache = _mutagenAccessor.GetLinkCache(environment);
        try
        {
            // Attempt to retrieve underlying Mutagen ILinkCache if present for stronger generic resolution
            var rawEnv = environment as ResourcedMutagenEnvironment;
            if (rawEnv != null)
            {
                var innerField = rawEnv.GetType().GetField("_env", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var innerAdapter = innerField?.GetValue(rawEnv);
                var linkCacheProp = innerAdapter?.GetType().GetProperty("LinkCache", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                var formLinkCacheObj = linkCacheProp?.GetValue(innerAdapter) as Mutagen.Bethesda.Plugins.Cache.ILinkCache;
                if (formLinkCacheObj != null)
                {
                    context.FormLinkCache = formLinkCacheObj;
                    _logger.LogDebug("BuildExtractionContext: FormLinkCache captured (type={Type})", formLinkCacheObj.GetType().FullName);
                }
                else
                {
                    _logger.LogDebug("BuildExtractionContext: FormLinkCache property absent or null");
                }
                // If FormLinkCache wasn't found via direct property, try building one from the underlying GameEnvironment.LoadOrder
                if (context.FormLinkCache == null)
                {
                    try
                    {
                        var innerEnvField = innerAdapter?.GetType().GetField("_env", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var gameEnv = innerEnvField?.GetValue(innerAdapter);
                        if (gameEnv != null)
                        {
                            var loadOrderProp = gameEnv.GetType().GetProperty("LoadOrder", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            var loadOrder = loadOrderProp?.GetValue(gameEnv);
                            if (loadOrder != null)
                            {
                                // Try ToImmutableLinkCache or ToLinkCache via reflection
                                var toImmutable = loadOrder.GetType().GetMethod("ToImmutableLinkCache", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, Type.DefaultBinder, Type.EmptyTypes, null);
                                var toLinkCache = loadOrder.GetType().GetMethod("ToLinkCache", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, Type.DefaultBinder, Type.EmptyTypes, null);
                                object? lc = null;
                                if (toImmutable != null)
                                {
                                    lc = toImmutable.Invoke(loadOrder, null);
                                }
                                else if (toLinkCache != null)
                                {
                                    lc = toLinkCache.Invoke(loadOrder, null);
                                }

                                if (lc is Mutagen.Bethesda.Plugins.Cache.ILinkCache builtCache)
                                {
                                    context.FormLinkCache = builtCache;
                                    _logger.LogInformation("BuildExtractionContext: built FormLinkCache from LoadOrder (type={Type})", builtCache.GetType().FullName);
                                }
                                else
                                {
                                    _logger.LogDebug("BuildExtractionContext: LoadOrder did not expose ToImmutableLinkCache/ToLinkCache or result was null");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "BuildExtractionContext: failed fallback attempt to build FormLinkCache from LoadOrder");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "BuildExtractionContext: failed to capture FormLinkCache");
        }

        // Build excluded plugins set
        try
        {
            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in _configService.GetExcludedPlugins() ?? Enumerable.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(p))
                    excluded.Add(p.Trim());
            }

            if (_configService.GetExcludeFallout4Esm())
                excluded.Add("Fallout4.esm");

            if (_configService.GetExcludeDlcEsms())
            {
                excluded.Add("DLCRobot.esm");
                excluded.Add("DLCworkshop01.esm");
                excluded.Add("DLCCoast.esm");
                excluded.Add("DLCworkshop02.esm");
                excluded.Add("DLCworkshop03.esm");
                excluded.Add("DLCNukaWorld.esm");
            }

            context.ExcludedPlugins = excluded;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build excluded plugin set");
            context.ExcludedPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        // Get all weapons
        try
        {
            var weapons = _mutagenAccessor.GetWinningWeaponOverrides(environment).ToList();
            context.AllWeapons = weapons;
            context.WeaponKeySet = BuildWeaponKeySet(weapons);
            _logger.LogInformation("Loaded {Count} weapons", weapons.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load weapons");
            context.AllWeapons = new List<object>();
            context.WeaponKeySet = new HashSet<(string Plugin, uint Id)>();
        }

        // Build ammo map
        try
        {
            context.AmmoMap = BuildAmmoMap(environment);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build ammo map");
        }

        return Task.FromResult(context);
    }

    private HashSet<(string Plugin, uint Id)> BuildWeaponKeySet(IEnumerable<object> weapons)
    {
        var set = new HashSet<(string Plugin, uint Id)>();
        foreach (var weapon in weapons)
        {
            try
            {
                if (_mutagenAccessor.TryGetPluginAndIdFromRecord(weapon, out var plugin, out var id))
                {
                    set.Add((plugin.ToLowerInvariant(), id));
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to add weapon to key set");
            }
        }
        return set;
    }

    private Dictionary<string, object> BuildAmmoMap(IResourcedMutagenEnvironment environment)
    {
        var ammoMap = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var ammoRecords = _mutagenAccessor.EnumerateRecordCollections(environment, "Ammo");
            foreach (var ammo in ammoRecords)
            {
                try
                {
                    if (_mutagenAccessor.TryGetPluginAndIdFromRecord(ammo, out var plugin, out var id))
                    {
                        var key = $"{plugin}:{id:X8}";
                        ammoMap[key] = ammo;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to add ammo to map");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate ammo records");
        }
        return ammoMap;
    }

    private async Task<ConfirmationContext> BuildConfirmationContextAsync(
    Dictionary<string, List<(object Record, string PropName, object PropValue)>> reverseMap,
    HashSet<string> excludedPlugins,
    List<object> allWeapons,
    Dictionary<string, object> ammoMap,
    IAmmunitionChangeDetector? detector,
    MunitionAutoPatcher.Services.Interfaces.ILinkResolver? linkCache,
    Mutagen.Bethesda.Plugins.Cache.ILinkCache? formLinkCache,
    CancellationToken cancellationToken)
    {
        ILinkResolver? resolver = null;
        try
        {
            // If we already have a concrete Mutagen ILinkCache, prefer a LinkResolver backed by it
            if (formLinkCache != null)
            {
                resolver = new Services.Implementations.LinkResolver(formLinkCache);
                _logger.LogInformation("BuildConfirmationContext: using concrete FormLinkCache-backed resolver (type={Type})", formLinkCache.GetType().FullName);
            }
            else
            {
                // Try to build a concrete ILinkCache from the current load order if available
                try
                {
                    var loadOrder = await _loadOrderService.GetLoadOrderAsync();
                    if (loadOrder != null)
                    {
                        object? built = null;
                        try
                        {
                            var toImmutable = loadOrder.GetType().GetMethod("ToImmutableLinkCache", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, Type.DefaultBinder, Type.EmptyTypes, null);
                            var toLinkCache = loadOrder.GetType().GetMethod("ToLinkCache", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, Type.DefaultBinder, Type.EmptyTypes, null);
                            if (toImmutable != null)
                            {
                                built = toImmutable.Invoke(loadOrder, null);
                            }
                            else if (toLinkCache != null)
                            {
                                built = toLinkCache.Invoke(loadOrder, null);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "BuildConfirmationContextAsync: failed to invoke ToImmutableLinkCache/ToLinkCache");
                        }

                        if (built is Mutagen.Bethesda.Plugins.Cache.ILinkCache builtCache)
                        {
                            resolver = new Services.Implementations.LinkResolver(builtCache);
                            _logger.LogInformation("BuildConfirmationContext: built LinkCache for confirmation (mods={Count})", (loadOrder as System.Collections.ICollection)?.Count ?? 0);
                        }
                        else
                        {
                            _logger.LogDebug("BuildConfirmationContext: unable to build concrete LinkCache from LoadOrder");
                        }
                    }
                    else
                    {
                        _logger.LogDebug("BuildConfirmationContext: loadOrder service returned null");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "BuildConfirmationContext: failed to build LinkCache");
                }
                // If still null, fall back to provided resolver wrapper
                if (resolver == null)
                {
                    resolver = (ILinkResolver?)linkCache;
                    _logger.LogInformation("BuildConfirmationContext: using provided resolver (type={Type})", linkCache?.GetType().FullName ?? "<null>");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BuildConfirmationContext: failed to create concrete resolver, falling back to provided resolver");
            resolver = (ILinkResolver?)linkCache;
        }

        return new ConfirmationContext
        {
            ReverseMap = reverseMap,
            ExcludedPlugins = excludedPlugins,
            AllWeapons = allWeapons,
            AmmoMap = ammoMap,
            Detector = detector,
            Resolver = resolver,
            LinkCache = resolver,
            CancellationToken = cancellationToken
        };
    }

    private void PostProcessUnconfirmedCandidates(
        List<OmodCandidate> candidates,
        Dictionary<string, List<(object Record, string PropName, object PropValue)>> reverseMap,
        string detectorName)
    {
        foreach (var candidate in candidates)
        {
            try
            {
                if (candidate.ConfirmedAmmoChange || !string.IsNullOrEmpty(candidate.ConfirmReason))
                    continue;

                // Compute reverse-reference count for diagnostics
                int refCount = 0;
                if (candidate.BaseWeapon != null)
                {
                    var baseKey = $"{candidate.BaseWeapon.PluginName}:{candidate.BaseWeapon.FormId:X8}";
                    if (reverseMap.TryGetValue(baseKey, out var refs))
                        refCount = refs.Count;
                }

                // Fill reason based on heuristics
                if (candidate.BaseWeapon == null)
                {
                    candidate.ConfirmReason = $"NoBaseWeapon;Refs={refCount};Detector={detectorName}";
                }
                else if (string.Equals(candidate.CandidateType, "COBJ", StringComparison.OrdinalIgnoreCase))
                {
                    candidate.ConfirmReason = candidate.CandidateAmmo == null
                        ? $"COBJ_NoAmmoLink;Refs={refCount};Detector={detectorName}"
                        : $"COBJ_AmmoPresent_NotConfirmed;Refs={refCount};Detector={detectorName}";
                }
                else if (candidate.CandidateAmmo != null && string.IsNullOrEmpty(candidate.CandidateAmmoName))
                {
                    candidate.ConfirmReason = $"CandidateAmmo_UnresolvedName;Refs={refCount};Detector={detectorName}";
                }
                else if (candidate.CandidateAmmo != null)
                {
                    candidate.ConfirmReason = $"CandidateAmmo_Present_NotConfirmed;Refs={refCount};Detector={detectorName}";
                }
                else
                {
                    candidate.ConfirmReason = $"NoAmmoDetected;Refs={refCount};Detector={detectorName}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to post-process candidate");
            }
        }
    }

    private void WriteNoveskeDiagnostic(
        ExtractionContext context,
        Dictionary<string, List<(object Record, string PropName, object PropValue)>> reverseMap,
        List<OmodCandidate> candidates)
    {
        try
        {
            var artifactsDir = _pathService.GetArtifactsDirectory();
            var diagFile = System.IO.Path.Combine(artifactsDir, $"noveske_diagnostic_{context.Timestamp:yyyyMMdd_HHmmss}.csv");

            using var writer = new System.IO.StreamWriter(diagFile, false, System.Text.Encoding.UTF8);
            writer.WriteLine("WeaponFormKey,EditorId,ReverseRefCount,ReverseSourcePlugins,ConfirmedCandidatesCount");

            foreach (var weapon in context.AllWeapons)
            {
                try
                {
                    if (!_mutagenAccessor.TryGetPluginAndIdFromRecord(weapon, out var fileName, out var id))
                        continue;

                    if (!string.Equals(fileName, "noveskeRecceL.esp", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var weaponKey = $"{fileName}:{id:X8}";
                    var editorId = _mutagenAccessor.GetEditorId(weapon);

                    int refCount = 0;
                    var srcPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    if (reverseMap.TryGetValue(weaponKey, out var refs))
                    {
                        refCount = refs.Count;
                        foreach (var entry in refs)
                        {
                            try
                            {
                                if (_mutagenAccessor.TryGetPluginAndIdFromRecord(entry.Record, out var srcPlugin, out _))
                                {
                                    if (!string.IsNullOrEmpty(srcPlugin))
                                        srcPlugins.Add(srcPlugin);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug(ex, "Failed to extract plugin from reverse-ref entry");
                            }
                        }
                    }

                    var confirmed = candidates.Count(c =>
                        c.BaseWeapon != null &&
                        string.Equals(c.BaseWeapon.PluginName, fileName, StringComparison.OrdinalIgnoreCase) &&
                        c.BaseWeapon.FormId == id &&
                        c.ConfirmedAmmoChange);

                    writer.WriteLine($"{weaponKey},{Escape(editorId)},{refCount},\"{Escape(string.Join(";", srcPlugins))}\",{confirmed}");
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to write noveske diagnostic entry");
                }
            }

            writer.Flush();
            context.Progress?.Report($"OMOD diagnostic を生成しました: {diagFile}");
            _logger.LogInformation("Wrote noveske diagnostic: {Path}", diagFile);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write noveske diagnostic");
        }
    }

    private static string Escape(string? s)
    {
        if (s == null) return string.Empty;
        return s.Replace("\"", "\\\"").Replace(',', ';');
    }

    #endregion
}
