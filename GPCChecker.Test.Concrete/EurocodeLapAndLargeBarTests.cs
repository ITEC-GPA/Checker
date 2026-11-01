using GPC.Checkers.Concrete.Cracking;
using GPC.Checkers.Concrete.Detailing;
using GPC.Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace ConcreteTests
{
    /// <summary>
    /// EN 1992-1-1 8.7.2(4) and 8.8 in the member detailing of the Eurocode family (Concrete 0.34.0.0, user decision of 1/11/2026: checks added,
    /// pending when the data are missing). Expected values from the text of the standard (UNI EN 1992-1-1:2015, 8.7.2(4) and 8.8(1)-(8)):
    /// lapped tension bars ≤ 100% in one layer, ≤ 50% in several layers; φlarge = 32 mm; large bars lapped only with a minimum dimension of
    /// 1.0 m (or σ ≤ 0.8 fyd, not known to the section). The checks come after every other check; NTC is unchanged.
    /// </summary>
    [TestClass]
    public class EurocodeLapAndLargeBarTests
    {
        private static CrackSectionGeometry Rectangle(double b, double h, double phi, int bottom)
        {
            var outline = new[] { new Point2d(-b / 2, -h / 2), new Point2d(b / 2, -h / 2), new Point2d(b / 2, h / 2), new Point2d(-b / 2, h / 2) };
            double area = Math.PI * phi * phi / 4, y = h / 2 - 60, step = (b - 120) / (bottom - 1);
            var bars = Enumerable.Range(0, bottom).Select(i => new CrackBar(-b / 2 + 60 + i * step, -y, phi, area))
                .Concat(new[] { new CrackBar(-b / 2 + 60, y, 16, Math.PI * 64), new CrackBar(b / 2 - 60, y, 16, Math.PI * 64) });
            return new CrackSectionGeometry(outline, null, bars, CrackBarLayout.Rows);
        }

        private static MemberDetailingResult Run(DetailingProfile profile, MemberDetailingKind kind, double b, double h, double phi, bool lapZone,
            LapArrangementData laps = null, int bottom = 4)
        {
            var g = Rectangle(b, h, phi, bottom);
            var options = new MemberDetailingOptions(null, false, false, false, false, laps);
            return MemberDetailingCalculator.Calculate(profile, new MemberDetailingInput(kind, g, b * h, 30, 2.9, 450, 391.3, b, b, b, 1e6, true, 10, 150, 2, 20,
                40, 25, 10, lapZone, true, true, 0, 0, options));
        }

        private static readonly string[] NewKeys = { "LapPercentage", "LargeBarAnchorage", "LargeBarCrackControl", "LargeBarLap" };

        [TestMethod]
        public void LapPercentageFollowsTheLayers()
        {
            foreach (var profile in new[] { DetailingProfile.EN1992p11, DetailingProfile.UniEN1992p11, DetailingProfile.DsEN1992p11 })
            {
                // Without the arrangement: pending, with the reason, after every other check.
                var pending = Run(profile, MemberDetailingKind.Beam, 300, 500, 20, true);
                var last = pending.Checks.Last();
                Assert.AreEqual("LapPercentage", last.Key, profile.ToString());
                Assert.IsNull(last.Passed); Assert.IsFalse(last.NotImplemented); StringAssert.Contains(last.Reference, "§8.7.2(4)");
                StringAssert.Contains(last.Explanation, "number of layers");
                // 100% in one layer satisfied; 60% in two layers beyond 50%; 50% in two layers satisfied.
                var one = Run(profile, MemberDetailingKind.Beam, 300, 500, 20, true, new LapArrangementData(100, 1)).Checks.Last();
                Assert.AreEqual(true, one.Passed); Assert.AreEqual(100.0, one.Actual); Assert.AreEqual(100.0, one.Limit); Assert.AreEqual("%", one.Unit);
                var two = Run(profile, MemberDetailingKind.Column, 300, 500, 20, true, new LapArrangementData(60, 2)).Checks.Last();
                Assert.AreEqual(false, two.Passed); Assert.AreEqual(50.0, two.Limit);
                Assert.AreEqual(true, Run(profile, MemberDetailingKind.Column, 300, 500, 20, true, new LapArrangementData(50, 2)).Checks.Last().Passed);
                // Outside the lap zone and with bars up to 32 mm nothing is added.
                Assert.IsFalse(Run(profile, MemberDetailingKind.Beam, 300, 500, 32, false).Checks.Any(c => NewKeys.Contains(c.Key)));
            }
            // NTC 2018: no new check, also at laps with large bars.
            Assert.IsFalse(Run(DetailingProfile.Ntc2018, MemberDetailingKind.Beam, 300, 500, 40, true).Checks.Any(c => NewKeys.Contains(c.Key)));
            // The arrangement is validated.
            Assert.ThrowsException<ArgumentException>(() => Run(DetailingProfile.EN1992p11, MemberDetailingKind.Beam, 300, 500, 20, true, new LapArrangementData(0, 1)));
            Assert.ThrowsException<ArgumentException>(() => Run(DetailingProfile.EN1992p11, MemberDetailingKind.Beam, 300, 500, 20, true, new LapArrangementData(100.5, 1)));
            Assert.ThrowsException<ArgumentException>(() => Run(DetailingProfile.EN1992p11, MemberDetailingKind.Beam, 300, 500, 20, true, new LapArrangementData(50, 0)));
        }

        [TestMethod]
        public void LargeBarsArePendingUnlessTheSectionAllowsTheLap()
        {
            // Ø36 > 32 mm in a 400 × 600 beam outside laps: anchorage and crack control pending, no lap check.
            var outside = Run(DetailingProfile.EN1992p11, MemberDetailingKind.Beam, 400, 600, 36, false);
            CollectionAssert.AreEqual(new[] { "LargeBarAnchorage", "LargeBarCrackControl" }, outside.Checks.Skip(outside.Checks.Count - 2).Select(c => c.Key).ToArray());
            Assert.IsTrue(outside.Checks.Skip(outside.Checks.Count - 2).All(c => c.Passed == null && !c.NotImplemented));
            StringAssert.Contains(outside.Checks[outside.Checks.Count - 2].Reference, "§8.8(3)");
            StringAssert.Contains(outside.Checks.Last().Explanation, "0.01 Act,ext");
            Assert.AreNotEqual(true, outside.Passed, "a pending rule never gives a satisfied outcome");
            // At laps in the same beam: minimum dimension 400 mm < 1.0 m, the lap stays pending (stress not known).
            var lap = Run(DetailingProfile.EN1992p11, MemberDetailingKind.Beam, 400, 600, 36, true, new LapArrangementData(50, 1));
            CollectionAssert.AreEqual(new[] { "LapPercentage", "LargeBarAnchorage", "LargeBarCrackControl", "LargeBarLap" },
                lap.Checks.Skip(lap.Checks.Count - 4).Select(c => c.Key).ToArray());
            Assert.IsNull(lap.Checks.Last().Passed); StringAssert.Contains(lap.Checks.Last().Explanation, "minimum dimension 400 mm");
            // 1200 × 1200 column with Ø40 at laps: minimum dimension 1200 mm ≥ 1000 mm, the lap of large bars is allowed.
            var column = Run(DetailingProfile.UniEN1992p11, MemberDetailingKind.Column, 1200, 1200, 40, true, new LapArrangementData(100, 1), 6);
            var allowed = column.Checks.Last();
            Assert.AreEqual("LargeBarLap", allowed.Key); Assert.AreEqual(true, allowed.Passed); Assert.AreEqual(1200.0, allowed.Actual); Assert.AreEqual(1000.0, allowed.Limit);
            // A 1200 × 1200 beam with a 300 mm web: the web governs the minimum dimension, pending.
            var g = Rectangle(1200, 1200, 40, 6);
            var web = MemberDetailingCalculator.Calculate(DetailingProfile.EN1992p11, new MemberDetailingInput(MemberDetailingKind.Beam, g, 1.44e6, 30, 2.9, 450, 391.3,
                1200, 1200, 300, 0, true, 10, 150, 2, 20, 40, 25, 10, true, true, true, 0, 0, MemberDetailingOptions.Default)).Checks.Last();
            Assert.AreEqual("LargeBarLap", web.Key); Assert.IsNull(web.Passed); StringAssert.Contains(web.Explanation, "minimum dimension 300 mm");
            // DS: the recommended φlarge is declared as such.
            StringAssert.Contains(Run(DetailingProfile.DsEN1992p11, MemberDetailingKind.Beam, 400, 600, 36, false).Checks.Last().Explanation, "DK NA not verified");
            // Ø32 is not larger than φlarge.
            Assert.IsFalse(Run(DetailingProfile.EN1992p11, MemberDetailingKind.Beam, 400, 600, 32, true).Checks.Any(c => c.Key.StartsWith("LargeBar")));
        }
    }
}
