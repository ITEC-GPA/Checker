using System.Globalization;
using System.Text.Json;
using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class BoltSlipTests
{
    private static BoltSlipPolicy Ec(double ultimate=1.25,double service=1.1)=>BoltSlipPolicy.Eurocode3(
        ConnectionDesignCode.Eurocode3(1,1.25,1.25,"Explicit SCI P358 benchmark factors"),ultimate,service);
    private static BoltSlipPolicy Ntc()=>BoltSlipPolicy.Ntc2018(ConnectionDesignCode.Ntc2018());
    private static BoltSlipInput Input(BoltSlipPolicy? policy=null,ConnectionBolt? bolt=null,
        BoltSlipLimitState state=BoltSlipLimitState.Ultimate,double shear=30000,double tension=0,int planes=1,double mu=.5,
        BoltTighteningControl tightening=BoltTighteningControl.Controlled,string? assembly="Fixture qualified EN14399 assembly and tightening",
        string? surface="Fixture qualified surface coefficient",string? combination="Fixture ULS/SLS concomitant load case",
        string? holes="Fixture NTC clearance evidence",double length=0)
        =>new(policy??Ec(),bolt??new(20,22,245,ConnectionBoltGrade.Class8_8,true),state,shear,tension,planes,mu,tightening,
            assembly!,surface!,combination!,holes,length);
    private static double Resistance(GPC.Checkers.Steel.Connections.ConnectionVerification<BoltSlipInput> r)
        =>r.Checks.Single(c=>c.Id=="Slip.Ultimate"||c.Id=="Slip.Serviceability").Resistance;
    private static double Trace(GPC.Checkers.Steel.Connections.ConnectionVerification<BoltSlipInput> r,string name)=>r.Trace.Single(t=>t.Name==name).Value;
    private static JsonDocument Fixture()=>JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","bolt-slip-independent.json")));
    private static void Equal(double expected,double actual)=>Assert.AreEqual(expected,actual,Math.Max(1e-8,Math.Abs(expected)*1e-11));
    private static void Unsupported(GPC.Checkers.Steel.Connections.ConnectionVerification<BoltSlipInput> r,string diagnostic)
    {
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);Assert.IsFalse(r.IsComplete);Assert.AreEqual(0,r.Checks.Count);
        Assert.IsNull(r.Utilization);CollectionAssert.Contains(r.Diagnostics.ToArray(),diagnostic);
    }
    public static IEnumerable<object[]> PublishedSlipCases()
    {
        using var doc=Fixture();
        foreach(var table in doc.RootElement.GetProperty("tables").EnumerateArray())
        foreach(var row in table.GetProperty("rows").EnumerateArray())
        for(int j=0;j<8;j++)
        {
            string printed=row[2][j].GetString()!;int decimalCount=printed.Contains('.')?printed.Length-printed.IndexOf('.')-1:0;
            yield return new object[]{table.GetProperty("table").GetString()!,row[0].GetDouble(),row[1].GetDouble(),
                table.GetProperty("grade").GetString()=="8.8"?ConnectionBoltGrade.Class8_8:ConnectionBoltGrade.Class10_9,
                Enum.Parse<BoltSlipLimitState>(table.GetProperty("state").GetString()!),new[]{.2,.3,.4,.5}[j/2],j%2+1,
                double.Parse(printed,CultureInfo.InvariantCulture)*1000,.5*Math.Pow(10,-decimalCount)*1000+1e-6};
        }
    }
    [DataTestMethod] [DynamicData(nameof(PublishedSlipCases),DynamicDataSourceType.Method)]
    public void EverySciPublishedSlipTableCellIsReproducedWithinPrintedPrecision(string table,double d,double area,
        ConnectionBoltGrade grade,BoltSlipLimitState state,double mu,int planes,double expected,double tolerance)
    {
        // M12 uses a normal1mm-clearance hole in this component; the source's0.85 shear reduction is irrelevant to its slip column.
        double hole=d+(d<=14?1:d<=24?2:3);
        var r=BoltSlip.Verify(Input(bolt:new(d,hole,area,grade,true),state:state,mu:mu,planes:planes,shear:1));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,table);
        Assert.AreEqual(expected,Resistance(r),tolerance,table+" per-bolt slip in N");
    }
    public static IEnumerable<object[]> PublishedNtcPreloads()
    {
        using var doc=Fixture();foreach(var row in doc.RootElement.GetProperty("ntcPreloads").GetProperty("rows").EnumerateArray())
        for(int j=0;j<2;j++)yield return new object[]{row[0].GetDouble(),row[1].GetDouble(),j==0?ConnectionBoltGrade.Class8_8:ConnectionBoltGrade.Class10_9,row[2+j].GetDouble()*1000};
    }
    [DataTestMethod] [DynamicData(nameof(PublishedNtcPreloads),DynamicDataSourceType.Method)]
    public void NominalPreloadMatchesEveryBoltInNtcTables(double d,double area,ConnectionBoltGrade grade,double published)
    {
        var r=BoltSlip.Verify(Input(policy:Ntc(),bolt:new(d,d+1,area,grade,true),shear:1));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status);
        // Both As and FpC are independently rounded in the publication. Propagate both printed intervals.
        // M36 8.8: As817 gives457.520kN, whereas the table prints457kN; do not force the formula to457.
        double halfAreaUnit=area==84.3?.05:.5;
        double halfForceUnit=published<100000?50:500;
        double tolerance=halfForceUnit+halfAreaUnit*.7*(grade==ConnectionBoltGrade.Class8_8?800:1000);
        Assert.AreEqual(published,Trace(r,"NominalPreload"),tolerance+1e-6,"NTC table printed area and preload precision");
    }

    [TestMethod]
    public void M36PreloadRetainsFormulaDespiteTheRoundedNtcTableEntry()
    {
        var r=BoltSlip.Verify(Input(policy:Ntc(),bolt:new(36,37,817,ConnectionBoltGrade.Class8_8,true),shear:1));
        Equal(457520,Trace(r,"NominalPreload"));
        Assert.AreNotEqual(457000,Trace(r,"NominalPreload"));
    }

    [DataTestMethod] [DataRow(false,BoltSlipLimitState.Ultimate,77568.0)]
    [DataRow(false,BoltSlipLimitState.Serviceability,88145.45454545454)]
    [DataRow(true,BoltSlipLimitState.Ultimate,77568.0)]
    [DataRow(true,BoltSlipLimitState.Serviceability,88145.45454545454)]
    public void ConcomitantTensionReducesSlipAtTheSpecifiedLimitState(bool ntc,BoltSlipLimitState state,double expected)
    {
        // Fp=137200N; residual=137200-0.8*20000=121200N; two interfaces, mu0.4.
        var r=BoltSlip.Verify(Input(policy:ntc?Ntc():Ec(),state:state,planes:2,mu:.4,tension:20000));
        Equal(137200,Trace(r,"NominalPreload"));Equal(121200,Trace(r,"ResidualClampingForce"));Equal(expected,Resistance(r));
        Equal(16000,r.Checks.Single(c=>c.Id=="Slip.PreloadReserve").Demand);
    }

    [TestMethod]
    public void NtcUncontrolledTighteningReducesPreloadBeforeSubtractingTension()
    {
        var r=BoltSlip.Verify(Input(policy:Ntc(),tightening:BoltTighteningControl.Uncontrolled,planes:2,mu:.4,tension:20000));
        Equal(1.1,Trace(r,"GammaM7"));Equal(137200/1.1,Trace(r,"PreloadForSlip"));
        Equal(69585.45454545454,Resistance(r));
        Assert.AreNotEqual(77568/1.1,Resistance(r),1e-6,"gammaM7 does not divide the external tensile action.");
    }
    [TestMethod]
    public void NtcUncontrolledPolicyCannotBeImportedIntoEurocode()
        =>Unsupported(BoltSlip.Verify(Input(tightening:BoltTighteningControl.Uncontrolled)),"EC_CONTROLLED_TIGHTENING_REQUIRED");

    [TestMethod]
    public void SeparateSlsAndUlsInputsKeepTheirOwnShearAndTensileActions()
    {
        var service=BoltSlip.Verify(Input(state:BoltSlipLimitState.Serviceability,shear:20000,tension:10000,combination:"SLS rare case1"));
        var ultimate=BoltSlip.Verify(Input(shear:30000,tension:25000,combination:"ULS case7"));
        Equal(58727.27272727272,Resistance(service));Equal(46880,Resistance(ultimate));
        Equal(20000,service.Checks.Single(c=>c.Id=="Slip.Serviceability").Demand);
        Equal(30000,ultimate.Checks.Single(c=>c.Id=="Slip.Ultimate").Demand);
        Assert.AreEqual("SLS rare case1",service.Input.ActionCombinationSource);
        Assert.AreEqual("ULS case7",ultimate.Input.ActionCombinationSource);
    }

    [DataTestMethod] [DataRow(0.0)] [DataRow(1e-8)] [DataRow(1000.0)]
    public void ExhaustedPreloadCannotGiveNegativeCapacityOrPassEvenWithZeroShear(double shear)
    {
        var r=BoltSlip.Verify(Input(shear:shear,tension:200000));
        Assert.AreEqual(ConnectionStatus.Failed,r.Status);Equal(0,Resistance(r));Equal(0,Trace(r,"ResidualClampingForce"));
        Assert.IsFalse(r.Checks.Single(c=>c.Id=="Slip.PreloadReserve").Passed);
        if(shear>0)Assert.IsTrue(double.IsPositiveInfinity(r.Checks.Single(c=>c.Id=="Slip.Ultimate").Utilization));
    }
    [DataTestMethod] [DataRow(0.0,ConnectionStatus.Passed)] [DataRow(1e-8,ConnectionStatus.Failed)]
    public void ExactlyZeroResidualClampingCapacityUsesZeroOverZeroConvention(double shear,ConnectionStatus expected)
    {
        var r=BoltSlip.Verify(Input(tension:171500,shear:shear));Equal(0,Resistance(r));Assert.AreEqual(expected,r.Status);
    }
    [DataTestMethod] [DataRow(1e-8)] [DataRow(.4)] [DataRow(1.0)]
    public void SmallShearIsNeverDropped(double shear)
    {
        var r=BoltSlip.Verify(Input(shear:shear));var c=r.Checks.Single(x=>x.Id=="Slip.Ultimate");
        Assert.AreEqual(shear/54880,c.Utilization,shear*1e-14);Assert.IsTrue(c.Utilization>0);
    }
    [DataTestMethod] [DataRow(.999999,ConnectionStatus.Passed)] [DataRow(1.000001,ConnectionStatus.Failed)]
    public void SlipThresholdControlsStatus(double factor,ConnectionStatus expected)
        =>Assert.AreEqual(expected,BoltSlip.Verify(Input(shear:54880*factor)).Status);

    [TestMethod]
    public void ExplicitEurocodeNationalFactorsAreIndependentOfMaterialStrengthFactors()
    {
        Equal(68600/1.5,Resistance(BoltSlip.Verify(Input(policy:Ec(1.5,1.2)))));
        Equal(68600/1.2,Resistance(BoltSlip.Verify(Input(policy:Ec(1.5,1.2),state:BoltSlipLimitState.Serviceability))));
    }
    [TestMethod]
    public void NtcSlipFactorsCannotBeOverriddenByTheEurocodeFactory()
    {
        Assert.ThrowsException<ArgumentException>(()=>BoltSlipPolicy.Eurocode3(ConnectionDesignCode.Ntc2018(),1,1));
        Assert.ThrowsException<ArgumentException>(()=>BoltSlipPolicy.Ntc2018(ConnectionDesignCode.Eurocode3(1,1.25,1.25,"EC")));
        Equal(1.25,Ntc().GammaM3Ultimate);Equal(1.1,Ntc().GammaM3Serviceability);
    }
    [DataTestMethod] [DataRow(0.0,1.0)] [DataRow(300.0,1.0)] [DataRow(350.0,.9875)] [DataRow(1300.0,.75)] [DataRow(2000.0,.75)]
    public void ExplicitLongJointPolicyRetainsItsBounds(double length,double beta)
    {
        var r=BoltSlip.Verify(Input(length:length));Equal(beta,Trace(r,"BetaLongJoint"));Equal(54880*beta,Resistance(r));
    }
    [DataTestMethod] [DataRow(1)] [DataRow(2)] [DataRow(4)]
    public void FrictionInterfacesAreNotConfusedWithNumberOfBolts(int planes)
        =>Equal(54880*planes,Resistance(BoltSlip.Verify(Input(planes:planes))));
    [TestMethod]
    public void ThreadPositionDoesNotChangeTensileAreaUsedForPreload()
    {
        var a=BoltSlip.Verify(Input());var b=BoltSlip.Verify(Input(bolt:new(20,22,245,ConnectionBoltGrade.Class8_8,false)));
        Equal(Resistance(a),Resistance(b));Equal(137200,Trace(b,"NominalPreload"));
    }
    [TestMethod] public void MissingAssemblyEvidenceIsUnsupported()=>Unsupported(BoltSlip.Verify(Input(assembly:null)),"PRELOADED_ASSEMBLY_QUALIFICATION_REQUIRED");
    [TestMethod] public void MissingSurfaceEvidenceIsUnsupported()=>Unsupported(BoltSlip.Verify(Input(surface:null)),"FRICTION_SURFACE_QUALIFICATION_REQUIRED");
    [TestMethod] public void MissingActionCaseIsUnsupported()=>Unsupported(BoltSlip.Verify(Input(combination:null)),"CONCOMITANT_LIMIT_STATE_COMBINATION_REQUIRED");
    [TestMethod] public void NtcHoleClearanceEvidenceRemainsRequired()
    {
        var r=BoltSlip.Verify(Input(policy:Ntc(),holes:null));Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);Assert.AreEqual(0,r.Checks.Count);
    }
    [TestMethod] public void OversizeHoleIsNotTreatedAsKsOne()
    {
        var r=BoltSlip.Verify(Input(bolt:new(20,26,245,ConnectionBoltGrade.Class8_8,true)));Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);Assert.AreEqual(0,r.Checks.Count);
    }
    [DataTestMethod] [DataRow(double.NaN)] [DataRow(double.PositiveInfinity)] [DataRow(-1.0)]
    public void InvalidActionRejected(double force)=>Assert.ThrowsException<ArgumentOutOfRangeException>(()=>Input(tension:force));
    [DataTestMethod] [DataRow(double.NaN)] [DataRow(double.PositiveInfinity)] [DataRow(0.0)] [DataRow(-.1)] [DataRow(1.1)]
    public void InvalidFrictionCoefficientRejected(double mu)=>Assert.ThrowsException<ArgumentOutOfRangeException>(()=>Input(mu:mu));
    [TestMethod] public void NoFrictionPlaneIsInvalid()=>Assert.ThrowsException<ArgumentOutOfRangeException>(()=>Input(planes:0));
    [TestMethod] public void SlipSuccessDoesNotClaimBoltOrPlateStrength()
    {
        var r=BoltSlip.Verify(Input());StringAssert.Contains(r.Scope,"No bolt tensile/shear strength");
        StringAssert.Contains(r.Scope,"Category B still requires ULS strength checks");Assert.AreEqual(2,r.Checks.Count);
    }
}
