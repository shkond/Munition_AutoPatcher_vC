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