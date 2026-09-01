# PROJECT_STATE

## Repository
- BallisticPenetration: `C:\Users\jbnel\Projects\BallisticPenetration`
- Purpose: Ballistic terminal modifier and diagnostics pipeline for BallisticsLab-enabled physical projectile flow.

## Runtime Branch / Commit
- Branch: `development/physical-projectile-system`
- Current HEAD: `802035a0fe1ca533ea7f62338276f95e6cde723b`
- Field-report milestone base HEAD: `d3eda0a`
- Pending uncommitted checkpoint: character-owned physical-component presentation gate and moving-surface embedded visual anchoring.

## Active Objective
- Keep physical projectile simulation intact while preventing independent component meshes from clipping through characters, armor, helmets, and corpses or reading as crude target-material chips.

## SPT 4.1.3 Compatibility Update - 2026-09-01
- Updated the exact `com.SPT.core` compatibility gate and user-facing compatibility text from SPT `4.1.2` to official SPT `4.1.3`; EFT remains `0.16.9.5.40743`.
- No projectile trajectory, velocity, penetration, damage, fragmentation, collision outcome, rendering policy, or other gameplay behavior changed for this compatibility update.
- Release build against `E:\Games\SPT` SPT `4.1.3-RELEASE+ddce41c` succeeded with `0 warnings, 0 errors`.
- Complete validation against the installed 4.1.3 catalog succeeded with `68 passed, 0 failed`.
- Deployed DLL length is `310784` bytes and source/installed SHA-256 is `EBFA1B58A8770D973C43D957C7D9FEC3BFF4C05505653106D7B3814EA41CBDF3`.
- Rollback backup is `E:\Games\SPT\BepInEx\plugins\BallisticPenetration\backup\BallisticPenetration.dll.20260901-175959-pre-spt413.bak`, SHA-256 `5FB25028B7A90F880E78AF5C1F25373C54E19998F5FE34F75B859BCCC3445C6E`.
- Runtime acceptance and GitHub Test 2 publication remain pending the existing collision-pair and `numeric-runaway` gates.

## User-Authorized Character Physical Visual Repair - 2026-08-28
- Live field-report evidence identified stopped `ProjectileFragment` and `DeformedProjectile` components on `BodyPartCollider/Jaw`, `BodyPartCollider/HeadCommon`, and `ArmorPlateCollider/SpineTop`, plus a 12-gauge deformed component whose actor collision was observed but never resolved before renderer cleanup.
- Collision preparation now snapshots whether the hit is actor-owned from `BodyPartCollider` (including `ArmorPlateCollider`) or a collider under `EFT.Player` or `EFT.Interactive.Corpse`.
- An existing in-flight component visual retires immediately when an actor collision is observed, so a missing outcome callback cannot leave the mesh stuck inside the actor. If normal outcome processing fails and the exact source binding remains active, the visual is restored in FIFO order.
- Stopped components on characters, corpses, armor, helmets, and other actor-owned equipment remain simulated and reported but do not register standalone embedded geometry.
- Target-material spall remains fully simulated and reported but no longer registers standalone live or embedded geometry. Projectile-origin bullets and fragments remain visible in flight and when embedded in world geometry or moving props.
- No trajectory, velocity, penetration, damage, fragmentation, collision outcome, organ, movement, armor, or gore behavior changed.
- Release build succeeds with `0 warnings, 0 errors`; complete validation succeeds with `68 passed, 0 failed`; `git diff --check` reports no whitespace errors (only existing LF-to-CRLF notices).
- Locally deployed at 2026-08-28 17:55:29 CDT with source/installed SHA-256 `5FB25028B7A90F880E78AF5C1F25373C54E19998F5FE34F75B859BCCC3445C6E`, length `310784` bytes each. SPT remained stopped.
- Runtime acceptance is pending the user-run actor/head/armor, world-surface, and moving-door smoke tests.

## User-Authorized Embedded Visual Repair - 2026-08-28
- Scope is rendering only: stopped projectile component meshes now retain the exact struck collider and snapshot their collider-local position and orientation before the visual command is queued.
- Moving non-static hit surfaces reconstruct the embedded mesh world pose from the captured local pose each frame.
- Static, collider-null, and uncapturable anchors retain the prior fixed world pose.
- Successfully captured anchors retire safely if the collider, transform, hierarchy, activation, or scene identity later becomes invalid.
- No trajectory, velocity, penetration, damage, fragmentation, collision outcome, target, armor, or gore behavior changed.
- This anchoring is included in the locally deployed visual-repair DLL. Runtime acceptance still requires firing into a movable door, opening and closing it, and confirming the embedded meshes remain attached at the original local points and orientations.

## Fixed Constraints
- No arbitrary speed clamp and no global checked-arithmetic disable.
- The only authorized physical change is the smallest source-proven host-integration stability correction; all other changes remain observational.
- No release publication. The user-authorized local deployment is recorded below and remains separate from runtime acceptance.

## Known Unresolved Defects
- A projectile can cross multiple layers at the same recorded velocity while penetration and damage compound (diagnostics investigation target).
- The field report retained `225` terminal-missing and `6` terminal-duplicate events; these remain visible and unresolved pending clean collision evidence.

## Files Currently Involved
- `src/BallisticPenetration/Core/Diagnostics/FieldReportRecord.cs`
- `src/BallisticPenetration/Core/Diagnostics/FieldReportLifecycleEventSnapshot.cs`
- `src/BallisticPenetration/Core/Diagnostics/FieldReportRecorder.cs`
- `src/BallisticPenetration/Runtime/Diagnostics/FieldReportRuntime.cs`
- `src/BallisticPenetration/Core/Diagnostics/PhysicalProjectileLifecycleTracker.cs`
- `src/BallisticPenetration/Runtime/Diagnostics/PhysicalProjectileLifecycleDiagnostics.cs`
- `src/BallisticPenetration/Core/Physics/PhysicalComponentVisualEligibility.cs`
- `src/BallisticPenetration/Core/Physics/PhysicalVisualAnchorGeometry.cs`
- `src/BallisticPenetration/Runtime/PhysicalProjectileRuntime.cs`
- `src/BallisticPenetration/Runtime/Rendering/PhysicalProjectileVisualRuntime.cs`
- `src/BallisticPenetration/Runtime/PluginConfiguration.cs`
- `src/BallisticPenetration/Runtime/Plugin.cs`
- `tests/BallisticPenetration.Validation/Program.cs`
- `docs/FIELD_REPORTS.md`
- `README.md`
- `docs/PROJECT_STATE.md`
- `docs/exec-plans/active/field-report-recorder.md`

## Latest Completed Work
- Replaced the runtime collision tuple set with the production `PhysicalCollisionEventDeduplicator`; its checked-build hash combination is explicitly `unchecked` without changing ordinal equality semantics.
- Added a `5000`-key production-path stress validation covering observed/resolved independence, per-projectile identity, and duplicate suppression.
- Added sanitized runtime-error detail with UTC/local timestamps, HRESULT, top method names, a stable fingerprint, power-of-two repetition aggregates, and final per-fingerprint totals.
- Added `shotBindingMatched` and `contextSource`; mismatched pooled shots now use the lifecycle tracker's last immutable snapshot or binding-creation state and never read current pooled-shot context.
- Proved a target-spall runaway path in EFT's fixed `10 ms` explicit-Euler trajectory integration: sufficiently small projected coefficients allow one drag step to reverse velocity and subsequent steps to compound magnitude.
- Added a derived physical-to-EFT coefficient floor of `0.000004440804472049359 * initialSpeedMetresPerSecond`, based on EFT's maximum G1 table value with a `0.01%` rounding margin. This prevents velocity reversal without clamping speed or changing assigned mass/energy.
- Added fail-open, once-per-projectile/stage `numeric-runaway` evidence before host-flight reconciliation accepts corrupted state.
- Added automatic per-process `.bpreport` JSONL recording under the current BepInEx installation.
- Added a fixed-capacity `4096`-record nonblocking producer queue with deterministic overflow accounting and a dedicated writer thread.
- Added one-second default flushing, prompt critical-event flush requests, bounded report size, and deterministic oldest-first owned-file retention.
- Added `F8` issue markers with debounce and recent projectile identities.
- Added stale partial crash recovery without deleting or rewriting report contents.
- Added session-start build/environment/configuration evidence and session-end lifecycle/queue/drop/truncation/error totals.
- Added typed immutable lifecycle snapshots shared by the existing human-readable lifecycle logger and the field recorder.
- Added safe shot/ammunition/weapon/target context when already available, with per-session shooter aliases and no raw profile names.
- Added focused filesystem, concurrency, overflow, truncation, recovery, privacy, finalization, and JSONL validation.
- Previous bounded lifecycle-diagnostics milestone is complete.
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
- Validation passpoint checkpoint for the previous lifecycle milestone:
  - `dotnet build ... -c Release -p:SptRoot="E:\\Games\\SPT" -p:TreatWarningsAsErrors=true` succeeded (0 warnings, 0 errors).
  - `dotnet run --project ...BallisticPenetration.Validation...` completed with `48 passed, 0 failed`.
- Gameplay behavior unchanged; diagnostics-only modifications.

## Latest Build and Validation
- BallisticPenetration build: succeeded with 0 warnings and 0 errors.
- BallisticPenetration validation: `68 passed, 0 failed`.
- Release build: `0 warnings, 0 errors` with warnings treated as errors.
- Current uncommitted visual work adds moving-surface anchoring, character/equipment presentation eligibility, target-spall mesh suppression, and actor-collision fallback restoration. The prior physical behavior change remains limited to low-coefficient child projections that would make EFT's fixed-step drag integration reverse velocity; no trajectory speed clamp, damage, penetration, fragmentation, collision-outcome, organ, movement, armor, or gore branch changed.

## Deployment Paths and Hashes
- BallisticPenetration install path: `E:\Games\SPT\BepInEx\plugins\BallisticPenetration\BallisticPenetration.dll`
- Runtime log path: `E:\Games\SPT\BepInEx\LogOutput.log`
- Character visual repair deployment verified on 2026-08-28 17:55:29 CDT:
  - Release DLL source: `C:\Users\jbnel\Projects\BallisticPenetration\src\BallisticPenetration\bin\Release\netstandard2.1\BallisticPenetration.dll`.
  - Installed DLL destination: `E:\Games\SPT\BepInEx\plugins\BallisticPenetration\BallisticPenetration.dll`.
  - Previous installed DLL: SHA-256 `C155E01E7B05A970FFED5D835162120565026FB643C575F0067DEF47F759A3EE`, length `309760` bytes.
  - Verified rollback backup: `E:\Games\SPT\BepInEx\plugins\BallisticPenetration\backup\BallisticPenetration.dll.20260828-175439-pre-character-visual-gate.bak`, SHA-256 `C155E01E7B05A970FFED5D835162120565026FB643C575F0067DEF47F759A3EE`, length `309760` bytes.
  - Release and installed DLL: SHA-256 `5FB25028B7A90F880E78AF5C1F25373C54E19998F5FE34F75B859BCCC3445C6E`, length `310784` bytes each.
  - Build: Release succeeded with `0 warnings, 0 errors` with warnings treated as errors.
  - Validation: `68 passed, 0 failed`.
  - SPT processes were absent before both copy operations and remained unstarted.
- Field-report recorder deployment verified on 2026-08-20 15:26:13 CDT (backup creation timestamp):
  - Deployed source commit: `fee5e469439ac1616daccf36fa8e95df74a791c2`.
  - Release DLL source: `C:\Users\jbnel\Projects\BallisticPenetration\src\BallisticPenetration\bin\Release\netstandard2.1\BallisticPenetration.dll`.
  - Installed DLL destination: `E:\Games\SPT\BepInEx\plugins\BallisticPenetration\BallisticPenetration.dll`.
  - Previous installed DLL: SHA-256 `E27865D7CDF414B8416463451C5F118386CA065D2D2792DB39947417B16E8249`, length `254976` bytes.
  - Verified backup: `E:\Games\SPT\BepInEx\plugins\BallisticPenetration\backup\BallisticPenetration.dll.20260820-151615-pre-field-report.bak`, SHA-256 `E27865D7CDF414B8416463451C5F118386CA065D2D2792DB39947417B16E8249`.
  - Release and installed DLL: SHA-256 `522B50D701BE9DB47B8300D19E136BCF4B903098D6BFB6DF72BAC43FCA766A92`, length `295424` bytes each.
  - Build: Release succeeded with 0 warnings and 0 errors.
  - Validation: `61 passed, 0 failed`.
  - Expected report directory: `E:\Games\SPT\BepInEx\FieldReports\BallisticPenetration`; it is derived from the active BepInEx root and will be created when the plugin loads.
  - Default issue-marker key: `F8`.
  - Field-report assembly evidence is present in the byte-identical Release/installed DLL.

## HollywoodFX Context
- Repository: `C:\Users\jbnel\Projects\HollywoodFX`
- Branch: `feature/surface-impact-marks`
- Confirmed gore-gate commit: `7f3fc3e`
- Last confirmed validation: `11/11 passed`
- Player/Corpse ownership is an identity gate, not sufficient by itself to produce gore.
- Stopped armor and helmet impacts must remain bloodless.
- Install target: `E:\Games\SPT\BepInEx\plugins\HollywoodFX\HollywoodFX.dll`

## Exact Next Action
- Run the user-led actor presentation smoke test: self/PMC armor and helmet impacts must not expose independent bullet or fragment meshes; corpse head and torso impacts must not show low-poly component chips; ordinary wound and armor marks must remain.
- Confirm a wall or rigid prop still shows eligible embedded projectile-origin geometry, and run the movable-door test by firing, opening, and closing the door to confirm the retained world mesh follows its original local pose.
- Confirm target-spall simulation and telemetry still occur without standalone target-spall meshes.
- Confirm collision observed/resolved pairs, runtime-error aggregation, binding context fields, and absence of `numeric-runaway` before considering GitHub Test 2 release publication.
- Use `tools\Test-Test2FieldReport.ps1` with the four explicit manual-confirmation switches. The verifier fails closed on the wrong SPT/DLL identity, incomplete collision pairs, missing binding context, numeric runaway, runtime/recorder errors, terminal anomalies, truncation/drops, absent target-spall telemetry, or an unconfirmed visual check. The exact run sequence is in `docs\community-alpha\TEST_2_ACCEPTANCE.md`.

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
