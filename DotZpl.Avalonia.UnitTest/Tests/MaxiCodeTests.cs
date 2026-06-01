using System.IO;

using Avalonia.Headless.XUnit;

using DotZpl.Avalonia.UnitTest.Support;

namespace DotZpl.Avalonia.UnitTest.Tests
{
    public class MaxiCodeTests
    {
        [AvaloniaFact]
        public void MaxiCode_MatchesSkia()
        {
            string path = Path.Combine(RenderHarness.LabelsRoot, "Test", "MaxiCode-102x152.zpl2");
            // Same rationale as the WPF gate: Skia/Avalonia aliased rasterisers disagree on the
            // hexagon vertices and the Bézier-approximated finder-ring circles by sub-pixels.
            // See MaxiCodeElementDrawer for the parity-gap explanation.
            TestSupport.CompareLabelFile(path, minPixelSimilarity: 0.90, minSsim: 0.85);
        }
    }
}
