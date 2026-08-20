# DECISIONS

## 2026-08-18
- Decision: Establish a documentation-first memory state system before additional implementation.
- Reason: User constrained this as the source-of-truth bootstrap and requested small-context checkpoints.
- Files/components affected: `AGENTS.md`, `docs/PROJECT_STATE.md`, `docs/exec-plans/active/lifecycle-diagnostics.md`.

- Decision: Freeze gameplay logic for this pass.
- Reason: Current objective is diagnostics-only lifecycle/event accounting.
- Files/components affected: BallisticPenetration diagnostic runtime and validation scope.

- Decision: Reuse compact HANDOFF reporting at meaningful checkpoints.
- Reason: User requested compact checkpoint reporting and no full history in chat.
- Files/components affected: `AGENTS.md`, `docs/PROJECT_STATE.md`, `docs/exec-plans/active/lifecycle-diagnostics.md`, `docs/DECISIONS.md`.

## 2026-08-20
- Decision: Keep ordinal collision equality and make only the `hash * 397` combination explicitly unchecked.
- Reason: The repository enables checked overflow globally; ordinary string hashes must not throw, and changing equality or the global arithmetic policy would widen scope.
- Files/components affected: `PhysicalCollisionEventDeduplicator`, lifecycle diagnostics, validation.

- Decision: Bound identical runtime errors by retaining one sanitized full record, emitting power-of-two aggregates, and writing final per-fingerprint totals.
- Reason: This preserves diagnostic detail and exact occurrence counts without allowing one hook failure to flood the report.
- Files/components affected: `FieldReportRuntimeErrorAccumulator`, `FieldReportRuntime`, `Plugin`.

- Decision: Treat a mismatched pooled `Shot` as untrusted report context.
- Reason: Current mutable shot fields can belong to a later incarnation; the tracker snapshot and binding-creation values are the only attributable evidence for the retired projectile.
- Files/components affected: lifecycle tracker, lifecycle report context, lifecycle report schema.

- Decision: Stabilize low-coefficient child projections at the non-reversing bound derived from EFT's fixed `10 ms` explicit-Euler drag step and maximum G1 table coefficient.
- Reason: The field report and EFT source show that an unstable drag step can reverse target-spall velocity and compound it to `10^17`-scale values. A derived coefficient floor preserves assigned mass, energy, direction, and speed while avoiding an arbitrary global speed clamp.
- Files/components affected: `PhysicalEftProjectileProjector`, runtime numeric-runaway guard, validation.
