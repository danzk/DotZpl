using System;
using System.IO;
using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;

using BinaryKits.Zpl.Viewer;

using WpfZpl.Rendering;

using Xunit;

namespace WpfZpl.Avalonia.SmokeTest
{
    /// <summary>
    /// Headless Avalonia render smoke tests. These run the real Avalonia/Skia runtime path that the
    /// (Windows-only) WPF unit tests cannot reach: avares:// embedded-font loading, GlyphRun.BuildGeometry,
    /// and RenderTargetBitmap.Save. The body runs on the Avalonia UI thread via [AvaloniaFact].
    /// </summary>
    public class SmokeTests
    {
        // 40 mm x 30 mm @ 8 dpmm = 320 x 240 dots.
        private const string Zpl =
            "^XA" +
            "^FO20,20^A0N,40^FDHello^FS" +     // font "0": system + condensed fallback
            "^FO20,80^ABN,44^FDWORLD^FS" +     // font "B": embedded avares pixel font
            "^FO20,150^BCN,60,N,N,N^FD12345^FS" + // Code 128: per-module geometry
            "^XZ";

        private static byte[] Render(string zpl)
        {
            var storage = new PrinterStorage();
            var elements = new ZplAnalyzer(storage).Analyze(zpl).LabelInfos[0].ZplElements;
            return new WpfZplElementDrawer(storage, new WpfDrawerOptions { OpaqueBackground = true })
                .DrawPng(elements, 40, 30, 8);
        }

        /// <summary>Decode the PNG via Avalonia and count near-black pixels (the inked content).</summary>
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
            // Font B is the embedded avares pixel font. Render it alone and require real ink.
            int dark = DarkPixels(Render("^XA^FO20,20^ABN,88^FDABC123^FS^XZ"));
            Assert.True(dark > 200, $"Embedded font B rendered {dark} dark pixels (expected ink).");
        }
    }
}
