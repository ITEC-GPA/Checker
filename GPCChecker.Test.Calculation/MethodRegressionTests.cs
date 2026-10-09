using System;
using GPC.Checkers.Concrete.Analysis;
using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CalculationTests
{
    [TestClass]
    public class MethodRegressionTests
    {
        [TestMethod]
        public void GetStrainPoint_UsesReferenceAndSignedCurvatures()
        { Assert.AreEqual(.008, new StrainPlane(.001, -.002, new Point2d(10, 20), .003).GetStrain(new Point2d(13, 19)), 1e-14); }
        [TestMethod]
        public void GetStrainCoordinates_UsesReferenceAndSignedCurvatures()
        { Assert.AreEqual(.008, new StrainPlane(.001, -.002, new Point2d(10, 20), .003).GetStrain(13, 19), 1e-14); }
        [TestMethod]
        public void GetNeutralAxis_PureBendingReturnsZeroStrainAtGlobalEndpoints()
        {
            var plane = new StrainPlane(.001, 0, new Point2d(10, 20), .003);
            var axis = plane.GetNeutralAxis();
            Assert.AreEqual(7.0, axis.Start.X, 1e-12); Assert.AreEqual(7.0, axis.End.X, 1e-12);
            Assert.AreEqual(0, plane.GetStrain(axis.Start), 1e-14); Assert.AreEqual(0, plane.GetStrain(axis.End), 1e-14);
        }
        [TestMethod]
        public void GetNeutralAxisRespectReferencePoint_KeepsRelativeCoordinates()
        {
            var axis = new StrainPlane(.001, 0, new Point2d(10, 20), .003).GetNeutralAxisRespectReferencePoint();
            Assert.AreEqual(-3.0, axis.Start.X, 1e-12); Assert.AreEqual(-3.0, axis.End.X, 1e-12);
        }
        [TestMethod]
        public void GetConstantStrainAxis_UniformStrainHasNoUniqueAxis()
        { Assert.IsNull(new StrainPlane(0, 0, new Point2d(10, 20), .003).GetConstantStrainAxis(.003)); }
        [TestMethod]
        public void GetConstantStrainAxis_ObliqueAxisHasRequestedStrain()
        {
            var plane = new StrainPlane(.001, -.002, new Point2d(10, 20), .003);
            var axis = plane.GetConstantStrainAxis(.005);
            Assert.AreEqual(.005, plane.GetStrain(axis.Start.X + 10, axis.Start.Y + 20), 1e-14);
            Assert.AreEqual(.005, plane.GetStrain(axis.End.X + 10, axis.End.Y + 20), 1e-14);
        }
        [DataTestMethod]
        [DataRow(5.0, true)] [DataRow(-5.0, true)] [DataRow(5.001, false)]
        public void NumericalResidual_AcceptsAbsoluteResidualAtInclusiveTolerance(double residual, bool accepted)
        { Assert.AreEqual(accepted, new NumericalResidual("N", "N", residual, 5).Accepted); }
        [TestMethod]
        public void AxialEquilibriumEvidence_RejectsOverflowOfOtherwiseFiniteForces()
        { Assert.ThrowsException<ArgumentException>(() => new AxialEquilibriumEvidence(-double.MaxValue, double.MaxValue, 1)); }
    }
}
