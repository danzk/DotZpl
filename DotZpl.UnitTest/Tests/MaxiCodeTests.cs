using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotZpl.UnitTest
{
    [TestClass]
    public class MaxiCodeTests
    {
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        public void MaxiCode_MatchesSkia()
        {
            string path = Path.Combine(RenderHarness.LabelsRoot, "Test", "MaxiCode-102x152.zpl2");
            // Skia and WPF/Avalonia aliased rasterisers disagree on the same hexagon vertices by a
            // sub-pixel each, and approximate finder-ring circles with different Béziers. Neither
            // engine matches Labelary exactly, so this gate is intentionally loose — see the class
            // comment on MaxiCodeElementDrawer for the parity-gap rationale.
            TestSupport.CompareLabelFile(path, TestContext, minPixelSimilarity: 0.90, minSsim: 0.85);
        }
    }
}
