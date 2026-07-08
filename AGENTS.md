# Agent rules — Vecto

## Reading ritual (start of every session)
1. AGENTS.md — this file
2. docs/ai/START_HERE.md — orientation; follow its links as the task needs

## Navigation — find code without crawling files
- Structure / "where is X?", callers, call paths → CodeGraph (`codegraph_explore` or
  `codegraph explore "..."`). Trust it; don't grep-loop.
- Intent / why / gotchas → the right file in docs/ai/ (map below).
- Do NOT hand-maintain a file map — CodeGraph owns structure.
- After editing code, run `codegraph sync` — this project lives on `/mnt/d` under WSL,
  so the index does not auto-refresh (see GOTCHAS).

## docs/ai/ map
| File | Owns |
|------|------|
| START_HERE.md | Orientation, current priority, how to run |
| ALGORITHM_INDEX.md | Pipeline stage → algorithm → parameters → code anchor |
| DECISIONS.md | Durable "why" choices |
| GOTCHAS.md | Build/run/env traps, licensing constraints |

## Workflow
- Plan → implement → build → test → summarize in one go. Milestone-sized steps.
- Every change: `dotnet.exe build -c Release` must stay at 0 warnings / 0 errors
  (TreatWarningsAsErrors is on), `dotnet.exe test` green, plus a CHANGELOG.md entry.
- The engine has a self-check: `vecto trace <img> --check` verifies the planar
  partition (region areas must tile the opaque canvas). Use it after engine changes.

## Commit & push
- Work on `dev`; merge to `main` only on an explicit release request.
- Commit format: short imperative summary + body, co-author line:
  `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`
- Push from WSL: `cmd.exe /c "git push origin dev"` (credentials live on the Windows side).

## Documentation upkeep
- CHANGELOG.md = what shipped (the session log; don't make a second one).
- Update DECISIONS.md on a durable choice; ALGORITHM_INDEX.md when a pipeline stage,
  algorithm, or tuning parameter changes.
- Keep START_HERE's "current priority" in sync with ROADMAP.md.
