using GPC.Examples;
using GPC.Model.Checker.Configuration;
using GPC.Model.Checking.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ModelChecker.Tests;

public partial class SteelBeamColumnWorkflowTest
{
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void CombinedShearMemberBudgetUsesOnlyTheSameCaseExportedExtrema(bool ntc)
    {
        var model=SteelBeamColumnFrame.Create();var p=Options(model,ntc);
        // Rolled H300x150, tf=18, tw=12, r=0: Av1=5400;
        // Av2=A-2*b*tf+tw*tf=264*12+12*18=3384 mm2 (6.2.6(3)).
        double fd=355/(ntc?1.05:1),v1=.6*5400*fd/Math.Sqrt(3),v2=.2*3384*fd/Math.Sqrt(3);
        var cuts=C1A(model).ToArray();
        foreach(var cut in cuts){cut.ResultBeamForces.V1=v1/2;cut.ResultBeamForces.V2=-v2/2;}
        cuts.First().ResultBeamForces.V1=v1;
        cuts.Last().ResultBeamForces.V2=-v2;
        p["HighShearMethod"]="ConservativeShearSuperposition";
        Assert.AreEqual(0,SteelParameterSchemas.Member.Validate(p).Count);
        var r=Single(model,p,ntc);
        Assert.AreNotEqual(EngineeringOutcome.NotEvaluated,r.Outcome,Diagnostics(new[]{r}));
        Assert.AreEqual(.6,Value(r,"MemberStability.ShearSuperposition.ShearRatio1"),1e-12);
        Assert.AreEqual(.2,Value(r,"MemberStability.ShearSuperposition.ShearRatio2"),1e-12);
        Assert.AreEqual(.2,Value(r,"MemberStability.ShearSuperposition.NormalResistanceFactor"),1e-12);
        // Member budget is a conservative envelope; exported section-cut simultaneity stays intact.
        Assert.AreEqual(-v2/2,cuts.First().ResultBeamForces.V2);
        Assert.AreEqual(v1/2,cuts.Last().ResultBeamForces.V1);
    }
}
