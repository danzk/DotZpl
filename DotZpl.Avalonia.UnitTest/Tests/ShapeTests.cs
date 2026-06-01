using System.Collections.Generic;
using System.IO;
using System.Linq;

using Avalonia.Headless.XUnit;

using DotZpl.Avalonia.UnitTest.Support;

using Xunit;

namespace DotZpl.Avalonia.UnitTest.Tests
{
    /// <summary>
    /// Per-element vector-shape parity against the Skia reference. Mirrors the MSTest suite in
    /// <c>DotZpl.UnitTest.ShapeTests</c> — same labels, same thresholds.
    /// </summary>
    public class ShapeTests
    {
        public static IEnumerable<object[]> GraphicBoxLabels()
            => RenderHarness.EnumerateTestLabels()
                .Where(f => Path.GetFileName(f).StartsWith("GraphicBox"))
                .Select(f => new object[] { f });

        [AvaloniaTheory]
        [MemberData(nameof(GraphicBoxLabels))]
        public void GraphicBox_MatchesSkia(string labelPath)
        {
            // Pure-geometry element: expect a close match (AA at edges accounts for the small gap).
            TestSupport.CompareLabelFile(labelPath, minPixelSimilarity: 0.97, minSsim: 0.95);
        }

        public static IEnumerable<object[]> VectorShapeLabels()
            => RenderHarness.EnumerateTestLabels()
                .Where(f =>
                {
                    string n = Path.GetFileName(f);
                    return n.StartsWith("GraphicCircle")
                        || n.StartsWith("GraphicEllipse")
                        || n.StartsWith("GraphicDiagonalLine");
                })
                .Select(f => new object[] { f });

        [AvaloniaTheory]
        [MemberData(nameof(VectorShapeLabels))]
        public void VectorShapes_MatchSkia(string labelPath)
        {
            // Curved/diagonal edges produce slightly more AA disagreement than axis-aligned boxes.
            TestSupport.CompareLabelFile(labelPath, minPixelSimilarity: 0.96, minSsim: 0.93);
        }
    }
}
