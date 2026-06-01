using System;

using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;

using DotZpl.Rendering;

namespace DotZpl.ElementDrawers
{
    /// <summary>
    /// WPF port of <c>GraphicBoxElementDrawer</c> (<c>^GB</c>). The border is built as a filled ring
    /// (outer shape XOR inner shape) instead of a centered <c>Pen</c> stroke, so it stays exactly
    /// within the box bounds — see the plan's "two-shape XOR" note.
    /// </summary>
    public class GraphicBoxElementDrawer : ElementDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element is ZplGraphicBox;

        public override bool IsReverseDraw(ZplElementBase element)
            => element is ZplGraphicBox box && box.ReversePrint;

        public override bool IsWhiteDraw(ZplElementBase element)
            => element is ZplGraphicBox box && box.LineColor == LineColor.White;

        public override Point Draw(ZplElementBase element, ZplRendererOptions options, Point currentPosition, InternationalFont internationalFont)
        {
            if (element is not ZplGraphicBox graphicBox)
            {
                return currentPosition;
            }

            int border = graphicBox.BorderThickness;
            int width1 = graphicBox.Width;
            int height1 = graphicBox.Height;

            // Mirror the Skia clamping of border/size.
            if (border > width1) width1 = border;
            if (border > height1) height1 = border;
            if (border > width1 / 2 && width1 <= height1) border = (int)Math.Ceiling(width1 / 2f);
            if (border > height1 / 2 && height1 <= width1) border = (int)Math.Ceiling(height1 / 2f);
            if (border < 1) border = 1;

            double baseX = graphicBox.PositionX;
            double baseY = graphicBox.PositionY;
            if (graphicBox.UseDefaultPosition)
            {
                baseX = currentPosition.X;
                baseY = currentPosition.Y;
            }

            // FieldTypeset (^FT) anchors the box bottom-up.
            double top = baseY;
            if (graphicBox.FieldTypeset != null)
            {
                top = baseY - height1;
                if (top < 0) top = 0;
            }

            // cornerRadius matches the Skia formula.
            // Skia strokes concentric round-rects with the SAME path radius from the thickest border
            // down to 1px; the thinnest (1px) pass defines the visible outer contour, so the effective
            // outer corner radius is ~cornerRadius (NOT cornerRadius + border/2, which would round a
            // solid box almost to a circle). The inner hole keeps the centered-stroke radius.
            double cornerRadius = (graphicBox.CornerRounding / 8.0) * (Math.Min(width1, height1) / 2.0);
            double rOuter = cornerRadius;
            double rInner = cornerRadius == 0 ? 0 : Math.Max(0, cornerRadius - border / 2.0);

            var outer = new RectangleGeometry(new Rect(baseX, top, width1, height1), rOuter, rOuter);

            Geometry borderGeometry;
            double iw = width1 - 2.0 * border;
            double ih = height1 - 2.0 * border;
            if (iw <= 0 || ih <= 0)
            {
                borderGeometry = outer; // border fills the whole box (solid line/bar)
            }
            else
            {
                var inner = new RectangleGeometry(new Rect(baseX + border, top + border, iw, ih), rInner, rInner);
                borderGeometry = Compat.MakeRing(outer, inner);
            }

            // Reverse always feeds the black bucket (the orchestrator decides background vs white XOR).
            if (!graphicBox.ReversePrint && graphicBox.LineColor == LineColor.White)
            {
                context.AddWhite(borderGeometry);
            }
            else
            {
                context.AddBlack(borderGeometry);
            }

            return CalculateNextDefaultPosition(baseX, baseY, width1, height1, graphicBox.FieldOrigin != null, FieldOrientation.Normal, currentPosition);
        }
    }
}
