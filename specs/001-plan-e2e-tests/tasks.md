---

description: "Task list template for feature implementation"
---

# Tasks: E2E Test Implementation Plan

**Input**: Design documents from `/specs/001-plan-e2e-tests/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: End-to-end harness work is itself a test suite, so test tasks are explicitly listed per user story as required by the feature charter.

**Organization**: Tasks are grouped by user story to enable independent implementation and validation.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Task can execute in parallel (different files, no blocking dependencies)
- **[Story]**: User story label (US1, US2, US3) once phases reach story work
- Include exact file paths in descriptions

## Path Conventions

Paths reference the existing solution layout:
- `MunitionAutoPatcher/` for production services/ViewModels
- `tests/IntegrationTests/` for integration and E2E harness files
- `.github/workflows/` for CI automation
- `specs/001-plan-e2e-tests/` for feature documentation

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Track repository scaffolding and housekeeping before harness code lands.

- [X] T001 Create tests/IntegrationTests/Infrastructure/README.md and tests/IntegrationTests/Scenarios/.gitkeep to document the new harness directory layout per quickstart guidance
- [X] T002 Update tests/IntegrationTests/IntegrationTests.csproj so it compiles `Infrastructure/**/*.cs`, embeds `Scenarios/**/*.json`, and exposes the folders to the test project
- [X] T003 [P] Add output exclusions (tests/IntegrationTests/TestResults/, `%TEMP%/MunitionAutoPatcher_E2E_Tests/`) to .gitignore to keep generated ESPs and diagnostics out of source control

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Define the shared data contracts and Mutagen research outputs every story depends on.

- [X] T004 [P] Create tests/IntegrationTests/Infrastructure/Models/E2EScenarioDefinition.cs implementing E2EScenarioDefinition, PluginSeed, ScenarioAssertion, Range, and CustomCheck per data-model.md
- [X] T005 [P] Create tests/IntegrationTests/Infrastructure/Models/ScenarioRunArtifact.cs implementing ScenarioRunArtifact, DiagnosticBundle, ValidationResult, and related structs reused across stories
- [X] T006 Capture Stage 1 ProposedAPIs, ErrorPolicy, and DisposePlan for EspFileValidator + TestServiceProvider inside specs/001-plan-e2e-tests/research.md with MCP references to the relevant Mutagen APIs

**Checkpoint**: Shared contracts and research guardrails in place — user story work can now begin.

---

## Phase 3: User Story 1 - ViewModel-driven patch generation (Priority: P1) 🎯 MVP

**Goal**: Run MapperViewModel end-to-end via TestEnvironmentBuilder, generate a real ESP, and validate it without touching the WPF shell.

**Independent Test**: Provision the baseline scenario, execute MapperViewModel through the harness, and confirm ESP generation plus validator checks succeed using only ViewModel commands.

### Tests for User Story 1

- [X] T007 [P] [US1] Add a failing xUnit fixture skeleton in tests/IntegrationTests/ViewModelE2ETests.cs that provisions a baseline scenario and asserts an ESP path placeholder
- [X] T008 [P] [US1] Add tests/IntegrationTests/Infrastructure/EspFileValidatorTests.cs to cover header normalization and structural count assertions before implementation exists

### Implementation for User Story 1

- [X] T009 [P] [US1] Implement tests/IntegrationTests/Infrastructure/TestServiceProvider.cs that mirrors App.xaml.cs registrations, swapping test-safe IConfigService/IPathService/IDiagnosticWriter
- [X] T010 [P] [US1] Implement tests/IntegrationTests/Infrastructure/AsyncTestHarness.cs to coordinate CancellationToken, timeout enforcement, and orderly disposal around MapperViewModel runs
- [X] T011 [US1] Implement tests/IntegrationTests/Infrastructure/EspFileValidator.cs performing Mutagen overlay reads plus header-field normalization as documented in research.md
- [X] T012 [US1] Implement tests/IntegrationTests/Infrastructure/ViewModelHarness.cs that builds TestEnvironmentBuilder, resolves MapperViewModel via TestServiceProvider, runs commands, and returns ScenarioRunArtifact data
- [X] T013 [US1] Update tests/IntegrationTests/ViewModelE2ETests.cs to execute the harness end-to-end, assert ScenarioRunArtifact contents (ESP exists, structural counts pass), and capture diagnostics/logs

**Checkpoint**: MapperViewModel-driven ESP generation validated for the baseline scenario with deterministic results.

---

## Phase 4: User Story 2 - Scenario authors expand coverage (Priority: P2)

**Goal**: Allow authors to describe additional scenarios declaratively via TestDataFactory and scenario manifests so coverage can grow without new code per scenario.

**Independent Test**: Drop a new scenario definition under the Scenarios folder, rerun the suite, and verify the harness discovers, seeds, and validates it automatically with scenario-specific diagnostics.

### Tests for User Story 2

- [X] T014 [P] [US2] Add tests/IntegrationTests/Infrastructure/ScenarioCatalogTests.cs validating JSON schema, duplicate detection, and config override enforcement for scenario manifests

### Implementation for User Story 2

- [X] T015 [P] [US2] Implement tests/IntegrationTests/Infrastructure/ScenarioCatalog.cs that loads Scenarios/*.json, materializes E2EScenarioDefinition objects, and exposes them to the harness
- [X] T016 [P] [US2] Implement tests/IntegrationTests/Infrastructure/ScenarioManifestSerializer.cs to read/write scenario manifests with strict validation errors per data-model.md
- [X] T017 [US2] Implement tests/IntegrationTests/Infrastructure/TestDataFactoryScenarioExtensions.cs registering builder actions referenced by PluginSeeds so scenarios reuse TestDataFactory helpers
- [X] T018 [US2] Add scenario manifests tests/IntegrationTests/Scenarios/scenario-basic-mapping.json and tests/IntegrationTests/Scenarios/scenario-dlc-remap.json covering distinct ammo/weapon mappings plus validation profiles
- [X] T019 [US2] Update tests/IntegrationTests/ViewModelE2ETests.cs to enumerate ScenarioCatalog results, execute each scenario, and surface scenario-specific assertion failures without manual test changes

**Checkpoint**: Multiple declarative scenarios can be authored and executed without code changes, enabling rapid coverage growth.

---

## Phase 5: User Story 3 - Artifact validation in CI (Priority: P3)

**Goal**: Enforce deterministic ESP validation inside CI, publish artifacts, and fail builds when baselines drift.

**Independent Test**: Run the suite on a headless agent, ensure ESPs are compared to golden baselines, and confirm artifacts upload with diff logs upon failure.

### Tests for User Story 3

- [X] T020 [P] [US3] Add tests/IntegrationTests/Infrastructure/ScenarioArtifactPublisherTests.cs to exercise manifest serialization and artifact packaging failure modes
- [X] T021 [P] [US3] Add tests/IntegrationTests/Infrastructure/BaselineDiffTests.cs covering ESP baseline comparison, ignore-list handling, and fatal vs warning classification

### Implementation for User Story 3

- [X] T022 [P] [US3] Implement tests/IntegrationTests/Infrastructure/ScenarioArtifactPublisher.cs that copies ESPs/logs/diagnostics into per-scenario folders, zips them, and records ScenarioRunArtifact manifests
- [X] T023 [US3] Extend tests/IntegrationTests/Infrastructure/EspFileValidator.cs to compare generated ESPs with baselines under tests/IntegrationTests/Artifacts/ and emit diff reports referenced by diagnostics
- [X] T024 [US3] Add baseline ESP artifacts plus README under tests/IntegrationTests/Artifacts/ describing approval workflow for updated baselines
- [X] T025 [US3] Add .github/workflows/e2e-tests.yml running `dotnet test tests/IntegrationTests/IntegrationTests.csproj --filter FullyQualifiedName~ViewModelE2ETests`, uploading `e2e-test-results`, and gating on failures
- [X] T026 [US3] Update run-integration-tests.ps1 and run-integration-tests.sh to accept a `-Suite ViewModelE2E` option that executes the new tests and collects artifacts locally

**Checkpoint**: CI enforces deterministic ESP validation with downloadable artifacts for every run.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final documentation, governance, and validation touches shared across stories.

- [X] T027 [P] Document scenario onboarding steps and quickstart pointers inside docs/README.md so contributors can find the new harness quickly
- [X] T028 [P] Record Stage 2 design agreements (DTO signatures, cancellation strategy) in DECISIONS.md for long-term traceability
- [X] T029 Update specs/001-plan-e2e-tests/quickstart.md after running the end-to-end suite to confirm commands, artifact paths, and troubleshooting steps remain accurate

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No prerequisites — must complete before foundational work.
- **Foundational (Phase 2)**: Depends on Setup — defines contracts and research gating all stories.
- **User Stories (Phases 3-5)**: Each depends on Phase 2 but can proceed in priority order once the foundation is stable.
- **Polish (Phase 6)**: Runs after the desired user stories are complete.

### User Story Dependencies

- **US1 (P1)**: First deliverable after foundation; produces the MVP harness.
- **US2 (P2)**: Builds atop US1 harness outputs but remains independently testable via new scenarios.
- **US3 (P3)**: Relies on US1 validator outputs and US2 scenario catalog to enforce CI rules.

### Within Each User Story

- Write listed tests (T007/T008, T014, T020-T021) before implementing the corresponding infrastructure.
- Build data models/services (e.g., T009-T012, T015-T017, T022-T024) prior to wiring tests or CI.
- Update shared test suites (T013, T019) only after underlying infrastructure compiles.

### Parallel Opportunities

- Setup: T003 can run in parallel with T001-T002 since it only touches .gitignore.
- Foundational: T004 and T005 operate on separate model files and can be completed concurrently once directories exist.
- Cross-story parallelism becomes available after Phase 2; any story-specific tasks marked [P] are safe to run simultaneously by different contributors.

---

## Parallel Execution Examples

### User Story 1

```
# Run tasks that can proceed together
T007 + T008  # author failing tests in ViewModelE2ETests and EspFileValidatorTests
T009 + T010  # scaffold TestServiceProvider and AsyncTestHarness in parallel before wiring the harness
```

### User Story 2

```
# Parallelizable authoring tasks
T015 + T016  # build ScenarioCatalog and serializer concurrently
T018 (scenario manifests) can proceed while T017 wires TestDataFactory extensions
```

### User Story 3

```
# Concurrent CI-focused work
T020 + T021  # write artifact publisher and baseline diff tests first
T022 + T025  # implement artifact publisher while CI workflow scaffolding is prepared
```

---

## Implementation Strategy

### MVP First

1. Complete Phases 1-2 to satisfy constitutional guardrails and shared contracts.
2. Deliver User Story 1 (Phase 3) as the MVP: MapperViewModel-driven ESP generation plus validator.
3. Validate the MVP independently using the baseline scenario before moving on.

### Incremental Delivery

1. After MVP, add declarative scenario authoring (Phase 4) to expand coverage without touching code per scenario.
2. Layer on CI enforcement (Phase 5) to gate regressions once scenarios exist.
3. Finish with documentation and governance updates (Phase 6) to keep contributors aligned.

### Parallel Team Strategy

- Once Phase 2 is complete, assign US1 to one engineer, US2 to another, and US3 to a third; coordination happens via the shared models and harness contracts defined earlier.
- Marked [P] tasks highlight safe concurrency opportunities within each story.
