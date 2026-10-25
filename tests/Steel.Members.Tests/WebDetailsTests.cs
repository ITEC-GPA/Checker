using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class WebDetailsTests
{
    internal static SteelVerificationInput Input(bool ntc=false,double shear=1e5,double moment=1e7)
        =>new(new SteelSectionInput(new SteelSection(new SectionH(1050,8,500,25,500,25,"H"),new SteelMaterial("S355",210000,355,510),Section.SectionTypes.Welded,Section.FormedTypes.HotFinished),true),
            new SteelActions(0,0,shear,0,moment,0),ntc?SteelDesignContext.Ntc2018():new(SectionResistanceCode.Eurocode3(1,1.25,"test"),1),new SteelMemberInput(6000,6000,6000));
    internal static SteelWebBoundaryDetail Boundary(SteelVerificationInput i,bool end=false,double thickness=20,double weld=6,
        double load=1e5,double web=0,double x=0,double adjacent=1600,double rigid=0,double bearingWidth=350)
        =>new(i,end?SteelWebBoundaryKind.EndSupport:SteelWebBoundaryKind.Intermediate,end?0:adjacent,0,150,thickness,weld,weld,
            load,web,x,0,bearingWidth,120,end?100:0,rigid,"Same FEM combination; continuous full-height plates and double fillets; direct footprint, flange lateral restraints, matching weld consumables");
    private static SteelHWebPanelDetails Details(SteelVerificationInput i,SteelWebBoundaryDetail? left=null,SteelWebBoundaryDetail? right=null)
        =>SteelHWebPanelDetails.Evaluate(i,1600,i.Code.GammaM1!.Value,1,1.25,left??Boundary(i),right??Boundary(i));

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void ActualDetailsAreEvaluatedAndRetainedByCommonPipeline(bool ntc)
    {
        var i=Input(ntc);var d=Details(i);Assert.IsTrue(d.Passed,string.Join(";",d.Checks.Where(c=>c.Utilization>1).Select(c=>$"{c.Name}={c.Utilization}")));
        var a=SteelShearBucklingAssessment.FromVerifiedDetails(d);
        var r=SteelVerificationPipeline.Execute(SteelMethodSelector.Select(i),new[]{a});
        Assert.IsTrue(r.IsComplete);Assert.IsTrue(r.Checks.All(c=>c.Status!=SteelExecutionStatus.Failed));
        var check=r.Checks.Single(c=>c.Decision.Target==SteelCheckTarget.WebPanelDetails);
        Assert.AreEqual(d.Utilization,check.Utilization);Assert.AreSame(d,check.ShearAssessments.Single().PanelDetails);
        Assert.IsFalse(d.UsesRigidEndPost); // Conservative internal-panel curve, no inferred terminal anchorage.
    }
    [DataTestMethod][DataRow("thin")][DataRow("weld")][DataRow("bearing")][DataRow("reaction")][DataRow("deviation")]
    public void AFailedDetailCannotLeaveAQualifiedPanel(string mode)
    {
        var i=Input();var b=Boundary(i,thickness:mode=="thin"?3:20,weld:mode=="weld"?1:6,
            load:mode=="reaction"?1e7:1e5,web:mode=="deviation"?1e12:0,bearingWidth:mode=="bearing"?100:350);
        var d=Details(i,b);Assert.IsFalse(d.Passed);
        var r=SteelVerificationPipeline.Execute(SteelMethodSelector.Select(i),new[]{SteelShearBucklingAssessment.FromVerifiedDetails(d)});
        Assert.AreEqual(SteelExecutionStatus.Failed,r.Checks.Single(c=>c.Decision.Target==SteelCheckTarget.WebPanelDetails).Status);
        Assert.AreEqual(SteelExecutionStatus.Failed,r.Checks.Single(c=>c.Decision.Target==SteelCheckTarget.Shear2).Status);
    }
    [TestMethod]
    public void DetailsRemainCheckedAtZeroSectionShear()
    {
        var i=Input(shear:0,moment:0);var d=Details(i,Boundary(i,load:1e7));
        var r=SteelVerificationPipeline.Execute(SteelMethodSelector.Select(i),new[]{SteelShearBucklingAssessment.FromVerifiedDetails(d)});
        Assert.IsTrue(r.Checks.Any(c=>c.Status==SteelExecutionStatus.Failed));
    }
    [TestMethod]
    public void DifferentSnapshotCannotBeMixed()
    {
        var i=Input();Assert.ThrowsException<ArgumentException>(()=>Details(i,Boundary(Input())));
        var d=Details(i);Assert.ThrowsException<ArgumentException>(()=>SteelVerificationPipeline.Execute(SteelMethodSelector.Select(Input()),new[]{SteelShearBucklingAssessment.FromVerifiedDetails(d)}));
    }
    [TestMethod]
    public void RemovedPanelUsesSumAndLargerAdjacentShear()
    {
        var i=Input(shear:8e5);var d=Details(i,Boundary(i,adjacent:3200));
        double tau=(5.34+4*Math.Pow(1000d/4800,2))*Math.PI*Math.PI*210000/(12*.91)*Math.Pow(8d/1000,2);
        double demand=Math.Max(0,8e5-tau*8000)+1e5;
        Assert.AreEqual(demand,d.Stiffeners[0].Compression,1e-7);
        Assert.AreEqual(4800d,d.Trace.Single(v=>v.Name=="Left.RemovedPanelLength").Value);
    }
    [TestMethod]
    public void JrcPublishedMinimumRigidityIsReproducedIndependently()
    {
        // EUR22898EN 17.5.12 p220, hw=a=2000,tw8: required I=1.536e6 mm4.
        var r=Ec3WebStiffener.Stiffener(2000,8,2000,2000,2000,1000,1000,200,20,200,20,235,210000,1.1,0,0,0,0);
        Assert.AreEqual(1.536e6,r.RigidityRatio*r.InertiaOut,1e-7);
        // Independent three-rectangle inertia and effective strip 30 epsilon tw.
        double expected=240*Math.Pow(8,3)/12+2*(20*Math.Pow(200,3)/12+4000*Math.Pow(104,2));
        Assert.AreEqual(expected,r.InertiaOut,1e-7);Assert.AreEqual(9920d,r.Area,1e-8);
    }
    [TestMethod]
    public void SecondOrderBowAndActualEccentricityFollowIndependentSecantSolution()
    {
        var r=Ec3WebStiffener.Stiffener(1000,8,1000,2000,2000,1000,1000,150,20,150,20,235,210000,1,1e5,0,12,7);
        double ncrX=210000*r.InertiaOut*Math.Pow(Math.PI/1000,2),ncrZ=210000*r.InertiaIn*Math.Pow(Math.PI/1000,2);
        double mx=1e5*12/Math.Cos(Math.PI/2*Math.Sqrt(1e5/ncrX))+1e5*5/(1-1e5/ncrX);
        double mz=1e5*7/Math.Cos(Math.PI/2*Math.Sqrt(1e5/ncrZ))+1e5*5/(1-1e5/ncrZ);
        Assert.AreEqual(mx,r.MomentOut,1e-7);Assert.AreEqual(mz,r.MomentIn,1e-7);
    }
    [TestMethod]
    public void RigidEndBenefitRequiresTwoComputedPairs()
    {
        var i=Input();var d=Details(i,Boundary(i,end:true,rigid:200));
        Assert.IsTrue(d.Passed);Assert.IsTrue(d.UsesRigidEndPost);Assert.AreEqual(3,d.Stiffeners.Count);
        var bad=Details(i,Boundary(i,end:true,rigid:200,weld:1));Assert.IsFalse(bad.UsesRigidEndPost);
        Assert.ThrowsException<ArgumentException>(()=>Details(i,Boundary(i,end:true,rigid:100)));
    }
    [DataTestMethod][DataRow(double.NaN)][DataRow(double.PositiveInfinity)][DataRow(-1d)]
    public void InvalidDetailNumbersAreRejected(double value)
        =>Assert.ThrowsException<ArgumentOutOfRangeException>(()=>Boundary(Input(),load:value));

    [TestMethod]
    public void EndPatchMatchesThreeIndependentJrcMechanisms()
    {
        // JRC pp79-81 formulas6.10/6.11/6.12 and Fig6.6, mm/N/MPa.
        var r=Ec3WebPatchLoading.CalculateAtUnstiffenedEnd(1000,10,300,20,355,355,210000,2000,100,50,1.05);
        double le=2.9*210000*100/(2*355*1000),ly=le+20*Math.Sqrt(80);
        Assert.AreEqual(2.9,r.Kf,1e-12);Assert.AreEqual(548100d,r.Fcr,1e-7);
        Assert.AreEqual(50d,r.M2,1e-12);Assert.AreEqual(ly,r.LoadedLength,1e-10);
        double lambda=Math.Sqrt(ly*10*355/548100);
        Assert.AreEqual(.5/lambda*ly*10*355/1.05,r.Resistance,1e-7);
    }
    [DataTestMethod][DataRow(0d)][DataRow(300d)][DataRow(1000d)]
    public void EndPatchRetainsAllMechanismsAndCoefficientCap(double c)
    {
        var r=Ec3WebPatchLoading.CalculateAtUnstiffenedEnd(1000,10,300,20,355,355,210000,2000,0,c,1);
        double le=Math.Min(c,r.Kf*210000*100/(2*355*1000));
        double m1=30,m2=r.M2;
        double expected=Math.Min(40*(1+Math.Sqrt(m1+m2)),Math.Min(le+20*Math.Sqrt(m1/2+le*le/400+m2),le+20*Math.Sqrt(m1+m2)));
        Assert.AreEqual(expected,r.LoadedLength,1e-10);Assert.IsTrue(r.Kf<=6);
    }
    [TestMethod]
    public void EndPatchSameStateAdapterAndInvalidFootprint()
    {
        var i=Input(shear:0);var section=SteelVerificationPipeline.Execute(SteelMethodSelector.Select(i));
        var p=SteelHWebPatchAssessment.EvaluateAtUnstiffenedEnd(section,false,50000,1600,100,50,1,"Actual free end, non-following support, same FEM state");
        Assert.IsTrue(p.IsComplete);Assert.AreSame(i,p.Input);
        Assert.ThrowsException<ArgumentException>(()=>Ec3WebPatchLoading.CalculateAtUnstiffenedEnd(1000,10,300,20,355,355,210000,2000,100,1950,1));
        Assert.ThrowsException<ArgumentException>(()=>Ec3WebPatchLoading.Calculate(WebPatchLoadType.UnstiffenedEnd,1000,10,300,20,355,355,210000,2000,100,1));
    }
}
