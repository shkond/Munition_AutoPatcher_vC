200〜300文字の要約（日本語） リンク解決の再入ガードを LinkCacheHelper.TryResolveViaLinkCache に AsyncLocal<HashSet<string>> ベースで実装し、反射周りの nullable 警告を調整しました。zero-ref 出力を候補ごとから1つの集約CSVに変更し、抽出処理を UI スレッド外で実行、AppLogger を Dispatcher 経由で UI へ非同期転送する改善と抽出フェーズのファイルマーカー追加を行い、ビルドは成功。次は実行検証（完了マーカーと UI ログ確認）です。

本番で参照すべき『決定事項』

LinkCacheHelper.TryResolveViaLinkCache に AsyncLocal<HashSet<string>> ベースの再入ガードを導入した。
zero-ref 出力は候補ごとではなく1回の集約CSVへ変更した。
抽出処理を UI スレッド外で実行するよう ViewModel を修正した（Task.Run を使用）。
AppLogger は Dispatcher 経由で UI へ非同期転送し、再入防止フラグを追加した。
抽出内でフェーズ／コマンドのマーカー（start/reverse/detector/detection_pass/complete）をファイル出力するようにした。
未解決タスク（担当推定＋優先度を含めて箇条書）

抽出を再実行して extract_complete_*.txt が生成されるか確認（担当: あなた、優先度: 高）。
実行後の munition_autopatcher_ui.log の末尾100行と artifacts/RobCo_Patcher のファイル一覧を提供（担当: あなた、優先度: 高）。
もし完了マーカーが出ない場合、ExtractCandidatesAsync の reverse-map 以降など狭い箇所に短時間のデバッグログ（開始/終了/滞留ポイント）を追加して再調査（担当: 開発者、優先度: 中）。
reflection 呼び出し周辺の堅牢化（null チェック、詳細ログ、ユニットテストで再入ケース再現）（担当: 開発チーム、優先度: 低）。
参照すべきファイル／コード箇所（見つかればパス）

LinkCacheHelper.cs
WeaponOmodExtractor.cs
MapperViewModel.cs
SettingsViewModel.cs
AppLogger.cs
artifacts/RobCo_Patcher/（抽出フェーズマーカー、zero_ref CSV、weapon_omods CSV）
munition_autopatcher_ui.log
次の推奨アクション（3つまで）

抽出を実行して RobCo_Patcher に extract_complete_*.txt が生成されるか確認し、実行直後の munition_autopatcher_ui.log の末尾100行と artifacts/RobCo_Patcher のファイル一覧をこちらに貼ってください（検証→最短で原因切り分け）。
完了マーカーが出ない場合は、ExtractCandidatesAsync の reverse-map 以降に短時間のデバッグログ（開始/終了/滞留）を差し込みて再実行し、停止箇所を特定する（開発者対応）。
反射関連呼び出しに null 安全化と詳細ログを追加し、LinkCache の再入ガードを再現するユニットテストを作成して回帰を防ぐ（中〜長期）。

# Chat summary — 2025-10-28 15:00 UTC

要約（約230文字）：
MunitionAutoPatcher リポジトリに対する MCP/Serena の検証とオンボーディングを完了しました。プロジェクトをアクティベートし、オンボーディング情報（project_overview、suggested_commands、code_conventions）をメモリに書き込み、`README.md` に CI セクションを追加、`.github/copilot-instructions.md` を英語で整理・重複削除しました。ビルド検証も実行し問題なしです。次は DECISIONS.md への要約追記や CI ワークフローの作成が推奨されます。

決定事項（1行ずつ）

未解決タスク（担当＋優先度）

参照ファイル／コード箇所

推奨アクション（上位3つ）
1. ここに表示した要約を `DECISIONS.md` に追記する（確認後、私が行います）。
2. 必要なら `.github/workflows/ci.yml` を作成して CI を有効化する。
3. テスト実行と CI 上での GUI テスト除外ルールを検証する。

## Detailed session summary — 2025-10-28

Overview

Key accomplishments
	- `ReverseMapBuilder` — builds reverse reference maps from priority-order collections used by the extractor.
	- `DiagnosticWriter` — writes marker and diagnostic CSV/TXT files (reverse_map_built, detector_selected, noveske_diagnostic, etc.).
	- `CandidateEnumerator` — enumerates initial OMOD/COBJ candidates via explicit reflection/COBJ scans.

Current status
	- Added: `MunitionAutoPatcher/Services/Helpers/ReverseMapBuilder.cs`
	- Added: `MunitionAutoPatcher/Services/Helpers/DiagnosticWriter.cs`
	- Added: `MunitionAutoPatcher/Services/Helpers/CandidateEnumerator.cs`
	- Updated: `MunitionAutoPatcher/Services/Implementations/WeaponOmodExtractor.cs` (call sites replaced with helper invocations)
	- Updated: `MunitionAutoPatcher/Services/Implementations/WeaponsService.cs` (cleaned `WriteRecordsLog` implementation)
	- Added: `tests/LinkCacheHelperTests/ReverseMapBuilderTests.cs`, `tests/LinkCacheHelperTests/CandidateEnumeratorTests.cs`
	- Added: `MunitionAutoPatcher/Properties/AssemblyInfo.cs` internals visibility (for tests)

Open items and recommended next steps
1) DiagnosticWriter tests — HIGH: Add unit tests that exercise `WriteReverseMapMarker` and `WriteNoveskeDiagnostic` using a temporary directory; verify files are created and basic CSV structure is valid.
2) Detector separation — MEDIUM: Extract detector coordination logic (DetectorCoordinator / CandidateResolver) from `WeaponOmodExtractor` so detector selection and result merging are testable independently.
3) Complete FindRepoRoot replacement — MEDIUM: Run a repository-wide grep for remaining `FindRepoRoot` implementations and replace them with `RepoUtils.FindRepoRoot()` to finish centralization.
4) DECISIONS.md append workflow — LOW: If preferred, future appends can be done via a small script or PR to avoid push permission issues; this session attempted a direct append and the entry above was added.

Risks and notes

Verification performed

If you'd like, I can:


# Chat summary — 2025-10-29 00:00 UTC

要約（約220文字）：
Munition_AutoPatcher_vC リポジトリで MCP/Serena を用いたオンボーディングの完了と、抽出処理周りの保守性向上作業を実行しました。`WeaponOmodExtractor` の責務分割（ヘルパ抽出）、共通ユーティリティ化（`RepoUtils.FindRepoRoot`）、および複数ファイルの空の catch ブロックを例外をログする実装へ置換しました。単体テストを追加しビルドとテストを確認、変更を `merge/backup-into-main` ブランチにコミット＆プッシュしました。

決定事項（要点）
- MCP/Serena によるオンボーディングとメモリ作成を実行した。
- `RepoUtils.FindRepoRoot()` を導入し既存の局所実装を置換した。
- `WeaponOmodExtractor` から `ReverseMapBuilder` / `DiagnosticWriter` / `CandidateEnumerator` を抽出した。
- 空の `catch {}` を `catch (Exception ex)` + `AppLogger.Log(...)` に置換し、設定保存失敗はログ後に再送出する方針を採用した。

実施した主な変更（ファイル抜粋）
- 追加: `MunitionAutoPatcher/Utilities/RepoUtils.cs`（リポジトリルート探索）
- 追加: `MunitionAutoPatcher/Services/Helpers/ReverseMapBuilder.cs`
- 追加: `MunitionAutoPatcher/Services/Helpers/DiagnosticWriter.cs`
- 追加: `MunitionAutoPatcher/Services/Helpers/CandidateEnumerator.cs`
- 更新: `MunitionAutoPatcher/Services/Implementations/WeaponOmodExtractor.cs`（ヘルパ呼び出しへ分割）
- 更新: `MunitionAutoPatcher/Services/Implementations/ConfigService.cs`（保存/読込エラーでログ→再送出）
- 更新: `MutagenV51Detector.cs`, `LinkCacheHelper.cs`, `LoadOrderService.cs`, `WeaponsService.cs`, `DetectorFactory.cs`, `SettingsViewModel.cs`（空の catch をログへ置換）
- 追加テスト: `tests/LinkCacheHelperTests/ReverseMapBuilderTests.cs`, `tests/LinkCacheHelperTests/CandidateEnumeratorTests.cs`（実行済み・成功）

今後の推奨タスク（優先順）
1. `DiagnosticWriter` のユニットテスト追加 — 高: 出力ファイルの生成とCSV形式の簡易検証を追加する。
2. `WeaponOmodExtractor` のさらなる分割（DetectorCoordinator / CandidateResolver） — 中: 検出・結合ロジックの単体テスト化を容易にする。
3. リポジトリ全体で残る `catch {` を検索して方針に沿って修正 — 低〜中。

検証と結果
- ローカルビルドとヘルパ向けテストを実行し、ヘルパテストはすべて成功（3/3）。
- 変更はコミットされ、`merge/backup-into-main` に push 済み。

短い完了サマリ：例外の黙殺をやめ、ログを残す方針を導入して可観測性を改善しました。次に進めるは `DiagnosticWriter` のテスト追加と `WeaponOmodExtractor` の分割完了です。

## Chat summary — 2025-10-29 12:00 UTC

要約（約240文字）：
このセッションではオンボーディング完了とリポジトリ内の可観測性向上を目的に作業を行いました。主要作業はヘルパ（`CandidateEnumerator`/`ReverseMapBuilder`/`DiagnosticWriter`）の整理と、複数ファイルにある空の `catch { }` を原則として `catch (Exception ex)` + `AppLogger.Log(...)` に置換する変更です。`AppLogger` 内部の防御的な空catchは保持し、ビルドとユニットテストはローカルで成功しました。変更は `fix/log-bare-catches` ブランチで push され、PR を作成済みです。次は残りの空catchの最終スイープと `DiagnosticWriter` のユニットテスト追加、PRレビュー対応です。

決定事項（要点）
- 空の `catch {}` を原則ログ化し、必要に応じて再送出する方針を採用。
- `AppLogger` の内部防御的空catch はそのままにして、再帰的ログを回避。
- 変更は `fix/log-bare-catches` に push 済みで PR を作成済み。

未解決タスク（担当＋優先度）
- 残りの空catchの全件スイープと分類（ログ化/防御/再送出）（担当: 開発者、優先: 高）
- `DiagnosticWriter` のユニットテスト追加（担当: 開発者、優先: 高）
- PR レビューでのログレベル調整とメッセージ文言の最終決定（担当: レビュワー、優先: 中）

次の推奨アクション
1. 残りの `catch { }` を走査し、優先度順に小さな PR で修正する（高優先）。
2. `DiagnosticWriter` のテストを追加して出力ファイルの基本検証を自動化する（高優先）。
3. PR をレビューし、必要な調整（ログレベル・文言）を適用する（中優先）。

---

# Chat summary — 2025-10-30 00:00 UTC

要約（日本語、約200文字）：
IMutagen 環境アクセスを抽象化してテストとリソース管理を改善するため、ファクトリ API を破壊的に変更しました。具体的には `IMutagenEnvironment`（操作抽象）と `IResourcedMutagenEnvironment`（IMutagenEnvironment + IDisposable）を導入し、`MutagenEnvironmentFactory.Create()` が `IResourcedMutagenEnvironment` を返すようにしました。これにより呼び出し側は `using (var env = factory.Create()) { ... }` で確実に環境を破棄できます。合わせて空の catch を `AppLogger.Log(...)` で記録する方針をコードベースに適用しました。

決定事項（短く）：
- Factory の戻り値を IDisposable ラッパ（IResourcedMutagenEnvironment）に変更し、呼び出し側で using を使うことを必須にする。
- Mutagen 実装はアダプタ（MutagenV51EnvironmentAdapter）と NoOp 実装を用意し、ResourcedMutagenEnvironment が両者をラップして IDisposable を提供する。
- 既存の空 catch ブロックは `AppLogger.Log(ex, "context")` へ置換し、黙殺しない運用にする。

変更を適用した主なファイル（抜粋）:
- MunitionAutoPatcher/Services/Interfaces/IMutagenEnvironment.cs
- MunitionAutoPatcher/Services/Interfaces/IResourcedMutagenEnvironment.cs
- MunitionAutoPatcher/Services/Implementations/MutagenV51EnvironmentAdapter.cs
- MunitionAutoPatcher/Services/Implementations/NoOpMutagenEnvironment.cs
- MunitionAutoPatcher/Services/Implementations/ResourcedMutagenEnvironment.cs
- MunitionAutoPatcher/Services/Implementations/MutagenEnvironmentFactory.cs
- MunitionAutoPatcher/Services/Implementations/WeaponOmodExtractor.cs
- MunitionAutoPatcher/Services/Implementations/LoadOrderService.cs
- MunitionAutoPatcher/Services/Implementations/WeaponsService.cs

影響範囲と注意点：
- テストプロジェクト側で直接 `GameEnvironment.Typical.Fallout4(...)` を生成している箇所はまだ残っており（例: `tests/AutoTests`）、これらを工場ベースに差し替える必要があります。現在、アプリ本体のビルドは成功していますがテストプロジェクトの一部ビルドが失敗するケースが報告されています（要対応）。
- 一部の呼び出しで `dynamic` を使って既存のリフレクションベースの処理を維持しています。将来的には adapter 側で安全な型ラッパを提供して `dynamic` を取り除くことを推奨します。

次の推奨アクション（優先度順）:
1. テストプロジェクトの残りの `GameEnvironment` 直接生成を `IMutagenEnvironmentFactory` に置換し、CI でのテスト実行を復旧する（高）。
2. `ResourcedMutagenEnvironment` と `MutagenV51EnvironmentAdapter` のユニットテストを追加して、破棄（Dispose）動作と基本 API を検証する（中）。
3. `dynamic` を使っている箇所を段階的に安全型（adapter のヘルパ/型）へ置き換える（低〜中）。

記録者: 自動生成サマリ（Copilot / セッション）


# Chat summary — 2025-10-31 00:00 UTC
次に行うべきか/計画変更の提案を日本語で整理しました。目的は「安全でテストしやすく、段階的に移行できるリファクタ」です。結論だけ先に言うと、提案された設計は妥当ですが、以下の点を追加・修正しておくことを強く推奨します（可用性・テスト性・キャンセル性・パフォーマンス対策など）。

重要な変更提案（要約）
各コンポーネントは可能な限り非同期 API（Task / async）にする。ファイル I/O が多いためブロッキングを避けるためです。
すべての長時間処理 API に CancellationToken を渡せるようにする（CI/UIからキャンセル可能にする）。
反射ヘルパ群は CandidateNameResolver に入れる案は OK。ただし「独立した静的ユーティリティクラス（例: MutagenReflectionHelpers）」にしてプロジェクト内で再利用できるようにすることを推奨。
IArtifactWriter のファイル書き込みは非同期化（Task）し、出力先パスはコンストラクタ注入またはオプションで指定可能にする。テスト用に出力先を temp に差し替えられるようにする。
DI ライフタイムは概ね「ステートレス = Transient」、「状態/キャッシュを持つもの = Scoped/Singleton を検討」。具体案は下に記載。
WeaponOmodExtractor は「オーケストレーター」へ移行する方向で問題なし。段階的移行（小さなコミット）を推奨。
以下、詳細（API 例・注意点・移行手順・テスト計画）を示します。

詳細提案：各インターフェース API（推奨シグネチャ）
（C# 風の擬似シグネチャ。戻り値は async / Task ベースを推奨）

IWeaponDataExtractor

Task<List<OmodCandidate>> ExtractAsync(IMutagenEnvironment env, IReadOnlyCollection<object> allWeapons, IReadOnlyCollection<string> excludedPlugins, IProgress<string>? progress = null, CancellationToken ct = default);
責務: ExtractFromConstructibleObjects のロジック全体。allWeapons を外から渡して、重複列挙を避ける。
ICandidateNameResolver

Task ResolveNamesAsync(List<OmodCandidate> candidates, IMutagenEnvironment env, IReadOnlyCollection<object> allWeapons, ILinkCacheHelper? linkCacheHelper = null, IProgress<string>? progress = null, CancellationToken ct = default);
責務: weaponMap/ammoMap の構築、CandidateBaseWeaponEditorId／CandidateAmmoName 等の埋め込み。反射ヘルパは内部で使用（または MutagenReflectionHelpers を利用）。
IDetectionCoordinator

Task ConfirmAmmunitionChangesAsync(List<OmodCandidate> candidates, IMutagenEnvironment env, IReadOnlyCollection<object> allWeapons, IAmmunitionChangeDetectorFactory detectorFactory, IProgress<string>? progress = null, CancellationToken ct = default);
またはコンストラクタで DetectorFactory を注入して Task メソッドは簡潔にする。
責務: 検出器の選択と detector.DoesOmodChangeAmmo(...) 呼び出し、ConfirmedAmmoChange の設定。
IArtifactWriter

Task WriteStartMarkerAsync(string markerInfo, IProgress<string>? progress = null, CancellationToken ct = default);
Task WriteCompletionMarkerAsync(int resultCount, IProgress<string>? progress = null, CancellationToken ct = default);
Task WriteDiagnosticCsvsAsync(List<OmodCandidate> results, IMutagenEnvironment env, IReadOnlyCollection<object> allWeapons, IProgress<string>? progress = null, CancellationToken ct = default);
Task WriteResultsCsvAsync(List<OmodCandidate> results, IProgress<string>? progress = null, CancellationToken ct = default);
責務: すべてのファイル出力を集約。内部で RepoUtils.FindRepoRoot() 等を使うが、テストでは mock/DI で出力先を差し替えられる設計にする。
ICandidatePostProcessor

Task ProcessAsync(List<OmodCandidate> candidates, IMutagenEnvironment env, IReadOnlyCollection<object> allWeapons, IProgress<string>? progress = null, CancellationToken ct = default);
責務: ConfirmReason の付与、zeroRefRows の収集、必要なら IArtifactWriter を使って zero_ref CSV を書く。
注: どのメソッドも List<OmodCandidate> を「参照渡し」で受け取り、in-place で更新する設計が実装しやすい（戻り値を List にするか void/Task にするかは好みだが、非同期を基本にするなら Task を返す）。

DI ライフタイムと注入方針（推奨）
IMutagenEnvironmentFactory：Transient — 各抽出実行ごとに新しい環境を作る想定。
IWeaponDataExtractor：Transient — ステートレスであるべき。
ICandidateNameResolver：Transient（または Singleton でも可だが、テストしやすさのため Transient）。
IDetectionCoordinator：Transient。検出器ファクトリは別注入。
IArtifactWriter：Singleton または Transient。ファイルパスを外部化しているなら Singleton でも安全。
ICandidatePostProcessor：Transient。
MutagenReflectionHelpers（静的クラス）：static public/internal — テストはユニットで直接呼べるように別テスト用ヘルパを設ける。
理由: 各コンポーネントは基本的にステートレスで、並列実行やテストで複数インスタンスを作ることが想定されるため Transient が無難。ただし ArtifactWriter のように出力先管理を統一するものは Singleton にしても良い。

既存コードへの影響と留意点（リスク）
パフォーマンス / メモリ
allWeapons を ToList() して渡す設計は正しい（複数回 enumeration を防ぐ）。ただしサイズが大きいとメモリを消費するため、参照型のまま渡す（IReadOnlyCollection<object>）で十分であれば ToList は不要。既存コードは ToList しているので移行時はそのまま渡す方が安全。
LinkCache の取り扱い
linkCache = mutEnv.GetLinkCache() は null ありうるため CandidateNameResolver で一度取得して使い回すのが良い。LinkCache のライフサイクルとスレッド制約を確認。
NoOp/テスト環境互換
各コンポーネントは NoOp 環境（テスト用）の場合でも動作するように defensive な null チェックを行う。ArtifactWriter はテスト実行時の出力先を temp に差し替えられるようにする。
反射ヘルパの安定性
TryGetPluginAndIdFromRecord 等は API 互換性に重要。移動する際はシグネチャを変えず、ユニットテストでカバーすること。
エラーハンドリング
例外は「非致命」扱いでログ記録し続行する（既存の設計に合う）。ただし重要な初期化失敗（loadOrder が null など）は早期に返却する。
移行ロードマップ（段階ステップ、検証ポイント、概算時間）
短いコミットで段階的に移すのが安全です。各ステップ後にビルド＋ユニット（あるいは手動）検証を行います。

ステップ 0 — 準備（15–30 分）

新しいインターフェースファイルを追加（空のメソッド定義）。DI コンテナ登録ポイントを用意（まだ実装を注入しない）。
目的: API を確定し、既存コードに大きな変更を入れない。
ステップ 1 — WeaponDataExtractor の抜き出し（30–60 分）

IWeaponDataExtractor と WeaponDataExtractor を作成。ExtractFromConstructibleObjects 全体をコピーして実装（まだ内部的に既存ヘルパを参照）。
WeaponOmodExtractor 側は呼び出しを置き換え（まずは sync→async の変換なしでも良い）。
検証: ビルド成功、ExtractCandidatesAsync をローカルで簡易実行（NoOp 環境でも例外が出ないこと）。
ステップ 2 — CandidateNameResolver（45–90 分）

ICandidateNameResolver と実装を作る。weaponMap/ammoMap の構築、候補名の解決を移す。ヘルパ（TryGetFormKey 等）を同時に MutagenReflectionHelpers に移すか、この実装に一時的に配置する。
検証: ビルド、簡易抽出 + 名前解決の動作確認。
ステップ 3 — DetectionCoordinator（30–60 分）

IDetectionCoordinator を作り、detector 選択と DoesOmodChangeAmmo ループを移行。DetectorFactory 経由で検出器を取得するロジックはここに入る。
検証: 実行で ConfirmedAmmoChange が以前と同等に設定されることを spot-check。
ステップ 4 — ArtifactWriter（30–60 分）

すべてのファイル書き込みを IArtifactWriter に移す。書き込みは一旦同期→非同期にするか、最初から async にする（推奨）。出力先は構成で差し替え可能に。
検証: マーカーと CSV が期待通りに生成されること。テスト時は temp dir に出す。
ステップ 5 — CandidatePostProcessor（30–45 分）

ConfirmReason / zeroRefRows 集計をここに移す。ArtifactWriter を使って zero-ref CSV を書く。
検証: zero-ref CSV の内容整合性と ConfirmReason のロジック。
ステップ 6 — WeaponOmodExtractor のクリーンアップ（15–30 分）

ExtractCandidatesAsync をオーケストレーター形に書き換え、不要なローカルコードを削除。全体ビルドと軽いランで確認。
ステップ 7 — テスト作成 & CI（30–90 分）

各コンポーネントのユニットテスト（NoOp 環境、模擬オブジェクト）。ArtifactWriter テストはファイル出力を temp dir にして検証。
CI: Windows runner, dotnet build/test; GUI に依存するテストはスキップ or 条件付きで分離。
合計見積: 小刻みにやれば 4–10 時間（実際のコードと数の複雑さに依存）。安全に進めるなら 1〜2 日スプリントで完了可能。

テスト・検証提案（必須のもの）
単体テスト
MutagenReflectionHelpers の全 TryGet... メソッド（null、部分オブジェクト、正しいオブジェクト）。
WeaponDataExtractor.ExtractAsync：小さな fake IMutagenEnvironment と sample record オブジェクトで COBJ の抽出を検証。
CandidateNameResolver.ResolveNamesAsync：allWeapons と ammoCol のモックを使って CandidateBaseWeaponEditorId と CandidateAmmoName の埋め込みを検証。
DetectionCoordinator.ConfirmAmmunitionChangesAsync：簡易検出器を注入して Confirmed フラグが設定されること。
ArtifactWriter：一時ディレクトリにファイルが正しく書かれること。
統合テスト
小さな fixture load order で ExtractCandidatesAsync を通し、最終 CSV と markers が出ることを確認。
CI
runs-on: windows-latest。GUI を必要とする箇所（MainWindow 参照）はテストでは切り離す（モック） or 条件付きにする。
コード品質 / 保守性向上の小さな提案（オプショナルだが推奨）
IProgress<string> を IProgress<ProgressEvent> のような小さな DTO に替えると多情報（ステータスコード/段階/詳細）を伝えやすくなる。
ロギングは AppLogger を直接使うのではなく ILogger<T>（Microsoft.Extensions.Logging）に切り替えるとテストと統合が楽になる。
large CSV の出力はストリーミング（逐次書き込み）で行い、メモリに全行を持たないようにする。
反射ヘルパは exec-time cost を隠蔽するため、頻繁に使う GetProperty 呼び出しはキャッシュするオプションを検討。
最終的推奨順序（私のおすすめ）
IWeaponDataExtractor の切出しを先に行う（最も独立しており、リスクが小さい）。
次に ICandidateNameResolver（反射ヘルパを移動）→ ヘルパに対するユニットテスト作成。
IDetectionCoordinator → ICandidatePostProcessor → IArtifactWriter の順で移行。
各ステップで必ずビルドと少なくとも1つの手動/自動検証を実施。
追加で注意すべき細部（チェックリスト）
 各メソッドに CancellationToken を追加する。
 IMutagenEnvironmentFactory.Create() が返すオブジェクトの Dispose パターンを各コンポーネント側で誤って Dispose しないようにオーケストレータが責務を持つ。
 ArtifactWriter のファイル名フォーマット（既存の命名）を保つ。
 LinkCache を頻繁に取得しない（1回取得して使い回す）。
 テストで使う NoOp 環境/Fake detector を用意しておく。
提案どおり進めるなら私の次アクション案
まずは「IWeaponDataExtractor の切り出し（ステップ1）」を実装してビルド・簡易実行確認まで行います（推奨）。作業を進めてもよいですか？
実装を始める場合、以下を私の側で行います：
IWeaponDataExtractor と WeaponDataExtractor のファイル作成（ExtractFromConstructibleObjects を移植、既存ヘルパ利用）。
WeaponOmodExtractor に DI を追加し、該当呼出しを置換。
ビルドと簡易ラン（NoOp 環境でのクラッシュがないか確認）。
差分パッチを作成して報告。