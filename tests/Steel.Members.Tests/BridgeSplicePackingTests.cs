using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Steel.Members.Tests;
[TestClass]
public class BridgeSplicePackingTests
{
    static WeldedIBeamSpliceForceInput Detail(WeldedSplicePacking? packing=null, PlanarConnectionActions? actions=null)
    {
        var d=BridgeSpliceUnequalWidthTests.Detail(360,actions??new(2e5,-1e5,0,0,0,50e6));
        // Leave40mm above/below the web covers for the inner flange plates and packings.
        var section=new WeldedIBeamSpliceSection(d.Section.TopFlange,new(360,16,355,510),d.Section.BottomFlange,8,8);
        return new(section,d.Top,d.Bottom,d.EachWebCover,d.WebBolts,d.WebCoverEnd,d.ButtGap,d.Actions,
            packing??new(20,5,10,0,5,"Single fitted full-footprint parallel plates; bolt grip checked"));
    }
    static WeldedIBeamSpliceSlipInput Slip(bool ntc,WeldedIBeamSpliceForceInput d,BoltSlipLimitState state,
        bool interfaces=true,double secondMu=.3)
    {
        var q=WeldedIBeamSpliceSlipTests.Input(ntc,state,d);
        return new(q.Policy,q.Bolts,state,q.Surfaces.Select(s=>interfaces&&d.Packing.Thickness(s.Cover)>0
            ?new WeldedSpliceFrictionSurface(s.Cover,.4,"Cover/packing",secondMu,"Packing/parent"):s),
            q.TighteningControl,q.AssemblyQualificationSource,q.ActionCombinationSource);
    }
    static WeldedIBeamSpliceStandardInput Workflow(bool ntc,SpliceBoltCategory cat, double scale=1)
    {
        var d=Detail(actions:new(2e5*scale,-1e5*scale,0,0,0,50e6*scale));
        var p=WeldedIBeamSpliceStandardTests.Input(ntc,d);
        return new(new[]{new WeldedSpliceStandardCase("U",WeldedIBeamSpliceStandardTests.Parents(p))},cat,
            cat==SpliceBoltCategory.A?null:new[]{new WeldedSpliceSlipCase("S",Slip(ntc,d,cat==SpliceBoltCategory.B
                ?BoltSlipLimitState.Serviceability:BoltSlipLimitState.Ultimate),cat==SpliceBoltCategory.C?"U":null)},
            SpliceBlockVerificationMethod.ActualCollinearForcesWithHierarchyFallback,SpliceFlangeFaceModel.PackedIndependentFaceDimensions);
    }
    [DataTestMethod]
    [DataRow(false,SpliceBoltCategory.A)][DataRow(false,SpliceBoltCategory.B)][DataRow(false,SpliceBoltCategory.C)]
    [DataRow(true,SpliceBoltCategory.A)][DataRow(true,SpliceBoltCategory.B)][DataRow(true,SpliceBoltCategory.C)]
    public void StandardWorkflowQualifiesWithEveryRealInterface(bool ntc,SpliceBoltCategory cat)
    {
        var r=WeldedIBeamSpliceStandard.Verify(Workflow(ntc,cat));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.MissingRequirements));
        Assert.IsTrue(r.IsStaticResistanceComplete);
        Assert.IsTrue(r.Cases[0].Components.Bolts.ComponentAssessment.Trace.Any(t=>t.Name.EndsWith("GripThickness")));
    }
    [TestMethod] public void IndependentFreeBodyUsesActualPackingOffsets()
    {
        var d=Detail(actions:new(200000,0,0,0,0,0));var r=WeldedIBeamSpliceForces.Calculate(d);
        Assert.IsTrue(r.IsBalanced,string.Join(";",r.Diagnostics));
        // Independent free body: N=a_outer*N_outer? No area split: forces follow the lever arms.
        double n=200000*d.Section.TopFlange.Width*d.Section.TopFlange.Thickness/d.Section.Area;
        double yo=(d.Section.TopFlange.Thickness+d.Top.Outer.Thickness)/2+20;
        double yi=(d.Section.TopFlange.Thickness+d.Top.EachInner.Thickness)/2+5;
        var g=r.CoverGroups.Where(g=>g.Side==SpliceSide.Left).ToArray();
        double fo=g.Single(g=>g.Cover==WeldedSpliceCover.TopOuter).Distribution.Forces.Sum(f=>f.Fx);
        double fi=g.Where(g=>g.Cover==WeldedSpliceCover.TopInnerPositive||g.Cover==WeldedSpliceCover.TopInnerNegative)
            .Sum(g=>g.Distribution.Forces.Sum(f=>f.Fx));
        Close(n*yi/(yo+yi),fo);Close(n*yo/(yo+yi),fi);Close(0,yo*fo-yi*fi,1e-7);
        Assert.IsTrue(r.Trace.Where(t=>t.Name.Contains(".Residual.")).All(t=>Math.Abs(t.Value)<1e-7));
    }
    [DataTestMethod][DataRow(0d,1d)][DataRow(8d,1d)][DataRow(20d,9d/10.5d)]
    public void ThickerPackingReducesBothPlanesPerEc361EvenOnTheUnpackedFace(double thickness,double beta)
    {
        var d=Detail(new(thickness,0,0,0,0,"Detail"));var q=WeldedIBeamSpliceTests.BoltInput(detail:d);
        var r=WeldedIBeamSpliceBolts.Verify(q).ComponentAssessment;Assert.IsTrue(r.IsComplete);
        foreach(string face in new[]{"TopOuter","TopInnerPositive","TopInnerNegative"})
        {
            double resistance=r.Checks.First(c=>c.Id.Contains("."+face+".")&&c.Id.EndsWith("ShearPlane")).Resistance;
            // M24 8.8 threaded, As=353; joint lengths below15d in this fixture.
            Close(.6*800*353/1.25*beta,resistance);
        }
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void SerialFrictionInterfacesEachReceiveFullForceAndFullPreload(bool ntc)
    {
        var p=Slip(ntc,Detail(),BoltSlipLimitState.Ultimate);var r=WeldedIBeamSpliceSlip.Verify(p);
        Assert.IsTrue(r.IsComplete,string.Join(";",r.Diagnostics));
        var a=r.Checks.Single(c=>c.Id=="Left.TopOuter.Bolt0.CoverToPacking.Slip.Ultimate");
        var b=r.Checks.Single(c=>c.Id=="Left.TopOuter.Bolt0.PackingToParent.Slip.Ultimate");
        Close(a.Demand,b.Demand);Close(.7*800*353*.4/1.25,a.Resistance);Close(.7*800*353*.3/1.25,b.Resistance);
        Assert.IsFalse(r.Checks.Any(c=>c.Id.Contains("BottomInner")&&c.Id.Contains("PackingToParent")));
    }
    [TestMethod] public void WeakSerialSurfaceFailsEvenWithStrongOtherInterface()
    {
        var r=WeldedIBeamSpliceSlip.Verify(Slip(false,Detail(),BoltSlipLimitState.Ultimate,secondMu:.00001));
        Assert.AreEqual(ConnectionStatus.Failed,r.Status);Assert.IsTrue(r.Checks.Any(c=>!c.Passed&&c.Id.Contains("PackingToParent")));
    }
    [TestMethod] public void MissingSecondInterfaceCannotPass()
    {
        var r=WeldedIBeamSpliceSlip.Verify(Slip(false,Detail(),BoltSlipLimitState.Ultimate,false));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Status);Assert.IsTrue(r.Diagnostics.Any(s=>s.Contains("INTERFACES_MISMATCH")));
    }
    [TestMethod] public void ModelBuilderAndReportKeepPackingAndBothSurfaceQualifications()
    {
        var d=Detail();var template=WeldedIBeamSpliceStandardTests.Input(detail:d);
        var section=WeldedIBeamSpliceStandardTests.Section(template).Plan.Input.Section;
        var p=BridgeSpliceApplication.CreateBolted(template,section,section,BridgeSpliceApplicationTests.Actions(),
            WeldedIBeamSpliceStandardTests.Welds(),"Global analysis",SpliceBoltCategory.C,BridgeSpliceApplicationTests.Prepare,
            Slip(false,d,BoltSlipLimitState.Ultimate),blockMethod:SpliceBlockVerificationMethod.ActualCollinearForcesWithHierarchyFallback,
            faceModel:SpliceFlangeFaceModel.PackedIndependentFaceDimensions);
        Assert.IsTrue(p.Cases.All(c=>ReferenceEquals(d.Packing,c.Parents.Detail.Plates.Bolts.Detail.Packing)));
        var r=WeldedIBeamSpliceStandard.Verify(p);Assert.AreEqual(ConnectionStatus.Passed,r.Status,string.Join(";",r.MissingRequirements));
        var note=BridgeSpliceReport.FromBolted(r).Notes.First(n=>n.Contains("TopOuter:PackingToParent:mu="));
        double mu=double.Parse(note.Split("mu=")[1].Split(';')[0],System.Globalization.CultureInfo.InvariantCulture);
        Assert.AreEqual(.3,mu);StringAssert.Contains(note,"source=Packing/parent");
    }
    [TestMethod] public void EnvelopeBindsThePackingDimensions()
    {
        var p=Workflow(false,SpliceBoltCategory.C);var different=Detail(new(19,5,10,0,5,"Other detail"));
        var q=new WeldedIBeamSpliceStandardInput(p.Cases,p.Envelope.Category,
            new[]{new WeldedSpliceSlipCase("S",Slip(false,different,BoltSlipLimitState.Ultimate),"U")},p.BlockMethod,p.FaceModel);
        var r=WeldedIBeamSpliceStandard.Verify(q);Assert.IsFalse(r.IsStaticResistanceComplete);
        Assert.IsTrue(r.MissingRequirements.Any(s=>s.Contains("MISMATCH")));
    }
    [TestMethod] public void ExistingUnpackedPoliciesDoNotSilentlyAcceptPacking()
    {
        var q=WeldedIBeamSpliceStandardTests.Input(detail:Detail());
        foreach(var mode in new[]{SpliceFlangeFaceModel.BalancedEqualFaces,SpliceFlangeFaceModel.EqualThicknessIndependentWidths,SpliceFlangeFaceModel.IndependentFaceDimensions})
            Assert.IsTrue(WeldedIBeamSpliceStandardModel.Verify(q,mode).Diagnostics.Contains("EXPLICIT_PACKED_FACE_MODEL_REQUIRED"));
    }
    [TestMethod] public void PackingCannotIntersectTheWebCover()
    {
        var r=WeldedIBeamSpliceForces.Calculate(Detail(new(0,1000,0,0,0,"Invalid geometry")));
        Assert.IsFalse(r.IsBalanced);Assert.IsTrue(r.Diagnostics.Contains("PACKED_INNER_FLANGES_INTERSECT_WEB_COVERS"));
    }
    [TestMethod] public void MissingExecutionPreventsWholeJointQualification()
    {
        var q=WeldedIBeamSpliceStandardTests.Input(detail:Detail(new(20,0,0,0,0,null!)));
        Assert.IsTrue(WeldedIBeamSpliceStandardModel.Verify(q,SpliceFlangeFaceModel.PackedIndependentFaceDimensions)
            .Diagnostics.Contains("PACKING_EXECUTION_SOURCE_REQUIRED"));
    }
    [TestMethod] public void NoPackingKeepsExistingUtilizationsExactly()
    {
        var a=BridgeSpliceUnequalWidthTests.Workflow(false,SpliceBoltCategory.C,360);
        var b=new WeldedIBeamSpliceStandardInput(a.Cases,a.Envelope.Category,a.Envelope.SlipCases,a.BlockMethod,SpliceFlangeFaceModel.PackedIndependentFaceDimensions);
        CollectionAssert.AreEqual(WeldedIBeamSpliceStandard.Verify(a).CheckSources.Select(c=>c.Utilization).ToArray(),
            WeldedIBeamSpliceStandard.Verify(b).CheckSources.Select(c=>c.Utilization).ToArray());
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void ReversalAndOverloadArePreserved(bool ntc)
    {
        Assert.AreEqual(ConnectionStatus.Passed,WeldedIBeamSpliceStandard.Verify(Workflow(ntc,SpliceBoltCategory.C,-1)).Status);
        Assert.AreEqual(ConnectionStatus.Failed,WeldedIBeamSpliceStandard.Verify(Workflow(ntc,SpliceBoltCategory.C,100)).Status);
    }
    [DataTestMethod][DataRow(-1d)][DataRow(double.NaN)][DataRow(double.PositiveInfinity)]
    public void InvalidPackingRejected(double t)=>Assert.ThrowsException<ArgumentOutOfRangeException>(()=>new WeldedSplicePacking(t,0,0,0,0,"Detail"));
    static void Close(double expected,double actual,double tolerance=1e-8)=>Assert.AreEqual(expected,actual,Math.Max(tolerance,Math.Abs(expected)*1e-10));
}
