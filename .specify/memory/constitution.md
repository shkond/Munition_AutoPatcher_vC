<!--
Sync Impact Report
- Version change: 1.0.0 -> 1.1.0
- Modified principles: AI-Assisted Development (Section 12) — explicit sourcing requirement
- Added sections: AI citation rule for Mutagen using GitHub MCP server
- Removed sections: none
- Templates requiring updates:
	- ✅ `.specify/templates/plan-template.md` (Constitution Check: now references MCP server)
	- ✅ `.specify/templates/agent-file-template.md` (agent guidance updated)
	- ⚠ `.specify/templates/spec-template.md` (requires a small References note; updated)
	- ⚠ `.specify/templates/tasks-template.md` (no required changes but review recommended)
- Follow-up TODOs:
	- TODO(RATIFICATION_DATE): Ratification date not present in repo; please supply YYYY-MM-DD.
	- Manual review: verify `.specify/templates/commands/*.md` for agent-specific tokens that
		should be made generic (e.g., agent names) and ensure MCP-server usage is referenced where
		Mutagen research is expected.
-->

# Munition AutoPatcher vC — Constitution

**Metadata**

- **CONSTITUTION_VERSION**: 1.1.0
- **RATIFICATION_DATE**: TODO(RATIFICATION_DATE)
- **LAST_AMENDED_DATE**: 2025-11-30


目的
- 本ドキュメントは Munition AutoPatcher vC の設計原則、Mutagen との境界、ログ/診断、WPF 制約、AI 支援運用、変更管理を定義する「憲章」です。
- ここに定める原則は開発・レビュー・テスト・運用の基準となります。

前提（プロジェクト固有の方針）
- Mutagen のバージョン pin は行わない（Detector パターンで機能検知し吸収する）。
- コードカバレッジ目標は設定しない。
- config/config.json は利用しない（設定は DI / ConfigService 等で管理）。
- DocFx による自動ドキュメント生成は採用しない。
- DECISIONS.md を運用してアーキテクチャ判断を記録する。

---

## 1. Core Architecture Principles

### 1.1 MVVM
- WPF MVVM を厳格に遵守する。
- Views (XAML): ビジネスロジック禁止。データバインディング／UI 表現のみ。
- ViewModels: プレゼンテーションロジック、コマンド、検証、進捗、キャンセル。
- Models/Services: ビジネスロジック／データ操作（UI 依存禁止）。
- ViewModel の生成は DI 経由（new 禁止）。全てのユーザ操作は ICommand（RelayCommand/AsyncRelayCommand）で実装。
- 長時間処理は非同期化し UI をブロックしない（.Result/.Wait 禁止）。
- Domain モデルは可能な限り不変（immutable）を推奨。

### 1.2 DI（依存性注入）
- Microsoft.Extensions.DependencyInjection を使用し App 起動時に登録する。
- コンストラクタインジェクションのみ許可（Service Locator 禁止）。
- ライフタイム:
	- Transient: 副作用のない処理や軽量ヘルパ
	- Singleton: 設定/パス/ログ/Accessor 等の共有リソース
- Scoped は WPF では使用しない。

---

## 2. Mutagen 統合方針（境界と Detector）

### 2.1 境界ルール
- Mutagen の直接呼び出しは IMutagenAccessor（または同等の境界レイヤ）経由のみ。View/ViewModel/他 Service からの直接参照禁止。
- Accessor は「安定 API」を公開し、Mutagen のバージョン差や API 差は内部で吸収する。

### 2.2 Detector パターン（機能検知）
- バージョン固定を行わず、起動時に「機能の有無（Caps）」を検知して最適 Strategy を選択する。
- 構成要素:
	- MutagenCapabilityDetector: リフレクションや小さなプローブで機能を検出（メソッド/型の存在、振る舞いの簡易確認）。
	- IMutagenApiStrategy: Accessor が上位に提供する安定インターフェイス。
	- Strategy 実装群: 高速パス（WinningOverrides 等）／フォールバックパス。
	- StrategyFactory: 検知結果から Strategy を生成し、結果を Singleton キャッシュ。
- 検知は起動時に一度実行し結果をキャッシュする。

### 2.3 リソース管理とパフォーマンス
- GameEnvironment / LinkCache 等は高コストな資源：所有権を明確にし Dispose を徹底する。必要なら参照カウントやプールで再利用。
- 競合解決は WinningOverrides を優先し、不要な全件 Materialize（ToList など）を避ける。ストリーミング/1-pass を目指す。
- FormKey / FormLink の解決は必ず Null / 無効チェックを行い、失敗は Diagnostics に蓄積する。

---

## 3. Orchestrator / Strategy / 拡張性

- Orchestrator（例: WeaponOmodExtractor）は「フローの調停」のみを行い、ビジネス判断や詳細は Provider/Confirmer 等の戦略インターフェイスへ委譲する。
- 新しい抽出戦略は ICandidateProvider を追加するだけで済むようにする（オープン/クローズド原則）。
- 生成物やレポートは DiagnosticWriter / AppLogger 経由で一元化する。

---

## 4. Async / Threading / キャンセル

- 非同期優先。UI 層のコマンドは AsyncRelayCommand を使用。
- UI 更新は Dispatcher 経由。ライブラリ層は ConfigureAwait(false) の使用を推奨。
- CancellationToken を受け取れる設計を必須とする。
- 同期ブロック (.Result/.Wait/Thread.Sleep) は禁止。

---

## 5. Diagnostics & Logging（必須詳細）

方針
- 既定ログパス: `./artifacts/logs/munition_autopatcher_ui.log`（アプリ起動時に初期化）
- Console.WriteLine / Debug.WriteLine は禁止。
- ログチャネル:
	- サービス層: Microsoft.Extensions.Logging の ILogger<T>
	- UI 層・ユーザ通知: IAppLogger（アプリ内イベント型のロガー）
- AppLoggerProvider (ILoggerProvider 実装) がファイル出力を担当し、Warning 以上を IAppLogger に転送して UI 備える。

初期化とフォールバック
- アプリ起動直後（DI 構築前）に `./artifacts/logs` を作成し、ログファイルの書込可否を検証する。
- 書き込み不可時は `%TEMP%/MunitionAutoPatcher/logs/munition_autopatcher_ui.log` にフォールバックし、起動時に一行フォールバック通知を残す。
- DI 構築前に発生した致命的例外は AppDomain.UnhandledException / DispatcherUnhandledException ハンドラで最小フォールバック書き込みを行う。

運用上の厳守事項
- AppLogger は ILogger を内部で直接利用しないこと（もしくは AppLogger 自身のカテゴリを Provider 側でフィルタして循環を防止する）。
- アプリ終了時に AppLoggerProvider を Flush / Dispose して最後のログが欠落しないようにする。
- 既定の最低ログレベルは `Information`。環境変数 `MUNITION_LOG_LEVEL` で上書き可能。
- 重大障害は AppLogger 経由で UI 通知（ダイアログ／トースト）＋ファイル記録する。軽微なものはファイル記録のみ。

ログフォーマット・ローテーション
- 可能であれば日次ローテーション（`munition_autopatcher_ui.yyyyMMdd.log`）を実装し、最大保持日数を設定する（実装は任意）。

---

## 6. WPF 制約（最小アクセシビリティ要件）

- フォーカス可視化（FocusVisualStyle）をグローバルリソースとして定義し、ウィンドウロード時に初期フォーカスを明示する（XAML の FocusManager.FocusedElement または Window.Loaded で Keyboard.Focus）。
- エラーメッセージはスクリーンリーダーに通知する（AutomationPeer.RaiseNotificationEvent 等を利用して LiveRegion 相当の反映を行う）。

---

## 7. ObservableCollection / UI コレクションガイドライン

- UI にバインドするコレクションは ObservableCollection 系を使用して変更通知を行う。
- コレクション変更は UI スレッドで実施。重い取得はバックグラウンドで行い、Dispatcher で一括反映する。
- 大量更新は AddRange/Reset を使ってまとめて反映し、1件ずつ Add を繰り返さない。
- 並び替え・絞り込みは ICollectionView を用いる。UI 仮想化を有効にする（VirtualizingPanel.IsVirtualizing=True 等）。

---

## 8. IDisposable / リソース寿命管理（Mutagen 向け）

- 外部リソース（GameEnvironment, LinkCache 等）は明確な所有権の下で生成・破棄する。
- 短命利用: using / await using を用いて即時 Dispose。
- 高コスト資源を再利用する場合: 参照カウントやプールで管理し、最後に確実に Dispose する。
- Dispose は UI スレッドで重い処理を行わない（背景スレッドで実施する等配慮）。
- Accessor 層は DisposePlan を明示し、上位には生存期間を気にせず使える安全 API を提供する。

---

## 9. Error Handling Policy

- 致命 (Fatal): 処理停止・ユーザ通知・ログ記録。
- 非致命 (Warning): 処理は継続し、Diagnostics に蓄積して最終報告。
- 例外は握り潰さず境界で捕捉→分類→ログ化し、必要であれば再スローする。
- ログに個人環境の絶対パスを直接出さない（マスク/短縮/ハッシュ化）。

---

## 10. Testability & Quality

- 新規サービスは単体テストを作成することを推奨（数値的カバレッジ目標は定めない）。
- Mutagen 連携は統合テストで検証（テスト用プラグインセット等の固定データ）。
- 重要な出力（Weapon→Ammo マッピングなど）は Snapshot/Golden File による回帰検査を行う。

---

## 11. Dependency 管理

- Mutagen バージョン pin は行わない。API 互換差は Detector/Accessor レイヤで吸収する。
- 他の .NET 依存は互換性に留意して管理する。

---

## 12. AI-Assisted 開発：コンテキスト提供手順（必須プロセス）

目的
- AI に「現在の制約と境界」を誤解なく伝え、リフレクション多用や未確認 API 直接呼び出しを防ぎ、安全で型安全な設計を得る。

運用原則（ステージ化）
- Stage 1 — API 選定レビュー（ProposedAPIs の列挙のみ。コード生成禁止）
- Stage 2 — 設計合意（入出力・エラー方針・Dispose/Performance の合意）
- Stage 3 — 最小スパイク（型付きサンプルや pseudo-code、短い snippet）
- Stage 4 — 実装（合意済み設計に基づくコード）

固定ガードレール（必ず AI に明示）
- Reflection / dynamic の使用禁止。
- Mutagen 呼び出しは IMutagenAccessor（または指定した Accessor API）経由のみ。
- 非同期、WinningOverrides、LinkCache 利用、DisposePlan の明示を必須化。
- View/ViewModel 層への直接的な Mutagen 呼び出しや UI 変更禁止。

Stage 1 の出力フォーマット（AI 回答に必須）
- ProposedAPIs: 型名 / メソッド名 / シグネチャ / 所在 namespace（箇条書き）
- Rationale: 採用理由と代替案
- ErrorPolicy: 各 API 呼び出しでの失敗時の扱い
- Performance: 1-pass 方針や LinkCache の生存範囲等
- DisposePlan: GameEnvironment / LinkCache の寿命管理案
- References: 公式ドキュメント URL または
	- When research requires inspecting Mutagen code or generated artifacts, AI MUST use the
		GitHub MCP server query the `Mutagen-Modding/Mutagen` repositories (examples: fetch
		generated C# files, XML schemas, or record definitions). Use the MCP server tools for
		implementation details and the repository contents before generating code that depends on
		Mutagen internals.

Stage 2 の内容（設計合意テンプレ）
- 入力/出力の具体シグネチャ（DTO/モデル定義を含む）
- 例外分類（致命/非致命）とログレベル
- 非同期/キャンセル点（どの API が CancellationToken を受け取るか）
- テスト観点（モック可能なポイント、必要な統合テストの概要）

AI への最小セッション開始メッセージ（毎回先頭で渡す）
- 1行 Goal（目的）
- Constraints（上記固定ガードレール列挙）
- Task ステージ（Stage 1 〜 4 のいずれかを明記）
- Output sections（ProposedAPIs 等の必須セクション）

出力レビューチェックリスト（受け取ったら必ず確認）
- 型・メソッドは具体名が示され、所在 namespace があるか
- 参照リンクが提示されているか（公式ドキュメント等）
- Reflection/dynamic を使っていないか
- WinningOverrides / LinkCache / DisposePlan が明記されているか
- ErrorPolicy とログレベルが合意に沿うか

差し戻しテンプレ（不適切な出力時）
- 「Reflection/dynamic が含まれているため却下。型付き API で再提示してください」
- 「メソッドの公式根拠（namespace/ドキュメント）が無いため根拠を追記して再提示してください」
- 「LinkCache / DisposePlan が未定義なので Accessor 内での管理方針を追記してください」

DECISIONS.md への落とし込み
- Stage 2 合意後は DECISIONS.md に記録する（Title / Context / Decision / Alternatives / Consequences / Date / Author / Related-PR）。

---

## 13. Change Management

- 憲章の変更は Pull Request にて行い、理由・影響・移行方針を明記する。
- 重要なアーキテクチャ判断は DECISIONS.md に記録する。
- BREAKING な変更は明確にタグ付けしてリリースノートに記載する。

---

## 14. Prohibited Practices（抜粋）

- IMutagenAccessor 外での Mutagen 直接呼び出し
- Reflection / dynamic による未検証 API 利用
- Console.WriteLine / Debug.WriteLine によるログ出力
- 同期ブロック (.Result/.Wait/Thread.Sleep)
- PathService を経由しないアドホックなファイル I/O
- 静的な可変グローバル状態の導入
- 例外の無記録 swallow（catch {}）

---

## 15. Appendix（運用的テンプレ等）

- ログ既定パス: `./artifacts/logs/munition_autopatcher_ui.log`（起動時初期化、不可時は `%TEMP%/MunitionAutoPatcher/logs/...` にフォールバック）
- 環境変数でログレベル上書き: `MUNITION_LOG_LEVEL`
- DECISIONS.md エントリ形式（テンプレ）:
	- Title:
	- Context:
	- Decision:
	- Alternatives:
	- Consequences:
	- Date:
	- Author:
	- Related-PR:

---
