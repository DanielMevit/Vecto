# Changelog — Vecto

## v0.4.2 — 2026-07-19

### Engine: sub-pixel corner relocation + angle snapping
- Validated corners no longer stay pinned to the pixel lattice: each moves to the
  intersection of least-squares lines fitted to its refined flanks (`RelocateCorners`).
  Straight sides fit one shared line from both ends, flank directions within 1° of a
  45° multiple snap exactly, and the AA-contaminated shoulder vertices are projected
  onto the flank lines — so axis-aligned and diagonal sides come out *mathematically*
  straight at the sub-pixel position the anti-aliasing encodes. Junctions stay pinned
  (multi-chain solve still on the roadmap).
- New test: an AA box with sub-pixel borders must trace to exactly 4 lines, exactly
  horizontal/vertical, at the true edge positions. Bench (4 ground-truth files):
  meanΔE −1…−7%, big-error tail (>0.1) −4…−10%, nodes flat to −4%.
- Found + documented in ROADMAP: chamfered-AA corners (ambiguous ~50% border pixels)
  escape k=3 corner detection entirely — a separate detection-side lever.

### Windows packaging: MSIX + portable (packaging/windows/build.ps1)
- One script builds every Windows artifact, version read from Directory.Build.props:
  compressed single-file exe, portable ZIP (app + `vecto` CLI + `portable.txt`),
  self-signed sideload MSIX with its public `.cer`, and — with `-Store` — the
  unsigned Partner Center upload stamped with the `VECTO_STORE_*` identity values.
- `AppxManifest.xml`: `runFullTrust` as the only capability, Open-with file
  association for png/jpg/jpeg/bmp/gif, Store tiles generated from the master logo
  (`scripts/make-msix-assets.ps1`). `certs/` is gitignored (the PFX is a secret).
- **Portable mode**: a `portable.txt` beside `Vecto.exe` moves `settings.json` next
  to the exe; the portable ZIP ships the marker plus a README explaining it.
- `docs/store/SUBMISSION.md` + `LISTING.md`: field-by-field Partner Center copy
  (properties, requirements with measured numbers, runFullTrust justification,
  listing text and keywords). The site gains `/privacy/` — certification requires
  a policy URL for full-trust apps.

### Quick wins (app + CLI)
- **App: PNG export** — Export PNG… renders the traced vector through the Core
  Rasterizer (1×, supersampled) and saves via the WPF PNG encoder.
- **App: Nodes view** — third right-pane mode drawing every path outline plus node
  markers, sized in screen pixels (rebuilt on zoom, clipped to canvas); markers skip
  above 20k nodes to stay responsive.
- **App: settings persistence** — last-used palette/style/detail and window size/state
  restored from `%APPDATA%\Vecto\settings.json`; corrupt/missing files fall back to
  defaults silently.
- **CLI: batch tracing** — `vecto trace` takes multiple inputs and self-expanded
  wildcards (`vecto trace *.png --out-dir out`); `-o`/`--seg-png` stay single-input;
  `--check` failures accumulate to exit 2. `--version` now reports the real assembly
  version (was hardcoded 0.1.0).
- Docs: START_HERE status/priority de-drifted (still said v0.1.0); ROADMAP gains a
  Distribution section (Store/winget/signing).

### Product website (site/)
- Astro landing page + changelog at https://danielmevit.github.io/vecto/ — mirrors the
  Eqho/Laydown site playbook (data-driven sections, version read from
  Directory.Build.props at build time, robots/sitemap/llms.txt, SoftwareApplication
  JSON-LD) in Vecto's own design language (near-black, hairline borders, #0C8CE9).
- Download buttons resolve the latest GitHub Release assets via JS, with the releases
  page as no-JS fallback. Deploys via GitHub Actions (withastro/action) on push to main.

## v0.4.1 — 2026-07-08

### Dark UI theme (Toolcraft-inspired, original XAML)
- New Themes/Theme.xaml: near-black surfaces, hairline low-alpha borders, #0C8CE9 accent,
  Inter typography, custom templates for Button/ComboBox/CheckBox/Slider/ScrollBar/ToolTip,
  thin pill scrollbars, dark title bar via DWM. Layout unchanged (compact).
- Provenance: design-token values follow Pixel Point's Toolcraft (proprietary license);
  zero Toolcraft code copied — see DECISIONS.md. Repo stays clean for open-sourcing.

## v0.4.0 — 2026-07-08

### Perfect circles + straight-run extraction
- **Corner validation**: detected corners are re-measured on the refined sub-pixel
  geometry (where staircase quantization spikes vanish); false corners are demoted and
  the chain reprocessed. Small circles no longer get lattice-pinned dents — rings/dots
  now reach the whole-circle arc fit and come out as exact circles.
- **Arc-before-cubic** on spans ≥16 points; **greedy straight-run extraction** peels
  truly straight prefixes/suffixes off mixed ranges ([line][cap][line] with no corner);
  **collinear line merge** heals sides split by chain seams or max-error splits.
- Tests 14 green (new: rounded-box sides must be exactly 4 single lines; circle radial
  bound tightened to 0.8 px, segment cap 8).
- Resolution finding (bench, 1× vs 2× raster of the same ground truth): mean ΔE and
  wrong-pixel rates halve at 2× — input resolution is the cheapest quality lever.
- Photo sanity check: 4K Windows Bloom wallpaper → 24 colors, 2 251 regions, 54 594
  nodes in 4.5 s end-to-end.

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
