# リファクタ: LinkCacheHelperにヘルパーベースのフォールバックを追加

## 概要
`LinkCacheHelper`に安全なリフレクションヘルパーベースのフォールバックパスを追加し、より堅牢なリンク解決を実現しました。

## 変更内容

### LinkCacheHelper.cs
- FormKeyプロパティ取得が失敗した場合に`MutagenReflectionHelpers.TryGetPropertyValue<object>`を使用するフォールバックを追加
- Resolve(formKey)呼び出しが失敗した場合に`MutagenReflectionHelpers.TryInvokeMethod`を使用するフォールバックを追加
- 既存の解決パスは全て維持(後方互換性を保持)

### テスト
- 新しいテストファイル`LinkCacheHelper_AdditionalTests.cs`を追加
- 複数の解決パスをカバー:
  - インスタンスレベルTryResolve
  - Resolve(FormKey)
  - TryResolve(FormKey, out)
  - 単一引数フォールバック
- テスト結果: 21/21 合格

### CandidateEnumerator.cs (大規模リファクタリング)
- 深いネスト(最大6レベル)を解消→3レベルに削減
- EnumerateCandidatesメソッド(500行超)を15の小メソッドに分割:
  - EnumerateCandidates (メインエントリポイント)
  - EnumerateCobjCandidates, TryCreateCobjCandidate
  - EnumerateReflectedCandidates, CollectWeapons, BuildWeaponKeySet
  - GetPriorityOrderMethods, ProcessCollectionMethod, ProcessRecord
  - TryExtractCandidateFromProperty, BuildCandidateFromWeaponReference
  - DetectAmmoKeyInRecord, FindWeaponEditorId
  - InvokeAndGetWinningOverrides, IsExcluded, TryDetectAmmoForWeapon
- マジックストリング定数を抽出(CobjTypeName, WeaponTypeName, CreatedObjectPropertyName)
- 全メソッドにXMLドキュメントコメント追加
- CS8601/CS8197警告を解消(explicit out型指定、スコープ化pragma)

### ReverseMapBuilder.cs (リファクタリング)
- Build()メソッド(93行、5レベルネスト)を8つの小メソッドに分割:
  - Build (メインエントリポイント)
  - GetCollectionMethods, GetRecordsFromMethod
  - ProcessRecords, ProcessRecord, ProcessProperty
  - TryExtractFormKeyReference, IsExcluded
- 手動リフレクション連鎖(FormKey→ModKey→FileName)を`MutagenReflectionHelpers.TryGetPluginAndIdFromRecord`に置換
- 最大ネスト: 5レベル→3レベルに削減
- 全メソッドにXMLドキュメントコメント追加

### MutagenV51Detector.cs (リファクタリング)
- DoesOmodChangeAmmo内の手動リフレクション(FormKey抽出/比較)を共通ヘルパーに置換
- 置換先: `MutagenReflectionHelpers.TryGetFormKey`
- ネスト削減とエラーログの簡素化
- `using MunitionAutoPatcher.Utilities;` を追加

### ReflectionFallbackDetector.cs (リファクタリング)
- DoesOmodChangeAmmo内の手動リフレクション(FormKey抽出/比較)を共通ヘルパーに置換
- 置換先: `MutagenReflectionHelpers.TryGetFormKey`
- ネスト削減とエラーハンドリングの簡素化
- `using MunitionAutoPatcher.Utilities;` を追加

## ビルド結果
- ✅ ビルド成功(警告0件、エラー0件)
- ✅ 全単体テスト合格(21/21)
- ✅ 全変更でビルド/テスト検証済み

## レビューポイント
- helper-basedフォールバックは既存パスが失敗した場合のみ実行されます
- CandidateEnumerator、ReverseMapBuilder、Detectorのリファクタリングは既存動作を保持しつつコード品質を向上
- メソッド抽出により各関数の責務が明確化され、テスタビリティ向上
- 共通ヘルパーの活用により重複削減と一貫性向上
- 変更は段階的に実施し、各ステップでビルド/テスト検証済み
- 全てのテストが通っています

関連: ステップ4-6(LinkCacheHelper簡素化、CandidateEnumerator/ReverseMapBuilder/Detector層改善) of reflection-cleanup計画  
前回PR: #18
