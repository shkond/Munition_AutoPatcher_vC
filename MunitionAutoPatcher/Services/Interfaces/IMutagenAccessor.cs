using System.Diagnostics.CodeAnalysis;
using MunitionAutoPatcher.Services.Implementations;
using Mutagen.Bethesda.Plugins.Cache;

namespace MunitionAutoPatcher.Services.Interfaces;

/// <summary>
/// Abstraction layer for Mutagen API access, isolating version-specific reflection calls.
/// </summary>
public interface IMutagenAccessor
{
    /// <summary>
    /// Gets an `ILinkResolver` (wrapper over LinkCache) from the environment if available.
    /// </summary>
    ILinkResolver? GetLinkCache(IResourcedMutagenEnvironment env);

    /// <summary>
    /// Builds a concrete Mutagen <see cref="ILinkCache"/> for the supplied environment when possible.
    /// Returns <c>null</c> if the cache cannot be materialized.
    /// </summary>
    ILinkCache? BuildConcreteLinkCache(IResourcedMutagenEnvironment env);

    /// <summary>
    /// Enumerates record collections by name (e.g., "Weapon", "ObjectMod").
    /// </summary>
    IEnumerable<object> EnumerateRecordCollections(IResourcedMutagenEnvironment env, string collectionName);

    /// <summary>
    /// Gets winning weapon overrides from the environment.
    /// </summary>
    IEnumerable<object> GetWinningWeaponOverrides(IResourcedMutagenEnvironment env);

    /// <summary>
    /// Gets winning ConstructibleObject overrides from the environment.
    /// </summary>
    IEnumerable<object> GetWinningConstructibleObjectOverrides(IResourcedMutagenEnvironment env);

    /// <summary>
    /// Gets winning ConstructibleObject overrides with strong typing.
    /// </summary>
    IEnumerable<Mutagen.Bethesda.Fallout4.IConstructibleObjectGetter> GetWinningConstructibleObjectOverridesTyped(IResourcedMutagenEnvironment env);

    /// <summary>
    /// Gets winning Weapon overrides with strong typing.
    /// </summary>
    IEnumerable<Mutagen.Bethesda.Fallout4.IWeaponGetter> GetWinningWeaponOverridesTyped(IResourcedMutagenEnvironment env);

    /// <summary>
    /// Tries to extract plugin name and FormID from a record object using reflection.
    /// </summary>
    bool TryGetPluginAndIdFromRecord(object record, out string pluginName, out uint formId);

    /// <summary>
    /// Tries to get the EditorID from a record object using reflection.
    /// </summary>
    string GetEditorId(object? record);

    /// <summary>
    /// 型安全なレコード解決（同期版）
    /// Mutagen境界を守るため、LinkCacheへの直接アクセスをこのメソッドに集約します。
    /// </summary>
    /// <typeparam name="T">解決するレコードの型（IMajorRecordGetterのサブタイプ）</typeparam>
    /// <param name="env">Mutagen環境</param>
    /// <param name="formKey">解決対象のFormKey</param>
    /// <param name="record">解決されたレコード（失敗時はnull）</param>
    /// <returns>解決に成功した場合はtrue</returns>
    bool TryResolveRecord<T>(
        IResourcedMutagenEnvironment env,
        Models.FormKey formKey,
        [NotNullWhen(true)] out T? record) 
        where T : class, Mutagen.Bethesda.Plugins.Records.IMajorRecordGetter;

    /// <summary>
    /// 型安全なレコード解決（非同期版）
    /// キャンセレーショントークンに対応した非同期APIです。
    /// </summary>
    /// <typeparam name="T">解決するレコードの型（IMajorRecordGetterのサブタイプ）</typeparam>
    /// <param name="env">Mutagen環境</param>
    /// <param name="formKey">解決対象のFormKey</param>
    /// <param name="ct">キャンセレーショントークン</param>
    /// <returns>解決成功フラグとレコードのタプル</returns>
    Task<(bool Success, T? Record)> TryResolveRecordAsync<T>(
        IResourcedMutagenEnvironment env, 
        Models.FormKey formKey, 
        CancellationToken ct) 
        where T : class, Mutagen.Bethesda.Plugins.Records.IMajorRecordGetter;

    #region 型安全な Weapon プロパティアクセサ

    /// <summary>
    /// Weapon レコードから名前を取得します。
    /// </summary>
    string? GetWeaponName(Mutagen.Bethesda.Fallout4.IWeaponGetter weapon);

    /// <summary>
    /// Weapon レコードから説明文を取得します。
    /// </summary>
    string? GetWeaponDescription(Mutagen.Bethesda.Fallout4.IWeaponGetter weapon);

    /// <summary>
    /// Weapon レコードから基本ダメージを取得します。
    /// </summary>
    float GetWeaponBaseDamage(Mutagen.Bethesda.Fallout4.IWeaponGetter weapon);

    /// <summary>
    /// Weapon レコードから発射レート（RPM）を取得します。
    /// </summary>
    float GetWeaponFireRate(Mutagen.Bethesda.Fallout4.IWeaponGetter weapon);

    /// <summary>
    /// Weapon レコードから弾薬リンクを取得します。
    /// </summary>
    object? GetWeaponAmmoLink(Mutagen.Bethesda.Fallout4.IWeaponGetter weapon);

    #endregion

    #region FormKey / プロパティアクセサ

    /// <summary>
    /// オブジェクトから FormKey を抽出します。
    /// </summary>
    bool TryGetFormKey(object? record, out Mutagen.Bethesda.Plugins.FormKey? formKey);

    /// <summary>
    /// オブジェクトからプロパティ値をリフレクションで取得します。
    /// Accessor 内部に隠蔽されたリフレクションロジックを使用します。
    /// </summary>
    bool TryGetPropertyValue<T>(object? obj, string propertyName, out T? value);

    #endregion
}
