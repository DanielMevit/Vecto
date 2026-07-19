using System.Globalization;
using Vecto.Cli;
using Vecto.Core;
using Xunit;

namespace Vecto.Tests;

public class TracerTests
{
    static readonly Rgba32 White = new(255, 255, 255, 255);
    static readonly Rgba32 Red = new(220, 40, 40, 255);
    static readonly Rgba32 Blue = new(20, 60, 200, 255);
    static readonly Rgba32 Green = new(30, 160, 70, 255);
    static readonly Rgba32 Black = new(0, 0, 0, 255);

    static RasterImage Make(int w, int h, Func<int, int, Rgba32> f)
    {
        var img = new RasterImage(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                img.SetPixel(x, y, f(x, y));
        return img;
    }

    /// <summary>Polygon mode with zero epsilon: exact lattice geometry, so areas are exact.</summary>
    static TraceOptions Polygons() => new() { CurveFitting = false, PolygonEpsilon = 0 };

    static double DocArea(VectorDocument doc)
    {
        double total = 0;
        foreach (var region in doc.Regions)
        {
            double sum = 0;
            foreach (var loop in region.Loops)
            {
                if (loop.Count == 0) continue;
                double signed = 0;
                var prev = loop[0].P0;
                foreach (var seg in loop)
                {
                    for (int s = 1; s <= 16; s++)
                    {
                        var cur = seg.Eval(s / 16.0);
                        signed += prev.X * cur.Y - cur.X * prev.Y;
                        prev = cur;
                    }
                }
                sum += signed * 0.5;
            }
            total += Math.Abs(sum);
        }
        return total;
    }

    static IEnumerable<Vec2> FlattenRegion(RegionPath region)
    {
        foreach (var loop in region.Loops)
            foreach (var seg in loop)
                for (int s = 0; s <= 16; s++)
                    yield return seg.Eval(s / 16.0);
    }

    [Fact]
    public void SolidImage_OneRegion_ExactArea()
    {
        var doc = Tracer.Trace(Make(64, 64, (_, _) => Red), Polygons()).Document;
        Assert.Single(doc.Regions);
        Assert.Equal(64 * 64, DocArea(doc), 3);
    }

    [Fact]
    public void RedSquareOnWhite_PlanarPartition_CornersExact()
    {
        var img = Make(96, 96, (x, y) => x is >= 24 and < 72 && y is >= 24 and < 72 ? Red : White);
        var doc = Tracer.Trace(img, Polygons()).Document;
        Assert.Equal(2, doc.Regions.Count);
        Assert.Equal(96 * 96, DocArea(doc), 3);
        var red = doc.Regions.Single(r => doc.Palette[r.PaletteIndex].Color.G < 128);
        var pts = FlattenRegion(red).ToList();
        foreach (var corner in new[] { new Vec2(24, 24), new Vec2(72, 24), new Vec2(72, 72), new Vec2(24, 72) })
            Assert.Contains(pts, p => p.DistSq(corner) < 1e-12);
    }

    [Fact]
    public void Square_CurveMode_CornersStaySharp()
    {
        var img = Make(96, 96, (x, y) => x is >= 24 and < 72 && y is >= 24 and < 72 ? Red : White);
        var doc = Tracer.Trace(img, new TraceOptions()).Document;
        var red = doc.Regions.OrderBy(r => r.Area).First();
        var pts = FlattenRegion(red).ToList();
        foreach (var corner in new[] { new Vec2(24, 24), new Vec2(72, 24), new Vec2(72, 72), new Vec2(24, 72) })
            Assert.Contains(pts, p => p.DistSq(corner) < 0.25);
    }

    [Fact]
    public void Checkerboard_PlanarPartitionExact()
    {
        var img = Make(64, 64, (x, y) => ((x / 8 + y / 8) & 1) == 0 ? White : Black);
        var doc = Tracer.Trace(img, Polygons()).Document;
        Assert.Equal(64, doc.Regions.Count);
        Assert.Equal(64 * 64, DocArea(doc), 3);
    }

    [Fact]
    public void ThreeRegionJunction_ExactPlanarPartition()
    {
        var img = Make(90, 90, (x, y) => y >= 45 ? Green : x < 45 ? Red : Blue);
        var doc = Tracer.Trace(img, Polygons()).Document;
        Assert.Equal(3, doc.Regions.Count);
        Assert.Equal(90 * 90, DocArea(doc), 3);
    }

    [Fact]
    public void Circle_FewNodes_LowRadialError()
    {
        const double cx = 128, cy = 128, radius = 90;
        var img = Make(256, 256, (x, y) =>
        {
            double d = Math.Sqrt((x + 0.5 - cx) * (x + 0.5 - cx) + (y + 0.5 - cy) * (y + 0.5 - cy));
            double cov = Math.Clamp(radius - d + 0.5, 0, 1);
            byte Mix(byte a, byte b) => (byte)Math.Round(a + (b - a) * cov);
            return new Rgba32(Mix(White.R, Blue.R), Mix(White.G, Blue.G), Mix(White.B, Blue.B), 255);
        });
        var doc = Tracer.Trace(img, new TraceOptions()).Document;
        Assert.Equal(2, doc.Regions.Count);
        var circle = doc.Regions.OrderBy(r => r.Area).First();
        var loop = Assert.Single(circle.Loops);
        Assert.InRange(loop.Count, 3, 8);
        foreach (var p in FlattenRegion(circle))
        {
            double r = Math.Sqrt((p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy));
            Assert.InRange(r, radius - 0.8, radius + 0.8);
        }
    }

    [Fact]
    public void RoundedBox_SidesBecomeSingleLines()
    {
        var img = Make(220, 220, (x, y) =>
        {
            double qx = Math.Abs(x + 0.5 - 110) - 60, qy = Math.Abs(y + 0.5 - 110) - 60;
            double ox = Math.Max(qx, 0), oy = Math.Max(qy, 0);
            double dist = Math.Sqrt(ox * ox + oy * oy) + Math.Min(Math.Max(qx, qy), 0) - 20;
            return dist <= 0 ? Red : White;
        });
        var doc = Tracer.Trace(img, new TraceOptions()).Document;
        var box = doc.Regions.Single(r => doc.Palette[r.PaletteIndex].Color.G < 128);
        var loop = Assert.Single(box.Loops);
        int longLines = loop.Count(s => s.IsLine(0.1) && (s.P3 - s.P0).Length > 70);
        Assert.Equal(4, longLines);
        Assert.True(loop.Count <= 14, $"loop has {loop.Count} segments");
    }

    [Fact]
    public void DiagonalBand_StraightEdgesBecomeSingleLines()
    {
        // crisp 30° band across the whole canvas: its long edges must collapse to true
        // straight lines (one segment each), not chains of wobbly cubics
        double sin = Math.Sin(Math.PI / 6), cos = Math.Cos(Math.PI / 6);
        var img = Make(200, 200, (x, y) =>
        {
            double dist = (y + 0.5 - 100) * cos - (x + 0.5 - 100) * sin;
            return Math.Abs(dist) <= 15 ? Black : White;
        });
        var doc = Tracer.Trace(img, new TraceOptions()).Document;
        var band = doc.Regions.Single(r => doc.Palette[r.PaletteIndex].Color.R < 128);
        var loop = Assert.Single(band.Loops);
        Assert.True(loop.Count <= 8, $"band loop has {loop.Count} segments");
        int longLines = loop.Count(s => s.IsLine(0.1) && (s.P3 - s.P0).Length > 100);
        Assert.Equal(2, longLines);
    }

    [Fact]
    public void AaBox_EdgesSnapToExactAxisLines_AtSubpixelPositions()
    {
        // axis-aligned box with sub-pixel borders (x 12.25–47.75, y 15.75–44.25), anti-aliased:
        // corner relocation + angle snap must yield exactly horizontal/vertical single lines
        // at the positions the AA encodes — not lattice-snapped, not tilted
        var img = Make(60, 60, (x, y) =>
        {
            double px = x + 0.5, py = y + 0.5;
            double cov = Math.Clamp(Math.Min(px - 12.25, 47.75 - px) + 0.5, 0, 1)
                       * Math.Clamp(Math.Min(py - 15.75, 44.25 - py) + 0.5, 0, 1);
            byte Mix(byte a, byte b) => (byte)Math.Round(a + (b - a) * cov);
            return new Rgba32(Mix(White.R, Red.R), Mix(White.G, Red.G), Mix(White.B, Red.B), 255);
        });
        // fixed 2-color palette: this test is about corner geometry, not palette inference
        var opt = new TraceOptions { PaletteMode = PaletteMode.FixedCount, ColorCount = 2 };
        var doc = Tracer.Trace(img, opt).Document;
        var box = doc.Regions.Single(r => doc.Palette[r.PaletteIndex].Color.G < 128);
        var loop = Assert.Single(box.Loops);
        var lines = loop.Where(s => s.IsLine(0.05) && (s.P3 - s.P0).Length > 20).ToList();
        Assert.Equal(4, lines.Count);
        foreach (var line in lines)
        {
            bool horizontal = Math.Abs(line.P3.Y - line.P0.Y) < 1e-6;
            bool vertical = Math.Abs(line.P3.X - line.P0.X) < 1e-6;
            Assert.True(horizontal || vertical,
                $"line ({line.P0.X:0.###},{line.P0.Y:0.###})->({line.P3.X:0.###},{line.P3.Y:0.###}) is neither exactly horizontal nor vertical");
            if (horizontal)
                Assert.True(Math.Abs(line.P0.Y - 15.75) < 0.2 || Math.Abs(line.P0.Y - 44.25) < 0.2,
                    $"horizontal edge at y={line.P0.Y:0.###} is not at a true edge position");
            else
                Assert.True(Math.Abs(line.P0.X - 12.25) < 0.2 || Math.Abs(line.P0.X - 47.75) < 0.2,
                    $"vertical edge at x={line.P0.X:0.###} is not at a true edge position");
        }
    }

    [Fact]
    public void TransparentBackground_OnlyOpaqueShapesEmitted()
    {
        var res = Tracer.Trace(SampleGen.Transparent(), Polygons());
        double raster = res.Document.Palette.Sum(p => (long)p.PixelCount);
        Assert.True(res.Document.Regions.Count >= 1);
        Assert.True(raster < 320.0 * 320.0 * 0.75, "background must not be part of the raster count");
        Assert.Equal(raster, DocArea(res.Document), 3);
    }

    [Fact]
    public void Speckles_AbsorbedIntoBackground()
    {
        var rnd = new Random(7);
        var img = Make(128, 128, (_, _) => White);
        for (int i = 0; i < 40; i++)
            img.SetPixel(rnd.Next(128), rnd.Next(128), Black);
        var doc = Tracer.Trace(img, new TraceOptions()).Document;
        Assert.Single(doc.Regions);
        Assert.Single(doc.Palette);
    }

    [Fact]
    public void CrispImage_PaletteIsExact()
    {
        var colors = new[] { White, Red, Blue, Green, Black };
        var img = Make(100, 50, (x, _) => colors[Math.Min(x / 20, 4)]);
        var doc = Tracer.Trace(img, new TraceOptions()).Document;
        Assert.Equal(5, doc.Palette.Count);
        Assert.All(colors, c => Assert.Contains(doc.Palette, p => p.Color == c));
        Assert.Equal(5, doc.Regions.Count);
    }

    [Fact]
    public void Curves_PlanarWithinHalfPercent()
    {
        var res = Tracer.Trace(SampleGen.Blended(), new TraceOptions());
        double raster = res.Document.Palette.Sum(p => (long)p.PixelCount);
        double deviation = Math.Abs(DocArea(res.Document) - raster) / raster;
        Assert.InRange(deviation, 0, 0.005);
    }

    [Fact]
    public void Trace_IsDeterministic()
    {
        var img = SampleGen.Blended(200);
        var a = SvgWriter.Write(Tracer.Trace(img, new TraceOptions()).Document);
        var b = SvgWriter.Write(Tracer.Trace(img, new TraceOptions()).Document);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Svg_IsCultureInvariant()
    {
        var prevDefault = CultureInfo.DefaultThreadCurrentCulture;
        var prevCurrent = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var svg = SvgWriter.Write(Tracer.Trace(SampleGen.Blended(160), new TraceOptions()).Document);
            Assert.DoesNotContain(",", svg);
            Assert.Contains("viewBox=\"0 0 160 160\"", svg);
        }
        finally
        {
            CultureInfo.DefaultThreadCurrentCulture = prevDefault;
            CultureInfo.CurrentCulture = prevCurrent;
        }
    }
}
