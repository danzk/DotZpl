using System;
using System.Collections;
using System.Collections.Generic;

using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Analyzer.Helpers;
using BinaryKits.Zpl.Analyzer.Symologies;

using DotZpl.Rendering;

namespace DotZpl.ElementDrawers
{
    /// <summary>
    /// Port of <c>MaxiCodeElementDrawer</c> (<c>^BD</c>). MaxiCode is not in ZXing; the hexagon
    /// pattern + three concentric finder rings are reproduced as <see cref="Geometry"/> (the Skia
    /// SKPath approach), mirroring the ISO/IEC 16023 dimensions exactly.
    ///
    /// <para><b>Known parity gap (~3% pixels):</b> the geometry math is byte-identical to Skia's,
    /// but the two backends rasterise the same non-axis-aligned hexagons (and Bézier-approximated
    /// finder rings) through different aliased scan converters, so every hexagon ends up a sub-pixel
    /// silhouette apart. Closing the gap would require bypassing the WPF/Avalonia rasteriser
    /// entirely (custom scan conversion into a <c>WriteableBitmap</c>). Skia itself doesn't match
    /// Labelary closely either, so neither backend is pixel-accurate ground truth; the SSIM gate
    /// (≥ 0.85, currently ~0.98) is what we hold the line on.</para>
    /// </summary>
    public class MaxiCodeElementDrawer : BarcodeDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element is ZplMaxiCode;

        public override Point Draw(ZplElementBase element, ZplRendererOptions options, Point currentPosition, InternationalFont internationalFont, int printDensityDpmm)
        {
            if (element is not ZplMaxiCode maxiCode)
            {
                return currentPosition;
            }

            double x = maxiCode.PositionX;
            double y = maxiCode.PositionY;
            if (maxiCode.UseDefaultPosition)
            {
                x = currentPosition.X;
                y = currentPosition.Y;
            }

            string content = maxiCode.Content;
            if (maxiCode.HexadecimalIndicator is char hexIndicator)
            {
                content = content.ReplaceHexEscapes(hexIndicator, internationalFont);
            }

            bool[] data = MaxiCodeSymbology.Encode(content, maxiCode.Mode);

            (Geometry geometry, int width, int height) = BuildMaxiCode(data, printDensityDpmm);
            DrawBarcode(geometry, x, y, width, height, maxiCode.FieldOrigin != null, maxiCode.FieldOrientation);

            return CalculateNextDefaultPosition(x, y, width, height, maxiCode.FieldOrigin != null, maxiCode.FieldOrientation, currentPosition);
        }

        private static (Geometry geometry, int width, int height) BuildMaxiCode(bool[] data, int dpmm)
        {
            // ISO/IEC 16023:2000 pp. 16, 38-40 — dimensions copied verbatim from the Skia drawer.
            double L, H, W, V, X, Y;
            double gX, gV;
            Vector[] pattern;
            double xoff, yoff;

            if (dpmm == 8)
            {
                W = 7; V = 8; X = W; Y = 6;
                gX = 1; gV = 1;
                xoff = 4; yoff = 2;
                pattern = new[]
                {
                    new Vector(0, 3), new Vector(2, 2), new Vector(1.5, 0), new Vector(2, -2),
                    new Vector(0, -3), new Vector(-2, -2), new Vector(-1.5, 0),
                };
                L = 29 * W;
                H = 32 * Y;
            }
            else if (dpmm == 12)
            {
                W = 10; V = 12; X = W; Y = 9;
                gX = 2; gV = 2;
                xoff = 5; yoff = 3;
                pattern = new[]
                {
                    new Vector(0, 4), new Vector(3, 3), new Vector(1.5, 0), new Vector(3, -3),
                    new Vector(0, -4), new Vector(-3, -3), new Vector(-1.5, 0),
                };
                L = 29 * W;
                H = 32 * Y;
            }
            else
            {
                L = 25.50 * dpmm;
                W = L / 29;
                V = 1.1547 * W;
                X = W;
                Y = 0.866 * W;
                H = 32 * Y;
                gX = dpmm / 6.0;
                gV = 1.1547 * gX;
                xoff = W / 2;
                yoff = (V - gV) / 4;

                double hexW = (X - gX) / 2;
                double hexH = (V - gV) / 4;
                pattern = new[]
                {
                    new Vector(0, hexH * 2), new Vector(hexW, hexH), new Vector(hexW, -hexH),
                    new Vector(0, -hexH * 2), new Vector(-hexW, -hexH),
                };
            }

            double r1 = 0.51 * dpmm, r2 = 1.18 * dpmm, r3 = 1.86 * dpmm;
            double r4 = 2.53 * dpmm, r5 = 3.20 * dpmm, r6 = 3.87 * dpmm;

            int width = (int)Math.Ceiling(L + X - gX);
            int height = (int)Math.Ceiling(H + V - gV);

            // Hexagons. The modules do not overlap, so the fill rule is immaterial within this geometry
            // (Avalonia's StreamGeometry has no FillRule property; the enclosing group sets non-zero).
            var hexes = new StreamGeometry();
            using (StreamGeometryContext ctx = hexes.Open())
            {
                IEnumerator dataEnum = data.GetEnumerator();
                for (int j = 0; j < 33; j++)
                {
                    for (int i = 0; i < 30 - j % 2; i++)
                    {
                        dataEnum.MoveNext();
                        if (!(bool)dataEnum.Current!)
                        {
                            continue;
                        }

                        var cur = new Point(i * W + j % 2 * xoff, j * Y + yoff);
                        ctx.Begin(cur);
                        foreach (Vector delta in pattern)
                        {
                            cur += delta;
                            ctx.Line(cur);
                        }

                        ctx.End();
                    }
                }
            }

            // Finder pattern: three annuli (R1-R2, R3-R4, R5-R6).
            double finderX = 14 * W + (X - gX) / 2;
            double finderY = 16 * Y + (V - gV) / 2;
            var center = new Point(finderX, finderY);

            var group = new GeometryGroup { FillRule = Compat.NonZeroFill };
            group.Children.Add(hexes);
            group.Children.Add(Annulus(center, r2, r1));
            group.Children.Add(Annulus(center, r4, r3));
            group.Children.Add(Annulus(center, r6, r5));

            return (group, width, height);
        }

        private static Geometry Annulus(Point center, double outerRadius, double innerRadius)
        {
            var outer = Compat.Ellipse(center, outerRadius, outerRadius);
            var inner = Compat.Ellipse(center, innerRadius, innerRadius);
            return new CombinedGeometry(GeometryCombineMode.Exclude, outer, inner);
        }
    }
}
