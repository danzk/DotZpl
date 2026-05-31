using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

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
        public static double LineSpacing(GlyphTypeface gt, double emSize) => gt.Height * emSize;

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
                ushort gi = gt.CharacterToGlyphMap.TryGetValue(c, out ushort g) ? g : (ushort)0;
                sum += gt.AdvanceWidths[gi] * emSize;
            }

            return sum * scaleX;
        }

        /// <summary>Tight bounds of text relative to a (0,0) baseline (Left/Width feed FieldBlock justification).</summary>
        public static Rect MeasureTightBounds(GlyphTypeface gt, string text, double emSize, double scaleX)
        {
            Geometry geo = BuildGeometryGlyphRun(gt, text, emSize, scaleX, new Point(0, 0));
            return geo.Bounds;
        }

        // NOTE on font weight: WPF's linear-coverage geometry fill renders text slightly heavier than
        // Skia's gamma-corrected, hinted glyph rasteriser, most visibly at small sizes (~1.25x ink at
        // em=13; large text already matches). We investigated compensating with a small uniform edge
        // erosion (subtracting a thin outline ring from each glyph fill). It DID match Skia's ink
        // (e.g. FieldDataText 1.077x -> 0.994x), but it made NO measurable difference to SSIM: the
        // residual text gap is sub-pixel AA/positioning between the two rasterisers, not weight, and
        // SSIM (locally contrast-normalised) does not reward weight-matching. The erosion was therefore
        // removed to avoid the extra GetWidenedPathGeometry cost for no metric gain. We also confirmed
        // FormattedText is no lighter (BuildGeometry 1.229x; DrawText raster 1.385x), so geometry fill
        // remains the closest-to-Skia WPF text path.

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
                return Geometry.Empty;
            }

            return ApplyScaleX(run.BuildGeometry(), scaleX, baselineOrigin);
        }

        public static Geometry BuildGeometryFormattedText(Typeface tf, string text, double emSize, double scaleX, Point baselineOrigin)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Geometry.Empty;
            }

            var ft = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                tf,
                emSize,
                Brushes.Black,
                1.0);

            // FormattedText draws top-left; convert to baseline by lifting the top by the font ascent.
            var topLeft = new Point(baselineOrigin.X, baselineOrigin.Y - ft.Baseline);
            return ApplyScaleX(ft.BuildGeometry(topLeft), scaleX, baselineOrigin);
        }

        private static Geometry ApplyScaleX(Geometry geo, double scaleX, Point baselineOrigin)
        {
            if (scaleX == 1.0)
            {
                return geo;
            }

            var scale = new ScaleTransform(scaleX, 1.0, baselineOrigin.X, baselineOrigin.Y);

            // BuildGeometry() may return a frozen geometry — wrap rather than mutate when needed.
            if (!geo.IsFrozen && (geo.Transform == null || geo.Transform.Value.IsIdentity))
            {
                geo.Transform = scale;
                return geo;
            }

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
            var advances = new List<double>(text.Length);
            foreach (char c in text)
            {
                ushort gi = gt.CharacterToGlyphMap.TryGetValue(c, out ushort g) ? g : (ushort)0;
                indices.Add(gi);
                advances.Add(gt.AdvanceWidths[gi] * emSize);
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
        }
    }
}
