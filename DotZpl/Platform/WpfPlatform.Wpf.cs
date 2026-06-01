using System.IO;

namespace DotZpl
{
    /// <summary>WPF (<c>System.Windows.Media</c>) implementation of <see cref="IPlatform"/>.</summary>
    internal sealed class WpfPlatform : IPlatform
    {
        public Geometry EmptyGeometry => Geometry.Empty;   // a process-wide singleton, stable by reference

        public FillRule NonZeroFill => FillRule.Nonzero;

        public Geometry Combine(Geometry a, Geometry b, GeometryCombineMode mode)
            // Eager: returns a flat PathGeometry — no CombinedGeometry tree to walk at rasterise time.
            => Geometry.Combine(a, b, mode, null);

        public PathGeometry NewPathGeometry() => new PathGeometry { FillRule = FillRule.Nonzero };

        public PathFigure NewFigure(Point start)
            => new PathFigure { StartPoint = start, IsClosed = true, IsFilled = true, Segments = new PathSegmentCollection() };

        public void AddLine(PathFigure figure, Point point)
            => figure.Segments.Add(new LineSegment(point, false));

        public void AddArc(PathFigure figure, Point endPoint, Size size, bool clockwise)
            => figure.Segments.Add(new ArcSegment(endPoint, size, 0, false,
                clockwise ? SweepDirection.Clockwise : SweepDirection.Counterclockwise, false));

        public void Begin(StreamGeometryContext ctx, Point point) => ctx.BeginFigure(point, true, true);

        public void Line(StreamGeometryContext ctx, Point point) => ctx.LineTo(point, false, false);

        public void End(StreamGeometryContext ctx) { /* figure is closed via the BeginFigure flag */ }

        public EllipseGeometry Ellipse(Point center, double radiusX, double radiusY)
            => new EllipseGeometry(center, radiusX, radiusY);

        public Matrix Prepend(Matrix current, Matrix inner)
        {
            current.Prepend(inner);   // Matrix is a struct (local copy); Prepend => this = inner * this
            return current;
        }

        public bool TryResolve(Typeface typeface) => typeface.TryGetGlyphTypeface(out _);

        public ushort GlyphIndex(GlyphTypeface glyphTypeface, char c)
            => glyphTypeface.CharacterToGlyphMap.TryGetValue(c, out ushort g) ? g : (ushort)0;

        public double GlyphAdvanceEm(GlyphTypeface glyphTypeface, ushort glyphIndex)
            => glyphTypeface.AdvanceWidths[glyphIndex];

        public double FontLineHeightEm(GlyphTypeface glyphTypeface) => glyphTypeface.Height;

        public BitmapSource DecodeImage(byte[] data)
        {
            using var ms = new MemoryStream(data);
            var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            return decoder.Frames[0];
        }

        public int PixelWidth(BitmapSource image) => image.PixelWidth;

        public int PixelHeight(BitmapSource image) => image.PixelHeight;

        public byte[] RenderToPng(Rendering.LabelDrawing label, bool antialias, int scale)
        {
            if (scale < 1)
            {
                scale = 1;
            }

            var visual = new System.Windows.Media.DrawingVisual();
            if (!antialias)
            {
                RenderOptions.SetEdgeMode(visual, EdgeMode.Aliased);
            }

            using (DrawingContext dc = visual.RenderOpen())
            {
                label.Draw(dc);
            }

            // The visual draws at the native dot grid (1 dot = 1 DIU). Raising the bitmap DPI to 96*scale
            // maps each DIU to `scale` device pixels, so the buffer is `scale`× larger with no explicit
            // transform — axis-aligned dot edges land on exact pixel boundaries (crisp), curves render at
            // the higher resolution, and the on-paper size is unchanged.
            var rtb = new RenderTargetBitmap(
                label.Width * scale, label.Height * scale, 96.0 * scale, 96.0 * scale, PixelFormats.Pbgra32);
            rtb.Render(visual);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }
    }
}
