using System;

using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer.Helpers;

using DotZpl.Rendering;
using DotZpl.Text;

using ZXing.OneD;

namespace DotZpl.ElementDrawers
{
    /// <summary>WPF port of <c>Barcode93ElementDrawer</c> (<c>^BA</c>).</summary>
    public class Barcode93ElementDrawer : BarcodeDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element is ZplBarcode93;

        public override Point Draw(ZplElementBase element, DrawerOptions options, Point currentPosition, InternationalFont internationalFont, int printDensityDpmm)
        {
            if (element is not ZplBarcode93 barcode)
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

            var writer = new Code93Writer();
            bool[] result = writer.encode(content);
            int width = result.Length * barcode.ModuleWidth;
            Geometry bars = BoolArrayToGeometry(result, barcode.Height, barcode.ModuleWidth);
            DrawBarcode(bars, x, y, width, barcode.Height, barcode.FieldOrigin != null, barcode.FieldOrientation);

            if (barcode.PrintInterpretationLine)
            {
                double emSize = FontScale.GetBitmappedFontSize("A", Math.Min(barcode.ModuleWidth, 10), printDensityDpmm)!.Value;
                GlyphTypeface gt = options.FontManager.FontLoader("A");
                DrawInterpretationLine(content, gt, emSize, x, y, width, barcode.Height, barcode.FieldOrigin != null, barcode.FieldOrientation, barcode.PrintInterpretationLineAboveCode, options);
            }

            return CalculateNextDefaultPosition(x, y, width, barcode.Height, barcode.FieldOrigin != null, barcode.FieldOrientation, currentPosition);
        }
    }
}
