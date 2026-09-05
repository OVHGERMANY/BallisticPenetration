# BallisticPenetration 1.3.1 — SPT 4.1.4 compatibility

For official SPT 4.1.4 / EFT 0.16.9.5.40743 only. SPT 4.1.3, 4.1.5 and other versions remain rejected by the exact-version guard.

This release ports the published 1.3.0 baseline. Compatibility and release identity change; trajectory, velocity, penetration, damage, fragmentation, collision handling, defaults and report schemas do not. Separate unpublished development work is not included. Existing known issues are not represented as fixed by this version update.

The preview loaded all four patches alongside BallisticsLab and HollywoodFX in a Factory-day raid on September 5, 2026, with no client error entries. That verifies combined startup and use, not the correctness of every physical-projectile outcome. The user closed further acceptance rounds. Controlled multilayer/actor/fixture and profiler testing was not completed. Finalization changes identity/startup wording, not the implementation tested in that raid.

The compatibility audit checks 11 game method contracts shared by the two companions, resolves runtime references, and compares compiled method bodies with the published baselines. Portable validation has 68 checks; this is not a claim of in-game physics certification.

Close Tarkov, then extract `BallisticPenetration-1.3.1-SPT-4.1.4.zip` into the SPT root. Only the plugin DLL is installed. Preserve existing configuration; experimental physical projectiles remain disabled by default. Keep the previous DLL for rollback.
