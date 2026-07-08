# Changelog — Vecto

## v0.3.0 — 2026-07-08

### Geometric primitives in the fitter (icons stop wobbling)
- Simplest-model-first fitting per range: straight-chord test (`TryLine`) before the
  cubic, least-squares circle arc (`TryArc`, Kåsa fit) before recursive splitting.
  Straight edges now emit single `L` segments and circles/round caps emit exact arc
  geometry (≤90° cubic pieces, endpoints kept exact — planarity preserved). A closed
  no-corner ring lands in the arc path automatically, so dots/heads become true circles.
- New test: a 30° band across the canvas must produce exactly 2 straight lines for its
  long edges (≤8 segments total); circle radial-error bound tightened 1.6 → 1.2 px.
- Bench: node counts −11…−18% (gt-crisp 508→454, gt-blend 705→579, transparency
  339→303) at flat ΔE; VM logo 118→116 nodes. Known trade documented in ROADMAP:
  chord endpoints pinned to the lattice can tilt accepted lines ~0.5 px — sub-pixel
  junction/corner relocation is the next quality lever.

## v0.2.0 — 2026-07-08

### Ground-truth evaluation harness + sub-pixel quality pass
- **Rasterizer** (Core): renders any VectorDocument to pixels — nonzero scanline fill,
  painter's order, supersampled. **SvgReader** (Cli): parses M/L/H/V/C/Z path SVGs back
  into documents. New CLI commands: `vecto render` (SVG→PNG) and `vecto bench` — the
  round trip *original vector → raster → trace → re-render → per-pixel Oklab ΔE diff*
  with heatmap output; VM's own sample SVGs serve as local ground truth.
- **Sub-pixel edge refinement**: boundary points slide along their normals to the 50%
  coverage crossing of the two region colors (bilinear-sampled; alpha against transparent
  sides). Recovers the edge position anti-aliasing encodes instead of snapping to the
  pixel lattice. Mean ΔE on the 4-logo ground-truth bench: −40…−46% per file.
- Corner-flank smoothing damping (thin tips no longer fatten into lobes) + tighter fit
  tolerances + post-refinement jitter pass (error held, ~20% fewer nodes than without).
- **App fix**: original pane now displays at true pixel size — PNGs with non-96-DPI
  metadata (like VM's samples) made the left pane shrink, breaking the side-by-side scale.

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
