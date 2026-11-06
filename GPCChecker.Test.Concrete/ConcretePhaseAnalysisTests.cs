using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Response;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Sections.Steel;
using GPC.Model.Sections;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConcreteTests
{
    [TestClass]
    public class ConcretePhaseAnalysisTests
    {
        public TestContext TestContext { get; set; }
        private static ReinforcedConcreteSection Section(bool bars = true, bool asymmetric = false)
        {
            var shape = DomainPointContractTests.Sections.Single(s => s.Name == (asymmetric ? "R3" : "R2"));
            return DomainPointContractTests.BuildSection(new DomainPointContractTests.BenchSection
            { Name = shape.Name, Width = shape.Width, Height = shape.Height, Fck = shape.Fck, Bars = bars ? shape.Bars : new double[0][] },
                ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
        }
        private static SectionCheckerModelCode2010 Checker(ReinforcedConcreteSection section, bool linear = true, CoordinateSystem axes = null)
        {
            var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(axes ?? DomainPointContractTests.LocalAxes(section),
                SectionSolver.FailureAnalysisTypes.ConstantN, SectionSolver.FailureDomainTypes.Plastic,
                linear ? SectionSolver.StressAnalysisTypes.Linear : SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, linear, 32);
            return new SectionCheckerModelCode2010(new SectionCheckerAttribute(section, null, null), options,
                new StandardNTC2018Concrete { AlphaCC = .85, GammaC = 1.5, GammaS = 1.15 }, false);
        }
        private static ConcretePhase Load(SectionChecker checker, double n, double m1 = 0, double m2 = 0, int steps = 1)
            => ConcretePhase.Load("load", new ResultBeamForces(n, 0, 0, 0, m1, m2, checker.SectionCheckerOptions.ForceReferenceCoordinateSystem), steps);
        private ConcretePhaseState Last(ConcretePhaseAnalysisResult result)
        {
            Assert.IsTrue(result.Completed, result.Message);
            foreach (var p in result.Phases)
            {
                Assert.AreEqual(1.0, p.ReachedFraction);
                Assert.IsTrue(p.State.NormalizedResidual <= 1);
                Assert.IsTrue(p.Attempts >= 1 && p.ForceEvaluations >= 1);
                Assert.IsTrue(Math.Abs(p.State.ForceResidual) < .02);
                Assert.IsTrue(Math.Abs(p.State.Moment1Residual) < 2);
                Assert.IsTrue(Math.Abs(p.State.Moment2Residual) < 2);
                TestContext.WriteLine("{0}: {1:F3} ms; iterations {2}; attempts {3}; evaluations {4}; residuals {5:G5}, {6:G5}, {7:G5}",
                    p.Phase.Name, p.ElapsedMilliseconds, p.Iterations, p.Attempts, p.ForceEvaluations,
                    p.State.ForceResidual, p.State.Moment1Residual, p.State.Moment2Residual);
            }
            return result.Phases.Last().State;
        }

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void FreeShrinkageHasNoStress(bool linear)
        {
            var checker = Checker(Section(false), linear);
            var state = Last(ConcretePhaseAnalysis.Calculate(checker, new[] { ConcretePhase.ShrinkageMicrostrain("shrink", -200) }));
            Assert.AreEqual(-.0002, state.TotalStrain.Epsilon, 1e-12);
            Assert.AreEqual(0, state.ConcreteMechanicalStrain.Epsilon, 1e-12);
            Assert.IsTrue(state.Points.All(p => Math.Abs(p.Stress) < 1e-8));
            Assert.IsNull(state.ConcreteNeutralAxis);
        }

        [TestMethod]
        public void SymmetricRebarsRestrainShrinkageAccordingToClosedForm()
        {
            var section = Section(); var checker = Checker(section);
            double ec = section.ConcreteMaterial.ElasticModulusTension, es = 200000, a = section.GetRebars().Sum(b => b.Area);
            double e = ec * (400 * 600 - a) * -.0002 / (ec * (400 * 600 - a) + es * a);
            var state = Last(ConcretePhaseAnalysis.Calculate(checker, new[] { ConcretePhase.Shrinkage("shrink", -.0002) }));
            Assert.AreEqual(e, state.TotalStrain.Epsilon, 1e-11);
            Assert.AreEqual(0, state.TotalStrain.ChiX, 1e-13); Assert.AreEqual(0, state.TotalStrain.ChiY, 1e-13);
            foreach (var p in state.Points)
                Assert.AreEqual(p.Kind == ConcretePhasePointKind.Rebar ? es * e : ec * (e + .0002), p.Stress, 1e-6);
            var k = state.Tangent;
            Assert.AreEqual(ec * (400 * 600 - a) + es * a, k[0, 0], 1);
        }

        [TestMethod]
        public void AsymmetricRebarsProduceSelfEquilibratedShrinkageCurvature()
        {
            var section = Section(true, true); var checker = Checker(section);
            double ec = section.ConcreteMaterial.ElasticModulusTension, es = 200000, area = 300 * 500, inertia = 300 * Math.Pow(500, 3) / 12;
            double a = section.GetRebars().Sum(b => b.Area), y = -200, sh = -.0002;
            double k00 = ec * area + (es - ec) * a, k01 = (es - ec) * a * y, k11 = ec * inertia + (es - ec) * a * y * y;
            double n = ec * (area - a) * sh, first = -ec * a * y * sh, det = k00 * k11 - k01 * k01;
            var state = Last(ConcretePhaseAnalysis.Calculate(checker, new[] { ConcretePhase.Shrinkage("shrink", sh) }));
            // The integration reference is the concrete centroid, (0,0).
            Assert.AreEqual((n * k11 - first * k01) / det, state.TotalStrain.ToStrainPlane().GetStrain(0, 0), 1e-11);
            Assert.AreEqual((first * k00 - n * k01) / det, state.TotalStrain.ChiY, 1e-13);
            Assert.IsTrue(Math.Abs(state.TotalStrain.ChiY) > 1e-9);
        }

        [TestMethod]
        public void NonlinearBiaxialShrinkageMatchesIndependentPolynomialQuadrature()
        {
            var section = Section(); var checker = Checker(section, false);
            const double e0 = -.0009, gx = 7e-7, gy = -8e-7, sh = -.0002;
            double n = 0, m1 = 0, m2 = 0;
            Func<double, double> concrete = e => { double r = e / -.002; return -35 * .85 / 1.5 * r * (2 - r); };
            Action<double, double, double> add = (x, y, f) => { n += f; m1 -= y * f; m2 += x * f; };
            double[] nodes = { -Math.Sqrt(3.0 / 5), 0, Math.Sqrt(3.0 / 5) }, weights = { 5.0 / 9, 8.0 / 9, 5.0 / 9 };
            for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++)
            {
                double x = nodes[i] * 200, y = nodes[j] * 300;
                add(x, y, weights[i] * weights[j] * 200 * 300 * concrete(e0 + gx * x + gy * y - sh));
            }
            foreach (var b in section.GetRebars())
            {
                double e = e0 + gx * b.Position.X + gy * b.Position.Y;
                add(b.Position.X, b.Position.Y, b.Area * (200000 * e - concrete(e - sh)));
            }
            var state = Last(ConcretePhaseAnalysis.Calculate(checker, new[] { Load(checker, n, m1, m2), ConcretePhase.Shrinkage("shrink", sh) }));
            Assert.AreEqual(e0, state.TotalStrain.ToStrainPlane().GetStrain(0, 0), 2e-11);
            Assert.AreEqual(gx, state.TotalStrain.ChiX, 1e-13); Assert.AreEqual(gy, state.TotalStrain.ChiY, 1e-13);
            Assert.IsNotNull(state.ConcreteNeutralAxis);
        }

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void SplitPhasesAndLoadUnloadHaveNoIrreversibleMemory(bool linear)
        {
            var checker = Checker(Section(), linear);
            var split = ConcretePhaseAnalysis.Calculate(checker, new[] { Load(checker, -400000, 5e6, -3e6, 4),
                ConcretePhase.ShrinkageMicrostrain("s1", -100), Load(checker, -400000, 5e6, -3e6), ConcretePhase.ShrinkageMicrostrain("s2", -100) });
            var one = ConcretePhaseAnalysis.Calculate(checker, new[] { Load(checker, -800000, 10e6, -6e6), ConcretePhase.ShrinkageMicrostrain("s", -200) });
            var a = Last(split); var b = Last(one);
            Assert.AreEqual(b.TotalStrain.Epsilon, a.TotalStrain.Epsilon, 2e-10);
            Assert.AreEqual(b.TotalStrain.ChiX, a.TotalStrain.ChiX, 2e-12);
            Assert.AreEqual(b.TotalStrain.ChiY, a.TotalStrain.ChiY, 2e-12);
            var unloaded = Last(ConcretePhaseAnalysis.Calculate(checker, new[] { Load(checker, -600000), Load(checker, 600000) }));
            Assert.AreEqual(0, unloaded.TotalStrain.Epsilon, 1e-10);
        }

        [TestMethod]
        public void RotatedAndTranslatedForceAxesPreservePhysicalStrainAndResiduals()
        {
            var section = Section(); var local = Checker(section);
            double angle = .47;
            var axes = new CoordinateSystem(new Point3d(37, -61, 0), new Vector3d(-Math.Cos(angle), -Math.Sin(angle), 0), new Vector3d(Math.Sin(angle), -Math.Cos(angle), 0));
            var rotated = Checker(section, true, axes);
            var phase = Load(local, -700000, 25e6, -12e6);
            var a = Last(ConcretePhaseAnalysis.Calculate(local, new[] { phase }));
            var b = Last(ConcretePhaseAnalysis.Calculate(rotated, new[] { phase }));
            Assert.AreEqual(a.TotalStrain.ToStrainPlane().GetStrain(0, 0), b.TotalStrain.ToStrainPlane().GetStrain(0, 0), 1e-11);
            Assert.AreEqual(a.TotalStrain.ChiX, b.TotalStrain.ChiX, 1e-13);
            Assert.AreEqual(a.TotalStrain.ChiY, b.TotalStrain.ChiY, 1e-13);
        }

        [TestMethod]
        public void ImpossibleLoadStopsAtLastAcceptedSubstepAndSkipsLaterPhases()
        {
            var checker = Checker(Section(), false);
            var result = ConcretePhaseAnalysis.Calculate(checker, new[] { Load(checker, -500000), Load(checker, -1e9), ConcretePhase.Shrinkage("never", -.0002) },
                new ConcretePhaseOptions(maximumSubdivisions: 5));
            Assert.IsFalse(result.Completed); Assert.AreEqual(2, result.Phases.Count);
            var last = result.Phases.Last(); Assert.IsFalse(last.Completed); Assert.IsTrue(last.ReachedFraction < 1);
            Assert.IsTrue(Math.Abs(last.State.ForceResidual) < .1);
            Assert.IsTrue(last.State.Points.Where(p => p.Kind == ConcretePhasePointKind.ConcreteVertex).All(p => p.MechanicalStrain >= -.0035));
            Assert.IsTrue(last.State.AppliedForces.N > -1e9);
        }

        [TestMethod]
        public void InitialFixedPrestressEquilibratesBeforeTheFirstPhase()
        {
            var section = Section(false);
            var steel = new SteelMaterial("bar", 200000, 450, 450, .075, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Rebar);
            section.AddRebars(new[] { new ReinforcedConcreteRebar(new RebarSectionCircular(20, steel), Point2d.Origin, 100) });
            var result = ConcretePhaseAnalysis.Calculate(Checker(section), new ConcretePhase[0]);
            Assert.IsTrue(result.Completed); Assert.AreEqual(0, result.Phases.Count);
            double a = Math.PI * 100, ec = section.ConcreteMaterial.ElasticModulusCompression;
            double expected = -a * 100 / (ec * (240000 - a) + 200000 * a);
            Assert.AreEqual(expected, result.InitialState.TotalStrain.Epsilon, 1e-11);
            Assert.AreEqual(-.0005, result.InitialState.Points.Single(p => p.Kind == ConcretePhasePointKind.Rebar).FreeStrain, 1e-12);
            Assert.IsTrue(Math.Abs(result.InitialState.ForceResidual) < .001);
        }

        [TestMethod]
        public void EmbeddedSteelRestrainsShrinkageWithNetConcreteArea()
        {
            var section = Section(false);
            var steel = new SteelMaterial("profile", 210000, 355, 355, .1, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Structural);
            var shape = new SectionH(300, 10, 200, 20, 200, 20, "H");
            section.AddSteelSection(new SteelSectionPosition(new SteelSection(shape, steel), Point2d.Origin, 0, Point2d.Origin, InsertionPointType.Centroid) { IsInsideConcrete = true });
            var state = Last(ConcretePhaseAnalysis.Calculate(Checker(section), new[] { ConcretePhase.Shrinkage("shrink", -.0002) }));
            const double a = 10600;
            double ec = section.ConcreteMaterial.ElasticModulusTension;
            double expected = ec * (240000 - a) * -.0002 / (ec * (240000 - a) + 210000 * a);
            Assert.AreEqual(expected, state.TotalStrain.ToStrainPlane().GetStrain(0, 0), 1e-11);
            Assert.IsTrue(state.Points.Any(p => p.Kind == ConcretePhasePointKind.StructuralSteel));
        }

        [TestMethod]
        public void ContinuousTensionStiffeningSupportsSmallShrinkage()
        {
            var checker = Checker(Section(), false);
            checker.SectionCheckerOptions.StressAnalysisConcreteTension = ConcreteMaterial.TensionResponseDiagrams.TensionStiffening;
            var state = Last(ConcretePhaseAnalysis.Calculate(checker, new[] { ConcretePhase.ShrinkageMicrostrain("shrink", -20) }));
            Assert.IsTrue(state.Points.Where(p => p.Kind == ConcretePhasePointKind.ConcreteVertex).All(p => p.Stress > 0));
            Assert.IsTrue(state.Points.Where(p => p.Kind == ConcretePhasePointKind.Rebar).All(p => p.Stress < 0));
        }

        [TestMethod]
        public void AdaptiveSubstepsRecoverAChallengingNonlinearIncrement()
        {
            var checker = Checker(Section(), false);
            var result = ConcretePhaseAnalysis.Calculate(checker, new[] { Load(checker, -3500000, 30e6, -10e6) },
                new ConcretePhaseOptions(maximumIterations: 3, maximumSubdivisions: 8));
            Last(result);
            Assert.IsTrue(result.Phases[0].Attempts > result.Phases[0].AcceptedSubsteps);
            Assert.IsTrue(result.Phases[0].AcceptedSubsteps > 1);
        }

        [TestMethod]
        public void ZeroTensionAndPlasticEnvelopeStillUnloadWithoutMemory()
        {
            var checker = Checker(Section(), false);
            // Bars exceed design yield and concrete approaches its plateau; unloading follows the same envelopes.
            double a = checker.SectionSolver.ConcreteSection.GetRebars().Sum(b => b.Area);
            double n = -35 * .85 / 1.5 * (240000 - a) - 450 / 1.15 * a;
            var result = ConcretePhaseAnalysis.Calculate(checker, new[] { Load(checker, n), Load(checker, -n) });
            var end = Last(result);
            Assert.IsTrue(result.Phases[0].State.TotalStrain.Epsilon < -450 / 1.15 / 200000);
            Assert.AreEqual(0, end.TotalStrain.Epsilon, 2e-10);
        }

        [TestMethod]
        public void SmallUnloadingCanLeaveAFlatPlateau()
        {
            var checker = Checker(Section(), false);
            double a = checker.SectionSolver.ConcreteSection.GetRebars().Sum(b => b.Area);
            double n = -35 * .85 / 1.5 * (240000 - a) - 450 / 1.15 * a;
            var result = ConcretePhaseAnalysis.Calculate(checker, new[] { Load(checker, n),
                ConcretePhase.Shrinkage("positive free expansion", .001), Load(checker, -n * .01) });
            var end = Last(result);
            Assert.IsTrue(result.Phases[1].State.ConcreteMechanicalStrain.Epsilon < -.0025);
            Assert.IsTrue(end.TotalStrain.Epsilon > result.Phases[1].State.TotalStrain.Epsilon);
            Assert.AreEqual(.99 * n, end.AppliedForces.N, 1e-7);
        }

        [TestMethod]
        public async Task AsyncUsesSameEngineAndInputResultsAreDetached()
        {
            var checker = Checker(Section());
            var force = new ResultBeamForces(-500000, 0, 0, 0, 0, 0, checker.SectionCheckerOptions.ForceReferenceCoordinateSystem);
            var phase = ConcretePhase.Load("load", force);
            force.N = -1e9;
            var sync = Last(ConcretePhaseAnalysis.Calculate(checker, new[] { phase }));
            var asyncResult = Last(await ConcretePhaseAnalysis.CalculateAsync(checker, new[] { phase }));
            Assert.AreEqual(-500000, sync.AppliedForces.N);
            Assert.AreEqual(sync.TotalStrain.Epsilon, asyncResult.TotalStrain.Epsilon);
            var returned = sync.AppliedForces; returned.N = 123;
            Assert.AreEqual(-500000, sync.AppliedForces.N);
            var tangent = sync.Tangent; tangent[0, 0] = 0; Assert.AreNotEqual(0, sync.Tangent[0, 0]);
        }

        [TestMethod]
        public void InvalidInputsUnsupportedLawsAndCancellationAreExplicit()
        {
            var checker = Checker(Section(), false);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ConcretePhase.Shrinkage("bad", double.NaN));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ConcretePhaseOptions(forceTolerance: 0));
            Assert.ThrowsException<ArgumentException>(() => ConcretePhase.Load("shear", new ResultBeamForces(0, 1, 0, 0, 0, 0, checker.SectionCheckerOptions.ForceReferenceCoordinateSystem)));
            Assert.ThrowsException<OperationCanceledException>(() => ConcretePhaseAnalysis.Calculate(checker, new ConcretePhase[0], cancellationToken: new CancellationToken(true)));
            checker.SectionCheckerOptions.PsiCoefficientRebar = 1;
            Assert.ThrowsException<NotSupportedException>(() => ConcretePhaseAnalysis.Calculate(checker, new ConcretePhase[0]));
            checker.SectionCheckerOptions.PsiCoefficientRebar = 0;
            checker.SectionCheckerOptions.StressAnalysisConcreteTension = ConcreteMaterial.TensionResponseDiagrams.TensionChord;
            Assert.ThrowsException<NotSupportedException>(() => ConcretePhaseAnalysis.Calculate(checker, new ConcretePhase[0]));
            var blockSection = DomainPointContractTests.BuildSection(DomainPointContractTests.Sections[1], ConcreteMaterial.CompressionStressStrainDiagrams.StressBlock);
            Assert.ThrowsException<NotSupportedException>(() => ConcretePhaseAnalysis.Calculate(Checker(blockSection, false), new ConcretePhase[0]));
        }
    }
}
