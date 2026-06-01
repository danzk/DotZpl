using System;
using System.IO;
using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;

using DotZpl.Avalonia.UnitTest.Support;

using Xunit;

namespace DotZpl.Avalonia.UnitTest.Tests
{
    /// <summary>
    /// Regression guard: a large glyph drawn over a MaxiCode. The glyph outline and the MaxiCode
    /// hexagons wind opposite ways, so accumulating the label with a fill-rule GeometryGroup would
    /// cancel them in the overlap into white holes (an inverse draw) instead of unioning them. The
    /// orchestrator must use a true boolean union. A union can never have fewer ink pixels than the
    /// Skia reference (which draws the glyph solid), so we assert the candidate keeps its ink.
    /// </summary>
    public class MaxiCodeOverlapTests
    {
        private const string Zpl =
            "^XA" +
            "^FO20,20^BD2^FH^FD002840100450000_5B)>_1E01_1D961Z00136071_1DUPSN_1D123X56_1D028_1D_1D001/001_1D011_1DN_1D_1DNEW YORK_1DNY_1E_04^FS" +
            "^FT55,160^A0N,175,220^FVH^FS" +
            "^XZ";

        [AvaloniaFact]
        public void OverlappingGlyph_DoesNotInverseDraw()
        {
            (byte[] skiaPng, byte[] dotPng) = RenderHarness.RenderBoth(Zpl, 30, 30, 8);
            int skiaDark = DarkPixels(skiaPng);
            int dotDark = DarkPixels(dotPng);
            Assert.True(dotDark >= skiaDark * 0.95,
                $"Candidate lost ink vs Skia ({dotDark} vs {skiaDark}) — inverse-draw cancellation in the H/MaxiCode overlap.");
        }

        private static int DarkPixels(byte[] png)
        {
            using var bmp = new Bitmap(new MemoryStream(png));
            PixelSize size = bmp.PixelSize;
            int stride = size.Width * 4, len = stride * size.Height;
            IntPtr ptr = Marshal.AllocHGlobal(len);
            try
            {
                bmp.CopyPixels(new PixelRect(0, 0, size.Width, size.Height), ptr, len, stride);
                var buf = new byte[len];
                Marshal.Copy(ptr, buf, 0, len);
                int dark = 0;
                for (int i = 0; i < len; i += 4)
                    if (buf[i] < 128 && buf[i + 1] < 128 && buf[i + 2] < 128) dark++;
                return dark;
            }
            finally { Marshal.FreeHGlobal(ptr); }
        }
    }
}
