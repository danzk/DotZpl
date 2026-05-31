using System;
using System.Collections.Generic;

using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer.Helpers;

using WpfZpl.Rendering;

using ZXing;
using ZXing.Common;
using ZXing.PDF417;
using ZXing.PDF417.Internal;

namespace WpfZpl.ElementDrawers
{
    /// <summary>WPF port of <c>Pdf417ElementDrawer</c> (<c>^B7</c>). Bit-matrix scaling logic copied verbatim.</summary>
    public class Pdf417WpfElementDrawer : WpfBarcodeDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element is ZplPDF417;

        public override Point Draw(ZplElementBase element, WpfDrawerOptions options, Point currentPosition, InternationalFont internationalFont)
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

            BitMatrix defaultBitmatrix = writer.encode(content, BarcodeFormat.PDF_417, 0, 0, hints);

            int barHeight = pdf417.ModuleWidth * 3;
            BitMatrix upscaled = ProportionalUpscale(defaultBitmatrix, pdf417.ModuleWidth);
            BitMatrix result = VerticalScale(upscaled, pdf417.Height, barHeight);

            int width = result.Width;
            int height = result.Height;
            Geometry bars = BitMatrixToGeometry(result, 1);
            DrawBarcode(bars, x, y, width, height, pdf417.FieldOrigin != null, pdf417.FieldOrientation);

            return CalculateNextDefaultPosition(x, y, width, height, pdf417.FieldOrigin != null, pdf417.FieldOrientation, currentPosition);
        }

        private static BitMatrix ProportionalUpscale(BitMatrix old, int scale)
        {
            if (scale == 0 || scale == 1)
            {
                return old;
            }

            BitMatrix upscaled = new(old.Width * scale, old.Height * scale);
            for (int i = 0; i < old.Height; i++)
            {
                BitArray oldRow = old.getRow(i, null);
                for (int j = 0; j < old.Width; j++)
                {
                    if (!oldRow[j])
                    {
                        continue;
                    }

                    upscaled.setRegion(j * scale, i * scale, scale, scale);
                }
            }

            return upscaled;
        }

        private static BitMatrix VerticalScale(BitMatrix oldMatrix, int newBarHeight, int oldBarHeight)
        {
            int width = oldMatrix.Width;
            int rows = oldMatrix.Height / oldBarHeight;

            if (newBarHeight == oldBarHeight || newBarHeight == 0)
            {
                return oldMatrix;
            }

            BitMatrix scaled = new(oldMatrix.Width, rows * newBarHeight);
            for (int i = 0; i < rows; i++)
            {
                BitArray oldRow = oldMatrix.getRow(i * oldBarHeight, null);
                for (int j = 0; j < width; j++)
                {
                    if (!oldRow[j])
                    {
                        continue;
                    }

                    scaled.setRegion(j, i * newBarHeight, 1, newBarHeight);
                }
            }

            return scaled;
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
