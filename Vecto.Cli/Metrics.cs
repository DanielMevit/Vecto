using Vecto.Core;

namespace Vecto.Cli;

public sealed class CompareResult
{
    public double MeanDe { get; init; }
    public double RmsDe { get; init; }
    public double MaxDe { get; init; }
    /// <summary>Fraction of pixels above the just-noticeable Oklab distance (0.02).</summary>
    public double PctOverJnd { get; init; }
    /// <summary>Fraction of pixels that are clearly wrong (Oklab distance > 0.1).</summary>
    public double PctOverBig { get; init; }
    public required RasterImage DiffImage { get; init; }
}

/// <summary>Per-pixel perceptual comparison of two same-size images, plus a heatmap.</summary>
public static class Metrics
{
    const double AlphaMismatchDe = 0.35;
    const byte OpaqueThreshold = 128;   // matches the engine's alpha cut

    public static CompareResult Compare(RasterImage a, RasterImage b)
    {
        if (a.Width != b.Width || a.Height != b.Height)
            throw new ArgumentException($"size mismatch: {a.Width}x{a.Height} vs {b.Width}x{b.Height}");
        var diff = new RasterImage(a.Width, a.Height);
        var pa = a.Pixels;
        var pb = b.Pixels;
        var pd = diff.Pixels;
        double sum = 0, sumSq = 0, max = 0;
        long overJnd = 0, overBig = 0;
        int n = a.Width * a.Height;
        for (int i = 0; i < n; i++)
        {
            int pi = i * 4;
            bool oa = pa[pi + 3] >= OpaqueThreshold;
            bool ob = pb[pi + 3] >= OpaqueThreshold;
            double de;
            if (!oa && !ob)
            {
                de = 0;
            }
            else if (oa != ob)
            {
                de = AlphaMismatchDe;
            }
            else
            {
                de = Math.Sqrt(Oklab.DistSq(
                    Oklab.FromRgb(pa[pi], pa[pi + 1], pa[pi + 2]),
                    Oklab.FromRgb(pb[pi], pb[pi + 1], pb[pi + 2])));
            }
            sum += de;
            sumSq += de * de;
            if (de > max) max = de;
            if (de > 0.02) overJnd++;
            if (de > 0.1) overBig++;
            double v = Math.Min(1.0, de / 0.2);
            pd[pi] = 255;
            pd[pi + 1] = (byte)(255 * (1 - v));
            pd[pi + 2] = (byte)(255 * (1 - v));
            pd[pi + 3] = 255;
        }
        return new CompareResult
        {
            MeanDe = sum / n,
            RmsDe = Math.Sqrt(sumSq / n),
            MaxDe = max,
            PctOverJnd = (double)overJnd / n,
            PctOverBig = (double)overBig / n,
            DiffImage = diff,
        };
    }
}
