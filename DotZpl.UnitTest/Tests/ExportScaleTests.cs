using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using BinaryKits.Zpl.Analyzer;
using BinaryKits.Zpl.Label.Elements;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using DotZpl.Rendering;

namespace DotZpl.UnitTest
{
    /// <summary>
    /// DrawPng's integer supersample factor must enlarge the raster by exactly that factor in each
    /// dimension while drawing the same content (more pixels per ZPL dot), not crop or rescale it.
    /// </summary>
    [TestClass]
    public class ExportScaleTests
    {
        // Box touching all four sides of the 50x30mm @ 8dpmm = 400x240 label, so a clipped (un-scaled)
        // render would lose ink near the right/bottom edges.
        private const string Zpl = "^XA^FO10,10^GB380,220,8^FS^XZ";

        [DataTestMethod]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(4)]
        public void DrawPng_ScalesDimensionsByFactor(int scale)
        {
            (int w, int h, long ink) = RenderInk(scale);

            Assert.AreEqual(400 * scale, w, "width scales by the factor");
            Assert.AreEqual(240 * scale, h, "height scales by the factor");
            Assert.IsTrue(ink > 100, "content rendered");
        }

        [TestMethod]
        public void DrawPng_Scale_PreservesContent_NotCropped()
        {
            (_, _, long ink1) = RenderInk(1);
            (_, _, long ink3) = RenderInk(3);

            // Each dot becomes 3x3 px, so ink area grows ~9x. Border stroke width also scales, so allow a
            // wide tolerance — the point is that it grew roughly with area (content filled the larger
            // buffer), not stayed flat (which would mean the draw was clipped to the top-left).
            double ratio = (double)ink3 / ink1;
            Assert.IsTrue(ratio > 7.0 && ratio < 11.0, $"ink should grow ~9x with a 3x supersample (got {ratio:F2}x)");
        }

        private static (int w, int h, long ink) RenderInk(int scale)
        {
            return StaRunner.Run(() =>
            {
                var storage = new PrinterStorage();
                IList<ZplElementBase> els = new ZplAnalyzer(storage).Analyze(Zpl).LabelInfos[0].ZplElements;
                byte[] png = new ZplRenderer(storage, new ZplRendererOptions { OpaqueBackground = true })
                    .DrawPng(els, 50, 30, 8, scale);

                using var ms = new MemoryStream(png);
                var dec = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                var fr = new FormatConvertedBitmap(dec.Frames[0], PixelFormats.Bgra32, null, 0);
                int w = fr.PixelWidth, h = fr.PixelHeight;
                var px = new byte[w * h * 4];
                fr.CopyPixels(px, w * 4, 0);

                long ink = 0;
                for (int p = 0; p < px.Length; p += 4)
                {
                    if (px[p] < 64 && px[p + 1] < 64 && px[p + 2] < 64)
                    {
                        ink++;
                    }
                }
                return (w, h, ink);
            });
        }
    }
}
