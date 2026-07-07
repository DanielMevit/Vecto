namespace Vecto.Core;

/// <summary>
/// Least-squares cubic Bezier fitting after Philip J. Schneider, "An Algorithm for
/// Automatically Fitting Digitized Curves" (Graphics Gems, 1990): fit with chord-length
/// parameterization, Newton-Raphson reparameterization, recursive split at max error.
/// Corners and junctions become segment boundaries with one-sided tangents so they stay sharp.
/// </summary>
internal static class BezierFitter
{
    public static List<CubicBezier> FitChain(List<Vec2> pts, bool closed, List<int> corners, double tolSq)
    {
        var output = new List<CubicBezier>();
        if (pts.Count < 2) return output;

        if (closed && corners.Count > 0)
        {
            // rotate the ring so a corner is the seam, then fit corner-to-corner like an open chain
            int n = pts.Count - 1, c0 = corners[0];
            var rotated = new List<Vec2>(pts.Count);
            for (int i = 0; i <= n; i++) rotated.Add(pts[(c0 + i) % n]);
            var splits = corners
                .Select(c => ((c - c0) % n + n) % n)
                .Where(c => c != 0)
                .OrderBy(c => c)
                .ToList();
            FitOpen(rotated, splits, tolSq, output);
        }
        else if (closed)
        {
            int n = pts.Count - 1;
            if (n < 2) return output;
            // wrap-around tangent keeps the seam C1-continuous
            var t1 = (pts[1] - pts[n - 1]).Normalized();
            if (t1.LengthSq < 0.5) t1 = (pts[1] - pts[0]).Normalized();
            FitCubic(pts, 0, pts.Count - 1, t1, -t1, tolSq, output, 0);
        }
        else
        {
            FitOpen(pts, corners, tolSq, output);
        }
        return output;
    }

    static void FitOpen(List<Vec2> pts, List<int> splits, double tolSq, List<CubicBezier> output)
    {
        int prev = 0;
        foreach (var s in splits.Append(pts.Count - 1))
        {
            if (s <= prev) continue;
            FitCubic(pts, prev, s, LeftTangent(pts, prev, s), RightTangent(pts, s, prev), tolSq, output, 0);
            prev = s;
        }
    }

    static Vec2 LeftTangent(List<Vec2> pts, int i, int limit)
    {
        for (int j = i + 1; j <= limit; j++)
        {
            var t = pts[j] - pts[i];
            if (t.LengthSq > 1e-12) return t.Normalized();
        }
        return new Vec2(1, 0);
    }

    static Vec2 RightTangent(List<Vec2> pts, int i, int limit)
    {
        for (int j = i - 1; j >= limit; j--)
        {
            var t = pts[j] - pts[i];
            if (t.LengthSq > 1e-12) return t.Normalized();
        }
        return new Vec2(-1, 0);
    }

    static void FitCubic(List<Vec2> d, int first, int last, Vec2 tHat1, Vec2 tHat2, double tolSq, List<CubicBezier> output, int depth)
    {
        if (last - first + 1 == 2)
        {
            output.Add(HeuristicSegment(d[first], d[last], tHat1, tHat2));
            return;
        }
        if (depth > 28)
        {
            for (int i = first; i < last; i++) output.Add(CubicBezier.Line(d[i], d[i + 1]));
            return;
        }
        var u = ChordLengthParameterize(d, first, last);
        var bez = GenerateBezier(d, first, last, u, tHat1, tHat2);
        var (err, split) = ComputeMaxError(d, first, last, bez, u);
        if (err < tolSq)
        {
            output.Add(bez);
            return;
        }
        if (err < tolSq * 16)
        {
            for (int i = 0; i < 4; i++)
            {
                u = Reparameterize(d, first, last, u, bez);
                bez = GenerateBezier(d, first, last, u, tHat1, tHat2);
                (err, split) = ComputeMaxError(d, first, last, bez, u);
                if (err < tolSq)
                {
                    output.Add(bez);
                    return;
                }
            }
        }
        var tCenter = (d[split - 1] - d[split + 1]).Normalized();
        if (tCenter.LengthSq < 0.5) tCenter = (d[first] - d[last]).Normalized();
        if (tCenter.LengthSq < 0.5) tCenter = new Vec2(0, 1);
        FitCubic(d, first, split, tHat1, tCenter, tolSq, output, depth + 1);
        FitCubic(d, split, last, -tCenter, tHat2, tolSq, output, depth + 1);
    }

    static CubicBezier HeuristicSegment(Vec2 p0, Vec2 p3, Vec2 t1, Vec2 t2)
    {
        double dist = (p3 - p0).Length / 3.0;
        return new CubicBezier(p0, p0 + t1 * dist, p3 + t2 * dist, p3);
    }

    static double[] ChordLengthParameterize(List<Vec2> d, int first, int last)
    {
        var u = new double[last - first + 1];
        for (int i = 1; i < u.Length; i++)
            u[i] = u[i - 1] + (d[first + i] - d[first + i - 1]).Length;
        double total = u[^1];
        if (total < 1e-12)
        {
            for (int i = 0; i < u.Length; i++) u[i] = i / (double)(u.Length - 1);
            return u;
        }
        for (int i = 1; i < u.Length; i++) u[i] /= total;
        return u;
    }

    static CubicBezier GenerateBezier(List<Vec2> d, int first, int last, double[] u, Vec2 tHat1, Vec2 tHat2)
    {
        double c00 = 0, c01 = 0, c11 = 0, x0 = 0, x1 = 0;
        for (int i = 0; i < u.Length; i++)
        {
            double t = u[i], mt = 1 - t;
            double b0 = mt * mt * mt, b1 = 3 * mt * mt * t, b2 = 3 * mt * t * t, b3 = t * t * t;
            var a0 = tHat1 * b1;
            var a1 = tHat2 * b2;
            c00 += a0.Dot(a0);
            c01 += a0.Dot(a1);
            c11 += a1.Dot(a1);
            var tmp = d[first + i] - (d[first] * (b0 + b1) + d[last] * (b2 + b3));
            x0 += a0.Dot(tmp);
            x1 += a1.Dot(tmp);
        }
        double det = c00 * c11 - c01 * c01;
        double alphaL = 0, alphaR = 0;
        if (Math.Abs(det) > 1e-12)
        {
            alphaL = (x0 * c11 - x1 * c01) / det;
            alphaR = (c00 * x1 - c01 * x0) / det;
        }
        double segLen = (d[last] - d[first]).Length;
        double eps = 1e-6 * segLen;
        if (alphaL < eps || alphaR < eps || alphaL > 3 * segLen || alphaR > 3 * segLen)
        {
            double dist = segLen / 3.0;
            alphaL = alphaR = dist;
        }
        return new CubicBezier(d[first], d[first] + tHat1 * alphaL, d[last] + tHat2 * alphaR, d[last]);
    }

    static (double Err, int Split) ComputeMaxError(List<Vec2> d, int first, int last, CubicBezier bez, double[] u)
    {
        double max = 0;
        int split = (first + last) / 2;
        for (int i = 1; i < last - first; i++)
        {
            double distSq = bez.Eval(u[i]).DistSq(d[first + i]);
            if (distSq > max)
            {
                max = distSq;
                split = first + i;
            }
        }
        split = Math.Clamp(split, first + 1, last - 1);
        return (max, split);
    }

    static double[] Reparameterize(List<Vec2> d, int first, int last, double[] u, CubicBezier bez)
    {
        var u2 = new double[u.Length];
        for (int i = 0; i < u.Length; i++)
            u2[i] = NewtonRaphson(bez, d[first + i], u[i]);
        return u2;
    }

    static double NewtonRaphson(CubicBezier q, Vec2 p, double u)
    {
        var d10 = (q.P1 - q.P0) * 3;
        var d11 = (q.P2 - q.P1) * 3;
        var d12 = (q.P3 - q.P2) * 3;
        var d20 = (d11 - d10) * 2;
        var d21 = (d12 - d11) * 2;
        double mt = 1 - u;
        var qu = q.Eval(u);
        var q1u = d10 * (mt * mt) + d11 * (2 * mt * u) + d12 * (u * u);
        var q2u = d20 * mt + d21 * u;
        var diff = qu - p;
        double num = diff.Dot(q1u);
        double den = q1u.Dot(q1u) + diff.Dot(q2u);
        if (Math.Abs(den) < 1e-12) return u;
        return Math.Clamp(u - num / den, 0, 1);
    }
}
