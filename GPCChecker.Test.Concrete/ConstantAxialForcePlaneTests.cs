using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Results;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Globalization;
using System.Linq;

namespace ConcreteTests
{
    /// <summary>
    /// Strain plane of the points interpolated at constant N (bug found with SLU-1 of 15/10/2026): FailureDomainResult.CalculateDomainConstantAxialForce
    /// and the constant-N branch of FailureDomain2d.GetDomainPoint interpolated the forces of the point between the ends A and B of a segment with
    /// the parameter t (0 at A) but weighted θ and immersion of the strain plane with t for A and 1 - t for B, so the plane was the one of the other
    /// end of the segment. Check: at constant N the resultant of the declared plane, integrated by the solver as the points of the domain, must be
    /// the point within a small part of its segment of the meridian (with the weights exchanged the distance is about |1 - 2t| times the segment,
    /// up to 0.97 before the fix). The points on the sides of the 2d domains (constant N and N–M meridian of the moment direction) declare since
    /// 16/10/2026 the plane of the point of the domain in their direction, with the stated tolerances of their tests.
    /// Section 300 × 500 C25/30 4Ø16 NTC 2018 (<see cref="UltimateSections.Symmetric"/>), plastic domain.
    /// </summary>
    [TestClass]
    public class ConstantAxialForcePlaneTests
    {
        private const double Allowed = 0.2;

        /// <summary>The forces of the plane in the force reference axes, as CalculateFailureDomain builds the points of the domain.</summary>
        private static ForceTuple Resultant(SectionSolver solver, StrainPlane plane) =>
            solver.GetExternalForces(solver.CalculateForceResultantForDomain(plane), solver.SectionOption.ForceReferenceCoordinateSystem);

        /// <summary>Distance of the forces in the adimensional units of the solver.</summary>
        private static double Distance(SectionSolver solver, ForceTuple a, ForceTuple b)
        {
            var d = solver.ConvertToAdimensionalForces(new ForceTuple(a.N - b.N, a.Mx - b.Mx, a.My - b.My));
            return Math.Sqrt(d.N * d.N + d.Mx * d.Mx + d.My * d.My);
        }

        [TestMethod]
        public void PlaneOfThePointAtAnAssignedAxialForceIsTheInterpolatedOne()
        {
            var section = UltimateSections.Symmetric();
            var checker = UltimateSections.Checker(section, SectionSolver.FailureAnalysisTypes.ConstantEccentricity);
            var solver = checker.SectionSolver;
            var domain = checker.GetFailureDomainResult();
            double worst = 0; string where = "";
            foreach (double n in new[] { -2300e3, -1800e3, -1000e3, -500e3, 0, 150e3 })
            {
                var result = domain.CalculateDomainConstantAxialForce(UltimateSections.Force(section, n / 1000, 0, 0));
                for (int i = 0; i < result.Domain.DomainPoints.Length; i++)
                {
                    var point = result.Domain.DomainPoints[i];
                    // The segment of the meridian of the 3d domain that contains N, as CalculateDomainConstantAxialForce finds it.
                    var meridian = domain.Domain.DomainPoints[i];
                    int j = Enumerable.Range(1, meridian.Length - 1).First(k => n > meridian[k].NRd && n < meridian[k - 1].NRd || n == meridian[k].NRd);
                    double segment = Distance(solver, meridian[j - 1].ForceTuple, meridian[j].ForceTuple);
                    if (segment == 0) continue;
                    double ratio = Distance(solver, Resultant(solver, point.StrainPlane), point.ForceTuple) / segment;
                    if (ratio > worst) { worst = ratio; where = "N " + n / 1000 + " kN, meridian " + i; }
                }
            }
            Assert.IsTrue(worst < Allowed, "plane of the point at constant N: distance " + worst.ToString("G4", CultureInfo.InvariantCulture)
                + " of the segment at " + where);
        }

        [TestMethod]
        public void PlaneOfThePointOfTheMxMyDomainHasItsAxialForce()
        {
            // 2d domain at constant N: the plane declared for a point on a side of the polygon is the one of the point of the domain at the same N
            // in the direction of its moment. Stated tolerance: N of the resultant of the plane within the tolerance on N of the points at an
            // assigned axial force (DomainPointAxialTolerance, 1 kN here); moment along the point within SolvedPlaneAngle (5e-3 rad) and not
            // shorter, longer by the gap between the side and the domain (below 5 % with 32 meridians). Before (16/10/2026) the plane
            // interpolated θ and immersion of the ends, also between ends in different failure fields: at N = -1000 kN, 350.6° (ends in F3B
            // 0.017 and F3A 0.997) its N was -971.9 kN.
            foreach (var section in new[] { UltimateSections.Symmetric(), UltimateSections.Asymmetric() })
                foreach (var state in new[] { SectionSolver.FailureDomainTypes.Plastic, SectionSolver.FailureDomainTypes.Elastic })
                {
                    var checker = UltimateSections.Checker(section, SectionSolver.FailureAnalysisTypes.ConstantEccentricity, state);
                    var solver = checker.SectionSolver;
                    var domain = checker.GetFailureDomainResult();
                    var local = DomainPointContractTests.LocalAxes(section);
                    foreach (double n in new[] { -1800e3, -1000e3, 0 })
                    {
                        var result = domain.CalculateDomainConstantAxialForce(UltimateSections.Force(section, n / 1000, 0, 0));
                        double tolerance = DomainPointAxialTolerance.Calculate(solver, n);
                        for (int k = 0; k < 48; k++)
                        {
                            double angle = (k + 0.37) * 2 * Math.PI / 48;
                            string id = section.Rebars.Count() + " bars " + state + ", N " + n / 1000 + " kN, direction " + (angle * 180 / Math.PI).ToString("F1", CultureInfo.InvariantCulture);
                            result.ClearForces();
                            var q = result.AddForce(new ResultBeamForces(n, 0, 0, 0, 1e6 * Math.Cos(angle), 1e6 * Math.Sin(angle), local, 1)).FailureDomainPoint;
                            var r = Resultant(solver, q.StrainPlane);
                            Assert.AreEqual(q.NRd, r.N, tolerance, id + ": N of the plane");
                            var mq = new Vector3d(q.MxRd, q.MyRd, 0); var mr = new Vector3d(r.Mx, r.My, 0);
                            Assert.IsTrue(mq.AngleTo(mr) <= FailureDomain2d.SolvedPlaneAngle, id + ": direction of the moment " + mq.AngleTo(mr));
                            Assert.IsTrue(mr.Length >= .999 * mq.Length && mr.Length <= 1.05 * mq.Length, id + ": moment " + mr.Length / mq.Length);
                        }
                    }
                }
            // The case of the report: N = -1000 kN, 350.6° on the symmetric section, plastic domain.
            var sym = UltimateSections.Symmetric();
            var plastic = UltimateSections.Checker(sym, SectionSolver.FailureAnalysisTypes.ConstantEccentricity);
            var section1000 = plastic.GetFailureDomainResult().CalculateDomainConstantAxialForce(UltimateSections.Force(sym, -1000, 0, 0));
            double a = (23 + 0.37) * 2 * Math.PI / 24;
            var point = section1000.AddForce(new ResultBeamForces(-1000e3, 0, 0, 0, 1e6 * Math.Cos(a), 1e6 * Math.Sin(a), DomainPointContractTests.LocalAxes(sym), 1)).FailureDomainPoint;
            Assert.AreEqual(-1000e3, Resultant(plastic.SectionSolver, point.StrainPlane).N, 1000, "N = -1000 kN, 350.6°: before -971.9 kN");
        }

        [TestMethod]
        public void PlaneOfThePointOfTheMomentDirectionMeridianIsInItsDirection()
        {
            // N–M meridian of the moment direction (CalculateDomainConstantMomentsRatio): the plane declared for a point on a side is the one of
            // the point of the domain at constant eccentricity in its direction. Stated tolerance: resultant of the plane along the point within
            // SolvedPlaneAngle (5e-3 rad, N in kN and moments in kNm), modulus from 0.96 to 1.10 times the point (the vertices of the meridian
            // can be slightly outside the domain; beyond, the gap between the side and the domain). Before (16/10/2026) the plane interpolated θ
            // and immersion of the ends, up to some hundreds of kN from the point (also between ends in different failure fields).
            // Near the pole of tension of the asymmetric section the sides of the 40 points of the meridian lie up to about 50 % inside the domain:
            // there the direction holds but the gap is larger, so the asymmetric section is checked in compression.
            foreach (var (section, angles, axials) in new[]
            {
                (UltimateSections.Symmetric(), new[] { 0.0, 30, 90, 135, 270 }, new[] { -2300e3, -2000e3, -1500e3, -1000e3, -600e3, -300e3, 0, 100e3, 250e3 }),
                (UltimateSections.Asymmetric(), new[] { 0.0, 90, 270 }, new[] { -2000e3, -1500e3, -1000e3, -600e3, -300e3 }),
            })
                foreach (var state in new[] { SectionSolver.FailureDomainTypes.Plastic, SectionSolver.FailureDomainTypes.Elastic })
                {
                    var checker = UltimateSections.Checker(section, SectionSolver.FailureAnalysisTypes.ConstantEccentricity, state);
                    var solver = checker.SectionSolver;
                    var domain = checker.GetFailureDomainResult();
                    var local = DomainPointContractTests.LocalAxes(section);
                    foreach (double theta in angles)
                    {
                        double t = theta * Math.PI / 180;
                        var meridian = domain.CalculateDomainConstantMomentsRatio(UltimateSections.Force(section, 0, Math.Cos(t), Math.Sin(t)));
                        double axis = meridian.Domain.MomentAxis;
                        foreach (double n in axials)
                            foreach (double m in new[] { 10e6, 50e6, 150e6, 300e6 })
                            {
                                string id = section.Rebars.Count() + " bars " + state + ", θ " + theta + ", N " + n / 1000 + " kN, M " + m / 1e6 + " kNm";
                                var action = UltimateSections.Force(section, n / 1000, m / 1e6 * Math.Cos(t), m / 1e6 * Math.Sin(t));
                                meridian.ClearForces();
                                var q = meridian.AddForce(new ResultBeamForces(action.N, 0, 0, 0, action.M1 * Math.Cos(axis) + action.M2 * Math.Sin(axis), 0, local, 1)).FailureDomainPoint;
                                var r = Resultant(solver, q.StrainPlane);
                                var vq = new Vector3d(q.MxRd / 1e6, q.MyRd / 1e6, q.NRd / 1000); var vr = new Vector3d(r.Mx / 1e6, r.My / 1e6, r.N / 1000);
                                Assert.IsTrue(vq.AngleTo(vr) <= FailureDomain2d.SolvedPlaneAngle, id + ": direction " + vq.AngleTo(vr));
                                Assert.IsTrue(vr.Length >= .96 * vq.Length && vr.Length <= 1.10 * vq.Length, id + ": modulus " + vr.Length / vq.Length);
                            }
                    }
                }
        }
    }
}
