# SPT 4.1.4 local compatibility preview

## Finalization follow-up

The user subsequently stopped further shooting rounds and requested completion/release. Final version `1.3.1` preserves the tested preview implementation and exact compatibility guard. Release build, 68 checks and the final compiled compatibility audit pass. The reproducible package and source/installed hashes are in `docs/PROJECT_STATE.md`; unresolved runtime cases remain explicit in `docs/SPT-4.1.4-release.md`. The original no-publication boundary below describes the earlier acceptance-only phase, not this subsequent release request.

## Scope

The user approved updating the installed mods. Work from published v1.3.0 only, preserve the separate dirty development worktree, and retain exact-version rejection plus transactional patch initialization. No trajectory, damage, penetration, fragmentation or collision-policy change is authorized by this port.

## Gates

1. Verify the installed official 4.1.4 game assembly and the exact patch method contracts, including Harmony argument names, against the backed-up 4.1.3 assembly.
2. Use numeric BepInEx version 1.3.1 and explicit informational/startup identity 1.3.1-preview.1. Update the exact supported version to 4.1.4; explicitly reject 4.1.3, 4.1.5 and four-component 4.1.4.0.
3. Build Release with warnings as errors; run the full validation catalog against installed items.json. Compare compiled method IL with the published installed baseline, allowing only compatibility and identity changes.
4. After the user closes Tarkov, back up and hash-check both client DLLs; replace only the verified targets atomically with automatic rollback on failure. Preserve all configuration and profile data.
5. Confirm fresh startup logs for both companion previews, then resume user-led HollywoodFX visual and second-raid acceptance. Static checks are not runtime acceptance.

## Initial evidence

- Published base: 9d851a07ed0c3447a1e95a78188360015b62bbbc.
- Installed game SHA-256: EE25CEE1259777B38ED8B3E7841FDC2DB3C98540B1469FA539B1FF183476E436.
- Unchanged baseline: clean Release build; 68 validation groups passed.
- Candidate: clean Release rebuild, 68 validation groups passed. No gameplay-method change found in the 1,534-body comparison; four methods differ only in compatibility/identity strings. All 226 runtime references resolve.
- Deployed with the client closed at 2026-09-05 11:30:06 -05:00. Source, staged candidate and installed SHA-256 match `CFCFF0A998BA4A0AA66545F6353D7AC5307CB0DC0916207192582405D9B98AFF` (311,296 bytes).
- Backup and apply manifest: `E:\Games\SPT-Mod-Backups\20260905-112852-spt414-client-compatibility`. The apply transaction replaced two companion DLLs and verified 22 other config/plugin files unchanged. All gameplay/profile files were left alone.
- Audit tool and generated evidence: `C:\Users\jbnel\Projects\HollywoodFX\artifacts\runtime-test-20260905-111242\compatibility`. The first audit run had three incorrect expectations for unused parameter names; observed game metadata corrected them. All 11 final contracts pass. An intermediate candidate added a constant before compiler-generated closures, renumbering their names; moving the constant after the methods retained the baseline identities and eliminated that audit noise.
- Fresh client startup passed: PID 2816 began at 11:30:56 -05:00; client log lines 50-55 verify exact 4.1.4, all four patches enabled and the preview identity. BL and HFX also loaded, with zero client error lines at 11:32:49. User-led raid acceptance remains pending. The launcher was safely reopened on the existing profile; the existing server was retained.
- No stable merge, tag, push or publication is part of this acceptance task.
