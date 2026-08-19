# PROJECT_STATE

## Repository
- BallisticPenetration: `C:\Users\jbnel\Projects\BallisticPenetration`
- Purpose: Ballistic terminal modifier and diagnostics pipeline for BallisticsLab-enabled physical projectile flow.

## Runtime Branch / Commit
- Branch: `development/physical-projectile-system`
- HEAD: `2b216c1`

## Active Objective
- Diagnostics-only per-collision and exactly-once lifecycle-terminal verification.
- Gameplay physics must remain unchanged.

## Fixed Constraints
- No gameplay behavior changes in this milestone.
- Changes are constrained to diagnostics/verification plumbing.
- No changes to projectile kinematics, damage, penetration, fragments, or collision outcomes unless task explicitly allows.

## Known Unresolved Defects
- A projectile can cross multiple layers at the same recorded velocity while penetration and damage compound (diagnostics investigation target).

## Files Currently Involved
- BallisticPenetration diagnostics runtime and validation files selected by the lifecycle diagnostics scope.
- `docs/PROJECT_STATE.md`
- `docs/exec-plans/active/lifecycle-diagnostics.md`
- `docs/DECISIONS.md`
- `AGENTS.md`

## Latest Completed Work
- Baseline gameplay logic freeze established.
- Diagnostics branch anchored at `2b216c1` with prior lifecycle work ready for follow-up.
- Updated terminal telemetry schema on physical projectile lifecycle diagnostics:
  - `ballisticTerminal` and `lifecycleTerminal` are Boolean (`true|false`) fields.
  - Added `lifecycleEndReason` (`none|stopped|replaced|aborted`) on all physical projectile lifecycle events.
- Validation passpoint checkpoint:
  - `dotnet build ... -c Release -p:SptRoot="E:\\Games\\SPT" -p:TreatWarningsAsErrors=true` succeeded (0 warnings, 0 errors).
  - `dotnet run --project ...BallisticPenetration.Validation...` completed with `46 passed, 0 failed`.
- Gameplay behavior unchanged; diagnostics-only modifications.

## Latest Build and Validation
- BallisticPenetration build: succeeded with 0 warnings and 0 errors.
- BallisticPenetration validation: `46 passed, 0 failed`.
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
- Next milestone: collision-observed/collision-resolved correlation and deduplication.
