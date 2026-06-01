using System;

using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer.Helpers;

using DotZpl.Rendering;
using DotZpl.Text;

using ZXing.OneD;

namespace DotZpl.ElementDrawers
{
    /// <summary>WPF port of <c>BarcodeAnsiCodabarElementDrawer</c> (<c>^BK</c>).</summary>
    public class BarcodeAnsiCodabarElementDrawer : BarcodeDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element is ZplBarcodeAnsiCodabar;

        public override Point Draw(ZplElementBase element, DrawerOptions options, Point currentPosition, InternationalFont internationalFont, int printDensityDpmm)
        {
            if (element is not ZplBarcodeAnsiCodabar barcode)
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

            string content = barcode.Content.Trim('*');
            if (barcode.HexadecimalIndicator is char hexIndicator)
            {
                content = content.ReplaceHexEscapes(hexIndicator, internationalFont);
            }

            string interpretation = string.Format("*{0}*", content);

            var writer = new CodaBarWriter();
            bool[] result = writer.encode(content);
            int narrow = barcode.ModuleWidth;
            int wide = (int)Math.Floor(barcode.WideBarToNarrowBarWidthRatio * narrow);
            result = AdjustWidths(result, wide, narrow);
            int width = result.Length;
            Geometry bars = BoolArrayToGeometry(result, barcode.Height);
            DrawBarcode(bars, x, y, width, barcode.Height, barcode.FieldOrigin != null, barcode.FieldOrientation);

            if (barcode.PrintInterpretationLine)
            {
                double emSize = FontScale.GetBitmappedFontSize("A", Math.Min(barcode.ModuleWidth, 10), printDensityDpmm)!.Value;
                GlyphTypeface gt = options.FontManager.FontLoader("A");
                DrawInterpretationLine(interpretation, gt, emSize, x, y, width, barcode.Height, barcode.FieldOrigin != null, barcode.FieldOrientation, barcode.PrintInterpretationLineAboveCode, options);
            }

            return CalculateNextDefaultPosition(x, y, width, barcode.Height, barcode.FieldOrigin != null, barcode.FieldOrientation, currentPosition);
        }
    }
}
