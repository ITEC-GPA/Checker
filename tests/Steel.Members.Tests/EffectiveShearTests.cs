using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Sections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;
[TestClass]
public class EffectiveShearTests
{
    private static SteelVerificationInput Input(bool ntc,double shear,double moment=0,double n=0)
    {
        var b=CommonSectionCoverageTests.Input(new SectionH(398,6.4,141.8,8.6,141.8,8.6,"SCI406x140x39",10.2),ntc,fy:355);
        return new(b.Section,new SteelActions(n,0,shear,0,moment,0),b.Code);
    }
    private static SteelShearBucklingAssessment Assessment(SteelVerificationInput i,bool ntc)=>SteelShearBucklingAssessment.EvaluateNonRigidHWeb(
        i,ntc?1.05:1,1,"Support stiffeners, welds and force introduction independently checked for this state; no intermediate stiffeners");
    private static double SciResistance(bool ntc)=>.83*86.4*6.4*6.4*Math.Sqrt(235d/355)*355/(Math.Sqrt(3)*(ntc?1.05:1));

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void PublishedSciAd368ResistanceIsAvailableThroughCommonPipeline(bool ntc)
    {
        var i=Input(ntc,1e5);var assessment=Assessment(i,ntc);
        Assert.IsFalse(assessment.IsBucklingExcluded);Assert.IsTrue(assessment.BucklingResistance.HasValue);
        Assert.AreEqual(SciResistance(ntc),assessment.BucklingResistance.Value,1e-7);
        // SCI AD368 NSC June2012 p28: 488 kN (eta=gammaM1=1).
        double printedChiResistance=.976*355*380.8*6.4/(Math.Sqrt(3)*(ntc?1.05:1));
        Assert.AreEqual(488000/(ntc?1.05:1),printedChiResistance,500/(ntc?1.05:1));
        // The advisory prints lambda=0.85 and chi=0.976; the code retains full
        // precision. Separate that known intermediate rounding from the 1kN result.
        Assert.AreEqual(488000/(ntc?1.05:1),assessment.BucklingResistance.Value,
            500/(ntc?1.05:1)+Math.Abs(SciResistance(ntc)-printedChiResistance));
        var r=SteelVerificationPipeline.Execute(SteelMethodSelector.Select(i),new[]{assessment});
        Assert.IsTrue(r.IsComplete);var shear=r.Checks.Single(c=>c.Decision.Target==SteelCheckTarget.Shear2);
        Assert.AreEqual(1e5/SciResistance(ntc),shear.Utilization!.Value,1e-12);
        Assert.AreEqual(SciResistance(ntc),shear.Resistance!.Value,1e-7);
    }
    public static IEnumerable<object[]> Cases=>from ntc in new[]{false,true} from ratio in new[]{.1,.5,.75,1d,1.1}
        from sign in new[]{-1d,1d} select new object[]{ntc,ratio,sign};
    [DataTestMethod,DynamicData(nameof(Cases))]
    public void Class4MajorBendingWebShearMatchesConservativeEquation71(bool ntc,double ratio,double sign)
    {
        // Web d/tw = 360.4/6.4 = 56.3 <= 72 eps: class 1 in pure bending, class 4 in uniform compression.
        // Web shear buckling is not excluded, so the shear cases keep the effective-section route.
        var pure=Input(ntc,0,sign*1e7);
        Assert.AreEqual(1,SteelMethodSelector.Select(pure).Decisions.Single(d=>d.Target==SteelCheckTarget.NormalResistance).SectionClass);
        var normal=SteelVerificationPipeline.Execute(SteelMethodSelector.Select(pure,classification:new SteelSectionClassification(pure,4,"uniform compression"))).Checks.First();
        var i=Input(ntc,sign*ratio*SciResistance(ntc),sign*1e7);
        var r=SteelVerificationPipeline.Execute(SteelMethodSelector.Select(i),new[]{Assessment(i,ntc)});var check=r.Checks.First();
        double expected=Math.Max(ratio,normal.Utilization!.Value+(ratio<=.5?0:Math.Pow(2*ratio-1,2)));
        Assert.IsTrue(r.IsComplete,check.Message);Assert.AreEqual(expected,check.Utilization!.Value,1e-10);
        Assert.AreEqual(expected<=1?SteelExecutionStatus.Passed:SteelExecutionStatus.Failed,check.Status);
        if(ratio>1)Assert.AreEqual(SteelExecutionStatus.Failed,r.Checks.Single(c=>c.Decision.Target==SteelCheckTarget.Shear2).Status);
    }
    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void LowShearKeepsConcomitantCompressionButHighShearNeedsAnotherInteraction(bool ntc)
    {
        foreach(double ratio in new[]{.4,.8}) {
            var i=Input(ntc,ratio*SciResistance(ntc),1e7,-1000);
            var r=SteelVerificationPipeline.Execute(SteelMethodSelector.Select(i),new[]{Assessment(i,ntc)});
            Assert.AreEqual(ratio<.5,r.IsComplete);
            if(ratio>.5)Assert.AreEqual("Class4.HighShearInteractionNotQualified",r.Checks.First().DiagnosticCode);
        }
    }
    [TestMethod]
    public void ResistanceCannotMasqueradeAsExclusionOrSkipSupportEvidence()
    {
        var i=Input(false,1e5,1e7);var plan=SteelMethodSelector.Select(i);
        Assert.IsFalse(SteelVerificationPipeline.Execute(plan).IsComplete);
        var rejected=SteelVerificationPipeline.Execute(plan,new[]{SteelShearBucklingAssessment.FromExternal(i,2,false,"buckling present")});
        Assert.AreEqual("ShearBuckling.ResistanceMethodRequired",rejected.Checks.First().DiagnosticCode);
        Assert.ThrowsException<ArgumentException>(()=>SteelShearBucklingAssessment.EvaluateNonRigidHWeb(i,1,1,""));
        Assert.ThrowsException<ArgumentException>(()=>SteelShearBucklingAssessment.EvaluateNonRigidHWeb(Input(true,1e5),1,1,"support"));
        Assert.ThrowsException<ArgumentException>(()=>SteelVerificationPipeline.Execute(plan,new[]{Assessment(Input(false,1e5,1e7),false)}));
        // Existing plastic interaction is not silently allowed to use Vpl after receiving a reduced Vbw.
        var forced=SteelMethodSelector.Select(i,classification:new SteelSectionClassification(i,1,"contract experiment"));
        Assert.IsFalse(SteelVerificationPipeline.Execute(forced,new[]{Assessment(i,false)}).IsComplete);
    }
}
