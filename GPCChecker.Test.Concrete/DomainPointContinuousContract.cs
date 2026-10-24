using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GPC.Checkers.Concrete.Analysis;
using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Model.Materials;
using GPC.Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConcreteTests
{
    /// <summary>User-approved replacement of algorithm-output equality for the 768 continuous-diagram cases. The old JSON stays intact.</summary>
    internal static class DomainPointContinuousContract
    {
        internal static void VerifyCatalogue()
        {
            int completed = 0, outsideAxial = 0, impossibleTension = 0, unqualifiedCompression = 0;
            var identities = new HashSet<string>();
            foreach (var shape in DomainPointContractTests.Sections)
            foreach (var diagram in DomainPointContractTests.Diagrams.Where(d => d != ConcreteMaterial.CompressionStressStrainDiagrams.StressBlock))
            {
                var section = DomainPointContractTests.BuildSection(shape, diagram);
                foreach (var state in DomainPointContractTests.States)
                {
                    var checker = DomainPointContractTests.BuildChecker(section, state);
                    var solver = checker.SectionSolver;
                    foreach (double nKn in shape.AxialKn)
                    foreach (var direction in DomainPointContractTests.Directions)
                    {
                        string id = shape.Name + "/" + diagram + "/" + state + "/" + nKn.ToString("R", CultureInfo.InvariantCulture) + "/" + direction.Label;
                        Assert.IsTrue(identities.Add(id), "Duplicate case " + id);
                        var demand = DomainPointContractTests.Force(DomainPointContractTests.LocalAxes(section), nKn, direction.Mx, direction.My);
                        var response = new LegacySectionCalculation(checker).SolveResistance(new SectionAnalysisInput(demand));
                        double steelArea = shape.Bars.Sum(b => Math.PI * b[2] * b[2] / 4);
                        double concreteArea = shape.Sides > 0 ? shape.Sides * Math.Pow(shape.Diameter / 2, 2) * Math.Sin(2 * Math.PI / shape.Sides) / 2 : shape.Width * shape.Height;
                        // Absolute stress bound, valid independently of the failure-domain search (N in compression is negative).
                        double maxCompression = (concreteArea - steelArea) * .85 * shape.Fck / 1.5 + steelArea * (450 / 1.15);
                        if (demand.N < -maxCompression)
                        {
                            Assert.AreEqual("R1", shape.Name, id); Assert.AreEqual(-5000000.0, demand.N, id);
                            Assert.AreEqual(CalculationStatus.Completed, response.Diagnostics.Status, id);
                            Assert.IsTrue(response.IsAxialForceOutsideDomain, id);
                            Assert.IsTrue(response.Utilization.Value > 1, id);
                            Assert.IsTrue(Math.Abs(response.N.Value) <= maxCompression + 1e-6 * maxCompression, id);
                            outsideAxial++; continue;
                        }
                        if (shape.Name == "R3" && demand.N > 0 && direction.Mx <= 0)
                        {
                            // All steel is at y=-200. C<=As*fyd-N and yc>=-250 imply
                            // Mx=200N+C(200+yc)>=200N-50(As*fyd-N)>0, excluding Mx<=0.
                            Assert.IsTrue(shape.Bars.All(b => b[1] == -200), id);
                            double lowerMoment = 200 * demand.N - 50 * (steelArea * (450 / 1.15) - demand.N);
                            Assert.IsTrue(lowerMoment > 0, id + ": independent static certificate");
                            AssertRejectedWithoutCapacity(response, id);
                            impossibleTension++; continue;
                        }
                        bool numericalUnqualifiedBranch = shape.Name == "R3" && demand.N == -2000000 && direction.Mx >= 0
                            && ((diagram == ConcreteMaterial.CompressionStressStrainDiagrams.Bilinear && state == SectionSolver.FailureDomainTypes.Elastic)
                             || (diagram == ConcreteMaterial.CompressionStressStrainDiagrams.NonLinear && state == SectionSolver.FailureDomainTypes.Plastic));
                        if (numericalUnqualifiedBranch)
                        {
                            // Six explicit unqualified branches, backed by the separate independent angular-envelope calculation.
                            // Its maximum Mx stays negative under angular refinement; it is numerical evidence, NOT an analytical impossibility theorem.
                            AssertRejectedWithoutCapacity(response, id);
                            unqualifiedCompression++; continue;
                        }
                        // No generic null/NotConverged allowance: all other 720 original inputs must produce a qualified physical result.
                        Assert.AreEqual(CalculationStatus.Completed, response.Diagnostics.Status, id + ": " + response.Diagnostics.Message);
                        Assert.IsFalse(response.IsAxialForceOutsideDomain || response.IsZeroDemand || response.IsZeroMomentDemand, id);
                        Assert.IsNotNull(response.Strain, id); Assert.IsTrue(response.Diagnostics.ResistanceConvergence.Accepted, id);
                        Assert.IsTrue(new[] { response.N.Value, response.M1.Value, response.M2.Value, response.Utilization.Value }.All(Finite), id);
                        Assert.IsTrue(response.Utilization.Value >= 0, id);
                        var plane = response.Strain.ToStrainPlane();
                        var integrated = solver.GetExternalForces(solver.CalculateForceResultantForDomain(plane), solver.SectionOption.ForceReferenceCoordinateSystem);
                        var point = new FailureDomain.FailureDomainPoint(integrated, (SectionSolver.FailureZones)Enum.Parse(typeof(SectionSolver.FailureZones), response.FailureMode), plane, 0);
                        // The discrete-domain sampling list omits some valid elastic fields (notably F3B).
                        // Validate the declared field against its actual strain-plane parameterization instead.
                        AssertFieldParameterization(solver, state, point,
                            section.ConcreteShape.Fill2d.Points.Concat(section.Rebars.Select(b => b.Position)), id);
                        Assert.IsTrue(DomainPointConvergence.Evaluate(solver, demand, point, SectionSolver.FailureAnalysisTypes.ConstantN).Accepted, id + ": returned plane constraints");
                        double tn = DomainPointAxialTolerance.Calculate(solver, demand.N);
                        double tm1 = solver.FailureAnalysisDistanceTolerance / solver.ConvertToAdimensionalForces(new ForceTuple(0, 1, 0)).Mx;
                        double tm2 = solver.FailureAnalysisDistanceTolerance / solver.ConvertToAdimensionalForces(new ForceTuple(0, 0, 1)).My;
                        Assert.AreEqual(response.N.Value, integrated.N, tn, id + ": N reintegration");
                        Assert.AreEqual(response.M1.Value, integrated.Mx, tm1, id + ": Mx reintegration");
                        Assert.AreEqual(response.M2.Value, integrated.My, tm2, id + ": My reintegration");
                        Assert.IsTrue(response.M1.Value * direction.Mx + response.M2.Value * direction.My > 0, id + ": physical direction");
                        double ey = diagram == ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle ? .002
                            : diagram == ConcreteMaterial.CompressionStressStrainDiagrams.Bilinear ? .00175 : Math.Min(2.8, .7 * Math.Pow(shape.Fck + 8, .31)) / 1000;
                        double concreteLimit = state == SectionSolver.FailureDomainTypes.Elastic ? ey : .0035;
                        const double strainRoundoff = 1e-12; // absolute strain arithmetic allowance, not a resistance tolerance
                        foreach (var v in section.ConcreteShape.Fill2d.Points)
                            Assert.IsTrue(plane.GetStrain(v) >= -concreteLimit - strainRoundoff, id + ": concrete strain bound");
                        double curvature = Math.Sqrt(plane.ChiX * plane.ChiX + plane.ChiY * plane.ChiY);
                        double steelTension = state == SectionSolver.FailureDomainTypes.Elastic ? 450 / 1.15 / 200000 : .075 * .9;
                        double steelCompression = state == SectionSolver.FailureDomainTypes.Elastic ? steelTension : .075;
                        foreach (var bar in section.Rebars)
                        {
                            double e = plane.GetStrain(bar.Position), edge = curvature * bar.RebarSection.Diameter / 2;
                            Assert.IsTrue(e + edge <= steelTension + strainRoundoff, id + ": steel tensile outer fibre");
                            Assert.IsTrue(e - edge >= -steelCompression - strainRoundoff, id + ": steel compressive outer fibre");
                        }
                        completed++;
                    }
                }
            }
            Assert.AreEqual(768, identities.Count); Assert.AreEqual(720, completed);
            Assert.AreEqual(24, outsideAxial); Assert.AreEqual(18, impossibleTension); Assert.AreEqual(6, unqualifiedCompression);
        }

        private static void AssertFieldParameterization(SectionSolver solver, SectionSolver.FailureDomainTypes state,
            FailureDomain.FailureDomainPoint point, IEnumerable<Point2d> probes, string id)
        {
            Assert.IsTrue(Enum.IsDefined(typeof(SectionSolver.FailureZones), point.FailureIndex), id + ": declared failure field");
            var plane = point.StrainPlane;
            var first = solver.BuildPlane(plane.Teta, state, point.FailureIndex, 0);
            var last = solver.BuildPlane(plane.Teta, state, point.FailureIndex, 1);
            Assert.IsNotNull(first, id); Assert.IsNotNull(last, id);
            double value = plane.Chi, lower = first.Chi, upper = last.Chi;
            Assert.IsTrue(new[] { value, lower, upper }.All(Finite), id + ": finite field curvatures");
            if (point.FailureIndex == SectionSolver.FailureZones.F3A)
            {
                // F3A interpolates compression depth: 1/chi, not chi, is affine in the field parameter.
                Assert.IsTrue(value > 0 && lower > 0 && upper > 0, id + ": F3A positive compression depth");
                value = 1 / value; lower = 1 / lower; upper = 1 / upper;
            }
            double parameter = upper == lower ? 0 : (value - lower) / (upper - lower);
            Assert.IsTrue(Finite(parameter), id + ": finite field parameter");
            // Project onto the CLOSED field. A point outside it must still match that endpoint to the
            // existing absolute strain arithmetic allowance; no new force/engineering tolerance is introduced.
            parameter = Math.Max(0, Math.Min(1, parameter));
            var rebuilt = solver.BuildPlane(plane.Teta, state, point.FailureIndex, parameter);
            Assert.IsNotNull(rebuilt, id);
            foreach (var probe in probes)
                Assert.AreEqual(plane.GetStrain(probe), rebuilt.GetStrain(probe), 1e-12, id + ": declared field reconstructs strain plane");
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static void AssertRejectedWithoutCapacity(SectionResistanceResponse response, string id)
        {
            Assert.AreEqual(CalculationStatus.NotConverged, response.Diagnostics.Status, id);
            Assert.IsNotNull(response.Diagnostics.ResistanceConvergence, id);
            Assert.IsFalse(response.Diagnostics.ResistanceConvergence.Accepted, id);
            Assert.IsFalse(string.IsNullOrWhiteSpace(response.Diagnostics.Message), id);
            Assert.IsNull(response.N, id); Assert.IsNull(response.M1, id); Assert.IsNull(response.M2, id);
            Assert.IsNull(response.Strain, id); Assert.IsNull(response.Utilization, id);
        }

        internal static void VerifyIndependentResistances()
        {
            Assert.AreEqual(30, DomainPointIndependentData.ZeroN.Length);
            foreach (var oracle in DomainPointIndependentData.ZeroN)
            {
                var shape = DomainPointContractTests.Sections.Single(s => s.Name == oracle.Section);
                var diagram = (ConcreteMaterial.CompressionStressStrainDiagrams)Enum.Parse(typeof(ConcreteMaterial.CompressionStressStrainDiagrams), oracle.Diagram);
                var state = (SectionSolver.FailureDomainTypes)Enum.Parse(typeof(SectionSolver.FailureDomainTypes), oracle.State);
                var section = DomainPointContractTests.BuildSection(shape, diagram);
                var checker = DomainPointContractTests.BuildChecker(section, state);
                var demand = DomainPointContractTests.Force(DomainPointContractTests.LocalAxes(section), 0, 1, 0);
                var response = new LegacySectionCalculation(checker).SolveResistance(new SectionAnalysisInput(demand));
                string id = oracle.Section + "/" + oracle.Diagram + "/" + oracle.State;
                Assert.AreEqual(CalculationStatus.Completed, response.Diagnostics.Status, id);
                // Accuracy requirement fixed before executing these tests: 2 per mille for fixed-Gauss versus exact strip integration.
                // This is an engineering acceptance target, not a mathematical quadrature error bound; a failure requires investigation.
                Assert.AreEqual(oracle.Moment, response.M1.Value, .002 * oracle.Moment, id + ": independent exact-strip resistance");
                Assert.IsTrue(Math.Abs(response.N.Value) <= DomainPointAxialTolerance.Calculate(checker.SectionSolver, 0), id);
            }
        }
    }
}
