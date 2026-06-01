using System.IO;

using Xunit;
using Xunit.Sdk;

namespace DotZpl.Avalonia.UnitTest.Support
{
    /// <summary>
    /// Shared comparison helper: renders a label file through both backends, writes a
    /// `skia | dotzpl | diff` PNG for visual inspection, then asserts the similarity thresholds.
    /// </summary>
    internal static class TestSupport
    {
        /// <summary>
        /// Directory where every test case's comparison PNG is written. Resolved to
        /// &lt;repo&gt;/TestOutput/RenderComparisons.Avalonia (separate from the WPF suite's
        /// output so the two don't overwrite each other when both run).
        /// </summary>
        public static string OutputDir { get; } = ResolveOutputDir();

        public static RenderComparer.Result CompareLabelFile(
            string labelPath,
            double minPixelSimilarity,
            double minSsim,
            int dpmm = 8)
        {
            string zpl = File.ReadAllText(labelPath);
            (double width, double height) = RenderHarness.SizeFromFileName(labelPath);

            RenderComparer.Result result = RenderHarness.RenderAndCompare(zpl, width, height, dpmm);

            string artifact = Path.Combine(OutputDir, Path.GetFileNameWithoutExtension(labelPath) + ".png");
            result.SaveDiff(artifact);

            bool pass = result.PixelSimilarity >= minPixelSimilarity && result.Ssim >= minSsim;
            if (!pass)
            {
                throw FailException.ForFailure(
                    $"{Path.GetFileName(labelPath)} below threshold: {result}. " +
                    $"Required pixel>={minPixelSimilarity}, ssim>={minSsim}. Image: {artifact}");
            }

            return result;
        }

        private static string ResolveOutputDir()
        {
            // Walk up to the repo root (identified by DotZpl.slnx). Put Avalonia diff PNGs in a
            // sibling folder so they don't trample the WPF suite's output.
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "DotZpl.slnx")))
            {
                dir = dir.Parent;
            }

            string root = dir?.FullName ?? System.AppContext.BaseDirectory;
            string output = Path.Combine(root, "TestOutput", "RenderComparisons.Avalonia");
            Directory.CreateDirectory(output);
            return output;
        }
    }
}
