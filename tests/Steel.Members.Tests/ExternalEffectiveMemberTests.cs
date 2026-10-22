using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Members;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;
[TestClass]
public class ExternalEffectiveMemberTests
{
    private static SteelVerificationInput Input(string family,bool ntc)
    {
        Section s=family switch {
            "C"=>new SectionC(300,5,100,6,100,6,"C"),
            "T"=>new SectionT(300,180,5,7,"T"),
            "L"=>new SectionL(150,5,150,5,"L"),
            "unequal L"=>new SectionL(150,5,100,5,"L"),
            "unequal C"=>new SectionC(300,5,100,6,80,6,"C"),
            _=>throw new ArgumentException(family)
        };
        return new(new SteelSectionInput(new SteelSection(s,new SteelMaterial("S355",210000,355,510)),true),
            new SteelActions(-1e5,0,0,0,1e7,0),ntc?SteelDesignContext.Ntc2018():new(SectionResistanceCode.Eurocode3(1,1.25,"assessed annex"),1),new SteelMemberInput(3000));
    }
    private static SteelStabilityValue[] Factors()=>new[]{new SteelStabilityValue(SteelStabilityQuantity.AlphaUltK,2,SteelStabilityUnit.One,"In-plane characteristic resistance including imperfections/second order"),
        new SteelStabilityValue(SteelStabilityQuantity.AlphaCrOp,4,SteelStabilityUnit.One,"Elastic bifurcation of same full proportional pattern")};
    private static SteelMemberAnalysis Analysis(SteelVerificationInput i,bool effective)=>effective?
        SteelMemberAnalysis.FromExternalEffective(i,"Pattern A; no rotated plastic hinges","EN6.3.4 applicability assessed",Ec3GeneralMethodCurve.B,Ec3GeneralMethodCurve.C,Factors(),"nominal member class4",
            "Independent effective in-plane analysis: widths and centroid shifts, global/local imperfections, second order; eigenmodel same pattern"):
        SteelMemberAnalysis.FromExternal(i,"Pattern A; no rotated plastic hinges","EN6.3.4 applicability assessed",Ec3GeneralMethodCurve.B,Ec3GeneralMethodCurve.C,Factors(),3,"member class3");
    private static SteelCheckExecution Run(SteelMemberAnalysis a)=>SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(a)).Checks.Single();

    public static IEnumerable<object[]> Cases()
    { foreach(var f in new[]{"C","T","L"})foreach(bool ntc in new[]{false,true})foreach(bool effective in new[]{false,true})yield return new object[]{f,ntc,effective}; }
    [DataTestMethod] [DynamicData(nameof(Cases),DynamicDataSourceType.Method)]
    public void SymmetricOpenSectionsUseSourcedFactorsAndTheirOwnNorm(string family,bool ntc,bool effective)
    {
        var result=Run(Analysis(Input(family,ntc),effective));Assert.IsTrue(result.IsEvaluated,result.Message);
        // EN6.3.4(4)(a), 6.3.1/2: solve the reduction quadratic by bisection,
        // independently of the production closed-form root. Curve c governs b/c.
        double lambda=Math.Sqrt(.5),b=1+.49*(lambda-.2)+lambda*lambda,lo=0,hi=1;
        for(int k=0;k<100;k++) {double x=(lo+hi)/2; if(lambda*lambda*x*x-b*x+1>0)lo=x;else hi=x;}
        double expected=(ntc?1.05:1)/(2*(lo+hi)/2);
        Assert.AreEqual(expected,result.Utilization!.Value,1e-12);
        Assert.AreEqual(ntc,result.Reference.Contains("NTC2018 4.2.4.1.3.3"));
        Assert.AreEqual(effective?4d:3d,result.Trace.Single(v=>v.Name=="MemberClass").Value);
    }
    [DataTestMethod] [DataRow("unequal L",false)] [DataRow("unequal L",true)] [DataRow("unequal C",false)] [DataRow("unequal C",true)]
    public void AnalysisSourcesDoNotWaiveSymmetryScope(string family,bool effective)
    { var r=Run(Analysis(Input(family,true),effective));Assert.AreEqual(SteelExecutionStatus.NotSupported,r.Status);Assert.IsNull(r.Utilization); }

    [TestMethod]
    public void Class4NeedsDedicatedAnalysisEvidenceAndRetainsIt()
    {
        var i=Input("T",false);
        Assert.ThrowsException<ArgumentException>(()=>SteelMemberAnalysis.FromExternalEffective(i,"pattern","policy",Ec3GeneralMethodCurve.B,Ec3GeneralMethodCurve.C,Factors(),"class4"," "));
        var old=SteelMemberAnalysis.FromExternal(i,"pattern","policy",Ec3GeneralMethodCurve.B,Ec3GeneralMethodCurve.C,Factors(),4,"class4");
        Assert.AreEqual(SteelExecutionStatus.NotSupported,Run(old).Status);
        Assert.IsNotNull(Analysis(i,true).EffectiveSectionAnalysisSource);
    }
}
