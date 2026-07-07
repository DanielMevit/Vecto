using Vecto.Core;

namespace Vecto.Cli;

/// <summary>
/// Procedural test images (no external assets): a crisp aliased logo, the same shapes
/// anti-aliased, and an anti-aliased emblem on a transparent background. The rotated bar
/// crosses the other shapes so the boundary graph gets real junctions to chew on.
/// </summary>
public static class SampleGen
{
    public static RasterImage Crisp(int size = 320) => Shapes(size, aa: false, transparentBg: false);
    public static RasterImage Blended(int size = 320) => Shapes(size, aa: true, transparentBg: false);
    public static RasterImage Transparent(int size = 320) => Shapes(size, aa: true, transparentBg: true);

    static RasterImage Shapes(int size, bool aa, bool transparentBg)
    {
        var img = new RasterImage(size, size);
        double s = size / 320.0;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                double px = x + 0.5, py = y + 0.5;
                var c = transparentBg ? (R: 0.0, G: 0.0, B: 0.0, A: 0.0) : (R: 0.97, G: 0.97, B: 0.96, A: 1.0);
                c = Blend(c, (0.08, 0.27, 0.78), Coverage(Disc(px, py, 105 * s, 105 * s, 62 * s), aa));
                c = Blend(c, (0.88, 0.23, 0.23), Coverage(RoundBox(px - 215 * s, py - 215 * s, 55 * s, 55 * s, 16 * s), aa));
                c = Blend(c, (0.12, 0.65, 0.35), Coverage(Bar(px, py, 160 * s, 160 * s, 110 * s, 14 * s, 45 * Math.PI / 180, 4 * s), aa));
                img.SetPixel(x, y, new Rgba32(
                    (byte)Math.Round(c.R * 255),
                    (byte)Math.Round(c.G * 255),
                    (byte)Math.Round(c.B * 255),
                    (byte)Math.Round(c.A * 255)));
            }
        }
        return img;
    }

    static double Coverage(double sdf, bool aa) =>
        aa ? Math.Clamp(sdf / 1.5 + 0.5, 0, 1) : (sdf > 0 ? 1 : 0);

    static (double R, double G, double B, double A) Blend(
        (double R, double G, double B, double A) under, (double R, double G, double B) over, double cov)
    {
        if (cov <= 0) return under;
        double a = cov + under.A * (1 - cov);
        if (a <= 0) return (0, 0, 0, 0);
        double w = under.A * (1 - cov);
        return (
            (over.R * cov + under.R * w) / a,
            (over.G * cov + under.G * w) / a,
            (over.B * cov + under.B * w) / a,
            a);
    }

    // signed distance fields, positive inside
    static double Disc(double x, double y, double cx, double cy, double r) =>
        r - Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));

    static double RoundBox(double dx, double dy, double hx, double hy, double round)
    {
        double qx = Math.Abs(dx) - (hx - round), qy = Math.Abs(dy) - (hy - round);
        double ox = Math.Max(qx, 0), oy = Math.Max(qy, 0);
        double outside = Math.Sqrt(ox * ox + oy * oy);
        double inside = Math.Min(Math.Max(qx, qy), 0);
        return round - (outside + inside);
    }

    static double Bar(double x, double y, double cx, double cy, double hx, double hy, double angle, double round)
    {
        double dx = x - cx, dy = y - cy;
        double ca = Math.Cos(angle), sa = Math.Sin(angle);
        return RoundBox(ca * dx + sa * dy, -sa * dx + ca * dy, hx, hy, round);
    }
}
