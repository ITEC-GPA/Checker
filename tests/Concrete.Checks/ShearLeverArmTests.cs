using System;
using GPC.Checkers.Concrete.Shear;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GPCChecker.Test.Concrete
{
    [TestClass]
    public class ShearLeverArmTests
    {
        [TestMethod]
        public void CircularAndDinGeometricChoicesHaveIndependentValues()
        {
            Assert.AreEqual(.9,ShearLeverArm.Ratio(ShearProfile.Ntc2018,500,40,10));
            Assert.AreEqual(.75,ShearLeverArm.Ratio(ShearProfile.Ntc2018,500,40,10,true,false,true));
            Assert.AreEqual(.6,ShearLeverArm.Ratio(ShearProfile.Ntc2018,500,40,10,true,true,true));
            Assert.AreEqual(.84,ShearLeverArm.Ratio(ShearProfile.DinEN1992p11,500,40,10),1e-15);
            Assert.AreEqual(.8,ShearLeverArm.Ratio(ShearProfile.DinEN1992p11,500,40,10,true,false,false,.8));
            Assert.ThrowsException<ArgumentException>(()=>ShearLeverArm.Ratio(ShearProfile.EN1992p11,500,40,10,true));
        }
        [TestMethod]
        public void DanishDuctilityAndInvalidChoicesAreExplicit()
        {
            Assert.IsTrue(ShearLeverArm.DanishSteelDuctility(500,540,50));
            Assert.IsFalse(ShearLeverArm.DanishSteelDuctility(500,539.99,50));
            Assert.IsFalse(ShearLeverArm.DanishSteelDuctility(500,540,49.99));
            Assert.IsFalse(ShearLeverArm.DanishSteelDuctility(500,double.NaN,50));
            Assert.ThrowsException<ArgumentException>(()=>ShearLeverArm.Ratio(ShearProfile.DinEN1992p11,500,40,10,true,false,true));
            Assert.ThrowsException<ArgumentException>(()=>ShearLeverArm.Ratio(ShearProfile.Ntc2018,500,40,10,true,false,false,1));
            Assert.ThrowsException<ArgumentException>(()=>ShearLeverArm.Ratio(ShearProfile.Ntc2018,double.NaN,40,10));
        }
    }
}
