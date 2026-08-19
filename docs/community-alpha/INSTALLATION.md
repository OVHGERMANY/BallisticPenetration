# Installation, Update, and Uninstallation

## Requirements

- SPT 4.1.2 exactly.
- EFT 0.16.9.40743.
- Windows client process `EscapeFromTarkov.exe`.

The plugin fails closed on another SPT core version.

## Install

1. Close SPT, the launcher, server, and game.
2. Back up any existing `BepInEx\plugins\BallisticPenetration` folder and `BepInEx\config\com.janky.ballisticpenetration.cfg`.
3. Extract the package into the SPT root. The package places the DLL at:

   `BepInEx\plugins\BallisticPenetration\BallisticPenetration.dll`

4. Start SPT normally.
5. Confirm the BepInEx log contains `Janky-BallisticPenetration 1.3.0` and no load failure.

The normal terminal-ballistics path is enabled by default. The experimental physical-projectile path remains disabled. To test it, close the game, edit:

`BepInEx\config\com.janky.ballisticpenetration.cfg`

and set:

```ini
[Experimental]
Enable Physical Projectiles = true
```

## Update

1. Close every SPT process.
2. Back up the installed DLL and configuration.
3. Replace only `BepInEx\plugins\BallisticPenetration\BallisticPenetration.dll` with the new package DLL.
4. Keep the existing configuration unless the release notes explicitly require a reset.
5. Verify the installed DLL hash against `SHA256SUMS.txt`.

## Uninstall

1. Close every SPT process.
2. Delete only `BepInEx\plugins\BallisticPenetration`.
3. Optionally delete `BepInEx\config\com.janky.ballisticpenetration.cfg` after saving any settings you want to keep.

Do not delete the BepInEx, SPT, or EFT directories.
