using Microsoft.Extensions.Logging;
using MunitionAutoPatcher.Services.Interfaces;
using MunitionAutoPatcher.Utilities;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;

namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// Mutagen v0.51-aware detector with type-safe implementation for Fallout4 weapon OMODs.
/// This implementation uses IWeaponModificationGetter and Weapon.Property.Ammo for optimal
/// performance and type safety. Falls back to reflection-based detector for non-weapon OMODs.
/// </summary>
/// <remarks>
/// FO4 武器弾薬パッチャー専用設計：
/// - IWeaponModificationGetter のみを処理対象とする
/// - Weapon.Property.Ammo による型安全な enum 判定
/// - 防具/NPC OMOD は仕様として無視される
/// </remarks>
public class MutagenV51Detector : ITypedAmmunitionChangeDetector
{
    private readonly ReflectionFallbackDetector _fallback;
    private readonly ILogger<MutagenV51Detector> _logger;
    private readonly IMutagenAccessor _accessor;
    private readonly IResourcedMutagenEnvironment _env;
    private readonly IOmodPropertyAdapter _propertyAdapter;

    public MutagenV51Detector(
        ILogger<MutagenV51Detector> logger,
        ILoggerFactory loggerFactory,
        IMutagenAccessor accessor,
        IResourcedMutagenEnvironment env,
        IOmodPropertyAdapter propertyAdapter)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));
        _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
        _env = env ?? throw new ArgumentNullException(nameof(env));
        _propertyAdapter = propertyAdapter ?? throw new ArgumentNullException(nameof(propertyAdapter));
        _fallback = new ReflectionFallbackDetector(loggerFactory.CreateLogger<ReflectionFallbackDetector>(), accessor);
    }

    public string Name => "MutagenV51Detector";

    /// <summary>
    /// 武器 OMOD の弾薬変更を型安全に検出（ITypedAmmunitionChangeDetector 実装）
    /// </summary>
    public bool DoesOmodChangeAmmo(
        IWeaponModificationGetter weaponMod,
        IAmmunitionGetter? originalAmmo,
        out IAmmunitionGetter? newAmmo)
    {
        ArgumentNullException.ThrowIfNull(weaponMod);

        newAmmo = null;
        var properties = weaponMod.Properties;
        if (properties == null || properties.Count == 0)
            return false;

        try
        {
            foreach (var prop in properties)
            {
                // ✓ 型安全な enum 判定: Weapon.Property.Ammo のみ処理
                if (prop.Property != Weapon.Property.Ammo)
                    continue;

                // FormKey を取得（2段階アプローチ）
                if (!_propertyAdapter.TryExtractFormKeyFromAmmoProperty(prop, weaponMod, out var ammoFormKey))
                    continue;

                // LinkCache 経由で IAmmunitionGetter を解決
                var appFormKey = Models.FormKey.FromMutagenFormKey(ammoFormKey);
                if (!_accessor.TryResolveRecord<IAmmunitionGetter>(_env, appFormKey, out var ammo) || ammo == null)
                {
                    _logger.LogDebug(
                        "MutagenV51Detector: Failed to resolve ammo FormKey {FormKey} for weapon OMOD {EditorId}",
                        ammoFormKey, weaponMod.EditorID);
                    continue;
                }

                // 元の弾薬と同じ場合はスキップ
                if (originalAmmo != null && ammo.FormKey.Equals(originalAmmo.FormKey))
                    continue;

                newAmmo = ammo;
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MutagenV51Detector: typed detection path failed for weapon OMOD {EditorId}",
                weaponMod.EditorID ?? "(unknown)");
        }

        return false;
    }

    /// <summary>
    /// object ベース版（IAmmunitionChangeDetector 実装）
    /// 型チェック後に武器専用 typed 版に委譲、または fallback detector を使用
    /// </summary>
    /// <remarks>
    /// NOTE: Currently we only support weapon modification OMODs (IWeaponModificationGetter).
    /// Armor/NPC/etc. modifications are intentionally ignored for this FO4 ammo patcher.
    /// If we later add support for other record types, extend the type checks below.
    /// </remarks>
    public bool DoesOmodChangeAmmo(object omod, object? originalAmmoLink, out object? newAmmoLink)
    {
        newAmmoLink = null;
        if (omod == null) return false;

        if (omod is IAObjectModificationGetter omodTyped)
        {
            if (TryDetectAmmoChangeTyped(omodTyped, originalAmmoLink, out var candidate))
            {
                newAmmoLink = candidate;
                return true;
            }
        }

        try
        {
            // 型判定: IWeaponModificationGetter のみ処理（FO4 武器専用）
            if (omod is not IWeaponModificationGetter weaponMod)
            {
                _logger.LogDebug(
                    "MutagenV51Detector: non-weapon modification type {Type} ignored (FO4 weapon ammo patcher)",
                    omod.GetType().Name);
                return false;
            }

            // originalAmmoLink から IAmmunitionGetter を抽出
            var originalAmmo = originalAmmoLink as IAmmunitionGetter;

            // 型安全版に委譲
            if (!DoesOmodChangeAmmo(weaponMod, originalAmmo, out var newAmmoTyped))
                return false;

            newAmmoLink = newAmmoTyped;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MutagenV51Detector: weapon OMOD detection failed, falling back to reflection detector");
        }

        // Fallback: 例外発生時または非武器 OMOD の場合
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

    private bool TryDetectAmmoChangeTyped(
        IAObjectModificationGetter omod,
        object? originalAmmoLink,
        out object? newAmmoLink)
    {
        newAmmoLink = null;

        if (omod is not IWeaponModificationGetter weaponMod)
            return false;

        var originalAmmo = originalAmmoLink as IAmmunitionGetter;
        if (!DoesOmodChangeAmmo(weaponMod, originalAmmo, out var newAmmoTyped))
            return false;

        newAmmoLink = newAmmoTyped;
        return true;
    }

}
