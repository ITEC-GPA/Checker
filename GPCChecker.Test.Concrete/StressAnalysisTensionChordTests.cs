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
using System.Runtime.Serialization.Formatters.Binary;

namespace ConcreteTests
{
    /// <summary>
    /// Tension chord of the nonlinear stress analyses (BUG-TS-2 option 1, decision of the ANTHEA user of 26-27/10/2026, β = 0.4):
    /// SectionOptions.StressAnalysisConcreteTension = TensionChord. Concrete in tension Ecm ε up to fctm and zero after cracking; the ordinary bars of
    /// the effective tension area (hc,eff = min[2.5 (h − d); (h − x)/3; h/2]) at the stress at the crack, the steel law at their equivalent strain.
    /// Section 300 × 500 C25/30 4Ø16 at (±104; ±204), B450C, NTC 2018 (<see cref="UltimateSections.Symmetric"/>).
    /// </summary>
    [TestClass]
    public class StressAnalysisTensionChordTests
    {
        private const ConcreteMaterial.TensionResponseDiagrams Chord = ConcreteMaterial.TensionResponseDiagrams.TensionChord;
        private const ConcreteMaterial.TensionResponseDiagrams Default = ConcreteMaterial.TensionResponseDiagrams.Default;
        private const ConcreteMaterial.TensionResponseDiagrams Stiffening = ConcreteMaterial.TensionResponseDiagrams.TensionStiffening;

        private static SectionCheckerModelCode2010 Checker(ReinforcedConcreteSection section, bool considerTensileConcrete, ConcreteMaterial.TensionResponseDiagrams tension)
        {
            var standard = new StandardNTC2018Concrete { AlphaCC = 0.85, GammaC = 1.5, GammaS = 1.15 };
            var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(DomainPointContractTests.LocalAxes(section), SectionSolver.FailureAnalysisTypes.ConstantN,
                SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, considerTensileConcrete, 32)
            { StressAnalysisConcreteTension = tension };
            var checker = new SectionCheckerModelCode2010(new SectionCheckerAttribute(section, null, null), options, standard, considerTensileConcrete);
            checker.SetDomainPointStrategy(SectionSolver.DomainPointStrategyTypes.Iterative);
            return checker;
        }

        private static double Residual(SectionCheckerModelCode2010 checker, ResultBeamForces force, StrainPlane plane)
        {
            var solver = checker.SectionSolver;
            var target = solver.ConvertToAdimensionalForces(force.ConvertToForceTuple(solver.SolverAxes));
            var resultant = solver.ConvertToAdimensionalForces(solver.CalculateForceResultantForTension(plane));
            return Math.Max(Math.Abs(resultant.N - target.N), Math.Max(Math.Abs(resultant.Mx - target.Mx), Math.Abs(resultant.My - target.My)));
        }

        [TestMethod]
        public void StressAnalysisFollowsTheIndependentTensionChord()
        {
            // Independent exact stress primitives and fixed point of the effective areas: docs/ts2-tension-chord/independent-tension-chord.py (and .json): the uncracked state
            // first, then the cracked one. Ecm = 31475.81, fctm = 2.564964 MPa, εcr = 8.149e-5; design parabola-rectangle; B450C fyd = 391.30; the bars subtract the concrete they
            // displace. (N kN, M kNm, χ 1/mm, ε at the tensile and compressed edges, ε, ρeff and σ at the crack of the tensile bars, σc at the edges)
            var section = UltimateSections.Symmetric();
            var local = DomainPointContractTests.LocalAxes(section);
            var cases = new[]
            {
                // uncracked (state I): the plane of the tension stiffening of Collins and Mitchell, bare bars (Rho 0)
                (N: 0.0, M: 20.0, Chi: 2.8889044033793417e-07, EpsB: 5.849948312413999e-05, EpsT: -8.59457370448271e-05, EpsBar: 4.5210522868595015e-05, Rho: 0.0, SigmaBar: 9.042104573719003, SigmaB: 1.8413183942017273, SigmaT: -1.1914034862235847),
                // cracked, stabilised cracking at the bars: hc,eff = (h − x)/3; with the tension neglected σs = 269.21 MPa
                (N: 0.0, M: 45.0, Chi: 2.7528549763743145e-06, EpsB: 0.0010194231106620292, EpsT: -0.00035700437752512803, EpsBar: 0.0008927917817488109, Rho: 0.011655764048101262, SigmaBar: 273.1014368739085, SigmaB: 0.0, SigmaT: -4.606169070204778),
                // cracked, crack formation
                (N: -300.0, M: 60.0, Chi: 1.7953607980834707e-06, EpsB: 0.0003666117207287208, EpsT: -0.0005310686783130146, EpsBar: 0.00028402512401688116, Rho: 0.01969269864508494, SigmaBar: 94.6750413389604, SigmaB: 0.0, SigmaT: -6.524602734757863),
                (N: 0.0, M: 55.0, Chi: 3.628840973273053e-06, EpsB: 0.0013590368887511444, EpsT: -0.0004553835978853823, EpsBar: 0.0011921102039805839, Rho: 0.011655764048101262, SigmaBar: 332.96512132026305, SigmaB: 0.0, SigmaT: -5.716817269877998),
            };
            foreach (var c in cases)
            {
                var checker = Checker(section, false, Chord);
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
                Assert.AreEqual(c.SigmaBar, result.GetRebarTension(bar), 3e-3 * c.SigmaBar, id + ": σ bar at the crack");
                Assert.AreEqual(c.SigmaB, result.GetConcreteTension(tensile), Math.Max(1e-12, 5e-3 * c.SigmaB), id + ": σc tensile edge");
                Assert.AreEqual(c.SigmaT, result.GetConcreteTension(compressed), 5e-3 * Math.Abs(c.SigmaT), id + ": σc compressed edge");
                // ρeff of the effective tension area of the solution (the fixed point of the analysis)
                int index = Array.IndexOf(section.GetRebars(), bar);
                var tension = checker.SectionSolver.GetStressAnalysisTension(plane);
                Assert.AreEqual(c.Rho > 0, tension.Cracked, id + ": state");
                var bars = tension.ChordBars;
                if (c.Rho > 0)
                {
                    Assert.IsTrue(bars.ContainsKey(index), id + ": the tensile bar is in the effective area");
                    Assert.AreEqual(c.Rho, bars[index].Ratio, 3e-3 * c.Rho, id + ": ρeff");
                    Assert.AreEqual(2, bars.Count, id + ": only the two tensile bars");
                }
                else
                    Assert.AreEqual(0, bars.Count, id + ": uncracked, bare bars");
                // the summary gives the stress at the crack of the most tensile bar
                Assert.AreEqual(result.GetRebarTension(bar), result.CalculateStrainPlaneResult(false).SigmaSMax, 1e-9, id + ": summary");
            }
        }

        [TestMethod]
        public void UniformTensionBelowCrackingIsUncracked()
        {
            // N = +200 kN, M = 0, below the cracking load of the tie fct (Ac + (αe − 1) As) = 398 kN (above the yield load 315 kN: the tie of this
            // section fails at cracking). By hand: ε = N / (Ecm (Ac − As) + Es As) = 200000 / (31475.81 · 149195.75 + 200000 · 804.25) = 4.1178e-5,
            // below εcr = 8.149e-5; the bars at the strain of the plane. (docs/ts2-tension-chord/independent-tension-chord.py, last row)
            var section = UltimateSections.Symmetric();
            var checker = Checker(section, false, Chord);
            var force = DomainPointContractTests.Force(DomainPointContractTests.LocalAxes(section), 200, 0, 0);
            var result = checker.GetTensionAnalysisResult(force);
            Assert.IsNotNull(result?.StrainPlane);
            var plane = result.StrainPlane;
            Assert.IsTrue(Residual(checker, force, plane) <= 1e-5);
            double ec = 22000 * Math.Pow(3.3, .3), steel = 4 * Math.PI * 16 * 16 / 4, strain = 200000 / (ec * (150000 - steel) + 200000 * steel);
            foreach (var bar in section.Rebars)
            {
                Assert.AreEqual(strain, plane.GetStrain(bar.Position), 1e-3 * strain, "strain");
                Assert.AreEqual(200000 * strain, result.GetRebarTension(bar), 1e-3 * 200000 * strain, "bar stress");
            }
            var tension = checker.SectionSolver.GetStressAnalysisTension(plane);
            Assert.IsFalse(tension.Cracked); Assert.AreEqual(0, tension.ChordBars.Count);
            Assert.AreEqual(ec * strain, result.GetConcreteTension(section.ConcreteShape.GetPoints2d().First()), 1e-3 * ec * strain, "uncracked concrete");
        }

        [TestMethod]
        public void TensionChordNeverReachesTheResistances()
        {
            // The failure domain, the resistance at an assigned N and the limit point of the moment-curvature curve are the same, bit for bit
            var section = UltimateSections.Symmetric();
            var local = DomainPointContractTests.LocalAxes(section);
            var plain = Checker(section, false, Default);
            var chord = Checker(section, false, Chord);
            var a = plain.GetFailureDomainResult().Domain.DomainPoints.SelectMany(p => p).ToArray();
            var b = chord.GetFailureDomainResult().Domain.DomainPoints.SelectMany(p => p).ToArray();
            Assert.AreEqual(a.Length, b.Length);
            for (int i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i].NRd, b[i].NRd, 0.0); Assert.AreEqual(a[i].MxRd, b[i].MxRd, 0.0); Assert.AreEqual(a[i].MyRd, b[i].MyRd, 0.0);
            }
            foreach (var (n, m) in new[] { (0.0, 60.0), (-300.0, 60.0), (100.0, 20.0) })
            {
                var force = DomainPointContractTests.Force(local, n, m, 0);
                var ra = new LegacySectionCalculation(plain).SolveResistance(new SectionAnalysisInput(force));
                var rb = new LegacySectionCalculation(chord).SolveResistance(new SectionAnalysisInput(force));
                Assert.AreEqual(ra.N, rb.N); Assert.AreEqual(ra.M1, rb.M1); Assert.AreEqual(ra.Utilization, rb.Utilization);
            }
        }

        [TestMethod]
        public void MomentCurvatureWithTensionChord()
        {
            // The curve completes, the limit point is that of the domain; once cracked the section is stiffer than with the tension neglected (the
            // bars carry the concrete between the cracks) and, at the same moment, never stiffer than uncracked
            var section = UltimateSections.Symmetric();
            var local = DomainPointContractTests.LocalAxes(section);
            double yieldStrain = 450 / 1.15 / 200000;
            var request = new MomentCurvatureRequest(0, 0, 30, 1, true, 30000);
            var neglected = MomentCurvatureAnalysis.Calculate(request, Checker(section, false, Default), section, local, yieldStrain);
            var chord = MomentCurvatureAnalysis.Calculate(request, Checker(section, false, Chord), section, local, yieldStrain);
            Assert.IsNull(chord.InterruptedAtStep, chord.Status);
            Assert.AreEqual(neglected.LimitMoment, chord.LimitMoment, 0.0);
            Assert.IsTrue(chord.Status.Contains("tension chord") && !neglected.Status.Contains("tension chord"));
            int compared = 0;
            for (int i = 1; i < chord.Points.Count - 1; i++)
            {
                var p = chord.Points[i];
                var same = neglected.Points.FirstOrDefault(q => q.Moment == p.Moment);
                if (same == null) continue;
                compared++;
                Assert.IsTrue(p.Curvature <= same.Curvature * (1 + 1e-9), "not softer than the bare bars at M = " + p.Moment);
            }
            Assert.IsTrue(compared >= 20, "points " + compared);
        }

        [TestMethod]
        public void OptionIsSerializedAndValidated()
        {
            var section = UltimateSections.Symmetric();
            var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(DomainPointContractTests.LocalAxes(section),
                SectionSolver.FailureAnalysisTypes.ConstantN, SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, false, 32)
            { StressAnalysisConcreteTension = Chord };
            using (var stream = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(stream, options);
                stream.Position = 0;
                var copy = (SectionCheckerModelCode2010.SectionOptionsModelCode2010)formatter.Deserialize(stream);
                Assert.AreEqual(Chord, copy.StressAnalysisConcreteTension);
                Assert.IsTrue(copy.Equals(options));
            }
            Assert.AreNotEqual(Stiffening, Chord);
        }
    }
}
