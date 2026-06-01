using System;

using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer.Helpers;

using DotZpl.Rendering;
using DotZpl.Text;

using ZXing.OneD;

namespace DotZpl.ElementDrawers
{
    /// <summary>WPF port of <c>Interleaved2of5BarcodeDrawer</c> (<c>^B2</c>).</summary>
    public class Interleaved2of5ElementDrawer : BarcodeDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element is ZplBarcodeInterleaved2of5;

        public override Point Draw(ZplElementBase element, DrawerOptions options, Point currentPosition, InternationalFont internationalFont)
        {
            if (element is not ZplBarcodeInterleaved2of5 barcode)
            {
                return currentPosition;
            }

            double x = barcode.PositionX;
            double y = barcode.PositionY;
            if (barcode.UseDefaultPosition)
            {
                x = currentPosition.X;
                y = currentPosition.Y;
            }

            string content = barcode.Content;
            if (barcode.HexadecimalIndicator is char hexIndicator)
            {
                content = content.ReplaceHexEscapes(hexIndicator, internationalFont);
            }

            if (barcode.Mod10CheckDigit)
            {
                int sum = 0;
                for (int i = 0; i < content.Length; i++)
                {
                    if (!char.IsDigit(content[i]))
                    {
                        return currentPosition;
                    }

                    int digit = content[i] - '0';
                    int weight = ((content.Length - i) % 2 == 0) ? 3 : 1;
                    sum += digit * weight;
                }

                int checkDigit = (10 - (sum % 10)) % 10;
                content = $"{content}{checkDigit}";
            }

            if (content.Length % 2 != 0)
            {
                content = $"0{content}";
            }

            var writer = new ITFWriter();
            bool[] result = writer.encode(content);
            int narrow = barcode.ModuleWidth;
            int wide = (int)Math.Floor(barcode.WideBarToNarrowBarWidthRatio * narrow);
            result = AdjustWidths(result, wide, narrow);
            int width = result.Length;
            Geometry bars = BoolArrayToGeometry(result, barcode.Height);
            DrawBarcode(bars, x, y, width, barcode.Height, barcode.FieldOrigin != null, barcode.FieldOrientation);

            if (barcode.PrintInterpretationLine)
            {
                double emSize = Math.Min(barcode.ModuleWidth * 10.0, 100.0);
                GlyphTypeface gt = options.FontManager.FontLoader("A");
                DrawInterpretationLine(content, gt, emSize, x, y, width, barcode.Height, barcode.FieldOrigin != null, barcode.FieldOrientation, barcode.PrintInterpretationLineAboveCode, options);
            }

            return CalculateNextDefaultPosition(x, y, width, barcode.Height, barcode.FieldOrigin != null, barcode.FieldOrientation, currentPosition);
        }
    }
}
