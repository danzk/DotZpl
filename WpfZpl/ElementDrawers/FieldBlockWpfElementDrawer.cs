using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    /// WPF port of <c>FieldBlockElementDrawer</c> (<c>^FB</c>). Word-wraps, lays out multiple baseline
    /// lines and applies per-line justification, mirroring the Skia drawer exactly.
    /// </summary>
    public class FieldBlockWpfElementDrawer : WpfElementDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element is ZplFieldBlock;

        public override bool IsReverseDraw(ZplElementBase element)
            => element is ZplFieldBlock fieldBlock && fieldBlock.ReversePrint;

        public override Point Draw(ZplElementBase element, WpfDrawerOptions options, Point currentPosition, InternationalFont internationalFont, int printDensityDpmm)
        {
            if (element is not ZplFieldBlock fieldBlock)
            {
                return currentPosition;
            }

            ZplFont font = fieldBlock.Font;
            (float fontSize, float scaleX) = FontScale.GetFontScaling(font.FontName, font.FontHeight, font.FontWidth, printDensityDpmm);

            GlyphTypeface gt = options.FontManager.FontLoader(font.FontName);
            Typeface tf = options.FontManager.TypefaceLoader(font.FontName);

            string text = fieldBlock.Text;
            if (fieldBlock.HexadecimalIndicator is char hexIndicator)
            {
                text = text.ReplaceHexEscapes(hexIndicator, internationalFont);
            }

            if (font.FontName == "0")
            {
                if (options.ReplaceDashWithEnDash)
                {
                    text = text.Replace("-", " – ");
                }

                if (options.ReplaceUnderscoreWithEnSpace)
                {
                    text = text.Replace('_', ' ');
                }
            }

            double capHeight = WpfTextRenderer.CapHeight(gt, fontSize);

            double x = fieldBlock.PositionX;
            double y = fieldBlock.PositionY + capHeight;
            if (fieldBlock.UseDefaultPosition)
            {
                x = currentPosition.X;
                y = currentPosition.Y + capHeight;
            }

            List<string> textLines = WordWrap(text, gt, fontSize, scaleX, fieldBlock.Width);
            int hangingIndent = 0;
            double lineHeight = fontSize + fieldBlock.LineSpace;

            // The ZPL printer does not include trailing line spacing in the total height.
            double totalHeight = lineHeight * fieldBlock.MaxLineCount - fieldBlock.LineSpace;

            if (fieldBlock.FieldTypeset != null)
            {
                totalHeight = lineHeight * (fieldBlock.MaxLineCount - 1) + capHeight;
                y -= totalHeight;
            }

            bool pushed = false;
            if (fieldBlock.FieldOrigin != null)
            {
                switch (font.FieldOrientation)
                {
                    case FieldOrientation.Rotated90:
                        pushed = Push(new RotateTransform(90, fieldBlock.PositionX + totalHeight / 2, fieldBlock.PositionY + totalHeight / 2));
                        break;
                    case FieldOrientation.Rotated180:
                        pushed = Push(new RotateTransform(180, fieldBlock.PositionX + fieldBlock.Width / 2.0, fieldBlock.PositionY + totalHeight / 2));
                        break;
                    case FieldOrientation.Rotated270:
                        pushed = Push(new RotateTransform(270, fieldBlock.PositionX + fieldBlock.Width / 2.0, fieldBlock.PositionY + fieldBlock.Width / 2.0));
                        break;
                }
            }
            else
            {
                switch (font.FieldOrientation)
                {
                    case FieldOrientation.Rotated90:
                        pushed = Push(new RotateTransform(90, fieldBlock.PositionX, fieldBlock.PositionY));
                        break;
                    case FieldOrientation.Rotated180:
                        pushed = Push(new RotateTransform(180, fieldBlock.PositionX, fieldBlock.PositionY));
                        break;
                    case FieldOrientation.Rotated270:
                        pushed = Push(new RotateTransform(270, fieldBlock.PositionX, fieldBlock.PositionY));
                        break;
                }
            }

            foreach (string textLine in textLines)
            {
                x = fieldBlock.PositionX + hangingIndent;

                Rect textBounds = WpfTextRenderer.MeasureTightBounds(gt, textLine, fontSize, scaleX);
                double diff = fieldBlock.Width - textBounds.Width;

                switch (fieldBlock.TextJustification)
                {
                    case TextJustification.Center:
                        x += diff / 2 - textBounds.Left;
                        break;
                    case TextJustification.Right:
                        x += diff - textBounds.Left * 2;
                        hangingIndent = -fieldBlock.HangingIndent;
                        break;
                    case TextJustification.Left:
                    case TextJustification.Justified:
                    default:
                        hangingIndent = fieldBlock.HangingIndent;
                        break;
                }

                Geometry geometry = WpfTextRenderer.BuildGeometry(
                    options.TextBackend, gt, tf, textLine, fontSize, scaleX, new Point(x, y));
                context.AddBlack(geometry);
                y += lineHeight;
            }

            if (pushed)
            {
                context.Pop();
            }

            return CalculateNextDefaultPosition(fieldBlock.PositionX, fieldBlock.PositionY, fieldBlock.Width, totalHeight, fieldBlock.FieldOrigin != null, font.FieldOrientation, currentPosition);
        }

        private bool Push(Transform t)
        {
            context.PushTransform(t);
            return true;
        }

        private static List<string> WordWrap(string text, GlyphTypeface gt, double emSize, double scaleX, int maxWidth)
        {
            double spaceWidth = WpfTextRenderer.MeasureAdvance(gt, " ", emSize, scaleX);
            List<string> lines = new();

            Stack<string> words = new(text.Split(new[] { ' ' }, StringSplitOptions.None).AsEnumerable().Reverse());
            StringBuilder line = new();
            double width = 0;
            while (words.Count != 0)
            {
                string word = words.Pop();
                if (word.Contains(@"\&"))
                {
                    string[] subwords = word.Split(new[] { @"\&" }, 2, StringSplitOptions.None);
                    word = subwords[0];
                    words.Push(subwords[1]);
                    double wordWidth = WpfTextRenderer.MeasureAdvance(gt, word, emSize, scaleX);
                    if (width + wordWidth <= maxWidth)
                    {
                        line.Append(word);
                        lines.Add(line.ToString());
                        line = new StringBuilder();
                        width = 0;
                    }
                    else
                    {
                        if (line.Length > 0)
                        {
                            lines.Add(line.ToString().Trim());
                        }

                        lines.Add(word);
                        line = new StringBuilder();
                        width = 0;
                    }
                }
                else
                {
                    double wordWidth = WpfTextRenderer.MeasureAdvance(gt, word, emSize, scaleX);
                    if (width + wordWidth <= maxWidth)
                    {
                        line.Append(word + " ");
                        width += wordWidth + spaceWidth;
                    }
                    else
                    {
                        if (line.Length > 0)
                        {
                            lines.Add(line.ToString().Trim());
                        }

                        line = new StringBuilder(word + " ");
                        width = wordWidth + spaceWidth;
                    }
                }
            }

            lines.Add(line.ToString().Trim());
            return lines;
        }
    }
}
