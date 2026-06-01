using System;

using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Analyzer.Helpers;
using BinaryKits.Zpl.Analyzer.Symologies;

using DotZpl.Rendering;
using DotZpl.Text;

namespace DotZpl.ElementDrawers
{
    /// <summary>WPF port of <c>BarcodeUpcExtensionElementDrawer</c>.</summary>
    public class BarcodeUpcExtensionElementDrawer : BarcodeDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element is ZplBarcodeUpcExtension;

        public override Point Draw(ZplElementBase element, ZplRendererOptions options, Point currentPosition, InternationalFont internationalFont, int printDensityDpmm)
        {
            if (element is not ZplBarcodeUpcExtension barcode)
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

            if (content.Length <= 2)
            {
                content = content.PadLeft(2, '0');
            }
            else
            {
                content = content.PadLeft(5, '0').Substring(0, 5);
            }

            string interpretation = content;

            bool[] data = UpcExtensionSymbology.Encode(content);
            int width = data.Length * barcode.ModuleWidth;
            Geometry bars = BoolArrayToGeometry(data, barcode.Height, barcode.ModuleWidth);
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
