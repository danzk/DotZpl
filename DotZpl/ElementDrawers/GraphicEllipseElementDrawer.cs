
using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;

using DotZpl.Rendering;

namespace DotZpl.ElementDrawers
{
    /// <summary>
    /// WPF port of <c>GraphicEllipseElementDrawer</c> (<c>^GE</c>). Border = outer ellipse XOR inner
    /// ellipse. Mirrors the Skia drawer which (by virtue of its type-check quirk) never applies
    /// reverse/white and always draws black.
    /// </summary>
    public class GraphicEllipseElementDrawer : ElementDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element is ZplGraphicEllipse;

        public override Point Draw(ZplElementBase element, DrawerOptions options, Point currentPosition, InternationalFont internationalFont)
        {
            if (element is not ZplGraphicEllipse graphicEllipse)
            {
                return currentPosition;
            }

            double border = graphicEllipse.BorderThickness;
            double width = graphicEllipse.Width;
            double height = graphicEllipse.Height;

            if (width < border * 2) border = width / 2;
            if (height < border * 2) border = height / 2;

            double x = graphicEllipse.PositionX;
            double y = graphicEllipse.PositionY;
            if (graphicEllipse.UseDefaultPosition)
            {
                x = currentPosition.X;
                y = currentPosition.Y;
            }

            if (graphicEllipse.FieldTypeset != null)
            {
                y -= height;
                if (y < 0) y = 0;
            }

            var center = new Point(x + width / 2.0, y + height / 2.0);
            var outer = Compat.Ellipse(center, width / 2.0, height / 2.0);

            Geometry borderGeometry;
            double innerRx = width / 2.0 - border;
            double innerRy = height / 2.0 - border;
            if (innerRx <= 0 || innerRy <= 0)
            {
                borderGeometry = outer;
            }
            else
            {
                var inner = Compat.Ellipse(center, innerRx, innerRy);
                borderGeometry = new CombinedGeometry(GeometryCombineMode.Xor, outer, inner);
            }

            context.AddBlack(borderGeometry);

            // Skia advances using the half-border-adjusted x/y; replicate.
            double halfBorder = border / 2.0;
            return CalculateNextDefaultPosition(x + halfBorder, y + halfBorder, width, height, graphicEllipse.FieldOrigin != null, FieldOrientation.Normal, currentPosition);
        }
    }
}
