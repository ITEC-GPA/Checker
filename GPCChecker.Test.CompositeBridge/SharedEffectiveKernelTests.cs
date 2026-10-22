using GPC.Checkers.CompositeBridge;
using Common=GPC.Checkers.Steel.EffectiveSections.EffectivePlateReduction;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace CompositeBridgeTests;

[TestClass]
public class SharedEffectiveKernelTests
{
    public static IEnumerable<object[]> Cases=>from b in new[]{100d,1000d} from t in new[]{3d,12d} from fy in new[]{235d,355d}
        from stresses in new[]{(-100d,-100d),(-100d,-40.6),(-100d,0d),(-100d,100d),(100d,-100d),(-100d,500d),(100d,200d)}
        select new object[]{b,t,fy,stresses.Item1,stresses.Item2};
    [DataTestMethod,DynamicData(nameof(Cases))]
    public void ExistingBridgeFacadeUsesExactlyTheSteelKernel(double b,double t,double fy,double start,double end)
    {
        var old=EffectivePlateReduction.InternalPlate(b,t,start,end,fy);var core=Common.InternalPlate(b,t,start,end,fy);
        Assert.AreEqual(core.Rho,old.Rho);Assert.AreEqual(core.Lambda,old.Lambda);Assert.AreEqual(core.KSigma,old.KSigma);
        Assert.AreEqual(core.EffectiveAtStart,old.EffectiveAtStart);Assert.AreEqual(core.EffectiveAtEnd,old.EffectiveAtEnd);
        var outstand=EffectivePlateReduction.Outstand(b,t,start,fy);var common=Common.Outstand(b,t,start,fy);
        Assert.AreEqual(common.Rho,outstand.Rho);Assert.AreEqual(common.EffectiveAtStart,outstand.EffectiveAtStart);
    }
}
