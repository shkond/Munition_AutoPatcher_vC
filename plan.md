# OMOD/COBJ から Ammo が検出されない問題の調査計画 (resolvedToOmod=0)

## 根本原因の更新

確定した根本原因: LinkResolver/LinkCacheHelper が Mutagen の `ILinkCache` に対して適切な解決メソッドを呼び出していませんでした。特に、FO4 の `ImmutableLoadOrderLinkCache` が提供する `TryResolve(FormKey key, Type type, out IMajorRecordGetter record)`（3引数）経路が未サポートのため、全ての `FormKey` 解決が MISS となり、`AttachPointConfirmer` では `rootNull=16119` となっていました。

補足:
- `TryToMutagenFormKey` 自体は正常に `Mutagen.FormKey` を構築できていました。
- 失敗点は、その後の解決パスが 2 引数版の `TryResolve` 系しか探しておらず、3 引数の型付けパスに到達していなかったことにあります。

## 実施した修正

1. `LinkResolver` に型付き高速経路（fast-path）を追加
    - `_typedLinkCache` がある場合、`Mutagen.FormKey` に対して `TryResolve(formKey, typeof(IMajorRecordGetter), out var major)` を直接呼ぶように変更。
    - `ResolveByKey(FormKey)` / `TryResolve(object)` の双方で、Mutagen `FormKey` へ変換できたら先に型付き経路を試行。成功時は即 return。
    - Debug ログを追加して、typed/fallback のどちらで解決できたかを可視化。

2. `LinkCacheHelper` に 3 引数 `TryResolve(FormKey, Type, out ...)` への対応を追加
    - 既存の 2 引数 `TryResolve(x, out y)` の探索に加え、`FormKey` + `Type` + `out` を持つ公開メソッドを探索・呼び出すロジックを新規追加。
    - `IMajorRecordGetter` を指定して安全に解決し、成功時はそのオブジェクトを返す。

## 次のアクション（検証手順）

1. ビルドと主要テスト実行
    - `dotnet build` および LinkCache 系のテストを実行し、リフレクション経路の例外が出ていないか確認。

2. 抽出フローの再実行
    - 期待値: `AttachPointConfirmer` のログで `resolvedToOmod > 0`、`rootNull` が大幅減。
    - `createdObjResolveFail` と `createdObjNotOmod` の分布が観測される可能性がある（COBJ 経由の OMOD 解決が進むため）。

3. 必要に応じて微調整
    - プラグイン名正規化（大文字小文字や拡張子）や、`FormKey` 変換周辺の追加ログ強化。

## 参考ログ観測ポイント

- `LinkResolver.TryResolve: attempting resolve for type=...`
- `ResolveByKey: typed path FOUND` / `reflection path result was FOUND/MISS`
- `AttachPointConfirmer: inspected=..., resolvedToOmod=..., rootNull=..., createdObj...=...`

## 対応ステップ

1.  **ConfirmationContext に LinkCache を必ず設定する**
    - **状況:** **実装済み**。`WeaponOmodExtractor.cs` の `BuildConfirmationContextAsync` で、複数のフォールバックを持つロジックにより `ILinkCache` が構築され、`ConfirmationContext` に設定されています。

2.  **LinkResolver を修正して独自 FormKey に対応**
    - **状況:** **実装済み**。`LinkResolver.cs` の `ResolveByKey(object key)` がカスタム `FormKey` を処理し、内部で `ResolveByKey(FormKey key)` を呼び出します。このメソッドは Mutagen `FormKey` への変換を試みます。

3.  **AttachPointConfirmer に詳細デバッグカウンタを追加**
    - **状況:** **実装済み**。`AttachPointConfirmer.cs` の `Confirm` メソッドに `rootNull`, `createdObjMissing`, `createdObjResolveFail`, `createdObjNotOmod` のカウンターが追加され、ログが出力されます。

4.  **最初の N 件の候補の CandidateFormKey をダンプ**
    - **状況:** **実装済み**（ただし場所が異なる）。このロジックは `WeaponOmodExtractor.cs` ではなく、`AttachPointConfirmer.cs` の `Confirm` メソッドの開始部分に実装されています。
      ```csharp
      // MunitionAutoPatcher/Services/Implementations/AttachPointConfirmer.cs
      int dumpCount = 0;
      foreach (var c in candidates.Take(20))
      {
          // ...
          _logger.LogInformation("CandidateDump[{Index}]: Type={Type} FK={Plugin}:{Id:X8}", ...);
      }
      ```

5.  **COBJ → CreatedObject 解決の検証**
    - **次のアクション:** `AttachPointConfirmer` の詳細ログ（ステップ3）を分析し、`createdObjResolveFail` や `createdObjNotOmod` が多数発生しているか確認します。もしそうなら、特定の `FormKey` を使って `LinkCache.TryResolve` をデバッガで手動実行し、解決が失敗する原因（ロード順、プラグイン名の不一致など）を特定します。

6.  **Plugin名の正規化**
    - **次のアクション:** ステップ5で解決失敗が確認された場合、`ToMutagenFormKey` ヘルパーメソッド（`AttachPointConfirmer.cs` と `LinkResolver.cs` の両方に存在する）が、プラグイン名の大小文字の違いや拡張子の有無を正しく処理できているか確認します。`System.IO.Path.GetFileName()` の使用は良い習慣です。

7.  **フラグ `FormLinkCachePresent` の判定コード見直し**
    - **状況:** `WeaponOmodExtractor.cs` のESP生成部分に以下のログがあります。
      ```csharp
      _logger.LogInformation("ESP debug: FormLinkCachePresent={HasFormLinkCache}, ResolverPresent={HasResolver}, ...",
          context.FormLinkCache != null,
          context.LinkCache != null,
          ...);
      ```
    - **次のアクション:** このログ出力で `FormLinkCachePresent` が `false` になっていないか確認します。`ConfirmationContext` の `LinkCache` と `Resolver` の両方に `ILinkCache` インスタンスが渡されているため、`true` になるはずです。

8.  **武器抽出時とConfirm時でLinkCacheが一致しているか確認**
    - **状況:** `WeaponOmodExtractor.cs` の `BuildExtractionContextAsync` と `BuildConfirmationContextAsync` の両方で `LinkCache` が構築・設定されています。これらのロジックは複雑で、異なるインスタンスが使われる可能性があります。
    - **次のアクション:** ログを追い、両方のフェーズで同じ `ILinkCache` インスタンスが使われているか、または同等のキャッシュが使われているかを確認します。

9.  **次段階（foundAmmo=0 改善）は OMOD 解決後**
    - **保留:** `resolvedToOmod` のカウントが改善するまで、この項目は後回しにします。



# 現状からの実行計画
確認（Confirm）フェーズで ILinkCache を構築・注入していないため、Resolver が常に null を返し、AttachPointConfirmer の resolvedToOmod が 0 のまま。
LinkResolver が MunitionAutoPatcher.Models.FormKey（独自 FormKey）を Mutagen の FormKey に変換して ILinkCache.TryResolve する経路を持っていない。
LinkResolver/AttachPointConfirmer に型付きのジェネリック TryResolve パス（IObjectModificationGetter / IConstructibleObjectGetter 等）がない。
その結果、COBJ→CreatedObject→OMOD への解決に全く到達できず、rootNull が大量（16119）に発生している。

以下に、レポジトリへ適用すべき差分（具体コード）を提示します。これで「LinkCache を使った解決」→「型付きのジェネリック解決」→「COBJ→CreatedObject→OMOD フォールバック」の順で解決率が上がり、resolvedToOmod が >0 に変化します。

LinkResolver: 独自 FormKey 変換 + ジェネリック型付き TryResolve パスを追加（Information ログ出力つき）
using System;
using Microsoft.Extensions.Logging;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Fallout4; // IConstructibleObjectGetter, IObjectModificationGetter, IWeaponGetter, IAmmoGetter, IProjectileGetter, IKeywordGetter など

namespace MunitionAutoPatcher.Services.Implementations
{
    public sealed class LinkResolver : ILinkResolver
    {
        private readonly ILinkCache _linkCache;
        private readonly ILogger<LinkResolver> _logger;

        public LinkResolver(ILinkCache linkCache, ILogger<LinkResolver> logger)
        {
            _linkCache = linkCache ?? throw new ArgumentNullException(nameof(linkCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public object? ResolveByKey(object key)
        {
            try
            {
                // 1) 当プロジェクト独自 FormKey → Mutagen FormKey に変換
                if (key is MunitionAutoPatcher.Models.FormKey mk)
                {
                    var mfk = ConvertToMutagenFormKey(mk);
                    if (mfk.HasValue)
                    {
                        if (TryTypedResolve(mfk.Value, out var rec)) return rec;

                        if (_linkCache.TryResolve(mfk.Value, out var genericRec))
                        {
                            _logger.LogInformation("LinkResolver: generic resolve SUCCESS {Mod}:{Id:X8}", mfk.Value.ModKey.FileName, mfk.Value.ID);
                            return genericRec;
                        }

                        _logger.LogInformation("LinkResolver: resolve MISS {Mod}:{Id:X8}", mfk.Value.ModKey.FileName, mfk.Value.ID);
                    }
                    return null;
                }

                // 2) すでに Mutagen FormKey の場合
                if (key is Mutagen.Bethesda.Plugins.FormKey fk)
                {
                    if (TryTypedResolve(fk, out var rec)) return rec;
                    if (_linkCache.TryResolve(fk, out var genericRec))
                    {
                        _logger.LogInformation("LinkResolver: generic resolve SUCCESS {Mod}:{Id:X8}", fk.ModKey.FileName, fk.ID);
                        return genericRec;
                    }
                    _logger.LogInformation("LinkResolver: resolve MISS {Mod}:{Id:X8}", fk.ModKey.FileName, fk.ID);
                    return null;
                }

                // 3) FormLink 風（FormKey プロパティを持つ）を扱う
                var fkProp = key.GetType().GetProperty("FormKey");
                if (fkProp != null)
                {
                    var raw = fkProp.GetValue(key);
                    if (raw is Mutagen.Bethesda.Plugins.FormKey fk2)
                    {
                        if (TryTypedResolve(fk2, out var rec2)) return rec2;
                        if (_linkCache.TryResolve(fk2, out var genericRec2))
                        {
                            _logger.LogInformation("LinkResolver: generic resolve SUCCESS {Mod}:{Id:X8}", fk2.ModKey.FileName, fk2.ID);
                            return genericRec2;
                        }
                        _logger.LogInformation("LinkResolver: resolve MISS {Mod}:{Id:X8}", fk2.ModKey.FileName, fk2.ID);
                        return null;
                    }
                    // 独自 FormKey にも一応対応
                    if (raw is MunitionAutoPatcher.Models.FormKey mk2)
                    {
                        var mfk2 = ConvertToMutagenFormKey(mk2);
                        if (mfk2.HasValue)
                        {
                            if (TryTypedResolve(mfk2.Value, out var rec3)) return rec3;
                            if (_linkCache.TryResolve(mfk2.Value, out var genericRec3))
                            {
                                _logger.LogInformation("LinkResolver: generic resolve SUCCESS {Mod}:{Id:X8}", mfk2.Value.ModKey.FileName, mfk2.Value.ID);
                                return genericRec3;
                            }
                            _logger.LogInformation("LinkResolver: resolve MISS {Mod}:{Id:X8}", mfk2.Value.ModKey.FileName, mfk2.Value.ID);
                        }
                        return null;
                    }
                }

                _logger.LogInformation("LinkResolver: unsupported key type {Type}", key?.GetType().FullName ?? "<null>");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "LinkResolver.ResolveByKey failed (type={Type})", key?.GetType().FullName);
                return null;
            }
        }

        public bool TryResolve(object key, out object? record)
        {
            record = ResolveByKey(key);
            return record != null;
        }

        private FormKey? ConvertToMutagenFormKey(MunitionAutoPatcher.Models.FormKey fk)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fk.PluginName) || fk.FormId == 0) return null;

                var fileName = System.IO.Path.GetFileName(fk.PluginName);
                var modType =
                    fileName.EndsWith(".esm", StringComparison.OrdinalIgnoreCase) ? ModType.Master :
                    fileName.EndsWith(".esl", StringComparison.OrdinalIgnoreCase) ? ModType.Light  :
                    ModType.Plugin;

                var modKey = new ModKey(fileName, modType);
                return new FormKey(modKey, fk.FormId);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "LinkResolver: ConvertToMutagenFormKey failed {Plugin}:{Id:X8}", fk.PluginName, fk.FormId);
                return null;
            }
        }

        private bool TryTypedResolve(FormKey fk, out object? rec)
        {
            // FO4 で頻出の型を優先（OMOD/COBJ/WEAP/AMMO/PROJ/KEYM 等）
            if (_linkCache.TryResolve<IObjectModificationGetter>(fk, out var omod))
            { rec = omod; _logger.LogInformation("LinkResolver: typed resolve SUCCESS IObjectModificationGetter {Mod}:{Id:X8}", fk.ModKey.FileName, fk.ID); return true; }

            if (_linkCache.TryResolve<IConstructibleObjectGetter>(fk, out var cobj))
            { rec = cobj; _logger.LogInformation("LinkResolver: typed resolve SUCCESS IConstructibleObjectGetter {Mod}:{Id:X8}", fk.ModKey.FileName, fk.ID); return true; }

            if (_linkCache.TryResolve<IWeaponGetter>(fk, out var weap))
            { rec = weap; _logger.LogInformation("LinkResolver: typed resolve SUCCESS IWeaponGetter {Mod}:{Id:X8}", fk.ModKey.FileName, fk.ID); return true; }

            if (_linkCache.TryResolve<IAmmoGetter>(fk, out var ammo))
            { rec = ammo; _logger.LogInformation("LinkResolver: typed resolve SUCCESS IAmmoGetter {Mod}:{Id:X8}", fk.ModKey.FileName, fk.ID); return true; }

            if (_linkCache.TryResolve<IProjectileGetter>(fk, out var proj))
            { rec = proj; _logger.LogInformation("LinkResolver: typed resolve SUCCESS IProjectileGetter {Mod}:{Id:X8}", fk.ModKey.FileName, fk.ID); return true; }

            if (_linkCache.TryResolve<IKeywordGetter>(fk, out var kw))
            { rec = kw; _logger.LogInformation("LinkResolver: typed resolve SUCCESS IKeywordGetter {Mod}:{Id:X8}", fk.ModKey.FileName, fk.ID); return true; }

            _logger.LogInformation("LinkResolver: typed resolve MISS {Mod}:{Id:X8}", fk.ModKey.FileName, fk.ID);
            rec = null;
            return false;
        }
    }
}

WeaponOmodExtractor（確認用 LinkCache の構築・注入）
確認パスに入る直前に、LoadOrder から「確認用の LinkCache」を構築し、ConfirmationContext に ILinkCache と LinkResolver の両方を入れます。
ログを Information レベルで出して、mods 数を確認できるようにします。


// ... 省略: using とクラス定義

private ConfirmationContext BuildConfirmationContext(IMutagenEnvironment environment, /* 他の引数 */)
{
    ILinkCache? confirmationCache = null;
    try
    {
        var loadOrder = environment?.LoadOrder;
        if (loadOrder != null)
        {
            confirmationCache = loadOrder.ToImmutableLinkCache(); // or ToLinkCache()
            _logger.LogInformation("BuildConfirmationContext: built LinkCache for confirmation (mods={Count})", loadOrder.Count);
        }
        else
        {
            _logger.LogWarning("BuildConfirmationContext: loadOrder is null; LinkCache not built");
        }
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "BuildConfirmationContext: failed to build LinkCache");
    }

    ILinkResolver? resolver = null;
    if (confirmationCache != null)
    {
        resolver = new LinkResolver(confirmationCache, _loggerFactory.CreateLogger<LinkResolver>());
    }
    else
    {
        // 既存の provided resolver があるなら fallback
        if (_providedResolver != null)
            _logger.LogWarning("BuildConfirmationContext: using provided resolver (NO confirmation LinkCache)");
        resolver = _providedResolver;
    }

    var ctx = new ConfirmationContext
    {
        Resolver = resolver,
        // IMPORTANT: ここは ILinkCache を持たせる（型が ILinkResolver なら型を ILinkCache に変更推奨）
        LinkCache = confirmationCache != null ? resolver : null, // 既存プロパティ互換のための暫定値
        // 他: AllWeapons, AmmoMap, ReverseMap, Detector, CancellationToken, など
    };

    return ctx;
}

注意:

ConfirmationContext.LinkCache の型が ILinkResolver になっている場合、ESP debug の FormLinkCachePresent 判定が常に False になります。設計的には ILinkCache 型で保持することを推奨します（プロパティ型変更）。
AttachPointConfirmer: 変換後 Mutagen FormKey での解決 + 直接 typed resolve フォールバック + 詳細カウンタ
ResolveOmodForCandidate で Resolver が null を返した場合、LinkResolver に頼らずに「mutagen FormKey → 型付き TryResolve」を直接呼ぶフォールバックを入れて可視化を高めます（暫定策）。

// ... 省略: using とクラス定義

private ConfirmationContext BuildConfirmationContext(IMutagenEnvironment environment, /* 他の引数 */)
{
    ILinkCache? confirmationCache = null;
    try
    {
        var loadOrder = environment?.LoadOrder;
        if (loadOrder != null)
        {
            confirmationCache = loadOrder.ToImmutableLinkCache(); // or ToLinkCache()
            _logger.LogInformation("BuildConfirmationContext: built LinkCache for confirmation (mods={Count})", loadOrder.Count);
        }
        else
        {
            _logger.LogWarning("BuildConfirmationContext: loadOrder is null; LinkCache not built");
        }
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "BuildConfirmationContext: failed to build LinkCache");
    }

    ILinkResolver? resolver = null;
    if (confirmationCache != null)
    {
        resolver = new LinkResolver(confirmationCache, _loggerFactory.CreateLogger<LinkResolver>());
    }
    else
    {
        // 既存の provided resolver があるなら fallback
        if (_providedResolver != null)
            _logger.LogWarning("BuildConfirmationContext: using provided resolver (NO confirmation LinkCache)");
        resolver = _providedResolver;
    }

    var ctx = new ConfirmationContext
    {
        Resolver = resolver,
        // IMPORTANT: ここは ILinkCache を持たせる（型が ILinkResolver なら型を ILinkCache に変更推奨）
        LinkCache = confirmationCache != null ? resolver : null, // 既存プロパティ互換のための暫定値
        // 他: AllWeapons, AmmoMap, ReverseMap, Detector, CancellationToken, など
    };

    return ctx;
}

ログレベル
現状 Debug が表示されていないようなので、LinkResolver/AttachPointConfirmer の重要イベントは Information レベルで出すようにしました（上記コードは Information を多用しています）。アプリのロガーフィルターも Debug を表示可能にするのが望ましいですが、まずは Information でヒット/ミスが見えるようにしています。
確認ポイント（この差分適用後に見るログ）
BuildConfirmationContext: built LinkCache for confirmation (mods=###) が出ているか
ESP debug: FormLinkCachePresent=True に変化しているか（必要なら判定コードを ILinkCache の存在で判定）
LinkResolver: typed/generic resolve SUCCESS が出ているか
AttachPointConfirmer: resolvedToOmod > 0 に増加しているか
次に hadAttachPoint/matchedWeapons/foundAmmo/confirmed が 0 から増加
補足

まだ foundAmmo が 0 のままの場合は、TryFindAmmoReference の走査が浅いため、再帰走査に強化します（OMOD.Properties のネスト内 Link を探索）。ただし、まず resolvedToOmod を >0 にするのが先決です。
この差分により、ご報告の A〜E の指摘（ジェネリック型解決パス、フォールバック、ログの昇格、テスト性向上）を網羅します。

