# BallisticPenetration implementation status

Snapshot date: 2026-09-01

## GitHub Test 2 prerelease candidate

- Plugin version: `1.3.0`
- Planned prerelease tag: `v1.3.0-field-report-test.2`
- Supported environment: SPT `4.1.3`, EFT `0.16.9.40743`
- Release target: `netstandard2.1`
- Physical projectile state schema: `2`
- Physical transition publisher schema: `2`
- Experimental physical projectiles: disabled by default

The ordinary gameplay path scales current terminal damage and penetration from exact impact
velocity, applies the result before EFT makes its existing armor and continuation decisions, and
preserves cumulative multi-surface and armor-CF behavior. Covered post-death hits can forward the
already-decided shot into EFT's existing armor-durability calculation without replaying health,
death, armor-penetration, or ricochet decisions.

## Complete ammunition catalog

The exact SPT 4.1.3 catalog resolves all `208` positive-speed ammunition templates:

- `185` kinetic projectile templates enter the physical-projectile path when it is enabled.
- `23` payload templates are explicitly classified and fail open to EFT's existing handling.
- No positive-speed template is unresolved.
- Every entry declares construction, terminal design, and initial physical shape.
- Shot and flechette loads are handled per EFT projectile rather than being rejected as a class.
- Wave-R supplies catalog fallback mass and diameter because its installed template omits both.
- Unknown or changed template identities are not guessed.

Construction profiles include jacketed lead, steel, tungsten, aluminum, and mixed penetrator/core
designs; monolithic copper, brass, zinc, steel, and lead; frangible and nonmetallic composites; and
target-derived material. Terminal design response distinguishes full-metal-jacket, semi-jacketed,
hollow-point, soft-point, expanding, polymer-tipped, open-tip, sabot, exposed penetrator,
frangible, solid, fragment, shot, and flechette projectiles.

## Physical simulation

- Immutable SI state tracks mass, dimensions, area, drag, position, velocity, momentum, energy,
  orientation, yaw/tumble, lineage, source material, collision history, terminal state, and render
  state for intact, deformed, projectile-fragment, target-spall, and target-spall-fragment bodies.
- A deterministic PCG stream and host-bounded child seed mapping drive deformation,
  fragmentation, spall, directions, and child-shot construction.
- Deformation accounts target resistance, plastic work, fracture, heat, residual energy,
  expansion, cross-section, yaw, tumble, drag, and remaining physical capability.
- Fragmentation partitions the immediate parent's reserved mass and energy. Target material
  provenance remains independent from immediate-parent mass source, including later target-spall
  fragmentation.
- Conservation gates reject mass over-allocation, excess output energy, invalid lineage,
  duplicated component identity, non-finite state, and inconsistent history.
- Each surviving component projects its own mass, equivalent diameter, velocity, drag, damage,
  and penetration into an EFT child shot. EFT remains the trajectory integrator and the authority
  for penetration, armor, ricochet, deviation, and fragmentation outcomes.
- Ten deterministic meshes cover spitzer, round-nose, flat-nose, mushroomed, flattened,
  irregular-fragment, spall-flake, spall-chunk, spherical-shot, and flechette geometry.
- Rendering uses shared meshes/materials, a generation-owned pool, bounded FIFO work, per-frame
  command limits, nearest-first culling, embedded expiry, destroyed-slot recovery, and scene
  cleanup.

## Telemetry boundary

Schema `2` publishes detached prepared and resolved collision snapshots through BCL-only
subscription methods. Records include host identity, impact geometry, exact projectile design,
complete parent/output state, material provenance, immediate-parent mass source, losses, residual
and output energy, closure error, and optional opaque surface identity. No pooled shot, collider,
Unity object, or mutable foreign collection is retained. With no subscriber, snapshot work is
skipped.

## Offline verification

- Release solution build: warnings as errors, checked arithmetic, recommended .NET analyzers,
  code-style enforcement, deterministic build; `0` warnings and `0` errors.
- Validation: `45` groups passed, `0` failed.
- Installed database: `210` numeric templates, `208` positive-speed fireable templates, `1,872`
  falloff calculations, and two expected abstract zero-speed fallbacks.
- Deterministic deformation and fragmentation property sweeps: `4,096` cases each.
- Renderer geometry, manifold/winding, ownership, culling, queue capacity, and deterministic stress
  checks pass.
- Physical state, projection, host-seed bounds, telemetry isolation, conservation, provenance,
  fail-open, and cumulative-flight checks pass.

## Known alpha limitations

- The physical path is experimental and remains disabled by default.
- Construction and target-material properties are engineering approximations from the installed
  database and public descriptions, not manufacturer drawings or certification data.
- EFT remains responsible for flight integration and outcome decisions; this project does not
  replace its full aerodynamic or armor model.
- Payload templates are cataloged but intentionally not converted to kinetic projectiles.
- Low-poly component geometry is a physical-state visualization, not a scanned projectile model.
- Some installed multi-projectile source values are game abstractions and are preserved per EFT
  child unless an explicit catalog fallback is required.
- The `1.3.0` candidate still requires the final minimal startup/load smoke test and broader
  community runtime testing for compatibility, balance, rendering, pooling, and performance.

## Release boundary

No further feature category is required before the community alpha. Changes after publication are
limited to defects reproduced by local or community testing, with crashes, corrupt state,
classification errors, conservation failures, provenance failures, pooled-identity leaks, severe
performance regressions, and broken installation or rollback taking priority.
