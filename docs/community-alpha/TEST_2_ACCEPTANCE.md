# Test 2 acceptance for SPT 4.1.3

GitHub Test 2 is blocked until one complete report from the exact installed SPT 4.1.3 DLL passes the automated telemetry gate and the same run passes four visual checks. Older SPT 4.1.2 reports are historical evidence only.

## Before the run

1. Confirm SPT Server, SPT Launcher, and EFT are stopped before replacing a DLL.
2. Confirm the installed `BallisticPenetration.dll` SHA-256 is `EBFA1B58A8770D973C43D957C7D9FEC3BFF4C05505653106D7B3814EA41CBDF3`.
3. Keep field reports enabled. Do not send an entire game directory or profile.
4. Start exact official SPT 4.1.3 and use a disposable or backed-up profile.

## One-run smoke test

- Actor: fire into PMC armor and helmet plus corpse head and torso. Independent bullet, fragment, or target-material chip meshes must not clip through or remain on the actor. Ordinary wound and armor marks must remain.
- World surface: fire into a rigid wall or prop. Eligible projectile-origin embedded geometry must still appear.
- Moving door: fire into a movable door, then open and close it. The embedded mesh must remain attached at its original local point and orientation.
- Target spall: produce a target-spall event. Simulation and telemetry must remain present, while standalone target-spall meshes remain absent.
- Finish the raid or close normally so the report ends with `session-end` rather than remaining partial.

## Automated gate

Run the verifier against the newest completed `.bpreport` and assert the manual checks only if you observed them:

```powershell
& .\tools\Test-Test2FieldReport.ps1 `
    -Path 'E:\Games\SPT\BepInEx\FieldReports\BallisticPenetration\<new-report>.bpreport' `
    -ActorVisualConfirmed `
    -WorldSurfaceVisualConfirmed `
    -MovingDoorVisualConfirmed `
    -TargetSpallVisualConfirmed
```

PASS requires exact SPT 4.1.3 and DLL identity, one observed and one resolved record per collision identity, complete binding-context fields, target-spall telemetry, zero numeric-runaway/runtime-error/terminal-missing/terminal-duplicate records, zero recorder drops/errors/suppression, an untruncated completed report, and all four manual confirmations.

Preserve the passing report, its SHA-256 from the verifier output, and screenshots of the four visual checks. Do not publish Test 2 if the verifier returns FAIL.

