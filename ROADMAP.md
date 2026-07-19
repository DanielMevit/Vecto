# Roadmap — Vecto

## Done
- **Phase 0** — bootstrap (solution, playbook docs, CodeGraph, git dev/main)
- **Phase 1–2** — engine (planar boundary graph + Bezier fitting), CLI, 12 tests,
  VM-sample parity (96 nodes vs 96, identical palette)
- **Phase 3** — WPF app MVP (side-by-side compare, segmentation view, live re-trace,
  export/copy SVG)
- **v0.4.1 release + site** — first public GitHub Release (5 binaries); product site
  (`site/`, Astro → GitHub Pages); quick wins 2026-07-19: PNG export, nodes view,
  settings persistence, CLI batch mode

## Phase 4 — VM-fashion UX
- Wizard flow: fully-automatic vs basic (image type → detail → colors) vs advanced
- Palette editor: merge/pick/lock colors, custom palette (engine: PaletteMode.FixedColors)
- Background removal (flood from borders + color similarity, like VM 1.13)
- ~~Wireframe/nodes view (third right-pane mode)~~ **done 2026-07-19**
- ~~Batch mode in CLI (`vecto trace *.png --out-dir`)~~ **done 2026-07-19**

## Distribution
- Microsoft Store first publish — MSIX packaging; Laydown repo has AppxManifest.xml +
  certs + packaging scripts to crib from
- winget manifest (`winget install vecto`); demo GIF for README/site
- Code signing (SmartScreen) — costs money, decide separately

## Phase 5 — Cross-platform (Linux + macOS)
- Quick win: publish `vecto` CLI for linux-x64 / osx-x64 / osx-arm64 (Core+Cli are already
  cross-platform; three `dotnet publish -r` commands) and attach to Releases
- Port the app WPF → Avalonia UI (single codebase for Win/Linux/macOS; Core reused 100%,
  ViewModel ~90%, theme translates style-for-style). MAUI ruled out — no Linux support
- GitHub Actions release matrix (build all targets on tag push); macOS needs notarization
  (Apple dev account) to avoid Gatekeeper friction

## Phase 6 — Power features
- Exporters: PDF, EPS, DXF, AI (mirror VM's formats.dll split as Vecto.Formats)
- Segmentation editor: pencil/zap/fill-gaps (VM's edit-result tools)
- ~~PNG/bitmap export of the vector render~~ **done 2026-07-19** (1×; scale picker later)
- Copy vector to clipboard for design apps (SVG clipboard format works for Figma/Inkscape)

## Tuning backlog (quality, engine)
- ~~Sub-pixel edge placement from AA gradients~~ **done 2026-07-08** (`SubpixelRefine`;
  mean ΔE −40% across the ground-truth bench)
- ~~Line/arc primitive recognition~~ **done 2026-07-08** (`TryLine`/`TryArc` in the
  fitter: straight edges → single lines, circles/caps → exact arcs; band test enforces it)
- ~~Sub-pixel **corner** relocation + angle snapping~~ **done 2026-07-19** (flank-line
  intersection with exact 45°-multiple snap; bench meanΔE −1…−7%, >0.1 tail −4…−10%).
  **Junction relocation remains** — chain endpoints still pin to the lattice; needs a
  consistent multi-chain solve (all chains meeting at the junction must agree).
- Chamfered-AA corner detection: a 90° corner whose ~50%-coverage border pixels label
  ambiguously gets chamfered in the raster, and k=3/68° detection misses it → the corner
  rounds. Needs re-detection on refined geometry or multi-scale support (found 2026-07-19).
- Node economy: ~1.4–1.8× Vector Magic's counts at equal detail — smarter tangent
  estimation or two-pass fitting could close the rest
- Centerline/stroke recognition (constant-width shapes → path + stroke weight + round
  caps) — the "Tier 3" mode; big lift, transforms icon output quality
- Gentle-slope staircases: constrained line detection would beat Laplacian+fit on
  near-horizontal edges
- `MinRegionArea` scaling with image resolution; photo-mode color count heuristics
- Parallelize labeling/k-means further if large photos feel slow
