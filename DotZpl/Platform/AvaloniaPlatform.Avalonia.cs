using System.IO;

namespace DotZpl
{
    /// <summary>Avalonia (<c>Avalonia.Media</c>) implementation of <see cref="IPlatform"/>.</summary>
    internal sealed class AvaloniaPlatform : IPlatform
    {
        // Avalonia has no Geometry.Empty singleton; cache one so EmptyGeometry stays reference-stable.
        private readonly Geometry _empty = new StreamGeometry();

        public Geometry EmptyGeometry => _empty;

        public FillRule NonZeroFill => FillRule.NonZero;

        public Geometry Combine(Geometry a, Geometry b, GeometryCombineMode mode)
            // Avalonia 12's static Geometry.Combine restricts the second operand to RectangleGeometry,
            // so arbitrary shapes go through the lazy CombinedGeometry.
            => new CombinedGeometry(mode, a, b);

        public PathGeometry NewPathGeometry()
            => new PathGeometry { FillRule = FillRule.NonZero, Figures = new PathFigures() };

        public PathFigure NewFigure(Point start)
            => new PathFigure { StartPoint = start, IsClosed = true, Segments = new PathSegments() };

        public void AddLine(PathFigure figure, Point point)
            => figure.Segments!.Add(new LineSegment { Point = point });

        public void AddArc(PathFigure figure, Point endPoint, Size size, bool clockwise)
            => figure.Segments!.Add(new ArcSegment
            {
                Point = endPoint,
                Size = size,
                RotationAngle = 0,
                IsLargeArc = false,
                SweepDirection = clockwise ? SweepDirection.Clockwise : SweepDirection.CounterClockwise,
            });

        public void Begin(StreamGeometryContext ctx, Point point) => ctx.BeginFigure(point, true);

        public void Line(StreamGeometryContext ctx, Point point) => ctx.LineTo(point);

        public void End(StreamGeometryContext ctx) => ctx.EndFigure(true);

        public EllipseGeometry Ellipse(Point center, double radiusX, double radiusY)
            => new EllipseGeometry(new Rect(center.X - radiusX, center.Y - radiusY, radiusX * 2, radiusY * 2));

        public Matrix Prepend(Matrix current, Matrix inner) => inner * current;   // value1 is applied first

        public bool TryResolve(Typeface typeface) => FontManager.Current.TryGetGlyphTypeface(typeface, out _);

        public ushort GlyphIndex(GlyphTypeface glyphTypeface, char c)
            => glyphTypeface.CharacterToGlyphMap[c];   // missing codepoints map to glyph 0 (.notdef)

        public double GlyphAdvanceEm(GlyphTypeface glyphTypeface, ushort glyphIndex)
        {
            glyphTypeface.TryGetHorizontalGlyphAdvance(glyphIndex, out ushort advance);
            return advance / (double)glyphTypeface.Metrics.DesignEmHeight;
        }

        public double FontLineHeightEm(GlyphTypeface glyphTypeface)
        {
            FontMetrics m = glyphTypeface.Metrics;
            // Avalonia ascent is negative (above the baseline); descent positive (below).
            return (m.Descent - m.Ascent + m.LineGap) / (double)m.DesignEmHeight;
        }

        public BitmapSource DecodeImage(byte[] data)
        {
            using var ms = new MemoryStream(data);
            return new BitmapSource(ms);   // BitmapSource is aliased to Avalonia.Media.Imaging.Bitmap
        }

        public int PixelWidth(BitmapSource image) => image.PixelSize.Width;

        public int PixelHeight(BitmapSource image) => image.PixelSize.Height;

        public byte[] RenderToPng(Rendering.LabelDrawing label, bool antialias, int scale)
        {
            if (scale < 1)
            {
                scale = 1;
            }

            // The label draws at the native dot grid; a DPI of 96*scale makes Avalonia scale each DIP to
            // `scale` device pixels, so the buffer is `scale`× larger with crisp axis-aligned edges and
            // higher-resolution curves — the on-paper size is unchanged.
            var rtb = new RenderTargetBitmap(
                new PixelSize(label.Width * scale, label.Height * scale), new Vector(96.0 * scale, 96.0 * scale));
            using (DrawingContext ctx = rtb.CreateDrawingContext())
            {
                if (!antialias)
                {
                    using (ctx.PushRenderOptions(new RenderOptions
                    {
                        EdgeMode = EdgeMode.Aliased,
                        BitmapInterpolationMode = BitmapInterpolationMode.None,
                    }))
                    {
                        label.Draw(ctx);
                    }
                }
                else
                {
                    label.Draw(ctx);
                }
            }

            using var ms = new MemoryStream();
            rtb.Save(ms);
            return ms.ToArray();
        }
    }
}
