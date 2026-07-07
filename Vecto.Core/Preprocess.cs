namespace Vecto.Core;

internal static class Preprocess
{
    /// <summary>3×3 per-channel median — knocks down sensor noise/JPEG speckle before clustering.</summary>
    public static RasterImage Median3x3(RasterImage src, CancellationToken ct)
    {
        int w = src.Width, h = src.Height;
        var dst = new byte[w * h * 4];
        var sp = src.Pixels;
        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            Span<byte> r = stackalloc byte[9];
            Span<byte> g = stackalloc byte[9];
            Span<byte> b = stackalloc byte[9];
            for (int x = 0; x < w; x++)
            {
                int n = 0;
                for (int dy = -1; dy <= 1; dy++)
                {
                    int yy = y + dy;
                    if ((uint)yy >= (uint)h) continue;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx;
                        if ((uint)xx >= (uint)w) continue;
                        int si = (yy * w + xx) * 4;
                        r[n] = sp[si];
                        g[n] = sp[si + 1];
                        b[n] = sp[si + 2];
                        n++;
                    }
                }
                int di = (y * w + x) * 4;
                dst[di] = Median(r[..n]);
                dst[di + 1] = Median(g[..n]);
                dst[di + 2] = Median(b[..n]);
                dst[di + 3] = sp[di + 3];
            }
        });
        return new RasterImage(w, h, dst);
    }

    static byte Median(Span<byte> v)
    {
        v.Sort();
        return v[v.Length / 2];
    }
}
