using System.Windows;
using System.Windows.Media;

using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer.Helpers;

using WpfZpl.Rendering;
using WpfZpl.Text;

namespace WpfZpl.ElementDrawers
{
    /// <summary>
    /// WPF port of <c>TextFieldElementDrawer</c> (<c>^FD</c> text). Text is baseline-anchored geometry,
    /// reproducing Skia's <c>DrawShapedText</c> baseline math (<c>y += capHeight</c>), rotation pivots
    /// and Left/Right/Auto justification.
    /// </summary>
    public class TextFieldWpfElementDrawer : WpfElementDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element.GetType() == typeof(ZplTextField);

        public override bool IsReverseDraw(ZplElementBase element)
            => element is ZplTextField textField && textField.ReversePrint;

        public override Point Draw(ZplElementBase element, WpfDrawerOptions options, Point currentPosition, InternationalFont internationalFont, int printDensityDpmm)
        {
            if (element is not ZplTextField textField)
            {
                return currentPosition;
            }

            double x = textField.PositionX;
            double y = textField.PositionY;
            FieldJustification fieldJustification = FieldJustification.None;

            if (textField.UseDefaultPosition)
            {
                x = currentPosition.X;
                y = currentPosition.Y;
            }

            ZplFont font = textField.Font;
            bool isPixelFont = options.FontManager.IsPixelFont(font.FontName);
            (float fontSize, float scaleX) = isPixelFont
                ? FontScale.GetPixelFontScaling(font.FontName, font.FontHeight, font.FontWidth, printDensityDpmm)
                : FontScale.GetFontScaling(font.FontName, font.FontHeight, font.FontWidth, printDensityDpmm);

            GlyphTypeface gt = options.FontManager.FontLoader(font.FontName);
            Typeface tf = options.FontManager.TypefaceLoader(font.FontName);

            string displayText = textField.Text;
            if (textField.HexadecimalIndicator is char hexIndicator)
            {
                displayText = displayText.ReplaceHexEscapes(hexIndicator, internationalFont);
            }

            if (font.FontName == "0")
            {
                if (options.ReplaceDashWithEnDash)
                {
                    displayText = displayText.Replace("-", " – ");
                }

                if (options.ReplaceUnderscoreWithEnSpace)
                {
                    displayText = displayText.Replace('_', ' ');
                }
            }

            double capHeight = WpfTextRenderer.CapHeight(gt, fontSize);
            double totalWidth = WpfTextRenderer.MeasureAdvance(gt, displayText, fontSize, scaleX);
            Rect tightBounds = WpfTextRenderer.MeasureTightBounds(gt, displayText, fontSize, scaleX);

            // Rotation pivots use the pre-baseline x/y (mirrors the Skia ordering).
            bool pushed = false;
            if (textField.FieldOrigin != null)
            {
                switch (font.FieldOrientation)
                {
                    case FieldOrientation.Rotated90:
                        pushed = Push(new RotateTransform(90, x + fontSize / 2, y + fontSize / 2));
                        break;
                    case FieldOrientation.Rotated180:
                        pushed = Push(new RotateTransform(180, x + tightBounds.Width / 2, y + fontSize / 2));
                        break;
                    case FieldOrientation.Rotated270:
                        pushed = Push(new RotateTransform(270, x + tightBounds.Width / 2, y + tightBounds.Width / 2));
                        break;
                }

                fieldJustification = textField.FieldOrigin.FieldJustification;
            }
            else
            {
                switch (font.FieldOrientation)
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

                fieldJustification = textField.FieldTypeset.FieldJustification;
            }

            if (textField.FieldTypeset == null)
            {
                y += capHeight;
            }

            // Left/Right/Auto -> emulate alignment by shifting the (left-origin) baseline.
            double originX = x;
            if (fieldJustification == FieldJustification.Right)
            {
                originX = x - totalWidth;
            }
            else if (fieldJustification == FieldJustification.Auto && IsRightToLeft(displayText))
            {
                originX = x - totalWidth;
            }

            Geometry geometry = WpfTextRenderer.BuildGeometry(
                options.TextBackend, gt, tf, displayText, fontSize, scaleX, new Point(originX, y));
            context.AddBlack(geometry);

            if (pushed)
            {
                context.Pop();
            }

            return CalculateNextDefaultPosition(x, y, totalWidth, tightBounds.Height, false, font.FieldOrientation, currentPosition);
        }

        private bool Push(Transform t)
        {
            context.PushTransform(t);
            return true;
        }

        internal static bool IsRightToLeft(string text)
        {
            foreach (char c in text)
            {
                if ((c >= 0x0590 && c <= 0x05FF) || // Hebrew
                    (c >= 0x0600 && c <= 0x06FF) || // Arabic
                    (c >= 0x0700 && c <= 0x074F) || // Syriac
                    (c >= 0x0750 && c <= 0x077F) || // Arabic Supplement
                    (c >= 0x08A0 && c <= 0x08FF) || // Arabic Extended-A
                    (c >= 0xFB1D && c <= 0xFDFF) || // Hebrew/Arabic presentation forms
                    (c >= 0xFE70 && c <= 0xFEFF))   // Arabic presentation forms-B
                {
                    return true;
                }
            }

            return false;
        }
    }
}
