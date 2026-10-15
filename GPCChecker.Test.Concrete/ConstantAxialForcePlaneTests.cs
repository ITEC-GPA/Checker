using GPC.Checkers.Concrete.Helper;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
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
    /// end of the segment. Checks: at constant N the resultant of the declared plane, integrated by the solver as the points of the domain, must be
    /// the point within a small part of its segment of the meridian (with the weights exchanged the distance is about |1 - 2t| times the segment,
    /// up to 0.97 before the fix); on the sides of the 2d Mx-My domain, θ and immersion of the plane must be the hand interpolation of the ends.
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
        public void PlaneOfThePointOfTheMxMyDomainIsTheInterpolatedOne()
        {
            // The 2d domain at constant N: the plane of a point on a side of the polygon must interpolate θ and immersion of the ends with the
            // parameter t of the point on the side (hand interpolation: (1 - t) at the first end, t at the second). Its resultant is not compared
            // with the point: within a side the forces are not linear in θ and immersion (up to about 0.4 of the side in N at N = 0).
            var section = UltimateSections.Symmetric();
            var checker = UltimateSections.Checker(section, SectionSolver.FailureAnalysisTypes.ConstantEccentricity);
            var domain = checker.GetFailureDomainResult();
            var local = DomainPointContractTests.LocalAxes(section);
            int checkedSides = 0, checkedImmersions = 0;
            foreach (double n in new[] { -1800e3, -1000e3, 0 })
            {
                var result = domain.CalculateDomainConstantAxialForce(UltimateSections.Force(section, n / 1000, 0, 0));
                var points = result.Domain.DomainPoints;
                for (int k = 0; k < 24; k++)
                {
                    double angle = (k + 0.37) * 2 * Math.PI / 24;
                    string id = "N " + n / 1000 + " kN, direction " + (angle * 180 / Math.PI).ToString("F1", CultureInfo.InvariantCulture);
                    result.ClearForces();
                    var q = result.AddForce(new ResultBeamForces(n, 0, 0, 0, 1e6 * Math.Cos(angle), 1e6 * Math.Sin(angle), local, 1)).FailureDomainPoint;
                    // The side (a, b) of the polygon (MxRd, MyRd) that contains the point and the parameter of the point on it.
                    int side = -1; double t = 0, best = double.MaxValue;
                    for (int i = 0; i < points.Length; i++)
                    {
                        var a = points[i]; var b = points[(i + 1) % points.Length];
                        double dx = b.MxRd - a.MxRd, dy = b.MyRd - a.MyRd, length2 = dx * dx + dy * dy;
                        if (!(length2 > 0)) continue;
                        double s = ((q.MxRd - a.MxRd) * dx + (q.MyRd - a.MyRd) * dy) / length2;
                        double off = Math.Abs((q.MxRd - a.MxRd) * dy - (q.MyRd - a.MyRd) * dx) / Math.Sqrt(length2);
                        if (s >= -1e-9 && s <= 1 + 1e-9 && off < best) { best = off; side = i; t = s; }
                    }
                    Assert.IsTrue(side >= 0 && best < 1e-3 * Math.Abs(q.MxRd + q.MyRd) + 1, id + ": point not on a side");
                    var pa = points[side]; var pb = points[(side + 1) % points.Length];
                    double thetaA = pa.StrainPlane.Teta, thetaB = pb.StrainPlane.Teta;
                    if (Math.Abs(thetaA - thetaB) > Math.PI) { if (thetaA < thetaB) thetaA += 2 * Math.PI; else thetaB += 2 * Math.PI; }
                    double theta = (1 - t) * thetaA + t * thetaB;
                    Assert.AreEqual(0, Math.Sin(q.StrainPlane.Teta - theta), 1e-6, id + ": θ of the plane");
                    Assert.IsTrue(Math.Cos(q.StrainPlane.Teta - theta) > 0, id + ": θ of the plane");
                    checkedSides++;
                    if (pa.FailureIndex != pb.FailureIndex) continue;
                    // Immersion only between ends in the same failure field: otherwise the immersion of the end in the later field is used as an
                    // immersion of the earlier one (SectionSolver.GetImmersione), another defect, reported.
                    Assert.AreEqual((1 - t) * pa.Immersione + t * pb.Immersione, q.Immersione, 1e-9, id + ": immersion of the plane");
                    checkedImmersions++;
                }
            }
            Assert.AreEqual(72, checkedSides);
            Assert.IsTrue(checkedImmersions >= 48, "immersions checked: " + checkedImmersions);
        }
    }
}
