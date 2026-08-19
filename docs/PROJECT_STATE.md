# PROJECT_STATE

## Repository
- BallisticPenetration: `C:\Users\jbnel\Projects\BallisticPenetration`
- Purpose: Ballistic terminal modifier and diagnostics pipeline for BallisticsLab-enabled physical projectile flow.

## Runtime Branch / Commit
- Branch: `development/physical-projectile-system`
- Milestone base HEAD: `611e8a7`
- Checkpoint: `Add bounded terminal lifecycle diagnostics`

## Active Objective
- Bounded physical lifecycle-terminal invariant diagnostics, including duplicate-terminal,
  missing-terminal, and expected-shutdown cleanup records.
- Gameplay physics must remain unchanged.

## Fixed Constraints
- No gameplay behavior changes in this milestone.
- Changes are constrained to diagnostics/verification plumbing.
- No changes to projectile kinematics, damage, penetration, fragments, or collision outcomes unless task explicitly allows.

## Known Unresolved Defects
- A projectile can cross multiple layers at the same recorded velocity while penetration and damage compound (diagnostics investigation target).

## Files Currently Involved
- `src/BallisticPenetration/Core/Diagnostics/PhysicalProjectileLifecycleTracker.cs`
- `src/BallisticPenetration/Runtime/Diagnostics/PhysicalProjectileLifecycleDiagnostics.cs`
- `src/BallisticPenetration/Runtime/State/PhysicalShotBinding.cs`
- `src/BallisticPenetration/Runtime/Plugin.cs`
- `tests/BallisticPenetration.Validation/Program.cs`
- `docs/PROJECT_STATE.md`
- `docs/exec-plans/active/lifecycle-diagnostics.md`

## Latest Completed Work
- Baseline gameplay logic freeze established.
- Diagnostics branch anchored at `2b216c1` with prior lifecycle work ready for follow-up.
- Updated terminal telemetry schema on physical projectile lifecycle diagnostics:
  - `ballisticTerminal` and `lifecycleTerminal` are Boolean (`true|false`) fields.
  - Added `lifecycleEndReason` (`none|stopped|replaced|aborted`) on all physical projectile lifecycle events.
- Completed observed/resolved collision correlation and per-projectile dedupe in diagnostics with explicit duplicate suppression:
  - `collision-observed` and `collision-resolved` share stable `collisionIdentity` and per-payload dedupe.
  - Stopped outcomes emit resolved lifecycle exactly once with `continued=false` and `replaced=false`.
  - Stopped resolved lifecycle now correctly reports `lifecycleTerminal=false`.
- Added an active-only physical lifecycle tracker and a fixed-capacity terminal tombstone
  tracker with deterministic oldest-first eviction.
- Terminal tombstone capacity is `1024`.
- Canonical `stopped`, `replaced`, and `aborted` retirement removes the active identity,
  records one tombstone, and suppresses later ordinary retirement records for that identity.
- A repeated terminal attempt inside retained capacity emits `event=terminal-duplicate`
  with first and attempted reasons and timestamps.
- Physical binding removal without a canonical terminal emits `event=terminal-missing`,
  closes the active diagnostics entry, and records a missing-terminal tombstone without
  assigning a normal lifecycle end reason.
- Expected plugin destruction emits one `shutdown-cleanup` retirement per active identity,
  then `event=shutdown-cleanup-summary`, then clears active lifecycle state, collision
  dedupe state, tombstones, and violation counters.
- Replacement identities remain independent; a retired retained identity cannot be
  registered again as a fresh lifecycle.
- Validation passpoint checkpoint:
  - `dotnet build ... -c Release -p:SptRoot="E:\\Games\\SPT" -p:TreatWarningsAsErrors=true` succeeded (0 warnings, 0 errors).
  - `dotnet run --project ...BallisticPenetration.Validation...` completed with `48 passed, 0 failed`.
- Gameplay behavior unchanged; diagnostics-only modifications.

## Latest Build and Validation
- BallisticPenetration build: succeeded with 0 warnings and 0 errors.
- BallisticPenetration validation: `48 passed, 0 failed`.
- No gameplay kinematic/impact branches changed in this step.

## Deployment Paths and Hashes
- BallisticPenetration install path: `E:\Games\SPT\BepInEx\plugins\BallisticPenetration\BallisticPenetration.dll`
- Runtime log path: `E:\Games\SPT\BepInEx\LogOutput.log`
- Current hash state to be filled after verified install.

## HollywoodFX Context
- Repository: `C:\Users\jbnel\Projects\HollywoodFX`
- Branch: `feature/surface-impact-marks`
- Confirmed gore-gate commit: `7f3fc3e`
- Last confirmed validation: `11/11 passed`
- Player/Corpse ownership is an identity gate, not sufficient by itself to produce gore.
- Stopped armor and helmet impacts must remain bloodless.
- Install target: `E:\Games\SPT\BepInEx\plugins\HollywoodFX\HollywoodFX.dll`

## Exact Next Action
- Combined in-game acceptance testing is the next action after this verified deployment.

## Verified Lifecycle Diagnostics Deployment
- Deployment date/time: 2026-08-19 09:07:26 CDT.
- Deployed source commit: `2b69e785f05ff21340453776be33b97e426561dd`.
- Release DLL source: `C:\Users\jbnel\Projects\BallisticPenetration\src\BallisticPenetration\bin\Release\netstandard2.1\BallisticPenetration.dll`.
- Installed DLL destination: `E:\Games\SPT\BepInEx\plugins\BallisticPenetration\BallisticPenetration.dll`.
- Prior installed SHA-256: `43D17BF2B509FC731CF8745B7FF0CD1E32C7E8C983CE68C689B0147191256F90`.
- Prior installed length: `233984` bytes.
- Backup: `E:\Games\SPT\BepInEx\plugins\BallisticPenetration\backup\BallisticPenetration.dll.20260819-090726-pre-lifecycle.bak`.
- Release SHA-256: `E27865D7CDF414B8416463451C5F118386CA065D2D2792DB39947417B16E8249`.
- Installed SHA-256: `E27865D7CDF414B8416463451C5F118386CA065D2D2792DB39947417B16E8249`.
- Release and installed lengths: `254976` bytes each; hashes and lengths match.
- Build: Release succeeded with 0 warnings and 0 errors using `dotnet build C:\Users\jbnel\Projects\BallisticPenetration\BallisticPenetration.sln --configuration Release -p:SptRoot=E:\Games\SPT -p:TreatWarningsAsErrors=true`.
- Validation: `48 passed, 0 failed`.
- Diagnostics strings verified in the Release assembly: `collision-observed`, `collision-resolved`, `terminal-duplicate`, `terminal-missing`, `shutdown-cleanup`, `shutdown-cleanup-summary`, `lifecycleTerminal`, and `lifecycleEndReason`.
- Runtime log: `E:\Games\SPT\BepInEx\LogOutput.log`.
- HollywoodFX is already deployed and hash-verified separately.
- The multilayer velocity defect remains unresolved; no physics changes are authorized yet.
- Gameplay behavior changed: no.
