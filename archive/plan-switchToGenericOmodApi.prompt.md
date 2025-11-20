Plan: Switch to Generic OMOD API

Goals
- Align OMOD enumeration with Mutagen’s official generic API.
- Restore reverse-scan detection to produce >0 candidates.
- Improve diagnostics to catch regressions early.
- Add minimal tests to lock in behavior.

Changes
1) MutagenV51EnvironmentAdapter: Generic OMOD winners
- Replace reflection/fallback logic in GetWinningObjectModificationsTyped with a single generic call:
  environment.LoadOrder.PriorityOrder.WinningOverrides<IAObjectModificationGetter>()
- Remove reflection-based accessor/properties search and per-mod fallback loops.
- Rationale: Official, type-safe, efficient; avoids version/friction around accessor names.

2) EnumerateRecordCollectionsTyped: Ensure OMOD presence + counts
- Populate the "ObjectModification" collection from the generic winners above.
- Emit per-collection counts at Info for quick triage (e.g., Weapon, ConstructibleObject, ObjectModification, Armor, Ammunition).
- If the OMOD winners are empty, log a Warning summarizing attempted approach and totals.

3) ReverseReferenceCandidateProvider: Fix single-link scanning
- For single-link properties, inspect the runtime value type (value.GetType()), not only the declared property type.
- Include IFormLinkGetter/IFormLink values and attempt to read FormKey from the value instance.
- Keep enumerable handling (iterable of links) as-is; cap still OK.
- Outcome: Single-link references (e.g., CreatedObject, weapon/ammo links) are detected.

4) Diagnostics: Harden visibility and fallback points
- When OMOD winners resolve to zero, log a Warning with environment and load context (mods counted, winners count).
- After typed enumeration, log a one-line summary with per-collection totals.
- (Optional) If "ObjectModification" is absent in typed collections, trigger a provider-level warning and surface a diagnostic marker.

5) Tests: Minimal lock-in
- Unit: Provider detects interface-typed FormLink (single-link scenario) after runtime-value fix.
- Adapter: OMOD winners call returns non-empty with an OMOD-containing fixture (or mocked adapter contract if fixtures unavailable).
- Integration (lightweight): Reverse-scan over a small fixture produces >0 candidates when OMODs are present.

Version/Capability Notes
- If the generic extension is unavailable (unlikely on v0.51.5), guard with a capability flag and log a Warning; keep the code path simple and avoid complex reflection fallbacks.
- Prefer explicit capability logging over silent fallback to reduce ambiguity in diagnostics.

Rollout Sequence
1. Implement items 1 and 3 (generic OMOD winners + single-link runtime-value scan).
2. Build, run a manual pass, verify reverse-scan > 0 and per-collection counts include ObjectModification > 0.
3. Implement item 2 (per-collection counts + warnings) and item 4 (diagnostics).
4. Add minimal tests from item 5 to prevent regressions.

Acceptance Criteria
- Reverse-reference scan reports >0 candidates.
- Per-collection summary shows ObjectModification count > 0 (non-trivial).
- ESP generation logs success > 0 (not all skipped).
- CSV outputs contain non-empty OMOD candidate rows.

Risks & Mitigations
- Risk: Generic method not available due to package mismatch → Mitigate with capability log + targeted package verification.
- Risk: Downstream code assumes old reflection-based shapes → Mitigate by preserving public method signatures and return types.
- Risk: Performance regressions in scanning → Mitigate with typed winners (already efficient) and maintain current enumerable cap.

Next Actions
- Apply adapter change to use WinningOverrides<IAObjectModificationGetter>().
- Patch ReverseReferenceCandidateProvider to use runtime value type for single-link.
- Add per-collection count logging and warnings for empty OMOD winners.
- Add unit/integration tests as described above.
