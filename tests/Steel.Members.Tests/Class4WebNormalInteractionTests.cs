using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Sections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class Class4WebNormalInteractionTests
{
    private static SteelVerificationInput Input(bool ntc,double n,double m=0,double v=0,double minor=0)
    {
        var b=CommonSectionCoverageTests.Input(new SectionH(840,6,300,20,300,20,"slender web H"),ntc,fy:355);
        return new(b.Section,new(n,0,v,0,m,minor),b.Code,new SteelMemberInput(6000,6000,6000));
    }
    private static SteelShearBucklingAssessment Assessment(SteelVerificationInput i,bool ntc,bool extended=true)
    {
        var r=SteelShearBucklingAssessment.EvaluateNonRigidHWeb(i,ntc?1.05:1,1,
            "Ordinary I web continuously attached to flanges; no longitudinal stiffeners or patch load; supports and attachments independently checked for this state");
        return extended?r.WithConservativeInPlaneNormalInteraction():r;
    }
    // EN1993-1-5 4.4 Table4.1: b=800, t=6, psi=1, ksigma=4.
    // Both outstands are compact: c/t=147/20=7.35 < 14*epsilon.
    private static double EffectiveArea()
    {double lambda=800d/6/(56.8*Math.Sqrt(235d/355));return 12000+4800*(lambda-.22)/(lambda*lambda);}
    private static double WebResistance(bool ntc)
        =>.83*86.4*36*Math.Sqrt(235d/355)*355/(Math.Sqrt(3)*(ntc?1.05:1));
    private static double Chi(double l,double alpha)
    {double phi=(1+alpha*(l-.2)+l*l)/2;return 1/(phi+Math.Sqrt(phi*phi-l*l));}

    [TestMethod]
    public void PublishedJrcFullyCompressedPlateInteractionUsesTheSharedKernel()
    {
        // JRC/ECCS EUR22898EN (2007), example16, pp189-190: eta1=.709,
        // eta3=.544 and printed result .717. No claim about bolted restraints.
        Assert.AreEqual(.717,Ec3WebPanelShear.ConservativeNormalInteraction(.709,.544),.0005);
        Assert.AreEqual(.716744,Ec3WebPanelShear.ConservativeNormalInteraction(.709,.544),1e-14);
    }

    public static IEnumerable<object[]> Cases=>from ntc in new[]{false,true}
        from ratio in new[]{0d,.4,.5,.500001,.75,1d,1.1} from sign in new[]{-1d,1d}
        select new object[]{ntc,ratio,sign};

    [DataTestMethod,DynamicData(nameof(Cases))]
    public void FullyCompressedClass4SectionMatchesIndependentEffectiveWidthAndEquation71(bool ntc,double ratio,double sign)
    {
        double n=-.1*EffectiveArea()*355/(ntc?1.05:1);
        var i=Input(ntc,n,v:sign*ratio*WebResistance(ntc));
        var r=SteelVerificationPipeline.Execute(SteelMethodSelector.Select(i),new[]{Assessment(i,ntc)});
        var normal=r.Checks.First();
        Assert.IsTrue(r.IsComplete,normal.Message);
        double interaction=.1+(ratio<=.5?0:Math.Pow(2*ratio-1,2));
        Assert.AreEqual(Math.Max(ratio,interaction),normal.Utilization!.Value,2e-9);
        Assert.AreEqual(EffectiveArea(),normal.Trace.Single(t=>t.Name=="AreaEffective").Value,1e-5);
        Assert.AreEqual(interaction<=1&&ratio<=1,r.Checks.Where(c=>c.IsEvaluated).All(c=>c.Status==SteelExecutionStatus.Passed));
    }

    private static SteelMemberAnalysis Member(SteelVerificationInput i,bool extended=true)
    {
        var p=new SteelBeamColumnParameters(SteelBucklingCurve.B,SteelBucklingCurve.C,false,1,1,1,
            "Conservative same-state extrema","Prismatic ordinary beam-column","T excluded","FT excluded",
            lateralTorsionalExclusionSource:"Continuous twist restraint");
        if(extended)p=p.WithConservativeClass4WebInteraction();
        double nrk=EffectiveArea()*355;
        return SteelMemberAnalysis.BeamColumn(i,"one case","same web and member",p,new[]{
            new SteelStabilityValue(SteelStabilityQuantity.Ncr1,nrk/.36,SteelStabilityUnit.N,"gross elastic eigenvalue"),
            new SteelStabilityValue(SteelStabilityQuantity.Ncr2,nrk/1.44,SteelStabilityUnit.N,"gross elastic eigenvalue")},4,"uniform-compression class4");
    }

    [DataTestMethod][DataRow(false,.4)][DataRow(true,.4)][DataRow(false,.75)][DataRow(true,.75)]
    [DataRow(false,.95)][DataRow(true,.95)]
    public void MemberRetainsGrossCriticalForcesAndOriginalEffectiveAreaUnderHighShear(bool ntc,double ratio)
    {
        double nrk=EffectiveArea()*355;
        var i=Input(ntc,-.03*nrk,v:ratio*WebResistance(ntc));var a=Member(i);
        var r=SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(a),new[]{Assessment(i,ntc)}).Checks.Single();
        Assert.IsTrue(r.IsEvaluated,r.Message);
        double expected=.03*(ntc?1.05:1)/((1-ratio)*Chi(1.2,.49));
        Assert.AreEqual(expected,r.Utilization!.Value,expected*2e-9);
        Assert.AreEqual(nrk,r.Trace.Single(t=>t.Name=="NRk").Value,nrk*1e-9);
        Assert.AreEqual(nrk/1.44,r.Trace.Single(t=>t.Name=="Ncr2").Value,nrk*1e-9);
        Assert.AreEqual(1-ratio,r.Trace.Single(t=>t.Name=="Class4WebInteraction.NormalResistanceFactor").Value,1e-12);
    }

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void ConcomitantBendingAndCompressionRemainInBothSectionAndMemberChecks(bool ntc)
    {
        var i=Input(ntc,-1e5,1e7,.75*WebResistance(ntc));var web=Assessment(i,ntc);
        var section=SteelVerificationPipeline.Execute(SteelMethodSelector.Select(i,classification:new(i,4,"uniform class")),new[]{web});
        Assert.IsTrue(section.IsComplete,section.Checks.First().Message);
        var member=SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(Member(i)),new[]{web}).Checks.Single();
        Assert.IsTrue(member.IsEvaluated,member.Message);
        Assert.IsTrue(member.Trace.Single(t=>t.Name=="Interaction1").Value>0);
        Assert.IsTrue(member.Trace.Single(t=>t.Name=="Weff1Min").Value>0);
        Assert.IsTrue(member.Trace.Single(t=>t.Name=="Class4WebInteraction.MbRd").Value>0);
    }

    [TestMethod]
    public void TheExtensionCannotInventSupportsOrAcceptDifferentPlanes()
    {
        var i=Input(false,-1e5,v:.75*WebResistance(false));var a=Member(i);
        var plan=SteelMethodSelector.SelectMember(a);
        Assert.IsFalse(SteelVerificationPipeline.Execute(plan).IsComplete);
        Assert.IsFalse(SteelVerificationPipeline.Execute(plan,new[]{Assessment(i,false,false)}).IsComplete);
        Assert.ThrowsException<NotSupportedException>(()=>SteelShearBucklingAssessment.FromExternal(i,2,true,"external screen").WithConservativeInPlaneNormalInteraction());
        Assert.ThrowsException<NotSupportedException>(()=>Assessment(Input(false,-1e5,v:1e5,minor:1e6),false));
        Assert.ThrowsException<ArgumentException>(()=>SteelVerificationPipeline.Execute(plan,new[]{Assessment(Input(false,-1e5,v:1e5),false)}));
        var old=SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(Member(i,false)),new[]{Assessment(i,false)});
        Assert.IsFalse(old.IsComplete); // Opt-in did not alter the historical contract.
    }

    [DataTestMethod][DataRow(1d)][DataRow(1.1)]
    public void ExhaustedWebBudgetFailsWithoutInfiniteNumbers(double ratio)
    {
        var i=Input(false,-1e5,v:ratio*WebResistance(false));
        var r=SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(Member(i)),new[]{Assessment(i,false)}).Checks.Single();
        Assert.AreEqual(SteelExecutionStatus.Failed,r.Status);
        Assert.AreEqual("Member.Class4WebNormalCapacityExhausted",r.DiagnosticCode);
        Assert.IsTrue(r.Trace.All(t=>double.IsFinite(t.Value)));
    }
}
