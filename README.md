# Janky-BallisticPenetration

Standalone SPT 4.1.2 client plugin for uncapped, impact-speed-based terminal ballistics.

At startup, the plugin reads the already-loaded `com.SPT.core` metadata from BepInEx
and requires its version to equal `4.1.2` exactly before resolving targets or enabling
any of its four Harmony patches. The hard `BepInDependency` remains for load ordering
and minimum dependency handling, but it does not replace the equality check. A missing
version or any other version is logged and initialization fails closed.

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

## Postmortem armor durability

EFT stops its normal player-hit path once a wearer is dead, which also prevents worn
armor from losing durability when the corpse is shot. When `Damage Armor On Corpses`
is enabled, forward hits against a corpse are sent through EFT's existing
`ArmorComponent.ApplyDamage` durability path.

The feature uses the armor block, deflection, and penetration decisions already stored
on the hit. It does not roll those decisions again. A penetrating hit can damage every
matching armor layer; a blocked or deflected hit damages matching layers through its
identified armor, then stops before later layers. Only a local copy of the hit data is
processed, so no body damage, health event, kill event, or skill-progression callback
is added after death.

## Scope

The plugin does not modify flight drag, trajectory integration, ricochet angle/RNG,
armor resistance formulas, armor durability formulas, or visual effects. Postmortem
armor wear calls the existing EFT durability method without changing its calculations.

Armor block and penetration outcomes can change because the existing armor calculation
reads the corrected `PenetrationPower`. That is the intended gameplay effect. The
separate armor-CF child-shot degradation remains unpatched.

## Physical projectile development

The development tree now contains a dependency-free, versioned physical-state core for
intact projectiles, deformed projectiles, projectile fragments, and target-generated
spall. It records component-specific SI values for mass, geometry, projected area, drag,
position, velocity, momentum, kinetic energy, orientation, yaw, terminal state, lineage,
and collision history.

Projectile-derived mass and target spall are separate categories. Transition validation
rejects projectile-mass over-allocation, child energy above the residual collision budget,
mixed parent/root/collision lineage, duplicate component identities, non-finite state, and
fragmentation events with no physical projectile fragment. A fixed PCG random stream is
provided for later deformation and fragmentation calculations.

The development tree also contains a deterministic deformation and material-response
solver. It accepts construction-specific projectile properties, target resistance
properties, measured physical thickness, and actual material path length. For an outcome
already selected by the host ballistics system, it calculates target-resistance work,
projectile plastic deformation, fracture work, heat, residual energy and speed, diameter
expansion, projected area, yaw/tumble state, drag, and remaining damage and penetration
capability. It never rerolls the host penetration, ricochet, deviation, fragmentation, or
stop decision.

Every response must close projectile mass and translational energy. A continuing intact or
deformed component keeps its physical identity and immutable prior collision records. A confirmed
fragmentation response partitions its reserved projectile mass and energy into a retained primary
component and deterministic projectile fragments. Target-generated spall is constructed separately
from target mass and penetration-work energy, so it is never counted as projectile mass. Every
output has independent geometry, projected area, drag, direction, velocity, momentum, energy,
physical capability, source, lineage, history, and render state. A host-reported zero fragment
count remains observable and produces the minimum one physical projectile component needed to
close a nonzero fragmentation reservation.

The development tree also contains the checked boundary for individual flight. A pure projector
converts each physical component into EFT mass, equivalent diameter, velocity, relative G1 drag,
damage, and penetration while preserving an explicitly supplied EFT target/armor transfer
multiplier. A separate flight reconciler accepts EFT's measured position and velocity at the next
collision and advances energy-based physical capability without replacing EFT's trajectory
integrator.

Only synthetic test profiles are present today. No construction or target profile is yet
mapped to live ammunition, armor, bodies, or world materials, and target density is reserved
for the later spall calculation. Physical thickness is recorded separately from the supplied
effective material path; thickness alone does not secretly alter work unless the measured
path changes.

The physical-state, deformation, fragmentation, projection, and flight core is not attached to EFT
shots and therefore does not change live gameplay in version `1.2.0`. Assembly metadata and managed
IL verify the intended integration seam: an outer `Shot.CreateFragments` postfix runs after EFT
constructs its one real child list but before `BallisticsCalculator.UpdateShots` schedules those
children. Runtime state binding, collider-path measurement, transactional child replacement, and
trajectory reinitialization remain the next stage.

## Configuration

After the game starts once, BepInEx creates:

```text
<SPT_ROOT>\BepInEx\config\com.janky.ballisticpenetration.cfg
```

Available settings:

- `General / Enabled` (default `true`)
- `General / Damage Armor On Corpses` (default `true`)
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

The validation suite checks postmortem armor guards and layer traversal; physical state,
collision history, SI-derived values, immutable state-revision lineage, fail-open rejection,
projectile/spall separation, mass and energy conservation, deterministic random output,
material-profile validation, deformation and obliquity response, stopped-energy closure,
component-specific fragmentation and target-spall construction, physical-to-EFT projection,
measured-flight reconciliation, and fragment-budget closure; the SNB regression rows;
weapon-independence at a fixed impact
speed; uncapped factors above one; zero and invalid-input handling; cumulative falloff;
5.45x39 US; all current local ballistic item templates; and exact acceptance of
`com.SPT.core` version `4.1.2` while rejecting missing, lower, higher, or four-part versions.

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
