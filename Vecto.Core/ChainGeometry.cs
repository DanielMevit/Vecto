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

    /// <summary>Turn angle (degrees) at one vertex over a ±k window; 0 when out of range.</summary>
    public static double TurnAngleAt(List<Vec2> pts, bool closed, int i, int k)
    {
        int n = closed ? pts.Count - 1 : pts.Count;
        if (n < 2 * k + 1) return 0;
        if (!closed && (i < k || i > n - 1 - k)) return 0;
        int Wrap(int j) => closed ? ((j % n) + n) % n : j;
        var v1 = pts[i] - pts[Wrap(i - k)];
        var v2 = pts[Wrap(i + k)] - pts[i];
        double l1 = v1.Length, l2 = v2.Length;
        if (l1 < 1e-9 || l2 < 1e-9) return 0;
        double cos = Math.Clamp(v1.Dot(v2) / (l1 * l2), -1.0, 1.0);
        return Math.Acos(cos) * (180.0 / Math.PI);
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

    /// <summary>
    /// Sub-pixel corner relocation: each surviving corner moves from its lattice pin to the
    /// intersection of the two flank lines fitted to the refined geometry on either side.
    /// Flank directions within <paramref name="snapDeg"/> of a 45° multiple snap exactly —
    /// and both corners of a shared straight side fit the identical line, so snapped sides
    /// come out mathematically straight. Chain-local, so planarity holds. Junctions (chain
    /// endpoints) stay pinned: consistent relocation there needs a multi-chain solve (ROADMAP).
    /// </summary>
    public static void RelocateCorners(List<Vec2> pts, bool closed, IReadOnlyList<int> corners, double maxShift, double snapDeg)
    {
        int n = closed ? pts.Count - 1 : pts.Count;
        if (n < 7 || corners.Count == 0) return;
        var pins = new SortedSet<int>(corners);
        if (!closed)
        {
            pins.Add(0);
            pins.Add(n - 1);
        }

        var moved = new List<(int Index, Vec2 P)>(corners.Count);
        foreach (var c in corners)
        {
            var left = FlankLine(c, -1);
            var right = FlankLine(c, +1);
            if (left is not { } l || right is not { } r) continue;
            double cross = l.Dir.X * r.Dir.Y - l.Dir.Y * r.Dir.X;
            if (Math.Abs(cross) < 0.2) continue;   // near-parallel flanks — not a trustworthy corner
            double t = ((r.Pt.X - l.Pt.X) * r.Dir.Y - (r.Pt.Y - l.Pt.Y) * r.Dir.X) / cross;
            var q = new Vec2(l.Pt.X + l.Dir.X * t, l.Pt.Y + l.Dir.Y * t);
            if (q.DistSq(pts[c]) > maxShift * maxShift) continue;   // implausible jump — keep the pin
            moved.Add((c, q));
            // the shoulders' own 50%-crossings are bent by the transverse edge's AA this
            // close to a corner — project them onto the flank lines they were excluded from
            Shoulder(c, -1, l);
            Shoulder(c, +1, r);
        }
        foreach (var (i, p) in moved) pts[i] = p;
        if (closed) pts[n] = pts[0];

        void Shoulder(int c, int dir, (Vec2 Pt, Vec2 Dir) line)
        {
            int j = c + dir;
            int w = closed ? ((j % n) + n) % n : j;
            if (w < 0 || w >= n || pins.Contains(w)) return;
            var p = pts[w];
            var proj = line.Pt + line.Dir * (p - line.Pt).Dot(line.Dir);
            if (proj.DistSq(p) <= maxShift * maxShift) moved.Add((w, proj));
        }

        (Vec2 Pt, Vec2 Dir)? FlankLine(int c, int dir)
        {
            // the run from this corner to the next pin, excluding both pins and one vertex
            // beside each (the smoothing-damped, AA-contaminated shoulders)
            var idx = new List<int>();
            for (int steps = 1; steps <= n; steps++)
            {
                int j = c + dir * steps;
                int w = closed ? ((j % n) + n) % n : j;
                if (w < 0 || w >= n) break;
                if (pins.Contains(w)) break;
                idx.Add(w);
            }
            if (idx.Count >= 2) idx.RemoveAt(0);
            if (idx.Count >= 2) idx.RemoveAt(idx.Count - 1);
            if (idx.Count < 2) return null;

            // straight run → fit every vertex (both corners of the side then share one line);
            // curved run → only the four vertices nearest the corner
            var a = pts[idx[0]];
            var b = pts[idx[^1]];
            var chord = b - a;
            double len2 = chord.LengthSq;
            bool straight = true;
            if (len2 > 1e-12)
            {
                foreach (var w in idx)
                {
                    double tt = Math.Clamp((pts[w] - a).Dot(chord) / len2, 0, 1);
                    if (pts[w].DistSq(a + chord * tt) > 0.16)   // 0.4 px
                    {
                        straight = false;
                        break;
                    }
                }
            }
            if (!straight && idx.Count > 4) idx.RemoveRange(4, idx.Count - 4);

            double mx = 0, my = 0;
            foreach (var w in idx)
            {
                mx += pts[w].X;
                my += pts[w].Y;
            }
            mx /= idx.Count;
            my /= idx.Count;
            double cxx = 0, cxy = 0, cyy = 0;
            foreach (var w in idx)
            {
                double dx = pts[w].X - mx, dy = pts[w].Y - my;
                cxx += dx * dx;
                cxy += dx * dy;
                cyy += dy * dy;
            }
            if (cxx + cyy < 1e-12) return null;
            double theta = 0.5 * Math.Atan2(2 * cxy, cxx - cyy);
            double deg = theta * (180.0 / Math.PI);
            double snapped = Math.Round(deg / 45.0) * 45.0;
            if (Math.Abs(deg - snapped) <= snapDeg) theta = snapped * (Math.PI / 180.0);
            return (new Vec2(mx, my), new Vec2(Math.Cos(theta), Math.Sin(theta)));
        }
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
