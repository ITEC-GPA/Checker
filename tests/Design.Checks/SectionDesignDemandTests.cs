using GPC.Design.Sections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace DesignTests;
[TestClass]
public sealed class SectionDesignDemandTests
{
    [TestMethod]
    public void CompressionUsesTheGoverningSignedActionIncludingShearRows()
    {
        Assert.AreEqual(6000d,SectionDesignDemand.MaximumCompression([-100,-6000,9000]));
        Assert.AreEqual(0d,SectionDesignDemand.MaximumCompression([100,6000]));
        Assert.AreEqual(0d,SectionDesignDemand.MaximumCompression([]));
        Assert.AreEqual(12d,SectionDesignDemand.Compression(-12));
        Assert.AreEqual(0d,SectionDesignDemand.Compression(12));
        Assert.ThrowsException<ArgumentException>(()=>SectionDesignDemand.MaximumCompression([-100,double.NaN]));
    }
    [TestMethod]
    public void StressAndAnchorageUseResolvedLimitsWithoutNewCoefficients()
    {
        Assert.AreEqual(.6,SectionDesignDemand.Ratio(SectionDesignDemand.Compression(-12),20));
        Assert.AreEqual(.75,SectionDesignDemand.Ratio(300,400));
        Assert.AreEqual(.8,SectionDesignDemand.Ratio(400,500));
        Assert.AreEqual(500d,SectionDesignDemand.RequiredCapacity(400,.8));
        Assert.AreEqual(0d,SectionDesignDemand.RequiredCapacity(0,.8));
    }
    [TestMethod]
    public void MissingOrInvalidCapacityNeverPassesTheTargetCheck()
    {
        foreach(double? capacity in new double?[]{null,0,-1,double.NaN,double.PositiveInfinity})
        {
            var ratio=SectionDesignDemand.Ratio(1,capacity);Assert.IsNull(ratio);
            Assert.IsFalse(SectionDesignCheck.FromRatio("x","x","x",ratio,1,"").Passed);
        }
        foreach(double demand in new[]{-1,double.NaN,double.PositiveInfinity})Assert.IsNull(SectionDesignDemand.Ratio(demand,1));
        Assert.IsNull(SectionDesignDemand.Ratio(double.MaxValue,double.Epsilon));
        foreach(double target in new[]{0,-1,2,double.NaN,double.PositiveInfinity})
            Assert.ThrowsException<ArgumentException>(()=>SectionDesignDemand.RequiredCapacity(1,target));
        Assert.ThrowsException<ArgumentException>(()=>SectionDesignDemand.RequiredCapacity(double.MaxValue,double.Epsilon));
    }
}
