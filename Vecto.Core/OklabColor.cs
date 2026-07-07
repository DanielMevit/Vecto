namespace Vecto.Core;

public readonly record struct Lab(float L, float A, float B);

/// <summary>
/// sRGB → Oklab (Björn Ottosson's public-domain transform). Used for all perceptual
/// color distances; display colors stay in sRGB so no inverse is needed.
/// </summary>
public static class Oklab
{
    static readonly float[] ToLinear = BuildLut();

    static float[] BuildLut()
    {
        var lut = new float[256];
        for (int i = 0; i < 256; i++)
        {
            double c = i / 255.0;
            lut[i] = (float)(c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4));
        }
        return lut;
    }

    public static Lab FromRgb(byte r8, byte g8, byte b8)
    {
        float r = ToLinear[r8], g = ToLinear[g8], b = ToLinear[b8];
        float l = 0.4122214708f * r + 0.5363325363f * g + 0.0514459929f * b;
        float m = 0.2119034982f * r + 0.6806995451f * g + 0.1073969566f * b;
        float s = 0.0883024619f * r + 0.2817188376f * g + 0.6299787005f * b;
        float l_ = MathF.Cbrt(l), m_ = MathF.Cbrt(m), s_ = MathF.Cbrt(s);
        return new Lab(
            0.2104542553f * l_ + 0.7936177850f * m_ - 0.0040720468f * s_,
            1.9779984951f * l_ - 2.4285922050f * m_ + 0.4505937099f * s_,
            0.0259040371f * l_ + 0.7827717662f * m_ - 0.8086757660f * s_);
    }

    public static float DistSq(in Lab a, in Lab b)
    {
        float dl = a.L - b.L, da = a.A - b.A, db = a.B - b.B;
        return dl * dl + da * da + db * db;
    }
}
