using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class WeldedIBeamSpliceDetailTests
{
    internal static WeldedIBeamSpliceDetailInput Input(bool ntc=false,SpliceExposure exposure=SpliceExposure.ExposedCarbonSteel,
        WeldedIBeamSpliceForceInput? detail=null)=>new(WeldedIBeamSplicePlateTests.Input(ntc,detail),exposure);
    static void Close(double e,double a)=>WeldedIBeamSpliceTests.Close(e,a,1e-10);
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void ActualOuterPlyThicknessControlsTheMaximumPitch(bool ntc)
    {
        var r=WeldedIBeamSpliceDetailing.Verify(Input(ntc));Assert.AreEqual(ConnectionStatus.Passed,r.Detailing.Status);
        Close(168,r.Detailing.Checks.Single(c=>c.Id=="WebPositive.CentralPitch").Resistance);
        Close(200,r.Detailing.Checks.Single(c=>c.Id=="TopOuter.TransversePitch").Resistance);
        Close(12,r.Detailing.Trace.Single(c=>c.Name=="WebPositive.ThinnerOuterThickness").Value);
        Assert.IsFalse(r.CanQualifyLocalTransfer);Assert.IsFalse(r.CanQualifyWholeJoint);
    }
    [DataTestMethod][DataRow(84d,true)][DataRow(84.001,false)]
    public void CentralPitchBoundaryUsesBothSidesOfTheButt(double first,bool passes)
    {
        var r=WeldedIBeamSpliceDetailing.Verify(Input(detail:WeldedIBeamSplicePlateTests.Detail(first:first)));
        Assert.AreEqual(passes,r.Detailing.Checks.Single(c=>c.Id=="WebPositive.CentralPitch").Passed);
    }
    [DataTestMethod][DataRow(SpliceExposure.ProtectedCarbonSteel,ConnectionStatus.Passed)][DataRow(SpliceExposure.ExposedCarbonSteel,ConnectionStatus.Failed)]
    public void ExposureIsExplicitAndDoesNotApplyAFreeEdgeToTheWeldedWebBoundary(SpliceExposure exposure,ConnectionStatus expected)
    {
        var d=WeldedIBeamSplicePlateTests.Detail();
        var changed=new WeldedIBeamSpliceForceInput(d.Section,new(d.Top.Outer,d.Top.EachInner,d.Top.InnerGap,121,d.Top.Bolts),
            d.Bottom,d.EachWebCover,d.WebBolts,d.WebCoverEnd,d.ButtGap,d.Actions);
        var r=WeldedIBeamSpliceDetailing.Verify(Input(exposure:exposure,detail:changed));
        Assert.AreEqual(expected,r.Detailing.Status);Assert.IsFalse(r.Detailing.Checks.Any(c=>c.Id.Contains("ParentWebEdge")));
    }
    [TestMethod]
    public void UnprotectedWeatheringSteelCannotUseOrdinaryExposedSteelLimits()
    {
        var r=WeldedIBeamSpliceDetailing.Verify(Input(exposure:SpliceExposure.UnprotectedWeatheringSteel));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Detailing.Status);Assert.AreEqual(0,r.FaceTransfers.Count);
    }
    [DataTestMethod][DataRow(WeldedSpliceCover.TopOuter,10d)][DataRow(WeldedSpliceCover.TopInnerPositive,-10d)]
    [DataRow(WeldedSpliceCover.BottomOuter,-10d)][DataRow(WeldedSpliceCover.BottomInnerNegative,10d)]
    [DataRow(WeldedSpliceCover.WebNegative,6d)][DataRow(WeldedSpliceCover.WebPositive,-6d)]
    public void FaceOffsetsAndTransportMomentsHaveThePhysicalSigns(WeldedSpliceCover cover,double e)
    {
        var r=WeldedIBeamSpliceDetailing.Verify(Input(detail:WeldedIBeamSplicePlateTests.Detail(new(1e6,2e5,0,0,0,100e6))));
        var transfer=r.FaceTransfers.Single(x=>x.Cover==cover&&x.Cut.X==0);
        Close(e,transfer.ContactOffsetW);Close(e*transfer.Cut.N,transfer.BendingV);Close(-e*transfer.Cut.V,transfer.TorsionU);
        Assert.IsFalse(r.CanQualifyLocalTransfer);
    }
    [TestMethod]
    public void ElasticFaceStrainDiagnosticUsesBothActualAreasNotAHalfForceCompatibilityClaim()
    {
        var r=WeldedIBeamSpliceDetailing.Verify(Input());double n=1e6*12000/29120;
        double eo=n/2/(210000*7200),ei=n/2/(210000*5600),k=(eo-ei)/50;
        double T(string name)=>r.ElasticFaceDiagnostics.Single(x=>x.Name=="Top."+name).Value;
        Close(25d*(7200-5600)/12800,T("AreaCentroidOffset"));Close(eo,T("OuterAxialStrain"));Close(ei,T("InnerAxialStrain"));
        Close(k,T("FaceStrainGradient"));Close(eo-k*25-n/(210000*12000d),T("AtParentCentroidStrainJump"));
        Assert.AreNotEqual(0,T("StrainGradientJump"));Assert.IsFalse(r.CanQualifyLocalTransfer);
    }
    [TestMethod]
    public void ZeroLoadsDoNotCertifyCompatibilityOrDiscardDetailRequirements()
    {
        var r=WeldedIBeamSpliceDetailing.Verify(Input(detail:WeldedIBeamSplicePlateTests.Detail(new(0,0,0,0,0,0),first:90)));
        Assert.AreEqual(ConnectionStatus.Failed,r.Detailing.Status);Assert.IsFalse(r.CanQualifyLocalTransfer);
        Assert.IsTrue(r.FaceTransfers.All(t=>t.BendingV==0&&t.TorsionU==0));
    }
    [TestMethod]
    public void NullAndUnknownExposureAreRejected()
    {
        Assert.ThrowsException<ArgumentNullException>(()=>WeldedIBeamSpliceDetailing.Verify(null!));
        Assert.ThrowsException<ArgumentOutOfRangeException>(()=>new WeldedIBeamSpliceDetailInput(WeldedIBeamSplicePlateTests.Input(),(SpliceExposure)99));
    }
}
