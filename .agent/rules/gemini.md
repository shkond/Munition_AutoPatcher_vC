---
trigger: always_on
---

# Munition AutoPatcher vC Constitution

**Core Mandate:** Establish design standards, Mutagen boundaries, and AI workflows. Strict adherence required.

## 0. Language & Interaction Protocol
- **Reasoning / Chain of Thought:** MUST be in **English** (to maximize logical precision).
- **Response / Planning / Chat:** MUST be in **Japanese (日本語)**.

## 0. Prerequisites
- **No Mutagen Version Pinning:** Use Detector Pattern for capability checks.
- **No `config.json` / DocFx:** Use DI/ConfigService.
- **Architecture:** Record decisions in `DECISIONS.md`.

## 1. Architecture (MVVM & DI)
- **Strict MVVM:** - **View:** XAML only. No business logic.
  - **ViewModel:** Presentation logic/commands only. Instantiated via DI.
  - **Model:** UI-agnostic business logic.
- **DI:** `Microsoft.Extensions.DependencyInjection`. Constructor Injection ONLY.
  - **Lifetimes:** Transient (stateless), Singleton (shared). **No Scoped.**
- **Async:** `ICommand` must be `AsyncRelayCommand`. No `.Result` / `.Wait`.

## 2. Mutagen Integration
- **Boundary:** Direct Mutagen calls **PROHIBITED** outside `IMutagenAccessor`.
- **Detector Pattern:** Detect caps at startup → Cache Strategy → Inject `IMutagenApiStrategy`.
- **Resources:** strict `Dispose` ownership for `GameEnvironment` / `LinkCache`.
- **Performance:** Prefer `WinningOverrides`. Stream processing (1-pass). Avoid `ToList()`.

## 3. Orchestration
- **Orchestrator:** Delegates logic to `ICandidateProvider` (OCP). No hardcoded logic.
- **Output:** Centralized via `DiagnosticWriter` / `AppLogger`.

## 4. Async & Threading
- **UI:** Updates via `Dispatcher`. Services use `ConfigureAwait(false)`.
- **Cancel:** All async flows must accept `CancellationToken`.
- **Prohibited:** `Thread.Sleep`, synchronous blocking.

## 5. Diagnostics & Logging
- **Paths:** `./artifacts/logs/munition_autopatcher_ui.log` (Fallback: `%TEMP%`).
- **Channels:** `ILogger<T>` (Service), `IAppLogger` (UI/User).
- **Rules:**
  - No `Console.WriteLine`.
  - Flush/Dispose `AppLoggerProvider` on exit.
  - Min Level: `Information` (Overridable via `MUNITION_LOG_LEVEL`).
  - Fatal errors = UI Notification + Log.

## 6. WPF & Accessibility
- **Focus:** Define `FocusVisualStyle`. Set explicit initial focus.
- **A11y:** Errors must trigger `AutomationPeer` notification (LiveRegion).

## 7. Collections (UI)
- **Type:** `ObservableCollection`.
- **Ops:** Updates on UI Thread only. Use `AddRange` / `Reset` for batches.
- **View:** Use `ICollectionView` for sort/filter. Enable Virtualization.

## 8. Lifecycle (Mutagen)
- **Disposal:** Use `using` / `await using`. Explicit ownership.
- **Plan:** Accessors must define `DisposePlan`. No heavy dispose on UI thread.

## 9. Error Handling
- **Fatal:** Stop → Notify → Log.
- **Warning:** Accumulate → Report.
- **Privacy:** Mask absolute paths in logs.
- **Rule:** Never swallow exceptions.

## 10. Testing
- **Unit:** Recommended for services.
- **Integration:** Required for Mutagen interaction.
- **Regression:** Snapshot testing for critical mapping logic.

## 11. AI-Assisted Development (MANDATORY)

**Goal:** Prevent reflection and unsafe API usage.

### Guardrails
- **PROHIBITED:** Reflection, `dynamic`, Direct Mutagen calls in View/VM.
- **REQUIRED:** `IMutagenAccessor`, Async, `DisposePlan`, `WinningOverrides`.
- **VERIFICATION:** MUST use GitHub MCP to inspect `Mutagen-Modding/Mutagen` for exact signatures BEFORE proposing APIs.

### Workflow Stages
1. **Stage 1 - Review:** Propse APIs only (No code).
2. **Stage 2 - Design:** Agree on DTOs, Error Policy, Lifecycle.
3. **Stage 3 - Spike:** Minimal typed snippets.
4. **Stage 4 - Impl:** Full code generation.

### AI Output Requirements (Stage 1)
- **ProposedAPIs:** Concrete Type/Method/Namespace.
- **Rationale:** Why chosen.
- **ErrorPolicy & DisposePlan:** Explicit strategy.
- **References:** Official Docs OR **Query GitHub MCP (`Mutagen-Modding/Mutagen`)**.

### Rejection Criteria
- Use of Reflection/dynamic.
- Missing namespace/docs.
- Undefined `LinkCache` lifecycle.

## 12. Change Management
- **Const:** Changes via PR.
- **Arch:** Record in `DECISIONS.md`.
- **Breaking:** Tag in release notes.

## 13. Prohibited Practices (Summary)
- Direct Mutagen calls outside Accessor.
- `Console.WriteLine`.
- Sync Blocking (`.Result`).
- Ad-hoc File I/O.
- Static Mutable State.
- Empty `catch` blocks.