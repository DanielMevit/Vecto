namespace Vecto.Core;

public enum ImageStyle
{
    Auto,
    /// <summary>Aliased art (flat colors, hard edges) — palette taken verbatim when small enough.</summary>
    Crisp,
    /// <summary>Anti-aliased art — palette inferred from flat areas, fringes excluded.</summary>
    Blended,
    /// <summary>Continuous-tone input — denoised first, tighter cluster merging.</summary>
    Photo,
}

public enum DetailLevel { Low, Medium, High }

public enum PaletteMode { Auto, FixedCount }

public sealed class TraceOptions
{
    public PaletteMode PaletteMode { get; set; } = PaletteMode.Auto;
    /// <summary>Upper bound on inferred colors when <see cref="PaletteMode.Auto"/>.</summary>
    public int MaxColors { get; set; } = 16;
    /// <summary>Exact color count when <see cref="PaletteMode.FixedCount"/>.</summary>
    public int ColorCount { get; set; } = 12;
    public ImageStyle Style { get; set; } = ImageStyle.Auto;
    public DetailLevel Detail { get; set; } = DetailLevel.Medium;
    /// <summary>false → emit simplified polygons instead of fitted curves (debug/inspection).</summary>
    public bool CurveFitting { get; set; } = true;
    /// <summary>Polygon-mode simplification tolerance; negative → derive from Detail, 0 → exact lattice polygons.</summary>
    public double PolygonEpsilon { get; set; } = -1;
    public int Seed { get; set; } = 1;
}

/// <summary>Every tuning constant the pipeline actually runs with, resolved from options + image stats.</summary>
public sealed class EffectiveParams
{
    public ImageStyle Style;
    public bool ExactPalette;
    public bool ExcludeEdgeSamples;
    public int KMeansK;
    public bool MergeClusters;
    public double MergeThreshold;     // Oklab distance (black↔white ≈ 1.0)
    public int MinRegionArea;         // px; smaller regions are absorbed into a neighbor
    public bool MedianPrefilter;
    public int SmoothIterations;
    public double SmoothLambda;
    public double SmoothClamp;        // px; max drift of a boundary point from the raster crack
    public double CornerThresholdDeg;
    public int CornerSupport;         // vertices on each side used to measure the turn angle
    public double FitToleranceSq;     // px²
    public double PolygonEpsilon;
    public int Seed;
}
