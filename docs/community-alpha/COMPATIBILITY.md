# Compatibility

## Supported environment

- SPT 4.1.2 exactly.
- EFT 0.16.9.40743.
- Windows and `EscapeFromTarkov.exe`.
- BepInEx and the SPT runtime bundled with that SPT release.

The plugin owns four Harmony targets: `Shot.HandleCollision`, `Shot.CreateFragments`, `BodyPartCollider.ApplyHit`, and `ArmorPlateCollider.ApplyHit`. It warns when another owner patches one of those methods. A warning does not prove a conflict, but it must be included in a compatibility report.

No optional testing tool is required for normal gameplay. No third-party visual package is required. The release contains no EFT, Unity, Harmony, BepInEx, or SPT runtime assembly.
