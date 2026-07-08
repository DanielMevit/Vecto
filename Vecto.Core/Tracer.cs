using System.Diagnostics;

namespace Vecto.Core;

public sealed class PaletteEntry
{
    public Rgba32 Color { get; init; }
    public int PixelCount { get; init; }
}

public sealed class RegionPath
{
    public int PaletteIndex { get; init; }
    /// <summary>Exact pixel count of the region.</summary>
    public int Area { get; init; }
    /// <summary>Outer boundary + holes; holes wind opposite, so nonzero fill renders correctly.</summary>
    public required List<List<CubicBezier>> Loops { get; init; }
}

public sealed class VectorDocument
{
    public int Width { get; init; }
    public int Height { get; init; }
    public required List<PaletteEntry> Palette { get; init; }
    /// <summary>Largest area first, so backgrounds paint below details regardless of renderer.</summary>
    public required List<RegionPath> Regions { get; init; }
}

public sealed class TraceDiagnostics
{
    public required EffectiveParams Params { get; init; }
    public int RegionCount { get; init; }
    public int ChainCount { get; init; }
    public int NodeCount { get; init; }
    public long PaletteMs { get; init; }
    public long SegmentMs { get; init; }
    public long BoundaryMs { get; init; }
    public long FitMs { get; init; }
    public long TotalMs { get; set; }
}

public sealed class TraceResult
{
    public required VectorDocument Document { get; init; }
    /// <summary>Palette index per pixel (-1 transparent) — feeds the segmentation view.</summary>
    public required int[] LabelMap { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public required TraceDiagnostics Diagnostics { get; init; }
}

/// <summary>
/// The pipeline: palette → label map → small-region absorption → planar boundary graph →
/// per-chain corner detection, smoothing, Bezier fitting → document assembly.
/// Every stage is deterministic for a given input + options.
/// </summary>
public static class Tracer
{
    public static TraceResult Trace(RasterImage image, TraceOptions options, CancellationToken ct = default, Action<string, double>? progress = null)
    {
        var total = Stopwatch.StartNew();
        var p = Resolve(options, image);
        var img = p.MedianPrefilter ? Preprocess.Median3x3(image, ct) : image;
        int w = img.Width, h = img.Height;

        progress?.Invoke("palette", 0.0);
        var sw = Stopwatch.StartNew();
        var centers = PaletteBuilder.Build(img, p, ct);
        long paletteMs = sw.ElapsedMilliseconds;

        progress?.Invoke("segmentation", 0.25);
        sw.Restart();
        var labels = Segmenter.Label(img, centers, ct);
        int opaque = 0;
        for (int i = 0; i < labels.Length; i++)
            if (labels[i] >= 0) opaque++;
        int minArea = Math.Min(p.MinRegionArea, Math.Max(1, opaque / 4));
        Segmenter.AbsorbSmallRegions(labels, w, h, minArea, ct);
        var palette = CompactPalette(img, labels, centers.Length);
        var seg = Segmenter.Components(labels, w, h);
        long segmentMs = sw.ElapsedMilliseconds;

        progress?.Invoke("boundaries", 0.45);
        sw.Restart();
        var graph = new BoundaryTracer(seg.RegionId, w, h, seg.RegionCount).Run(ct);
        long boundaryMs = sw.ElapsedMilliseconds;

        progress?.Invoke("curves", 0.6);
        sw.Restart();
        Rgba32? ColorOf(int region) => region < 0 ? null : palette[seg.RegionPalette[region]].Color;
        Parallel.ForEach(graph.Chains, new ParallelOptions { CancellationToken = ct }, chain =>
        {
            if (options.CurveFitting)
            {
                var corners = ChainGeometry.DetectCorners(chain.Points, chain.Closed, p.CornerThresholdDeg, p.CornerSupport);
                ChainGeometry.SmoothInPlace(chain.Points, chain.Closed, corners, p.SmoothIterations, p.SmoothLambda, p.SmoothClamp);
                ChainGeometry.SubpixelRefine(chain.Points, chain.Closed, corners, img,
                    ColorOf(chain.Left), ColorOf(chain.Right), p.SubpixelMaxShift);
                // light second pass: per-point refinement estimates jitter; smoothing them
                // (clamped to the refined positions) costs <0.25px but many fewer nodes
                ChainGeometry.SmoothInPlace(chain.Points, chain.Closed, corners, 3, 0.5, 0.25);
                chain.Curve = BezierFitter.FitChain(chain.Points, chain.Closed, corners, p.FitToleranceSq);
            }
            else
            {
                var pts = ChainGeometry.SimplifyDp(chain.Points, p.PolygonEpsilon);
                var segs = new List<CubicBezier>(pts.Count - 1);
                for (int i = 0; i + 1 < pts.Count; i++) segs.Add(CubicBezier.Line(pts[i], pts[i + 1]));
                chain.Curve = segs;
            }
        });
        long fitMs = sw.ElapsedMilliseconds;

        progress?.Invoke("emit", 0.9);
        var regionOrder = Enumerable.Range(0, seg.RegionCount)
            .OrderByDescending(r => seg.RegionArea[r])
            .ToList();
        var regions = new List<RegionPath>(seg.RegionCount);
        int nodes = 0;
        foreach (var r in regionOrder)
        {
            var loops = graph.RegionLoops[r].Select(loop => BuildLoop(loop, graph.Chains)).ToList();
            nodes += loops.Sum(l => l.Count);
            regions.Add(new RegionPath
            {
                PaletteIndex = seg.RegionPalette[r],
                Area = seg.RegionArea[r],
                Loops = loops,
            });
        }
        var doc = new VectorDocument { Width = w, Height = h, Palette = palette, Regions = regions };
        var diag = new TraceDiagnostics
        {
            Params = p,
            RegionCount = seg.RegionCount,
            ChainCount = graph.Chains.Count,
            NodeCount = nodes,
            PaletteMs = paletteMs,
            SegmentMs = segmentMs,
            BoundaryMs = boundaryMs,
            FitMs = fitMs,
        };
        diag.TotalMs = total.ElapsedMilliseconds;
        return new TraceResult { Document = doc, LabelMap = labels, Width = w, Height = h, Diagnostics = diag };
    }

    static List<CubicBezier> BuildLoop(List<ChainUse> loop, List<Chain> chains)
    {
        var segs = new List<CubicBezier>();
        foreach (var use in loop)
        {
            var curve = chains[use.Chain].Curve!;
            if (use.Forward)
            {
                segs.AddRange(curve);
            }
            else
            {
                for (int i = curve.Count - 1; i >= 0; i--) segs.Add(curve[i].Reversed());
            }
        }
        return segs;
    }

    /// <summary>
    /// Drops empty palette entries and computes display colors. Display colors average only
    /// flat (non-edge) pixels when a cluster has enough of them, so anti-aliasing fringes
    /// can't tint the fill — a pure white area stays #ffffff, like Vector Magic's output.
    /// </summary>
    static List<PaletteEntry> CompactPalette(RasterImage img, int[] labels, int centerCount)
    {
        if (centerCount == 0) return new List<PaletteEntry>();
        int w = img.Width;
        var sum = new long[centerCount, 3];
        var flatSum = new long[centerCount, 3];
        var count = new int[centerCount];
        var flatCount = new int[centerCount];
        var px = img.Pixels;
        for (int i = 0; i < labels.Length; i++)
        {
            int l = labels[i];
            if (l < 0) continue;
            int pi = i * 4;
            sum[l, 0] += px[pi];
            sum[l, 1] += px[pi + 1];
            sum[l, 2] += px[pi + 2];
            count[l]++;
            if (!PaletteBuilder.IsEdge(img, i % w, i / w))
            {
                flatSum[l, 0] += px[pi];
                flatSum[l, 1] += px[pi + 1];
                flatSum[l, 2] += px[pi + 2];
                flatCount[l]++;
            }
        }
        var map = new int[centerCount];
        var entries = new List<PaletteEntry>();
        for (int c = 0; c < centerCount; c++)
        {
            if (count[c] == 0)
            {
                map[c] = -1;
                continue;
            }
            bool useFlat = flatCount[c] >= Math.Max(1, count[c] / 50);
            long n = useFlat ? flatCount[c] : count[c];
            var src = useFlat ? flatSum : sum;
            map[c] = entries.Count;
            entries.Add(new PaletteEntry
            {
                Color = new Rgba32(
                    (byte)(src[c, 0] / n),
                    (byte)(src[c, 1] / n),
                    (byte)(src[c, 2] / n),
                    255),
                PixelCount = count[c],
            });
        }
        for (int i = 0; i < labels.Length; i++)
            if (labels[i] >= 0) labels[i] = map[labels[i]];
        return entries;
    }

    static EffectiveParams Resolve(TraceOptions o, RasterImage img)
    {
        int unique = CountUniqueOpaque(img, 1025);
        var style = o.Style != ImageStyle.Auto ? o.Style
            : unique <= 64 ? ImageStyle.Crisp
            : ImageStyle.Blended;
        int cap = Math.Clamp(o.PaletteMode == PaletteMode.FixedCount ? o.ColorCount : o.MaxColors, 1, 64);
        bool exact = style == ImageStyle.Crisp && unique <= cap;
        var (minLow, minMed, minHigh) = style switch
        {
            ImageStyle.Photo => (24, 12, 6),
            ImageStyle.Crisp => (10, 5, 2),
            _ => (14, 6, 3),
        };
        return new EffectiveParams
        {
            Style = style,
            ExactPalette = exact,
            ExcludeEdgeSamples = style is ImageStyle.Blended or ImageStyle.Photo,
            KMeansK = cap,
            MergeClusters = o.PaletteMode == PaletteMode.Auto && !exact,
            MergeThreshold = style == ImageStyle.Photo ? 0.03 : 0.055,
            MinRegionArea = o.Detail switch { DetailLevel.Low => minLow, DetailLevel.High => minHigh, _ => minMed },
            MedianPrefilter = style == ImageStyle.Photo,
            SmoothIterations = 8,
            SmoothLambda = 0.55,
            SmoothClamp = 0.6,
            // must exceed 63.4°: a shallow↔steep staircase transition measures as
            // (3,0) vs (1,2) at k=3 support and would otherwise pin false corners on circles
            CornerThresholdDeg = 68,
            CornerSupport = 3,
            SubpixelMaxShift = style == ImageStyle.Crisp ? 0.35 : 0.75,
            FitToleranceSq = o.Detail switch { DetailLevel.Low => 1.0, DetailLevel.High => 0.09, _ => 0.25 },
            PolygonEpsilon = o.PolygonEpsilon >= 0 ? o.PolygonEpsilon
                : o.Detail switch { DetailLevel.Low => 1.6, DetailLevel.High => 0.4, _ => 0.8 },
            Seed = o.Seed,
        };
    }

    static int CountUniqueOpaque(RasterImage img, int cap)
    {
        var set = new HashSet<uint>();
        var px = img.Pixels;
        for (int i = 0; i < px.Length; i += 4)
        {
            if (px[i + 3] < Segmenter.OpaqueThreshold) continue;
            set.Add((uint)(px[i] << 16 | px[i + 1] << 8 | px[i + 2]));
            if (set.Count >= cap) break;
        }
        return set.Count;
    }
}
