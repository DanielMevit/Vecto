# Vecto

**Pixels in, curves out.** A Windows desktop bitmap-to-vector tracer in the fashion of
Vector Magic: full-color **planar** vectorization — every boundary between two shapes is
fitted once and shared by both, so the output has no hairline gaps and no overlaps.

C# / .NET 8 / WPF. Engine (`Vecto.Core`) is dependency-free and headless; `vecto` CLI and
a WPF app sit on top.

```
dotnet.exe build -c Release
./Vecto.Cli/bin/Release/net8.0/vecto.exe trace logo.png -o logo.svg --stats
"./Vecto.App/bin/Release/net8.0-windows/Vecto.exe"
```

- App: open/paste/drop an image → automatic trace → side-by-side compare with synced zoom,
  segmentation view, palette swatches → export or copy SVG.
- Engine pipeline and parameters: `docs/ai/ALGORITHM_INDEX.md`
- Benchmark vs Vector Magic's own sample output: identical palette, 8 paths vs 8,
  96 nodes vs 96, planarity deviation 0.00%.

Start reading at `AGENTS.md` → `docs/ai/START_HERE.md`.
