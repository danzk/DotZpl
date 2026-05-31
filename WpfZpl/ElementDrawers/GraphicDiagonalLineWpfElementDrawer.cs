using System.Windows;
using System.Windows.Media;

using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;

using WpfZpl.Rendering;

namespace WpfZpl.ElementDrawers
{
    /// <summary>
    /// WPF port of <c>GraphicDiagonalLineElementDrawer</c> (<c>^GD</c>). The thick diagonal is a filled
    /// parallelogram (StreamGeometry), mirroring the Skia <c>SKPath</c> with MoveTo/RLineTo.
    /// </summary>
    public class GraphicDiagonalLineWpfElementDrawer : WpfElementDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element is ZplGraphicDiagonalLine;

        public override bool IsReverseDraw(ZplElementBase element)
            => element is ZplGraphicDiagonalLine line && line.ReversePrint;

        public override bool IsWhiteDraw(ZplElementBase element)
            => element is ZplGraphicDiagonalLine line && line.LineColor == LineColor.White;

        public override Point Draw(ZplElementBase element, WpfDrawerOptions options, Point currentPosition, InternationalFont internationalFont)
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
                    ctx.BeginFigure(new Point(x, y + height), true, true);
                    ctx.LineTo(new Point(x + border, y + height), false, false);
                    ctx.LineTo(new Point(x + border + width, y), false, false);
                    ctx.LineTo(new Point(x + width, y), false, false);
                }
                else
                {
                    ctx.BeginFigure(new Point(x, y), true, true);
                    ctx.LineTo(new Point(x + border, y), false, false);
                    ctx.LineTo(new Point(x + border + width, y + height), false, false);
                    ctx.LineTo(new Point(x + width, y + height), false, false);
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
