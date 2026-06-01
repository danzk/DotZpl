
using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;

using DotZpl.Rendering;

namespace DotZpl.ElementDrawers
{
    /// <summary>
    /// WPF port of <c>GraphicDiagonalLineElementDrawer</c> (<c>^GD</c>). The thick diagonal is a filled
    /// parallelogram (StreamGeometry), mirroring the Skia <c>SKPath</c> with MoveTo/RLineTo.
    /// </summary>
    public class GraphicDiagonalLineElementDrawer : ElementDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element is ZplGraphicDiagonalLine;

        public override bool IsReverseDraw(ZplElementBase element)
            => element is ZplGraphicDiagonalLine line && line.ReversePrint;

        public override bool IsWhiteDraw(ZplElementBase element)
            => element is ZplGraphicDiagonalLine line && line.LineColor == LineColor.White;

        public override Point Draw(ZplElementBase element, DrawerOptions options, Point currentPosition, InternationalFont internationalFont)
        {
            if (element is not ZplGraphicDiagonalLine graphicLine)
            {
                return currentPosition;
            }

            int border = graphicLine.BorderThickness;
            int width = graphicLine.Width;
            int height = graphicLine.Height;

            double x = graphicLine.PositionX;
            double y = graphicLine.PositionY;
            if (graphicLine.UseDefaultPosition)
            {
                x = currentPosition.X;
                y = currentPosition.Y;
            }

            if (graphicLine.FieldTypeset != null)
            {
                y -= height;
                if (y < 0) y = 0;
            }

            var geometry = new StreamGeometry();
            using (StreamGeometryContext ctx = geometry.Open())
            {
                if (graphicLine.RightLeaningDiagonal)
                {
                    ctx.Begin(new Point(x, y + height));
                    ctx.Line(new Point(x + border, y + height));
                    ctx.Line(new Point(x + border + width, y));
                    ctx.Line(new Point(x + width, y));
                    ctx.End();
                }
                else
                {
                    ctx.Begin(new Point(x, y));
                    ctx.Line(new Point(x + border, y));
                    ctx.Line(new Point(x + border + width, y + height));
                    ctx.Line(new Point(x + width, y + height));
                    ctx.End();
                }
            }

            if (!graphicLine.ReversePrint && graphicLine.LineColor == LineColor.White)
            {
                context.AddWhite(geometry);
            }
            else
            {
                context.AddBlack(geometry);
            }

            return CalculateNextDefaultPosition(x, y, width, height, graphicLine.FieldOrigin != null, FieldOrientation.Normal, currentPosition);
        }
    }
}
