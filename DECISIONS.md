# Munition AutoPatcher vC - 決定事項と開発記録

## プロジェクト概要

Fallout 4 の武器 MOD に対して RobCo Patcher の設定ファイル（INI）を自動生成する WPF アプリケーション。Mutagen を使用してプラグインから武器データを抽出し、弾薬とのマッピングを行い、パッチ設定を生成します。

**最終更新**: 2025-11-01  
**現在のブランチ**: `merge/backup-into-main`  
**最新コミット**: feat(extraction): Refactor weapon data extraction and add unit tests

---

## アーキテクチャ上の主要決定事項

### 1. MVVM パターンの採用
- **View**: XAML で定義された純粋な UI 層
- **ViewModel**: UI ステートとロジック管理（データバインディング、コマンド）
- **Model**: データ構造（WeaponData、StrategyConfig など）

### 2. 依存性注入（DI）とサービス層
- `Microsoft.Extensions.DependencyInjection` を使用
- すべての依存関係は DI コンテナから提供
- ビジネスロジックはインターフェースを通じてサービス層に分離
- 主要サービス：
  - `IWeaponsService`: 武器データ管理
  - `IConfigService`: 設定管理
  - `IWeaponOmodExtractor`: OMOD 候補抽出
  - `IRobCoIniGenerator`: INI ファイル生成
  - `ILoadOrderService`: ロードオーダー管理

### 3. Mutagen 環境の Factory パターン
- **`IMutagenEnvironmentFactory`**: Mutagen 環境インスタンスの生成責務
- **`IResourcedMutagenEnvironment`**: `IMutagenEnvironment` + `IDisposable`
- **重要**: 必ず `using` ブロックで使用し、リソースリークとファイルロックを防止
  ```csharp
  using (var env = factory.Create()) {
      // Mutagen 操作
  }
  ```

### 4. 単一責任原則とモジュール化
複雑な操作を小さなヘルパークラスに分割：
- **`CandidateEnumerator`**: OMOD/COBJ 候補の列挙
- **`ReverseMapBuilder`**: 逆引きマップの構築
- **`DiagnosticWriter`**: 診断ファイルとマーカーの出力
- **`LinkCacheHelper`**: LinkCache を使った参照解決（再入ガード付き）
- **`WeaponDataExtractor`**: 武器データ抽出ロジックの分離

### 5. 非同期処理と UI レスポンシブネス
- 長時間処理は `Task.Run` と `AsyncRelayCommand` で非同期実行
- UI スレッドをブロックしない
- 進捗報告は `IProgress<string>` を通じてリアルタイム更新
- 抽出処理は UI スレッド外で実行（`MapperViewModel` で `Task.Run` 使用）

### 6. 例外ハンドリングポリシー
- **空の `catch {}` ブロックは禁止**
- すべての例外は `AppLogger` でログ記録
  ```csharp
  catch (Exception ex) {
      AppLogger.Log(ex, "エラー内容");
  }
  ```
- 設定保存失敗など重要な例外は再スロー

### 7. LinkCache 再入ガード
- `AsyncLocal<HashSet<string>>` ベースの再入防止機構を実装
- 循環参照による無限ループを防止
- `LinkCacheHelper.TryResolveViaLinkCache` に実装

### 8. 診断とデバッグのサポート
- フェーズマーカーファイル出力（start/reverse/detector/detection_pass/complete）
- zero-ref 集約 CSV 出力（候補ごとではなく1回の集約）
- Noveske 診断 CSV
- UI ログは `Dispatcher` 経由で非同期転送、再入防止フラグ付き

### 9. 検出器の拡張性
- `DetectorFactory` パターンで複数の検出器をサポート
- `MutagenV51Detector`: Mutagen 0.51.x 用の検出器
- `ReflectionFallbackDetector`: リフレクションベースのフォールバック検出器

---

## 現在の実装状況

### ✅ 実装済み機能

1. **設定画面**
   - ゲームデータパス設定
   - 出力パス設定
   - マッピング戦略設定
   - 除外プラグイン編集

2. **武器データ抽出**
   - Mutagen によるプラグイン読み込み
   - ロードオーダー考慮
   - WinningOverrides による競合解決
   - 武器メタデータ抽出（名前、ダメージ、発射速度、弾薬など）
   - `IWeaponDataExtractor` インターフェースによる抽出ロジックの分離

3. **OMOD 候補抽出**
   - リフレクションベースの抽出
   - COBJ レコードのスキャン
   - 逆引きマップ構築（`ReverseMapBuilder`）
   - 弾薬変更検出（複数検出器対応）
   - 除外プラグインのフィルタリング

4. **マッピング画面**
   - 抽出された武器一覧表示
   - 武器フィルタリング（選択した武器の OMOD のみ表示）
   - OMOD マッピング管理

5. **INI 生成**
   - タイムスタンプ付き INI ファイル生成
   - 手動マッピングフラグサポート
   - ディレクトリ自動作成

6. **ログシステム**
   - `AppLogger` による集約ログ
   - UI ログは `Dispatcher` 経由で非同期転送
   - 永続化ログファイル（`munition_autopatcher_ui.log`）
   - 再入防止フラグによるログループ防止

7. **診断機能**
   - フェーズマーカーファイル出力
   - zero-ref 集約 CSV
   - Noveske 診断 CSV
   - reverse-map 構築マーカー

8. **テスト**
   - `LinkCacheHelperTests`
   - `ReverseMapBuilderTests`
   - `CandidateEnumeratorTests`
   - `MutagenAdapterTests`
   - `AssemblyInfo.cs` で `InternalsVisibleTo` を設定

### 🔄 進行中の改善

- 未コミットの変更がある（7ファイル modified）:
  - `.gitignore`
  - `CandidateEnumerator.cs`
  - `LinkCacheHelper.cs`
  - `WeaponDataExtractor.cs`
  - `Program.cs` (tests)
  - `CandidateEnumeratorTests.cs`
  - `LinkCacheHelperTests.csproj`
  - `MutagenAdapterTests.cs`

---

## 未解決タスク・今後の作業

### 優先度: 高 🔴

1. **実行検証**
   - 抽出を再実行して `extract_complete_*.txt` が生成されるか確認
   - `munition_autopatcher_ui.log` の末尾100行と `artifacts/RobCo_Patcher` のファイル一覧を検証
   - 完了マーカーが正しく出力されることを確認

2. **未コミット変更のレビューとコミット**
   - 現在の modified ファイルをレビュー
   - テストが通ることを確認
   - コミット可能な状態か確認

3. **CI/CD セットアップ**
   - `.github/workflows/ci.yml` の作成
   - `runs-on: windows-latest` を使用
   - GUI 依存テストの分離またはスキップ
   - ビルドとテストの自動実行

### 優先度: 中 🟡

4. **デバッグログ強化**
   - 完了マーカーが出ない場合の調査用ログ追加
   - `ExtractCandidatesAsync` の reverse-map 以降に滞留ポイントログ
   - 短時間のデバッグログ（開始/終了/滞留）

5. **テスト拡充**
   - `DiagnosticWriter` のユニットテスト
   - `DetectionCoordinator` の分離とテスト
   - LinkCache 再入ケースの再現テスト
   - 統合テスト（小さな fixture load order で全フロー検証）

6. **コンポーネント分離の継続**
   - `ICandidateNameResolver` の抽出
   - `IDetectionCoordinator` の抽出
   - `IArtifactWriter` の抽出
   - `ICandidatePostProcessor` の抽出

### 優先度: 低 🟢

7. **リフレクション周辺の堅牢化**
   - null チェック強化
   - 詳細エラーログ
   - API 互換性テスト
   - `MutagenReflectionHelpers` のテスト

8. **UI 改善**
   - 優先表示言語の設定（日本語/英語/EditorID）
   - `ITranslatedString` のフォールバック改善
   - `IProgress<ProgressEvent>` のような DTO への切り替え（多情報伝達）

9. **パフォーマンス最適化**
   - 大規模 CSV のストリーミング書き込み
   - リフレクション呼び出しのキャッシング
   - `allWeapons.ToList()` の最適化（必要な場合のみ）

10. **ロギング改善**
    - `ILogger<T>` (Microsoft.Extensions.Logging) への切り替え
    - テストと統合の容易化

---

## 参照すべきファイル

### コアサービス
- `MunitionAutoPatcher/Services/Implementations/WeaponOmodExtractor.cs` - OMOD 抽出のオーケストレーター
- `MunitionAutoPatcher/Services/Implementations/LinkCacheHelper.cs` - LinkCache 参照解決と再入ガード
- `MunitionAutoPatcher/Services/Implementations/WeaponDataExtractor.cs` - 武器データ抽出ロジック
- `MunitionAutoPatcher/Services/Implementations/MutagenEnvironmentFactory.cs` - Mutagen 環境の生成
- `MunitionAutoPatcher/Services/Implementations/DetectorFactory.cs` - 検出器の生成
- `MunitionAutoPatcher/Services/Implementations/WeaponsService.cs` - 武器データ管理

### ヘルパー
- `MunitionAutoPatcher/Services/Helpers/CandidateEnumerator.cs` - OMOD/COBJ 候補の列挙
- `MunitionAutoPatcher/Services/Helpers/ReverseMapBuilder.cs` - 逆引きマップ構築
- `MunitionAutoPatcher/Services/Helpers/DiagnosticWriter.cs` - 診断ファイル出力

### ViewModel
- `MunitionAutoPatcher/ViewModels/MapperViewModel.cs` - マッピング画面のロジック
- `MunitionAutoPatcher/ViewModels/SettingsViewModel.cs` - 設定画面のロジック
- `MunitionAutoPatcher/ViewModels/MainViewModel.cs` - メインウィンドウのロジック

### ユーティリティ
- `MunitionAutoPatcher/Utilities/RepoUtils.cs` - リポジトリルート探索
- `MunitionAutoPatcher/AppLogger.cs` - 集約ログ

### テスト
- `tests/LinkCacheHelperTests/` - LinkCache 関連のテスト
- `tests/AutoTests/Program.cs` - 自動テストプログラム

### 出力・ログ
- `artifacts/RobCo_Patcher/` - 抽出フェーズマーカー、CSV
- `munition_autopatcher_ui.log` - UI ログファイル

### ドキュメント
- `ARCHITECTURE.md` - アーキテクチャ設計書
- `README.md` - プロジェクト概要とセットアップ

---

## 次の推奨アクション

1. **検証実行**: 抽出を実行し、完了マーカーと UI ログを確認して現在の実装が正常動作するか検証する
2. **変更のコミット**: 未コミットの7ファイルをレビューし、テストが通ることを確認してコミットする
3. **CI 構築**: GitHub Actions ワークフローを作成し、自動ビルド・テストを有効化する

---

## 過去のチャットサマリー（履歴）

<details>
<summary>2025-10-28 セッション 1 - MCP/Serena オンボーディング</summary>

### 要約
MunitionAutoPatcher リポジトリに対する MCP/Serena の検証とオンボーディングを完了しました。プロジェクトをアクティベートし、オンボーディング情報（project_overview、suggested_commands、code_conventions）をメモリに書き込み、`README.md` に CI セクションを追加、`.github/copilot-instructions.md` を英語で整理・重複削除しました。ビルド検証も実行し問題なしです。

### 決定事項
- MCP/Serena によるオンボーディングとメモリ作成を実行
- `README.md` に CI セクションを追加
- `.github/copilot-instructions.md` を整理

### 推奨アクション
1. DECISIONS.md への要約追記
2. `.github/workflows/ci.yml` を作成して CI を有効化
3. テスト実行と CI 上での GUI テスト除外ルールを検証

</details>

<details>
<summary>2025-10-28 セッション 2 - リファクタリングと保守性向上</summary>

### 要約
`WeaponOmodExtractor` の責務分割（ヘルパ抽出）、共通ユーティリティ化（`RepoUtils.FindRepoRoot`）、および複数ファイルの空の catch ブロックを例外をログする実装へ置換しました。単体テストを追加しビルドとテストを確認、変更を `merge/backup-into-main` ブランチにコミット＆プッシュしました。

### 主な変更
- 追加: `MunitionAutoPatcher/Utilities/RepoUtils.cs` - リポジトリルート探索
- 追加: `MunitionAutoPatcher/Services/Helpers/ReverseMapBuilder.cs`
- 追加: `MunitionAutoPatcher/Services/Helpers/DiagnosticWriter.cs`
- 追加: `MunitionAutoPatcher/Services/Helpers/CandidateEnumerator.cs`
- 更新: `MunitionAutoPatcher/Services/Implementations/WeaponOmodExtractor.cs` - ヘルパ呼び出しに置換
- 更新: `MunitionAutoPatcher/Services/Implementations/WeaponsService.cs` - `WriteRecordsLog` 実装のクリーンアップ
- 追加: `tests/LinkCacheHelperTests/ReverseMapBuilderTests.cs`
- 追加: `tests/LinkCacheHelperTests/CandidateEnumeratorTests.cs`
- 追加: `MunitionAutoPatcher/Properties/AssemblyInfo.cs` - internals visibility

### 決定事項
- `RepoUtils.FindRepoRoot()` を導入し既存の局所実装を置換
- `WeaponOmodExtractor` から `ReverseMapBuilder` / `DiagnosticWriter` / `CandidateEnumerator` を抽出
- 空の `catch {}` を `catch (Exception ex)` + `AppLogger.Log(...)` に置換
- 設定保存失敗はログ後に再送出する方針を採用

### オープンアイテム
- DiagnosticWriter tests - HIGH: ユニットテスト追加
- Detector separation - MEDIUM: 検出器調整ロジックの抽出
- Complete FindRepoRoot replacement - MEDIUM: リポジトリ全体で `FindRepoRoot` を置換

</details>

<details>
<summary>2025-10-28 セッション 3 - LinkCache 再入ガードと抽出改善</summary>

### 要約（約200文字）
LinkCache 解決の再入ガードを `LinkCacheHelper.TryResolveViaLinkCache` に `AsyncLocal<HashSet<string>>` ベースで実装し、反射周りの nullable 警告を調整しました。zero-ref 出力を候補ごとから1つの集約CSVに変更し、抽出処理を UI スレッド外で実行、AppLogger を Dispatcher 経由で UI へ非同期転送する改善と抽出フェーズのファイルマーカー追加を行い、ビルドは成功。

### 決定事項
- `LinkCacheHelper.TryResolveViaLinkCache` に `AsyncLocal<HashSet<string>>` ベースの再入ガードを導入
- zero-ref 出力は候補ごとではなく1回の集約CSVへ変更
- 抽出処理を UI スレッド外で実行するよう ViewModel を修正（Task.Run を使用）
- AppLogger は Dispatcher 経由で UI へ非同期転送し、再入防止フラグを追加
- 抽出内でフェーズ／コマンドのマーカー（start/reverse/detector/detection_pass/complete）をファイル出力

### 未解決タスク
- 抽出を再実行して `extract_complete_*.txt` が生成されるか確認（優先度: 高）
- 実行後の `munition_autopatcher_ui.log` の末尾100行と `artifacts/RobCo_Patcher` のファイル一覧を提供（優先度: 高）
- もし完了マーカーが出ない場合、`ExtractCandidatesAsync` の reverse-map 以降に短時間のデバッグログを追加して再調査（優先度: 中）
- reflection 呼び出し周辺の堅牢化（null チェック、詳細ログ、ユニットテストで再入ケース再現）（優先度: 低）

</details>

<details>
<summary>将来のリファクタリング提案 - コンポーネント分離</summary>

### 提案されたインターフェース

1. **IWeaponDataExtractor**
   - 責務: 武器データの抽出（ExtractFromConstructibleObjects）
   - ライフタイム: Transient

2. **ICandidateNameResolver**
   - 責務: 候補の名前解決、weaponMap/ammoMap の構築
   - ライフタイム: Transient

3. **IDetectionCoordinator**
   - 責務: 検出器の選択と弾薬変更の確認
   - ライフタイム: Transient

4. **IArtifactWriter**
   - 責務: ファイル書き込み（CSV、マーカー）
   - ライフタイム: Singleton または Transient

5. **ICandidatePostProcessor**
   - 責務: ConfirmReason の設定、zero-ref 集計
   - ライフタイム: Transient

### 移行ロードマップ（段階的）
- ステップ 0: 準備（インターフェース定義）
- ステップ 1: WeaponDataExtractor の抜き出し（30-60分）
- ステップ 2: CandidateNameResolver（45-90分）
- ステップ 3: DetectionCoordinator（30-60分）
- ステップ 4: ArtifactWriter（30-60分）
- ステップ 5: CandidatePostProcessor（30-45分）
- ステップ 6: WeaponOmodExtractor のクリーンアップ（15-30分）
- ステップ 7: テスト作成 & CI（30-90分）

合計見積: 4-10時間（小刻みに実施）

### DI ライフタイム推奨
- IMutagenEnvironmentFactory: Transient
- IWeaponDataExtractor: Transient
- ICandidateNameResolver: Transient
- IDetectionCoordinator: Transient
- IArtifactWriter: Singleton または Transient
- ICandidatePostProcessor: Transient

理由: 各コンポーネントは基本的にステートレスで、並列実行やテストで複数インスタンスを作ることが想定されるため Transient が無難。

</details>

---

## 備考

### 表示言語に関する注意事項
本アプリケーションでは、Mutagen が提供する翻訳文字列（ITranslatedString）から表示用文字列を取得する際に、優先言語の選択が重要です。現在の実装では、Mutagen の翻訳文字列を日本語 → 英語 → TargetLanguage の順で選択するロジックを採用しています。

今後の改善として、UI に「優先表示言語」設定を追加し、ユーザーが日本語／英語／EditorID の順序を切り替えられるようにすることが推奨されます。翻訳エントリが存在しない場合は EditorID をフォールバック表示することで、文字化けや不正なレンダリングを回避できます。

---

**このドキュメントは開発の進捗に応じて更新されます。**
