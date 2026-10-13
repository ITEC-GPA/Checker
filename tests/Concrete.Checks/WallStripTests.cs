using System;
using System.Globalization;
using System.IO;
using System.Linq;
using GPC.Checkers.Concrete.Walls;
using GPC.Model.Checking.Contracts;
using GPC.Model.Materials;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConcreteTests
{
    [TestClass]
    public class WallStripTests
    {
        [TestMethod]
        public void FrozenWallLongitudinalChecksRemainEquivalent()
        {
            var rows = File.ReadAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "wall-detailing-f4.csv")).Skip(1).ToArray();
            Assert.AreEqual(576, rows.Length);
            foreach (string row in rows)
            {
                var c = row.Split(','); double D(int i) => double.Parse(c[i], CultureInfo.InvariantCulture);
                var strip = new WallStrip((WallMember)Enum.Parse(typeof(WallMember), c[1]), 1000, D(2), D(3), (int)D(4), D(5), (int)D(6), D(7));
                var material = new ConcreteMaterialEN1992("capture", D(8), ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
                var r = WallStripDetailing.Evaluate(strip, material.Fctm, D(9));
                void Same(double actual, int column) => Assert.AreEqual(D(column), actual, 1e-12 * Math.Max(1, Math.Abs(D(column))), c[0] + " column " + column);
                Same(r.MinimumPerFace.Demand, 10); Same(r.MinimumPerFace.Capacity.Value, 11);
                Same(r.MaximumTotal.Demand, 12); Same(r.MaximumTotal.Capacity.Value, 13);
                Assert.AreEqual(D(10) <= D(11) ? EngineeringOutcome.Satisfied : EngineeringOutcome.NotSatisfied, r.MinimumPerFace.Outcome);
                Assert.AreEqual(D(12) <= D(13) ? EngineeringOutcome.Satisfied : EngineeringOutcome.NotSatisfied, r.MaximumTotal.Outcome);
            }
        }

        [TestMethod]
        public void IndependentAsymmetricStripChecksBothFacesAndMomentSigns()
        {
            var strip = new WallStrip(WallMember.Stem, 1000, 500, 40, 4, 16, 5, 20);
            var result = WallStripDetailing.Evaluate(strip, 2.9, 450);
            Assert.AreEqual(757.3511111111111, result.MinimumPerFace.Demand, 1e-10);
            Assert.AreEqual(804.247719318987, result.MinimumPerFace.Capacity.Value, 1e-10);
            Assert.AreEqual(20000, result.MaximumTotal.Capacity.Value, 0);
            Assert.AreEqual(WallFace.Bottom, strip.TensionFace(0));
            Assert.AreEqual(WallFace.Bottom, strip.TensionFace(1));
            Assert.AreEqual(WallFace.Top, strip.TensionFace(-1));
            Assert.AreEqual(450, strip.EffectiveDepth(WallFace.Bottom), 0);
            Assert.AreEqual(452, strip.EffectiveDepth(WallFace.Top), 0);
            Assert.AreEqual(1570.7963267948967, strip.TensionArea(1), 1e-10);
            Assert.AreEqual(804.247719318987, strip.TensionArea(-1), 1e-10);
            Assert.AreEqual(904d / 3, strip.MaximumBarSpacing, 0);
        }

        [TestMethod]
        public void StemMinimumIsPerFaceWhileFoundationUsesBendingMinimum()
        {
            WallReinforcementChecks Check(WallMember member) => WallStripDetailing.Evaluate(new WallStrip(member, 1000, 100, 30, 2, 12, 2, 12), 1, 450);
            Assert.AreEqual(100, Check(WallMember.Stem).MinimumPerFace.Demand, 1e-12);
            Assert.AreEqual(83.2, Check(WallMember.Toe).MinimumPerFace.Demand, 1e-12);
            Assert.AreEqual(83.2, Check(WallMember.Heel).MinimumPerFace.Demand, 1e-12);
        }

        [TestMethod]
        public void InvalidGeometryAndNonfiniteDataAreRejected()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new WallStrip((WallMember)99, 1000, 500, 40, 4, 16, 4, 16));
            foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, 0, -1 })
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => new WallStrip(WallMember.Stem, invalid, 500, 40, 4, 16, 4, 16));
            Assert.ThrowsException<ArgumentException>(() => new WallStrip(WallMember.Stem, 1000, 100, 45, 4, 16, 4, 16));
            Assert.ThrowsException<ArgumentException>(() => new WallStrip(WallMember.Stem, 100, 500, 40, 4, 16, 4, 16));
            var strip = new WallStrip(WallMember.Stem, 1000, 500, 40, 4, 16, 4, 16);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => strip.TensionFace(double.NaN));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => strip.EffectiveDepth((WallFace)99));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => WallStripDetailing.Evaluate(strip, 0, 450));
            Assert.IsTrue(typeof(WallStrip).GetProperties().All(p => !p.CanWrite));
        }
    }
}
