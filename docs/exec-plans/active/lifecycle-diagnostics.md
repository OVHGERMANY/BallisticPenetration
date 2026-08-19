# Lifecycle Diagnostics - Active Plan

## Requirements
- [x] Track and log `ballisticTerminal` and `lifecycleTerminal` states for each observed physical projectile event.
- [ ] Track `continued` and `replaced` flags with explicit semantics:
  - Stopped: `continued=false`, `replaced=false`, `ballisticTerminal=true`, `lifecycleTerminal=true`.
  - Continued same projectile: `continued=true`, `replaced=false`, `ballisticTerminal=false`, `lifecycleTerminal=false`.
  - Continued via replacement: original gets `continued=true`, `replaced=true`, `ballisticTerminal=false`, `lifecycleTerminal=true`; replacement must get new lifecycle identity.
  - Runtime/runtime-abort retirement: `lifecycleTerminal=true`, `ballisticTerminal=false`, `lifecycleEndReason=aborted`.
- [ ] Emit collision phase records: `event=collision-observed` then `event=collision-resolved`.
- [ ] Deduplicate by `(projectileIdentity, collisionSequence)` so each phase is emitted once.
- [ ] Keep both `recordSequence` and derived per-lifecycle `collisionOrdinal`.
- [ ] Add bounded trackers with lifecycle tombstones and duplicate-terminal detection.
- [ ] Emit `terminal-missing` for terminal-lifecycle gaps and `terminal-duplicate` on repeats.
- [ ] Add explicit `shutdown-cleanup` marker to exclude expected unload cleanup from missing-terminal defects.
- [ ] Keep gameplay behavior untouched.

## Files Expected to Change
- `src/BallisticPenetration/...` diagnostics runtime/state + validation tests (execution steps only after this memory pass).
- `docs/PROJECT_STATE.md`
- `docs/DECISIONS.md`

## Prohibited Gameplay Changes
- No changes to trajectory, velocity, penetration, damage, fragment behavior, collision outcomes, or other core gameplay systems.

## Validation Commands
- `dotnet build BallisticPenetration.sln -c Release -p:SptRoot="E:\Games\SPT" -p:TreatWarningsAsErrors=true`
- BallisticPenetration strict validation test suite (all diagnostics tests).

## Latest Validation and Checkpoint
- Build result: `dotnet build ... -p:TreatWarningsAsErrors=true` succeeded, 0 warnings, 0 errors.
- Validation result: `dotnet run --project BallisticPenetration.Validation...` => 46 passed, 0 failed.
- Gameplay behavior: diagnostics-only changes for terminal telemetry fields; no trajectory, velocity, penetration, damage, fragments, collision outcomes, or branch logic modified.
- Next action: `collision-observed`/`collision-resolved` correlation and deduplication.

## Installation Steps
- Build release DLL.
- Deploy only if approved by current objective.
- Verify runtime hash equivalence before any installation claim.

## Hash Verification
- Confirm source build hash and deployed file hash match for:
  - `E:\Games\SPT\BepInEx\plugins\BallisticPenetration\BallisticPenetration.dll`

## Completion Conditions
- [ ] Memory files complete and committed.
- [ ] Runtime lifecycle diagnostics plan logged and bounded.
- [ ] No gameplay behavior edits.

## Progress Notes and Decisions
- [x] Project-memory bootstrap requested and completed in this step.
- [x] Logged terminal-state fields (`ballisticTerminal`, `lifecycleTerminal`) on lifecycle diagnostics events without gameplay changes.
