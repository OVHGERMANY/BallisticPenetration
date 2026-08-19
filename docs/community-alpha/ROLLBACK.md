# Rollback

The integrated installer creates a timestamped backup before replacing any installed file. Keep that folder until testing is finished.

To roll back manually:

1. Close every SPT process.
2. Copy the backed-up plugin directory back to `BepInEx\plugins\BallisticPenetration`.
3. Copy the backed-up configuration back to `BepInEx\config\com.janky.ballisticpenetration.cfg`.
4. If no earlier plugin existed, remove only the new `BepInEx\plugins\BallisticPenetration` directory and the new configuration.
5. Verify the restored DLL hash if the backup includes a checksum manifest.

The package rollback script accepts an explicit SPT root and restores only files listed in its backup manifest. It does not scan or remove unrelated mods.
