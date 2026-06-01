using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotZpl.UnitTest
{
    [TestClass]
    public class ShapeTests
    {
        public TestContext TestContext { get; set; } = null!;

        public static IEnumerable<object[]> GraphicBoxLabels()
        {
            return RenderHarness.EnumerateTestLabels()
                .Where(f => Path.GetFileName(f).StartsWith("GraphicBox"))
                .Select(f => new object[] { f });
        }

        [DataTestMethod]
        [DynamicData(nameof(GraphicBoxLabels), DynamicDataSourceType.Method)]
        public void GraphicBox_MatchesSkia(string labelPath)
        {
            // Pure-geometry element: expect a close match (AA at edges accounts for the small gap).
            TestSupport.CompareLabelFile(labelPath, TestContext, minPixelSimilarity: 0.97, minSsim: 0.95);
        }

        public static IEnumerable<object[]> VectorShapeLabels()
        {
            return RenderHarness.EnumerateTestLabels()
                .Where(f =>
                {
                    string n = Path.GetFileName(f);
                    return n.StartsWith("GraphicCircle")
                        || n.StartsWith("GraphicEllipse")
                        || n.StartsWith("GraphicDiagonalLine");
                })
                .Select(f => new object[] { f });
        }

        [DataTestMethod]
        [DynamicData(nameof(VectorShapeLabels), DynamicDataSourceType.Method)]
        public void VectorShapes_MatchSkia(string labelPath)
        {
            // Curved/diagonal edges produce slightly more AA disagreement than axis-aligned boxes.
            TestSupport.CompareLabelFile(labelPath, TestContext, minPixelSimilarity: 0.96, minSsim: 0.93);
        }
    }
}
