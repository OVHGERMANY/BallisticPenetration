# Changelog

## 1.3.0

- Added an exact physical-design catalog for all 208 positive-speed ammunition templates in SPT 4.1.3.
- Classified 185 kinetic projectile templates and 23 payload templates, with zero unresolved positive-speed templates.
- Added construction, terminal-design, and initial-shape profiles for conventional bullets, penetrators, slugs, buckshot, flechettes, and game-defined fragments.
- Added per-projectile handling for multi-projectile shot and flechette loads.
- Added deterministic spherical-shot and flechette geometry and material rendering.
- Added design-specific expansion, fracture, and drag response.
- Corrected false mushroom rendering: a projectile now uses mushroom geometry only after calculated diameter expansion.
- Embedded stopped components now place their calculated center inside the struck material.
- Mapped deterministic child seeds into the host random-table range without changing the full physical seed.
- Advanced physical transition telemetry to schema 2 with projectile-design evidence.
- Added automatic local field reports with bounded retention, privacy-safe context, lifecycle correlation, and fail-open error handling.
- Added complete observed/resolved collision correlation, terminal lifecycle diagnostics, and numeric-runaway evidence.
- Prevented unstable target-spall host-flight projection without adding a speed clamp.
- Prevented independent projectile and target-spall meshes from remaining on characters, armor, helmets, or corpses.
- Added collider-local anchoring so eligible embedded world geometry follows moving props.
- Updated the exact compatibility gate for SPT 4.1.3.
- Kept the physical-projectile runtime disabled by default.

The Release build and complete offline validation pass. The exact release artifact has not completed in-game runtime acceptance.
