
using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;

using DotZpl.Rendering;
using DotZpl.Text;

namespace DotZpl.ElementDrawers
{
    /// <summary>
    /// WPF port of <c>GraphicSymbolElementDrawer</c> (<c>^GS</c>). Renders a single glyph from the
    /// embedded graphic-symbol font as baseline-anchored geometry.
    /// </summary>
    public class GraphicSymbolElementDrawer : ElementDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element.GetType() == typeof(ZplGraphicSymbol);

        public override Point Draw(ZplElementBase element, DrawerOptions options, Point currentPosition, InternationalFont internationalFont, int printDensityDpmm)
        {
            if (element is not ZplGraphicSymbol graphicSymbol)
            {
                return currentPosition;
            }

            double x = graphicSymbol.PositionX;
            double y = graphicSymbol.PositionY;
            FieldJustification fieldJustification = FieldJustification.None;

            if (graphicSymbol.UseDefaultPosition)
            {
                x = currentPosition.X;
                y = currentPosition.Y;
            }

            (float fontSize, float scaleX) = FontScale.GetFontScaling("GS", graphicSymbol.Height, graphicSymbol.Width, printDensityDpmm);

            // remove incorrect scaling (mirrors Skia)
            fontSize /= 1.1f;
            double emSize = fontSize * 1.25;

            GlyphTypeface gt = options.FontManager.GlyphGS;

            string displayText = $"{(char)graphicSymbol.Character}";
            double totalWidth = TextRenderer.MeasureAdvance(gt, displayText, emSize, scaleX);
            Rect textBounds = TextRenderer.MeasureTightBounds(gt, displayText, emSize, scaleX);

            bool pushed = false;
            if (graphicSymbol.FieldOrigin != null)
            {
                switch (graphicSymbol.FieldOrientation)
                {
                    case FieldOrientation.Rotated90:
                        pushed = Push(new RotateTransform(90, x + fontSize / 2, y + fontSize / 2));
                        break;
                    case FieldOrientation.Rotated180:
                        pushed = Push(new RotateTransform(180, x + textBounds.Width / 2, y + fontSize / 2));
                        break;
                    case FieldOrientation.Rotated270:
                        pushed = Push(new RotateTransform(270, x + textBounds.Width / 2, y + textBounds.Width / 2));
                        break;
                }

                fieldJustification = graphicSymbol.FieldOrigin.FieldJustification;
            }
            else
            {
                switch (graphicSymbol.FieldOrientation)
                {
                    case FieldOrientation.Rotated90:
                        pushed = Push(new RotateTransform(90, x, y));
                        break;
                    case FieldOrientation.Rotated180:
                        pushed = Push(new RotateTransform(180, x, y));
                        break;
                    case FieldOrientation.Rotated270:
                        pushed = Push(new RotateTransform(270, x, y));
                        break;
                }

                fieldJustification = graphicSymbol.FieldTypeset.FieldJustification;
            }

            if (graphicSymbol.FieldTypeset == null)
            {
                y += fontSize;
            }

            double originX = x;
            if (fieldJustification == FieldJustification.Right)
            {
                originX = x - totalWidth;
            }

            Geometry geometry = TextRenderer.BuildGeometryGlyphRun(gt, displayText, emSize, scaleX, new Point(originX, y));
            context.AddBlack(geometry);

            if (pushed)
            {
                context.Pop();
            }

            return CalculateNextDefaultPosition(x, y, totalWidth, textBounds.Height, false, graphicSymbol.FieldOrientation, currentPosition);
        }

        private bool Push(Transform t)
        {
            context.PushTransform(t);
            return true;
        }
    }
}
