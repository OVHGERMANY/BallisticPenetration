# Janky-BallisticPenetration

Standalone SPT 4.1.2 client plugin for uncapped, impact-speed-based terminal ballistics.

At startup, the plugin reads the already-loaded `com.SPT.core` metadata from BepInEx
and requires its version to equal `4.1.2` exactly before resolving targets or enabling
either Harmony patch. The hard `BepInDependency` remains for load ordering and minimum
dependency handling, but it does not replace the equality check. A missing version or
any other version is logged and initialization fails closed.

## What it changes

For every valid forward `EFT.Ballistics.Shot` impact, the plugin derives the speed
fraction from the velocity at the exact hit point and the ammunition template's base
`InitialSpeed`:

```text
fraction = impactSpeed / ammoTemplateInitialSpeed
penetrationFactor = fraction ^ PenetrationExponent
damageFactor = fraction ^ DamageExponent
```

The defaults are:

```text
Penetration Exponent = 1.4
Damage Exponent = 0.4
```

Neither factor is capped and the fraction is not upper-clamped. A round at zero valid
impact speed receives zero damage and penetration factors. On invalid input, this
plugin writes neither stat rather than writing a partial or non-finite result.

The plugin intentionally uses `AmmoTemplate.InitialSpeed`, not `Shot.InitialSpeed`.
`Shot.InitialSpeed` includes weapon velocity modifiers and is therefore not a stable
reference for the same ammunition across different barrel configurations.

## Collision timing

The plugin does not use a `HandleCollision` postfix. Tarkov calls `CreateFragments()`
inside `HandleCollision`, and that method performs armor status, penetration, ricochet,
fragment, and child-shot decisions before a postfix would run.

Instead it:

1. snapshots the damage, penetration, and template speed seen by its Priority.Last
   `HandleCollision` prefix before the original method body runs;
2. waits for Tarkov to interpolate `_currentVelocity` to the actual collision point;
3. replaces damage and penetration in a `CreateFragments` prefix immediately before
   Tarkov makes its terminal-ballistics decisions.

The saved values are removed after use, including when Tarkov throws during a collision.
Each new collision starts from the previous corrected values, so falloff remains
cumulative across multiple surfaces and child shots.

## Scope

The plugin does not modify flight drag, trajectory integration, ricochet angle/RNG,
armor resistance code, durability code, or visual effects.

Armor block and penetration outcomes can change because the existing armor calculation
reads the corrected `PenetrationPower`. That is the intended gameplay effect. The
separate armor-CF child-shot degradation remains unpatched.

## Configuration

After the game starts once, BepInEx creates:

```text
<SPT_ROOT>\BepInEx\config\com.janky.ballisticpenetration.cfg
```

Available settings:

- `General / Enabled` (default `true`)
- `Falloff / Penetration Exponent` (default `1.4`, range `0.1` through `4.0`)
- `Falloff / Damage Exponent` (default `0.4`, range `0.1` through `4.0`)
- `Diagnostics / Log Adjustments` (default `false`)
- `Diagnostics / Enable In-Game Diagnostics` (default `false`)
- `Diagnostics / Show Latest Adjustment Overlay` (default `true`)
- `Diagnostics / Show World-Space Trace And Impact Marker` (default `true`)
- `Diagnostics / Overlay Lifetime Seconds` (default `6`)
- `Diagnostics / Trace Lifetime Seconds` (default `2`)
- `Diagnostics / Maximum Trace Segment Meters` (default `30`)
- `Diagnostics / Impact Marker Size Meters` (default `0.15`)

The live overlay shows the values at three points in this plugin:

- `ENTRY`: damage and penetration when its `HandleCollision` prefix runs.
- `BP INPUT`: values received by its `CreateFragments` prefix after Tarkov's normal
  collision degradation.
- `BP OUTPUT`: values written by BallisticPenetration when the adjustment is applied.

For a skipped hit, the overlay says `BP OUTPUT NOT WRITTEN` and gives the reason. Another
Harmony patch can still change the values after this plugin runs. Live diagnostics are
disabled by default.

## Build and validate

The plugin targets `netstandard2.1`. The validation runner targets `net8.0` and links
the calculator and SPT version check from `Core`; it does not load BepInEx, Unity, or
Tarkov.

```powershell
$env:SPT_ROOT = 'C:\SPT'
dotnet build .\BallisticPenetration.sln -c Release "-p:SptRoot=$env:SPT_ROOT"
dotnet run --project .\tests\BallisticPenetration.Validation\BallisticPenetration.Validation.csproj -c Release -- "$env:SPT_ROOT\SPT_Runtime\SPT_Data\database\templates\items.json"
```

The validation suite checks the SNB regression rows, weapon-independence at a fixed
impact speed, uncapped factors above one, zero and invalid-input handling, cumulative
falloff, 5.45x39 US, all current local ballistic item templates, and exact acceptance
of `com.SPT.core` version `4.1.2` while rejecting missing, lower, higher, or four-part
versions.

The game-facing project has compile-only references to BepInEx, Harmony, SPT reflection,
Assembly-CSharp, UnityEngine.CoreModule, and UnityEngine. `UnityEngine.dll` is required
solely because BepInEx `BaseUnityPlugin` exposes `MonoBehaviour` from that assembly.
All game references use `Private=false`; none is copied to deployment.

## Explicit deployment

A normal build never copies files into SPT. Deploy only after validation passes:

```powershell
dotnet msbuild .\src\BallisticPenetration\BallisticPenetration.csproj -t:Deploy -p:Configuration=Release "-p:SptRoot=$env:SPT_ROOT" "-p:DeployRoot=$env:SPT_ROOT"
```

This copies only `BallisticPenetration.dll` to:

```text
<SPT_ROOT>\BepInEx\plugins\BallisticPenetration\BallisticPenetration.dll
```

Restart SPT after deployment so BepInEx loads the replaced assembly.
