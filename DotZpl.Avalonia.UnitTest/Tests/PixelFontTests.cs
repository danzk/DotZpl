using System;
using System.IO;
using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;

using BinaryKits.Zpl.Analyzer;

using DotZpl.Rendering;

using Xunit;

namespace DotZpl.Avalonia.UnitTest.Tests
{
    /// <summary>
    /// Verifies the embedded pixel fonts render at their Zebra font matrices (Tables 29/31): cell
    /// height = matrix H, character width = matrix W, advance pitch = W + intercharacter gap, and
    /// Font C = 2× Font A. Mirrors <c>DotZpl.UnitTest.PixelFontTests</c>; uses Avalonia's Bitmap
    /// for the pixel inspection.
    /// </summary>
    public class PixelFontTests
    {
        private const int Scale = 10;   // 10× the base matrix so integer pixels are easy to measure

        [AvaloniaFact]
        public void FontA_Caps_MatchMatrix()
        {
            // Matrix A = 9x5, baseline 7. An uppercase 'H' inks 5 wide x 7 tall (cap = baseline).
            (int w, int h) = InkSize("^XA^FO40,40^AAN,90^FDH^FS^XZ");
            AssertCloseTo(5 * Scale, w, Scale, "Font A 'H' width = W (5 dots)");
            AssertCloseTo(7 * Scale, h, Scale, "Font A 'H' cap height = baseline (7 dots)");
        }

        [AvaloniaFact]
        public void FontA_FullCell_MatchesMatrix()
        {
            // 'Hg' spans cap top (baseline 7) to descender bottom (9 - 7 = 2) = the full 9-dot cell.
            (int _, int h) = InkSize("^XA^FO40,40^AAN,90^FDHg^FS^XZ");
            AssertCloseTo(9 * Scale, h, Scale, "Font A full cell (cap + descender) = H (9 dots)");
        }

        [AvaloniaFact]
        public void FontA_Pitch_IsWidthPlusGap()
        {
            int one = InkSize("^XA^FO40,40^AAN,90^FDH^FS^XZ").w;
            int two = InkSize("^XA^FO40,40^AAN,90^FDHH^FS^XZ").w;
            int pitch = two - one;
            AssertCloseTo(6 * Scale, pitch, Scale, "advance pitch = W (5) + intercharacter gap (1)");
        }

        [AvaloniaFact]
        public void FontB_Caps_MatchMatrix()
        {
            // Matrix B = 11x7, baseline 11 (caps-only): an 'H' inks the full 7×11 cell.
            (int w, int h) = InkSize("^XA^FO40,40^ABN,110^FDH^FS^XZ");
            AssertCloseTo(7 * Scale, w, Scale, "Font B 'H' width = W (7 dots)");
            AssertCloseTo(11 * Scale, h, Scale, "Font B 'H' cap height = baseline = cell (11 dots)");
        }

        [AvaloniaFact]
        public void FontB_Pitch_IsWidthPlusGap()
        {
            int one = InkSize("^XA^FO40,40^ABN,110^FDH^FS^XZ").w;
            int two = InkSize("^XA^FO40,40^ABN,110^FDHH^FS^XZ").w;
            int pitch = two - one;
            AssertCloseTo(9 * Scale, pitch, Scale, "advance pitch = W (7) + intercharacter gap (2)");
        }

        [AvaloniaFact]
        public void FontB_IsCapsOnly()
        {
            // Font B has no lowercase: 'h' renders as the uppercase 'H' glyph (same ink box).
            (int uw, int uh) = InkSize("^XA^FO40,40^ABN,110^FDH^FS^XZ");
            (int lw, int lh) = InkSize("^XA^FO40,40^ABN,110^FDh^FS^XZ");
            Assert.Equal(uw, lw);
            Assert.Equal(uh, lh);
        }

        [AvaloniaFact]
        public void FontC_MatchesMatrix_AndIsTwiceFontA()
        {
            (int aw, int ah) = InkSize("^XA^FO40,40^AAN,90^FDH^FS^XZ");
            (int cw, int ch) = InkSize("^XA^FO40,40^ACN,180^FDH^FS^XZ");

            // Matrix C = 18x10, baseline 14: 'H' inks 10 wide × 14 tall.
            AssertCloseTo(10 * Scale, cw, Scale, "Font C 'H' width = W (10 dots)");
            AssertCloseTo(14 * Scale, ch, Scale, "Font C 'H' cap height = baseline (14 dots)");

            // C is exactly 2× A (18x10 vs 9x5).
            AssertCloseTo(2 * aw, cw, Scale, "Font C width = 2× Font A");
            AssertCloseTo(2 * ah, ch, Scale, "Font C height = 2× Font A");
        }

        private static (int w, int h) InkSize(string zpl)
        {
            var storage = new PrinterStorage();
            var elements = new ZplAnalyzer(storage).Analyze(zpl).LabelInfos[0].ZplElements;
            byte[] png = new ZplRenderer(storage, new ZplRendererOptions { OpaqueBackground = true })
                .DrawPng(elements, 60, 60, 8);
            return BlackInkSize(png);
        }

        private static (int w, int h) BlackInkSize(byte[] png)
        {
            using var bmp = new Bitmap(new MemoryStream(png));
            int w = bmp.PixelSize.Width;
            int h = bmp.PixelSize.Height;
            int stride = w * 4;
            int len = stride * h;
            IntPtr ptr = Marshal.AllocHGlobal(len);
            try
            {
                bmp.CopyPixels(new PixelRect(0, 0, w, h), ptr, len, stride);
                var px = new byte[len];
                Marshal.Copy(ptr, px, 0, len);

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
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        private static void AssertCloseTo(int expected, int actual, int tolerance, string because)
            => Assert.True(Math.Abs(expected - actual) <= tolerance,
                $"{because}: expected {expected} ± {tolerance}, got {actual}");
    }
}
