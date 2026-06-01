using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using DotZpl.Controls;

namespace DotZpl.UnitTest
{
    [TestClass]
    public class ControlTests
    {
        // 50x30 mm @ 8 dpmm = 400 x 240 dots; a box so something draws.
        private const string Zpl = "^XA^FO50,50^GB300,140,6^FS^XZ";

        [TestMethod]
        public void Control_FillsViewport_AndRendersContent()
        {
            var result = StaRunner.Run(() =>
            {
                var view = new ZplLabelView { Zpl = Zpl, LabelWidth = 50, LabelHeight = 30, PrintDensityDpmm = 8 };
                (Size desired, byte[] px, int w, int h) = Render(view, new Size(400, 300));
                return (desired, dark: CountDark(px), white: CountWhite(px));
            });

            // The control is a viewport: it fills the available space (the label is scaled/centred inside).
            Assert.AreEqual(400.0, result.desired.Width, 0.5, "fills available width");
            Assert.AreEqual(300.0, result.desired.Height, 0.5, "fills available height");
            Assert.IsTrue(result.dark > 100, $"box renders black pixels (got {result.dark})");
            Assert.IsTrue(result.white > 1000, $"opaque white label background expected (got {result.white})");
        }

        [TestMethod]
        public void Control_Pan_MovesWithoutResizing()
        {
            // Stretch.None keeps the label at 1:1 and smaller than the viewport, so a moderate pan
            // stays fully inside (no clipping). The black ink count must be identical — proving the pan
            // only translates the label and does not change its size.
            long noPan = StaRunner.Run(() =>
            {
                var view = new ZplLabelView { Zpl = Zpl, LabelWidth = 50, LabelHeight = 30, PrintDensityDpmm = 8, Stretch = Stretch.None };
                return CountDark(Render(view, new Size(800, 600)).px);
            });

            long panned = StaRunner.Run(() =>
            {
                var view = new ZplLabelView { Zpl = Zpl, LabelWidth = 50, LabelHeight = 30, PrintDensityDpmm = 8, Stretch = Stretch.None, OffsetX = 120, OffsetY = 80 };
                return CountDark(Render(view, new Size(800, 600)).px);
            });

            Assert.IsTrue(noPan > 100, "label rendered");
            Assert.AreEqual(noPan, panned, 4, "panning must move the label, not resize it (ink count unchanged)");
        }

        private static long CountDark(byte[] bgra)
        {
            long n = 0;
            for (int p = 0; p < bgra.Length; p += 4)
            {
                if (bgra[p] < 64 && bgra[p + 1] < 64 && bgra[p + 2] < 64) n++;
            }
            return n;
        }

        private static long CountWhite(byte[] bgra)
        {
            long n = 0;
            for (int p = 0; p < bgra.Length; p += 4)
            {
                if (bgra[p] > 200 && bgra[p + 1] > 200 && bgra[p + 2] > 200) n++;
            }
            return n;
        }

        private static (Size desired, byte[] px, int w, int h) Render(FrameworkElement element, Size available)
        {
            element.Measure(available);
            element.Arrange(new Rect(element.DesiredSize));
            element.UpdateLayout();

            int w = Math.Max(1, (int)Math.Ceiling(element.RenderSize.Width));
            int h = Math.Max(1, (int)Math.Ceiling(element.RenderSize.Height));
            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(element);

            var converted = new FormatConvertedBitmap(rtb, PixelFormats.Bgra32, null, 0);
            var px = new byte[w * h * 4];
            converted.CopyPixels(px, w * 4, 0);
            return (element.DesiredSize, px, w, h);
        }
    }
}
