using GPC.Checkers.Concrete.Analysis;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Response;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace ConcreteTests
{
    /// <summary>
    /// Opt-in tension stiffening of the nonlinear stress analyses (user request 16/10/2026: a descending post-cracking branch for all the concretes,
    /// for the convergence of the nonlinear analyses; never in the resistances). SectionOptions.StressAnalysisConcreteTension = TensionStiffening:
    /// in tension the law of Model ConcreteTensionStiffening (Ecm up to fctm, then fctm / (1 + √(500 ε)), Collins and Mitchell 1991), in compression
    /// the design diagram of the solver. Section 300 × 500 C25/30 4Ø16 at (±104; ±204), B450C, NTC 2018 (<see cref="UltimateSections.Symmetric"/>).
    /// </summary>
    [TestClass]
    public class StressAnalysisTensionStiffeningTests
    {
        private static SectionCheckerModelCode2010 Checker(ReinforcedConcreteSection section, bool considerTensileConcrete,
            ConcreteMaterial.TensionResponseDiagrams tension, SectionSolver.FailureAnalysisTypes criterion = SectionSolver.FailureAnalysisTypes.ConstantN)
        {
            var standard = new StandardNTC2018Concrete { AlphaCC = 0.85, GammaC = 1.5, GammaS = 1.15 };
            var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(DomainPointContractTests.LocalAxes(section), criterion,
                SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, considerTensileConcrete, 32)
            { StressAnalysisConcreteTension = tension };
            var checker = new SectionCheckerModelCode2010(new SectionCheckerAttribute(section, null, null), options, standard, considerTensileConcrete);
            checker.SetDomainPointStrategy(SectionSolver.DomainPointStrategyTypes.Iterative);
            return checker;
        }

        private const ConcreteMaterial.TensionResponseDiagrams Stiffening = ConcreteMaterial.TensionResponseDiagrams.TensionStiffening;
        private const ConcreteMaterial.TensionResponseDiagrams Default = ConcreteMaterial.TensionResponseDiagrams.Default;

        /// <summary>The largest adimensional residual of the equilibrium of a stress analysis (the measure of the solver)</summary>
        private static double Residual(SectionCheckerModelCode2010 checker, ResultBeamForces force, StrainPlane plane)
        {
            var solver = checker.SectionSolver;
            var target = solver.ConvertToAdimensionalForces(force.ConvertToForceTuple(solver.SolverAxes));
            var resultant = solver.ConvertToAdimensionalForces(solver.CalculateForceResultantForTension(plane));
            return Math.Max(Math.Abs(resultant.N - target.N), Math.Max(Math.Abs(resultant.Mx - target.Mx), Math.Abs(resultant.My - target.My)));
        }

        [TestMethod]
        public void StressAnalysisFollowsTheIndependentFibreModel()
        {
            // Independent exact stress primitives: docs/analytic-parabola-20261023/independent-tension-stiffening.py
            // Ecm = 31475.81, fctm = 2.564964 MPa, εcr = 8.149e-5; analytic design parabola-rectangle (fcd = 14.1667 MPa);
            // B450C fyd = 391.30; the bars subtract the concrete they displace. (N kN, M kNm, χ 1/mm, ε at the tensile and compressed edges, ε and σ of
            // the tensile bars, σc at the edges)
            var section = UltimateSections.Symmetric();
            var local = DomainPointContractTests.LocalAxes(section);
            var cases = new[]
            {
                // uncracked (εb < εcr): before, with the concrete in tension neglected, σs = 119.44 MPa
                (N: 0.0, M: 20.0, Chi: 2.888904403379341e-07, EpsB: 5.849948312413992e-05, EpsT: -8.594573704482714e-05, EpsBar: 4.521052286859495e-05, SigmaBar: 9.042104573718989, SigmaB: 1.841318394201725, SigmaT: -1.191403486223585),
                // cracked, descending branch at the tensile edge: neglected tension σs = 269.21 MPa
                (N: 0.0, M: 45.0, Chi: 9.462497084570421e-07, EpsB: 0.0002301574071399132, EpsT: -0.0002429674470886078, EpsBar: 0.0001866299205508893, SigmaBar: 37.32598411017786, SigmaB: 1.915249128918305, SigmaT: -3.232962986700935),
                (N: -300.0, M: 60.0, Chi: 1.190858735651542e-06, EpsB: 0.0001443955531058104, EpsT: -0.0004510338147199603, EpsBar: 8.961605126583952e-05, SigmaBar: 17.92321025316791, SigmaB: 2.02173178731628, SigmaT: -5.669159138875631),
            };
            var law = section.ConcreteMaterial.GetTensionStiffening();
            foreach (var c in cases)
            {
                var checker = Checker(section, false, Stiffening);
                var force = DomainPointContractTests.Force(local, c.N, c.M, 0);
                var result = checker.GetTensionAnalysisResult(force);
                string id = $"N = {c.N} kN, M = {c.M} kNm";
                Assert.IsNotNull(result?.StrainPlane, id);
                var plane = result.StrainPlane;
                Assert.IsTrue(Residual(checker, force, plane) <= 1e-5, id + ": equilibrium");
                var vertices = section.ConcreteShape.GetPoints2d();
                Point2d tensile = vertices.OrderBy(v => plane.GetStrain(v)).Last(), compressed = vertices.OrderBy(v => plane.GetStrain(v)).First();
                var bar = section.Rebars.OrderBy(b => plane.GetStrain(b.Position)).Last();
                Assert.AreEqual(c.Chi, Math.Sqrt(plane.ChiX * plane.ChiX + plane.ChiY * plane.ChiY), 3e-3 * c.Chi, id + ": χ");
                Assert.AreEqual(c.EpsB, plane.GetStrain(tensile), 3e-3 * Math.Abs(c.EpsB), id + ": ε tensile edge");
                Assert.AreEqual(c.EpsT, plane.GetStrain(compressed), 3e-3 * Math.Abs(c.EpsT), id + ": ε compressed edge");
                Assert.AreEqual(c.EpsBar, plane.GetStrain(bar.Position), 3e-3 * Math.Abs(c.EpsBar), id + ": ε bar");
                Assert.AreEqual(c.SigmaBar, result.GetRebarTension(bar), 3e-3 * c.SigmaBar, id + ": σ bar");
                Assert.AreEqual(c.SigmaB, result.GetConcreteTension(tensile), 5e-3 * c.SigmaB, id + ": σc tensile edge");
                Assert.AreEqual(c.SigmaT, result.GetConcreteTension(compressed), 5e-3 * Math.Abs(c.SigmaT), id + ": σc compressed edge");
                // the results use the law of the analysis: σc of the vertices and of the summary
                Assert.AreEqual(law.GetStress(plane.GetStrain(tensile)), result.GetConcreteTension(tensile), 0.0, id);
                Assert.AreEqual(law.GetStress(plane.GetStrain(tensile)), result.GetConcreteVerticesTension().Single(v => v.point.X == tensile.X && v.point.Y == tensile.Y).tension, 0.0, id);
                // On the descending tensile branch the maximum vertex stress need not be at the maximum strain.
                Assert.AreEqual(vertices.Max(v => result.GetConcreteTension(v)), result.CalculateStrainPlaneResult(false).SigmaCMax, 0.0, id + ": summary");
            }
        }

        [TestMethod]
        public void NonlinearSummarySeparatesStressExtremaFromStrainExtrema()
        {
            var section = UltimateSections.Symmetric();
            var checker = Checker(section, false, Stiffening);
            var law = section.ConcreteMaterial.GetTensionStiffening();
            var force = DomainPointContractTests.Force(DomainPointContractTests.LocalAxes(section), 0, 0, 0);
            var vertices = section.ConcreteShape.GetPoints2d();
            foreach (double direction in new[] { -1.0, 1.0 })
            {
                // All vertices are beyond cracking, on the descending branch. Reflection changes their traversal order.
                var plane = new StrainPlane(direction * 2e-6, 1e-6, Point2d.Origin, .0007);
                var summary = plane.CalculateStrainPlaneResult(section, force, checker.SectionSolver, new StandardNTC2018Concrete(), false, 0, 0, null, new GPC.Checkers.Concrete.SectionSolvers.StressAnalysisTension(law));
                Assert.AreEqual(vertices.Max(v => law.GetStress(plane.GetStrain(v))), summary.SigmaCMax, 0);
                Assert.AreEqual(vertices.Min(v => law.GetStress(plane.GetStrain(v))), summary.SigmaCMin, 0);
                Assert.AreEqual(vertices.Max(v => plane.GetStrain(v)), summary.EpsilonCMax, 0);
                Assert.AreEqual(vertices.Min(v => plane.GetStrain(v)), summary.EpsilonCMin, 0);
            }
        }

        [TestMethod]
        public void OptionNeverReachesTheResistances()
        {
            // The failure domain, the resistance at an assigned N, the limit point of the moment-curvature curve and the linear stress analysis are
            // the same, bit for bit, with and without the option (with the concrete in tension of the domain neglected and considered)
            var section = UltimateSections.Symmetric();
            var local = DomainPointContractTests.LocalAxes(section);
            foreach (bool tensile in new[] { false, true })
            {
                var plain = Checker(section, tensile, Default);
                var stiffened = Checker(section, tensile, Stiffening);
                var a = plain.GetFailureDomainResult().Domain.DomainPoints.SelectMany(p => p).ToArray();
                var b = stiffened.GetFailureDomainResult().Domain.DomainPoints.SelectMany(p => p).ToArray();
                Assert.AreEqual(a.Length, b.Length);
                for (int i = 0; i < a.Length; i++)
                {
                    Assert.AreEqual(a[i].NRd, b[i].NRd, 0.0); Assert.AreEqual(a[i].MxRd, b[i].MxRd, 0.0); Assert.AreEqual(a[i].MyRd, b[i].MyRd, 0.0);
                }
                foreach (var (n, m) in new[] { (0.0, 60.0), (-300.0, 60.0), (100.0, 20.0), (-1000.0, 150.0) })
                {
                    var force = DomainPointContractTests.Force(local, n, m, 0);
                    var ra = new LegacySectionCalculation(plain).SolveResistance(new SectionAnalysisInput(force));
                    var rb = new LegacySectionCalculation(stiffened).SolveResistance(new SectionAnalysisInput(force));
                    Assert.AreEqual(ra.Diagnostics.Status, rb.Diagnostics.Status);
                    Assert.AreEqual(ra.N, rb.N); Assert.AreEqual(ra.M1, rb.M1); Assert.AreEqual(ra.M2, rb.M2); Assert.AreEqual(ra.Utilization, rb.Utilization);
                    Assert.AreEqual(plain.SectionSolver.CalculateDomainPoint(force).NRd, stiffened.SectionSolver.CalculateDomainPoint(force).NRd, 0.0);
                    var la = plain.SectionSolver.GetLinearStressAnalysisResult(force, 0.5, 0).StrainPlane;
                    var lb = stiffened.SectionSolver.GetLinearStressAnalysisResult(force, 0.5, 0).StrainPlane;
                    Assert.AreEqual(la.ChiX, lb.ChiX, 0.0); Assert.AreEqual(la.ChiY, lb.ChiY, 0.0); Assert.AreEqual(la.StrainReferencePoint, lb.StrainReferencePoint, 0.0);
                }
                var request = new MomentCurvatureRequest(-300e3, 0, 20, 1, true, 30000);
                double yieldStrain = 450 / 1.15 / 200000;
                var ca = MomentCurvatureAnalysis.Calculate(request, plain, section, local, yieldStrain);
                var cb = MomentCurvatureAnalysis.Calculate(request, stiffened, section, local, yieldStrain);
                Assert.AreEqual(ca.LimitMoment, cb.LimitMoment, 0.0); Assert.AreEqual(ca.UltimateCurvature.Value, cb.UltimateCurvature.Value, 0.0);
                Assert.AreEqual(ca.LimitAxialForce, cb.LimitAxialForce, 0.0);
                Assert.AreEqual(ca.Points.Last().Curvature, cb.Points.Last().Curvature, 0.0);
                Assert.IsTrue(cb.Status.Contains("tension stiffening") && !ca.Status.Contains("tension stiffening"));
            }
        }

        [TestMethod]
        public void TensionStiffeningConvergesForHistoricalMaterialFailures()
        {
            // These forces failed with the historical chords and abrupt tensile cutoff. Keep that characterization
            // explicit; the analytic compression law may also improve convergence with the same tensile diagram.
            var section = UltimateSections.Symmetric();
            var local = DomainPointContractTests.LocalAxes(section);
            var historicalSection = UltimateSections.Symmetric();
#pragma warning disable CS0618
            ((ConcreteMaterialEuropeanCommon)historicalSection.ConcreteMaterial).ParabolaRectangleMethod = ConcreteParabolaRectangleMethod.TabulatedLegacy;
#pragma warning restore CS0618
            var material = Checker(historicalSection, true, Default);
            var analytic = Checker(section, true, Default);
            var stiffened = Checker(section, false, Stiffening);
            foreach (var (n, m) in new[] { (100.0, 18.65), (100.0, 18.70), (100.0, 18.75), (100.0, 18.80), (100.0, 18.85), (-300.0, 42.00), (-300.0, 42.05), (-300.0, 42.10) })
            {
                var force = DomainPointContractTests.Force(local, n, m, 0);
                Assert.IsNull(material.GetTensionAnalysisResult(force).StrainPlane, $"material diagram, N = {n}, M = {m}: historical characterization");
                var improved = analytic.GetTensionAnalysisResult(force);
                if (improved.StrainPlane != null)
                    Assert.IsTrue(Residual(analytic, force, improved.StrainPlane) <= 1e-5, "analytic material equilibrium");
                var result = stiffened.GetTensionAnalysisResult(force);
                Assert.IsNotNull(result.StrainPlane, $"tension stiffening, N = {n}, M = {m}");
                Assert.IsTrue(Residual(stiffened, force, result.StrainPlane) <= 1e-5, $"equilibrium, N = {n}, M = {m}");
            }
        }

        [TestMethod]
        public void TensionStiffeningConvergesInUniaxialAndBiaxialSweeps()
        {
            // 150 moments from 0 to MRd (domain at constant N) at N = +150, 0, -500 kN, θ = 0° and 30°: no failure (probe of 16/10/2026, 4 sections,
            // 12000 analyses: none with the option; with the material diagram 172 of 3600 biaxial, 10 of 4800 and 8 of 3600 uniaxial)
            var section = UltimateSections.Symmetric();
            var local = DomainPointContractTests.LocalAxes(section);
            var checker = Checker(section, false, Stiffening);
            foreach (double theta in new[] { 0.0, 30.0 })
                foreach (double n in new[] { 150e3, 0, -500e3 })
                {
                    double mrd = MomentCurvatureAnalysis.Calculate(new MomentCurvatureRequest(n, theta, 10, 0.1, false, 30000, 0), checker, section, local, 1).LimitMoment;
                    double c = Math.Cos(theta * Math.PI / 180), s = Math.Sin(theta * Math.PI / 180);
                    for (int i = 1; i <= 150; i++)
                    {
                        double m = mrd * i / 151.0;
                        var force = new ResultBeamForces(n, 0, 0, 0, m * c, m * s, local);
                        var result = checker.GetTensionAnalysisResult(force);
                        Assert.IsNotNull(result.StrainPlane, $"θ = {theta}, N = {n / 1000} kN, M = {m / 1e6:F3} kNm");
                        // within the tolerance, or the jump of the law at cracking between Gauss points (declared in the log)
                        Assert.IsTrue(Residual(checker, force, result.StrainPlane) <= 5e-4, $"θ = {theta}, N = {n / 1000} kN, M = {m / 1e6:F3} kNm");
                    }
                }
        }

        [TestMethod]
        public void MomentCurvatureWithTensionStiffening()
        {
            // The curve completes; the responses are those of the stress analyses with the option, the limit point that of the domain. Before
            // cracking the section is stiffer than with the tension neglected (uncracked: χ of the first point of the independent model of
            // StressAnalysisFollowsTheIndependentFibreModel scales linearly; here only the ordering is checked)
            var section = UltimateSections.Symmetric();
            var local = DomainPointContractTests.LocalAxes(section);
            double yieldStrain = 450 / 1.15 / 200000;
            var request = new MomentCurvatureRequest(0, 0, 30, 1, true, 30000);
            var neglected = MomentCurvatureAnalysis.Calculate(request, Checker(section, false, Default), section, local, yieldStrain);
            var stiffened = MomentCurvatureAnalysis.Calculate(request, Checker(section, false, Stiffening), section, local, yieldStrain);
            Assert.IsNull(stiffened.InterruptedAtStep, stiffened.Status);
            Assert.AreEqual(neglected.LimitMoment, stiffened.LimitMoment, 0.0);
            var checker = Checker(section, false, Stiffening);
            int compared = 0;
            for (int i = 1; i < stiffened.Points.Count - 1; i++)
            {
                var p = stiffened.Points[i];
                var same = neglected.Points.FirstOrDefault(q => q.Moment == p.Moment);
                if (same == null) continue; // a point of the refinement of the first yield
                compared++;
                Assert.IsTrue(p.Curvature < same.Curvature, "stiffer at M = " + p.Moment);
                var plane = checker.GetTensionAnalysisResult(new ResultBeamForces(0, 0, 0, 0, p.Mx, p.My, local)).StrainPlane;
                Assert.AreEqual(Math.Sqrt(plane.ChiX * plane.ChiX + plane.ChiY * plane.ChiY), p.Curvature, 0.0, "the response of the stress analysis");
            }
            Assert.IsTrue(compared >= 28);
        }

        [TestMethod]
        public void OptionIsSerializedOnlyWhenAssignedAndValidated()
        {
            var section = UltimateSections.Symmetric();
            var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(DomainPointContractTests.LocalAxes(section),
                SectionSolver.FailureAnalysisTypes.ConstantN, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, false, 32);
            Assert.AreEqual(Default, options.StressAnalysisConcreteTension);
            var info = new SerializationInfo(options.GetType(), new FormatterConverter());
            options.GetObjectData(info, new StreamingContext());
            bool entry = false;
            foreach (SerializationEntry e in info) entry |= e.Name == "StressAnalysisConcreteTension";
            Assert.IsFalse(entry, "the data of the default options are those of before");
            int hash = options.GetHashCode();
            options.StressAnalysisConcreteTension = Stiffening;
            Assert.AreNotEqual(hash, options.GetHashCode());
            var copy = (SectionCheckerModelCode2010.SectionOptionsModelCode2010)RoundTrip(options);
            Assert.AreEqual(Stiffening, copy.StressAnalysisConcreteTension);
            Assert.IsTrue(copy.Equals(options));
            options.StressAnalysisConcreteTension = Default;
            Assert.AreEqual(hash, options.GetHashCode());
            Assert.IsFalse(copy.Equals(options));
            Assert.AreEqual(Default, ((SectionCheckerModelCode2010.SectionOptionsModelCode2010)RoundTrip(options)).StressAnalysisConcreteTension);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => options.StressAnalysisConcreteTension = (ConcreteMaterial.TensionResponseDiagrams)7);
        }

        private static object RoundTrip(object value)
        {
            using (var stream = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(stream, value);
                stream.Position = 0;
                return formatter.Deserialize(stream);
            }
        }

        [TestMethod]
        public void Aci318ConcreteUsesItsOwnTensionStiffening()
        {
            // ACI 318, f'c 30: Ec = 57000 √f'c psi = 25923.58 MPa, fcr = 0.33 √30 = 1.80748 MPa (Collins and Mitchell 1991): the solver of ACI 318 uses
            // the same law of Model; at ε = 1e-3: 1.80748 / (1 + √0.5) = 1.058800 MPa (independent Python ref_ts.py)
            var concrete = new ConcreteMaterialACI318("30", 30, ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
            var section = new ReinforcedConcreteSection(UltimateSections.Symmetric().ConcreteShape, concrete);
            var options = new SectionCheckerACI318.SectionOptionsStandardACI318(DomainPointContractTests.LocalAxes(section)) { StressAnalysisConcreteTension = Stiffening };
            var solver = new SectionSolverACI318(section, options, new StandardACI318p14(), false, section.Centroid);
            Assert.AreEqual(1.058800, solver.CalculateStressAnalysisSigmaC(1e-3), 1e-6);
            Assert.AreEqual(solver.CalculateSigmaC(-1e-3), solver.CalculateStressAnalysisSigmaC(-1e-3), 0.0, "compression: the design diagram of the solver");
            options.StressAnalysisConcreteTension = Default;
            Assert.AreEqual(0, solver.CalculateStressAnalysisSigmaC(1e-3), 0.0, "default: tension neglected");
        }
    }
}
