namespace DotZpl
{
    /// <summary>
    /// Strategy that papers over the handful of WPF / Avalonia API-shape differences the shared
    /// rendering code touches. Exactly one implementation is compiled per target framework —
    /// <c>WpfPlatform</c> (<c>WpfPlatform.Wpf.cs</c>) or <c>AvaloniaPlatform</c>
    /// (<c>AvaloniaPlatform.Avalonia.cs</c>), each free of inline <c>#if</c> branching — and
    /// <see cref="Compat"/> selects it once. The geometry / font / image types named here resolve
    /// to the right framework's types per TFM via the global usings.
    /// </summary>
    internal interface IPlatform
    {
        /// <summary>A stable shared empty-geometry sentinel (compared by reference).</summary>
        Geometry EmptyGeometry { get; }

        /// <summary>The non-zero fill rule (spelled <c>Nonzero</c> on WPF, <c>NonZero</c> on Avalonia).</summary>
        FillRule NonZeroFill { get; }

        /// <summary>Combine two geometries with a boolean op.</summary>
        Geometry Combine(Geometry a, Geometry b, GeometryCombineMode mode);

        /// <summary>A new, empty <see cref="PathGeometry"/> with non-zero fill and an initialised figure list.</summary>
        PathGeometry NewPathGeometry();

        /// <summary>A new closed, filled <see cref="PathFigure"/> starting at <paramref name="start"/>.</summary>
        PathFigure NewFigure(Point start);

        /// <summary>Append a straight (fill-only) line segment to <paramref name="figure"/>.</summary>
        void AddLine(PathFigure figure, Point point);

        /// <summary>Append a small (≤180°) arc segment to <paramref name="figure"/>.</summary>
        void AddArc(PathFigure figure, Point endPoint, Size size, bool clockwise);

        /// <summary>Begin a filled, closed figure on a <see cref="StreamGeometryContext"/>.</summary>
        void Begin(StreamGeometryContext ctx, Point point);

        /// <summary>Add a straight (fill-only) segment on a <see cref="StreamGeometryContext"/>.</summary>
        void Line(StreamGeometryContext ctx, Point point);

        /// <summary>Close the current <see cref="StreamGeometryContext"/> figure.</summary>
        void End(StreamGeometryContext ctx);

        /// <summary>An ellipse from a centre and radii.</summary>
        EllipseGeometry Ellipse(Point center, double radiusX, double radiusY);

        /// <summary>Pre-multiply <paramref name="inner"/> onto <paramref name="current"/> (inner applied first).</summary>
        Matrix Prepend(Matrix current, Matrix inner);

        /// <summary>Whether a <see cref="Typeface"/> resolves to a real glyph typeface.</summary>
        bool TryResolve(Typeface typeface);

        /// <summary>Glyph index for a character (0 / .notdef when missing).</summary>
        ushort GlyphIndex(GlyphTypeface glyphTypeface, char c);

        /// <summary>Glyph advance as a fraction of the em.</summary>
        double GlyphAdvanceEm(GlyphTypeface glyphTypeface, ushort glyphIndex);

        /// <summary>Line height as a fraction of the em.</summary>
        double FontLineHeightEm(GlyphTypeface glyphTypeface);

        /// <summary>Decode PNG/raster bytes to the platform image type (caller guarantees non-empty).</summary>
        BitmapSource DecodeImage(byte[] data);

        /// <summary>Pixel width of a decoded image.</summary>
        int PixelWidth(BitmapSource image);

        /// <summary>Pixel height of a decoded image.</summary>
        int PixelHeight(BitmapSource image);

        /// <summary>
        /// Rasterise a built <see cref="Rendering.LabelDrawing"/> to a PNG byte array. <paramref name="scale"/>
        /// is an integer supersample factor (≥ 1): the image is rendered at <c>scale</c>× the native dot
        /// grid with the bitmap DPI raised to match (96 × scale), so the on-paper size is unchanged but the
        /// pixel density — and print sharpness — grows.
        /// </summary>
        byte[] RenderToPng(Rendering.LabelDrawing label, bool antialias, int scale);
    }
}
