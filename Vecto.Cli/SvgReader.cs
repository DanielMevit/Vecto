using System.Globalization;
using System.Xml.Linq;
using Vecto.Core;

namespace Vecto.Cli;

/// <summary>
/// Minimal SVG reader for benchmark ground truth: flat `path` elements with hex fills and
/// M/L/H/V/C/Z data (absolute or relative). Covers Vector Magic's and Vecto's own output;
/// anything fancier (transforms, arcs, quadratics, gradients) throws with a clear message.
/// </summary>
public static class SvgReader
{
    public static VectorDocument ReadFile(string path)
    {
        var root = XDocument.Load(path).Root ?? throw new InvalidDataException("empty SVG");
        XNamespace ns = root.Name.Namespace;
        var (w, h) = Dimensions(root);
        var palette = new List<PaletteEntry>();
        var colorIndex = new Dictionary<uint, int>();
        var regions = new List<RegionPath>();
        foreach (var el in root.Descendants(ns + "path"))
        {
            if (el.Attribute("transform") != null)
                throw new NotSupportedException("SVG transforms are not supported");
            var fill = (string?)el.Attribute("fill") ?? "#000000";
            if (fill.Equals("none", StringComparison.OrdinalIgnoreCase)) continue;
            var color = ParseColor(fill);
            uint key = (uint)(color.R << 16 | color.G << 8 | color.B);
            if (!colorIndex.TryGetValue(key, out int idx))
            {
                idx = palette.Count;
                colorIndex[key] = idx;
                palette.Add(new PaletteEntry { Color = color, PixelCount = 0 });
            }
            var loops = ParsePathData((string?)el.Attribute("d") ?? "");
            if (loops.Count > 0)
                regions.Add(new RegionPath { PaletteIndex = idx, Area = 0, Loops = loops });
        }
        return new VectorDocument
        {
            Width = Math.Max(1, (int)Math.Round(w)),
            Height = Math.Max(1, (int)Math.Round(h)),
            Palette = palette,
            Regions = regions,
        };
    }

    static (double W, double H) Dimensions(XElement root)
    {
        var vb = (string?)root.Attribute("viewBox");
        if (vb != null)
        {
            var p = vb.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (p.Length == 4) return (Num(p[2]), Num(p[3]));
        }
        return (Length((string?)root.Attribute("width")), Length((string?)root.Attribute("height")));
    }

    static double Length(string? s) =>
        s == null ? 0 : Num(s.TrimEnd('p', 't', 'x', 'e', 'm', '%', ' '));

    static double Num(string s) => double.Parse(s, CultureInfo.InvariantCulture);

    static Rgba32 ParseColor(string s)
    {
        if (s.StartsWith('#'))
        {
            var hex = s[1..];
            if (hex.Length == 3)
                hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
            if (hex.Length == 6)
                return new Rgba32(
                    Convert.ToByte(hex[..2], 16),
                    Convert.ToByte(hex[2..4], 16),
                    Convert.ToByte(hex[4..6], 16),
                    255);
        }
        throw new NotSupportedException("unsupported fill color: " + s);
    }

    static List<List<CubicBezier>> ParsePathData(string d)
    {
        var loops = new List<List<CubicBezier>>();
        var segs = new List<CubicBezier>();
        var tokens = Tokenize(d);
        Vec2 cur = default, start = default;
        char cmd = '\0';
        int i = 0;

        void CloseLoop()
        {
            if (segs.Count > 0)
            {
                if (cur.DistSq(start) > 1e-12) segs.Add(CubicBezier.Line(cur, start));
                loops.Add(segs);
                segs = new List<CubicBezier>();
            }
            cur = start;
        }

        while (i < tokens.Count)
        {
            var tok = tokens[i];
            if (tok.Length == 1 && char.IsLetter(tok[0]))
            {
                cmd = tok[0];
                i++;
                if (cmd is 'Z' or 'z')
                {
                    CloseLoop();
                    continue;
                }
            }
            else if (cmd is 'M') cmd = 'L';   // implicit lineto after moveto
            else if (cmd is 'm') cmd = 'l';

            bool rel = char.IsLower(cmd);
            switch (char.ToUpperInvariant(cmd))
            {
                case 'M':
                    CloseLoop();   // an unclosed previous subpath closes implicitly for fills
                    cur = start = Pt(ref i, rel ? cur : default);
                    segs.Clear();
                    break;
                case 'L':
                {
                    var q = Pt(ref i, rel ? cur : default);
                    segs.Add(CubicBezier.Line(cur, q));
                    cur = q;
                    break;
                }
                case 'H':
                {
                    var q = new Vec2(Num(tokens[i++]) + (rel ? cur.X : 0), cur.Y);
                    segs.Add(CubicBezier.Line(cur, q));
                    cur = q;
                    break;
                }
                case 'V':
                {
                    var q = new Vec2(cur.X, Num(tokens[i++]) + (rel ? cur.Y : 0));
                    segs.Add(CubicBezier.Line(cur, q));
                    cur = q;
                    break;
                }
                case 'C':
                {
                    var origin = rel ? cur : default;
                    var c1 = Pt(ref i, origin);
                    var c2 = Pt(ref i, origin);
                    var e = Pt(ref i, origin);
                    segs.Add(new CubicBezier(cur, c1, c2, e));
                    cur = e;
                    break;
                }
                default:
                    throw new NotSupportedException($"SVG path command '{cmd}' not supported");
            }
        }
        CloseLoop();
        return loops;

        Vec2 Pt(ref int idx, Vec2 origin)
        {
            double x = Num(tokens[idx++]);
            double y = Num(tokens[idx++]);
            return new Vec2(origin.X + x, origin.Y + y);
        }
    }

    static List<string> Tokenize(string d)
    {
        var tokens = new List<string>();
        int i = 0;
        while (i < d.Length)
        {
            char c = d[i];
            if (char.IsWhiteSpace(c) || c == ',')
            {
                i++;
            }
            else if (char.IsLetter(c))
            {
                tokens.Add(c.ToString());
                i++;
            }
            else
            {
                int j = i;
                // a number: sign, digits, dot, exponent; a second '-' or '.' starts a new token
                bool seenDot = false, seenExp = false;
                while (j < d.Length)
                {
                    char n = d[j];
                    if (char.IsDigit(n)) { j++; continue; }
                    if (n == '.' && !seenDot && !seenExp) { seenDot = true; j++; continue; }
                    if ((n == 'e' || n == 'E') && !seenExp) { seenExp = true; j++; continue; }
                    if ((n == '+' || n == '-') && (j == i || d[j - 1] is 'e' or 'E')) { j++; continue; }
                    break;
                }
                if (j == i) throw new InvalidDataException($"bad path data near '{d[i..Math.Min(d.Length, i + 12)]}'");
                tokens.Add(d[i..j]);
                i = j;
            }
        }
        return tokens;
    }
}
