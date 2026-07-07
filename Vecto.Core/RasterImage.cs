namespace Vecto.Core;

public readonly record struct Rgba32(byte R, byte G, byte B, byte A);

/// <summary>RGBA8888 bitmap, row-major, 4 bytes per pixel. The engine's only input type.</summary>
public sealed class RasterImage
{
    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }

    public RasterImage(int width, int height, byte[]? pixels = null)
    {
        if (width < 1 || height < 1)
            throw new ArgumentOutOfRangeException(nameof(width), "image must be at least 1×1");
        Width = width;
        Height = height;
        Pixels = pixels ?? new byte[width * height * 4];
        if (Pixels.Length != width * height * 4)
            throw new ArgumentException("pixel buffer must be width*height*4 bytes", nameof(pixels));
    }

    public Rgba32 GetPixel(int x, int y)
    {
        int i = (y * Width + x) * 4;
        return new Rgba32(Pixels[i], Pixels[i + 1], Pixels[i + 2], Pixels[i + 3]);
    }

    public void SetPixel(int x, int y, Rgba32 c)
    {
        int i = (y * Width + x) * 4;
        Pixels[i] = c.R;
        Pixels[i + 1] = c.G;
        Pixels[i + 2] = c.B;
        Pixels[i + 3] = c.A;
    }
}
