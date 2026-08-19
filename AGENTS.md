# AGENTS.md

- Never change projectile trajectory, velocity, penetration, damage, fragments, collision outcomes, or gameplay behavior unless the current task explicitly authorizes it.
- Make narrowly scoped changes only.
- Inspect Git status, current branch, HEAD, and recent commits before editing.
- Build with warnings treated as errors.
- Run the relevant validation project after changes.
- Never claim installation until source and installed DLL SHA-256 hashes match.
- Never commit unrelated files.
- Read `docs/PROJECT_STATE.md` and the active execution plan before work.
- Update project state before ending a task.
- Prefer targeted searches and relevant files over scanning the entire repository.
- Save large logs to files and report only paths, important lines, and summaries.