# Implementation Plan: E2E Test Implementation Plan

**Branch**: `001-plan-e2e-tests` | **Date**: 2025-11-30 | **Spec**: [specs/001-plan-e2e-tests/spec.md](specs/001-plan-e2e-tests/spec.md)
**Input**: Feature specification from `/specs/001-plan-e2e-tests/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Introduce a ViewModel-driven end-to-end harness that exercises Munition AutoPatcher through the existing TestEnvironmentBuilder/TestDataFactory infrastructure, skips the WPF shell, emits real ESP artifacts, and validates them with deterministic rules so CI can gate releases. The plan adds (1) reusable ESP validation utilities, (2) a dedicated test service provider that wires production services safely, (3) richer scenario generation helpers, (4) ViewModel-based integration tests that assert artifact correctness, and (5) Windows CI coverage that surfaces regressions via uploaded logs/artifacts.

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: C# 12 running on .NET 8.0 (WPF desktop / test projects)  
**Primary Dependencies**: Mutagen.Bethesda.Fallout4 (0.51.5), Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Logging, AppLogger, xUnit  
**Storage**: File-system based artifacts (ESP files under tests/IntegrationTests/*, logs under artifacts/)  
**Testing**: xUnit integration tests executed via `dotnet test` plus GitHub Actions (windows-latest)  
**Target Platform**: Windows desktop agents (dev + CI); no cross-platform requirement
**Project Type**: Single WPF solution with supporting integration-test projects  
**Performance Goals**: Initial scenario suite completes within 10 minutes total (<5 minutes per scenario) and ESP validation provides diagnostics under 60 seconds post-run  
**Constraints**: Must exercise ViewModel layer only (GUI skipped), respect IMutagenAccessor boundaries, deterministic artifact comparison despite timestamped headers  
**Scale/Scope**: Start with 3 high-value scenarios covering diverse ammo/weapon mappings; future scenarios added via declarative TestDataFactory extensions

**Clarifications from Phase 0 Research** (see [specs/001-plan-e2e-tests/research.md](specs/001-plan-e2e-tests/research.md))

1. ESPs seeded by `TestEnvironmentBuilder` will be materialized via Mutagen's write builder (preferred) or `WriteToBinary` with explicit load-order metadata so we never bypass IMutagenAccessor.  
2. `TestServiceProvider` mirrors production DI but swaps in-memory `IConfigService`, temp-root `IPathService`, and sandboxed `IDiagnosticWriter`, keeping IMutagenAccessor + extractor stack untouched.  
3. `EspFileValidator` normalizes volatile header fields using Mutagen header structs before byte-diffing baselines, while structural counts rely on overlays for speed.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- 本プロジェクトの憲章: `.specify/memory/constitution.md`（Munition AutoPatcher vC — Constitution）に準拠すること。
- Mutagen 関連の設計/実装タスクでは、IMutagenAccessor/Detector/Strategy 境界を尊重すること。
- AI 支援を使う場合は「12. AI-Assisted 開発」セクションの Stage/Guardrail を満たすこと。
- Mutagen の API/レコード定義を調査する場合、AI は GitHub MCP サーバ `mcp_mutagen-rag_search_repository` を使用して `Mutagen-Modding/Mutagen` リポジトリからソース/スキーマを参照した上で提案していることを確認すること。

**Status**: PASS — Current scope (test harness + validation helpers) keeps Mutagen calls behind existing services, adheres to MVVM + DI rules, and plans to rely on Mutagen MCP references before coding low-level interactions. Additional guardrails will be revalidated after Phase 1 once service boundaries and disposal plans are captured.

**Post-Phase 1 Review**: PASS — Newly defined ESP validator + DI scaffolding remain outside production orchestrators, continue to honor IMutagenAccessor boundaries, and isolate filesystem output via temp path services as required by sections 2, 5, and 8 of the constitution.

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)
<!--
  ACTION REQUIRED: Replace the placeholder tree below with the concrete layout
  for this feature. Delete unused options and expand the chosen structure with
  real paths (e.g., apps/admin, packages/something). The delivered plan must
  not include Option labels.
-->

```text
MunitionAutoPatcher/                # WPF application (production services, ViewModels, DI registrations)
├── Models/
├── Services/
│   ├── Interfaces/
│   └── Implementations/
├── Utilities/
└── ViewModels/

tests/
├── IntegrationTests/
│   ├── IntegrationTests.csproj
│   ├── Infrastructure/            # (New) EspFileValidator, TestServiceProvider, TestDataFactory extensions
│   │   ├── AsyncTestHarness.cs   # CancellationToken/Timeout管理ヘルパー
│   ├── Scenarios/ (planned)       # Golden data + scenario manifests
│   └── ViewModelE2ETests.cs       # New E2E test suite
├── AutoTests/
└── LinkCacheHelperTests/

.github/
└── workflows/
  └── e2e-tests.yml              # Windows CI job for ViewModel E2E suite
```

**Structure Decision**: Extend existing `tests/IntegrationTests` project with new Infrastructure helpers, scenario data, and E2E test fixtures, while keeping production code changes confined to `MunitionAutoPatcher` only where DI exposure is required. CI automation lives under `.github/workflows` to mirror existing build/test jobs.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |

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


## Phase Outputs
- Research: [specs/001-plan-e2e-tests/research.md](specs/001-plan-e2e-tests/research.md)
- Data model: [specs/001-plan-e2e-tests/data-model.md](specs/001-plan-e2e-tests/data-model.md)
- API contract: [specs/001-plan-e2e-tests/contracts/e2e-harness.openapi.yaml](specs/001-plan-e2e-tests/contracts/e2e-harness.openapi.yaml)
- Quickstart: [specs/001-plan-e2e-tests/quickstart.md](specs/001-plan-e2e-tests/quickstart.md)
- Agent context updated via `.specify/scripts/powershell/update-agent-context.ps1 -AgentType copilot`
