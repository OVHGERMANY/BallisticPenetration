# BallisticPenetration implementation status

Snapshot date: 2026-08-13

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

## Conserved fragmentation and target spall

- Pure solver: `PhysicalFragmentationSolver.TrySolve`
- It runs only after a host-confirmed fragmentation outcome and consumes the observed host
  fragment count without invoking EFT decision or random methods
- Reserved projectile mass and energy are deterministically partitioned into component-specific
  projectile fragments; a zero host count remains recorded and still produces the one minimum
  physical fragment required to close nonzero reservations
- Retained primary projectile mass, projectile fragments, and target-generated spall remain
  separate outputs with distinct mass, energy, material origin, shape, drag, direction, lineage,
  and history
- Target-spall kinetic energy is reclassified from penetration work rather than added to the
  collision energy budget
- Every child receives its own mass, equivalent diameter, projected area, length, aspect ratio,
  drag, velocity, momentum, energy, penetration capability, damage capability, orientation,
  fragment index, generation, source collision, and render state
- Mass and energy closure is revalidated after component construction

## Individual-flight integration boundary

- Pure projector: `PhysicalEftProjectileProjector.TryProject`
- A component maps to EFT mass in grams, equivalent diameter in millimetres, speed, direction,
  relative G1 coefficient, damage, and penetration without inheriting whole-projectile mass,
  diameter, or drag
- Projection preserves an explicitly measured EFT target/armor transfer multiplier while
  replacing EFT's placeholder fragment share with the physical capability share
- Pure flight reconciler: `PhysicalProjectileFlightState.TryAdvance`
- EFT remains the flight integrator; measured position and velocity advance immutable physical
  state and energy-based capability before the next material interaction
- The runtime seam is verified: `Shot.CreateFragments` finishes child construction before
  `BallisticsCalculator.UpdateShots` schedules `Shot.Fragments`, so an outer postfix can validate,
  rewrite, and reinitialize children before their first tick
- These layers are not connected to live shots yet; no gameplay behavior changed in this baseline

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
- Validation groups: 30 passed, zero failed, including 4,096 deterministic deformation cases,
  4,096 deterministic fragmentation cases, physical-to-EFT projection, measured-flight
  reconciliation, fail-open rejection, and the complete installed-ammunition sweep
- Installed ammunition sweep: 210 templates, including 208 positive-speed templates over
  nine fractions for 1,872 successful calculations and two expected abstract fallbacks
- No deployment was performed for this development baseline

## Next dependency

Connect the verified physical state, deformation, fragmentation, projection, and flight layers to
EFT shots through the existing `CreateFragments` target. Runtime integration must keep a pool-safe
shot-to-state binding, measure a real collider material path, preserve EFT's already-selected
outcome and armor CF, reinitialize every rewritten child trajectory, create any conserved retained
primary or target-spall components before scheduling, and leave the original child list untouched
when any validation step fails.
