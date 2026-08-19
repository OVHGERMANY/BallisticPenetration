# Lifecycle Diagnostics - Active Plan

## Requirements
- [x] Track and log `ballisticTerminal` and `lifecycleTerminal` states for each observed physical projectile event.
- [x] Track `continued` and `replaced` flags with explicit semantics:
  - Stopped resolved event: `continued=false`, `replaced=false`, `ballisticTerminal=true`, `lifecycleTerminal=false`.
  - Continued same projectile: `continued=true`, `replaced=false`, `ballisticTerminal=false`, `lifecycleTerminal=false`.
  - Continued via replacement: original gets `continued=true`, `replaced=true`, `ballisticTerminal=false`, `lifecycleTerminal=true`; replacement must get new lifecycle identity.
  - Runtime/runtime-abort retirement: `lifecycleTerminal=true`, `ballisticTerminal=false`, `lifecycleEndReason=aborted`.
- [x] Emit collision phase records: `event=collision-observed` then `event=collision-resolved`.
- [x] Deduplicate by `(projectileIdentity, collisionIdentity, phase)` so each phase is emitted once.
- [x] Keep both `recordSequence` and derived per-lifecycle `collisionOrdinal`.
- [x] Add bounded trackers with lifecycle tombstones and duplicate-terminal detection.
- [x] Emit `terminal-missing` for terminal-lifecycle gaps and `terminal-duplicate` on repeats.
- [x] Add explicit `shutdown-cleanup` marker to exclude expected unload cleanup from missing-terminal defects.
- [x] Keep gameplay behavior untouched.

## Files Expected to Change
- `src/BallisticPenetration/Core/Diagnostics/PhysicalProjectileLifecycleTracker.cs`
- `src/BallisticPenetration/Runtime/Diagnostics/PhysicalProjectileLifecycleDiagnostics.cs`
- `src/BallisticPenetration/Runtime/State/PhysicalShotBinding.cs`
- `src/BallisticPenetration/Runtime/Plugin.cs`
- `tests/BallisticPenetration.Validation/Program.cs`
- `docs/PROJECT_STATE.md`
- `docs/exec-plans/active/lifecycle-diagnostics.md`

## Prohibited Gameplay Changes
- No changes to trajectory, velocity, penetration, damage, fragment behavior, collision outcomes, or other core gameplay systems.

## Validation Commands
- `dotnet build BallisticPenetration.sln -c Release -p:SptRoot="E:\Games\SPT" -p:TreatWarningsAsErrors=true`
- BallisticPenetration strict validation test suite (all diagnostics tests).

## Latest Validation and Checkpoint
- Build result: `dotnet build ... -p:TreatWarningsAsErrors=true` succeeded, 0 warnings, 0 errors.
- Validation result: `dotnet run --project BallisticPenetration.Validation...` => 48 passed, 0 failed.
- Gameplay behavior: diagnostics-only lifecycle state and unload wiring; no trajectory,
  velocity, penetration, damage, fragments, collision outcomes, or gameplay branch logic modified.
- Tombstones: fixed capacity `1024`, deterministic oldest-first eviction.
- Next action: lifecycle diagnostics plan complete; the known multilayer velocity defect is
  the next incomplete project milestone and was not started in this session.

## Installation Steps
- Build release DLL.
- Deploy only if approved by current objective.
- Verify runtime hash equivalence before any installation claim.

## Hash Verification
- Confirm source build hash and deployed file hash match for:
  - `E:\Games\SPT\BepInEx\plugins\BallisticPenetration\BallisticPenetration.dll`

## Completion Conditions
- [x] Project state and active execution plan updated.
- [x] Runtime lifecycle diagnostics plan logged and bounded.
- [x] No gameplay behavior edits.

## Progress Notes and Decisions
- [x] Project-memory bootstrap requested and completed in this step.
- [x] Logged terminal-state fields (`ballisticTerminal`, `lifecycleTerminal`) on lifecycle diagnostics events without gameplay changes.
- [x] Preserved `collision-observed`/`collision-resolved` correlation and phase deduplication from `611e8a7`.
- [x] Added one-shot canonical terminal closure for `stopped`, `replaced`, and `aborted`.
- [x] Added bounded duplicate-terminal and missing-terminal invariant reporting.
- [x] Added expected-shutdown terminal cleanup and summary reporting before state clear.
- [x] Added explicit validation assertions for all 18 required lifecycle behaviors.
