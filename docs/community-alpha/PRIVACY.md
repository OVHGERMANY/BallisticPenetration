# Privacy and Local Data

The plugin does not upload telemetry, logs, reports, system information, or player data.

Adjustment logging and in-game diagnostics are disabled by default. When adjustment logging is enabled, entries are written to the normal local BepInEx log. They can contain ammunition, collision, and runtime context. Review the relevant excerpt before sharing it.

To disable evidence generation:

```ini
[Diagnostics]
Log Adjustments = false
Enable In-Game Diagnostics = false
```

Delete old evidence by closing SPT and removing only the local BepInEx log files you no longer want. Do not send logs automatically or attach an entire installation.
