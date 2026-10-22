using System.Text.Json;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Sections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class HollowBeamColumnTests
{
    private static SteelBeamColumnParameters Parameters()=>new(SteelBucklingCurve.A,SteelBucklingCurve.A,false,.8,.9,1,
        "Same state equivalent moments","Global effects accounted for","T assessed excluded","FT assessed excluded",
        lateralTorsionalExclusionSource:"Independent hollow-member twist assessment");
    private static SteelCheckExecution Run(SteelVerificationInput i,int cl)=>SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(
        SteelMemberAnalysis.BeamColumn(i,"same state","EC3/NTC elastic hollow policy",Parameters(),new[]{
            new SteelStabilityValue(SteelStabilityQuantity.Ncr1,i.Section.Area*235/(.5*.5),SteelStabilityUnit.N,"lambda1=.5"),
            new SteelStabilityValue(SteelStabilityQuantity.Ncr2,i.Section.Area*235/(.8*.8),SteelStabilityUnit.N,"lambda2=.8")},cl,"independent resistance-only class"))).Checks.Single();
    public static IEnumerable<object[]> Cases=>from family in new[]{"RHS","CHS"} from ntc in new[]{false,true} from cl in new[]{1,2,3}
        from sign in new[]{-1d,1d} select new object[]{family,ntc,cl,sign};
    [DataTestMethod,DynamicData(nameof(Cases))]
    public void ElasticInteractionMatchesIndependentGeometryAndFixedTableSubstitution(string family,bool ntc,int cl,double sign)
    {
        var o=JsonSerializer.Deserialize<CodeVerificationTests.Oracle[]>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","section-oracles.json")))!.Single(o=>o.Name==family);
        double gamma=ntc?1.05:1;
        // Independent closed-form roots of chi's quadratic; table elastic coefficients
        // from EC3 B.1 p79 and Circular2019 C4.2.IV p107, visually inspected.
        double Chi(double l) {double q=1+.21*(l-.2)+l*l;return 2/(q+Math.Sqrt(q*q-4*l*l));}
        double n1=.2*gamma/Chi(.5),n2=.2*gamma/Chi(.8),m1=.1*gamma,m2=.15*gamma;
        double k11=.8*(1+.3*n1),k22=.9*(1+.48*n2);
        if(ntc)k22=Math.Max(k22,.9*(1+.3*n1));
        double expected=Math.Max(n1+k11*m1+k22*m2,n2+.8*k11*m1+k22*m2);
        var b=CommonSectionCoverageTests.Input(CommonSectionCoverageTests.Shape(family),ntc,-.2*o.Area*235,sign*.1*o.Wel1*235,-sign*.15*o.Wel2*235);
        var r=Run(new(b.Section,b.Actions,b.Code,new SteelMemberInput(3000)),cl);
        Assert.IsTrue(r.IsEvaluated,r.Message);Assert.AreEqual(expected,r.Utilization!.Value,expected*2e-10);
        Assert.AreEqual(o.Wel1*235,r.Trace.Single(t=>t.Name=="M1Rk").Value,o.Wel1*235*1e-10);
        Assert.AreEqual(o.Wel2*235,r.Trace.Single(t=>t.Name=="M2Rk").Value,o.Wel2*235*1e-10);
        Assert.AreEqual(1,r.Trace.Single(t=>t.Name=="ElasticInteraction").Value);
    }
    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void RoundedRhsUsesItsQualifiedElasticPropertiesForCompactAndSlenderMembers(bool ntc)
    {
        var b=CommonSectionCoverageTests.Input(new SectionRHSRoundedCorners(150,100,6,9,6,"rounded"),ntc,-10000,1e6,5e5);
        var i=new SteelVerificationInput(b.Section,b.Actions,b.Code,new SteelMemberInput(3000));
        var compact=Run(i,1);var elastic=Run(i,3);
        Assert.IsTrue(compact.IsEvaluated,compact.Message);Assert.AreEqual(elastic.Utilization,compact.Utilization);
        Assert.AreEqual(b.Section.CreateSectionCopy().Wel1*235,compact.Trace.Single(t=>t.Name=="M1Rk").Value);
    }
    [TestMethod]
    public void UnequalRhsWallsAreNotDoubleSymmetricAndDoNotEnterAnnexB()
    {
        var b=CommonSectionCoverageTests.Input(new SectionRHS(300,200,10,12,10,10,"asym"),false,-1000,1000,1000);
        Assert.AreEqual("Member.BeamColumnGeometry",Run(new(b.Section,b.Actions,b.Code,new SteelMemberInput(3000)),3).DiagnosticCode);
    }
}
