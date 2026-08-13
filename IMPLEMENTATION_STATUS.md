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

## Verification

- Release build: zero warnings, zero errors
- Validation groups: 18 passed, zero failed
- Installed ammunition sweep: 210 templates, including 208 positive-speed templates over
  nine fractions for 1,872 successful calculations and two expected abstract fallbacks
- No deployment was performed for this development baseline

## Next dependency

Implement the deformation and material-response solver against schema 1. It must produce
validated states and an explicit energy-loss budget before any runtime fragment replacement,
individual fragment flight, or rendering is enabled.
