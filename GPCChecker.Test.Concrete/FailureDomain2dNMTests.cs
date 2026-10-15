using GPC.Checkers.Concrete.Analysis;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace ConcreteTests
{
    /// <summary>
    /// Sections of the ULS bug fixes of 15/10/2026 (SLU-1, SLU-2, SLU-4), built as ANTHEA's CheckerSection does (see
    /// <see cref="DomainPointContractTests.BuildSection"/>): rectangle 300 × 500 centred in the origin, C25/30 parabola-rectangle, NTC 2018
    /// (αcc 0.85, γc 1.5, γs 1.15), B450C elastic perfectly plastic with εuk 7.5 %, local axes (centroid, -X, -Y), plastic domain, 32 angles.
    /// </summary>
    internal static class UltimateSections
    {
        /// <summary>4Ø16 at (±104; ±204): cover 30, links Ø8 (scenario of the findings sezione-slu F1, F2, F4)</summary>
        internal static ReinforcedConcreteSection Symmetric() => DomainPointContractTests.BuildSection(new DomainPointContractTests.BenchSection
        {
            Name = "C25", Width = 300, Height = 500, Fck = 25,
            Bars = new[] { new double[] { -104, -204, 16 }, new double[] { 104, -204, 16 }, new double[] { -104, 204, 16 }, new double[] { 104, 204, 16 } },
        }, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);

        /// <summary>2Ø16 at (±104; 204) and 3Ø20 at (-102, 0, 102; -202): not symmetric about the x axis (finding sezione-slu F1)</summary>
        internal static ReinforcedConcreteSection Asymmetric() => DomainPointContractTests.BuildSection(new DomainPointContractTests.BenchSection
        {
            Name = "C25", Width = 300, Height = 500, Fck = 25,
            Bars = new[] { new double[] { -104, 204, 16 }, new double[] { 104, 204, 16 }, new double[] { -102, -202, 20 }, new double[] { 0, -202, 20 }, new double[] { 102, -202, 20 } },
        }, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);

        internal static SectionCheckerModelCode2010 Checker(ReinforcedConcreteSection section, SectionSolver.FailureAnalysisTypes criterion,
            SectionSolver.FailureDomainTypes state = SectionSolver.FailureDomainTypes.Plastic)
        {
            var standard = new StandardNTC2018Concrete { AlphaCC = 0.85, GammaC = 1.5, GammaS = 1.15 };
            var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(DomainPointContractTests.LocalAxes(section), criterion, state,
                SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, false, 32);
            var checker = new SectionCheckerModelCode2010(new SectionCheckerAttribute(section, null, null), options, standard, false);
            checker.SetDomainPointStrategy(SectionSolver.DomainPointStrategyTypes.Iterative);
            return checker;
        }

        /// <summary>Force in kN and kNm in the local axes, as CheckerSection.Force of ANTHEA</summary>
        internal static ResultBeamForces Force(ReinforcedConcreteSection section, double nKn, double mxKnm, double myKnm) =>
            DomainPointContractTests.Force(DomainPointContractTests.LocalAxes(section), nKn, mxKnm, myKnm);
    }

    /// <summary>
    /// Bug SLU-1 of 15/10/2026 (finding sezione-slu F1): the N–M 2d domain with a moment direction θ ≠ 0 rebuilt the 3d resistance point by a
    /// rotation valid only for a meridian of positive Mx (θ = 90°: N = 80 604 kN, η = 0.0127 instead of 2.48), took the sign of the 2d moment from
    /// Mx (mixing the two branches of a meridian with a transverse Mx), and the ratio of <see cref="FailureDomain.FailureDomainForce2d"/> divided N
    /// by the scale of the moments. The 2d check is done as ANTHEA's CheckerDomain2D.Check: the 2d moment of the action is its projection on
    /// <see cref="FailureDomain2d.MomentAxis"/>, passed in M1.
    /// Expected values: η of the independent fibre model of the verifier (scratchpad/bug/sezione-slu-d2nm/ref.py and ref_asym.py, rerun on
    /// 15/10/2026) and of agent-conc-slu/ref_slu.py (same model, asymmetric section about x), within 1 %; and the 3d check at constant eccentricity
    /// of the library (another code path, LegacySectionCalculation.SolveResistance), within 1 %.
    /// </summary>
    [TestClass]
    public class FailureDomain2dNMTests
    {
        private static (double Ratio2d, double Ratio3dPoint, FailureDomain.FailureDomainPoint Point) Check2d(ReinforcedConcreteSection section,
            FailureDomainResult2d domain, double nKn, double mxKnm, double myKnm)
        {
            var local = DomainPointContractTests.LocalAxes(section);
            var action = UltimateSections.Force(section, nKn, mxKnm, myKnm);
            double axis = domain.Domain.MomentAxis;
            double m = action.M1 * Math.Cos(axis) + action.M2 * Math.Sin(axis);
            domain.ClearForces();
            var force = domain.AddForce(new ResultBeamForces(action.N, 0, 0, 0, m, 0, local, 1));
            var comparable = new ResultBeamForces(action.N, 0, 0, 0, m * Math.Cos(axis), m * Math.Sin(axis), local);
            return (force.CalculateWorkingRatio(FailureDomainResult2d.DomainTypes.ConstantMxMy, 1e6, 1000),
                force.FailureDomainPoint.CalculateWorkingRatio(SectionSolver.FailureAnalysisTypes.ConstantEccentricity, comparable, 1e6, 1000),
                force.FailureDomainPoint);
        }

        private static double Ratio3d(SectionCheckerModelCode2010 checker, ReinforcedConcreteSection section, double nKn, double mxKnm, double myKnm)
        {
            var response = new LegacySectionCalculation(checker).SolveResistance(new SectionAnalysisInput(UltimateSections.Force(section, nKn, mxKnm, myKnm)));
            Assert.AreEqual(CalculationStatus.Completed, response.Diagnostics.Status);
            return response.Utilization.Value;
        }

        private static void AssertCase(ReinforcedConcreteSection section, SectionCheckerModelCode2010 checker, FailureDomainResult2d domain,
            double nKn, double mxKnm, double myKnm, double expected, string id, double tolerance = 0.01)
        {
            var (ratio2d, ratio3dPoint, point) = Check2d(section, domain, nKn, mxKnm, myKnm);
            double ratio3d = Ratio3d(checker, section, nKn, mxKnm, myKnm);
            Assert.AreEqual(expected, ratio2d, tolerance * expected, id + ": 2d ratio against the independent fibre model");
            Assert.AreEqual(ratio3d, ratio2d, tolerance * ratio3d, id + ": 2d ratio against the 3d check at constant eccentricity");
            Assert.AreEqual(ratio2d, ratio3dPoint, 1e-3 * ratio2d, id + ": ratio of the 3d point of the 2d domain");
            // The resistance has the signs of the action: compression, and a moment along the moment of the action (before: N of 80 604 kN, or
            // tension with the opposite moment at θ = 180°).
            var action = UltimateSections.Force(section, nKn, mxKnm, myKnm);
            Assert.IsTrue(point.NRd < 0, id + ": NRd " + point.NRd);
            Assert.IsTrue(point.MxRd * action.M1 + point.MyRd * action.M2 > 0, id + ": moment of the resistance along the action");
            double transverse = Math.Abs(point.MxRd * action.M2 - point.MyRd * action.M1) / Math.Sqrt(action.M1 * action.M1 + action.M2 * action.M2);
            Assert.IsTrue(transverse < 0.01 * Math.Sqrt(point.MxRd * point.MxRd + point.MyRd * point.MyRd), id + ": transverse moment " + transverse);
        }

        private static FailureDomainResult2d MomentDirectionDomain(SectionCheckerModelCode2010 checker, ReinforcedConcreteSection section, double thetaDegrees)
        {
            double theta = thetaDegrees * Math.PI / 180;
            return checker.GetFailureDomainResult().CalculateDomainConstantMomentsRatio(UltimateSections.Force(section, 0, Math.Cos(theta), Math.Sin(theta)));
        }

        [TestMethod]
        public void SymmetricSectionMeridianOfTheMomentDirection()
        {
            var section = UltimateSections.Symmetric();
            var checker = UltimateSections.Checker(section, SectionSolver.FailureAnalysisTypes.ConstantEccentricity);
            // Expected: ref.py of the verifier (theta 0, 45, 30, 90; 180 and 270 by the double symmetry of the section).
            AssertCase(section, checker, MomentDirectionDomain(checker, section, 0), -1000, 250, 0, 1.380, "θ = 0");
            AssertCase(section, checker, MomentDirectionDomain(checker, section, 30), -500, 86.6025, 50, 0.827, "θ = 30");
            AssertCase(section, checker, MomentDirectionDomain(checker, section, 45), -1000, 120, 120, 1.581, "θ = 45");
            AssertCase(section, checker, MomentDirectionDomain(checker, section, 90), -1000, 0, 200, 2.478, "θ = 90");
            AssertCase(section, checker, MomentDirectionDomain(checker, section, 90), -1000, 0, 60, 0.677, "θ = 90, My 60");
            AssertCase(section, checker, MomentDirectionDomain(checker, section, 180), -1000, -250, 0, 1.380, "θ = 180");
            AssertCase(section, checker, MomentDirectionDomain(checker, section, 270), -1000, 0, -200, 2.478, "θ = 270");
        }

        [TestMethod]
        public void SymmetricSectionMeridianOfTheNeutralAxis()
        {
            // The meridians of the neutral axis at 0°, 90°, 180°, 270° (GetPlasticFailureDomainResult2d, ANTHEA for θ multiple of 90° in local
            // axes): for a doubly symmetric section they are meridians of the moment direction. Expected: ref.py of the verifier.
            var section = UltimateSections.Symmetric();
            var checker = UltimateSections.Checker(section, SectionSolver.FailureAnalysisTypes.ConstantEccentricity);
            foreach (var (theta, n, mx, my, expected) in new[] { (0.0, -1000.0, 250.0, 0.0, 1.380), (90, -1000, 0, 200, 2.478), (180, -1000, -250, 0, 1.380),
                (270, -1000, 0, -200, 2.478), (90, -1000, 0, 60, 0.677) })
            {
                var domain = checker.SectionSolver.GetPlasticFailureDomainResult2d(theta * Math.PI / 180);
                AssertCase(section, checker, domain, n, mx, my, expected, "neutral axis θ = " + theta);
            }
        }

        [TestMethod]
        public void AsymmetricSectionMeridianOfTheMomentDirection()
        {
            var section = UltimateSections.Asymmetric();
            var checker = UltimateSections.Checker(section, SectionSolver.FailureAnalysisTypes.ConstantEccentricity);
            // Expected: θ = 90 and 270 ref_asym.py of the verifier (neutral axis rotated by -3.11° so that Mx = 0: R = (-508.3; My 101.66),
            // η = 1.967; the section is symmetric about the y axis); θ = 0 and 180 agent-conc-slu/ref_slu.py (compression on the 2Ø16 side
            // 1.1193, on the 3Ø20 side 1.1511).
            AssertCase(section, checker, MomentDirectionDomain(checker, section, 90), -1000, 0, 200, 1.967, "θ = 90");
            AssertCase(section, checker, MomentDirectionDomain(checker, section, 270), -1000, 0, -200, 1.967, "θ = 270");
            AssertCase(section, checker, MomentDirectionDomain(checker, section, 0), -1000, 250, 0, 1.1193, "θ = 0");
            AssertCase(section, checker, MomentDirectionDomain(checker, section, 180), -1000, -250, 0, 1.1511, "θ = 180");
        }

        [TestMethod]
        public void AsymmetricSectionMeridianOfTheNeutralAxisKeepsItsBranches()
        {
            // The meridian of the neutral axis at 90° has a transverse Mx from about +42 to -39 kNm: with the sign of the 2d moment taken from Mx
            // the two branches were mixed (η 0.0095 «inside»). With the sign of the projection on the My axis the branches are kept; the meridian
            // of the neutral axis itself is not the section of the domain at Mx = 0 (verifier: NA fixed, M = hypot (-537.7; 107.54) instead of
            // (-508.3; 101.66), about 5 % unconservative): the N–M check of such a section uses the meridian of the moment direction (test above).
            var section = UltimateSections.Asymmetric();
            var checker = UltimateSections.Checker(section, SectionSolver.FailureAnalysisTypes.ConstantEccentricity);
            foreach (double theta in new[] { 90.0, 270.0 })
            {
                var domain = checker.SectionSolver.GetPlasticFailureDomainResult2d(theta * Math.PI / 180);
                Assert.AreEqual(Math.PI / 2, domain.Domain.MomentAxis, 1e-12, "axis of the 2d moment");
                double my = theta == 90 ? 200 : -200;
                var (ratio2d, _, point) = Check2d(section, domain, -1000, 0, my);
                // ref_asym.py of the verifier, neutral axis fixed and M = hypot: R = (-537.7 kN; 107.54 kNm), η = |(-1000; 200)| / |R| = 1.860.
                Assert.AreEqual(1.860, ratio2d, 0.01 * 1.860, "neutral axis θ = " + theta);
                Assert.IsTrue(point.NRd < 0 && point.MyRd * UltimateSections.Force(section, -1000, 0, my).M2 > 0, "branch of the action, θ = " + theta);
            }
        }

        [TestMethod]
        public void ElasticDomainMeridianOfTheMomentDirection()
        {
            // The elastic (SLV) N–M domain of the moment direction had two null points (ArgumentNullException at the first force) and, because the
            // search at constant eccentricity of the elastic domain returns the pole of compression for a pure tension, a pole of tension at
            // N = NRd,c that cut the domain (η 4.64 instead of 0.76 for (-1000; 0; 60)). Expected: the 3d check at constant eccentricity of the
            // elastic domain (another code path, LegacySectionCalculation.SolveResistance), within 1 %; no independent model of the elastic domain.
            var section = UltimateSections.Symmetric();
            var checker = UltimateSections.Checker(section, SectionSolver.FailureAnalysisTypes.ConstantEccentricity, SectionSolver.FailureDomainTypes.Elastic);
            var domain = MomentDirectionDomain(checker, section, 90);
            Assert.IsFalse(Array.Exists(domain.Domain.DomainPoints, p => p == null), "null points");
            Assert.AreEqual(1, Array.FindAll(domain.Domain.DomainPoints, p => p.NRd > 300e3).Length, "one pole of tension (NRd,t = 314.7 kN)");
            foreach (var (n, my) in new[] { (-1000.0, 60.0), (100, 10), (-2000, 20), (0, 50) })
            {
                var (ratio2d, _, point) = Check2d(section, domain, n, 0, my);
                double ratio3d = Ratio3d(checker, section, n, 0, my);
                Assert.AreEqual(ratio3d, ratio2d, 0.01 * ratio3d, "elastic (" + n + "; 0; " + my + ")");
                Assert.IsTrue(point.MyRd * my > 0, "branch of the action");
            }
        }

        [TestMethod]
        public void MomentAxisFollowsTheSignRuleOfAnthea()
        {
            // ANTHEA's CheckerDomain2D multiplies the projection of the action on θ by sign(cos θ) (sign(sin θ) when |cos θ| ≤ 1e-8): the axis is
            // the direction of the meridian reduced to (-90°; 90°].
            var section = UltimateSections.Symmetric();
            var checker = UltimateSections.Checker(section, SectionSolver.FailureAnalysisTypes.ConstantEccentricity);
            var local = DomainPointContractTests.LocalAxes(section);
            foreach (double theta in new[] { 0.0, 30, 90, 135, 180, 225, 270, 315 })
            {
                var direction = UltimateSections.Force(section, 0, Math.Cos(theta * Math.PI / 180), Math.Sin(theta * Math.PI / 180));
                double localTheta = Math.Atan2(direction.M2, direction.M1);
                double sign = Math.Abs(Math.Cos(localTheta)) > 1e-8 ? Math.Sign(Math.Cos(localTheta)) : Math.Sign(Math.Sin(localTheta));
                double axis = MomentDirectionDomain(checker, section, theta).Domain.MomentAxis;
                Assert.AreEqual(Math.Cos(localTheta) * sign, Math.Cos(axis), 1e-9, "cos, θ = " + theta);
                Assert.AreEqual(Math.Sin(localTheta) * sign, Math.Sin(axis), 1e-9, "sin, θ = " + theta);
                Assert.IsTrue(axis > -Math.PI / 2 && axis <= Math.PI / 2, "range, θ = " + theta);
            }
            Assert.IsTrue(double.IsNaN(checker.GetFailureDomainResult().CalculateDomainConstantAxialForce(UltimateSections.Force(section, -1000, 0, 0)).Domain.MomentAxis));
        }
    }
}
