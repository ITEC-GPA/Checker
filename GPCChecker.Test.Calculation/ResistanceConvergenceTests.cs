using System;
using System.Linq;
using GPC.Checkers.Concrete.Analysis;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Results;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace ConcreteTests
{
    [TestClass]
    public class ResistanceConvergenceTests
    {
        private static SectionAnalysisInput Force(double n, double m1, double m2, CoordinateSystem axes = null) =>
            new SectionAnalysisInput(new ResultBeamForces(n, 0, 0, 0, m1, m2, axes ?? CoordinateSystem.Global));
        private static ResistanceConvergence Check(SectionSolver.FailureAnalysisTypes criterion, SectionAnalysisInput capacity) =>
            ResistanceConvergence.Evaluate(criterion, Force(-300000, 120e6, 60e6), capacity, CoordinateSystem.Global, 1000, 10000, 5000, .00025);
        [DataTestMethod]
        [DataRow(SectionSolver.FailureAnalysisTypes.ConstantN, -300000.0, 240e6, 120e6)]
        [DataRow(SectionSolver.FailureAnalysisTypes.ConstantEccentricity, -600000.0, 240e6, 120e6)]
        [DataRow(SectionSolver.FailureAnalysisTypes.ConstantMxMy, -600000.0, 120e6, 60e6)]
        [DataRow(SectionSolver.FailureAnalysisTypes.ConstantNMx, -300000.0, 120e6, 120e6)]
        [DataRow(SectionSolver.FailureAnalysisTypes.ConstantNMy, -300000.0, 240e6, 60e6)]
        public void EachCriterionAcceptsItsOwnSearchLine(SectionSolver.FailureAnalysisTypes criterion, double n, double m1, double m2)
        {
            var result = Check(criterion, Force(n, m1, m2)); Assert.IsTrue(result.Accepted);
            Assert.AreEqual(criterion, result.Criterion); Assert.IsTrue(result.Residuals.All(r => r.Accepted));
        }
        [DataTestMethod]
        [DataRow(SectionSolver.FailureAnalysisTypes.ConstantN, -302000.0, 240e6, 120e6)]
        [DataRow(SectionSolver.FailureAnalysisTypes.ConstantN, -300000.0, 240e6, 110e6)]
        [DataRow(SectionSolver.FailureAnalysisTypes.ConstantEccentricity, -700000.0, 240e6, 120e6)]
        [DataRow(SectionSolver.FailureAnalysisTypes.ConstantMxMy, -600000.0, 121e6, 60e6)]
        [DataRow(SectionSolver.FailureAnalysisTypes.ConstantMxMy, -600000.0, 120e6, 61e6)]
        [DataRow(SectionSolver.FailureAnalysisTypes.ConstantNMx, -300000.0, 121e6, 120e6)]
        [DataRow(SectionSolver.FailureAnalysisTypes.ConstantNMx, -302000.0, 120e6, 120e6)]
        [DataRow(SectionSolver.FailureAnalysisTypes.ConstantNMy, -300000.0, 240e6, 61e6)]
        [DataRow(SectionSolver.FailureAnalysisTypes.ConstantNMy, -302000.0, 240e6, 60e6)]
        public void FinitePointOutsideConstraintsIsRejected(SectionSolver.FailureAnalysisTypes criterion, double n, double m1, double m2)
        { Assert.IsFalse(Check(criterion, Force(n, m1, m2)).Accepted); }
        [DataTestMethod]
        [DataRow(SectionSolver.FailureAnalysisTypes.ConstantN, -300000.0, -240e6, -120e6)]
        [DataRow(SectionSolver.FailureAnalysisTypes.ConstantEccentricity, 600000.0, -240e6, -120e6)]
        [DataRow(SectionSolver.FailureAnalysisTypes.ConstantMxMy, 600000.0, 120e6, 60e6)]
        [DataRow(SectionSolver.FailureAnalysisTypes.ConstantNMx, -300000.0, 120e6, -120e6)]
        [DataRow(SectionSolver.FailureAnalysisTypes.ConstantNMy, -300000.0, -240e6, 60e6)]
        public void OppositeBranchCannotPassWithAnAbsoluteWorkingRatio(SectionSolver.FailureAnalysisTypes criterion, double n, double m1, double m2)
        { Assert.IsFalse(Check(criterion, Force(n, m1, m2)).Accepted); }
        [TestMethod]
        public void FixedMomentToleranceIncludesBoundaryWithoutFixingTheFreeComponent()
        {
            Assert.IsTrue(Check(SectionSolver.FailureAnalysisTypes.ConstantMxMy, Force(-1e9, 120e6 + 10000, 60e6 - 5000)).Accepted);
            Assert.IsFalse(Check(SectionSolver.FailureAnalysisTypes.ConstantMxMy, Force(-1e9, 120e6 + 10001, 60e6)).Accepted);
        }
        [TestMethod]
        public void FrameChangesPreserveThePhysicalConstraints()
        {
            var frame = new CoordinateSystem(new Point3d(0, 0, 0), new Vector3d(0, 1, 0), new Vector3d(-1, 0, 0));
            var demand = Force(-300000, 120e6, 60e6).Forces.ToCoordinateSystem(frame);
            var capacity = Force(-600000, 120e6, 60e6).Forces.ToCoordinateSystem(frame);
            var result = ResistanceConvergence.Evaluate(SectionSolver.FailureAnalysisTypes.ConstantMxMy,
                new SectionAnalysisInput(demand), new SectionAnalysisInput(capacity), CoordinateSystem.Global, 1000, 10000, 5000, .00025);
            Assert.IsTrue(result.Accepted);
            capacity.M1 = 0; demand.N = 1; result.ConstraintAxes.Origin.X = 999;
            Assert.IsTrue(result.Accepted); Assert.AreEqual(-300000, result.Demand.Forces.N);
        }
        [TestMethod]
        public void ZeroSearchDirectionAndInvalidToleranceAreNotAccepted()
        {
            foreach (SectionSolver.FailureAnalysisTypes criterion in Enum.GetValues(typeof(SectionSolver.FailureAnalysisTypes)))
                Assert.IsFalse(ResistanceConvergence.Evaluate(criterion, Force(0,0,0), Force(0,0,0), CoordinateSystem.Global, 1000,10000,5000,.00025).Accepted);
            Assert.ThrowsException<ArgumentException>(() => ResistanceConvergence.Evaluate(SectionSolver.FailureAnalysisTypes.ConstantN,
                Force(0,1,0), Force(0,1,0), CoordinateSystem.Global, 1000, -1, 1, .00025));
        }
    }
}
