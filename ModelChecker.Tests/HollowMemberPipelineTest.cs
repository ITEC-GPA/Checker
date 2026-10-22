using GPC.Model.Checker;
using GPC.Model.Checker.Configuration;
using GPC.Model.Checking.Contracts;
using GPC.Model.Data.Steel;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Queries;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using GPC.Model.Structure.Assignments;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service=GPC.Model.Checker.ModelChecker;

namespace ModelChecker.Tests;
[TestClass]
public class HollowMemberPipelineTest
{
    [DataTestMethod] [DataRow(false,false)] [DataRow(false,true)] [DataRow(true,false)] [DataRow(true,true)]
    public void CommonAdapterRunsHollowBeamColumnsWithAutomaticCurves(bool ntc,bool circular)
    {
        var (model,plan)=SteelMemberPipelineTest.Fixture(sectionFactory:()=>new SteelSection(
            circular?(Section)new SectionCHS(200,10,"CHS"):new SectionRHSRoundedCorners(200,150,10,15,10,"RHS"),SteelMaterialEN1993Data.S355));
        var beam=model.BeamElements[250];
        foreach(var sample in ResultQueries.Samples<StationResultBeamForces>(beam,plan.Results[0])) {sample.ResultBeamForces.M1=1e6;sample.ResultBeamForces.M2=-5e5;}
        var p=SteelMemberPipelineTest.Options(model,plan);
        foreach(var key in new[]{"InPlaneCurve","LateralCurve","LateralTorsionalCurve"})p.Remove(key);
        p["AnalysisMode"]="BeamColumn";p["CurveSelection"]="Automatic";
        p["TorsionSusceptible"]="false";p["Cm1"]="1";p["Cm2"]="1";p["CmLT"]="1";
        p["MomentFactorSource"]="Conservative whole-span factors";p["StabilityModelSource"]="Global second order accounted for";
        p["TorsionalExclusionSource"]="Independent torsional screen";p["FlexuralTorsionalExclusionSource"]="Independent FT screen";
        p["LateralTorsionalExclusionSource"]="Hollow section, independent twist-restraint assessment";p["McrBasis"]="UniformMoment";
        foreach(var q in new[]{"Ncr1","Ncr2"}) {p[q+".Value"]="2000000";p[q+".Unit"]="N";p[q+".Source"]="Same-member elastic critical analysis";}
        Assert.AreEqual(0,SteelParameterSchemas.Member.Validate(p).Count);
        var result=new Service().Verify(model,SteelMemberPipelineTest.Request(plan,p,ntc?new StandardNTC2018Steel():new StandardEN1993p11())).Jobs[0].Results.Single();
        Assert.AreEqual(EngineeringOutcome.Satisfied,result.Outcome,string.Join(";",result.Diagnostics.Select(d=>d.Message??d.Code)));
        Assert.IsTrue(result.Details.Trace.Any(t=>t.Key.EndsWith("ElasticInteraction")&&t.Value==1));
    }
}
