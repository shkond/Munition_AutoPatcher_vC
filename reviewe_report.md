E2Eテスト計画 Constitution準拠性レビューレポート
レビュー日: 2025-11-30
対象ブランチ: 001-plan-e2e-tests
レビュー対象ファイル:


specs/001-plan-e2e-tests/plan.md

specs/001-plan-e2e-tests/research.md

specs/001-plan-e2e-tests/data-model.md

specs/001-plan-e2e-tests/quickstart.md

specs/001-plan-e2e-tests/contracts/e2e-harness.openapi.yaml
✅ 総合評価
計画全体は Constitution v1.1.0 の主要原則に概ね準拠しており、以下の重要ポイントをクリアしています：

Section 2 (Mutagen境界): IMutagenAccessor経由のアクセスを遵守、Detector/Strategy境界を尊重
Section 1 (MVVM & DI): ViewModel層のテスト、DI mirroringによる本番環境再現
Section 4 (Async): xUnit async testsパターンの採用（既存コードベースとの整合性）
Section 8 (Dispose): LinkCache/GameEnvironmentの明確な所有権設計
Section 12 (AI開発): Mutagen MCP参照の推奨（Constitution L201-205）
🔍 改善ポイント（Constitution準拠観点）
1. Logging & Diagnostics（Section 5）の明示不足
現状:


research.md
 L9で「IDiagnosticWriterをsandbox化」と記載

data-model.md
ではDiagnostics: List<string>（ファイルパスのリスト）のみ

quickstart.md
 L19で「validation logs + diffs appear under tests/IntegrationTests/TestResults/<timestamp>」
Constitution要件:

Section 5.1: ILogger<T> (サービス層) + IAppLogger (UI層/ユーザ通知) の2チャネル体制
Section 5.2: ログパス ./artifacts/logs/munition_autopatcher_ui.log (fallback: %TEMP%/MunitionAutoPatcher/logs/...)
Section 5.3: AppLoggerProviderのFlush/Dispose徹底
改善案:

1.1 

research.md
への追記
L9-11のDI surface説明箇所に以下を追加：

**Logging in tests**:  
- テスト用`TestServiceProvider`は`NullLoggerFactory`を既定とするが、CI実行時やデバッグ時には`ILoggerFactory`をxUnit `ITestOutputHelper`へブリッジする`XunitLoggerProvider`に差し替え可能とする。
- `IDiagnosticWriter`はテストシナリオごとの成果物フォルダ配下（`scenario-{Id}/diagnostics/`）に出力し、本番LoggerProviderとは独立させる。
- Constitution Section 5の`ILogger<T>` / `IAppLogger`分離を尊重：テストハーネス内では`ILogger<ViewModelE2ETests>`等でログ記録し、`IAppLogger`はモック化してUI通知イベントのアサーション対象とする。
1.2 

data-model.md
 L65-74 ScenarioRunArtifactの拡張
Diagnosticsフィールドの説明を以下に変更：

| `Diagnostics` | `DiagnosticBundle` | Logger出力とDiagnosticWriterファイルを集約。詳細は下記。 |
新規セクション追加（L96以降）：

### `DiagnosticBundle`
| Field | Type | Description |
| --- | --- | --- |
| `LogFilePaths` | `List<string>` | xUnit ITestOutputHelper経由のログまたはファイルベースログへのパス |
| `DiagnosticWriterOutputs` | `List<string>` | `IDiagnosticWriter`が生成したCSV/JSONパス |
| `ValidationReports` | `List<string>` | `EspFileValidator`のdiffレポートパス |
| `CIArtifactRoot` | string? | CI環境でのアップロード先ルート（GitHub Actions artifacts等） |
1.3 

quickstart.md
 L19-19の改善
既存の「Inspect artifacts」セクションを以下に置き換え：

5. **Inspect artifacts**: 
   - **Generated ESPs**: `%TEMP%/MunitionAutoPatcher_E2E_Tests/<run>/Output`
   - **Validation logs**: `tests/IntegrationTests/TestResults/<timestamp>/diagnostics/`（`IDiagnosticWriter`出力）
   - **xUnit logs**: xUnit ITestOutputHelperがCIコンソールに出力、CLI実行時は標準出力に表示
   - **CI artifacts**: GitHub Actions実行時は`e2e-test-results`としてzipアップロード
2. Async & CancellationToken（Section 4）のカバレッジ不足
現状:


research.md
 L20でCancellationTokenの必要性に触れているが、

data-model.md
や

e2e-harness.openapi.yaml
に反映されていない
E2EScenarioDefinitionやRunOptionsにキャンセル機構が未定義
Constitution要件:

Section 4.3: 「CancellationTokenを受け取れる設計を必須とする」
Section 1 (MVVM): 「長時間処理は非同期化しUIをブロックしない」
改善案:

2.1 

data-model.md
 L20追記
E2EScenarioDefinitionテーブルに以下フィールドを追加：

| `TimeoutSeconds` | int? | Optional; シナリオ全体のタイムアウト（CancellationTokenSourceの設定に使用）。未指定時は既定300秒。 |
2.2 

e2e-harness.openapi.yaml
 L207-214 RunOptions拡張
RunOptions:
      type: object
      properties:
        publishArtifacts:
          type: boolean
          default: true
        overrideOutputPath:
          type: string
        timeoutSeconds:
          type: integer
          description: Optional scenario execution timeout; triggers CancellationToken if exceeded.
2.3 

plan.md
 L83-85への実装ノート追加
Infrastructureセクションに以下を追記：

│   ├── Infrastructure/            # (New) EspFileValidator, TestServiceProvider, TestDataFactory extensions
│   │   ├── AsyncTestHarness.cs   # CancellationToken/Timeout管理ヘルパー
Rationale: ViewModelのAsyncRelayCommandはCancellationTokenを受け取る設計であり、E2Eハーネスでも同様のパターンを採用することでConstitution Section 4と整合する。

3. AI-Assisted Development（Section 12）の具体的適用手順が不明
現状:


plan.md
 L40-46でConstitution Checkは「PASS」とマークされ、「Mutagen MCP references before coding low-level interactions」と記載
しかしStage 1-4のワークフローやProposedAPIs出力フォーマットへの具体的言及がない
Constitution要件:

Section 12.2 (運用原則): Stage 1（API選定レビュー）→ Stage 2（設計合意）→ Stage 3（最小スパイク）→ Stage 4（実装）
Section 12.3 (固定ガードレール): Reflection/dynamic禁止、WinningOverrides/LinkCache/DisposePlan明示
Section 12.4 (Stage 1出力フォーマット): ProposedAPIs/Rationale/ErrorPolicy/Performance/DisposePlan/References
改善案:

3.1 

plan.md
 L106-111に新セクション追加
## AI-Assisted Development Workflow (Section 12 Compliance)
本機能の実装では以下のAI支援ステージを遵守する：
### Stage 1 — API選定レビュー（コード生成禁止）
**Target**: `EspFileValidator`, `TestServiceProvider`, Mutagen export helpers  
**Required Output**:
- **ProposedAPIs**: `Fallout4Mod.CreateFromBinaryOverlay`, `MutagenBinaryReadStream.ReadModHeaderFrame`, `BeginWrite...WriteAsync`等の具体的型/メソッド/namespace
- **Rationale**: なぜその APIを選択したか（代替案との比較）
- **ErrorPolicy**: 各API失敗時の扱い（Warning蓄積 vs Fatal終了）
- **Performance**: 1-pass overlay読み込み、WinningOverrides利用
- **DisposePlan**: `GameEnvironment`/`LinkCache`の所有者（TestEnvironmentBuilder? TestServiceProvider?）と破棄タイミング
- **References**: Mutagen公式ドキュメント **または** GitHub MCP server query (`mcp_mutagen-rag_search_repository`) による`Mutagen-Modding/Mutagen`リポジトリからのソース参照
### Stage 2 — 設計合意
- DTOシグネチャ（`ESPValidationProfile`, `ValidationResult`）の最終確定
- 例外分類（Mutagen parse failure = Warning vs 致命エラー）
- CancellationToken受け渡しポイント
- テスト観点（既存`WeaponDataExtractorIntegrationTests`との統合、新規xUnit fixture設計）
### Stage 3 — 最小スパイク
- `EspFileValidator.NormalizeHeader`の疑似コード（型付き）
- `TestServiceProvider.Build`のDI登録シーケンス（20行以内）
### Stage 4 — 実装
- 完全なコード生成、Constitution Section 2/4/5/8の全ガードレールを満たしたもの
### Rejection Criteria
- Reflection/dynamic使用 → 即却下
- namespace/公式ドキュメント未記載 → 根拠追記要求
- LinkCache DisposePlan未定義 → Accessor層管理方針追記要求
3.2 

research.md
 L5-6の参照URLを具体化
既存：

Mutagen's official exporting guidance recommends the builder pipeline so we can feed load-order metadata...
([Mutagen "Exporting"](https://mutagen-modding.github.io/Mutagen/plugins/Exporting/#builder))
推奨追記:

**AI verification required**: Before implementing `WriteToBinary` or `BeginWrite` calls, AI must use GitHub MCP 
(`mcp_mutagen-rag_search_repository`) to query the `Mutagen-Modding/Mutagen` repository for the exact signature 
of `Fallout4Mod.BeginWrite(...)` and confirm the required load-order parameters (Constitution Section 12, L201-205).
4. Performance Goals & DisposePlan（Section 8）の明確化不足
現状:


plan.md
 L26で「ESP validation provides diagnostics under 60 seconds post-run」と性能目標を記載
しかしLinkCache/GameEnvironmentのDispose戦略が

research.md
に抽象的にしか記載されていない（L19-20）
Constitution要件:

Section 8.1: 所有権明確化、using/await using徹底
Section 8.2: Dispose は UI スレッドで重い処理を行わない
Section 8.3: Accessor層はDisposePlanを明示
改善案:

4.1 

data-model.md
 L31-37 PluginSeedに所有権フィールド追加
| `OwnsEnvironment` | bool | この`PluginSeed`が専用`GameEnvironment`を作成する場合true（テスト終了時にDispose）。falseの場合は共有環境を参照。 |
4.2 

research.md
 L19-21を詳細化
既存：

Keep Mutagen access behind `IMutagenAccessor` / `IMutagenEnvironmentFactory` even in tests, 
and acquire link caches via `IResourcedMutagenEnvironment` provided by `TestEnvironmentBuilder`.
推奨置き換え:

**Dependency & integration best practices**
- **Decision**: Keep Mutagen access behind `IMutagenAccessor` / `IMutagenEnvironmentFactory` even in tests. 
  For E2E scenarios, `TestEnvironmentBuilder` creates a dedicated `GameEnvironment` per scenario run and owns it; 
  the environment is wrapped in `await using` at the xUnit test method level to ensure deterministic disposal 
  before artifact validation begins (Constitution Section 8.1).
- **LinkCache lifetime**: Each scenario receives a fresh `LinkCache` from the builder's environment; the cache 
  is disposed alongside the environment. `EspFileValidator` opens ESPs via **overlay** (read-only, no GameEnvironment 
  required) to avoid re-acquiring heavy resources post-generation (Constitution Section 2.3 — prefer WinningOverrides/overlays).
- **Performance**: Disposal of `GameEnvironment` must complete within 5 seconds to meet the 60-second validation 
  target; if disposal is heavyweight (large plugin count), consider moving it to a background task while validation 
  starts (Constitution Section 8.2 — avoid UI thread blocking, adapted here to test-thread blocking).
5. Error Handling Policy（Section 9）の詳細化不足
現状:


plan.md
 L33で「research.md参照」とあるが、

research.md
にはErrorPolicyの具体的分類が記載されていない

data-model.md
 L82-91のValidationResultにはErrors/Warnings区別があるが、どのエラーが致命（Fatal）かが未定義
Constitution要件:

Section 9: 致命（Fatal: 処理停止・ユーザ通知・ログ記録）vs 非致命（Warning: 蓄積・継続）
Section 9.2: 例外は握り潰さず境界で捕捉→分類→ログ化
改善案:

5.1 

data-model.md
 L42-46 ESPValidationProfileに新フィールド追加
| `FatalErrorPatterns` | `List<string>` | エラーメッセージ部分一致で致命判定するパターン（例: "FormKey resolution failed for master record"）。該当時はテスト失敗。 |
5.2 

research.md
の冒頭に新セクション追加
## Error classification for E2E validation
- **Decision**: Align with Constitution Section 9 by categorizing validation failures into Fatal vs Warning:
  - **Fatal**: 
    - ESP file missing after ViewModel execution
    - Mutagen overlay parse failure (corrupted ESP structure)
    - Structural count outside expected Range (indicates mapping logic regression)
  - **Warning**: 
    - Header timestamp mismatch (expected; normalized before diffing)
    - Small file size warning (< 1KB; allowed for minimal test scenarios)
    - Non-critical form link warnings from DiagnosticWriter
- **Rationale**: Fatal errors block CI (xUnit assertion fails); Warnings accumulate in `ValidationResult.Warnings` 
  and are uploaded as artifacts but do not fail the test unless exceeding a scenario-specific threshold.
- **Alternatives considered**: (1) Fail on any warning — rejected to avoid flaky CI from benign header differences. 
  (2) Ignore all warnings — rejected because it would mask real issues like missing DLC exclusions.
📋 Constitution Checkの更新推奨

plan.md
 L45-47のPost-Phase 1 Reviewを以下に置き換え：

**Post-Phase 1 Review**: PASS (with minor clarifications) — Newly defined ESP validator + DI scaffolding remain 
outside production orchestrators, continue to honor IMutagenAccessor boundaries, and isolate filesystem output via 
temp path services as required by sections 2, 5, and 8 of the constitution. **Recommended enhancements**:
- Explicit Logging strategy (Section 5 compliance) added to `research.md` and `data-model.md` (see review report).
- CancellationToken integration (Section 4) extended to `RunOptions` and scenario timeout handling.
- AI-Assisted Development workflow (Section 12 Stage 1-4) codified in plan with ProposedAPIs template.
- DisposePlan details (Section 8) formalized in `research.md` with LinkCache/GameEnvironment ownership clarification.
- Error classification (Section 9 Fatal vs Warning) documented in `research.md` with concrete examples.
🎯 優先度付けされた改善アクション
高優先度（Phase 0完了前に対処推奨）
Logging戦略の明示（改善案 1.1-1.3）
→ CIでのデバッグ性向上、Constitution Section 5完全準拠
ErrorPolicy詳細化（改善案 5.1-5.2）
→ CI安定性向上、Flaky test回避
中優先度（Phase 1実装前に対処推奨）
AI-Assisted Development手順の文書化（改善案 3.1-3.2）
→ 実装中のMutagen API誤用リスク低減
DisposePlan詳細化（改善案 4.1-4.2）
→ メモリリーク/リソース競合回避
低優先度（Phase 2テスト実装時に対処可）
CancellationToken統合（改善案 2.1-2.3）
→ 現状のシナリオ（<5分）では影響小、将来の長時間テスト対応
✨ 総評と推奨Next Steps
Strong Points:

Mutagen境界遵守、MVVM/DI原則、既存テストパターン再利用など、Constitutionの核心ルールに忠実
Phase 0リサーチが既存コードベース（EspPatchService, WeaponDataExtractorIntegrationTests）を正しく参照
OpenAPI契約とデータモデルの一貫性が高く、将来のCI自動化を見据えた設計
Recommended Next Steps (Priority Order):

改善案1 (Logging) + 5 (ErrorPolicy) を

research.md
/

data-model.md
に反映
→ Constitution Section 5/9完全準拠、CI品質向上
改善案3 (AI手順) を

plan.md
に追加
→ Phase 1実装開始前のガードレール確立
改善案4 (DisposePlan) を

research.md
に追加
→ パフォーマンス目標（60秒検証）達成のための明確化
Constitution Checkを更新 (

plan.md
 L45-47)
→ レビュー結果の正式記録、Stage 2移行準備完了マーカー
Phase 1実装着手（EspFileValidator, TestServiceProvider, ViewModelE2ETests）
Overall Confidence: 🟢 High — 計画は堅実で拡張性があり、指摘された改善点は主に「明示性の向上」であり設計変更不要。