# BallisticPenetration implementation status

Snapshot date: 2026-08-13

## Current development build

- Plugin version: `1.2.0`
- Supported environment: SPT `4.1.2`, EFT `0.16.9.40743`
- Production systems: exact collision-point velocity falloff, uncapped damage and penetration
  curves, cumulative multi-surface scaling, EFT armor integration, and postmortem armor
  durability processing
- Experimental physical runtime: connected to host-confirmed collision outcomes behind a
  default-off configuration gate; implemented offline and awaiting end-of-development integrated
  game testing
- This development build has not been deployed

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

- Root factory: `PhysicalRootProjectileFactory.TryCreate`
- It derives frontal area, equivalent cylinder length, orientation, kinetic energy, and physical
  capabilities from measured mass, diameter, density, position, and velocity without substituting
  EFT damage or penetration stats for SI geometry
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
- `PhysicalShotBindingStore` implements that seam and rejects stale entries when EFT recycles a
  pooled `Shot`; it matches the complete captured creation identity rather than trusting the object
  reference alone
- A collision transition replaces the host child list only after every physical state, projection,
  replacement shot, trajectory, armor-CF application, and binding succeeds
- Roots, retained primaries, projectile fragments, and target spall continue through the same
  measured-flight and later-collision path

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
- Runtime mappings use conservative construction and material-class profiles derived from the
  limited host fields. They remain engineering estimates, not manufacturer metallurgy or
  certification data
- Target density is carried for the later spall stage and is not claimed to affect the
  present deformation calculation
- Physical thickness is retained as measured geometry while effective path drives work;
  no thickness effect is claimed without a supplied material path

## Physical component rendering

- Eight deterministic low-poly meshes represent spitzer, round-nose, flat-nose, expanded,
  flattened, irregular projectile-fragment, spall-flake, and spall-chunk geometry
- Mesh scale uses each component's calculated diameter and length; physical attitude and yaw are
  carried through later measured flight instead of being reconstructed from a decal
- Unity work is restricted to the main thread; collision hooks enqueue immutable render commands
- A generation-owned fixed pool rejects stale owners, validates pooled-shot identity, recovers
  destroyed slots, shrinks safely while idle, and cleans up on scene transitions
- Nearest-first culling, separate visible/tracked limits, embedded expiry, shared materials and
  meshes, and disabled shadows/probes/motion vectors bound rendering cost

## Physical transition telemetry

- Public schema: `PhysicalProjectileTelemetry.SchemaVersion == 1`
- `Subscribe(Action<object>)` and `Unsubscribe(Action<object>)` form a compile-time-independent
  observation boundary; the payload is the typed immutable `PhysicalProjectileTelemetryEvent`
- No host object, collider, shot, Unity object, or mutable output collection is retained
- Prepared events are emitted only after collision state validation; resolved events are emitted
  only after stopped-state registration or complete transactional child replacement
- Records carry copied host identity, exact impact geometry, target profile, the complete immutable
  parent and outputs, projectile-derived mass, fresh target-spall mass, all loss categories,
  residual/output energy, and closure error
- The runtime performs no telemetry snapshot work with zero subscribers, and one failing subscriber
  cannot interrupt another subscriber or the physical transaction

## Verification

- Checked Release and Debug builds with all default analyzers enabled, warnings as errors, checked
  arithmetic, deterministic continuous-integration settings: zero warnings, zero errors
- Validation groups: 38 passed, zero failed, including immutable telemetry and observer isolation,
  deterministic render geometry and ownership, 4,096 deterministic deformation cases,
  4,096 deterministic fragmentation cases, physical-to-EFT projection, measured-flight
  reconciliation, fail-open rejection, and the complete installed-ammunition sweep
- Installed ammunition sweep: 210 templates, including 208 positive-speed templates over
  nine fractions for 1,872 successful calculations and two expected abstract fallbacks
- No deployment was performed for this development baseline

## Next dependency

Extend the standalone development lab through reflection-only subscription to the public telemetry
boundary. Add schema-version validation, immutable report DTOs, paired prepared/resolved transition
storage, conservation exports, deterministic campaign matrices, and automatic fixture reset without
introducing a project reference or runtime dependency in either direction.
