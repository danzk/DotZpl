using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using BinaryKits.Zpl.Analyzer;

using DotZpl.Rendering;

namespace DotZpl.UnitTest
{
    /// <summary>
    /// Verifies the embedded pixel fonts render at their Zebra font matrices (Tables 29/31): cell
    /// height = matrix H, character width = matrix W, advance pitch = W + intercharacter gap, and
    /// Font C = 2x Font A. Uses the default (pixel-font) options — not the system-font comparison harness.
    /// </summary>
    [TestClass]
    public class PixelFontTests
    {
        public TestContext TestContext { get; set; } = null!;

        // Rendered at 10x the base matrix so the integer-aligned pixels are easy to measure.
        private const int Scale = 10;

        [TestMethod]
        public void FontA_Caps_MatchMatrix()
        {
            // Matrix A = 9x5, baseline 7. An uppercase 'H' inks 5 wide x 7 tall (cap = baseline).
            (int w, int h) = InkSize("^XA^FO40,40^AAN,90^FDH^FS^XZ");
            TestContext.WriteLine($"Font A 'H' @90: {w}x{h}");
            Assert.AreEqual(5 * Scale, w, Scale, "Font A 'H' width = W (5 dots)");
            Assert.AreEqual(7 * Scale, h, Scale, "Font A 'H' cap height = baseline (7 dots)");
        }

        [TestMethod]
        public void FontA_FullCell_MatchesMatrix()
        {
            // 'Hg' spans cap top (baseline 7) to descender bottom (9 - 7 = 2) = the full 9-dot cell.
            (int _, int h) = InkSize("^XA^FO40,40^AAN,90^FDHg^FS^XZ");
            TestContext.WriteLine($"Font A 'Hg' @90 height: {h}");
            Assert.AreEqual(9 * Scale, h, Scale, "Font A full cell (cap + descender) = H (9 dots)");
        }

        [TestMethod]
        public void FontA_Pitch_IsWidthPlusGap()
        {
            int one = InkSize("^XA^FO40,40^AAN,90^FDH^FS^XZ").w;
            int two = InkSize("^XA^FO40,40^AAN,90^FDHH^FS^XZ").w;
            int pitch = two - one;   // distance the pen advanced between the two H's
            TestContext.WriteLine($"Font A pitch @90: {pitch}");
            Assert.AreEqual(6 * Scale, pitch, Scale, "advance pitch = W (5) + intercharacter gap (1)");
        }

        [TestMethod]
        public void FontB_Caps_MatchMatrix()
        {
            // Matrix B = 11x7, baseline 11 (caps-only, no descenders): an 'H' inks the full 7x11 cell.
            (int w, int h) = InkSize("^XA^FO40,40^ABN,110^FDH^FS^XZ");
            TestContext.WriteLine($"Font B 'H' @110: {w}x{h}");
            Assert.AreEqual(7 * Scale, w, Scale, "Font B 'H' width = W (7 dots)");
            Assert.AreEqual(11 * Scale, h, Scale, "Font B 'H' cap height = baseline = cell (11 dots)");
        }

        [TestMethod]
        public void FontB_Pitch_IsWidthPlusGap()
        {
            int one = InkSize("^XA^FO40,40^ABN,110^FDH^FS^XZ").w;
            int two = InkSize("^XA^FO40,40^ABN,110^FDHH^FS^XZ").w;
            int pitch = two - one;
            TestContext.WriteLine($"Font B pitch @110: {pitch}");
            Assert.AreEqual(9 * Scale, pitch, Scale, "advance pitch = W (7) + intercharacter gap (2)");
        }

        [TestMethod]
        public void FontB_IsCapsOnly()
        {
            // Font B has no lowercase: 'h' renders as the uppercase 'H' glyph (same ink box).
            (int uw, int uh) = InkSize("^XA^FO40,40^ABN,110^FDH^FS^XZ");
            (int lw, int lh) = InkSize("^XA^FO40,40^ABN,110^FDh^FS^XZ");
            TestContext.WriteLine($"Font B 'H'={uw}x{uh}  'h'={lw}x{lh}");
            Assert.AreEqual(uw, lw, "lowercase 'h' width = uppercase 'H' (caps-only)");
            Assert.AreEqual(uh, lh, "lowercase 'h' height = uppercase 'H' (caps-only)");
        }

        [TestMethod]
        public void FontC_MatchesMatrix_AndIsTwiceFontA()
        {
            (int aw, int ah) = InkSize("^XA^FO40,40^AAN,90^FDH^FS^XZ");
            (int cw, int ch) = InkSize("^XA^FO40,40^ACN,180^FDH^FS^XZ");
            TestContext.WriteLine($"Font A 'H'={aw}x{ah}  Font C 'H'={cw}x{ch}");

            // Matrix C = 18x10, baseline 14: 'H' inks 10 wide x 14 tall.
            Assert.AreEqual(10 * Scale, cw, Scale, "Font C 'H' width = W (10 dots)");
            Assert.AreEqual(14 * Scale, ch, Scale, "Font C 'H' cap height = baseline (14 dots)");

            // C is exactly 2x A (18x10 vs 9x5).
            Assert.AreEqual(2 * aw, cw, Scale, "Font C width = 2x Font A");
            Assert.AreEqual(2 * ah, ch, Scale, "Font C height = 2x Font A");
        }

        private static (int w, int h) InkSize(string zpl)
        {
            return StaRunner.Run(() =>
            {
                var storage = new PrinterStorage();
                var elements = new ZplAnalyzer(storage).Analyze(zpl).LabelInfos[0].ZplElements;
                byte[] png = new ZplRenderer(storage, new ZplRendererOptions { OpaqueBackground = true })
                    .DrawPng(elements, 60, 60, 8);   // 480 x 480 dot label
                return BlackInkSize(png);
            });
        }

        private static (int w, int h) BlackInkSize(byte[] png)
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
                    if (px[i] < 128 && px[i + 1] < 128 && px[i + 2] < 128)   // dark pixel = ink
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
