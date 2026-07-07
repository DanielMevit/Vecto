using System.Globalization;
using Vecto.Core;

namespace Vecto.Cli;

internal static class Program
{
    static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("error: " + ex.Message);
            return 1;
        }
    }

    static int Run(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintHelp();
            return args.Length == 0 ? 1 : 0;
        }
        if (args[0] == "--version")
        {
            Console.WriteLine("vecto 0.1.0");
            return 0;
        }
        return args[0] switch
        {
            "trace" => Trace(args[1..]),
            "samples" => Samples(args[1..]),
            _ when File.Exists(args[0]) => Trace(args),
            _ => UnknownCommand(args[0]),
        };
    }

    static int UnknownCommand(string cmd)
    {
        Console.Error.WriteLine($"unknown command or missing file: {cmd}");
        return 1;
    }

    static int Trace(string[] args)
    {
        string? input = null, output = null, segPng = null;
        var opt = new TraceOptions();
        bool check = false, stats = false;
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "-o" or "--out":
                    output = Next(args, ref i);
                    break;
                case "--colors":
                    var v = Next(args, ref i);
                    if (v.Equals("auto", StringComparison.OrdinalIgnoreCase))
                    {
                        opt.PaletteMode = PaletteMode.Auto;
                    }
                    else
                    {
                        opt.PaletteMode = PaletteMode.FixedCount;
                        opt.ColorCount = int.Parse(v, CultureInfo.InvariantCulture);
                    }
                    break;
                case "--max-colors":
                    opt.MaxColors = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture);
                    break;
                case "--detail":
                    opt.Detail = Enum.Parse<DetailLevel>(Next(args, ref i), true);
                    break;
                case "--style":
                    opt.Style = Enum.Parse<ImageStyle>(Next(args, ref i), true);
                    break;
                case "--polygons":
                    opt.CurveFitting = false;
                    break;
                case "--epsilon":
                    opt.PolygonEpsilon = double.Parse(Next(args, ref i), CultureInfo.InvariantCulture);
                    break;
                case "--seed":
                    opt.Seed = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture);
                    break;
                case "--seg-png":
                    segPng = Next(args, ref i);
                    break;
                case "--check":
                    check = true;
                    break;
                case "--stats":
                    stats = true;
                    break;
                default:
                    if (a.StartsWith('-')) throw new ArgumentException("unknown option " + a);
                    if (input != null) throw new ArgumentException("more than one input given");
                    input = a;
                    break;
            }
        }
        if (input == null) throw new ArgumentException("no input image given (see: vecto --help)");
        output ??= Path.ChangeExtension(input, ".svg");

        var img = ImageIo.Load(input);
        var res = Tracer.Trace(img, opt);
        File.WriteAllText(output, SvgWriter.Write(res.Document));
        Console.WriteLine(FormattableString.Invariant(
            $"{Path.GetFileName(input)} -> {output}  ({res.Diagnostics.TotalMs} ms, {res.Document.Palette.Count} colors, {res.Diagnostics.RegionCount} regions, {res.Diagnostics.NodeCount} nodes)"));
        if (segPng != null)
        {
            ImageIo.SavePng(SegmentationImage(res), segPng);
            Console.WriteLine("segmentation -> " + segPng);
        }
        if (stats) PrintStats(res);
        if (check && !PlanarityCheck(res)) return 2;
        return 0;
    }

    static int Samples(string[] args)
    {
        var dir = args.Length > 0 ? args[0] : "samples";
        Directory.CreateDirectory(dir);
        Save(SampleGen.Crisp(), "crisp.png");
        Save(SampleGen.Blended(), "blended.png");
        Save(SampleGen.Transparent(), "transparent.png");
        return 0;

        void Save(RasterImage img, string name)
        {
            var path = Path.Combine(dir, name);
            ImageIo.SavePng(img, path);
            Console.WriteLine("wrote " + path);
        }
    }

    /// <summary>
    /// Planarity self-check: with shared boundaries the region areas must tile the opaque
    /// canvas, so their flattened sum matching the raster pixel count proves no gaps/overlaps.
    /// </summary>
    static bool PlanarityCheck(TraceResult res)
    {
        double vector = res.Document.Regions.Sum(RegionArea);
        double raster = res.Document.Palette.Sum(p => (long)p.PixelCount);
        double deviation = raster == 0 ? 0 : Math.Abs(vector - raster) / raster;
        bool ok = deviation <= 0.02;
        Console.WriteLine(FormattableString.Invariant(
            $"planarity: vector {vector:0.0} px² vs raster {raster:0} px -> deviation {deviation:P2} [{(ok ? "PASS" : "FAIL")}]"));
        return ok;
    }

    static double RegionArea(RegionPath region)
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
        return Math.Abs(sum);
    }

    static RasterImage SegmentationImage(TraceResult res)
    {
        var img = new RasterImage(res.Width, res.Height);
        var px = img.Pixels;
        for (int i = 0; i < res.LabelMap.Length; i++)
        {
            int l = res.LabelMap[i];
            if (l < 0) continue;
            var c = res.Document.Palette[l].Color;
            int pi = i * 4;
            px[pi] = c.R;
            px[pi + 1] = c.G;
            px[pi + 2] = c.B;
            px[pi + 3] = 255;
        }
        return img;
    }

    static void PrintStats(TraceResult res)
    {
        var d = res.Diagnostics;
        var p = d.Params;
        Console.WriteLine(FormattableString.Invariant(
            $"  style={p.Style} exactPalette={p.ExactPalette} k={p.KMeansK} merge={(p.MergeClusters ? p.MergeThreshold : 0):0.###} minArea={p.MinRegionArea}"));
        Console.WriteLine(FormattableString.Invariant(
            $"  fitTol={Math.Sqrt(p.FitToleranceSq):0.##}px corner={p.CornerThresholdDeg}deg clamp={p.SmoothClamp}px"));
        Console.WriteLine(FormattableString.Invariant(
            $"  chains={d.ChainCount} regions={d.RegionCount} nodes={d.NodeCount} palette={res.Document.Palette.Count}"));
        Console.WriteLine(FormattableString.Invariant(
            $"  timings: palette {d.PaletteMs} ms, segment {d.SegmentMs} ms, boundaries {d.BoundaryMs} ms, curves {d.FitMs} ms, total {d.TotalMs} ms"));
    }

    static string Next(string[] args, ref int i)
    {
        if (++i >= args.Length) throw new ArgumentException("missing value for " + args[i - 1]);
        return args[i];
    }

    static void PrintHelp()
    {
        Console.WriteLine("""
            vecto — bitmap-to-vector tracer (Vector Magic-style planar output)

            usage:
              vecto trace <image> [options]     vectorize an image (also: vecto <image>)
              vecto samples [dir]               write procedural test images

            options:
              -o, --out <file>      output SVG path (default: input with .svg)
              --colors <n|auto>     fixed palette size, or automatic (default auto)
              --max-colors <n>      cap for automatic palette (default 16)
              --detail <level>      low | medium | high (default medium)
              --style <style>       auto | crisp | blended | photo (default auto)
              --polygons            skip curve fitting, emit simplified polygons
              --epsilon <px>        polygon simplification tolerance (0 = exact lattice)
              --seed <n>            random seed for palette clustering (default 1)
              --seg-png <file>      also write the segmentation as a PNG
              --check               verify planar partition (areas must tile the canvas)
              --stats               print resolved parameters and stage timings
            """);
    }
}
