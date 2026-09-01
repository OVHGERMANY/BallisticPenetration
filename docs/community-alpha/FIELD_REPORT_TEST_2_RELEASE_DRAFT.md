# BallisticPenetration 1.3.0 - Field Report Test 2

- Tag: `v1.3.0-field-report-test.2`
- Target: `<release commit SHA>`
- GitHub state: prerelease
- Publication gate: keep unpublished until the exact release DLL passes `TEST_2_ACCEPTANCE.md`
- Supported environment: exact SPT `4.1.3`
- Release DLL SHA-256: `<release DLL SHA-256>`
- Release ZIP SHA-256: `<release ZIP SHA-256>`

## Candidate contents

- The current physical-projectile, visual-eligibility, moving-surface anchoring, collision-telemetry, target-spall stability, and automatic field-report work.
- An install-ready DLL under `BepInEx/plugins/BallisticPenetration`.
- Local-only field-report instructions, the Test 2 acceptance procedure, and the fail-closed report verifier.
- No EFT, SPT, Unity, BepInEx, Harmony, or other third-party runtime assemblies.
- No field reports, profiles, configuration files, or installed-game data.

## Offline evidence required before publication

- Release build against exact SPT 4.1.3 with warnings treated as errors: `0 warnings, 0 errors`.
- Complete validation against the installed 4.1.3 `items.json`: `68 passed, 0 failed`.
- Package entry and checksum audit passes.

## Runtime evidence still required

- One completed report from the exact release DLL and SPT 4.1.3.
- One observed and one resolved record for every collision identity.
- No `numeric-runaway`, runtime-error, terminal-missing, or terminal-duplicate records.
- No recorder drops, suppression, errors, or truncation.
- Target-spall telemetry remains present.
- The actor, world-surface, moving-door, and target-spall visual checks are all observed and confirmed.
- `Test-Test2FieldReport.ps1` returns `PASS` when given the final DLL SHA-256 explicitly.

This draft does not authorize deployment, tagging, pushing, repository visibility changes, or publication.
