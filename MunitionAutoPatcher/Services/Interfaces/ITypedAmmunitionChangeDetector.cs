using Mutagen.Bethesda.Fallout4;

namespace MunitionAutoPatcher.Services.Interfaces;

/// <summary>
/// 型安全な弾薬変更検出インターフェース（Fallout4 専用）。
/// IAmmunitionChangeDetector の内部実装用として、Mutagen の型を直接使用します。
/// </summary>
/// <remarks>
/// このインターフェースは、MutagenV51Detector のような Fallout4 + Mutagen v0.51 専用の
/// 実装で使用されます。object ベースの IAmmunitionChangeDetector は外部・バージョン非依存の
/// 抽象層として維持され、このインターフェースの実装はそちらに委譲されます。
/// </remarks>
public interface ITypedAmmunitionChangeDetector : IAmmunitionChangeDetector
{
    /// <summary>
    /// 型安全版の弾薬変更検出メソッド。
    /// IObjectModificationGetter と IAmmunitionGetter を直接扱います。
    /// </summary>
    /// <param name="omod">OMOD レコード（IObjectModificationGetter）</param>
    /// <param name="originalAmmo">元の弾薬（null 可能）</param>
    /// <param name="newAmmo">新しい弾薬（変更が検出された場合のみ設定）</param>
    /// <returns>弾薬変更が検出された場合は true</returns>
    bool DoesOmodChangeAmmo(
        IObjectModificationGetter omod,
        IAmmunitionGetter? originalAmmo,
        out IAmmunitionGetter? newAmmo);
}
