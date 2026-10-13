using System;
using GPC.Checkers.Concrete.Durability;
using GPC.Model.Checking.Contracts;
using GPC.Model.Core.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace ConcreteTests
{
    [TestClass]
    public class CoverAssessmentTests
    {
        [TestMethod]
        public void ActualBarsAndMaterialRequirementBothGovern()
        {
            var material=CoverAssessment.Evaluate(40,40,30);
            Assert.AreEqual(40d,material.Required);Assert.AreEqual(true,material.Passed);Assert.AreEqual(DataStatus.Ready,material.Data);
            var bars=CoverAssessment.Evaluate(45,40,50);
            Assert.AreEqual(50d,bars.Required);Assert.AreEqual(false,bars.Passed);Assert.AreEqual(EngineeringOutcome.NotSatisfied,bars.Outcome);
            Assert.AreEqual(true,CoverAssessment.Evaluate(50,40,50).Passed);
            Assert.AreEqual(false,CoverAssessment.Evaluate(0,40,50).Passed);
        }
        [TestMethod]
        public void MissingBarsCannotPassOrHideKnownInsufficientCover()
        {
            var unknown=CoverAssessment.Evaluate(80,40,null);
            Assert.IsNull(unknown.Passed);Assert.AreEqual(EngineeringOutcome.NotEvaluated,unknown.Outcome);Assert.AreEqual(DataStatus.Insufficient,unknown.Data);
            var failed=CoverAssessment.Evaluate(20,40,null);
            Assert.AreEqual(false,failed.Passed);Assert.AreEqual(EngineeringOutcome.NotSatisfied,failed.Outcome);Assert.AreEqual(DataStatus.Insufficient,failed.Data);
        }
        [TestMethod]
        public void InvalidNumbersCannotBeCertified()
        {
            foreach(double invalid in new[]{-1d,double.NaN,double.PositiveInfinity,double.NegativeInfinity})
            {
                Assert.ThrowsException<ArgumentException>(()=>CoverAssessment.Evaluate(invalid,40,40));
                Assert.ThrowsException<ArgumentException>(()=>CoverAssessment.Evaluate(40,invalid,40));
                Assert.ThrowsException<ArgumentException>(()=>CoverAssessment.Evaluate(40,40,invalid));
            }
        }
    }
}
