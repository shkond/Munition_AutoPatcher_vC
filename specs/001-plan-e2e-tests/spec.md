# Feature Specification: E2E Test Implementation Plan

**Feature Branch**: `001-plan-e2e-tests`  
**Created**: 2025-11-30  
**Status**: Draft  
**Input**: User description: "E2E テスト実装計画 前提条件 既存の統合テストインフラ(TestEnvironmentBuilder, TestDataFactory)を活用 ViewModel 層からのテストで GUI 層はスキップ 実際の ESP ファイル生成と検証を行う"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - ViewModel-driven patch generation (Priority: P1)

QA engineers need to execute the full Munition AutoPatcher flow end-to-end by instantiating the ViewModel layer via TestEnvironmentBuilder, producing a real ESP artifact without invoking the GUI shell.

**Why this priority**: This proves that the most critical user outcome—correct ESP generation—is exercised exactly as players would experience it, ensuring regressions are caught before release.

**Independent Test**: Provision the predefined scenario, trigger the ViewModel command set, and confirm the resulting ESP plus logs satisfy the expected assertions with no UI interactions.

**Acceptance Scenarios**:

1. **Given** a seeded plugin set produced by TestDataFactory, **When** the WeaponOmodExtractor ViewModel flow runs under the E2E harness, **Then** a new ESP is emitted in the scenario artifact folder with the configured ModKey.
2. **Given** baseline expectations for record counts and key mappings, **When** the ESP validator runs, **Then** counts, ammo linkages, and metadata match the scenario definition without manual review.

---

### User Story 2 - Scenario authors expand coverage (Priority: P2)

Test authors must be able to describe new end-to-end scenarios (mods, ammo mappings, expected outcomes) using TestDataFactory so gaps can be plugged quickly as new weapon packs are supported.

**Why this priority**: Sustainable coverage depends on low-friction scenario authoring; otherwise the E2E suite will stagnate as new mods arrive.

**Independent Test**: Define a fresh scenario file, regenerate fixtures via TestDataFactory, and verify the harness discovers and executes it without bespoke code changes.

**Acceptance Scenarios**:

1. **Given** a new scenario definition referencing additional ESP inputs, **When** the author registers it through TestDataFactory, **Then** the harness lists and executes it during the next run.
2. **Given** scenario-specific assertions (e.g., expected ammo-to-weapon mapping), **When** validations execute, **Then** mismatches are reported with actionable diagnostics tied to that scenario.

---

### User Story 3 - Artifact validation in CI (Priority: P3)

Build engineers need deterministic validation of generated ESPs inside CI so regressions surface automatically and artifacts are retained for diffing.

**Why this priority**: Automated enforcement in CI is the final gate that ensures the ViewModel-based flow is reliable outside developer workstations.

**Independent Test**: Run the E2E suite in a headless Windows agent, capture produced ESPs, and compare them to golden artifacts; failures block the pipeline with clear logs.

**Acceptance Scenarios**:

1. **Given** nightly CI execution, **When** the suite runs, **Then** each ESP artifact is compared byte-wise (with normalized headers) against its baseline and deviations fail the build.
2. **Given** a regression that alters form mappings, **When** CI validation runs, **Then** the job surfaces the failing scenario, provides the diff path, and stores both artifacts for download.

---

### Edge Cases

- Scenario references a plugin that TestEnvironmentBuilder cannot locate; harness must fail fast with a remediation hint instead of producing partial ESPs.
- ESP output contains timestamped headers causing non-deterministic diffs; validator must normalize or ignore those regions before comparison.
- Multiple scenarios run in parallel and compete for the same temp directories; harness must isolate filesystem state to prevent cross-contamination.
- Validation detects extra records not declared in expectations; system must either auto-update expectations through explicit approval or flag as regression without deleting artifacts.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Harness MUST instantiate Munition AutoPatcher ViewModels through TestEnvironmentBuilder so every E2E test exercises the same dependency graph as production while explicitly skipping WPF rendering.
- **FR-002**: Each scenario MUST be defined through TestDataFactory (or an extension thereof) so inputs, mod keys, and expected outputs remain declarative and version-controlled.
- **FR-003**: Test execution MUST generate an actual ESP file per scenario using the production Mutagen pipeline, storing artifacts under a deterministic directory structure for later inspection.
- **FR-004**: Validation logic MUST verify both structural (record counts, ammo mappings, weapon-omod associations) and binary expectations, allowing tolerances for known non-deterministic regions defined per scenario.
- **FR-005**: Harness MUST expose scenario-level assertion hooks so authors can check ViewModel state (logs, progress, warnings) alongside ESP contents without editing core orchestration code.
- **FR-006**: Runs MUST capture diagnostics (logs, JSON summaries, diff reports) whenever validation fails, and attach them to CI artifacts so failures can be investigated without rerunning locally.
- **FR-007**: Execution MUST isolate filesystem state via per-scenario temp directories and cleanly dispose of them after validation to avoid interference between tests.
- **FR-008**: Suite MUST integrate with the existing integration-test entry points so it can run in CI within the current time budget (target under 10 minutes for the initial scenario set).

### Key Entities *(include if feature involves data)*

- **E2EScenarioDefinition**: Declarative description of inputs (plugins, config knobs), expected outputs (record counts, ammo mappings), and any normalization rules; stored alongside integration test assets.
- **ESPValidationProfile**: Describes how to inspect generated ESPs (structural checks, ignore regions, baseline artifact references) and links to diagnostics for diffing.
- **ScenarioRunArtifact**: Bundle consisting of produced ESP, logs, metadata manifest, and diff results saved per execution for CI retention.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: At least three high-value weapon mod scenarios execute end-to-end via the ViewModel harness with zero manual steps within 5 minutes per scenario on a standard CI agent.
- **SC-002**: 100% of executed scenarios produce ESP artifacts whose structural validation passes on the first run, with failures providing actionable diagnostics in under 60 seconds from completion.
- **SC-003**: CI pipeline publishes deterministic ESP artifacts nightly, with at least 90% of runs yielding byte-identical (post-normalization) files compared to their baselines; deviations automatically fail the build.
- **SC-004**: Adding a new E2E scenario (definition + expectations) takes less than 30 minutes of author effort, measured by onboarding feedback once the suite is documented.

### References *(if Mutagen-related)*

- Architecture and boundary rules: `.specify/memory/constitution.md` セクション 2, 8, 11, 12。
- Existing integration harness context: `tests/IntegrationTests` and `archive/INTEGRATION_TESTS_IMPLEMENTATION_SUMMARY.md` for prior environment setup decisions.
- Mutagen API / レコード定義の根拠:
  - Use MCP server `mcp_mutagen-rag_search_repository` to query `Mutagen-Modding/Mutagen` for generated C# files and XML schemas.
  - Capture key links or file paths used during research here to justify proposed APIs.

## Assumptions

- Windows-based CI agents provide Mutagen dependencies and Fallout 4 masters needed by the scenarios; local developers can mirror the setup through existing TestEnvironmentBuilder scripts.
- Golden ESP artifacts live under `tests/IntegrationTests/Artifacts` (or a new adjacent directory) with version control approval so baseline updates are intentional.
- Timestamp or header bytes that vary between runs can be normalized without losing validation fidelity, enabling deterministic diffing.
