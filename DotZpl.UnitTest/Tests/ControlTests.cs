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

        [TestMethod]
        public void Control_Zoom_EnlargesContent()
        {
            // A small label that fits with margin at zoom 1 (Stretch.None, 1:1), so zooming in grows the
            // ink rather than just filling already-clipped space. Zoom 2 must produce more black pixels.
            (long z1, long z2) = StaRunner.Run(() =>
            {
                var v1 = new ZplLabelView { Zpl = Zpl, LabelWidth = 50, LabelHeight = 30, PrintDensityDpmm = 8, Stretch = Stretch.None };
                long a = CountDark(Render(v1, new Size(800, 600)).px);

                var v2 = new ZplLabelView { Zpl = Zpl, LabelWidth = 50, LabelHeight = 30, PrintDensityDpmm = 8, Stretch = Stretch.None, Zoom = 2.0 };
                long b = CountDark(Render(v2, new Size(800, 600)).px);
                return (a, b);
            });

            Assert.IsTrue(z1 > 100, "label rendered at zoom 1");
            Assert.IsTrue(z2 > z1 * 1.5, $"zoom 2 should enlarge the ink (z1={z1}, z2={z2})");
        }

        [TestMethod]
        public void Control_ZoomBy_AnchorsAtCursor()
        {
            // Zoom toward the box's own centroid: the box must stay centred on that point. We render,
            // find the dark centroid, ZoomBy(2) at it, render again, and require the centroid to be
            // essentially unmoved — that's the zoom-to-cursor anchoring invariant.
            (double dx, double dy) = StaRunner.Run(() =>
            {
                var view = new ZplLabelView { Zpl = Zpl, LabelWidth = 50, LabelHeight = 30, PrintDensityDpmm = 8, Stretch = Stretch.Uniform };
                var (_, px1, w1, h1) = Render(view, new Size(800, 600));
                (double cx1, double cy1) = DarkCentroid(px1, w1, h1);

                view.ZoomBy(2.0, new Point(cx1, cy1));
                var (_, px2, w2, h2) = Render(view, new Size(800, 600));
                (double cx2, double cy2) = DarkCentroid(px2, w2, h2);

                return (Math.Abs(cx2 - cx1), Math.Abs(cy2 - cy1));
            });

            Assert.IsTrue(dx < 3.0 && dy < 3.0, $"zoom must stay anchored at the cursor (drift dx={dx:F1}, dy={dy:F1})");
        }

        private static (double cx, double cy) DarkCentroid(byte[] bgra, int w, int h)
        {
            double sx = 0, sy = 0; long n = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int p = (y * w + x) * 4;
                    if (IsOpaqueDark(bgra, p)) { sx += x; sy += y; n++; }
                }
            }
            return n == 0 ? (0, 0) : (sx / n, sy / n);
        }

        private static long CountDark(byte[] bgra)
        {
            long n = 0;
            for (int p = 0; p < bgra.Length; p += 4)
            {
                if (IsOpaqueDark(bgra, p)) n++;
            }
            return n;
        }

        // Opaque ink only — the control fills the viewport with a transparent (0,0,0,0) hit-test rect,
        // which would otherwise read as "dark" since its RGB is zero.
        private static bool IsOpaqueDark(byte[] bgra, int p)
            => bgra[p + 3] > 128 && bgra[p] < 64 && bgra[p + 1] < 64 && bgra[p + 2] < 64;

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
