using System;
using System.Collections.Generic;
using System.Globalization;

namespace WpfZpl.Text
{
    public enum TextBackend
    {
        /// <summary>Manual <see cref="GlyphRun"/> — baseline-origin, directly matches Skia's DrawShapedText.</summary>
        GlyphRun,

        /// <summary><see cref="FormattedText"/> — top-left origin, converted to baseline (spike comparison only).</summary>
        FormattedText,
    }

    /// <summary>
    /// Produces text <see cref="Geometry"/> positioned at a baseline origin, reproducing Skia's
    /// baseline-anchored <c>DrawShapedText</c>. Offers a <see cref="TextBackend.GlyphRun"/> path
    /// (primary) and a <see cref="TextBackend.FormattedText"/> path (for the requested comparison).
    /// </summary>
    public static class WpfTextRenderer
    {
        /// <summary>Line spacing ≈ Skia <c>SKFont.Spacing</c> (used for barcode interpretation margins).</summary>
        public static double LineSpacing(GlyphTypeface gt, double emSize) => Compat.FontLineHeightEm(gt) * emSize;

        /// <summary>Cap height of "X" ≈ Skia <c>MeasureText("X").Height</c> (independent of scaleX).</summary>
        public static double CapHeight(GlyphTypeface gt, double emSize)
        {
            GlyphRun? run = BuildRun(gt, "X", emSize, new Point(0, 0));
            return run == null ? emSize : run.BuildGeometry().Bounds.Height;
        }

        /// <summary>Advance width of text ≈ Skia <c>MeasureText</c> return value, scaled by scaleX.</summary>
        public static double MeasureAdvance(GlyphTypeface gt, string text, double emSize, double scaleX)
        {
            double sum = 0;
            foreach (char c in text)
            {
                ushort gi = Compat.GlyphIndex(gt, c);
                sum += Compat.GlyphAdvanceEm(gt, gi) * emSize;
            }

            return sum * scaleX;
        }

        /// <summary>Tight bounds of text relative to a (0,0) baseline (Left/Width feed FieldBlock justification).</summary>
        public static Rect MeasureTightBounds(GlyphTypeface gt, string text, double emSize, double scaleX)
        {
            Geometry geo = BuildGeometryGlyphRun(gt, text, emSize, scaleX, new Point(0, 0));
            return geo.Bounds;
        }

        // NOTE on font weight: the geometry-fill text path renders slightly heavier than Skia's
        // gamma-corrected, hinted glyph rasteriser at small sizes (a rasteriser difference, not a
        // weight/font difference). This is inherent and does not affect SSIM; see the git history for
        // the erosion experiment that was tried and removed.

        /// <summary>Build filled text geometry at a baseline origin using the selected backend.</summary>
        public static Geometry BuildGeometry(
            TextBackend backend, GlyphTypeface gt, Typeface tf, string text,
            double emSize, double scaleX, Point baselineOrigin)
        {
            return backend == TextBackend.FormattedText
                ? BuildGeometryFormattedText(tf, text, emSize, scaleX, baselineOrigin)
                : BuildGeometryGlyphRun(gt, text, emSize, scaleX, baselineOrigin);
        }

        public static Geometry BuildGeometryGlyphRun(GlyphTypeface gt, string text, double emSize, double scaleX, Point baselineOrigin)
        {
            GlyphRun? run = BuildRun(gt, text, emSize, baselineOrigin);
            if (run == null)
            {
                return Compat.EmptyGeometry;
            }

            return ApplyScaleX(run.BuildGeometry() ?? Compat.EmptyGeometry, scaleX, baselineOrigin);
        }

        public static Geometry BuildGeometryFormattedText(Typeface tf, string text, double emSize, double scaleX, Point baselineOrigin)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Compat.EmptyGeometry;
            }

#if WPF
            var ft = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                tf,
                emSize,
                Brushes.Black,
                1.0);
#elif AVALONIA
            var ft = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                tf,
                emSize,
                Brushes.Black);
#endif

            // FormattedText draws top-left; convert to baseline by lifting the top by the font ascent.
            var topLeft = new Point(baselineOrigin.X, baselineOrigin.Y - ft.Baseline);
            return ApplyScaleX(ft.BuildGeometry(topLeft) ?? Compat.EmptyGeometry, scaleX, baselineOrigin);
        }

        private static Geometry ApplyScaleX(Geometry geo, double scaleX, Point baselineOrigin)
        {
            if (scaleX == 1.0)
            {
                return geo;
            }

            Transform scale = Compat.HorizontalScale(scaleX, baselineOrigin);

#if WPF
            // BuildGeometry() may return a frozen geometry — wrap rather than mutate when needed.
            if (!geo.IsFrozen && (geo.Transform == null || geo.Transform.Value.IsIdentity))
            {
                geo.Transform = scale;
                return geo;
            }
#endif

            var group = new GeometryGroup();
            group.Children.Add(geo);
            group.Transform = scale;
            return group;
        }

        private static GlyphRun? BuildRun(GlyphTypeface gt, string text, double emSize, Point baselineOrigin)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            var indices = new List<ushort>(text.Length);
            foreach (char c in text)
            {
                indices.Add(Compat.GlyphIndex(gt, c));
            }

#if WPF
            var advances = new List<double>(text.Length);
            foreach (ushort gi in indices)
            {
                advances.Add(Compat.GlyphAdvanceEm(gt, gi) * emSize);
            }

            return new GlyphRun(
                gt,
                bidiLevel: 0,
                isSideways: false,
                renderingEmSize: emSize,
                pixelsPerDip: 1.0f,
                glyphIndices: indices,
                baselineOrigin: baselineOrigin,
                advanceWidths: advances,
                glyphOffsets: null,
                characters: null,
                deviceFontName: null,
                clusterMap: null,
                caretStops: null,
                language: null);
#elif AVALONIA
            // Avalonia derives per-glyph advances from the font (correct for our monospace pixel fonts
            // and for system fonts alike), so only the indices and baseline origin are supplied.
            return new GlyphRun(gt, emSize, text.AsMemory(), indices)
            {
                BaselineOrigin = baselineOrigin,
            };
#endif
        }
    }
}
