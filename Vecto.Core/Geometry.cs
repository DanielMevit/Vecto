namespace Vecto.Core;

public readonly record struct Vec2(double X, double Y)
{
    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 a, double s) => new(a.X * s, a.Y * s);
    public static Vec2 operator -(Vec2 a) => new(-a.X, -a.Y);

    public double Dot(Vec2 b) => X * b.X + Y * b.Y;
    public double Length => Math.Sqrt(X * X + Y * Y);
    public double LengthSq => X * X + Y * Y;

    public Vec2 Normalized()
    {
        double len = Length;
        return len < 1e-12 ? new Vec2(0, 0) : new Vec2(X / len, Y / len);
    }

    public double DistSq(Vec2 b)
    {
        double dx = X - b.X, dy = Y - b.Y;
        return dx * dx + dy * dy;
    }
}

public readonly record struct CubicBezier(Vec2 P0, Vec2 P1, Vec2 P2, Vec2 P3)
{
    public Vec2 Eval(double t)
    {
        double mt = 1 - t;
        double a = mt * mt * mt, b = 3 * mt * mt * t, c = 3 * mt * t * t, d = t * t * t;
        return new Vec2(
            a * P0.X + b * P1.X + c * P2.X + d * P3.X,
            a * P0.Y + b * P1.Y + c * P2.Y + d * P3.Y);
    }

    /// <summary>Exact same geometry walked the other way — used so two adjacent regions share one fit.</summary>
    public CubicBezier Reversed() => new(P3, P2, P1, P0);

    public static CubicBezier Line(Vec2 a, Vec2 b)
    {
        var d = (b - a) * (1.0 / 3.0);
        return new CubicBezier(a, a + d, a + d + d, b);
    }

    public bool IsLine(double tol = 0.02)
    {
        double t2 = tol * tol;
        return DistToChordSq(P1) <= t2 && DistToChordSq(P2) <= t2;
    }

    double DistToChordSq(Vec2 p)
    {
        var ab = P3 - P0;
        double len = ab.LengthSq;
        double t = len < 1e-12 ? 0 : Math.Clamp((p - P0).Dot(ab) / len, 0, 1);
        return p.DistSq(P0 + ab * t);
    }
}
