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
    public void DetailingCompressionTakesTheMostSevereRowOfAllFamilies()
    {
        DesignAxialAction A(string name,DesignActionFamily family,double n)=>new(name,family,n);
        var ultimate=A("SLU 1",DesignActionFamily.Ultimate,-100);var shear=A("V 1",DesignActionFamily.Shear,-6000);
        var seismic=A("SLV 1",DesignActionFamily.Seismic,-2500);var rare=A("Rara 1",DesignActionFamily.Characteristic,-4000);
        var all=SectionDesignDemand.DetailingCompression([ultimate,rare,seismic,shear,A("SLU 2",DesignActionFamily.Ultimate,9000)]);
        Assert.AreEqual(6000d,all.Value);Assert.AreSame(shear,all.Source);Assert.IsFalse(all.FromServiceability);
        // Without ultimate rows the serviceability rows give NEd, marked as a service value
        var service=SectionDesignDemand.DetailingCompression([rare,A("QP 1",DesignActionFamily.QuasiPermanent,-3000)]);
        Assert.AreEqual(4000d,service.Value);Assert.AreSame(rare,service.Source);Assert.IsTrue(service.FromServiceability);
        // Equal compression: the design family wins whatever the order; then the first row
        var tie=SectionDesignDemand.DetailingCompression([A("Rara",DesignActionFamily.Characteristic,-500),A("SLU",DesignActionFamily.Ultimate,-500),A("SLV",DesignActionFamily.Seismic,-500)]);
        Assert.AreEqual("SLU",tie.Source!.Name);
        var none=SectionDesignDemand.DetailingCompression([A("T",DesignActionFamily.Ultimate,50),A("Q",DesignActionFamily.Frequent,0)]);
        Assert.AreEqual(0d,none.Value);Assert.IsNull(none.Source);Assert.IsFalse(none.FromServiceability);
        Assert.AreEqual(0d,SectionDesignDemand.DetailingCompression(Array.Empty<DesignAxialAction>()).Value);
        Assert.ThrowsException<ArgumentException>(()=>SectionDesignDemand.DetailingCompression([A("x",DesignActionFamily.Ultimate,double.NaN)]));
        Assert.ThrowsException<ArgumentException>(()=>SectionDesignDemand.DetailingCompression([A("x",(DesignActionFamily)99,-1)]));
        Assert.ThrowsException<ArgumentNullException>(()=>SectionDesignDemand.DetailingCompression([ultimate,null!]));
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
