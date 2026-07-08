# Roadmap — Vecto

## Done
- **Phase 0** — bootstrap (solution, playbook docs, CodeGraph, git dev/main)
- **Phase 1–2** — engine (planar boundary graph + Bezier fitting), CLI, 12 tests,
  VM-sample parity (96 nodes vs 96, identical palette)
- **Phase 3** — WPF app MVP (side-by-side compare, segmentation view, live re-trace,
  export/copy SVG)

## Phase 4 — VM-fashion UX
- Wizard flow: fully-automatic vs basic (image type → detail → colors) vs advanced
- Palette editor: merge/pick/lock colors, custom palette (engine: PaletteMode.FixedColors)
- Background removal (flood from borders + color similarity, like VM 1.13)
- Wireframe/nodes view (third right-pane mode)
- Batch mode in CLI (`vecto trace *.png --out-dir`)

## Phase 5 — Power features
- Exporters: PDF, EPS, DXF, AI (mirror VM's formats.dll split as Vecto.Formats)
- Segmentation editor: pencil/zap/fill-gaps (VM's edit-result tools)
- PNG/bitmap export of the vector render
- Copy vector to clipboard for design apps (SVG clipboard format works for Figma/Inkscape)

## Tuning backlog (quality, engine)
- ~~Sub-pixel edge placement from AA gradients~~ **done 2026-07-08** (`SubpixelRefine`;
  mean ΔE −40% across the ground-truth bench)
- Node economy: still ~1.5–2× Vector Magic's node counts at equal detail — smarter
  tangent estimation or two-pass fitting could close it
- Sub-pixel corner/junction relocation (corners and junctions stay pinned to integer
  lattice points; diff hotspots concentrate there)
- Gentle-slope staircases: constrained line detection would beat Laplacian+fit on
  near-horizontal edges
- `MinRegionArea` scaling with image resolution; photo-mode color count heuristics
- Parallelize labeling/k-means further if large photos feel slow
