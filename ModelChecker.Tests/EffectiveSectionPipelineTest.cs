using GPC.Model.Checker;
using GPC.Model.Checking.Contracts;
using GPC.Model.Core;
using GPC.Model.Data.Steel;
using GPC.Model.Persistence;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Queries;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service=GPC.Model.Checker.ModelChecker;

namespace ModelChecker.Tests;
[TestClass]
public class EffectiveSectionPipelineTest
{
    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void Class4MemberCompressionUsesEffectiveAreaThroughCommonAdapter(bool ntc)
    {
        var (model,plan)=SteelMemberPipelineTest.Fixture(sectionFactory:()=>new SteelSection(
            new SectionRHS(400,200,4,4,4,4,"Class4 RHS"),SteelMaterialEN1993Data.S355));
        var p=SteelMemberPipelineTest.Options(model,plan);
        foreach(var key in new[]{"InPlaneCurve","LateralCurve","LateralTorsionalCurve"})p.Remove(key);
        p["AnalysisMode"]="Compression";p["CurveSelection"]="Automatic";
        p["TorsionalExclusionSource"]="Continuous independent torsional restraint";
        p["FlexuralTorsionalExclusionSource"]="Continuous restraint excludes coupled mode";
        p["Ncr1.Value"]="3000000";p["Ncr2.Value"]="1500000";
        foreach(var q in new[]{"Ncr1","Ncr2"}) {p[q+".Unit"]="N";p[q+".Source"]="Gross elastic eigenvalue";}
        var report=new Service().Verify(model,SteelMemberPipelineTest.Request(plan,p,ntc?new StandardNTC2018Steel():new StandardEN1993p11()));
        Assert.AreEqual(EngineeringOutcome.Satisfied,report.Outcome,string.Join(";",report.Jobs[0].Results.SelectMany(r=>r.Diagnostics).Select(d=>d.Code+" "+d.Message)));
        var result=report.Jobs[0].Results.Single();Assert.IsTrue(result.Details.Trace.Any(t=>t.Key.EndsWith(".AreaEffective")&&t.Value<4736));
        Assert.IsTrue(result.Details.Trace.Any(t=>t.Key.EndsWith(".MemberClass")&&t.Value==4));
        using var stream=new MemoryStream();CheckReportArchive.Save(report.Jobs,stream);stream.Position=0;
        Assert.AreEqual(EngineeringOutcome.Satisfied,Service.CurrentOutcome(CheckReportArchive.Load(stream),model,SteelMemberPipelineTest.Request(plan,p,ntc?new StandardNTC2018Steel():new StandardEN1993p11())));
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void Class4SectionWorkflowAndArchiveRetainEffectiveProperties(bool ntc)
    {
        var (model,plan)=SteelMemberPipelineTest.Fixture(sectionFactory:()=>new SteelSection(
            new SectionRHS(400,200,4,4,4,4,"Class4 RHS"),SteelMaterialEN1993Data.S355));
        foreach(var sample in ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[250],plan.Results[0])) {
            sample.ResultBeamForces.M1=1e6;sample.ResultBeamForces.M2=-5e5;
        }
        plan.SectionMechanisms=new[]{CheckMechanism.UlsBiaxialSection};plan.MemberChecks=Array.Empty<MemberCheckSpecification>();
        var request=new MultiMaterialCheckRequest();
        request.Jobs.Add(new MultiMaterialCheckJob {Name="Class4 sections",Plan=plan,
            Assignments=new[]{new MaterialCheckerAssignment {Checker=new SteelPipelineMaterialChecker(
                ntc?new StandardNTC2018Steel():new StandardEN1993p11(),ntc?"2018":"2005",
                parameters:new Dictionary<string,string>{{"Unperforated","true"}}),Mechanisms=new[]{CheckMechanism.UlsBiaxialSection}}}});
        var report=new Service().Verify(model,request);
        Assert.AreEqual(EngineeringOutcome.Satisfied,report.Outcome,string.Join(";",report.Jobs[0].Results.SelectMany(r=>r.Diagnostics).Select(d=>d.Code+" "+d.Message)));
        Assert.AreEqual(5,report.Executed);
        foreach(var r in report.Jobs[0].Results) {
            Assert.IsTrue(r.Details.Trace.Any(t=>t.Key.EndsWith(".Method")&&t.Expression=="EffectiveNormalResistance"));
            Assert.IsTrue(r.Details.Trace.Any(t=>t.Key.EndsWith(".EffectiveResidual")&&t.Value<1e-8));
            Assert.IsTrue(r.Details.Trace.Any(t=>t.Key.EndsWith(".CentroidShiftX")));
        }
        using var stream=new MemoryStream();CheckReportArchive.Save(report.Jobs,stream);stream.Position=0;
        var restored=CheckReportArchive.Load(stream);
        Assert.AreEqual(EngineeringOutcome.Satisfied,Service.CurrentOutcome(restored,model,request));
        Assert.AreEqual(report.Jobs[0].Results[0].Details.Trace.Count,restored[0].Results[0].Details.Trace.Count);
    }
}
