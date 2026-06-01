
using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;

using DotZpl.Rendering;

namespace DotZpl.ElementDrawers
{
    /// <summary>
    /// WPF port of <c>GraphicCircleElementDrawer</c> (<c>^GC</c>). Border = outer disk XOR inner disk.
    /// </summary>
    public class GraphicCircleElementDrawer : ElementDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element is ZplGraphicCircle;

        public override bool IsReverseDraw(ZplElementBase element)
            => element is ZplGraphicCircle circle && circle.ReversePrint;

        public override bool IsWhiteDraw(ZplElementBase element)
            => element is ZplGraphicCircle circle && circle.LineColor == LineColor.White;

        public override Point Draw(ZplElementBase element, ZplRendererOptions options, Point currentPosition, InternationalFont internationalFont)
        {
            if (element is not ZplGraphicCircle graphicCircle)
            {
                return currentPosition;
            }

            double radius = graphicCircle.Diameter / 2.0;
            double border = graphicCircle.BorderThickness;
            if (border > radius)
            {
                border = radius;
            }

            double baseX = graphicCircle.PositionX;
            double baseY = graphicCircle.PositionY;
            if (graphicCircle.UseDefaultPosition)
            {
                baseX = currentPosition.X;
                baseY = currentPosition.Y;
            }

            double cx = baseX + radius;
            double cy = baseY + radius;
            if (graphicCircle.FieldTypeset != null)
            {
                cy -= graphicCircle.Diameter;
                if (cy < radius)
                {
                    cy = radius;
                }
            }

            var center = new Point(cx, cy);
            var outer = Compat.Ellipse(center, radius, radius);

            Geometry borderGeometry;
            double innerRadius = radius - border;
            if (innerRadius <= 0)
            {
                borderGeometry = outer;
            }
            else
            {
                var inner = Compat.Ellipse(center, innerRadius, innerRadius);
                borderGeometry = Compat.Combine(outer, inner, GeometryCombineMode.Xor);
            }

            if (!graphicCircle.ReversePrint && graphicCircle.LineColor == LineColor.White)
            {
                context.AddWhite(borderGeometry);
            }
            else
            {
                context.AddBlack(borderGeometry);
            }

            return CalculateNextDefaultPosition(baseX, baseY, graphicCircle.Diameter, graphicCircle.Diameter, graphicCircle.FieldOrigin != null, FieldOrientation.Normal, currentPosition);
        }
    }
}
