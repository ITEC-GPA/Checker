using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Concrete.Cracking;
using GPC.Geometry;
using GPC.Model.Sections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConcreteTests
{
    [TestClass]
    public class CircularSpacingTests
    {
        private static IEnumerable<CrackBar> Ring(double radius, int count, double rotation = 0, double x = 0, double y = 0)
        {
            for (int i = 0; i < count; i++)
            {
                double angle = rotation + 2 * Math.PI * i / count;
                yield return new CrackBar(x + radius * Math.Cos(angle), y + radius * Math.Sin(angle), 16, 64 * Math.PI);
            }
        }

        private static CrackSectionGeometry Geometry(IEnumerable<CrackBar> bars, bool concentric = true, double x = 0, double y = 0)
        {
            var circle = new ArcCurve3d(new Point3d(x, y, 0), Vector3d.ZAxis, Vector3d.XAxis, 200, 2 * Math.PI);
            return CrackSectionGeometry.FromCurves(new SectionCurveOutline(circle), bars, .1, concentricRings: concentric);
        }

        private static double? All(CrackSectionGeometry geometry) => geometry.MaximumSpacing(Enumerable.Range(0, geometry.Bars.Count).ToArray());

        public static IEnumerable<object[]> BoundaryCases()
        {
            foreach (double radius in new[] { 144d, 144.000005, 144.000005 - 1e-11, 144.000005 + 1e-11, 110.000005 })
                foreach (double rotation in new[] { 0d, .123, Math.PI / 8 })
                    foreach (bool reverse in new[] { false, true }) yield return new object[] { radius, rotation, reverse };
        }

        [DataTestMethod, DynamicData(nameof(BoundaryCases), DynamicDataSourceType.Method)]
        public void OneRingHasTheAnalyticArcDespiteRoundingBoundary(double radius, double rotation, bool reverse)
        {
            var bars = Ring(radius, 8, rotation).ToArray();
            if (reverse) Array.Reverse(bars);
            // Independent geometry: equally spaced points divide the circumference into eight equal arcs.
            Assert.AreEqual(2 * Math.PI * radius / 8, All(Geometry(bars, false)).Value, 1e-9);
        }

        [TestMethod]
        public void Api02TwoCrownsMatchTheIndependentExpectedValue()
        {
            // D400, cover39.999995, link8, 8Ø16 + 6Ø12, clear radial gap20 (mm).
            var bars = Ring(144.000005, 8).Concat(Ring(110.000005, 6)).ToArray();
            Assert.AreEqual(115.19173586761349, All(Geometry(bars)).Value, 1e-9);
        }

        [DataTestMethod]
        [DataRow(0d, 0d)]
        [DataRow(1234.5, -9876.5)]
        [DataRow(1000000d, -1000000d)]
        public void TranslationRotationAndPermutationDoNotChangeRingSpacing(double x, double y)
        {
            var bars = Ring(144.000005, 8, .43, x, y).Concat(Ring(110.000005, 6, .17, x, y)).Reverse();
            Assert.AreEqual(115.19173586761349, All(Geometry(bars, true, x, y)).Value, 1e-8);
        }

        [TestMethod]
        public void SelectionUsesOnlyAdjacentBarsOfTheOriginalRing()
        {
            var geometry = Geometry(Ring(144.000005, 8));
            double expected = 2 * Math.PI * 144.000005 / 8;
            Assert.AreEqual(expected, geometry.MaximumSpacing(new[] { 0, 1 }).Value, 1e-9);
            Assert.AreEqual(expected, geometry.MaximumSpacing(new[] { 0, 7 }).Value, 1e-9);
            Assert.IsNull(geometry.MaximumSpacing(new[] { 0, 2 }));
            Assert.IsNull(geometry.MaximumSpacing(new[] { 0, 0 }));
        }

        [TestMethod]
        public void IsolatedBarsOnDifferentRingsNeverCreateACircumference()
        {
            Assert.IsNull(All(Geometry(new[] { Ring(144, 1).Single(), Ring(110, 1, .2).Single() })));
            var geometry = Geometry(Ring(144, 8).Concat(Ring(110, 1)));
            Assert.AreEqual(2 * Math.PI * 144 / 8, All(geometry).Value, 1e-9);
            Assert.IsNull(geometry.MaximumSpacing(new[] { 0, 8 }));
        }

        [TestMethod]
        public void MultipleRingsRequireAnExplicitConcentricLayout()
        {
            Assert.IsNull(All(Geometry(Ring(144, 8).Concat(Ring(110, 6)), false)));
        }

        [TestMethod]
        public void CloseButDistinctRingsAreNotJoinedByAToleranceChain()
        {
            // The first two radii fit within 1e-5 mm; the third must remain separate even though
            // it is close to the second. Group diameter is bounded against the minimum radius.
            var bars = new[] { Ring(100, 1).Single(), Ring(100 + .75e-5, 1, Math.PI).Single(), Ring(100 + 1.5e-5, 1, .2).Single() };
            Assert.AreEqual(Math.PI * (100 + .375e-5), All(Geometry(bars)).Value, 1e-9);
            var separate = Ring(144, 8).Concat(Ring(144 + 2e-5, 6));
            Assert.AreEqual(2 * Math.PI * (144 + 2e-5) / 6, All(Geometry(separate)).Value, 1e-9);
        }
    }
}
