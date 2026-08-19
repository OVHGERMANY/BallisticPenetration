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