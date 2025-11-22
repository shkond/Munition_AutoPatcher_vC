using Microsoft.Extensions.Logging;
using MunitionAutoPatcher.Services.Interfaces;
using MunitionAutoPatcher.Utilities;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using System.Reflection;

namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// Mutagen v0.51-aware detector with type-safe implementation for Fallout4.
/// This implementation uses direct Mutagen types (IObjectModificationGetter, IAmmunitionGetter)
/// for optimal performance and type safety. Falls back to reflection-based detector for
/// non-Mutagen types or unexpected structures.
/// </summary>
public class MutagenV51Detector : ITypedAmmunitionChangeDetector
{
    private readonly ReflectionFallbackDetector _fallback;
    private readonly ILogger<MutagenV51Detector> _logger;
    private readonly IMutagenAccessor _accessor;
    private readonly IResourcedMutagenEnvironment _env;

    public MutagenV51Detector(
        ILogger<MutagenV51Detector> logger,
        ILoggerFactory loggerFactory,
        IMutagenAccessor accessor,
        IResourcedMutagenEnvironment env)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));
        _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
        _env = env ?? throw new ArgumentNullException(nameof(env));
        _fallback = new ReflectionFallbackDetector(loggerFactory.CreateLogger<ReflectionFallbackDetector>());
    }

    public string Name => "MutagenV51Detector";

    /// <summary>
    /// 型安全版の弾薬変更検出（ITypedAmmunitionChangeDetector 実装）
    /// </summary>
    public bool DoesOmodChangeAmmo(
        IObjectModificationGetter omod,
        IAmmunitionGetter? originalAmmo,
        out IAmmunitionGetter? newAmmo)
    {
        newAmmo = null;
        if (omod == null) return false;

        try
        {
            var properties = omod.Properties;
            if (properties == null || properties.Count == 0)
                return false;

            foreach (var prop in properties)
            {
                // Ammo または Projectile プロパティをチェック
                if (prop.Property != ObjectModProperty.Ammo &&
                    prop.Property != ObjectModProperty.Projectile)
                    continue;

                // ValueType が FormIdInt の場合のみ処理（Ammo 変更の標準形式）
                if (prop.ValueType != ObjectModProperty.ValueType.FormIdInt)
                {
                    _logger.LogWarning(
                        "Unexpected ValueType {ValueType} for Ammo/Projectile property in OMOD {EditorId}",
                        prop.ValueType, omod.EditorID);
                    continue;
                }

                // Value1 から FormID を取得（float として格納されているので uint にキャスト）
                var formId = (uint)prop.Value1;
                if (formId == 0)
                    continue;

                // OMOD 自体の ModKey を使って完全な FormKey を構築
                var baseModKey = omod.FormKey.ModKey;
                var ammoFormKey = new FormKey(baseModKey, formId);

                // IMutagenAccessor で IAmmunitionGetter を解決
                var appFormKey = Models.FormKey.FromMutagenFormKey(ammoFormKey);
                if (!_accessor.TryResolveRecord<IAmmunitionGetter>(_env, appFormKey, out var ammoGetter))
                {
                    _logger.LogDebug(
                        "MutagenV51Detector: Failed to resolve ammo FormKey {FormKey} for OMOD {EditorId}",
                        ammoFormKey, omod.EditorID);
                    continue;
                }

                // 元の弾薬と同じかチェック
                if (originalAmmo != null && ammoGetter != null)
                {
                    if (originalAmmo.FormKey.Equals(ammoGetter.FormKey))
                    {
                        // 同じ弾薬 → 変更なし
                        continue;
                    }
                }

                // 新しい弾薬として返す
                newAmmo = ammoGetter;
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MutagenV51Detector: typed detection path failed");
        }

        return false;
    }

    /// <summary>
    /// object ベース版（IAmmunitionChangeDetector 実装）
    /// 型チェック後に typed 版に委譲、または fallback detector を使用
    /// </summary>
    public bool DoesOmodChangeAmmo(object omod, object? originalAmmoLink, out object? newAmmoLink)
    {
        newAmmoLink = null;
        if (omod == null) return false;

        try
        {
            // 1. Typed path: IObjectModificationGetter にキャストできる場合は型安全ロジックを使う
            if (omod is IObjectModificationGetter omodGetter)
            {
                IAmmunitionGetter? originalAmmoTyped = TryExtractAmmoFromObject(originalAmmoLink);
                if (DoesOmodChangeAmmo(omodGetter, originalAmmoTyped, out var newAmmoTyped))
                {
                    newAmmoLink = newAmmoTyped;
                    return true;
                }

                // 型安全ロジックで「弾薬変更なし」と判断された場合は false
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MutagenV51Detector: typed detection path failed, falling back to reflection detector");
        }

        // 2. Typed path が使えない or 例外発生時のみ reflection fallback
        try
        {
            return _fallback.DoesOmodChangeAmmo(omod, originalAmmoLink, out newAmmoLink);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MutagenV51Detector: reflection fallback detector threw an exception");
            newAmmoLink = null;
            return false;
        }
    }

    /// <summary>
    /// object から IAmmunitionGetter を抽出
    /// </summary>
    private static IAmmunitionGetter? TryExtractAmmoFromObject(object? value)
    {
        if (value is IAmmunitionGetter ammo)
            return ammo;

        // IFormLinkGetter<IAmmunitionGetter> の場合は解決が必要だが、
        // ここでは LinkCache を持っていないので null を返す
        // （呼び出し側で FormKey ベース比較を行う）
        return null;
    }
}
