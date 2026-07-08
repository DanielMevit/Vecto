namespace Vecto.Core;

/// <summary>
/// Per-chain geometry passes that run between boundary extraction and curve fitting.
/// Everything here operates on one chain at a time, so results stay identical for the
/// two regions that share the chain.
/// </summary>
internal static class ChainGeometry
{
    /// <summary>
    /// Finds true corners on the raw staircase polyline by measuring the turn angle over a
    /// k-vertex support window. k=3 distinguishes real corners (~90°) from the ±45° zigzag
    /// of a diagonal pixel staircase (≤~37° at that support).
    /// </summary>
    public static List<int> DetectCorners(List<Vec2> pts, bool closed, double thresholdDeg, int k)
    {
        var result = new List<int>();
        int n = closed ? pts.Count - 1 : pts.Count;
        if (n < 2 * k + 1) return result;
        int lo = closed ? 0 : k, hi = closed ? n - 1 : n - 1 - k;
        var candidates = new List<(double Angle, int Index)>();
        for (int i = lo; i <= hi; i++)
        {
            var v1 = pts[i] - pts[Wrap(i - k)];
            var v2 = pts[Wrap(i + k)] - pts[i];
            double l1 = v1.Length, l2 = v2.Length;
            if (l1 < 1e-9 || l2 < 1e-9) continue;
            double cos = Math.Clamp(v1.Dot(v2) / (l1 * l2), -1.0, 1.0);
            double angle = Math.Acos(cos) * (180.0 / Math.PI);
            if (angle >= thresholdDeg) candidates.Add((angle, i));
        }
        // greedy non-maximum suppression: strongest corners win, none closer than k apart
        candidates.Sort((a, b) => a.Angle != b.Angle ? b.Angle.CompareTo(a.Angle) : a.Index.CompareTo(b.Index));
        foreach (var (_, i) in candidates)
            if (result.All(j => CyclicDist(i, j) > k))
                result.Add(i);
        result.Sort();
        return result;

        int Wrap(int i) => closed ? ((i % n) + n) % n : i;
        int CyclicDist(int a, int b)
        {
            int d = Math.Abs(a - b);
            return closed ? Math.Min(d, n - d) : d;
        }
    }

    /// <summary>
    /// Clamped Laplacian smoothing: flattens the staircase into sub-pixel positions while
    /// never drifting more than <paramref name="clamp"/> px from the raster boundary.
    /// Chain endpoints and detected corners stay pinned to their exact lattice points.
    /// </summary>
    public static void SmoothInPlace(List<Vec2> pts, bool closed, IReadOnlyList<int> pinned, int iterations, double lambda, double clamp)
    {
        int n = closed ? pts.Count - 1 : pts.Count;
        if (n < 3) return;
        var orig = new Vec2[n];
        for (int i = 0; i < n; i++) orig[i] = pts[i];
        var cur = (Vec2[])orig.Clone();
        var next = new Vec2[n];
        var pin = new bool[n];
        var lam = new double[n];
        Array.Fill(lam, lambda);
        foreach (var i in pinned)
            if (i >= 0 && i < n) pin[i] = true;
        if (!closed)
        {
            pin[0] = true;
            pin[n - 1] = true;
        }
        // damp smoothing next to pinned corners: full-strength Laplacian bulges the
        // flanks of sharp tips (thin features fatten into lobes)
        for (int i = 0; i < n; i++)
        {
            if (!pin[i]) continue;
            for (int d = 1; d <= 2; d++)
            {
                double factor = d == 1 ? 0.25 : 0.55;
                foreach (var j in new[] { i - d, i + d })
                {
                    int idx = closed ? ((j % n) + n) % n : j;
                    if (idx >= 0 && idx < n) lam[idx] = Math.Min(lam[idx], lambda * factor);
                }
            }
        }
        double clampSq = clamp * clamp;
        for (int it = 0; it < iterations; it++)
        {
            for (int i = 0; i < n; i++)
            {
                if (pin[i])
                {
                    next[i] = cur[i];
                    continue;
                }
                var prev = cur[i == 0 ? n - 1 : i - 1];
                var nxt = cur[i == n - 1 ? 0 : i + 1];
                var target = new Vec2((prev.X + nxt.X) * 0.5, (prev.Y + nxt.Y) * 0.5);
                var p = cur[i] + (target - cur[i]) * lam[i];
                var drift = p - orig[i];
                if (drift.LengthSq > clampSq) p = orig[i] + drift.Normalized() * clamp;
                next[i] = p;
            }
            (cur, next) = (next, cur);
        }
        for (int i = 0; i < n; i++) pts[i] = cur[i];
        if (closed) pts[n] = pts[0];
    }

    /// <summary>
    /// Sub-pixel edge refinement: slides each boundary point along its normal to where the
    /// source image's coverage crosses 50% between the two region colors. Anti-aliasing
    /// encodes the true edge position at sub-pixel precision — the crack lattice discards
    /// it, this recovers it. Corners/junctions stay pinned; per-chain, so planarity holds.
    /// </summary>
    public static void SubpixelRefine(List<Vec2> pts, bool closed, IReadOnlyList<int> pinned,
        RasterImage img, Rgba32? left, Rgba32? right, double maxShift)
    {
        if (left == null && right == null) return;
        int n = closed ? pts.Count - 1 : pts.Count;
        if (n < 3) return;
        var pin = new bool[n];
        foreach (var i in pinned)
            if (i >= 0 && i < n) pin[i] = true;
        if (!closed)
        {
            pin[0] = true;
            pin[n - 1] = true;
        }

        double axisR = 0, axisG = 0, axisB = 0, axisLenSq = 0;
        if (left is { } l && right is { } r)
        {
            axisR = l.R - r.R;
            axisG = l.G - r.G;
            axisB = l.B - r.B;
            axisLenSq = axisR * axisR + axisG * axisG + axisB * axisB;
            if (axisLenSq < 1) return;   // indistinguishable colors — nothing to measure
        }

        var result = new Vec2[n];
        for (int i = 0; i < n; i++)
        {
            var p = pts[i];
            result[i] = p;
            if (pin[i]) continue;
            var prev = pts[i == 0 ? (closed ? n - 1 : 0) : i - 1];
            var nxt = pts[i == n - 1 ? (closed ? 0 : n - 1) : i + 1];
            var tangent = (nxt - prev).Normalized();
            if (tangent.LengthSq < 0.5) continue;
            var normal = new Vec2(tangent.Y, -tangent.X);   // toward the LEFT region (y-down)

            double f0 = CoverLeft(p - normal);
            double f1 = CoverLeft(p);
            double f2 = CoverLeft(p + normal);
            double t;
            if ((f0 - 0.5) * (f1 - 0.5) <= 0 && Math.Abs(f1 - f0) > 1e-9)
                t = -1 + (0.5 - f0) / (f1 - f0);
            else if ((f1 - 0.5) * (f2 - 0.5) <= 0 && Math.Abs(f2 - f1) > 1e-9)
                t = (0.5 - f1) / (f2 - f1);
            else
                continue;
            if (double.IsNaN(t)) continue;
            result[i] = p + normal * Math.Clamp(t, -maxShift, maxShift);
        }
        for (int i = 0; i < n; i++) pts[i] = result[i];
        if (closed) pts[n] = pts[0];

        double CoverLeft(Vec2 q)
        {
            var (sr, sg, sb, sa) = Sample(img, q.X, q.Y);
            if (left == null) return 1 - sa;   // left side is transparency
            if (right == null) return sa;
            var rr = right.Value;
            double dot = (sr - rr.R) * axisR + (sg - rr.G) * axisG + (sb - rr.B) * axisB;
            return Math.Clamp(dot / axisLenSq, 0, 1);
        }
    }

    static (double R, double G, double B, double A) Sample(RasterImage img, double x, double y)
    {
        double u = x - 0.5, v = y - 0.5;   // pixel centers sit at +0.5 in crack coordinates
        int x0 = (int)Math.Floor(u), y0 = (int)Math.Floor(v);
        double fx = u - x0, fy = v - y0;
        double sr = 0, sg = 0, sb = 0, sa = 0;
        for (int dy = 0; dy <= 1; dy++)
        {
            for (int dx = 0; dx <= 1; dx++)
            {
                int xi = Math.Clamp(x0 + dx, 0, img.Width - 1);
                int yi = Math.Clamp(y0 + dy, 0, img.Height - 1);
                double w = (dx == 0 ? 1 - fx : fx) * (dy == 0 ? 1 - fy : fy);
                int pi = (yi * img.Width + xi) * 4;
                sr += w * img.Pixels[pi];
                sg += w * img.Pixels[pi + 1];
                sb += w * img.Pixels[pi + 2];
                sa += w * img.Pixels[pi + 3];
            }
        }
        return (sr, sg, sb, sa / 255.0);
    }

    /// <summary>Douglas–Peucker with distance-to-segment (handles closed chains where first == last).</summary>
    public static List<Vec2> SimplifyDp(List<Vec2> pts, double epsilon)
    {
        if (epsilon <= 0 || pts.Count <= 2) return new List<Vec2>(pts);
        var keep = new bool[pts.Count];
        keep[0] = keep[^1] = true;
        var stack = new Stack<(int A, int B)>();
        stack.Push((0, pts.Count - 1));
        double epsSq = epsilon * epsilon;
        while (stack.Count > 0)
        {
            var (a, b) = stack.Pop();
            if (b - a < 2) continue;
            double worst = -1;
            int at = -1;
            for (int i = a + 1; i < b; i++)
            {
                double d = DistToSegmentSq(pts[i], pts[a], pts[b]);
                if (d > worst) { worst = d; at = i; }
            }
            if (worst > epsSq)
            {
                keep[at] = true;
                stack.Push((a, at));
                stack.Push((at, b));
            }
        }
        var outPts = new List<Vec2>();
        for (int i = 0; i < pts.Count; i++)
            if (keep[i]) outPts.Add(pts[i]);
        return outPts;
    }

    static double DistToSegmentSq(Vec2 p, Vec2 a, Vec2 b)
    {
        var ab = b - a;
        double len = ab.LengthSq;
        double t = len < 1e-12 ? 0 : Math.Clamp((p - a).Dot(ab) / len, 0, 1);
        return p.DistSq(a + ab * t);
    }
}
