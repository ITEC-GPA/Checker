using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Steel.Members.Tests;
[TestClass]
public class DoubleAngleCleatSupportContactTests
{
    private static DoubleAngleCleatSupportContactInput Input(bool ntc = false, double v = 450000, double h = 20,
        double width = 15, double tf = 20, double dm = 32, string source = "Actual upper20x15mm short-leg contact patch")
    {
        var c = new DoubleAngleCleatComponentInput(ntc ? ConnectionDesignCode.Ntc2018() : ConnectionDesignCode.Eurocode3(1,1.25,1.25,"Explicit factors"),
            new(90,10,275,430),11,new(547,210,9,15.7,24,11700,275,430),new(20,22,245,ConnectionBoltGrade.Class8_8,true),
            6,new(0,-v,0,0,0,0),true,holeClearanceServiceabilitySource:"SLE evidence");
        var column = new ConnectionRolledISection(300,250,10,tf,12,2*250*tf+(300-2*tf)*10+4*(1-Math.PI/4)*144,275,430);
        return new(new(c,column,275,430,100,100,"Column drawing"),h,width,dm,source);
    }
    private static double T(DoubleAngleCleatSupportContactResult r,string key) => r.ContactAndShear.Trace.Single(x=>x.Name==key).Value;
    private static void Eq(double e,double a) => Assert.AreEqual(e,a,1e-9*Math.Max(1,Math.Abs(e)));
    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void IndependentFreeBodyBalancesContactAndEccentricShear(bool ntc)
    {
        var r=DoubleAngleCleatSupportContact.Verify(Input(ntc));
        Assert.AreEqual(ConnectionStatus.Passed,r.ContactAndShear.Status,string.Join(";",r.ContactAndShear.Diagnostics));
        // Weynand et al Fig9, six rows z=30,100,170,240,310,380mm.
        // Hand sum z^2=337900mm2, M=225kN*50mm. This tests equilibrium,
        // not a table claiming a whole-joint capacity.
        Eq(337900,T(r,"EachCleat.BoltLeverSquares")); Eq(11250000,T(r,"EachCleat.Moment"));
        double sum=0,moment=0,vertical=0;
        for(int i=1;i<=6;i++)
        {
            double f=T(r,$"Right.Bolt{i}.TransverseShear");
            Eq(-f,T(r,$"Left.Bolt{i}.TransverseShear"));
            sum+=f; moment+=f*T(r,$"Right.Bolt{i}.ContactLever"); vertical+=T(r,$"Right.Bolt{i}.VerticalShear");
        }
        Eq(11250000,moment); Eq(-225000,vertical); Eq(sum,T(r,"EachCleat.ContactForce"));
        Eq(12651.67209233501,T(r,"Right.Bolt6.TransverseShear"));
        Assert.IsFalse(r.CanQualifyWholeJoint); Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);
    }
    [TestMethod] public void PunchingHierarchyUsesActualHeadNotWasherOrTransverseShear()
    {
        var r=DoubleAngleCleatSupportContact.Verify(Input());
        Eq(141120,r.PunchingHierarchy[0].Demand);
        Eq(.6*Math.PI*32*10*430/1.25,r.PunchingHierarchy[0].Resistance);
        Assert.IsTrue(r.PunchingExcludedBeforeBoltTensionResistance);
        Assert.IsFalse(r.ContactAndShear.Checks.Any(x=>x.Id.Contains("Tension")));
    }
    [TestMethod] public void WeakPunchingHierarchyIsInsufficientEvidenceNotAnActualTensionFailure()
    {
        var r=DoubleAngleCleatSupportContact.Verify(Input(v:1000,tf:5));
        Assert.IsFalse(r.PunchingExcludedBeforeBoltTensionResistance);
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);
    }
    [TestMethod] public void ContactFailureRemainsVisibleAlongsideAnUnqualifiedWholeJoint()
    { var r=DoubleAngleCleatSupportContact.Verify(Input(width:1)); Assert.AreEqual(ConnectionStatus.Failed,r.Status); }
    [TestMethod] public void ExistingFailedCleatsAreNotHiddenWhenSupplementaryGeometryIsUnsupported()
    { var r=DoubleAngleCleatSupportContact.Verify(Input(v:1e7,h:40)); Assert.AreEqual(ConnectionStatus.Failed,r.Status); }
    [DataTestMethod] [DataRow(40d,15d,32d)] [DataRow(20d,20d,32d)] [DataRow(20d,15d,50d)] [DataRow(20d,15d,22d)]
    public void UnsupportedContactOrHardwareDoesNotInventAForceDistribution(double h,double b,double dm)
    { var r=DoubleAngleCleatSupportContact.Verify(Input(h:h,width:b,dm:dm)); Assert.AreEqual(ConnectionStatus.NotSupported,r.ContactAndShear.Status); }
    [TestMethod] public void SourceIsRequired()
    { Assert.AreEqual(ConnectionStatus.NotSupported,DoubleAngleCleatSupportContact.Verify(Input(source:"")).ContactAndShear.Status); }
    [TestMethod] public void ZeroLoadFiniteAndImmutable()
    {
        var r=DoubleAngleCleatSupportContact.Verify(Input(v:0));
        Assert.AreEqual(ConnectionStatus.Passed,r.ContactAndShear.Status); Assert.IsTrue(r.ContactAndShear.Trace.All(x=>double.IsFinite(x.Value)));
        Assert.ThrowsException<NotSupportedException>(()=>((IList<ConnectionCheck>)r.PunchingHierarchy).Clear());
    }
    [TestMethod] public void SmallerPatchRaisesOnlyLocalContactStress()
    {
        var a=DoubleAngleCleatSupportContact.Verify(Input()); var b=DoubleAngleCleatSupportContact.Verify(Input(width:7.5));
        Eq(2*a.ContactAndShear.Checks[0].Demand,b.ContactAndShear.Checks[0].Demand);
        Eq(T(a,"EachCleat.ContactForce"),T(b,"EachCleat.ContactForce"));
    }
    [TestMethod] public void VanishingPatchReturnsNumericDiagnostic()
    { Assert.AreEqual(ConnectionStatus.NotSupported,DoubleAngleCleatSupportContact.Verify(Input(width:1e-310)).ContactAndShear.Status); }
}
