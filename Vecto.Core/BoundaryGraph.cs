namespace Vecto.Core;

/// <summary>A maximal boundary polyline separating exactly two regions of the label map.</summary>
internal sealed class Chain
{
    /// <summary>Pixel-corner lattice coordinates; closed chains repeat the first point at the end.</summary>
    public required List<Vec2> Points { get; init; }
    /// <summary>Region on the left when walking Points forward; -1 = void (transparent or outside).</summary>
    public int Left { get; init; }
    public int Right { get; init; }
    public bool Closed { get; init; }
    /// <summary>Fitted curve in Points order; both adjacent regions reuse it verbatim (one reversed).</summary>
    public List<CubicBezier>? Curve { get; set; }
}

internal readonly record struct ChainUse(int Chain, bool Forward);

internal sealed class BoundaryGraph
{
    public required List<Chain> Chains { get; init; }
    /// <summary>Per region: its boundary loops, each a cyclic sequence of chain traversals.</summary>
    public required List<List<ChainUse>>[] RegionLoops { get; init; }
}

/// <summary>
/// Extracts the planar boundary graph of a region map. Boundaries live on the "cracks"
/// between pixels; every chain between two junction points is traced exactly once and
/// shared by both adjacent regions, which structurally rules out gaps and overlaps —
/// the property that defines Vector Magic-style output.
/// </summary>
internal sealed class BoundaryTracer
{
    const int DirE = 0, DirS = 1, DirW = 2, DirN = 3;
    static readonly int[] Dx = { 1, 0, -1, 0 };
    static readonly int[] Dy = { 0, 1, 0, -1 };
    // successor preference at a junction, relative to the arrival direction:
    // sharpest left turn first, so contours pinch at checkerboard points instead of crossing
    static readonly int[] TurnOrder = { 3, 0, 1, 2 };

    readonly int[] _region;
    readonly int _w, _h, _regionCount;
    readonly bool[] _hVisited;   // horizontal crack (x,y)-(x+1,y): index y*w + x
    readonly bool[] _vVisited;   // vertical crack (x,y)-(x,y+1): index y*(w+1) + x

    public BoundaryTracer(int[] region, int w, int h, int regionCount)
    {
        _region = region;
        _w = w;
        _h = h;
        _regionCount = regionCount;
        _hVisited = new bool[(h + 1) * w];
        _vVisited = new bool[h * (w + 1)];
    }

    int Reg(int x, int y) => (uint)x < (uint)_w && (uint)y < (uint)_h ? _region[y * _w + x] : -1;

    bool SegExists(int x, int y, int dir) => dir switch
    {
        DirE => x < _w && Reg(x, y - 1) != Reg(x, y),
        DirS => y < _h && Reg(x - 1, y) != Reg(x, y),
        DirW => x > 0 && Reg(x - 1, y - 1) != Reg(x - 1, y),
        DirN => y > 0 && Reg(x - 1, y - 1) != Reg(x, y - 1),
        _ => false,
    };

    bool SegVisited(int x, int y, int dir) => dir switch
    {
        DirE => _hVisited[y * _w + x],
        DirS => _vVisited[y * (_w + 1) + x],
        DirW => _hVisited[y * _w + x - 1],
        DirN => _vVisited[(y - 1) * (_w + 1) + x],
        _ => true,
    };

    void MarkSeg(int x, int y, int dir)
    {
        switch (dir)
        {
            case DirE: _hVisited[y * _w + x] = true; break;
            case DirS: _vVisited[y * (_w + 1) + x] = true; break;
            case DirW: _hVisited[y * _w + x - 1] = true; break;
            case DirN: _vVisited[(y - 1) * (_w + 1) + x] = true; break;
        }
    }

    int Degree(int x, int y)
    {
        int d = 0;
        for (int dir = 0; dir < 4; dir++)
            if (SegExists(x, y, dir)) d++;
        return d;
    }

    /// <summary>(left, right) region of the crack leaving (x,y) toward dir. Left = left of travel, y-down.</summary>
    (int left, int right) Sides(int x, int y, int dir) => dir switch
    {
        DirE => (Reg(x, y - 1), Reg(x, y)),
        DirS => (Reg(x, y), Reg(x - 1, y)),
        DirW => (Reg(x - 1, y), Reg(x - 1, y - 1)),
        DirN => (Reg(x - 1, y - 1), Reg(x, y - 1)),
        _ => (-1, -1),
    };

    public BoundaryGraph Run(CancellationToken ct)
    {
        var chains = new List<Chain>();
        // open chains start and end at junction points (3+ regions meet)
        for (int y = 0; y <= _h; y++)
        {
            ct.ThrowIfCancellationRequested();
            for (int x = 0; x <= _w; x++)
            {
                if (Degree(x, y) < 3) continue;
                for (int dir = 0; dir < 4; dir++)
                    if (SegExists(x, y, dir) && !SegVisited(x, y, dir))
                        chains.Add(Walk(x, y, dir));
            }
        }
        // whatever is left over are pure cycles (a region enclosed by a single neighbor);
        // every cycle contains a horizontal crack, so sweeping those finds them all
        for (int y = 0; y <= _h; y++)
        {
            ct.ThrowIfCancellationRequested();
            for (int x = 0; x < _w; x++)
                if (SegExists(x, y, DirE) && !SegVisited(x, y, DirE))
                    chains.Add(Walk(x, y, DirE));
        }
        return new BoundaryGraph { Chains = chains, RegionLoops = Assemble(chains) };
    }

    Chain Walk(int startX, int startY, int startDir)
    {
        var pts = new List<Vec2> { new(startX, startY) };
        var (left, right) = Sides(startX, startY, startDir);
        int x = startX, y = startY, dir = startDir;
        bool closed = false;
        while (true)
        {
            MarkSeg(x, y, dir);
            x += Dx[dir];
            y += Dy[dir];
            pts.Add(new Vec2(x, y));
            if (Degree(x, y) != 2) break;   // reached a junction (covers loops hanging off one junction too)
            if (x == startX && y == startY) { closed = true; break; }
            int back = (dir + 2) & 3;
            for (int d = 0; d < 4; d++)
            {
                if (d == back || !SegExists(x, y, d)) continue;
                dir = d;
                break;
            }
        }
        return new Chain { Points = pts, Left = left, Right = right, Closed = closed };
    }

    List<List<ChainUse>>[] Assemble(List<Chain> chains)
    {
        var loops = new List<List<ChainUse>>[_regionCount];
        for (int r = 0; r < _regionCount; r++) loops[r] = new List<List<ChainUse>>();

        var byDeparture = new Dictionary<(int pt, int region, int dir), ChainUse>();
        for (int i = 0; i < chains.Count; i++)
        {
            var c = chains[i];
            if (c.Closed)
            {
                // a closed chain is a complete loop for each side on its own
                if (c.Left >= 0) loops[c.Left].Add(new List<ChainUse> { new(i, true) });
                if (c.Right >= 0) loops[c.Right].Add(new List<ChainUse> { new(i, false) });
                continue;
            }
            if (c.Left >= 0)
                byDeparture[(PtId(c.Points[0]), c.Left, DirBetween(c.Points[0], c.Points[1]))] = new ChainUse(i, true);
            if (c.Right >= 0)
                byDeparture[(PtId(c.Points[^1]), c.Right, Opp(DirBetween(c.Points[^2], c.Points[^1])))] = new ChainUse(i, false);
        }

        var seen = new HashSet<ChainUse>();
        for (int i = 0; i < chains.Count; i++)
        {
            var c = chains[i];
            if (c.Closed) continue;
            for (int side = 0; side < 2; side++)
            {
                bool forward = side == 0;
                int region = forward ? c.Left : c.Right;
                if (region < 0) continue;
                var start = new ChainUse(i, forward);
                if (seen.Contains(start)) continue;
                var loop = new List<ChainUse>();
                var cur = start;
                while (true)
                {
                    loop.Add(cur);
                    seen.Add(cur);
                    var (endPt, arriveDir) = ArrivalOf(cur, chains);
                    ChainUse next = default;
                    bool found = false;
                    for (int t = 0; t < 4 && !found; t++)
                        found = byDeparture.TryGetValue((endPt, region, (arriveDir + TurnOrder[t]) & 3), out next);
                    if (!found)
                        throw new InvalidOperationException("boundary graph inconsistency: no successor at junction");
                    if (next == start) break;
                    cur = next;
                }
                loops[region].Add(loop);
            }
        }
        return loops;
    }

    (int pt, int dir) ArrivalOf(ChainUse u, List<Chain> chains)
    {
        var pts = chains[u.Chain].Points;
        return u.Forward
            ? (PtId(pts[^1]), DirBetween(pts[^2], pts[^1]))
            : (PtId(pts[0]), Opp(DirBetween(pts[0], pts[1])));
    }

    int PtId(Vec2 p) => (int)p.Y * (_w + 1) + (int)p.X;

    static int Opp(int dir) => (dir + 2) & 3;

    static int DirBetween(Vec2 a, Vec2 b)
    {
        if (b.X > a.X) return DirE;
        if (b.X < a.X) return DirW;
        return b.Y > a.Y ? DirS : DirN;
    }
}
