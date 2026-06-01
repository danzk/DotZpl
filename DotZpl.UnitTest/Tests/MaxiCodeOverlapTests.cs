using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotZpl.UnitTest
{
    /// <summary>
    /// Regression guard: a large glyph drawn over a MaxiCode. The glyph outline and the MaxiCode
    /// hexagons wind opposite ways, so accumulating the label with a fill-rule GeometryGroup would
    /// cancel them in the overlap into white holes (an inverse draw) instead of unioning them. The
    /// orchestrator must use a true boolean union. A union can never have fewer ink pixels than the
    /// Skia reference (which draws the glyph solid), so we assert the candidate keeps its ink.
    /// </summary>
    [TestClass]
    public class MaxiCodeOverlapTests
    {
        public TestContext TestContext { get; set; } = null!;

        private const string Zpl =
            "^XA" +
            "^FO20,20^BD2^FH^FD002840100450000_5B)>_1E01_1D961Z00136071_1DUPSN_1D123X56_1D028_1D_1D001/001_1D011_1DN_1D_1DNEW YORK_1DNY_1E_04^FS" +
            "^FT55,160^A0N,175,220^FVH^FS" +
            "^XZ";

        [TestMethod]
        public void OverlappingGlyph_DoesNotInverseDraw()
        {
            (byte[] skiaPng, byte[] dotPng) = StaRunner.Run(() => RenderHarness.RenderBoth(Zpl, 30, 30, 8));
            int skiaDark = DarkPixels(skiaPng);
            int dotDark = DarkPixels(dotPng);
            TestContext.WriteLine($"dark pixels: skia={skiaDark}, dotzpl={dotDark}");
            Assert.IsTrue(dotDark >= skiaDark * 0.95,
                $"Candidate lost ink vs Skia ({dotDark} vs {skiaDark}) — inverse-draw cancellation in the H/MaxiCode overlap.");
        }

        private static int DarkPixels(byte[] png)
        {
            using var ms = new MemoryStream(png);
            var dec = new PngBitmapDecoder(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var conv = new FormatConvertedBitmap(dec.Frames[0], PixelFormats.Bgra32, null, 0);
            int w = conv.PixelWidth, h = conv.PixelHeight, stride = w * 4;
            var px = new byte[h * stride];
            conv.CopyPixels(px, stride, 0);
            int dark = 0;
            for (int i = 0; i < px.Length; i += 4)
                if (px[i] < 128 && px[i + 1] < 128 && px[i + 2] < 128) dark++;
            return dark;
        }
    }
}
