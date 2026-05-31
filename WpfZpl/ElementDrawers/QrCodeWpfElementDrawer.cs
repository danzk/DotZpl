using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;

using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer.Helpers;

using WpfZpl.Rendering;

using ZXing;
using ZXing.Common;
using ZXing.QrCode;

namespace WpfZpl.ElementDrawers
{
    /// <summary>WPF port of <c>QrCodeElementDrawer</c> (<c>^BQ</c>).</summary>
    public class QrCodeWpfElementDrawer : WpfBarcodeDrawerBase
    {
        private static readonly Regex gs1Regex = new(@"^>;>8(.+)$", RegexOptions.Compiled);

        public override bool CanDraw(ZplElementBase element) => element is ZplQrCode;

        public override Point Draw(ZplElementBase element, WpfDrawerOptions options, Point currentPosition, InternationalFont internationalFont)
        {
            if (element is not ZplQrCode qrcode)
            {
                return currentPosition;
            }

            double x = qrcode.PositionX;
            double y = qrcode.PositionY;
            if (qrcode.UseDefaultPosition)
            {
                x = currentPosition.X;
                y = currentPosition.Y;
            }

            string content = qrcode.Content;
            if (qrcode.HexadecimalIndicator is char hexIndicator)
            {
                content = content.ReplaceHexEscapes(hexIndicator, internationalFont);
            }

            bool gs1Mode = false;
            Match gs1Match = gs1Regex.Match(content);
            if (gs1Match.Success)
            {
                content = gs1Match.Groups[1].Value;
                gs1Mode = true;
            }

            int verticalQuietZone = qrcode.VerticalQuietZone;

            var writer = new QRCodeWriter();
            var encodingOptions = new QrCodeEncodingOptions
            {
                ErrorCorrection = ConvertErrorCorrection(qrcode.ErrorCorrectionLevel),
                QrMaskPattern = qrcode.MaskValue,
                CharacterSet = "UTF-8",
                Margin = 0,
                GS1Format = gs1Mode,
            };
            BitMatrix result = writer.encode(content, BarcodeFormat.QR_CODE, 0, 0, encodingOptions.Hints);

            int width = result.Width * qrcode.MagnificationFactor;
            int height = result.Height * qrcode.MagnificationFactor;
            Geometry bars = BitMatrixToGeometry(result, qrcode.MagnificationFactor);
            DrawBarcode(bars, x, y + verticalQuietZone, width, height + 2 * verticalQuietZone, qrcode.FieldOrigin != null, qrcode.FieldOrientation);

            return CalculateNextDefaultPosition(x, y, width, height + 2 * verticalQuietZone, qrcode.FieldOrigin != null, qrcode.FieldOrientation, currentPosition);
        }

        private static ZXing.QrCode.Internal.ErrorCorrectionLevel ConvertErrorCorrection(ErrorCorrectionLevel errorCorrectionLevel)
        {
            return errorCorrectionLevel switch
            {
                ErrorCorrectionLevel.UltraHighReliability => ZXing.QrCode.Internal.ErrorCorrectionLevel.H,
                ErrorCorrectionLevel.HighReliability => ZXing.QrCode.Internal.ErrorCorrectionLevel.Q,
                ErrorCorrectionLevel.Standard => ZXing.QrCode.Internal.ErrorCorrectionLevel.M,
                ErrorCorrectionLevel.HighDensity => ZXing.QrCode.Internal.ErrorCorrectionLevel.L,
                _ => ZXing.QrCode.Internal.ErrorCorrectionLevel.M,
            };
        }
    }
}
