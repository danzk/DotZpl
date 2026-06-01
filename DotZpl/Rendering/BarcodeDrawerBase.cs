using System;
using System.Collections.Generic;
using System.Linq;

using BinaryKits.Zpl.Label;

using DotZpl.Text;

using ZXing.Common;

namespace DotZpl.Rendering
{
    /// <summary>
    /// WPF analogue of <c>BinaryKits.Zpl.Viewer.ElementDrawers.BarcodeDrawerBase</c>. Barcodes are
    /// rendered as <see cref="Geometry"/> (one rectangle per module run) instead of bitmaps, so they
    /// participate in the fill/XOR pipeline. The interpretation line reuses <see cref="TextRenderer"/>.
    /// </summary>
    public abstract class BarcodeDrawerBase : ElementDrawerBase
    {
        protected const double MIN_LABEL_MARGIN = 5.0;

        /// <summary>Build bar geometry (local 0-based coords) from a 1-D module array, run-length merged.</summary>
        protected static Geometry BoolArrayToGeometry(bool[] array, int height, int moduleWidth = 1)
        {
            var geo = new StreamGeometry();
            using (StreamGeometryContext ctx = geo.Open())
            {
                int col = 0;
                while (col < array.Length)
                {
                    if (!array[col]) { col++; continue; }
                    int start = col;
                    while (col < array.Length && array[col]) { col++; }
                    AddRect(ctx, start * moduleWidth, 0, (col - start) * moduleWidth, height);
                }
            }

            return geo;
        }

        /// <summary>Bar geometry where a module is filled only when both array and mask are set.</summary>
        protected static Geometry BoolArrayWithMaskToGeometry(bool[] array, bool[] mask, int height, int moduleWidth = 1)
        {
            var geo = new StreamGeometry();
            using (StreamGeometryContext ctx = geo.Open())
            {
                int col = 0;
                while (col < array.Length)
                {
                    bool on = array[col] && mask[col];
                    if (!on) { col++; continue; }
                    int start = col;
                    while (col < array.Length && array[col] && mask[col]) { col++; }
                    AddRect(ctx, start * moduleWidth, 0, (col - start) * moduleWidth, height);
                }
            }

            return geo;
        }

        /// <summary>Build 2-D matrix geometry (local 0-based coords), one rectangle per set cell (row run-merged).</summary>
        protected static Geometry BitMatrixToGeometry(BitMatrix matrix, int pixelScale)
        {
            var geo = new StreamGeometry();
            using (StreamGeometryContext ctx = geo.Open())
            {
                for (int row = 0; row < matrix.Height; row++)
                {
                    int col = 0;
                    while (col < matrix.Width)
                    {
                        if (!matrix[col, row]) { col++; continue; }
                        int start = col;
                        while (col < matrix.Width && matrix[col, row]) { col++; }
                        AddRect(ctx, start * pixelScale, row * pixelScale, (col - start) * pixelScale, pixelScale);
                    }
                }
            }

            return geo;
        }

        private static void AddRect(StreamGeometryContext ctx, double x, double y, double w, double h)
        {
            ctx.Begin(new Point(x, y));
            ctx.Line(new Point(x + w, y));
            ctx.Line(new Point(x + w, y + h));
            ctx.Line(new Point(x, y + h));
            ctx.End();
        }

        /// <summary>Rotation transform mirroring <c>BarcodeDrawerBase.GetRotationMatrix</c>.</summary>
        protected static RotateTransform? GetRotationTransform(double x, double y, int width, int height, bool useFieldOrigin, FieldOrientation fieldOrientation)
        {
            if (useFieldOrigin)
            {
                return fieldOrientation switch
                {
                    FieldOrientation.Rotated90 => new RotateTransform(90, x + height / 2.0, y + height / 2.0),
                    FieldOrientation.Rotated180 => new RotateTransform(180, x + width / 2.0, y + height / 2.0),
                    FieldOrientation.Rotated270 => new RotateTransform(270, x + width / 2.0, y + width / 2.0),
                    _ => null,
                };
            }

            return fieldOrientation switch
            {
                FieldOrientation.Rotated90 => new RotateTransform(90, x, y),
                FieldOrientation.Rotated180 => new RotateTransform(180, x, y),
                FieldOrientation.Rotated270 => new RotateTransform(270, x, y),
                _ => null,
            };
        }

        /// <summary>Place local bar geometry at (x,y) with the non-field-origin y-adjust and rotation.</summary>
        protected void DrawBarcode(Geometry barsLocal, double x, double y, int barcodeWidth, int barcodeHeight, bool useFieldOrigin, FieldOrientation fieldOrientation)
        {
            double drawY = y;
            if (!useFieldOrigin)
            {
                drawY -= barcodeHeight;
                if (drawY < 0) drawY = 0;
            }

            RotateTransform? rot = GetRotationTransform(x, y, barcodeWidth, barcodeHeight, useFieldOrigin, fieldOrientation);
            if (rot != null) context.PushTransform(rot);
            context.PushTransform(new TranslateTransform(x, drawY));
            context.AddBlack(barsLocal);
            context.Pop();
            if (rot != null) context.Pop();
        }

        /// <summary>Centered single-string interpretation line, mirroring <c>DrawInterpretationLine</c>.</summary>
        protected void DrawInterpretationLine(string interpretation, GlyphTypeface gt, double emSize, double x, double y, int barcodeWidth, int barcodeHeight, bool useFieldOrigin, FieldOrientation fieldOrientation, bool printAboveCode, DrawerOptions options)
        {
            RotateTransform? rot = GetRotationTransform(x, y, barcodeWidth, barcodeHeight, useFieldOrigin, fieldOrientation);
            if (rot != null) context.PushTransform(rot);

            Rect textBounds = TextRenderer.MeasureTightBounds(gt, interpretation, emSize, 1.0);
            double penX = x + (barcodeWidth - textBounds.Width) / 2;
            double yy = y;
            if (!useFieldOrigin)
            {
                yy -= barcodeHeight;
                if (yy < 0) yy = 0;
            }

            double margin = Math.Max((TextRenderer.LineSpacing(gt, emSize) - textBounds.Height) / 2, MIN_LABEL_MARGIN);
            double baselineY = printAboveCode ? yy - margin : yy + barcodeHeight + textBounds.Height + margin;

            Geometry g = TextRenderer.BuildGeometryGlyphRun(gt, interpretation, emSize, 1.0, new Point(penX, baselineY));
            context.AddBlack(g);

            if (rot != null) context.Pop();
        }

        /// <summary>
        /// Per-digit interpretation line with guard bars (EAN-13 / UPC-A / UPC-E), mirroring the
        /// custom Skia methods. <paramref name="extraModulesAfter"/> returns the extra module gap
        /// inserted after digit index i.
        /// </summary>
        protected void DrawDigitInterpretationLine(
            bool[] guardArray, bool[]? maskArray, string interpretation, GlyphTypeface gt, double emSize,
            double x, double y, int barcodeWidth, int barcodeHeight, bool useFieldOrigin,
            FieldOrientation fieldOrientation, int moduleWidth, DrawerOptions options,
            Func<int, int> extraModulesAfter)
        {
            RotateTransform? rot = GetRotationTransform(x, y, barcodeWidth, barcodeHeight, useFieldOrigin, fieldOrientation);
            if (rot != null) context.PushTransform(rot);

            Rect textBounds = TextRenderer.MeasureTightBounds(gt, interpretation, emSize, 1.0);
            double yy = y;
            if (!useFieldOrigin)
            {
                yy -= barcodeHeight;
                if (yy < 0) yy = 0;
            }

            double margin = Math.Max((TextRenderer.LineSpacing(gt, emSize) - textBounds.Height) / 2, MIN_LABEL_MARGIN);
            int spacing = moduleWidth * 7;

            int guardHeight = (int)(margin + textBounds.Height / 2);
            Geometry guard = maskArray == null
                ? BoolArrayToGeometry(guardArray, guardHeight, moduleWidth)
                : BoolArrayWithMaskToGeometry(guardArray, maskArray, guardHeight, moduleWidth);

            context.PushTransform(new TranslateTransform(x, yy + barcodeHeight));
            context.AddBlack(guard);
            context.Pop();

            double cx = x;
            double baselineY = yy + barcodeHeight + textBounds.Height + margin;
            for (int i = 0; i < interpretation.Length; i++)
            {
                string digit = interpretation[i].ToString();
                Rect digitBounds = TextRenderer.MeasureTightBounds(gt, digit, emSize, 1.0);
                double penX = cx - (spacing + digitBounds.Width) / 2 - moduleWidth;
                Geometry g = TextRenderer.BuildGeometryGlyphRun(gt, digit, emSize, 1.0, new Point(penX, baselineY));
                context.AddBlack(g);

                cx += spacing + extraModulesAfter(i) * moduleWidth;
            }

            if (rot != null) context.Pop();
        }

        /// <summary>Copy of <c>BarcodeDrawerBase.AdjustWidths</c> (pure bool[] expansion).</summary>
        protected static bool[] AdjustWidths(bool[] array, int wide, int narrow)
        {
            List<bool> result = new();
            bool last = true;
            int count = 0;
            foreach (bool current in array)
            {
                if (current != last)
                {
                    result.AddRange(Enumerable.Repeat(last, count == 1 ? narrow : wide));
                    last = current;
                    count = 0;
                }

                count += 1;
            }

            result.AddRange(Enumerable.Repeat(last, narrow));
            return result.ToArray();
        }
    }
}
