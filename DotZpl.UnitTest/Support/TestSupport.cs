using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotZpl.UnitTest
{
    /// <summary>
    /// Shared comparison helper: renders a label file through both backends, writes a
    /// `skia | wpf | diff` PNG for visual inspection, then asserts the similarity thresholds.
    /// </summary>
    internal static class TestSupport
    {
        /// <summary>
        /// Directory where every test case's comparison PNG is written. Resolved to
        /// &lt;repo&gt;/TestOutput/RenderComparisons so the images are easy to find and browse.
        /// </summary>
        public static string OutputDir { get; } = ResolveOutputDir();

        public static RenderComparer.Result CompareLabelFile(
            string labelPath,
            TestContext testContext,
            double minPixelSimilarity,
            double minSsim,
            int dpmm = 8)
        {
            string zpl = File.ReadAllText(labelPath);
            (double width, double height) = RenderHarness.SizeFromFileName(labelPath);

            RenderComparer.Result result = RenderHarness.RenderAndCompare(zpl, width, height, dpmm);
            testContext.WriteLine($"{Path.GetFileName(labelPath)}: {result}");

            // Always emit a side-by-side comparison image so results can be checked visually.
            string artifact = Path.Combine(OutputDir, Path.GetFileNameWithoutExtension(labelPath) + ".png");
            result.SaveDiff(artifact);
            testContext.AddResultFile(artifact);
            testContext.WriteLine($"  comparison image: {artifact}");

            bool pass = result.PixelSimilarity >= minPixelSimilarity && result.Ssim >= minSsim;
            if (!pass)
            {
                Assert.Fail($"{Path.GetFileName(labelPath)} below threshold: {result}. " +
                            $"Required pixel>={minPixelSimilarity}, ssim>={minSsim}. Image: {artifact}");
            }

            return result;
        }

        private static string ResolveOutputDir()
        {
            // Walk up from the test assembly location to the repo root (identified by DotZpl.slnx).
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "DotZpl.slnx")))
            {
                dir = dir.Parent;
            }

            string root = dir?.FullName ?? System.AppContext.BaseDirectory;
            string output = Path.Combine(root, "TestOutput", "RenderComparisons");
            Directory.CreateDirectory(output);
            return output;
        }
    }
}
