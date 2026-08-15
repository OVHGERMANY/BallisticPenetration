# Known Issues and Limitations

No reproducible defect is confirmed for `1.3.0-alpha.1` at publication time. New reports remain unconfirmed until they can be reproduced or supported by sufficient evidence.

## Known limitations

- Only SPT 4.1.2 / EFT 0.16.9.40743 is supported.
- The physical-projectile runtime is experimental and disabled by default.
- Unknown ammunition template identities fail open to the host behavior instead of receiving a guessed physical design.
- The material system uses conservative engineering profiles based on the information exposed by the game. It is not manufacturer test data or certification evidence.
- A visible loose item may not have an active ballistic collider. In that case the physical path correctly observes the world collider behind it.
- Broad runtime balance, rendering, compatibility, and long-session performance evidence is still being collected.

## Confirmed issue register

| Short title | Affected version | Ammunition or system | Severity | Reproduction status | Workaround | Fix status | Issue |
|---|---|---|---|---|---|---|---|
| None currently confirmed | 1.3.0-alpha.1 | — | — | — | — | Open for community evidence | — |
