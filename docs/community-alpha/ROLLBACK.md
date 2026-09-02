# Rollback

The release is a simple archive and does not create a backup automatically. Back up the existing plugin folder and configuration before extraction.

To roll back manually:

1. Close every SPT process.
2. Copy the backed-up plugin directory back to `BepInEx\plugins\BallisticPenetration`.
3. Copy the backed-up configuration back to `BepInEx\config\com.janky.ballisticpenetration.cfg`.
4. If no earlier plugin existed, remove only the new `BepInEx\plugins\BallisticPenetration` directory and the new configuration.
5. Verify the restored DLL hash if the backup includes a checksum manifest.

The release contains no rollback utility and never scans or removes unrelated mods.
