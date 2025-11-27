description: Decompose WeaponOmodExtractor.ExtractCandidatesAsync into an orchestrator; extract diagnostics, candidate providers, and confirmer behind DI; abstract Mutagen and paths; improve error handling and logging; clean up constants/comments.
OMOD extractor refactor plan
Branch: refactor/omod-extractor/1-decompose-method
Scope: Split responsibilities of extraction into diagnostics, providers, confirmation, and orchestration with DI; reduce UI/static coupling; keep public contracts stable.

## Objectives
Make ExtractCandidatesAsync a thin orchestrator.
Move file/CSV/marker writing to a dedicated DiagnosticWriter.
Introduce strategy providers for candidate discovery (COBJ/reflection).
Separate candidate confirmation via ReverseMapConfirmer.
Improve error handling (scoped try-catch) and switch to DI logging/progress.
Abstract Mutagen reflection and path lookup.
Clean up: remove dead code/comments; move hard-coded strings to config/const; add regions.

## Decisions on Open Questions

Based on the initial discussion, the following high-level decisions have been made:

1.  **Logging Strategy:**
    *   **Decision:** Standardize on `Microsoft.Extensions.Logging.ILogger<T>`.
    *   **Rationale:** It is the .NET standard, DI-friendly, testable, and supports flexible configuration and structured logging.
    *   **Migration:** The existing `AppLogger` will be temporarily adapted to wrap `ILogger<T>`, allowing for a gradual replacement of its direct calls across the codebase without breaking existing functionality.

2.  **CSV Output Specification:**
    *   **Decision:** Utilize type-safe DTOs (Data Transfer Objects) with fixed headers and filename conventions defined within the code.
    *   **Rationale:** This approach prioritizes robustness and maintainability over the unnecessary complexity of external configuration (`config.json`). It ensures compile-time safety and makes the output structure self-documenting within the code.

3.  **`ExtractCandidatesAsync` Signature:**
    *   **Decision:** The public contract for `IWeaponOmodExtractor.ExtractCandidatesAsync` will be unified to always require a `CancellationToken` as a parameter.
    *   **Rationale:** This aligns with modern asynchronous programming best practices, improves API clarity, and eliminates the need for the internal `_pendingToken` field, resulting in cleaner and safer code. Existing callers will be updated to pass `CancellationToken.None`.
Interfaces to add (Services/Interfaces)
IDiagnosticWriter
void WriteStartMarker(ExtractionContext ctx)
void WriteDetectorSelected(string name, ExtractionContext ctx)
void WriteReverseMapMarker(ExtractionContext ctx)
void WriteDetectionPassMarker(ExtractionContext ctx)
void WriteResultsCsv(IEnumerable<OmodCandidate> confirmed, ExtractionContext ctx)
void WriteZeroReferenceReport(IEnumerable<OmodCandidate> confirmed, ExtractionContext ctx)
void WriteCompletionMarker(ExtractionContext ctx)
ICandidateProvider
IEnumerable<OmodCandidate> GetCandidates(ExtractionContext context)
ICandidateConfirmer
void Confirm(IEnumerable<OmodCandidate> candidates, ConfirmationContext context)
IMutagenAccessor
object? GetLinkCache(IResourcedMutagenEnvironment env)
IEnumerable<object> EnumerateRecordCollections(IResourcedMutagenEnvironment env, string collectionName)
IEnumerable<object> GetWinningWeaponOverrides(IResourcedMutagenEnvironment env)
Helper methods to fetch FormKey/EditorID safely (wrapping MutagenReflectionHelpers)
IPathService
string GetRepoRoot()
Notes:

Keep signatures minimal as above. Progress/Cancellation can be added later if required (backward-compatible overloads).
Models to add (Models/)
ExtractionContext: env, linkCache, excludedPlugins (HashSet<string>), config, repoRoot, timestamp, etc.
ConfirmationContext: reverseMap, linkCache, config, etc.
Implementations to add (Services/Implementations)
DiagnosticWriter : IDiagnosticWriter
Encapsulates all current marker/CSV writes from WeaponOmodExtractor, including:
start/completion markers; detector markers; detection pass markers
noveske diagnostics CSV; zero-reference aggregated outputs
Uses IPathService for paths; ILogger<DiagnosticWriter> for logging.
CobjCandidateProvider : ICandidateProvider
Calls _weaponDataExtractor.ExtractAsync(env, excluded, progress?) and returns candidates.
ReverseReferenceCandidateProvider : ICandidateProvider
Performs reflection-based scan over records to find candidates referencing weapons (logic currently in WeaponOmodExtractor).
ReverseMapConfirmer : ICandidateConfirmer
Moves logic from private static ConfirmCandidatesThroughReverseMap(...) into Confirm(...).
MutagenAccessor : IMutagenAccessor
Wraps/uses MutagenReflectionHelpers; centralizes Mutagen version quirks.
Changes to existing classes
WeaponOmodExtractor (Services/Implementations/WeaponOmodExtractor.cs)
ctor: add IDiagnosticWriter diagnosticWriter, IEnumerable<ICandidateProvider> providers, ICandidateConfirmer confirmer, IMutagenAccessor mutagenAccessor, IPathService pathService, ILogger<WeaponOmodExtractor> logger.
ExtractCandidatesAsync flow:
Build ExtractionContext (env/linkCache/excluded/config/repoRoot).
diagnosticWriter.WriteStartMarker(ctx).
Aggregate candidates = providers.SelectMany(p => p.GetCandidates(ctx)).ToList().
confirmer.Confirm(candidates, confirmationContext).
diagnosticWriter.WriteResultsCsv/WriteZeroReferenceReport(ctx).
diagnosticWriter.WriteCompletionMarker(ctx).
Remove direct UI logging (MainViewModel) and static AppLogger calls in this method; prefer IProgress<string> (method param) and ILogger for diagnostics.
Keep existing IWeaponOmodExtractor.ExtractCandidatesAsync(IProgress<string>? progress = null) signature for compatibility; optionally keep the overload with CancellationToken.
WeaponDataExtractor
No signature change. Ensure it uses ILogger instead of AppLogger where feasible (gradual).
MutagenReflectionHelpers & RepoUtils
Keep for now; route new code via IMutagenAccessor/IPathService; gradually replace direct usages.
Error handling
Replace one large try-catch with scoped blocks:
(a) Start marker, (b) Providers aggregation, (c) Confirmation, (d) Report output, (e) Completion marker.
Each block: catch/log non-fatal errors, continue where safe; propagate fatal exceptions if they invalidate the stage.
DI registrations (MunitionAutoPatcher/App.xaml.cs)
services.AddSingleton<IDiagnosticWriter, DiagnosticWriter>();
services.AddSingleton<ICandidateProvider, CobjCandidateProvider>();
services.AddSingleton<ICandidateProvider, ReverseReferenceCandidateProvider>();
services.AddSingleton<ICandidateConfirmer, ReverseMapConfirmer>();
services.AddSingleton<IMutagenAccessor, MutagenAccessor>();
services.AddSingleton<IPathService, PathService>();
Ensure logging is configured (ILogger<T> available).
IWeaponOmodExtractor stays singleton and now consumes above services.
Files to add (relative paths)
MunitionAutoPatcher/Services/Interfaces/IDiagnosticWriter.cs
MunitionAutoPatcher/Services/Interfaces/ICandidateProvider.cs
MunitionAutoPatcher/Services/Interfaces/ICandidateConfirmer.cs
MunitionAutoPatcher/Services/Interfaces/IMutagenAccessor.cs
MunitionAutoPatcher/Services/Interfaces/IPathService.cs
MunitionAutoPatcher/Services/Implementations/DiagnosticWriter.cs
MunitionAutoPatcher/Services/Implementations/CobjCandidateProvider.cs
MunitionAutoPatcher/Services/Implementations/ReverseReferenceCandidateProvider.cs
MunitionAutoPatcher/Services/Implementations/ReverseMapConfirmer.cs
MunitionAutoPatcher/Services/Implementations/MutagenAccessor.cs
MunitionAutoPatcher/Services/Implementations/PathService.cs
MunitionAutoPatcher/Models/ExtractionContext.cs
MunitionAutoPatcher/Models/ConfirmationContext.cs
Files to edit (high level)
WeaponOmodExtractor.cs
Remove in-method file writes (markers/CSV) and UI direct logs; inject and call services; shrink to orchestration.
Delete or deprecate private static ConfirmCandidatesThroughReverseMap; delegate to ReverseMapConfirmer.
Replace RepoUtils.FindRepoRoot() calls with IPathService; replace AppLogger with ILogger.
App.xaml.cs
Register new services, ensure ILogger pipeline.
Optionally replace AppLogger usages across ViewModels/Services gradually with ILogger.
Tests to update (paths)
ConfirmReverseMapCancellationTests.cs
Update to target ReverseMapConfirmer.Confirm(...) instead of reflection on WeaponOmodExtractor.
WeaponOmodExtractorCancellationTests.cs
Validate ExtractCandidatesAsync cancellation behavior still holds with orchestrator.
tests/WeaponDataExtractor (no signature change; verify behavior unaffected).
Consider new unit tests:
DiagnosticWriter (writes expected files/markers with stable names)
CobjCandidateProvider and ReverseReferenceCandidateProvider
ReverseMapConfirmer happy/edge cases
Naming, paths, and outputs
Keep existing artifacts directory and file naming patterns by default.
Move hard-coded strings (e.g., "noveskeRecceL.esp") to config.json or internal constants; expose via IDiagnosticWriter if they affect output layout.
Risks and assumptions
Mutagen version differences: isolate via IMutagenAccessor to reduce reflection fragility.
UI coupling: Ensure no App.Current.* references from services; ViewModels consume progress/logging separately.
Backward compatibility: Preserve IWeaponOmodExtractor public signatures; adapters may be needed for interim.
Milestones
Phase 1: Introduce interfaces; move diagnostics; inject into WeaponOmodExtractor; compile green.
Phase 2: Providers + confirmer; orchestrator refactor; tests updated to new targets.
Phase 3: Abstractions (Mutagen/Path) and cleanup; replace AppLogger usages where safe.
Acceptance criteria
ExtractCandidatesAsync < ~150 lines, orchestration-only.
All diagnostics/CSV/marker writes live in DiagnosticWriter.
Candidate discovery and confirmation are externally testable units.
App builds and existing tests pass; updated tests cover new units.