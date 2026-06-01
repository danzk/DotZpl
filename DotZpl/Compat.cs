using System;

namespace DotZpl
{
    /// <summary>
    /// Cross-framework facade over the WPF / Avalonia API-shape differences the shared rendering code
    /// touches. The divergent operations live behind <see cref="IPlatform"/> (one implementation per
    /// framework, picked once below); everything here is framework-agnostic — the public surface
    /// delegates to the platform strategy or builds geometry from its primitives, so the rest of the
    /// codebase has no inline <c>#if</c> branching.
    /// </summary>
    internal static class Compat
    {
        private static readonly IPlatform Platform =
#if WPF
            new WpfPlatform();
#else
            new AvaloniaPlatform();
#endif

        /// <summary>Shared empty-geometry sentinel (stable by reference).</summary>
        public static Geometry EmptyGeometry => Platform.EmptyGeometry;

        /// <summary>The non-zero fill rule (spelled differently per framework).</summary>
        public static FillRule NonZeroFill => Platform.NonZeroFill;

        /// <summary>
        /// Combine two geometries with a boolean op — eager <c>Geometry.Combine</c> (flat
        /// <see cref="PathGeometry"/>) on WPF, lazy <see cref="CombinedGeometry"/> on Avalonia.
        /// </summary>
        public static Geometry Combine(Geometry a, Geometry b, GeometryCombineMode mode) => Platform.Combine(a, b, mode);

        /// <summary>
        /// Build a rounded-rectangle "ring" (outer rect minus inner rect) as a single
        /// <see cref="PathGeometry"/> with two figures of opposite winding — outer clockwise, inner
        /// counter-clockwise. Under NonZero fill the two windings cancel inside the inner rect,
        /// producing the hole. The hole comes from contour winding (path data), not from a FillRule
        /// applied across separate children, so it survives a boolean combine and any geometry
        /// nesting on both backends.
        /// </summary>
        public static Geometry MakeRectRing(Rect outerRect, double outerRadius, Rect innerRect, double innerRadius)
        {
            PathGeometry ring = Platform.NewPathGeometry();
            ring.Figures!.Add(BuildRectFigure(outerRect, outerRadius, clockwise: true));
            ring.Figures!.Add(BuildRectFigure(innerRect, innerRadius, clockwise: false));
            return ring;
        }

        /// <summary>Elliptical equivalent of <see cref="MakeRectRing"/>.</summary>
        public static Geometry MakeEllipseRing(Point center, double outerRx, double outerRy, double innerRx, double innerRy)
        {
            PathGeometry ring = Platform.NewPathGeometry();
            ring.Figures!.Add(BuildEllipseFigure(center, outerRx, outerRy, clockwise: true));
            ring.Figures!.Add(BuildEllipseFigure(center, innerRx, innerRy, clockwise: false));
            return ring;
        }

        private static PathFigure BuildRectFigure(Rect rect, double cornerRadius, bool clockwise)
        {
            double r = Math.Max(0, Math.Min(cornerRadius, Math.Min(rect.Width, rect.Height) / 2));
            double x = rect.X, y = rect.Y, w = rect.Width, h = rect.Height;

            if (r <= 0)
            {
                // Plain rectangle, four line segments.
                PathFigure rectFig = Platform.NewFigure(new Point(x, y));
                if (clockwise)
                {
                    Platform.AddLine(rectFig, new Point(x + w, y));
                    Platform.AddLine(rectFig, new Point(x + w, y + h));
                    Platform.AddLine(rectFig, new Point(x, y + h));
                }
                else
                {
                    Platform.AddLine(rectFig, new Point(x, y + h));
                    Platform.AddLine(rectFig, new Point(x + w, y + h));
                    Platform.AddLine(rectFig, new Point(x + w, y));
                }
                return rectFig;
            }

            // Rounded rectangle. Each corner is a quarter-circle arc (small arc).
            var radSize = new Size(r, r);
            PathFigure fig = Platform.NewFigure(new Point(x + r, y));
            if (clockwise)
            {
                Platform.AddLine(fig, new Point(x + w - r, y));
                Platform.AddArc(fig, new Point(x + w, y + r), radSize, clockwise: true);
                Platform.AddLine(fig, new Point(x + w, y + h - r));
                Platform.AddArc(fig, new Point(x + w - r, y + h), radSize, clockwise: true);
                Platform.AddLine(fig, new Point(x + r, y + h));
                Platform.AddArc(fig, new Point(x, y + h - r), radSize, clockwise: true);
                Platform.AddLine(fig, new Point(x, y + r));
                Platform.AddArc(fig, new Point(x + r, y), radSize, clockwise: true);
            }
            else
            {
                Platform.AddArc(fig, new Point(x, y + r), radSize, clockwise: false);
                Platform.AddLine(fig, new Point(x, y + h - r));
                Platform.AddArc(fig, new Point(x + r, y + h), radSize, clockwise: false);
                Platform.AddLine(fig, new Point(x + w - r, y + h));
                Platform.AddArc(fig, new Point(x + w, y + h - r), radSize, clockwise: false);
                Platform.AddLine(fig, new Point(x + w, y + r));
                Platform.AddArc(fig, new Point(x + w - r, y), radSize, clockwise: false);
                Platform.AddLine(fig, new Point(x + r, y));
            }
            return fig;
        }

        private static PathFigure BuildEllipseFigure(Point center, double rx, double ry, bool clockwise)
        {
            var size = new Size(rx, ry);
            PathFigure fig = Platform.NewFigure(new Point(center.X, center.Y - ry));
            // Two semi-arcs traversing the same direction give the full ellipse with consistent winding.
            Platform.AddArc(fig, new Point(center.X, center.Y + ry), size, clockwise);
            Platform.AddArc(fig, new Point(center.X, center.Y - ry), size, clockwise);
            return fig;
        }

        /// <summary>Begin a filled, closed figure at <paramref name="p"/>.</summary>
        public static void Begin(this StreamGeometryContext ctx, Point p) => Platform.Begin(ctx, p);

        /// <summary>Add a straight (fill-only) segment to <paramref name="p"/>.</summary>
        public static void Line(this StreamGeometryContext ctx, Point p) => Platform.Line(ctx, p);

        /// <summary>Close the current figure.</summary>
        public static void End(this StreamGeometryContext ctx) => Platform.End(ctx);

        /// <summary>An ellipse from a centre and radii.</summary>
        public static EllipseGeometry Ellipse(Point center, double radiusX, double radiusY)
            => Platform.Ellipse(center, radiusX, radiusY);

        /// <summary>
        /// A horizontal-only scale about <paramref name="about"/> (mirrors WPF's
        /// <c>ScaleTransform(sx, 1, cx, cy)</c>). Built from the shared 6-argument <see cref="Matrix"/>
        /// constructor, whose component order is identical on both frameworks, so it needs no strategy.
        /// </summary>
        public static Transform HorizontalScale(double scaleX, Point about)
        {
            double offsetX = about.X * (1 - scaleX);   // x' = x*sx + about.X*(1-sx); y unchanged
            return new MatrixTransform(new Matrix(scaleX, 0, 0, 1, offsetX, 0));
        }

        /// <summary>Pre-multiply <paramref name="inner"/> onto <paramref name="current"/> (inner applied first).</summary>
        public static Matrix Prepend(Matrix current, Matrix inner) => Platform.Prepend(current, inner);

        // Pushing a transform as an IDisposable scope: on WPF a small extension supplies it
        // (PushTransform.Wpf.cs); on Avalonia DrawingContext.PushTransform(Matrix) already returns an
        // IDisposable, so the call site `using (dc.PushTransform(matrix))` binds to the instance method.

        /// <summary>Whether a <see cref="Typeface"/> resolves to a real glyph typeface on this platform.</summary>
        public static bool TryResolve(Typeface typeface) => Platform.TryResolve(typeface);

        /// <summary>Glyph index for a character (0 / .notdef when missing).</summary>
        public static ushort GlyphIndex(GlyphTypeface gt, char c) => Platform.GlyphIndex(gt, c);

        /// <summary>Glyph advance as a fraction of the em (multiply by the rendering em size for DIUs).</summary>
        public static double GlyphAdvanceEm(GlyphTypeface gt, ushort glyphIndex) => Platform.GlyphAdvanceEm(gt, glyphIndex);

        /// <summary>Line height as a fraction of the em.</summary>
        public static double FontLineHeightEm(GlyphTypeface gt) => Platform.FontLineHeightEm(gt);

        /// <summary>Decode PNG/raster bytes to the platform image type (null for empty input).</summary>
        public static BitmapSource? DecodeImage(byte[] data)
            => data == null || data.Length == 0 ? null : Platform.DecodeImage(data);

        /// <summary>Pixel width of a decoded image.</summary>
        public static int PixelWidth(BitmapSource image) => Platform.PixelWidth(image);

        /// <summary>Pixel height of a decoded image.</summary>
        public static int PixelHeight(BitmapSource image) => Platform.PixelHeight(image);

        /// <summary>Rasterise a built <see cref="Rendering.LabelDrawing"/> to a PNG byte array at 96 dpi.</summary>
        public static byte[] RenderToPng(Rendering.LabelDrawing label, bool antialias) => Platform.RenderToPng(label, antialias);
    }
}
