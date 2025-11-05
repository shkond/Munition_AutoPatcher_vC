using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Mutagen.Bethesda.Plugins.Cache;
using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Interfaces;
using MunitionAutoPatcher.Utilities;

namespace MunitionAutoPatcher.Services.Implementations;

public class OrchestratorService : IOrchestrator
{
    private readonly IWeaponsService _weaponsService;
    private readonly IRobCoIniGenerator _iniGenerator;
    private readonly ILoadOrderService _loadOrderService;
    private readonly IWeaponOmodExtractor _omodExtractor;
    private readonly IEspPatchService _espPatchService;
    private readonly IMutagenEnvironmentFactory _mutagenEnvironmentFactory;
    private readonly IMutagenAccessor _mutagenAccessor;
    private readonly IConfigService _configService;
    private bool _isInitialized;

    public OrchestratorService(
        IWeaponsService weaponsService,
        IRobCoIniGenerator iniGenerator,
        ILoadOrderService loadOrderService,
        IWeaponOmodExtractor omodExtractor,
        IMutagenEnvironmentFactory mutagenEnvironmentFactory,
        IMutagenAccessor mutagenAccessor,
        IConfigService configService,
        IEspPatchService espPatchService)
    {
        _weaponsService = weaponsService ?? throw new ArgumentNullException(nameof(weaponsService));
        _iniGenerator = iniGenerator ?? throw new ArgumentNullException(nameof(iniGenerator));
        _loadOrderService = loadOrderService ?? throw new ArgumentNullException(nameof(loadOrderService));
        _omodExtractor = omodExtractor ?? throw new ArgumentNullException(nameof(omodExtractor));
        _mutagenEnvironmentFactory = mutagenEnvironmentFactory ?? throw new ArgumentNullException(nameof(mutagenEnvironmentFactory));
        _mutagenAccessor = mutagenAccessor ?? throw new ArgumentNullException(nameof(mutagenAccessor));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _espPatchService = espPatchService ?? throw new ArgumentNullException(nameof(espPatchService));
    }

    public bool IsInitialized => _isInitialized;

    public async Task<bool> InitializeAsync()
    {
        if (_isInitialized)
        {
            return true;
        }

        try
        {
            var loadOrderValid = await _loadOrderService.ValidateLoadOrderAsync();
            _isInitialized = loadOrderValid;
            return loadOrderValid;
        }
        catch (Exception ex)
        {
            AppLogger.Log("OrchestratorService: initialization failed", ex);
            _isInitialized = false;
            return false;
        }
    }

    public async Task<List<WeaponData>> ExtractWeaponsAsync(IProgress<string>? progress = null)
    {
        var weapons = new List<WeaponData>();
        try
        {
            progress?.Report("武器データの抽出を開始しています...");
            weapons = await _weaponsService.ExtractWeaponsAsync(progress) ?? new List<WeaponData>();
            progress?.Report($"抽出完了: {weapons.Count}個の武器が見つかりました");

            var mode = _configService.GetOutputMode();
            if (string.Equals(mode, "esp", StringComparison.OrdinalIgnoreCase))
            {
                await GenerateEspPatchInternalAsync(weapons, progress, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Log("OrchestratorService: weapon extraction failed", ex);
            progress?.Report($"エラー: 武器データの抽出に失敗しました: {ex.Message}");
        }

        return weapons;
    }

    public async Task<bool> GenerateMappingsAsync(List<WeaponData> weapons, IProgress<string>? progress = null)
    {
        var weaponCount = weapons?.Count ?? 0;
        progress?.Report($"マッピングを生成しています... (武器 {weaponCount} 件)");
        try
        {
            var candidates = weaponCount > 0
                ? await _omodExtractor.ExtractCandidatesAsync(progress)
                : new List<OmodCandidate>();
            progress?.Report($"候補を {candidates.Count} 件検出しました");
            return true;
        }
        catch (OperationCanceledException)
        {
            progress?.Report("マッピング生成がキャンセルされました");
            return false;
        }
        catch (Exception ex)
        {
            AppLogger.Log("OrchestratorService: mapping generation failed", ex);
            progress?.Report($"エラー: マッピング生成中に例外が発生しました: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> GenerateIniAsync(string outputPath, List<WeaponMapping> mappings, IProgress<string>? progress = null)
    {
        progress?.Report("INIファイルを生成しています...");

        try
        {
            await _iniGenerator.GenerateIniAsync(outputPath, mappings ?? new List<WeaponMapping>(), progress);
            progress?.Report($"INIファイル生成完了: {outputPath}");
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Log("OrchestratorService: INI generation failed", ex);
            progress?.Report($"エラー: INI 生成に失敗しました: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> GeneratePatchAsync(string outputPath, List<WeaponData> weapons, IProgress<string>? progress = null)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            try
            {
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    _configService.SetOutputDirectory(directory);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log("OrchestratorService: failed to apply output directory override for ESP", ex);
            }
        }

        return await GenerateEspPatchInternalAsync(weapons ?? new List<WeaponData>(), progress, CancellationToken.None);
    }

    private async Task<bool> GenerateEspPatchInternalAsync(List<WeaponData> weapons, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        weapons ??= new List<WeaponData>();
        progress?.Report("ESP パッチ生成を開始します...");
        progress?.Report($"ESP 生成対象の武器件数: {weapons.Count}");

        try
        {
            using var envRes = _mutagenEnvironmentFactory.Create();
            var resolver = _mutagenAccessor.GetLinkCache(envRes);
            var nativeCache = TryGetNativeLinkCache(resolver);
            if (nativeCache == null)
            {
                progress?.Report("エラー: Mutagen LinkCache を取得できませんでした");
                return false;
            }

            var extraction = new ExtractionContext
            {
                Environment = envRes,
                LinkCache = resolver,
                FormLinkCache = nativeCache,
                RepoRoot = ResolveRepoRoot(),
                Progress = progress,
                Timestamp = DateTime.Now,
                CancellationToken = cancellationToken,
                ExcludedPlugins = BuildExcludedPluginSet()
            };

            var confirmation = new ConfirmationContext
            {
                ExcludedPlugins = new HashSet<string>(extraction.ExcludedPlugins, StringComparer.OrdinalIgnoreCase),
                AllWeapons = new List<object>(),
                Resolver = resolver,
                LinkCache = resolver,
                CancellationToken = cancellationToken
            };

            List<OmodCandidate> candidates;
            try
            {
                candidates = await _omodExtractor.ExtractCandidatesAsync(progress, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                progress?.Report("ESP パッチ生成がキャンセルされました");
                return false;
            }

            await _espPatchService.BuildAsync(extraction, confirmation, candidates, cancellationToken);
            progress?.Report("ESP パッチ生成が完了しました");
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Log("OrchestratorService: ESP generation failed", ex);
            progress?.Report($"エラー: ESP 生成に失敗しました: {ex.Message}");
            return false;
        }
    }

    private static ILinkCache? TryGetNativeLinkCache(ILinkResolver? resolver)
    {
        if (resolver == null)
        {
            AppLogger.Log("TryGetNativeLinkCache: resolver is null");
            return null;
        }

        try
        {
            AppLogger.Log($"TryGetNativeLinkCache: resolverType={resolver.GetType().FullName}");
            var field = resolver.GetType().GetField("_linkCache", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                AppLogger.Log("TryGetNativeLinkCache: _linkCache field not found on resolver");
            }
            var value = field?.GetValue(resolver);
            AppLogger.Log($"TryGetNativeLinkCache: fieldType={field?.FieldType.FullName}, valueType={value?.GetType().FullName}");
            if (value is ILinkCache cache)
            {
                return cache;
            }
            else
            {
                AppLogger.Log("TryGetNativeLinkCache: underlying value is not Mutagen ILinkCache");
            }
        }
        catch (Exception ex)
        {
            AppLogger.Log("OrchestratorService: failed to unwrap native LinkCache", ex);
        }

        return null;
    }

    private HashSet<string> BuildExcludedPluginSet()
    {
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var plugin in _configService.GetExcludedPlugins() ?? Enumerable.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(plugin))
                {
                    excluded.Add(plugin.Trim());
                }
            }

            if (_configService.GetExcludeFallout4Esm())
            {
                excluded.Add("Fallout4.esm");
            }

            if (_configService.GetExcludeDlcEsms())
            {
                excluded.UnionWith(new[]
                {
                    "DLCRobot.esm",
                    "DLCworkshop01.esm",
                    "DLCCoast.esm",
                    "DLCworkshop02.esm",
                    "DLCworkshop03.esm",
                    "DLCNukaWorld.esm"
                });
            }

        }
        catch (Exception ex)
        {
            AppLogger.Log("OrchestratorService: failed to build excluded plugin set", ex);
        }

        return excluded;
    }

    private static string ResolveRepoRoot()
    {
        try
        {
            return RepoUtils.FindRepoRoot();
        }
        catch (Exception ex)
        {
            AppLogger.Log("OrchestratorService: failed to resolve repository root", ex);
            return Directory.GetCurrentDirectory();
        }
    }
}
