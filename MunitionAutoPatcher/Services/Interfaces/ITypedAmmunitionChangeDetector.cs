using Mutagen.Bethesda.Fallout4;

namespace MunitionAutoPatcher.Services.Interfaces;

/// <summary>
/// 型安全な弾薬変更検出インターフェース（Fallout4 武器 OMOD 専用）。
/// IAmmunitionChangeDetector の内部実装用として、Mutagen の型を直接使用します。
/// </summary>
/// <remarks>
/// このインターフェースは、MutagenV51Detector のような Fallout4 + Mutagen v0.51 専用の
/// 実装で使用されます。object ベースの IAmmunitionChangeDetector は外部・バージョン非依存の
/// 抽象層として維持され、object 版はこのインターフェースの実装に委譲されます。
/// 
/// FO4 武器弾薬パッチャー専用のため、IWeaponModificationGetter のみを対象とします。
/// 防具 OMOD (IArmorModificationGetter) や NPC OMOD (INpcModificationGetter) は
/// 仕様として無視されます。
/// </remarks>
public interface ITypedAmmunitionChangeDetector : IAmmunitionChangeDetector
{
    /// <summary>
    /// 武器 OMOD が弾薬を変更するか判定（型安全版）。
    /// IWeaponModificationGetter と IAmmunitionGetter を直接扱います。
    /// </summary>
    /// <param name="weaponMod">武器 OMOD レコード（IWeaponModificationGetter）</param>
    /// <param name="originalAmmo">元の弾薬（null 可能）</param>
    /// <param name="newAmmo">新しい弾薬（変更が検出された場合のみ設定）</param>
    /// <returns>弾薬変更が検出された場合は true</returns>
    bool DoesOmodChangeAmmo(
        IWeaponModificationGetter weaponMod,
        IAmmunitionGetter? originalAmmo,
        out IAmmunitionGetter? newAmmo);
}

