# Privacy and Local Data

The plugin does not upload telemetry, logs, reports, system information, or player data.

Local field reports are enabled by default and written under:

`BepInEx\FieldReports\BallisticPenetration`

They are append-only `.bpreport` files with configured file-count and size limits. They contain ballistic, collision, plugin-version, runtime-error, and lifecycle context; profile identities are represented by per-session aliases. They do not contain a username, computer name, IP address, credentials, chat text, or full personal filesystem path.

Adjustment logging and in-game diagnostics are disabled by default. When adjustment logging is enabled, entries are written to the normal local BepInEx log. They can contain ammunition, collision, and runtime context. Review the relevant excerpt before sharing it.

To disable local field reports and optional diagnostics:

```ini
[Field Reports]
Enable Field Bug Reports = false

[Diagnostics]
Log Adjustments = false
Enable In-Game Diagnostics = false
```

Delete old evidence by closing SPT and removing only the `.bpreport` files or local BepInEx logs you no longer want. Do not attach an entire installation or profile.
