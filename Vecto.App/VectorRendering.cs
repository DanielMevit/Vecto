using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vecto.Core;

namespace Vecto.App;

internal static class VectorRendering
{
    /// <summary>Vector document → WPF DrawingImage; stays crisp at any zoom because it re-renders as geometry.</summary>
    public static ImageSource ToDrawingImage(VectorDocument doc)
    {
        var group = new DrawingGroup();
        // transparent canvas rect keeps bounds equal to the full image even when shapes don't cover it
        group.Children.Add(new GeometryDrawing(
            Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, doc.Width, doc.Height))));
        foreach (var region in doc.Regions)
        {
            var geometry = new StreamGeometry { FillRule = FillRule.Nonzero };
            using (var g = geometry.Open())
            {
                foreach (var loop in region.Loops)
                {
                    if (loop.Count == 0) continue;
                    g.BeginFigure(P(loop[0].P0), isFilled: true, isClosed: true);
                    foreach (var seg in loop)
                        g.BezierTo(P(seg.P1), P(seg.P2), P(seg.P3), isStroked: false, isSmoothJoin: false);
                }
            }
            geometry.Freeze();
            var c = doc.Palette[region.PaletteIndex].Color;
            var brush = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));
            brush.Freeze();
            group.Children.Add(new GeometryDrawing(brush, null, geometry));
        }
        group.Freeze();
        var image = new DrawingImage(group);
        image.Freeze();
        return image;

        static Point P(Vec2 v) => new(v.X, v.Y);
    }

    public static BitmapSource ToSegmentationBitmap(TraceResult result)
    {
        int w = result.Width, h = result.Height;
        var buf = new byte[w * h * 4];
        for (int i = 0; i < result.LabelMap.Length; i++)
        {
            int l = result.LabelMap[i];
            if (l < 0) continue;
            var c = result.Document.Palette[l].Color;
            int pi = i * 4;
            buf[pi] = c.B;
            buf[pi + 1] = c.G;
            buf[pi + 2] = c.R;
            buf[pi + 3] = 255;
        }
        var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, buf, w * 4);
        bmp.Freeze();
        return bmp;
    }
}
