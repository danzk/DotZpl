using System;

using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Analyzer.Helpers;

using DotZpl.Rendering;
using DotZpl.Text;

using ZXing.OneD;

namespace DotZpl.ElementDrawers
{
    /// <summary>WPF port of <c>BarcodeEAN13ElementDrawer</c> (<c>^BE</c>).</summary>
    public class BarcodeEAN13ElementDrawer : BarcodeDrawerBase
    {
        private static readonly bool[] guards = new bool[95];

        static BarcodeEAN13ElementDrawer()
        {
            foreach (int idx in new[] { 0, 2, 46, 48, 92, 94 })
            {
                guards[idx] = true;
            }
        }

        public override bool CanDraw(ZplElementBase element) => element is ZplBarcodeEan13;

        public override Point Draw(ZplElementBase element, ZplRendererOptions options, Point currentPosition, InternationalFont internationalFont, int printDensityDpmm)
        {
            if (element is not ZplBarcodeEan13 barcode)
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

            content = content.PadLeft(12, '0').Substring(0, 12);
            string interpretation = content;

            int checksum = 0;
            for (int i = 0; i < 12; i++)
            {
                checksum += (content[i] - 48) * (9 - i % 2 * 2);
            }

            interpretation = string.Format("{0}{1}", interpretation, checksum % 10);

            var writer = new EAN13Writer();
            bool[] result = writer.encode(content);
            int width = result.Length * barcode.ModuleWidth;
            Geometry bars = BoolArrayToGeometry(result, barcode.Height, barcode.ModuleWidth);
            DrawBarcode(bars, x, y, width, barcode.Height, barcode.FieldOrigin != null, barcode.FieldOrientation);

            if (barcode.PrintInterpretationLine)
            {
                double emSize = FontScale.GetBitmappedFontSize("A", Math.Min(barcode.ModuleWidth, 10), printDensityDpmm)!.Value;
                GlyphTypeface gt = options.FontManager.FontLoader("A");
                if (barcode.PrintInterpretationLineAboveCode)
                {
                    DrawInterpretationLine(interpretation, gt, emSize, x, y, width, barcode.Height, barcode.FieldOrigin != null, barcode.FieldOrientation, true, options);
                }
                else
                {
                    DrawDigitInterpretationLine(guards, null, interpretation, gt, emSize, x, y, width, barcode.Height, barcode.FieldOrigin != null, barcode.FieldOrientation, barcode.ModuleWidth, options, i => (i == 0 || i == 6) ? 4 : 0);
                }
            }

            return CalculateNextDefaultPosition(x, y, width, barcode.Height, barcode.FieldOrigin != null, barcode.FieldOrientation, currentPosition);
        }
    }
}
