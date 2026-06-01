using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using DotZpl.Rendering;
using DotZpl.Text;

// BinaryKits.Zpl.Viewer's PrinterStorage / ZplAnalyzer are non-colliding utilities; the rest of
// that namespace (ZplElementDrawer / DrawerOptions / FontManager) collides with our types.
using PrinterStorage = BinaryKits.Zpl.Viewer.PrinterStorage;
using ZplAnalyzer = BinaryKits.Zpl.Viewer.ZplAnalyzer;

namespace DotZpl.UnitTest
{
    /// <summary>
    /// The real Zebra font "0" is condensed. When it resolves to a non-condensed fallback (plain Arial,
    /// on a machine without one of the <see cref="FontManager.FontStack0"/> condensed fonts), the
    /// renderer squeezes it horizontally (<see cref="FontManager.Font0FallbackCondense"/>) to
    /// approximate font 0. Arial is always present, so these tests are deterministic.
    /// </summary>
    [TestClass]
    public class Font0CondenseTests
    {
        public TestContext TestContext { get; set; } = null!;

        private const string Zpl = "^XA^FO10,10^A0N,40^FDCondensed WWWMMMiii 12345^FS^XZ";

        [TestMethod]
        public void Font0_ArialFallback_IsCondensedHorizontally()
        {
            (int condensedW, int condensedH) = ArialFont0Ink(0.86);
            (int naturalW, int naturalH) = ArialFont0Ink(1.0);
            double ratio = (double)condensedW / naturalW;
            TestContext.WriteLine($"Arial font0 width: natural={naturalW}, condensed={condensedW}, ratio={ratio:0.000}");

            Assert.AreEqual(0.86, ratio, 0.03, "Arial fallback should be squeezed to ~0.86 of its natural width");
            Assert.AreEqual(naturalH, condensedH, 1, "condense is horizontal only — height is unchanged");
        }

        [TestMethod]
        public void HorizontalScale_OnlyAffectsFont0()
        {
            var mgr = new FontManager { FontStack0 = new List<string> { "Arial" } };
            Assert.AreEqual(0.86, mgr.GetHorizontalScale("0"), 1e-9, "font 0 on the Arial fallback condenses");
            Assert.AreEqual(1.0, mgr.GetHorizontalScale("A"), 1e-9, "pixel font A is never condensed");
            Assert.AreEqual(1.0, mgr.GetHorizontalScale("C"), 1e-9, "pixel font C is never condensed");

            mgr.Font0FallbackCondense = 1.0;
            Assert.AreEqual(1.0, mgr.GetHorizontalScale("0"), 1e-9, "disabling the factor renders raw Arial");
        }

        private static (int w, int h) ArialFont0Ink(double condense)
        {
            return StaRunner.Run(() =>
            {
                var mgr = new FontManager
                {
                    FontStack0 = new List<string> { "Arial" },   // force the no-condensed-font fallback
                    Font0FallbackCondense = condense,
                };
                var options = new DrawerOptions(mgr) { OpaqueBackground = true };
                var storage = new PrinterStorage();
                var elements = new ZplAnalyzer(storage).Analyze(Zpl).LabelInfos[0].ZplElements;
                byte[] png = new ZplElementDrawer(storage, options).DrawPng(elements, 100, 20, 8);
                return InkSize(png);
            });
        }

        private static (int w, int h) InkSize(byte[] png)
        {
            using var ms = new MemoryStream(png);
            var decoder = new PngBitmapDecoder(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var converted = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
            int w = converted.PixelWidth, h = converted.PixelHeight, stride = w * 4;
            var px = new byte[h * stride];
            converted.CopyPixels(px, stride, 0);

            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * stride + x * 4;
                    if (px[i] < 128 && px[i + 1] < 128 && px[i + 2] < 128)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            return maxX < 0 ? (0, 0) : (maxX - minX + 1, maxY - minY + 1);
        }
    }
}
