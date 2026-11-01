using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Response;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Model.Materials;
using GPC.Model.Sections.Concrete;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace ConcreteTests
{
    /// <summary>
    /// Curvature ductility μφ (NTC 2018 §4.1.2.3.4.2, Circolare 2019 Fig. C4.1.12; voce R14 of ANTHEA, 1/11/2026) against the independent reference
    /// tools/attesi/duttilita_curvatura.py: rectangular section integrated in closed form on the exact laws, neutral axis by bisection at each
    /// curvature, 2000 curvature steps, peak by golden section. Sections of <see cref="UltimateSections"/> (300 × 500 C25/30 4Ø16, B450C elastic
    /// perfectly plastic, εuk 7.5 %, NTC 2018) with the parabola-rectangle and with the nonlinear law of EN 1992-1-1 3.1.5 (descending branch after
    /// εc1, the case in which the moment falls to 0.85 Mmax before the limit point), and the section of the example of the page (§10).
    /// Tolerances: with the parabola-rectangle the library integrates the analytic law exactly on the mesh of the rectangle, so only the searches
    /// remain (bisections, golden section, residual on N of the limit point): 1e-5 (measured: at most 3e-8 on the curvatures, 4e-7 on MRd at
    /// N = -1000 kN); the nonlinear law is a 14-point table integrated by Gauss points across its kinks: 2e-3 (measured: at most 7e-4 on MRd).
    /// </summary>
    [TestClass]
    public class MomentCurvatureDuctilityTests
    {
        private static ReinforcedConcreteSection Symmetric(ConcreteMaterial.CompressionStressStrainDiagrams diagram) =>
            DomainPointContractTests.BuildSection(new DomainPointContractTests.BenchSection
            {
                Name = "C25", Width = 300, Height = 500, Fck = 25,
                Bars = new[] { new double[] { -104, -204, 16 }, new double[] { 104, -204, 16 }, new double[] { -104, 204, 16 }, new double[] { 104, 204, 16 } },
            }, diagram);

        private static ReinforcedConcreteSection PageExample() => DomainPointContractTests.BuildSection(new DomainPointContractTests.BenchSection
        {
            Name = "C30", Width = 300, Height = 500, Fck = 30,
            Bars = new[] { new double[] { -100, -202, 20 }, new double[] { 0, -202, 20 }, new double[] { 100, -202, 20 }, new double[] { -100, 204, 16 }, new double[] { 100, 204, 16 } },
        }, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);

        private static MomentCurvatureResult Curve(ReinforcedConcreteSection section, double axialForce, MomentCurvatureDuctilityRequest ductility = null,
            ConcreteMaterial.TensionResponseDiagrams tension = ConcreteMaterial.TensionResponseDiagrams.Default)
        {
            var checker = UltimateSections.Checker(section, SectionSolver.FailureAnalysisTypes.ConstantN);
            checker.SectionCheckerOptions.StressAnalysisConcreteTension = tension;
            var request = new MomentCurvatureRequest(axialForce, 0, 40);
            var axes = DomainPointContractTests.LocalAxes(section);
            return ductility == null ? MomentCurvatureAnalysis.Calculate(request, checker, section, axes, 450 / 1.15 / 200000)
                : MomentCurvatureAnalysis.Calculate(request, checker, section, axes, 450 / 1.15 / 200000, ductility);
        }

        private static void Relative(double expected, double actual, double tolerance, string what) =>
            Assert.AreEqual(1, actual / expected, tolerance, $"{what}: expected {expected:G7}, actual {actual:G7}");

        /// <summary>Values of tools/attesi/duttilita_curvatura.py (N in kN, moments in kNm, curvatures in 1/mm).</summary>
        private static void AssertCase(ReinforcedConcreteSection section, double nKn, MomentCurvatureFirstYield firstYield, double myd1, double chiYd1,
            double mrd, double mmax, double phiYd, double phiU, MomentCurvatureUltimate ultimate, double mu, double tolerance = 1e-5)
        {
            var result = Curve(section, nKn * 1e3, new MomentCurvatureDuctilityRequest());
            var d = result.Ductility;
            Assert.IsNotNull(d, result.DuctilityMessage);
            string at = $"N = {nKn} kN: ";
            Console.WriteLine(at + $"M'yd {d.FirstYieldMoment:R} φ'yd {d.FirstYieldCurvature:R} MRd {d.ResistingMoment:R} Mmax {d.MaximumMoment:R} φyd {d.YieldCurvature:R} " +
                $"φu {d.UltimateCurvature:R} μφ {d.CurvatureDuctility:R} ({d.FirstYield}, {d.Ultimate}); N limit {result.LimitAxialForce:R}");
            Assert.AreEqual(firstYield, d.FirstYield, at + "first yield");
            Assert.AreEqual(ultimate, d.Ultimate, at + "ultimate");
            Relative(myd1 * 1e6, d.FirstYieldMoment, tolerance, at + "M'yd");
            Relative(chiYd1, d.FirstYieldCurvature, tolerance, at + "φ'yd");
            Relative(mrd * 1e6, d.ResistingMoment, tolerance, at + "MRd");
            Relative(mmax * 1e6, d.MaximumMoment, tolerance, at + "Mmax");
            Relative(phiYd, d.YieldCurvature, tolerance, at + "φyd");
            Relative(phiU, d.UltimateCurvature, tolerance, at + "φu");
            Relative(mu, d.CurvatureDuctility, tolerance, at + "μφ");
            // definitions
            Assert.AreEqual(d.ResistingMoment / d.FirstYieldMoment * d.FirstYieldCurvature, d.YieldCurvature, 1e-12 * d.YieldCurvature);
            Assert.AreEqual(d.UltimateCurvature / d.YieldCurvature, d.CurvatureDuctility, 1e-12 * d.CurvatureDuctility);
            Assert.AreEqual(result.LimitMoment, d.ResistingMoment);
            // the curvature-controlled branch ends at the limit point of the domain; the sample before it is a state at the exact N
            Assert.AreEqual(result.UltimateCurvature.Value, d.LimitCurvature, 1e-9 * d.LimitCurvature);
            Assert.AreEqual(d.ResistingMoment, d.Points.Last().Moment);
            Assert.AreEqual(nKn * 1e3, result.LimitAxialForce, 1000);
            Assert.AreEqual(50, d.Points.Count);
            Assert.IsTrue(d.Points.Zip(d.Points.Skip(1), (a, b) => b.Curvature > a.Curvature).All(v => v));
        }

        [TestMethod]
        public void ParabolaRectangleMatchesTheIndependentReference()
        {
            var section = Symmetric(ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
            AssertCase(section, 0, MomentCurvatureFirstYield.TensionSteel, 65.29117824967983, 5.682050121602971e-06, 68.44920191371588, 68.4492019197639,
                5.956881258447238e-06, 7.624436766886174e-05, MomentCurvatureUltimate.LimitPoint, 12.799376781389148);
            AssertCase(section, -500, MomentCurvatureFirstYield.TensionSteel, 148.5085044804422, 8.099610300216461e-06, 158.54338533769067, 158.54338533918306,
                8.64690976052123e-06, 2.3812030738306806e-05, MomentCurvatureUltimate.LimitPoint, 2.7538197341926964);
            AssertCase(section, -1000, MomentCurvatureFirstYield.ConcretePeakStrain, 157.71051690955306, 6.0988605434476525e-06, 191.9336418558409, 191.9336418558409,
                7.42231106848839e-06, 1.1987197541623298e-05, MomentCurvatureUltimate.LimitPoint, 1.6150222526397808);
            AssertCase(section, -1500, MomentCurvatureFirstYield.ConcretePeakStrain, 122.96964065237016, 4.188470855138333e-06, 152.6784802711588, 152.6784802711588,
                5.200384106434614e-06, 8.697856383735212e-06, MomentCurvatureUltimate.LimitPoint, 1.6725411442153004);
        }

        [TestMethod]
        public void NonlinearLawFollowsThePostPeakBranch()
        {
            var section = Symmetric(ConcreteMaterial.CompressionStressStrainDiagrams.NonLinear);
            AssertCase(section, -1000, MomentCurvatureFirstYield.ConcretePeakStrain, 159.484719, 6.34817794533328e-06, 169.573376, 176.829126,
                6.74974990290127e-06, 1.1311573968145592e-05, MomentCurvatureUltimate.LimitPoint, 1.675851, 2e-3);
            // the moment falls to 0.85 Mmax just before the limit point: φu from the post-peak branch
            AssertCase(section, -1500, MomentCurvatureFirstYield.ConcretePeakStrain, 124.81988, 4.3674647626309065e-06, 117.062464, 138.503132,
                4.096031740023324e-06, 8.104447332900921e-06, MomentCurvatureUltimate.PostPeakReduction, 1.97861, 2e-3);
        }

        [TestMethod]
        public void ExampleOfThePage()
        {
            // §10 of ca.momento-curvatura: M'yd = My,1 = 147.535 kNm (the bottom bars yield first, εc = 0.98 ‰ < εc2), MRd = 154.688 kNm
            AssertCase(PageExample(), 0, MomentCurvatureFirstYield.TensionSteel, 147.535471630657, 6.506881897750773e-06, 154.6877288014168, 154.68772880757925,
                6.822323955163116e-06, 5.119063729573926e-05, MomentCurvatureUltimate.LimitPoint, 7.503401719438774);
            // values of the curve printed in the table of §10 of the page (library column)
            var curve = Curve(PageExample(), 0);
            var yielded = curve.Points.First(v => v.Yielded);
            Console.WriteLine($"page §10: Mr {curve.LimitMoment:R} chi_u {curve.UltimateCurvature:R} eps_s limit {curve.Points.Last().SteelStrain:R} " +
                $"My1 {curve.YieldRefinement.Moment:R} chi_y1 {curve.YieldCurvature:R} eps_c yield {yielded.ConcreteCompressionStrain:R} N limit {curve.LimitAxialForce:R} points {curve.Points.Count}");
        }

        [TestMethod]
        public void TheCurveIsTheSameWithAndWithoutTheDuctility()
        {
            var section = Symmetric(ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
            var plain = Curve(section, -500e3);
            var withDuctility = Curve(section, -500e3, new MomentCurvatureDuctilityRequest());
            Assert.IsNull(plain.Ductility);
            Assert.IsNull(plain.DuctilityMessage);
            Assert.AreEqual(plain.Points.Count, withDuctility.Points.Count);
            for (int i = 0; i < plain.Points.Count; i++)
            {
                MomentCurvaturePoint a = plain.Points[i], b = withDuctility.Points[i];
                Assert.AreEqual(a.Moment, b.Moment); Assert.AreEqual(a.Curvature, b.Curvature); Assert.AreEqual(a.SteelStrain, b.SteelStrain);
                Assert.AreEqual(a.ConcreteCompressionStrain, b.ConcreteCompressionStrain); Assert.AreEqual(a.ReferenceStrain, b.ReferenceStrain);
            }
            Assert.AreEqual(plain.LimitMoment, withDuctility.LimitMoment);
            Assert.AreEqual(plain.YieldCurvature, withDuctility.YieldCurvature);
            Assert.IsTrue(withDuctility.Status.StartsWith(plain.Status));
        }

        /// <summary>
        /// The generic variant (0.35.0.0, as ANTHEA uses it) in kN and kNm, with the functions of the native overload and the section of
        /// <see cref="MomentCurvatureDuctilitySection.From"/>: the same ductility as the native overload, up to the stopping tests of the searches,
        /// which are relative to the moments and to N in the units of the caller (1e-5).
        /// </summary>
        [TestMethod]
        public void GenericVariantGivesTheNativeDuctility()
        {
            var section = PageExample();
            foreach (double nKn in new[] { 0.0, -500 })
            {
                var native = Curve(section, nKn * 1e3, new MomentCurvatureDuctilityRequest());
                var checker = UltimateSections.Checker(section, SectionSolver.FailureAnalysisTypes.ConstantN);
                var axes = DomainPointContractTests.LocalAxes(section);
                var domain = checker.GetFailureDomainResult();
                domain.FailureAnalysisType = SectionSolver.FailureAnalysisTypes.ConstantN;
                var origin = checker.SectionCheckerOptions.ForceReferenceCoordinateSystem.Origin;
                MomentCurvatureLimit Limit(double n, double cx, double sy)
                {
                    var point = domain.CalculatePoint(new GPC.Model.Results.ResultBeamForces(n * 1e3, 0, 0, 0, cx * 1e6, sy * 1e6, axes));
                    return point?.StrainPlane == null ? null
                        : new MomentCurvatureLimit(point.NRd / 1e3, point.MxRd / 1e6, point.MyRd / 1e6, MomentCurvatureStrains.From(point.StrainPlane, section, origin));
                }
                MomentCurvatureStrains Response(double n, double mx, double my) => MomentCurvatureStrains.From(
                    checker.GetTensionAnalysisResult(new GPC.Model.Results.ResultBeamForces(n * 1e3, 0, 0, 0, mx * 1e6, my * 1e6, axes)).StrainPlane, section, origin);
                var generic = MomentCurvatureAnalysis.Calculate(new MomentCurvatureRequest(nKn, 0, 40, axialTolerance: 1), Limit, Response, 450 / 1.15 / 200000,
                    new MomentCurvatureUnits("kN", "kNm"), MomentCurvatureDuctilitySection.From(checker, section, 1e3, 1e6), new MomentCurvatureDuctilityRequest());
                MomentCurvatureDuctility a = native.Ductility, b = generic.Ductility;
                Assert.IsNotNull(b, generic.DuctilityMessage);
                Relative(a.FirstYieldMoment / 1e6, b.FirstYieldMoment, 1e-5, nKn + " M'yd");
                Relative(a.FirstYieldCurvature, b.FirstYieldCurvature, 1e-5, nKn + " φ'yd");
                Relative(a.ResistingMoment / 1e6, b.ResistingMoment, 1e-9, nKn + " MRd");
                Relative(a.MaximumMoment / 1e6, b.MaximumMoment, 1e-5, nKn + " Mmax");
                Relative(a.YieldCurvature, b.YieldCurvature, 1e-5, nKn + " φyd");
                Relative(a.UltimateCurvature, b.UltimateCurvature, 1e-9, nKn + " φu");
                Relative(a.CurvatureDuctility, b.CurvatureDuctility, 1e-5, nKn + " μφ");
                Assert.AreEqual(a.FirstYield, b.FirstYield); Assert.AreEqual(a.Ultimate, b.Ultimate); Assert.AreEqual(a.Points.Count, b.Points.Count);
                StringAssert.Contains(generic.Status, "kNm");
                // Without the limit point (end fraction below 1) the ductility is not computed and the message says why.
                var partial = MomentCurvatureAnalysis.Calculate(new MomentCurvatureRequest(nKn, 0, 40, 0.9, axialTolerance: 1), Limit, Response, 450 / 1.15 / 200000,
                    new MomentCurvatureUnits("kN", "kNm"), MomentCurvatureDuctilitySection.From(checker, section, 1e3, 1e6), new MomentCurvatureDuctilityRequest());
                Assert.IsNull(partial.Ductility); StringAssert.Contains(partial.DuctilityMessage, "limit point");
            }
            // The section of a checker whose responses use the concrete in tension is refused.
            var stiff = UltimateSections.Checker(section, SectionSolver.FailureAnalysisTypes.ConstantN);
            stiff.SectionCheckerOptions.StressAnalysisConcreteTension = ConcreteMaterial.TensionResponseDiagrams.TensionChord;
            Assert.ThrowsException<ArgumentException>(() => MomentCurvatureDuctilitySection.From(stiff, section));
        }

        [TestMethod]
        public void RejectionsAndOptionsNotSupported()
        {
            var section = Symmetric(ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
            Assert.ThrowsException<ArgumentException>(() => Curve(section, 0, new MomentCurvatureDuctilityRequest(9)));
            Assert.ThrowsException<ArgumentException>(() => Curve(section, 0, new MomentCurvatureDuctilityRequest(50, 61)));
            var stiffening = Curve(section, 0, new MomentCurvatureDuctilityRequest(), ConcreteMaterial.TensionResponseDiagrams.TensionStiffening);
            Assert.IsNull(stiffening.Ductility);
            StringAssert.Contains(stiffening.DuctilityMessage, "concrete in tension");
        }
    }
}
