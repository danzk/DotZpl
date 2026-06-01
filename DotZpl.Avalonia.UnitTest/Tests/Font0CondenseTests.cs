using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;

using BinaryKits.Zpl.Analyzer;

using DotZpl.Rendering;
using DotZpl.Text;

using Xunit;

namespace DotZpl.Avalonia.UnitTest.Tests
{
    /// <summary>
    /// The real Zebra font "0" is condensed. When it resolves to a non-condensed fallback (plain
    /// Arial), the renderer squeezes it horizontally (<see cref="ZplFontManager.Font0FallbackCondense"/>)
    /// to approximate font 0. Arial is always present on Windows hosts. Mirrors
    /// <c>DotZpl.UnitTest.Font0CondenseTests</c>.
    /// </summary>
    public class Font0CondenseTests
    {
        private const string Zpl = "^XA^FO10,10^A0N,40^FDCondensed WWWMMMiii 12345^FS^XZ";

        [AvaloniaFact]
        public void Font0_ArialFallback_IsCondensedHorizontally()
        {
            (int condensedW, int condensedH) = ArialFont0Ink(0.86);
            (int naturalW, int naturalH) = ArialFont0Ink(1.0);
            double ratio = (double)condensedW / naturalW;

            Assert.InRange(ratio, 0.83, 0.89);
            Assert.True(Math.Abs(naturalH - condensedH) <= 1, "condense is horizontal only — height unchanged");
        }

        [AvaloniaFact]
        public void HorizontalScale_OnlyAffectsFont0()
        {
            var mgr = new ZplFontManager { FontStack0 = new List<string> { "Arial" } };
            Assert.Equal(0.86, mgr.GetHorizontalScale("0"), precision: 9);
            Assert.Equal(1.0, mgr.GetHorizontalScale("A"), precision: 9);
            Assert.Equal(1.0, mgr.GetHorizontalScale("C"), precision: 9);

            mgr.Font0FallbackCondense = 1.0;
            Assert.Equal(1.0, mgr.GetHorizontalScale("0"), precision: 9);
        }

        private static (int w, int h) ArialFont0Ink(double condense)
        {
            var mgr = new ZplFontManager
            {
                FontStack0 = new List<string> { "Arial" },
                Font0FallbackCondense = condense,
            };
            var options = new ZplRendererOptions(mgr) { OpaqueBackground = true };
            var storage = new PrinterStorage();
            var elements = new ZplAnalyzer(storage).Analyze(Zpl).LabelInfos[0].ZplElements;
            byte[] png = new ZplRenderer(storage, options).DrawPng(elements, 100, 20, 8);
            return InkSize(png);
        }

        private static (int w, int h) InkSize(byte[] png)
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
    }
}
