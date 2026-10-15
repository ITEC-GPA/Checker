using GPC.Checkers.Concrete.Response;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace ConcreteTests
{
    /// <summary>
    /// Bug SLU-4 of 15/10/2026 (finding sezione-slu F4): in the moment-curvature curve on a native checker every ε0 was the strain at the reference
    /// point of the integration (the centroid of the local axes), except the limit point, whose strain plane comes from the failure domain with
    /// its reference at the pivot vertex: -3.5 ‰ in the same column. Section 300 × 500 C25/30 4Ø16 NTC 2018 (<see cref="UltimateSections.Symmetric"/>).
    /// Expected values: strain at the centroid of the limit plane of the fibre model of agent-conc-slu/ref_slu.py (pivot B or A, neutral axis
    /// from equilibrium at N): -0.5032 ‰ at N = -1000 kN, +15.559 ‰ at N = 0 (the finding gives -0.508 ‰ and +15.54 ‰ from the library plane).
    /// </summary>
    [TestClass]
    public class MomentCurvatureReferenceStrainTests
    {
        private static MomentCurvatureResult Curve(double axialForce)
        {
            var section = UltimateSections.Symmetric();
            var checker = UltimateSections.Checker(section, SectionSolver.FailureAnalysisTypes.ConstantN);
            return MomentCurvatureAnalysis.Calculate(new MomentCurvatureRequest(axialForce, 0, 10, 1, false), checker, section,
                DomainPointContractTests.LocalAxes(section), 450 / 1.15 / 200000);
        }

        [TestMethod]
        public void LimitPointStrainIsAtTheReferencePointOfTheOtherPoints()
        {
            var compressed = Curve(-1000e3);
            var limit = compressed.Points.Last();
            Assert.IsTrue(limit.Limit);
            // Before: -3.5e-3, the strain at the compressed vertex.
            Assert.AreEqual(-0.5032e-3, limit.ReferenceStrain, 0.02e-3, "N = -1000 kN");
            // The points before the limit are stress analyses at the same N: their ε0 is of the same order (-0.45 to -0.54 ‰).
            Assert.IsTrue(compressed.Points.Take(compressed.Points.Count - 1).All(p => p.ReferenceStrain < -0.4e-3 && p.ReferenceStrain > -0.6e-3));
            Assert.AreEqual(15.559e-3, Curve(0).Points.Last().ReferenceStrain, 0.1e-3, "N = 0");
        }

        [TestMethod]
        public void StrainsOfAPlaneAtAGivenReferencePoint()
        {
            var section = UltimateSections.Symmetric();
            var plane = new StrainPlane(1e-5, -2e-5, new Point2d(-150, 250), -3.5e-3);
            // Without the point: the strain at the reference point of the plane, as before.
            Assert.AreEqual(-3.5e-3, MomentCurvatureStrains.From(plane, section).ReferenceStrain);
            // ε(0, 0) = -3.5e-3 + 1e-5 (0 + 150) - 2e-5 (0 - 250) = 3.0e-3 (hand calculation).
            Assert.AreEqual(3.0e-3, MomentCurvatureStrains.From(plane, section, new Point2d(0, 0)).ReferenceStrain, 1e-15);
            Assert.AreEqual(-3.5e-3, MomentCurvatureStrains.From(plane, section, new Point2d(-150, 250)).ReferenceStrain);
        }
    }
}
