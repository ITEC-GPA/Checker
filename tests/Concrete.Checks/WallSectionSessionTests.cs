using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.Serviceability;
using GPC.Checkers.Concrete.Walls;
using GPC.Geometry;
using GPC.Model.Checking.Contracts;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Materials;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConcreteTests
{
    [TestClass]
    public class WallSectionSessionTests : ConcreteTestBase
    {
        WallSectionSession Session(double creep = 2)
        {
            var strip = new WallStrip(WallMember.Stem, 1000, 400, 40, 6, 16, 6, 16);
            var concrete = new ConcreteMaterialEN1992("C30", 30, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
            var steel = new SteelMaterial("B450", 200000, 450, 450, .075, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Rebar);
            var section = new ReinforcedConcreteSection(GetRectangularShape(1000, 400), concrete);
            var bar = new RebarSectionCircular(16, steel);
            section.AddRebars(new[] { 48d, 352d }.SelectMany(y => Enumerable.Range(0, 6).Select(i =>
                new ReinforcedConcreteRebar(bar, new Point3d(48 + 904d * i / 5, y, 0)))).ToArray());
            return new WallSectionSession(strip, section, new StandardNTC2018Concrete(), creep, "XC2");
        }

        [TestMethod]
        public void MomentChecksBothSignsWithoutTreatingZeroDemandAsZeroCapacity()
        {
            var session = Session();
            var positive = session.CheckMoment(new WallSectionActions(-100000, 0, 1e7));
            var negative = session.CheckMoment(new WallSectionActions(-100000, 0, -1e7));
            var zero = session.CheckMoment(new WallSectionActions(-100000, 0, 0));
            Assert.AreEqual(ExecutionStatus.Completed, positive.Execution);
            Assert.AreEqual(EngineeringOutcome.Satisfied, positive.Outcome);
            // The iterative native solver is not exactly antisymmetric; compare each sign with the
            // frozen pre-port solver in the corpus, rather than invent a symmetry tolerance here.
            Assert.AreEqual(ExecutionStatus.Completed, negative.Execution);
            Assert.AreEqual(EngineeringOutcome.Satisfied, negative.Outcome);
            Assert.IsTrue(negative.Capacity > 0);
            Assert.IsTrue(zero.Capacity > 0); Assert.AreEqual(0, zero.Ratio.Value, 0);
            Assert.AreEqual(positive.Capacity, zero.Capacity);
            Assert.AreEqual(1e7 / positive.Capacity.Value, positive.Ratio.Value, 0);
        }

        [TestMethod]
        public void OutsideDomainRetainsReadyInputAndNeverPasses()
        {
            var result = Session().CheckMoment(new WallSectionActions(1e12, 0, 1e6));
            Assert.AreEqual(ExecutionStatus.Error, result.Execution);
            Assert.AreEqual(DataStatus.Ready, result.Data);
            Assert.AreEqual(EngineeringOutcome.NotEvaluated, result.Outcome);
            Assert.IsNull(result.Capacity); Assert.IsNull(result.Ratio);
        }

        [DataTestMethod]
        [DataRow(ServiceabilityCombination.Characteristic, 2)]
        [DataRow(ServiceabilityCombination.Frequent, 1)]
        [DataRow(ServiceabilityCombination.QuasiPermanent, 2)]
        public void ServiceabilityChecksOnlyTheApplicableLimits(ServiceabilityCombination combination, int count)
        {
            var result = Session().CheckServiceability(new WallSectionActions(-20000, 5000, 2e7), combination);
            Assert.AreEqual(count, result.Checks.Count);
            Assert.IsTrue(result.Curvature > 0);
            Assert.IsTrue(result.Checks.All(c => c.Execution == ExecutionStatus.Completed && c.Capacity.HasValue));
            Assert.AreEqual(combination == ServiceabilityCombination.Characteristic, result.Checks.Any(c => c.Method == "Wall.SteelStress"));
            Assert.AreEqual(combination != ServiceabilityCombination.Frequent, result.Checks.Any(c => c.Method == "Wall.ConcreteStress"));
            if (combination != ServiceabilityCombination.Characteristic) Assert.IsNotNull(result.CrackResponse);
        }

        [TestMethod]
        public void CurvatureSignAndCreepFollowTheExistingSectionEngine()
        {
            var session = Session();
            var a = session.CheckServiceability(new WallSectionActions(-20000, 0, 2e7), ServiceabilityCombination.QuasiPermanent);
            var b = session.CheckServiceability(new WallSectionActions(-20000, 0, -2e7), ServiceabilityCombination.QuasiPermanent);
            Assert.IsTrue(a.Curvature > 0 && b.Curvature < 0);
            Assert.AreEqual(2, a.Response.PsiRebar, 0);
            var fresh = Session(0).CheckServiceability(new WallSectionActions(-20000, 0, 2e7), ServiceabilityCombination.QuasiPermanent);
            Assert.AreEqual(0, fresh.Response.PsiRebar, 0);
            Assert.IsTrue(a.Curvature > fresh.Curvature);
        }

        [TestMethod]
        public void CancelledCallsDoNotReturnPartialVerificationAndSessionRecovers()
        {
            var session = Session(); var action = new WallSectionActions(-100000, 0, 1e7); var cancelled = new CancellationToken(true);
            Assert.ThrowsException<OperationCanceledException>(() => session.CheckMoment(action, cancelled));
            Assert.ThrowsException<OperationCanceledException>(() => session.CheckServiceability(action, ServiceabilityCombination.Frequent, cancelled));
            Assert.AreEqual(ExecutionStatus.Completed, session.CheckMoment(action).Execution);
        }

        [TestMethod]
        public void ConcurrentCallsUseOneSessionWithoutSharingMutableResponses()
        {
            var session = Session(); var result = new WallServiceabilityResult[8];
            Parallel.For(0, result.Length, i => result[i] = session.CheckServiceability(new WallSectionActions(-20000, 0, 2e7), ServiceabilityCombination.QuasiPermanent));
            Assert.IsTrue(result.All(r => r.Curvature == result[0].Curvature));
            Assert.IsFalse(ReferenceEquals(result[0].Response, result[1].Response));
            Assert.IsFalse(ReferenceEquals(result[0].Checks, result[1].Checks));
        }

        [TestMethod]
        public void InvalidActionsAndServiceabilityStateAreRejected()
        {
            Assert.ThrowsException<ArgumentException>(() => new WallSectionActions(double.NaN, 0, 0));
            Assert.ThrowsException<ArgumentException>(() => new WallSectionActions(0, double.PositiveInfinity, 0));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => Session(-1));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => Session().CheckServiceability(new WallSectionActions(0, 0, 0), (ServiceabilityCombination)99));
        }

        [TestMethod]
        public void EntirelyCompressedSectionDoesNotInventAnAdditionalUncrackedProof()
        {
            var result = Session().CheckServiceability(new WallSectionActions(-100000, 0, 7e6), ServiceabilityCombination.QuasiPermanent);
            Assert.IsFalse(result.ProvenUncracked);
            Assert.IsNull(result.UncrackedMaximumStress);
            Assert.AreEqual(GPC.Checkers.Concrete.Cracking.CrackReason.EntirelyCompressed, result.CrackResponse.Reason);
            var crack = result.Checks.Single(c => c.Method == "Wall.Cracking");
            Assert.AreEqual(0, crack.Demand, 0);
            Assert.AreEqual(EngineeringOutcome.Satisfied, crack.Outcome);
        }

        /// <summary>
        /// Decision E1-D4 of the user (15/10/2026): the wall crack check follows the sections. N = −1000 kN, M = 75 kNm on the 1000 × 400 strip: e = 75 mm,
        /// just beyond h/6, so a shallow tensile zone (no-tension rectangle by hand: x = 3 (200 − 75) = 375 mm, h − x = 25 mm), shallower than the bars at
        /// 48 mm: neutral axis within the cover of the face bars, wk = 0, satisfied, with the projected cover a = c + Ø/2 = 40 + 8 = 48 mm
        /// (gradient normal to the face). The proof σt,el ≤ fctk,0.05 is withdrawn: ProvenUncracked is always false. A tensile zone without reinforcement
        /// cannot be built with the strip (bars at the cover of both faces); its check is NotSatisfied without a width.
        /// </summary>
        [TestMethod]
        public void CrackCheckFollowsTheSectionsWithoutTheTensileStrengthProof()
        {
            var result = Session().CheckServiceability(new WallSectionActions(-1000000, 0, 7.5e7), ServiceabilityCombination.QuasiPermanent);
            var crack = result.CrackResponse;
            Assert.AreEqual(GPC.Checkers.Concrete.Cracking.CrackOutcome.Evaluated, crack.Outcome, crack.Status);
            StringAssert.StartsWith(crack.Status, "Neutral axis within the cover");
            Assert.AreEqual(0, crack.Width.Value);
            double depth = crack.Details.Single(d => d.Symbol == "h − x").Value;
            Assert.IsTrue(depth > 0 && depth < 48, "tensile zone above the bars: " + depth);
            Assert.AreEqual(48, crack.Details.Single(d => d.Symbol == "a,c").Value, 1e-3); // 48 (|qx| + |qy|), solver noise in χx
            Assert.IsFalse(result.ProvenUncracked);
            var check = result.Checks.Single(c => c.Method == "Wall.Cracking");
            Assert.AreEqual(EngineeringOutcome.Satisfied, check.Outcome); Assert.AreEqual(0, check.Demand, 0);
            var unreinforced = WallSectionCheck.NotSatisfiedWithoutDemand("Wall.Cracking", "mm", .3, "No tensile reinforcement: unreinforced tensile zone, crack width not controlled");
            Assert.AreEqual(EngineeringOutcome.NotSatisfied, unreinforced.Outcome); Assert.AreEqual(ExecutionStatus.Completed, unreinforced.Execution);
            Assert.AreEqual(DataStatus.Ready, unreinforced.Data); Assert.AreEqual(.3, unreinforced.Capacity); Assert.IsNull(unreinforced.Ratio);
            Assert.AreEqual(0, unreinforced.Demand, 0);
        }

        [TestMethod]
        public void ShearUsesConcomitantCompressionAndDeclinesTension()
        {
            var session = Session();
            var zero = session.CheckShear(new WallSectionActions(0, 200000, 1e7));
            var compressed = session.CheckShear(new WallSectionActions(-400000, 200000, 1e7));
            Assert.IsTrue(compressed.Check.Capacity > zero.Check.Capacity);
            Assert.AreEqual(1, compressed.Response.Details.Single(d => d.Symbol == "σcp").Value, 0);
            var tension = session.CheckShear(new WallSectionActions(40000, 200000, 1e7));
            Assert.AreEqual(EngineeringOutcome.NotEvaluated, tension.Check.Outcome);
            Assert.IsNull(tension.Check.Capacity); Assert.IsNull(tension.Check.Ratio);
            Assert.ThrowsException<OperationCanceledException>(() => session.CheckShear(new WallSectionActions(0, 0, 0), new CancellationToken(true)));
        }

        [DataTestMethod]
        [DataRow(0.0, 1)]
        [DataRow(0.0, -1)]
        [DataRow(2.0, 1)]
        [DataRow(2.0, -1)]
        [DataRow(5.0, 1)]
        [DataRow(5.0, -1)]
        public void TotalCurvatureMatchesTheIndependentElasticStrainGradient(double phi, int sign)
        {
            // Fully compressed linear section: epsilon = sigma / Ec,eff at both vertices.
            // Independent kinematic check: kappa = delta(epsilon)/delta(y), not the raw solver gradient.
            var result = Session(phi).CheckServiceability(new WallSectionActions(-1000000, 0, sign * 1e7), ServiceabilityCombination.QuasiPermanent);
            var vertices = result.Response.Concrete;
            double x = vertices.Min(p => p.X);
            var edge = vertices.Where(p => p.X == x).OrderBy(p => p.Y).ToArray();
            Assert.IsTrue(edge.All(p => p.Stress < 0));
            var material = new ConcreteMaterialEN1992("C30", 30, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
            double effectiveModulus = material.E / (1 + phi);
            double gradient = (edge.Last().Stress / effectiveModulus - edge.First().Stress / effectiveModulus) / (edge.Last().Y - edge.First().Y);
            Assert.AreEqual(sign * Math.Abs(gradient), result.Curvature.Value, 1e-15);
        }
    }
}
