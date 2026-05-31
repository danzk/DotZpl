using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Label.Helpers;

using WpfZpl.Rendering;

namespace WpfZpl.ElementDrawers
{
    internal static class ImageDecoder
    {
        public static BitmapSource? Decode(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return null;
            }

            using var ms = new MemoryStream(data);
            BitmapDecoder decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            return decoder.Frames[0];
        }
    }

    /// <summary>WPF port of <c>GraphicFieldElementDrawer</c> (<c>^GF</c>). Image kept as a raster.</summary>
    public class GraphicFieldWpfElementDrawer : WpfElementDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element is ZplGraphicField;

        public override Point Draw(ZplElementBase element, WpfDrawerOptions options, Point currentPosition, InternationalFont internationalFont)
        {
            if (element is not ZplGraphicField graphicField)
            {
                return currentPosition;
            }

            byte[] imageData = ByteHelper.HexToBytes(graphicField.Data);
            BitmapSource? image = ImageDecoder.Decode(imageData);
            if (image == null)
            {
                return currentPosition;
            }

            double x = graphicField.PositionX;
            double y = graphicField.PositionY;
            if (graphicField.UseDefaultPosition)
            {
                x = currentPosition.X;
                y = currentPosition.Y;
            }

            if (graphicField.FieldTypeset != null)
            {
                y -= image.PixelHeight;
                if (y < 0) y = 0;
            }

            context.AddImage(image, new Rect(x, y, image.PixelWidth, image.PixelHeight));
            return CalculateNextDefaultPosition(x, y, image.PixelWidth, image.PixelHeight, graphicField.FieldOrigin != null, FieldOrientation.Normal, currentPosition);
        }
    }

    /// <summary>WPF port of <c>ImageMoveElementDrawer</c> (<c>^IM</c>).</summary>
    public class ImageMoveWpfElementDrawer : WpfElementDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element is ZplImageMove;

        public override Point Draw(ZplElementBase element, WpfDrawerOptions options, Point currentPosition, InternationalFont internationalFont)
        {
            if (element is not ZplImageMove imageMove)
            {
                return currentPosition;
            }

            byte[] imageData = printerStorage.GetFile(imageMove.StorageDevice, imageMove.ObjectName);
            BitmapSource? image = ImageDecoder.Decode(imageData);
            if (image == null)
            {
                return currentPosition;
            }

            double x = imageMove.PositionX;
            double y = imageMove.PositionY;
            if (imageMove.UseDefaultPosition)
            {
                x = currentPosition.X;
                y = currentPosition.Y;
            }

            if (imageMove.FieldTypeset != null)
            {
                y -= image.PixelHeight;
                if (y < 0) y = 0;
            }

            context.AddImage(image, new Rect(x, y, image.PixelWidth, image.PixelHeight));
            return CalculateNextDefaultPosition(x, y, image.PixelWidth, image.PixelHeight, imageMove.FieldOrigin != null, FieldOrientation.Normal, currentPosition);
        }
    }

    /// <summary>WPF port of <c>RecallGraphicElementDrawer</c> (<c>^XG</c>).</summary>
    public class RecallGraphicWpfElementDrawer : WpfElementDrawerBase
    {
        public override bool CanDraw(ZplElementBase element) => element is ZplRecallGraphic;

        public override Point Draw(ZplElementBase element, WpfDrawerOptions options, Point currentPosition, InternationalFont internationalFont)
        {
            if (element is not ZplRecallGraphic recallGraphic)
            {
                return currentPosition;
            }

            byte[] imageData = printerStorage.GetFile(recallGraphic.StorageDevice, recallGraphic.ImageName);
            BitmapSource? image = ImageDecoder.Decode(imageData);
            if (image == null)
            {
                return currentPosition;
            }

            double x = recallGraphic.PositionX;
            double y = recallGraphic.PositionY;
            if (recallGraphic.UseDefaultPosition)
            {
                x = currentPosition.X;
                y = currentPosition.Y;
            }

            if (recallGraphic.FieldTypeset != null)
            {
                y -= image.PixelHeight;
                if (y < 0) y = 0;
            }

            context.AddImage(image, new Rect(x, y, image.PixelWidth, image.PixelHeight));
            return CalculateNextDefaultPosition(x, y, image.PixelWidth, image.PixelHeight, recallGraphic.FieldOrigin != null, FieldOrientation.Normal, currentPosition);
        }
    }
}
