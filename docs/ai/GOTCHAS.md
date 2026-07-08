# Gotchas — Vecto

## Environment (WSL working on /mnt/d)
- **Always build with `dotnet.exe`** (Windows .NET SDK via WSL interop), never the Linux
  `dotnet` — the WPF project (`net8.0-windows`) won't build on the Linux SDK. The Windows
  dotnet can't parse `/mnt/d/...` arguments, so run it from inside the repo with relative
  paths (`cd` first).
- **CodeGraph does not auto-sync here** — run `codegraph sync` after edits (live
  file-watching is unreliable on `/mnt` drives).
- **Push via `cmd.exe /c "git push origin dev"`** — git credentials live on the Windows side.
- `git config core.filemode false` is set — don't "fix" phantom mode diffs.

## Licensing / dependencies
- **SixLabors.ImageSharp must stay on 3.1.x** (`Version="3.1.*"` in Vecto.Cli.csproj).
  The 4.x line fails the build outright without a paid license key. If this ever becomes a
  problem, the fallback is System.Drawing.Common (Windows-only, fine for this app).
- Vector Magic's sample images (`C:\Program Files\Vector Magic\Samples`) are licensed
  sample content: fine for local benchmarking in `_bench/` (gitignored), **never commit them**.

## Engine
- `TreatWarningsAsErrors` is on solution-wide — new warnings break the build by design.
- SVG/CLI numeric output must go through InvariantCulture; the de-DE test will catch
  violations, but remember it for any new writer/exporter.
- Corner threshold is 68° for a geometric reason (false corners at staircase slope
  transitions measure 63.4° at k=3) — don't lower it without re-deriving the bound; see
  the comment in `Tracer.Resolve` and ALGORITHM_INDEX.md.
- `MinRegionArea` is in absolute pixels, tuned for logo-sized inputs (≤ ~2000px). Very
  large photos may need it scaled with resolution (tuning backlog).

## App
- `Vecto.exe --smoke` is a headless self-test (traces a generated image, writes
  `%TEMP%\vecto-smoke.svg`, exit 0/11) — WinExe has no console, so check the exit code.
- Crashes log to `%TEMP%\vecto-crash.log`.
