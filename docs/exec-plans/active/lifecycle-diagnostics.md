# Lifecycle Diagnostics - Completed Plan

Status: completed before the automatic field-report-recorder milestone. The known multilayer velocity defect remains unresolved.

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

## Verified Deployment Checkpoint
- Deployed source commit: `2b69e785f05ff21340453776be33b97e426561dd`.
- Release DLL source: `C:\Users\jbnel\Projects\BallisticPenetration\src\BallisticPenetration\bin\Release\netstandard2.1\BallisticPenetration.dll`.
- Installed DLL destination: `E:\Games\SPT\BepInEx\plugins\BallisticPenetration\BallisticPenetration.dll`.
- Prior installed SHA-256/length: `43D17BF2B509FC731CF8745B7FF0CD1E32C7E8C983CE68C689B0147191256F90` / `233984` bytes.
- Backup: `E:\Games\SPT\BepInEx\plugins\BallisticPenetration\backup\BallisticPenetration.dll.20260819-090726-pre-lifecycle.bak`.
- Release SHA-256/length: `E27865D7CDF414B8416463451C5F118386CA065D2D2792DB39947417B16E8249` / `254976` bytes.
- Installed SHA-256/length: `E27865D7CDF414B8416463451C5F118386CA065D2D2792DB39947417B16E8249` / `254976` bytes; hashes and lengths match.
- Deployment date/time: 2026-08-19 09:07:26 CDT.
- Build: Release succeeded with 0 warnings and 0 errors.
- Validation: `48 passed, 0 failed`.
- Diagnostics strings were verified in the Release assembly and the installed DLL is byte-identical to it.
- Runtime log: `E:\Games\SPT\BepInEx\LogOutput.log`.
- HollywoodFX is already deployed and hash-verified separately.
- Next action: combined in-game acceptance testing. Do not launch the game in this session.
- The multilayer velocity defect remains unresolved; no physics changes are authorized yet.
