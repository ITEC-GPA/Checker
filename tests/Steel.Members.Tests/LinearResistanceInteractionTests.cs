using System.Text.Json;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Selection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

// EN1993-1-1:2005 6.2.1(7); independent geometric integration fixture and
// tabulated shear-area formulae, NOT values copied from ModelShearAreas.
[TestClass]
public class LinearResistanceInteractionTests
{
    public static IEnumerable<object[]> Cases => from family in new[]{"H","C","T","RHS","CHS"}
        from ntc in new[]{false,true} from sign in new[]{-1d,1d}
        from mode in new[]{"NV","NMV3","VV"} select new object[]{family,ntc,sign,mode};

    [DataTestMethod, DynamicData(nameof(Cases))]
    public void QualifiedFamiliesMatchIndependentResistanceSuperposition(string family,bool ntc,double sign,string mode)
    {
        var o=JsonSerializer.Deserialize<CodeVerificationTests.Oracle[]>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory,"Fixtures","section-oracles.json")))!.Single(o=>o.Name==family);
        double fd=235/(ntc?1.05:1);
        double av1=family switch {"H"=>8000,"C"=>3000,"T"=>3000,"RHS"=>o.Area*.4,_=>2*o.Area/Math.PI};
        double av2=family switch {"H"=>2800,"C"=>2850,"T"=>ntc?.9*1850:1925,"RHS"=>o.Area*.6,_=>av1};
        var original=CommonSectionCoverageTests.Input(CommonSectionCoverageTests.Shape(family),ntc);
        var input=new SteelVerificationInput(original.Section,new SteelActions(
            mode=="VV"?0:sign*.25*o.Area*fd, mode=="VV"?sign*.2*av1*fd/Math.Sqrt(3):0,
            -sign*.3*av2*fd/Math.Sqrt(3),0,mode=="NMV3"?sign*.1*o.Wel1*fd:0,
            mode=="NMV3"?-sign*.1*o.Wel2*fd:0),original.Code);
        var plan=SteelMethodSelector.Select(input,classification:new SteelSectionClassification(input,3,"independent class3 experiment"));
        var r=SteelVerificationPipeline.Execute(plan,new[]{1,2}.Where(a=>(a==1?input.Actions.V1:input.Actions.V2)!=0)
            .Select(a=>SteelShearBucklingAssessment.FromExternal(input,a,true,"independent short panel assessment")));
        Assert.IsTrue(r.IsComplete,string.Join(";",r.Checks.Select(c=>c.Message)));
        double expected=mode=="VV"?.5:mode=="NV"?.55:.75;
        var check=r.Checks.First(c=>c.IsEvaluated);
        Assert.AreEqual(expected,check.Utilization!.Value,2e-9);
        Assert.AreEqual(mode=="VV"?SteelMethod.LinearShearInteraction:SteelMethod.LinearResistanceInteraction,check.Decision.Method);
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void SeparatePassesCannotHideCombinedFailureAndBothShearAssessmentsAreRequired(bool ntc)
    {
        var original=CommonSectionCoverageTests.Input(CommonSectionCoverageTests.Shape("H"),ntc);
        double fd=235/(ntc?1.05:1);
        var input=new SteelVerificationInput(original.Section,new SteelActions(0,.6*8000*fd/Math.Sqrt(3),.6*2800*fd/Math.Sqrt(3),0,0,0),original.Code);
        var plan=SteelMethodSelector.Select(input);
        var a1=SteelShearBucklingAssessment.FromExternal(input,1,true,"flanges" );
        var a2=SteelShearBucklingAssessment.FromExternal(input,2,true,"web");
        var partial=SteelVerificationPipeline.Execute(plan,new[]{a1});
        Assert.IsFalse(partial.IsComplete);
        Assert.IsTrue(partial.Checks.Where(c=>c.Decision.Target is SteelCheckTarget.Shear1 or SteelCheckTarget.Shear2).All(c=>!c.IsEvaluated));
        var complete=SteelVerificationPipeline.Execute(plan,new[]{a1,a2});
        Assert.IsTrue(complete.IsComplete);
        Assert.IsTrue(complete.Checks.Where(c=>c.IsEvaluated).All(c=>c.Status==SteelExecutionStatus.Failed));
        Assert.AreEqual(1.2,complete.Checks.First(c=>c.IsEvaluated).Utilization!.Value,1e-12);
    }

    [TestMethod]
    public void LinearAllowanceIsStricterThanWholeSectionRhoReductionAtEveryShearLevel()
    {
        for(int k=0;k<=1000;k++) {
            double v=k/1000d,rho=v<=.5?0:Math.Pow(2*v-1,2);
            Assert.IsTrue(1-v<=1-rho+1e-15);
        }
    }
}
