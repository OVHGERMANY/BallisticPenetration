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

The normal falloff path does not modify flight drag, trajectory integration, ricochet
angle/RNG, armor resistance formulas, armor durability formulas, or visual effects.
Postmortem armor wear calls the existing EFT durability method without changing its
calculations. The disabled-by-default physical-projectile path supplies component-specific
mass, diameter, drag, velocity, damage, and penetration to EFT child shots while retaining
EFT's trajectory integrator and observed collision decisions.

Armor block and penetration outcomes can change because the existing armor calculation
reads the corrected `PenetrationPower`. That is the intended gameplay effect. The
separate armor-CF child-shot degradation remains unpatched.

## Physical projectile development

The development tree now contains a dependency-free, versioned physical-state core for
intact projectiles, deformed projectiles, projectile fragments, and target-generated
spall. It records component-specific SI values for mass, geometry, projected area, drag,
position, velocity, momentum, kinetic energy, orientation, yaw, terminal state, lineage,
and collision history.

Mass retained from the immediate parent and fresh target spall are separate categories.
Every component also records whether its material originated in the ammunition or the
struck target, including target-spall fragments created at a later collision. Transition
validation rejects parent-mass over-allocation, child energy above the residual collision
budget, mixed parent/root/collision lineage, duplicate component identities, non-finite
state, and fragmentation events with no physical parent fragment. A fixed PCG random
stream is provided for later deformation and fragmentation calculations.

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
from target mass and penetration-work energy, so it never consumes mass retained by the
immediate parent. Every output has independent geometry, projected area, drag, direction,
velocity, momentum, energy,
physical capability, source, lineage, history, and render state. A host-reported zero fragment
count remains observable and produces the minimum one physical projectile component needed to
close a nonzero fragmentation reservation. On hard materials, a confirmed penetration or
deviation can also eject target spall when the projectile itself did not fragment.

The development tree also contains the checked boundary for individual flight. A pure projector
converts each physical component into EFT mass, equivalent diameter, velocity, relative G1 drag,
damage, and penetration while preserving an explicitly supplied EFT target/armor transfer
multiplier. A separate flight reconciler accepts EFT's measured position and velocity at the next
collision and advances energy-based physical capability without replacing EFT's trajectory
integrator. Root state construction derives area, equivalent length, orientation, energy, and
capability from measured shot values. The runtime binding rejects stale state if EFT recycles the
same pooled `Shot` object for another projectile.

The runtime has conservative development profiles for projectile construction and EFT world,
body, and armor material classes. These are deterministic engineering estimates derived from
the limited fields exposed by EFT; they are not manufacturer metallurgy or certification data.
Single-projectile large-caliber ammunition remains eligible when EFT's grenade-component fields
describe only a visual impact effect: explosive strength, fragment count, fuse time, and blast
distance must all be zero. Multi-projectile loads and mechanically explosive ammunition remain
outside the physical path.
An optional schema-1 reflection contract allows any collider to provide a canonical physical
material class and opaque surface identity without a compile-time assembly reference. Invalid
metadata leaves the host collision untouched. The built-in profiles distinguish titanium from
armored steel while retaining separate aluminum, ceramic, polymer, fabric, and composite classes.
The exact struck collider is measured from the entry hit to its far face. Physical thickness
normal to the surface remains separate from the actual oblique path through material.

The physical path is attached to `Shot.CreateFragments` behind `Experimental / Enable Physical
Projectiles`, which defaults to `false`. It reads EFT's already-selected stop, penetration,
deviation, ricochet, or fragmentation outcome and never invokes those decision methods again.
For an accepted collision it replaces EFT's child list only after every physical state,
projection, child shot, trajectory, armor-CF application, and pool-safe binding succeeds.
Penetrating components begin their next flight at the measured far face; ricochets and stopped
components remain at the impact face. Projectile fragments and target spall retain independent
mass, shape, cross-section, drag, velocity, energy, damage, penetration, lineage, and subsequent
collision state. Target spall can itself deform and fragment at a later target without being
reclassified as bullet mass.

The physical path now includes dedicated post-impact geometry for intact and deformed projectiles,
projectile fragments, and target-generated spall. Eight deterministic low-poly shape classes cover
spitzer, round-nose, flat-nose, mushroomed, flattened, irregular-fragment, spall-flake, and
spall-chunk states. Mesh scale comes from each component's calculated diameter and length. Physical
yaw is applied around a deterministic azimuth, while moving geometry follows the exact pooled EFT
shot only while its binding identity remains current.

Rendering is main-thread-only and uses shared meshes and materials, a generation-checked slot pool,
nearest-first distance culling, separate visible/tracked budgets, scene cleanup, embedded-component
expiry, and destroyed-slot recovery. Collision patches enqueue immutable commands and never create
or mutate Unity objects. The default dimension scale is one and the default minimum visible diameter
is zero, so calculated component size is preserved unless the user deliberately enables visual-only
enlargement.

The physical runtime also exposes an optional, versioned transition telemetry boundary. It publishes
immutable prepared and resolved collision records through BCL-only subscription methods, so an
external development tool can observe complete SI state without a compile-time dependency. Records
include copied shot lineage, measured impact geometry, target profile, parent and output components,
separate projectile-derived and fresh target-spall mass, declared losses, residual/output energy,
closure error, and an optional opaque target-surface identity. No pooled host or Unity object is
retained. With no subscriber, the collision path
returns before constructing a telemetry snapshot; subscriber exceptions are isolated from the
simulation and from other subscribers.

This runtime path has passed offline compiler, analyzer, invariant, conservation, deterministic,
renderer-isolation, ownership-generation, mesh-geometry, and full ammunition-database tests. It has
entered the final integrated in-game acceptance campaign and remains experimental. Only the exact
ballistic collider reported by EFT participates. A loose inventory item's visible mesh may lack an
active ballistic collider; in that case the shot and physical model continue to the world surface
behind it, and a decal overlapping the loose mesh is not evidence that the item absorbed the hit.

## Configuration

After the game starts once, BepInEx creates:

```text
<SPT_ROOT>\BepInEx\config\com.janky.ballisticpenetration.cfg
```

Available settings:

- `General / Enabled` (default `true`)
- `General / Damage Armor On Corpses` (default `true`)
- `Experimental / Enable Physical Projectiles` (default `false`)
- `Physical Rendering / Render Physical Components` (default `true`; requires the experimental physical path)
- `Physical Rendering / Maximum Visible Components` (default `128`, range `8` through `512`)
- `Physical Rendering / Maximum Tracked Components` (default `512`, range `16` through `4096`)
- `Physical Rendering / Maximum Commands Processed Per Frame` (default `256`, range `32` through `1024`)
- `Physical Rendering / Culling Distance Meters` (default `200`, range `10` through `2000`)
- `Physical Rendering / Dimension Scale` (default `1`, range `0.25` through `25`)
- `Physical Rendering / Minimum Rendered Diameter Millimeters` (default `0`, range `0` through `50`)
- `Physical Rendering / Embedded Component Lifetime Seconds` (default `45`, range `0.25` through `600`)
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
measured-flight reconciliation, material-exit placement, continuing target-spall fragmentation,
fragment-budget closure, deterministic component meshes, physical yaw, visual ownership generations,
culling boundaries, capacity limits, renderer-core dependency isolation, immutable physical
transition telemetry, projectile/spall accounting, energy closure, and observer isolation; the SNB regression rows;
weapon-independence at a fixed impact
speed; uncapped factors above one; zero and invalid-input handling; cumulative falloff;
5.45x39 US; all current local ballistic item templates; and exact acceptance of
`com.SPT.core` version `4.1.2` while rejecting missing, lower, higher, or four-part versions.

The game-facing project has compile-only references to BepInEx, Harmony, SPT reflection,
Assembly-CSharp, UnityEngine.CoreModule, UnityEngine.PhysicsModule, and UnityEngine.
`UnityEngine.PhysicsModule` supplies exact collider ray measurement. `UnityEngine.dll` is
required solely because BepInEx `BaseUnityPlugin` exposes `MonoBehaviour` from that assembly.
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
