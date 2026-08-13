# BallisticPenetration implementation status

Snapshot date: 2026-08-12

## Current live release

- Plugin version: `1.2.0`
- Supported environment: SPT `4.1.2`, EFT `0.16.9.40743`
- Active systems: exact collision-point velocity falloff, uncapped damage and penetration
  curves, cumulative multi-surface scaling, EFT armor integration, and postmortem armor
  durability processing
- Physical projectile state: not connected to live shots in `1.2.0`

## Physical-state development baseline

- State schema: `1`
- Supported component kinds: intact projectile, deformed projectile, projectile fragment,
  and target-generated spall
- Units: kilograms, metres, seconds, radians, joules, kilogram-metres per second for
  momentum, square metres, and kilograms per square metre for physical ballistic coefficient
- Immutable state includes component geometry, mass, drag, orientation, yaw/tumble,
  velocity, momentum, energy, physical capability, lineage, source material/collision,
  collision history, terminal state, and render disposition
- Component ballistic coefficient is derived from that component's retained mass, projected
  area, and drag coefficient; it is not copied from a parent projectile
- Projectile mass and target-spall mass are accounted separately
- Conservation validation bounds projectile allocation and all child kinetic energy after
  penetration, deformation, fracture, heat, and other declared losses
- Fragmentation validation rejects a nominal fragmentation event with no physical
  projectile fragment
- Fixed seed and stream use the stable PCG-XSH-RR sequence
- Invalid state returns a typed failure and no physical object; the caller can leave EFT
  state untouched

## Deformation and material response

- Pure solver: `PhysicalDeformationSolver.TrySolve`
- Projectile inputs: construction, density, plastic-work density, fracture energy per mass,
  ductility, brittleness, deformation coupling, expansion limit, fragment-mass bounds,
  shape penalty, drag multiplier, and yaw/tumble thresholds
- Target inputs: material class, density, calibrated effective resistance pressure,
  deformation/fracture coupling, and heat-loss fraction
- Collision inputs: explicit physical thickness, effective material path, surface normal,
  impact position, and the host-selected outcome and outgoing direction
- Outputs: target work, deformation/fracture/heat/other losses, normal impact energy,
  residual system energy and speed, expansion, projected area, shape, yaw/tumble, drag,
  current physical capabilities, and an appended immutable collision record
- A continuing component keeps its projectile identity, deterministic seed, nominal geometry,
  original mass, lineage, generation, and complete prior collision history
- A host-confirmed fragmentation reserves exact projectile mass and energy for fragment
  construction; the deformation stage does not invent child count, shape, or trajectory
- Stopped events account for all parent translational energy; impossible moving outcomes
  fail open instead of overriding the host result
- No host penetration, ricochet, deviation, fragmentation, or armor random decision is
  evaluated again
- Current profiles are synthetic validation fixtures only. There is no live ammunition,
  armor, body, or world-material calibration or runtime mapping yet
- Target density is carried for the later spall stage and is not claimed to affect the
  present deformation calculation
- Physical thickness is retained as measured geometry while effective path drives work;
  no thickness effect is claimed without a supplied material path

## Verification

- Checked Release build with latest recommended analyzers and warnings as errors: zero
  warnings, zero errors
- Validation groups: 22 passed, zero failed, including 4,096 deterministic deformation
  property cases across speed, impact angle, material path, target resistance, fracture
  coupling, and every supported host outcome
- Installed ammunition sweep: 210 templates, including 208 positive-speed templates over
  nine fractions for 1,872 successful calculations and two expected abstract fallbacks
- No deployment was performed for this development baseline

## Next dependency

Implement deterministic projectile-fragment and target-spall construction from the reserved
mass and energy budgets. Every child must receive component-specific geometry, cross-section,
mass, velocity, energy, drag, orientation, lineage, and collision history before individual
flight, runtime replacement, or rendering is enabled.
