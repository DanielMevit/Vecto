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
        foreach (var i in pinned)
            if (i >= 0 && i < n) pin[i] = true;
        if (!closed)
        {
            pin[0] = true;
            pin[n - 1] = true;
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
                var p = cur[i] + (target - cur[i]) * lambda;
                var drift = p - orig[i];
                if (drift.LengthSq > clampSq) p = orig[i] + drift.Normalized() * clamp;
                next[i] = p;
            }
            (cur, next) = (next, cur);
        }
        for (int i = 0; i < n; i++) pts[i] = cur[i];
        if (closed) pts[n] = pts[0];
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
