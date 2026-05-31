using System;
using System.IO;

namespace WpfZpl
{
    /// <summary>
    /// Cross-framework helpers that paper over the few WPF/Avalonia API-shape differences which the
    /// shared rendering code touches: <see cref="StreamGeometryContext"/> figure building, the empty
    /// geometry sentinel, the non-zero fill rule (spelled differently), matrix pre-multiplication, the
    /// raster→PNG path, and pushing a transform as an <see cref="IDisposable"/> scope.
    /// </summary>
    internal static class Compat
    {
        /// <summary>Shared empty-geometry sentinel (WPF has <c>Geometry.Empty</c>; Avalonia does not).</summary>
#if WPF
        public static readonly Geometry EmptyGeometry = Geometry.Empty;
        public const FillRule NonZeroFill = FillRule.Nonzero;
#elif AVALONIA
        public static readonly Geometry EmptyGeometry = new StreamGeometry();
        public const FillRule NonZeroFill = FillRule.NonZero;
#endif

        /// <summary>Begin a filled, closed figure at <paramref name="p"/>.</summary>
        public static void Begin(this StreamGeometryContext ctx, Point p)
        {
#if WPF
            ctx.BeginFigure(p, true, true);
#elif AVALONIA
            ctx.BeginFigure(p, true);
#endif
        }

        /// <summary>Add a straight (fill-only) segment to <paramref name="p"/>.</summary>
        public static void Line(this StreamGeometryContext ctx, Point p)
        {
#if WPF
            ctx.LineTo(p, false, false);
#elif AVALONIA
            ctx.LineTo(p);
#endif
        }

        /// <summary>Close the current figure (WPF closes via the BeginFigure flag; Avalonia needs EndFigure).</summary>
        public static void End(this StreamGeometryContext ctx)
        {
#if AVALONIA
            ctx.EndFigure(true);
#endif
        }

        /// <summary>An ellipse from a centre and radii (WPF has this ctor; Avalonia takes a bounding rect).</summary>
        public static EllipseGeometry Ellipse(Point center, double radiusX, double radiusY)
        {
#if WPF
            return new EllipseGeometry(center, radiusX, radiusY);
#elif AVALONIA
            return new EllipseGeometry(new Rect(center.X - radiusX, center.Y - radiusY, radiusX * 2, radiusY * 2));
#endif
        }

        /// <summary>
        /// A horizontal-only scale about <paramref name="about"/> (mirrors WPF's
        /// <c>ScaleTransform(sx, 1, cx, cy)</c>). Built from the shared 6-argument <see cref="Matrix"/>
        /// constructor, whose component order is identical on both frameworks, so no #if is needed.
        /// </summary>
        public static Transform HorizontalScale(double scaleX, Point about)
        {
            double offsetX = about.X * (1 - scaleX);   // x' = x*sx + about.X*(1-sx); y unchanged
            return new MatrixTransform(new Matrix(scaleX, 0, 0, 1, offsetX, 0));
        }

        /// <summary>Pre-multiply <paramref name="inner"/> onto <paramref name="current"/> (inner applied first).</summary>
        public static Matrix Prepend(Matrix current, Matrix inner)
        {
#if WPF
            current.Prepend(inner);   // Matrix is a struct (local copy); Prepend => this = inner * this
            return current;
#elif AVALONIA
            return inner * current;   // Avalonia Matrix: value1 is applied first
#endif
        }

        /// <summary>Push a matrix transform as an <see cref="IDisposable"/> scope (uniform across frameworks).</summary>
#if WPF
        public static IDisposable PushTransform(this DrawingContext dc, Matrix m)
        {
            dc.PushTransform(new MatrixTransform(m));
            return new PopGuard(dc);
        }

        private sealed class PopGuard : IDisposable
        {
            private readonly DrawingContext _dc;
            public PopGuard(DrawingContext dc) => _dc = dc;
            public void Dispose() => _dc.Pop();
        }
#endif
        // On Avalonia, DrawingContext.PushTransform(Matrix) already returns an IDisposable, so the
        // call site `using (dc.PushTransform(matrix))` binds to the instance method directly.

        /// <summary>Whether a <see cref="Typeface"/> resolves to a real glyph typeface on this platform.</summary>
        public static bool TryResolve(Typeface typeface)
        {
#if WPF
            return typeface.TryGetGlyphTypeface(out _);
#elif AVALONIA
            return FontManager.Current.TryGetGlyphTypeface(typeface, out _);
#endif
        }

        /// <summary>Glyph index for a character (WPF: dictionary lookup; Avalonia: map indexer).</summary>
        public static ushort GlyphIndex(GlyphTypeface gt, char c)
        {
#if WPF
            return gt.CharacterToGlyphMap.TryGetValue(c, out ushort g) ? g : (ushort)0;
#elif AVALONIA
            return gt.CharacterToGlyphMap[c];   // missing codepoints map to glyph 0 (.notdef)
#endif
        }

        /// <summary>Glyph advance as a fraction of the em (multiply by the rendering em size for DIUs).</summary>
        public static double GlyphAdvanceEm(GlyphTypeface gt, ushort glyphIndex)
        {
#if WPF
            return gt.AdvanceWidths[glyphIndex];
#elif AVALONIA
            gt.TryGetHorizontalGlyphAdvance(glyphIndex, out ushort advance);
            return advance / (double)gt.Metrics.DesignEmHeight;
#endif
        }

        /// <summary>Line height as a fraction of the em (≈ WPF <c>GlyphTypeface.Height</c>).</summary>
        public static double FontLineHeightEm(GlyphTypeface gt)
        {
#if WPF
            return gt.Height;
#elif AVALONIA
            FontMetrics m = gt.Metrics;
            // Avalonia ascent is negative (above the baseline); descent positive (below).
            return (m.Descent - m.Ascent + m.LineGap) / (double)m.DesignEmHeight;
#endif
        }

        /// <summary>Decode PNG/raster bytes to the platform image type.</summary>
        public static BitmapSource? DecodeImage(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return null;
            }

            using var ms = new MemoryStream(data);
#if WPF
            var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            return decoder.Frames[0];
#elif AVALONIA
            return new BitmapSource(ms);   // BitmapSource is aliased to Avalonia.Media.Imaging.Bitmap
#endif
        }

        /// <summary>Pixel width of a decoded image.</summary>
        public static int PixelWidth(BitmapSource image) =>
#if WPF
            image.PixelWidth;
#elif AVALONIA
            image.PixelSize.Width;
#endif

        /// <summary>Pixel height of a decoded image.</summary>
        public static int PixelHeight(BitmapSource image) =>
#if WPF
            image.PixelHeight;
#elif AVALONIA
            image.PixelSize.Height;
#endif

        /// <summary>Rasterise a vector <see cref="DrawingGroup"/> to a PNG byte array at 96 dpi.</summary>
        public static byte[] RenderToPng(DrawingGroup drawing, int width, int height, bool antialias)
        {
#if WPF
            var visual = new System.Windows.Media.DrawingVisual();
            if (!antialias)
            {
                RenderOptions.SetEdgeMode(visual, EdgeMode.Aliased);
            }

            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawDrawing(drawing);
            }

            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
#elif AVALONIA
            var rtb = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
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
                        drawing.Draw(ctx);
                    }
                }
                else
                {
                    drawing.Draw(ctx);
                }
            }

            using var ms = new MemoryStream();
            rtb.Save(ms);
            return ms.ToArray();
#endif
        }
    }
}
