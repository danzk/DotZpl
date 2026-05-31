using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using WpfZpl.Controls;

namespace WpfZpl.UnitTest
{
    [TestClass]
    public class ControlTests
    {
        // 50x30 mm @ 8 dpmm = 400 x 240 dots; a centered box so something draws.
        private const string Zpl = "^XA^FO50,50^GB300,140,6^FS^XZ";

        [TestMethod]
        public void Control_ScalesUniformly_AndRendersContent()
        {
            var result = StaRunner.Run(() =>
            {
                var view = new ZplLabelView { Zpl = Zpl, LabelWidth = 50, LabelHeight = 30, PrintDensityDpmm = 8 };
                (Size desired, byte[] px, int w, int h) = Render(view, new Size(400, 300));

                long dark = 0, white = 0;
                for (int i = 0; i < w * h; i++)
                {
                    int p = i * 4;
                    if (px[p] < 64 && px[p + 1] < 64 && px[p + 2] < 64) dark++;
                    else if (px[p] > 200 && px[p + 1] > 200 && px[p + 2] > 200) white++;
                }

                return (desired, dark, white);
            });

            // Uniform fit of 400x240 into 400x300 → scale 1.0 → 400x240, aspect preserved.
            Assert.AreEqual(400.0, result.desired.Width, 1.0, "width fills available");
            Assert.AreEqual(240.0, result.desired.Height, 1.0, "height keeps 400:240 aspect");
            Assert.IsTrue(result.dark > 100, $"box should render black pixels (got {result.dark})");
            Assert.IsTrue(result.white > 1000, $"opaque white background expected (got {result.white})");
        }

        [TestMethod]
        public void Control_Rotation90_SwapsAspect()
        {
            Size desired = StaRunner.Run(() =>
            {
                var view = new ZplLabelView { Zpl = Zpl, LabelWidth = 50, LabelHeight = 30, PrintDensityDpmm = 8, RotationAngle = 90 };
                return Render(view, new Size(400, 300)).desired;
            });

            // 400x240 rotated 90° → 240x400; Uniform into 400x300 → scale 0.75 → 180x300.
            Assert.AreEqual(180.0, desired.Width, 1.5, "rotated width");
            Assert.AreEqual(300.0, desired.Height, 1.5, "rotated height fills available");
        }

        private static (Size desired, byte[] px, int w, int h) Render(FrameworkElement element, Size available)
        {
            element.Measure(available);
            element.Arrange(new Rect(element.DesiredSize));
            element.UpdateLayout();

            int w = Math.Max(1, (int)Math.Ceiling(element.DesiredSize.Width));
            int h = Math.Max(1, (int)Math.Ceiling(element.DesiredSize.Height));
            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(element);

            var converted = new FormatConvertedBitmap(rtb, PixelFormats.Bgra32, null, 0);
            var px = new byte[w * h * 4];
            converted.CopyPixels(px, w * 4, 0);
            return (element.DesiredSize, px, w, h);
        }
    }
}
