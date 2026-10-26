using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class BridgeSpliceEffectiveCompressionTests
{
    internal static WeldedIBeamSpliceDetailInput Input(bool ntc=false, double n=-200000, double v=0,
        double outerWidth=400, double thickness=7, double first=35, SpliceExposure exposure=SpliceExposure.ProtectedCarbonSteel,
        bool slenderWeb=false)
    {
        var code=ntc?ConnectionDesignCode.Ntc2018():ConnectionDesignCode.Eurocode3(1,1.25,1.25,"EC recommended");
        var flange=new SpliceFlangeCoverDetail(new(outerWidth,thickness,355,510),new(50,thickness,355,510),40,30,new(3,2,70,90,first));
        var section=new WeldedIBeamSpliceSection(new(450,30,355,510),new(320,16,355,510),new(450,30,355,510),8,8);
        var d=new WeldedIBeamSpliceForceInput(section,flange,flange,new(280,slenderWeb?4:25,355,510),
            new(3,3,70,90,70),60,10,new(n,v,0,0,0,0));
        var bolt=new ConnectionBolt(16,18,157,ConnectionBoltGrade.Class8_8,true);
        var bolts=new WeldedIBeamSpliceBoltInput(code,d,bolt,bolt,30,30,"Concomitant ULS","NTC SLE clearance");
        return new(new(ntc?IBeamSplicePlatePolicy.Ntc2018():IBeamSplicePlatePolicy.Eurocode3(code,1),bolts),exposure);
    }
    static WeldedIBeamSpliceSlipInput Slip(WeldedIBeamSpliceDetailInput p,SpliceBoltCategory category)
    {
        var code=p.Plates.Policy.Code;
        return new(code.IsNtc?BoltSlipPolicy.Ntc2018(code):BoltSlipPolicy.Eurocode3(code,1.25,1.1),p.Plates.Bolts,
            category==SpliceBoltCategory.B?BoltSlipLimitState.Serviceability:BoltSlipLimitState.Ultimate,
            Enum.GetValues<WeldedSpliceCover>().Select(c=>new WeldedSpliceFrictionSurface(c,.4,"Actual qualified surface")),
            BoltTighteningControl.Controlled,"Actual preloaded assemblies","Concomitant slip case");
    }
    internal static WeldedIBeamSpliceStandardInput Workflow(WeldedIBeamSpliceDetailInput p,SpliceBoltCategory category=SpliceBoltCategory.A,
        SplicePlateStabilityMethod method=SplicePlateStabilityMethod.EffectiveCompression)
        =>new(new[]{new WeldedSpliceStandardCase("U",WeldedIBeamSpliceStandardTests.Parents(p))},category,
            category==SpliceBoltCategory.A?null:new[]{new WeldedSpliceSlipCase("S",Slip(p,category),category==SpliceBoltCategory.C?"U":null)},
            SpliceBlockVerificationMethod.ActualCollinearForcesWithHierarchyFallback,SpliceFlangeFaceModel.EqualThicknessIndependentWidths,method);
    static double T(ConnectionVerification<WeldedIBeamSpliceDetailInput> r,string id)=>r.Trace.Single(x=>x.Name==id).Value;
    static ConnectionCheck C(ConnectionVerification<WeldedIBeamSpliceDetailInput> r,string id)=>r.Checks.Single(x=>x.Id==id);
    static void Near(double e,double a)=>Assert.AreEqual(e,a,Math.Max(1e-8,Math.Abs(e)*1e-10));

    [DataTestMethod]
    [DataRow(false,SpliceBoltCategory.A)][DataRow(false,SpliceBoltCategory.B)][DataRow(false,SpliceBoltCategory.C)]
    [DataRow(true,SpliceBoltCategory.A)][DataRow(true,SpliceBoltCategory.B)][DataRow(true,SpliceBoltCategory.C)]
    public void CompleteWorkflowAdmitsSlenderCompressedOutstandsWithAllOtherChecks(bool ntc,SpliceBoltCategory category)
    {
        var r=WeldedIBeamSpliceStandard.Verify(Workflow(Input(ntc),category));
        Assert.IsTrue(r.IsStaticResistanceComplete,string.Join(";",r.MissingRequirements));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.CheckSources.Where(x=>x.Failed).Select(x=>x.ComponentCheck?.Id)));
        Assert.IsNotNull(r.Cases[0].EffectiveCompression);
        Assert.IsTrue(r.CheckSources.Any(x=>x.Component=="EffectiveCoverCompression"));
        Assert.IsFalse(r.CheckSources.Any(x=>x.Component=="TopOuter.Compression"));
    }

    [TestMethod]
    public void DefaultPathAndPreviousOverloadsKeepTheClassThreeLimit()
    {
        var p=Workflow(Input(),method:SplicePlateStabilityMethod.CompactCovers);
        var r=WeldedIBeamSpliceStandard.Verify(p);
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);
        Assert.IsTrue(r.MissingRequirements.Any(x=>x.Contains("COMPRESSED_OUTSTAND_OUTSIDE_CLASS3")));
        Assert.IsNull(r.Cases[0].EffectiveCompression);
        var old=new WeldedIBeamSpliceStandardInput(p.Cases,p.Envelope.Category,p.Envelope.SlipCases,p.BlockMethod,p.FaceModel);
        Assert.AreEqual(SplicePlateStabilityMethod.CompactCovers,old.PlateStability);
    }

    [TestMethod]
    public void IndependentUniformOutstandCalculationMatchesEffectiveAreaAndSecondMoment()
    {
        var r=WeldedIBeamSpliceEffectiveCompression.Verify(Input());
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.Diagnostics));
        // Hand integration: two c=155mm outstands around bolt lines y=+/-45,
        // t=7, fy355. EN1993-1-5 Table4.2 k=.43, lambda=1.46137386329.
        Near(.5962566834121699,T(r,"TopOuter.LeftOutstandRho"));
        Near(92.41978592888633,T(r,"TopOuter.LeftOutstandEffectiveWidth"));
        Near(1923.8770030044084,T(r,"TopOuter.EffectiveArea"));
        Near(12110291.138332317,T(r,"TopOuter.EffectiveInertia"));
        Near(1671.8770030044084,T(r,"TopOuter.EffectiveNetArea"));
        Near(11593187.138332317,T(r,"TopOuter.EffectiveNetInertia"));
        Near(0,T(r,"TopOuter.EffectiveNetCentroid"));
    }

    [TestMethod]
    public void ClassFourSlendernessUsesEffectiveAreaAndGrossEulerInertia()
    {
        var r=WeldedIBeamSpliceEffectiveCompression.Verify(Input());
        Near(42,T(r,"TopOuter.EffectiveLength"));
        Near(.2720173027006162,T(r,"TopOuter.GrossSlenderness"));
        Near(.22547903417259788,T(r,"TopOuter.EffectiveSlenderness"));
        Near(.9870262070568665,T(r,"TopOuter.Chi"));
        Near(Math.PI*Math.PI*210000*(400*7*7*7/12d)/(42*42),T(r,"TopOuter.GrossWeakAxisNcr"));
        Near(21.846452686249286,T(r,"TopOuter.MaximumEffectiveCompression"));
        Near(355*.9870262070568665,C(r,"TopOuter.EffectiveInterFastenerBuckling").Resistance);
    }

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void EcAndNtcShareGeometryButKeepTheirOwnFactors(bool ntc)
    {
        var r=WeldedIBeamSpliceEffectiveCompression.Verify(Input(ntc));
        Near(1923.8770030044084,T(r,"TopOuter.EffectiveArea"));
        Near(355/(ntc?1.05:1),C(r,"TopOuter.EffectiveCompressionYield").Resistance);
        Near(355*.9870262070568665/(ntc?1.05:1),C(r,"TopOuter.EffectiveInterFastenerBuckling").Resistance);
    }

    [TestMethod]
    public void TensionDoesNotRemoveSteelForLocalCompressionBuckling()
    {
        var r=WeldedIBeamSpliceEffectiveCompression.Verify(Input(n:200000));
        Near(2800,T(r,"TopOuter.EffectiveArea")); Near(2548,T(r,"TopOuter.EffectiveNetArea"));
        Near(0,T(r,"TopOuter.HasCompression")); Near(1,T(r,"TopOuter.LeftOutstandRho"));
        Near(0,T(r,"TopOuter.MaximumEffectiveCompression"));
    }

    [TestMethod]
    public void CompactCoversRecoverTheExistingInterFastenerCalculation()
    {
        var p=Input(outerWidth:180);
        var old=WeldedIBeamSplicePlates.Verify(p.Plates).Covers.Single(c=>c.Cover==WeldedSpliceCover.TopOuter).LongitudinalCompression;
        var r=WeldedIBeamSpliceEffectiveCompression.Verify(p);
        Near(1260,T(r,"TopOuter.EffectiveArea"));
        Near(old.Trace.Single(x=>x.Name=="Chi").Value,T(r,"TopOuter.Chi"));
        Near(old.Checks.Single(x=>x.Id=="MaximumCompression.InterFastenerBuckling").Utilization,
            C(r,"TopOuter.EffectiveInterFastenerBuckling").Utilization);
    }

    [DataTestMethod][DataRow(-1e-15)][DataRow(-100000d)]
    public void EveryNonzeroCompressionActivatesTheSameGeometricReduction(double n)
    {
        var r=WeldedIBeamSpliceEffectiveCompression.Verify(Input(n:n));
        Near(.5962566834121699,T(r,"TopOuter.LeftOutstandRho"));
    }

    [TestMethod]
    public void OverloadedEffectiveSectionIsFailedRatherThanUnsupported()
    {
        var r=WeldedIBeamSpliceStandard.Verify(Workflow(Input(n:-20000000)));
        Assert.IsTrue(r.IsStaticResistanceComplete,string.Join(";",r.MissingRequirements));
        Assert.AreEqual(ConnectionStatus.Failed,r.Status); Assert.IsTrue(r.HasFailedComponent);
        Assert.IsTrue(r.CheckSources.Any(x=>x.Component=="EffectiveCoverCompression"&&x.Failed));
    }

    [TestMethod]
    public void MaximumBoltSpacingIsStillMandatory()
    {
        var r=WeldedIBeamSpliceStandard.Verify(Workflow(Input(first:60)));
        Assert.IsTrue(r.CheckSources.Any(x=>x.Component=="Detailing"&&x.Failed));
        Assert.AreNotEqual(ConnectionStatus.Passed,r.Status);
    }

    [TestMethod]
    public void ExposureEdgeLimitIsNotWaivedByEffectiveWidths()
    {
        var r=WeldedIBeamSpliceStandard.Verify(Workflow(Input(exposure:SpliceExposure.ExposedCarbonSteel)));
        Assert.AreEqual(ConnectionStatus.Failed,r.Status);
        Assert.IsTrue(r.CheckSources.Any(x=>x.Component=="Detailing"&&x.ComponentCheck.Id=="TopOuter.LowerEdge"&&x.Failed));
    }

    [DataTestMethod][DataRow(-1e-15)][DataRow(1e-15)]
    public void CompressionAndShearInALocallyReducedWebRemainExplicitlyUnsupported(double v)
    {
        // Wide outstands of the thin web require rho<1. No tiny-shear tolerance.
        var r=WeldedIBeamSpliceEffectiveCompression.Verify(Input(v:v,slenderWeb:true));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);
        Assert.IsTrue(r.Diagnostics.Any(x=>x.EndsWith("CLASS4_COMPRESSION_WITH_SHEAR_REQUIRES_INTERACTION")));
    }

    [TestMethod]
    public void ZeroActionsStillRequireAValidBoltedPhysicalDetail()
    {
        var r=WeldedIBeamSpliceEffectiveCompression.Verify(Input(n:0,first:1));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);
    }

    [TestMethod]
    public void InvalidModeIsRejectedAtInputConstruction()
        => Assert.ThrowsException<ArgumentOutOfRangeException>(()=>Workflow(Input(),method:(SplicePlateStabilityMethod)99));

    [TestMethod]
    public void ApplicationBuilderAndReportRetainTheSelectedStabilityMethod()
    {
        var p=Input();var section=WeldedIBeamSpliceStandardTests.Section(p).Plan.Input.Section;
        var q=BridgeSpliceApplication.CreateBolted(p,section,section,
            new[]{new SpliceActionCase("U",p.Plates.Bolts.Detail.Actions,"Actual compression case")},
            WeldedIBeamSpliceStandardTests.Welds(),"Actual global analysis",SpliceBoltCategory.A,
            SplicePlateStabilityMethod.EffectiveCompression,BridgeSpliceApplicationTests.Prepare,
            blockMethod:SpliceBlockVerificationMethod.ActualCollinearForcesWithHierarchyFallback,
            faceModel:SpliceFlangeFaceModel.EqualThicknessIndependentWidths);
        Assert.AreEqual(SplicePlateStabilityMethod.EffectiveCompression,q.PlateStability);
        var r=WeldedIBeamSpliceStandard.Verify(q); Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.MissingRequirements));
        string text=BridgeSpliceReport.FromBolted(r).ToText();
        StringAssert.Contains(text,"EffectiveCompression");StringAssert.Contains(text,"TopOuter.EffectiveArea=");
    }
}
