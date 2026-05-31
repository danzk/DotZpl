using System.Windows;
using System.Windows.Media;

using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;

using WpfZpl.Rendering;

namespace WpfZpl.ElementDrawers
{
    /// <summary>
    /// WPF port of <c>GraphicCircleElementDrawer</c> (<c>^GC</c>). Border = outer disk XOR inner disk.
    /// </summary>
    public class GraphicCircleWpfElementDrawer : WpfElementDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element is ZplGraphicCircle;

        public override bool IsReverseDraw(ZplElementBase element)
            => element is ZplGraphicCircle circle && circle.ReversePrint;

        public override bool IsWhiteDraw(ZplElementBase element)
            => element is ZplGraphicCircle circle && circle.LineColor == LineColor.White;

        public override Point Draw(ZplElementBase element, WpfDrawerOptions options, Point currentPosition, InternationalFont internationalFont)
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
            var outer = new EllipseGeometry(center, radius, radius);

            Geometry borderGeometry;
            double innerRadius = radius - border;
            if (innerRadius <= 0)
            {
                borderGeometry = outer;
            }
            else
            {
                var inner = new EllipseGeometry(center, innerRadius, innerRadius);
                borderGeometry = new CombinedGeometry(GeometryCombineMode.Xor, outer, inner);
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
