# Changelog — Vecto

## v0.1.0 — 2026-07-08

### Phase 0 — Bootstrap
- .NET 8 solution: Vecto.Core (engine, dependency-free), Vecto.Cli (`vecto`),
  Vecto.App (WPF, assembly `Vecto`), Vecto.Tests (xUnit).
- Solution-wide props: nullable, implicit usings, TreatWarningsAsErrors, version 0.1.0.
- Git: `main` = releases, `dev` = working branch. `.codegraph/`, `_bench/`, bin/obj ignored.

### Phase 1–2 — Engine + CLI
- Full pipeline (see docs/ai/ALGORITHM_INDEX.md): Oklab weighted k-means++ palette with
  agglomerative auto-merge and edge-excluded sampling; exact palettes for crisp art;
  nearest-color labeling with alpha threshold; small-region absorption; **planar boundary
  graph** — crack-following chains shared between adjacent regions (structurally no
  gaps/overlaps, same property as Vector Magic's output); corner detection with non-max
  suppression; corner-pinned clamped Laplacian smoothing; Schneider least-squares Bezier
  fitting with C1 wrap-around seams; VM-style SVG writer (region paths + holes, 2-decimal
  invariant coords, line collapsing).
- CLI: `trace` (colors/detail/style/polygons/seed/seg-png/stats), `samples` (procedural
  crisp/blended/transparent test images), `--check` planarity self-verification.
- 12 tests: exact planar partition, corner preservation, circle node count + radial error,
  transparency, speckle absorption, exact crisp palettes, curved planarity ≤0.5%,
  determinism, de-DE culture guard.
- Palette display colors average flat pixels only (AA fringes can't tint fills);
  weighted k-means over unique-color histogram (palette stage 534→39 ms on the VM sample).

### Phase 3 — WPF app MVP
- Side-by-side original/vector with synced zoom+pan (Ctrl+wheel, Fit/1:1), right pane
  switchable to segmentation view, live re-trace on setting change (250 ms debounce,
  cancellable), palette swatches with hex tooltips, stage-timing stats.
- Open/drag-drop/paste input; SVG export and copy-to-clipboard; `--smoke` headless self-test;
  crash log to %TEMP%.
- Corner threshold 60°→68° (false corners at staircase slope transitions); benchmark on
  Vector Magic's "Logo With Blending Small": identical palette (#1404ab/#ffffff),
  8 paths vs 8, **96 nodes vs VM's 96**, planarity deviation 0.00%.
