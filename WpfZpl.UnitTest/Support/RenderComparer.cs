using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WpfZpl.UnitTest
{
    /// <summary>
    /// Decodes two PNGs (Skia reference vs WPF candidate) to identical BGRA32 buffers and scores
    /// their similarity with a normalized pixel-difference ratio and a windowed SSIM. Both metrics
    /// are pure byte math; the optional triage artifact is built via WIC (no STA requirement).
    /// </summary>
    internal static class RenderComparer
    {
        public sealed class Result
        {
            public int Width;
            public int Height;
            public double PixelSimilarity; // 1 - (differing pixels / total)
            public double Ssim;            // mean SSIM over 8x8 windows

            private byte[] _skiaGray = Array.Empty<byte>();
            private byte[] _wpfGray = Array.Empty<byte>();

            public override string ToString() =>
                $"PixelSimilarity={PixelSimilarity:F4}, SSIM={Ssim:F4} ({Width}x{Height})";

            /// <summary>Write a side-by-side `skia | wpf | abs-diff` grayscale PNG for triage.</summary>
            public void SaveDiff(string path)
            {
                int w = Width, h = Height;
                int panelW = w * 3;
                int stride = panelW * 4;
                var buf = new byte[h * stride];

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        byte s = _skiaGray[y * w + x];
                        byte t = _wpfGray[y * w + x];
                        byte d = (byte)Math.Abs(s - t);

                        WriteGray(buf, stride, x, y, s);             // panel 0: skia
                        WriteGray(buf, stride, x + w, y, t);          // panel 1: wpf
                        WriteDiff(buf, stride, x + 2 * w, y, d);      // panel 2: heatmap
                    }
                }

                BitmapSource bmp = BitmapSource.Create(panelW, h, 96, 96, PixelFormats.Bgra32, null, buf, stride);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmp));
                using FileStream fs = File.Create(path);
                encoder.Save(fs);
            }

            private static void WriteGray(byte[] buf, int stride, int x, int y, byte v)
            {
                int i = y * stride + x * 4;
                buf[i] = v; buf[i + 1] = v; buf[i + 2] = v; buf[i + 3] = 255;
            }

            private static void WriteDiff(byte[] buf, int stride, int x, int y, byte d)
            {
                // red heatmap on white background: stronger diff => more red
                int i = y * stride + x * 4;
                byte inv = (byte)(255 - d);
                buf[i] = inv;       // B
                buf[i + 1] = inv;   // G
                buf[i + 2] = 255;   // R
                buf[i + 3] = 255;
            }

            internal void SetGray(byte[] skiaGray, byte[] wpfGray)
            {
                _skiaGray = skiaGray;
                _wpfGray = wpfGray;
            }
        }

        public static Result Compare(byte[] skiaPng, byte[] wpfPng, int perPixelTolerance = 24)
        {
            (byte[] a, int aw, int ah) = DecodeBgra(skiaPng);
            (byte[] b, int bw, int bh) = DecodeBgra(wpfPng);

            if (aw != bw || ah != bh)
            {
                throw new InvalidOperationException(
                    $"Render dimensions differ: skia {aw}x{ah} vs wpf {bw}x{bh}.");
            }

            int w = aw, h = ah, total = w * h;
            var skiaGray = new byte[total];
            var wpfGray = new byte[total];
            long bad = 0;

            for (int p = 0; p < total; p++)
            {
                int i = p * 4;
                // Composite over white so alpha differences do not register as pixel diffs.
                byte sg = GrayOverWhite(a[i + 2], a[i + 1], a[i], a[i + 3]);
                byte tg = GrayOverWhite(b[i + 2], b[i + 1], b[i], b[i + 3]);
                skiaGray[p] = sg;
                wpfGray[p] = tg;

                if (Math.Abs(sg - tg) > perPixelTolerance)
                {
                    bad++;
                }
            }

            var result = new Result
            {
                Width = w,
                Height = h,
                PixelSimilarity = 1.0 - (double)bad / total,
                Ssim = ComputeWindowedSsim(skiaGray, wpfGray, w, h),
            };
            result.SetGray(skiaGray, wpfGray);
            return result;
        }

        private static byte GrayOverWhite(byte r, byte g, byte b, byte a)
        {
            // BGRA buffers from FormatConvertedBitmap(Bgra32) are straight (un-premultiplied) alpha.
            double alpha = a / 255.0;
            double rr = r * alpha + 255 * (1 - alpha);
            double gg = g * alpha + 255 * (1 - alpha);
            double bb = b * alpha + 255 * (1 - alpha);
            return (byte)Math.Round(0.299 * rr + 0.587 * gg + 0.114 * bb);
        }

        private static double ComputeWindowedSsim(byte[] x, byte[] y, int w, int h)
        {
            const double c1 = 6.5025;   // (0.01*255)^2
            const double c2 = 58.5225;  // (0.03*255)^2
            const int win = 8;

            double total = 0;
            int windows = 0;

            for (int by = 0; by + win <= h; by += win)
            {
                for (int bx = 0; bx + win <= w; bx += win)
                {
                    double sumX = 0, sumY = 0, sumXX = 0, sumYY = 0, sumXY = 0;
                    int n = win * win;

                    for (int j = 0; j < win; j++)
                    {
                        int row = (by + j) * w + bx;
                        for (int k = 0; k < win; k++)
                        {
                            double xv = x[row + k];
                            double yv = y[row + k];
                            sumX += xv; sumY += yv;
                            sumXX += xv * xv; sumYY += yv * yv; sumXY += xv * yv;
                        }
                    }

                    double meanX = sumX / n;
                    double meanY = sumY / n;
                    double varX = sumXX / n - meanX * meanX;
                    double varY = sumYY / n - meanY * meanY;
                    double covXY = sumXY / n - meanX * meanY;

                    double ssim = ((2 * meanX * meanY + c1) * (2 * covXY + c2)) /
                                  ((meanX * meanX + meanY * meanY + c1) * (varX + varY + c2));
                    total += ssim;
                    windows++;
                }
            }

            return windows == 0 ? 1.0 : total / windows;
        }

        private static (byte[] pixels, int width, int height) DecodeBgra(byte[] png)
        {
            using var ms = new MemoryStream(png);
            var decoder = new PngBitmapDecoder(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            BitmapSource frame = decoder.Frames[0];
            var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);

            int w = converted.PixelWidth;
            int h = converted.PixelHeight;
            int stride = w * 4;
            var pixels = new byte[h * stride];
            converted.CopyPixels(pixels, stride, 0);
            return (pixels, w, h);
        }
    }
}
