# START HERE — Vecto

**What this is:** a Windows desktop bitmap-to-vector tracer in the fashion of Vector Magic
(the Qt app in `C:\Program Files\Vector Magic`): full-color planar vectorization — every
boundary is fitted once and shared by both adjacent regions, so the output has no gaps and
no overlaps between shapes. C# / .NET 8 / WPF.

**Current status:** v0.1.0 — Phases 0–3 done (engine + CLI + tests + WPF app MVP).
**Current priority:** Phase 4 (see ROADMAP.md) — wizard flow, palette editor, background
removal; plus tuning-backlog items as they bite.

## Solution layout (structure beyond this → CodeGraph)
- `Vecto.Core` — the engine, zero dependencies. Pipeline lives in `Tracer.Trace`.
- `Vecto.Cli` — headless front-end (`vecto`), also generates procedural test images.
- `Vecto.App` — WPF UI (assembly `Vecto`), thin layer over Core.
- `Vecto.Tests` — xUnit; geometric invariants, not snapshots.

**Fresh clone?** Run `codegraph init` once in the repo root — the CodeGraph index is a
local build artifact (gitignored by design, see the setup playbook); it rebuilds in seconds.

## Build / test / run (from WSL — always `dotnet.exe`, see GOTCHAS)
```
dotnet.exe build -c Release          # must be 0 warnings / 0 errors
dotnet.exe test Vecto.Tests -c Release
./Vecto.Cli/bin/Release/net8.0/vecto.exe trace in.png -o out.svg --stats --check
./Vecto.Cli/bin/Release/net8.0/vecto.exe samples _bench    # writes test PNGs
"./Vecto.App/bin/Release/net8.0-windows/Vecto.exe" [image] # the app (or --smoke)
```

## Where to read next
- How the pipeline works, stage by stage, with parameters → `ALGORITHM_INDEX.md`
- Why it's built this way (planar graph, WPF, no VTracer, …) → `DECISIONS.md`
- Environment traps (WSL, ImageSharp license, culture) → `GOTCHAS.md`
- What shipped when → `../../CHANGELOG.md`; what's next → `../../ROADMAP.md`

## Benchmarking against Vector Magic
`_bench/` (gitignored) holds traces of Vector Magic's own sample logos — VM's reference
output lives in `C:\Program Files\Vector Magic\Samples\Sample Output`. Current parity on
"Logo With Blending Small": identical palette (#1404ab/#ffffff), 8 paths vs 8,
96 nodes vs 96, planarity deviation 0.00%. Never commit VM's sample images.
