using GPC.Model.Checker;
using GPC.Model.Checker.Configuration;
using GPC.Model.Checking.Contracts;
using GPC.Model.Data.Steel;
using GPC.Model.Persistence;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service=GPC.Model.Checker.ModelChecker;

namespace ModelChecker.Tests;
[TestClass]
public class ExternalEffectivePipelineTest
{
    [DataTestMethod] [DataRow(false,false)] [DataRow(false,true)] [DataRow(true,false)] [DataRow(true,true)]
    public void BothNormsRunTheSourcedGeneralMethodAndArchiveItsEffectiveEvidence(bool ntc,bool effective)
    {
        var (model,plan)=SteelMemberPipelineTest.Fixture(sectionFactory:()=>new SteelSection(new SectionT(300,180,5,7,"T"),SteelMaterialEN1993Data.S355));
        var p=SteelMemberPipelineTest.Options(model,plan,true);
        if(effective) {p["MemberClass"]="4";p["EffectiveSectionAnalysisSource"]="Analysis report A: effective widths, centroid shifts, imperfections and second order";}
        var code=ntc?new StandardNTC2018Steel():new StandardEN1993p11();
        var request=SteelMemberPipelineTest.Request(plan,p,code);
        var report=new Service().Verify(model,request);
        Assert.AreEqual(EngineeringOutcome.Satisfied,report.Outcome,string.Join(";",report.Jobs[0].Results.SelectMany(r=>r.Diagnostics).Select(d=>d.Message)));
        var result=report.Jobs[0].Results.Single();
        Assert.AreEqual(ntc?1.05:1,result.Details.Trace.Single(t=>t.Key=="MemberStability.GammaM1").Value);
        Assert.AreEqual(effective,result.Details.Trace.Any(t=>t.Key=="Member.EffectiveSectionAnalysisSource"));
        Assert.AreEqual(0,SteelParameterSchemas.Member.Validate(p).Count);
        using var stream=new MemoryStream();CheckReportArchive.Save(report.Jobs,stream);stream.Position=0;
        var restored=CheckReportArchive.Load(stream);
        Assert.AreEqual(EngineeringOutcome.Satisfied,Service.CurrentOutcome(restored,model,request));
        if(effective) {
            Assert.AreEqual(p["EffectiveSectionAnalysisSource"],restored[0].Results.Single().Details.Trace.Single(t=>t.Key=="Member.EffectiveSectionAnalysisSource").Expression);
            p["EffectiveSectionAnalysisSource"]="Changed analysis report";
            Assert.AreEqual(EngineeringOutcome.NotEvaluated,Service.CurrentOutcome(restored,model,SteelMemberPipelineTest.Request(plan,p,code)));
        }
    }
    [DataTestMethod] [DataRow("missing")] [DataRow("other class")] [DataRow("other mode")]
    public void SchemaAndExecutionRejectMisplacedEffectiveEvidence(string problem)
    {
        var (model,plan)=SteelMemberPipelineTest.Fixture();var p=SteelMemberPipelineTest.Options(model,plan,true);
        p["MemberClass"]="4";
        if(problem!="missing")p["EffectiveSectionAnalysisSource"]="report";
        if(problem=="other class")p["MemberClass"]="3";
        if(problem=="other mode")p["AnalysisMode"]="Compression";
        Assert.IsTrue(SteelParameterSchemas.Member.Validate(p).Count>0);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated,new Service().Verify(model,SteelMemberPipelineTest.Request(plan,p)).Outcome);
    }
}
