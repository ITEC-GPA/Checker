using GPC.Examples;
using GPC.Model.Checker.Configuration;
using GPC.Model.Checking.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ModelChecker.Tests;

public partial class SteelBeamColumnWorkflowTest
{
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void MemberHighShearOptInUsesTheActualSameCaseForceEnvelope(bool ntc)
    {
        var model=SteelBeamColumnFrame.Create();var p=Options(model,ntc);
        // H300x150, two 18mm flanges: Av1=2*150*18=5400mm2;
        // EC3 6.2.6(3), NTC 4.2.4.1.2.4. All source cuts retained.
        double maximum=.75*5400*355/(Math.Sqrt(3)*(ntc?1.05:1));
        var cuts=C1A(model).ToArray();
        foreach(var cut in cuts){cut.ResultBeamForces.V1=-maximum/2;cut.ResultBeamForces.V2=0;}
        cuts.Last().ResultBeamForces.V1=-maximum;
        Assert.AreEqual(EngineeringOutcome.NotEvaluated,Single(model,p,ntc).Outcome);
        p["HighShearMethod"]="ConservativeUniformReduction";
        Assert.AreEqual(0,SteelParameterSchemas.Member.Validate(p).Count);
        var r=Single(model,p,ntc);
        Assert.AreNotEqual(EngineeringOutcome.NotEvaluated,r.Outcome,Diagnostics(new[]{r}));
        Assert.AreEqual(.75,Value(r,"MemberStability.HighShear.VEdOverVplRd"),1e-12);
        Assert.AreEqual(.25,Value(r,"MemberStability.HighShear.Rho"),1e-12);
        Assert.AreEqual(.75,Value(r,"MemberStability.HighShear.NormalResistanceFactor"),1e-12);
        Assert.AreEqual(-maximum/2,cuts.First().ResultBeamForces.V1);
        Assert.AreEqual(-maximum,cuts.Last().ResultBeamForces.V1);
    }

    [TestMethod]
    public void UnknownHighShearPolicyIsNotIgnored()
    {
        var model=SteelBeamColumnFrame.Create();var p=Options(model);
        p["HighShearMethod"]="DoNotReduceResistance";
        Assert.IsTrue(SteelParameterSchemas.Member.Validate(p).Count>0);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated,Single(model,p).Outcome);
    }
}
