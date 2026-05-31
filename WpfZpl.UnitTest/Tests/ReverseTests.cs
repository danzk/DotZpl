using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WpfZpl.UnitTest
{
    [TestClass]
    public class ReverseTests
    {
        public TestContext TestContext { get; set; } = null!;

        public static IEnumerable<object[]> ReverseLabels()
        {
            return RenderHarness.EnumerateTestLabels()
                .Where(f => Path.GetFileName(f).StartsWith("FieldReversePrint"))
                .Select(f => new object[] { f });
        }

        [DataTestMethod]
        [DynamicData(nameof(ReverseLabels), DynamicDataSourceType.Method)]
        public void FieldReverse_MatchesSkia(string labelPath)
        {
            TestSupport.CompareLabelFile(labelPath, TestContext, minPixelSimilarity: 0.95, minSsim: 0.92);
        }
    }
}
