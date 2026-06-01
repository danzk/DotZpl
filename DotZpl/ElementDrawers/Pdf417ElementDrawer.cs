using System;
using System.Collections.Generic;

using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Analyzer.Helpers;

using DotZpl.Rendering;

using ZXing;
using ZXing.Common;
using ZXing.PDF417;
using ZXing.PDF417.Internal;

namespace DotZpl.ElementDrawers
{
    /// <summary>
    /// Port of <c>Pdf417ElementDrawer</c> (<c>^B7</c>). Renders at module resolution and lets the
    /// <c>DrawBarcode</c> scale transform handle the pixel sizing — the Skia port materialised an
    /// upscaled pixel <see cref="BitMatrix"/> and emitted one rectangle per pixel-row run (which is
    /// fine for Skia because it then becomes a raster blit), but in our vector pipeline that
    /// inflated the rectangle count by <c>pdf417.Height</c> and was the dominant render cost.
    /// </summary>
    public class Pdf417ElementDrawer : BarcodeDrawerBase
    {
        // PDF417_ASPECT_RATIO=A3: ZXing writes each codeword bar across 3 identical pixel rows in the
        // matrix to encode the 3:1 height/width aspect ratio. We sample one row per bar instead.
        private const int AspectRatio = 3;

        public override bool CanDraw(ZplElementBase element) => element is ZplPDF417;

        public override Point Draw(ZplElementBase element, ZplRendererOptions options, Point currentPosition, InternationalFont internationalFont)
        {
            if (element is not ZplPDF417 pdf417)
            {
                return currentPosition;
            }

            if (pdf417.Height == 0)
            {
                throw new Exception("PDF417 Height is set to zero.");
            }

            string content = pdf417.Content;
            if (pdf417.HexadecimalIndicator is char hexIndicator)
            {
                content = content.ReplaceHexEscapes(hexIndicator, internationalFont);
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                throw new Exception("PDF147 Content is empty.");
            }

            double x = pdf417.PositionX;
            double y = pdf417.PositionY;
            if (pdf417.UseDefaultPosition)
            {
                x = currentPosition.X;
                y = currentPosition.Y;
            }

            int mincols, maxcols, minrows, maxrows;
            if (pdf417.Rows != null)
            {
                minrows = pdf417.Rows.Value;
                maxrows = pdf417.Rows.Value;
            }
            else
            {
                minrows = 3;
                maxrows = 90;
            }

            if (pdf417.Columns != null)
            {
                mincols = pdf417.Columns.Value;
                maxcols = pdf417.Columns.Value;
            }
            else
            {
                mincols = 1;
                maxcols = 30;

                if (pdf417.Rows != null)
                {
                    minrows /= 2;
                }
            }

            var writer = new PDF417Writer();
            var hints = new Dictionary<EncodeHintType, object>
            {
                { EncodeHintType.PDF417_COMPACT, pdf417.Compact },
                { EncodeHintType.PDF417_COMPACTION, Compaction.AUTO },
                { EncodeHintType.PDF417_ASPECT_RATIO, PDF417AspectRatio.A3 },
                { EncodeHintType.PDF417_IMAGE_ASPECT_RATIO, 1.0f },
                { EncodeHintType.MARGIN, 0 },
                { EncodeHintType.ERROR_CORRECTION, ConvertErrorCorrection(pdf417.SecurityLevel) },
                { EncodeHintType.PDF417_DIMENSIONS, new Dimensions(mincols, maxcols, minrows, maxrows) },
            };

            BitMatrix matrix = writer.encode(content, BarcodeFormat.PDF_417, 0, 0, hints);

            // Module-resolution geometry: one rect per horizontal run per bar (one in AspectRatio
            // rows). The DrawBarcode scale transform sizes each module to (moduleWidth × Height).
            int moduleRows = matrix.Height / AspectRatio;
            int width = matrix.Width * pdf417.ModuleWidth;
            int height = moduleRows * pdf417.Height;
            Geometry bars = BitMatrixToGeometry(matrix, pixelScale: 1, rowStride: AspectRatio);
            DrawBarcode(bars, x, y, width, height, pdf417.FieldOrigin != null, pdf417.FieldOrientation,
                scaleX: pdf417.ModuleWidth, scaleY: pdf417.Height);

            return CalculateNextDefaultPosition(x, y, width, height, pdf417.FieldOrigin != null, pdf417.FieldOrientation, currentPosition);
        }

        private static PDF417ErrorCorrectionLevel ConvertErrorCorrection(int correction)
        {
            return correction switch
            {
                0 => PDF417ErrorCorrectionLevel.L0,
                1 => PDF417ErrorCorrectionLevel.L1,
                2 => PDF417ErrorCorrectionLevel.L2,
                3 => PDF417ErrorCorrectionLevel.L3,
                4 => PDF417ErrorCorrectionLevel.L4,
                5 => PDF417ErrorCorrectionLevel.L5,
                6 => PDF417ErrorCorrectionLevel.L6,
                7 => PDF417ErrorCorrectionLevel.L7,
                8 => PDF417ErrorCorrectionLevel.L8,
                _ => PDF417ErrorCorrectionLevel.AUTO,
            };
        }
    }
}
