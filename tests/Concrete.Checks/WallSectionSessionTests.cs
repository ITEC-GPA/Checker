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

        /// <summary>
        /// S3 of the embedded-wall review (scheda decisioni ANTHEA 16/11/2026): with <see cref="WallCrackSpacing.TensionFace"/> the crack check takes the bar
        /// spacing of the face in tension in each combination, not the sparser face. Strip 1000 × 400, clear cover 40: top 4Ø20, s = (1000 − 80 − 20)/3 = 300 mm;
        /// bottom 7Ø16, s = (1000 − 80 − 16)/6 = 150.67 mm. NTC (Circolare C4.1.2.2.4.5): Δsm = Δsm,near when s ≤ 5 (c + Ø/2), otherwise max(Δsm,near;
        /// 0.75 (h − x)). Bottom in tension (M = +50 kNm, Ø16, limit 5 (40 + 8) = 240 mm): the sparser face gave s = 300 > 240, so Δsm = max(near; far) with
        /// far = 0.75 (h − x) > near and a larger wk; the tension face gives s = 150.67 ≤ 240 and Δsm = near. Top in tension (M = −50 kNm): s = 300 with both
        /// rules, wk identical. Entirely tensile strip (N = +800 kN, M = 10 kNm): both faces in tension, the larger spacing with both rules. The default
        /// session (retaining walls) keeps the sparser face.
        /// Independent expected values (R2 of the review G-207, 16/11/2026): the cracked section by hand in tools/attesi/fessurazione_s3_faccia_tesa.py
        /// (part B), not the intermediate results of the same calculation: C30 Ecm = 22 000 (38/10)^0.3 = 32 836.568 MPa, fctm = 2.896468 MPa,
        /// n = Es (1 + φ)/Ecm = 18.272312 (φ = 2), compressed bars with n − 1, no tensile concrete. M = +50 kNm: x = 102.658455 mm from the top, h − x =
        /// 297.341545 mm, σs = 112.791411 MPa, hc,eff = (h − x)/3 = 99.113848 mm, ρ = 7 · 201.06/(1000 hc,eff) = 0.0142002, εsm − εcm = 3.38374e-4,
        /// Δsm,near = 192.674706 mm, Δsm,far = 223.006159 mm, wk 0.128281215 → 0.110833465 mm. M = −50 kNm: h − x = 303.599917 mm, σs = 126.449656 MPa,
        /// s = 300 > 5 (40 + 10) = 250 mm, Δsm = max(near 241.064759; far 227.699938), wk = 0.155461035 mm with both rules. Tolerance 1e-3 relative:
        /// the section solver integrates the stresses on its mesh (deviation from the closed form about 1e-4 in this case); the branch of Δsm is
        /// checked exactly by <see cref="CrackWidthBranchOfS3MatchesTheIndependentScalarVector"/>.
        /// </summary>
        [TestMethod]
        public void CrackSpacingOfTheTensionFaceIsOptIn()
        {
            WallSectionSession Unequal(WallCrackSpacing? rule)
            {
                var strip = new WallStrip(WallMember.Stem, 1000, 400, 40, 4, 20, 7, 16);
                var concrete = new ConcreteMaterialEN1992("C30", 30, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
                var steel = new SteelMaterial("B450", 200000, 450, 450, .075, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Rebar);
                var section = new ReinforcedConcreteSection(GetRectangularShape(1000, 400), concrete);
                var top = new RebarSectionCircular(20, steel); var bottom = new RebarSectionCircular(16, steel);
                section.AddRebars(Enumerable.Range(0, 4).Select(i => new ReinforcedConcreteRebar(top, new Point3d(40 + 10 + (1000 - 80 - 20) * i / 3.0, 400 - 40 - 10, 0)))
                    .Concat(Enumerable.Range(0, 7).Select(i => new ReinforcedConcreteRebar(bottom, new Point3d(40 + 8 + (1000 - 80 - 16) * i / 6.0, 40 + 8, 0)))).ToArray());
                return rule.HasValue ? new WallSectionSession(strip, section, new StandardNTC2018Concrete(), 2, "XC2", rule.Value)
                    : new WallSectionSession(strip, section, new StandardNTC2018Concrete(), 2, "XC2");
            }
            double sTop = (1000 - 80 - 20) / 3.0, sBottom = (1000 - 80 - 16) / 6.0;
            var probe = new WallStrip(WallMember.Stem, 1000, 400, 40, 4, 20, 7, 16);
            Assert.AreEqual(sTop, probe.BarSpacing(WallFace.Top), 1e-12); Assert.AreEqual(sBottom, probe.BarSpacing(WallFace.Bottom), 1e-12);
            Assert.AreEqual(Math.Max(sTop, sBottom), probe.MaximumBarSpacing, 0);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => probe.BarSpacing((WallFace)7));
            var sparser = Unequal(null); var tension = Unequal(WallCrackSpacing.TensionFace);
            Assert.AreEqual(WallCrackSpacing.SparserFace, sparser.CrackSpacing); Assert.AreEqual(WallCrackSpacing.TensionFace, tension.CrackSpacing);
            GPC.Checkers.Concrete.Cracking.SectionCrackResult Crack(WallSectionSession session, double n, double m)
                => session.CheckServiceability(new WallSectionActions(n, 0, m), ServiceabilityCombination.QuasiPermanent).CrackResponse;
            double Entry(GPC.Checkers.Concrete.Cracking.SectionCrackResult r, string symbol) => r.Details.Single(d => d.Symbol == symbol).Value;

            // Bottom face in tension: the spacing and the NTC branch change, wk decreases.
            var before = Crack(sparser, 0, 5e7); var after = Crack(tension, 0, 5e7);
            Assert.AreEqual(sTop, before.BarSpacing.Value, 1e-12); Assert.AreEqual(sBottom, after.BarSpacing.Value, 1e-12);
            Assert.AreEqual(240, Entry(after, "s,lim"), 1e-12);
            double near = Entry(after, "Δsm,near"), far = Entry(after, "Δsm,far"), strain = Entry(after, "εsm − εcm");
            Assert.IsTrue(far > near, "0.75 (h − x) = " + far + " > Δsm,near = " + near);
            Assert.AreEqual(near, Entry(before, "Δsm,near"), 0); Assert.AreEqual(far, Entry(before, "Δsm,far"), 0); Assert.AreEqual(strain, Entry(before, "εsm − εcm"), 0);
            Assert.AreEqual(1.7 * near * strain, after.Width.Value, 1e-12, "close bars: Δsm = near");
            Assert.AreEqual(1.7 * far * strain, before.Width.Value, 1e-12, "sparser face: Δsm = max(near; far) = far");
            Console.WriteLine("S3 bottom in tension: s {0} -> {1} mm, wk {2:R} -> {3:R} mm", before.BarSpacing, after.BarSpacing, before.Width, after.Width);
            // Independent expected values (tools/attesi/fessurazione_s3_faccia_tesa.py, part B), relative tolerance 1e-3 (mesh of the section solver).
            void Near(double expected, double actual, string what) => Assert.AreEqual(expected, actual, 1e-3 * Math.Abs(expected), what + " = " + actual.ToString("R"));
            foreach (var r in new[] { before, after })
            {
                Near(297.341545138, Entry(r, "h − x"), "h − x"); Near(112.791411298, Entry(r, "σs"), "σs");
                Near(99113.848379, Entry(r, "Ac,eff"), "Ac,eff"); Assert.AreEqual(7 * Math.PI * 64, Entry(r, "As,eff"), 1e-9, "As,eff: the seven bottom bars");
                Near(0.014200170126, Entry(r, "ρp,eff"), "ρp,eff"); Near(3.38374234e-4, Entry(r, "εsm − εcm"), "εsm − εcm");
                Near(192.674706418, Entry(r, "Δsm,near"), "Δsm,near"); Near(223.006158854, Entry(r, "Δsm,far"), "Δsm,far");
            }
            Near(0.110833465, after.Width.Value, "wk, tension face (Δsm = near)"); Near(0.128281215, before.Width.Value, "wk, sparser face (Δsm = far)");

            // Top face in tension: the sparser face is the tension face, nothing changes.
            var topBefore = Crack(sparser, 0, -5e7); var topAfter = Crack(tension, 0, -5e7);
            Assert.AreEqual(sTop, topAfter.BarSpacing.Value, 1e-12); Assert.AreEqual(topBefore.Width, topAfter.Width); Assert.AreEqual(topBefore.Status, topAfter.Status);
            Near(303.599917460, Entry(topAfter, "h − x"), "top: h − x"); Near(126.449656155, Entry(topAfter, "σs"), "top: σs");
            Near(241.064758620, Entry(topAfter, "Δsm,near"), "top: Δsm,near"); Near(227.699938095, Entry(topAfter, "Δsm,far"), "top: Δsm,far");
            Assert.AreEqual(250, Entry(topAfter, "s,lim"), 1e-12); Near(0.155461035, topAfter.Width.Value, "top: wk (s = 300 > 250, Δsm = max = near)");

            // Entirely tensile strip: both faces in tension, the larger spacing with both rules.
            var tensileBefore = Crack(sparser, 8e5, 1e7); var tensileAfter = Crack(tension, 8e5, 1e7);
            Assert.AreEqual(sTop, tensileAfter.BarSpacing.Value, 1e-12); Assert.AreEqual(tensileBefore.Width, tensileAfter.Width);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => Unequal((WallCrackSpacing)9));
        }

        /// <summary>
        /// R2 of the review G-207 (16/11/2026): the branch of Δsm that S3 switches, with fixed expected values from explicit inputs, not from the
        /// Details of the same calculation. Vector of the review (section S3, table W1-W4), recomputed in tools/attesi/fessurazione_s3_faccia_tesa.py
        /// (part A): σs 200 MPa, Es 200 000, Ecm 30 000, fct 3 MPa, ρ 0.02, Ø16 ribbed (k1 0.8), k2 0.5, long term (kt 0.4), c 40 mm, h − x 300 mm.
        /// εsm − εcm = max[(200 − 0.4 · 3/0.02 · (1 + 0.02 · 200 000/30 000))/200 000; 0.6 · 200/200 000] = 0.00066; Δsm,near = (3.4 · 40 + 0.8 · 0.5 ·
        /// 0.425 · 16/0.02)/1.7 = 160 mm; Δsm,far = 0.75 · 300 = 225 mm; s,lim = 5 (40 + 8) = 240 mm. wk = 1.7 · 160 · 0.00066 = 0.17952 mm for
        /// s = 150.67 and s = 240 exactly (close bars), 1.7 · 225 · 0.00066 = 0.25245 mm for s = 300 and s = 240.000001 mm.
        /// </summary>
        [TestMethod]
        public void CrackWidthBranchOfS3MatchesTheIndependentScalarVector()
        {
            foreach (var (s, distance, width) in new[] { ((1000 - 80 - 16) / 6.0, 160.0, .17952), (300.0, 225.0, .25245), (240.0, 160.0, .17952), (240.000001, 225.0, .25245) })
            {
                var details = new System.Collections.Generic.List<GPC.Checkers.Concrete.Shear.ShearCalculationDetail>();
                double wk = GPC.Checkers.Concrete.Cracking.CrackWidthCalculator.Width(GPC.Checkers.Concrete.Cracking.CrackProfile.Ntc2018,
                    new GPC.Checkers.Concrete.Cracking.CrackWidthInput(200, 200000, 30000, 3, .02, 16, 40, s, 300, false, true, .5), details);
                double Entry(string symbol) => details.Single(d => d.Symbol == symbol).Value;
                string what = "s = " + s.ToString("R");
                Assert.AreEqual(.00066, Entry("εsm − εcm"), 1e-15, what); Assert.AreEqual(160, Entry("Δsm,near"), 1e-10, what);
                Assert.AreEqual(225, Entry("Δsm,far"), 1e-10, what); Assert.AreEqual(240, Entry("s,lim"), 1e-12, what);
                Assert.AreEqual(distance, Entry("Δsm"), 1e-10, what); Assert.AreEqual(width, wk, 1e-12, what); Assert.AreEqual(width, Entry("wk"), 1e-12, what);
            }
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
