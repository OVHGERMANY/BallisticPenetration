# Automatic Field Report Recorder - Active Plan

## Status

- [x] Implementation complete.
- [x] Release build succeeds with warnings treated as errors.
- [x] Validation succeeds: `61 passed, 0 failed`.
- [x] Deployment and SHA-256 verification complete.
- [ ] Runtime acceptance: launch SPT and perform a field-report smoke test.

## Objective

Produce one automatic, self-contained, append-only JSONL `.bpreport` per game process. The recorder is observational only, requires no tester notes, performs no upload, and must never alter or interrupt projectile processing.

## Runtime Contract

- Report directory: `BepInEx\FieldReports\BallisticPenetration`, derived from the current BepInEx root.
- Active filename: `<timestamp>-<session-id>.partial.bpreport`.
- Completed filename: `<timestamp>-<session-id>.bpreport`; `-marked` is added when an issue marker was written.
- Schema version: `1` on every record.
- Queue capacity: `4096` records.
- Producer behavior: bounded, thread-safe, and free of file I/O.
- Ordinary overflow: deterministically drop the newly submitted ordinary record.
- Critical overflow: evict the oldest queued ordinary record when one exists; otherwise count the critical record as dropped.
- Normal flush: approximately every configured interval, default `1` second.
- Prompt flush request: issue markers, critical errors/invariants, and shutdown.
- Size limit: default `256 MiB`, with a bounded critical-event reserve.
- Retention: default `20` completed reports and `512 MiB`, oldest owned completed report first.
- Crash recovery: stale partial files are renamed deterministically with `recovered-crash-`; contents are preserved.

## Recorded Evidence

- Session identity, UTC/local timestamps and offset.
- Running DLL version, informational version, SHA-256, and length.
- BepInEx, SPT, game, runtime, and operating-system versions.
- Loaded plugin names/versions, relevant configuration, diagnostics state, and recorder configuration.
- HollywoodFX DLL SHA-256 and length when the expected DLL exists.
- Typed immutable physical-projectile lifecycle snapshots shared by the existing human-readable logger and the field recorder.
- Creation, collision observation/resolution, retirement, terminal invariant, shutdown cleanup, issue marker, runtime error, truncation, and session-end evidence.
- Projectile/root/collision correlation, velocities, material/outcome state, lifecycle terminal semantics, target context, and safe shot context when available.

## Privacy Contract

- No username, computer name, IP address, credential, chat text, real name, or full personal filesystem path.
- Running files are identified by filename, length, and hash.
- Shooter/profile identities are represented by per-session salted aliases.
- Missing optional context is null or omitted; values are not guessed.

## Validation Coverage

- [x] Session-start is first and valid JSON.
- [x] Lifecycle correlation fields survive serialization.
- [x] Report sequences increase monotonically.
- [x] Concurrent submissions remain parseable JSONL.
- [x] Issue markers are written and promptly flushed.
- [x] Normal shutdown writes session-end and finalizes the filename.
- [x] Stale partial reports are preserved and recovered.
- [x] Recorder initialization/write failures do not escape callers.
- [x] Disabled recording creates no report directory or file.
- [x] Queue overflow is bounded, counted, and reported.
- [x] Retention deletes only oldest owned completed reports.
- [x] Active partial reports are excluded from retention deletion.
- [x] Size limiting writes a truncation record when space permits.
- [x] Critical records remain eligible after truncation.
- [x] Report content omits the report directory and private path token.
- [x] Every nonempty report line parses as one JSON object.
- [x] All prior lifecycle and physics validation groups remain passing.
- [x] Final source diff contains no projectile gameplay calculation or branch change.

## Files Added

- `src/BallisticPenetration/Core/Diagnostics/FieldReportRecord.cs`
- `src/BallisticPenetration/Core/Diagnostics/FieldReportLifecycleEventSnapshot.cs`
- `src/BallisticPenetration/Core/Diagnostics/FieldReportRecorder.cs`
- `src/BallisticPenetration/Runtime/Diagnostics/FieldReportRuntime.cs`
- `docs/FIELD_REPORTS.md`
- `docs/exec-plans/active/field-report-recorder.md`

## Files Updated

- `src/BallisticPenetration/Runtime/Diagnostics/PhysicalProjectileLifecycleDiagnostics.cs`
- `src/BallisticPenetration/Runtime/PluginConfiguration.cs`
- `src/BallisticPenetration/Runtime/Plugin.cs`
- `tests/BallisticPenetration.Validation/Program.cs`
- `docs/PROJECT_STATE.md`

## Prohibited Changes

- No projectile velocity, trajectory, drag, penetration, damage, fragmentation, collision outcome, target, armor, gore, or visual behavior change.
- No networking or automatic upload.
- No HollywoodFX edit.
- No additional DLL deployment beyond the verified field-report installation recorded below.

## Verified Deployment

- Deployed source commit: `fee5e469439ac1616daccf36fa8e95df74a791c2`.
- Deployment record timestamp: 2026-08-20 15:26:13 CDT (verified backup creation timestamp).
- Release plugin DLL: `C:\Users\jbnel\Projects\BallisticPenetration\src\BallisticPenetration\bin\Release\netstandard2.1\BallisticPenetration.dll`.
- Installed DLL: `E:\Games\SPT\BepInEx\plugins\BallisticPenetration\BallisticPenetration.dll`.
- Previous installed artifact: SHA-256 `E27865D7CDF414B8416463451C5F118386CA065D2D2792DB39947417B16E8249`, length `254976` bytes.
- Backup: `E:\Games\SPT\BepInEx\plugins\BallisticPenetration\backup\BallisticPenetration.dll.20260820-151615-pre-field-report.bak`, SHA-256 `E27865D7CDF414B8416463451C5F118386CA065D2D2792DB39947417B16E8249`.
- Release and installed artifact: SHA-256 `522B50D701BE9DB47B8300D19E136BCF4B903098D6BFB6DF72BAC43FCA766A92`, length `295424` bytes each.
- Build result: Release succeeded with 0 warnings and 0 errors.
- Validation result: `61 passed, 0 failed`.
- Expected field-report directory: `E:\Games\SPT\BepInEx\FieldReports\BallisticPenetration` (created when the plugin loads).
- Default issue-marker key: `F8`.
- The known multilayer velocity defect remains unresolved. No projectile physics changes are authorized.

## Exact Next Action

Launch SPT and perform a field-report smoke test.
