using System;
using System.IO;
using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;

using BinaryKits.Zpl.Analyzer;

using DotZpl.Avalonia.UnitTest.Support;
using DotZpl.Rendering;

using Xunit;

namespace DotZpl.Avalonia.UnitTest.Tests
{
    /// <summary>
    /// Headless Avalonia render smoke tests. Validate the real Avalonia/Skia runtime path —
    /// avares:// embedded-font loading, GlyphRun.BuildGeometry, RenderTargetBitmap.Save —
    /// plus parity sanity for a trivial empty label. The body runs on the Avalonia UI thread
    /// via <c>[AvaloniaFact]</c>.
    /// </summary>
    public class SmokeTests
    {
        // 40 mm x 30 mm @ 8 dpmm = 320 x 240 dots.
        private const string Zpl =
            "^XA" +
            "^FO20,20^A0N,40^FDHello^FS" +
            "^FO20,80^ABN,44^FDWORLD^FS" +
            "^FO20,150^BCN,60,N,N,N^FD12345^FS" +
            "^XZ";

        private static byte[] Render(string zpl)
        {
            var storage = new PrinterStorage();
            var elements = new ZplAnalyzer(storage).Analyze(zpl).LabelInfos[0].ZplElements;
            return new ZplRenderer(storage, new ZplRendererOptions { OpaqueBackground = true })
                .DrawPng(elements, 40, 30, 8);
        }

        private static int DarkPixels(byte[] png)
        {
            using var bmp = new Bitmap(new MemoryStream(png));
            PixelSize size = bmp.PixelSize;
            int stride = size.Width * 4;
            int len = stride * size.Height;
            IntPtr ptr = Marshal.AllocHGlobal(len);
            try
            {
                bmp.CopyPixels(new PixelRect(0, 0, size.Width, size.Height), ptr, len, stride);
                var buf = new byte[len];
                Marshal.Copy(ptr, buf, 0, len);
                int dark = 0;
                for (int i = 0; i < len; i += 4)
                {
                    if (buf[i] < 128 && buf[i + 1] < 128 && buf[i + 2] < 128)
                    {
                        dark++;
                    }
                }

                return dark;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        [AvaloniaFact]
        public void RendersLabel_ToValidPng()
        {
            byte[] png = Render(Zpl);
            Assert.True(png.Length > 0, "PNG was empty");

            using var bmp = new Bitmap(new MemoryStream(png));
            Assert.Equal(320, bmp.PixelSize.Width);
            Assert.Equal(240, bmp.PixelSize.Height);
        }

        [AvaloniaFact]
        public void RendersContent_NotBlank()
        {
            int dark = DarkPixels(Render(Zpl));
            Assert.True(dark > 500, $"Expected inked content; only {dark} dark pixels.");
        }

        [AvaloniaFact]
        public void EmbeddedPixelFontB_RendersInk()
        {
            int dark = DarkPixels(Render("^XA^FO20,20^ABN,88^FDABC123^FS^XZ"));
            Assert.True(dark > 200, $"Embedded font B rendered {dark} dark pixels (expected ink).");
        }

        /// <summary>
        /// An empty label must render to an identical (all-white, opaque background) image on both
        /// backends — proves the RenderTargetBitmap + PNG encode + comparison plumbing works end-to-end.
        /// </summary>
        [AvaloniaFact]
        public void EmptyLabel_BothBackendsIdentical()
        {
            const string zpl = "^XA^XZ";

            RenderComparer.Result result = RenderHarness.RenderAndCompare(zpl, 50, 30, 8);

            Assert.Equal(400, result.Width);
            Assert.Equal(240, result.Height);
            Assert.True(result.PixelSimilarity > 0.999, $"empty label should be ~identical: {result}");
            Assert.True(result.Ssim > 0.999, $"empty label SSIM should be ~1: {result}");
        }
    }
}
