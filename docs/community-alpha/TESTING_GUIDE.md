# Community Alpha Testing Guide

This build is experimental. Bugs, balance problems, visual defects, and compatibility failures are expected.

## Normal testing

Ordinary gameplay reports are useful. The optional physical-projectile path must be enabled in the configuration before testing bullet deformation, projectile fragmentation, target spall, individual child continuation, or physical component rendering.

Focus on:

- every ammunition family, especially rare or unusual rounds;
- different barrels and muzzle-velocity modifiers;
- intact penetration, stops, ricochets, and deviation;
- calculated expansion, flattening, projectile fragmentation, and target-generated spall;
- target-spall fragments and projectile fragments striking later targets;
- body entry and exit;
- multi-surface continuation, multi-layer armor, and spaced armor;
- post-death armor durability;
- bullet, fragment, spall, and embedded-component geometry;
- long sessions, pooling, cleanup, frame time, and compatibility with other SPT mods.

## Useful evidence

Record the exact release tag and commit, SPT/EFT versions, round, weapon and barrel, target or material, expected result, actual result, and repeatable steps. A short relevant log excerpt and clear screenshot are better than an entire log or game directory.

Do not attach EFT assemblies, assets, profiles, or the full installation. Review every log and image for personal information before sharing it.
