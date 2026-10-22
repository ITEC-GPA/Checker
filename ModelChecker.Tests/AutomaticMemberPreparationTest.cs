using GPC.Model.Checker;
using GPC.Model.Checker.Configuration;
using GPC.Model.Checking.Contracts;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Persistence;
using GPC.Model.Results.Queries;
using GPC.Model.Results.Locations;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service=GPC.Model.Checker.ModelChecker;

namespace ModelChecker.Tests;

[TestClass]
public class AutomaticMemberPreparationTest
{
    [DataTestMethod] [DataRow(false,false)] [DataRow(true,false)] [DataRow(false,true)] [DataRow(true,true)]
    public void BothCodesPrepareUniformMemberAndPreserveProvenance(bool ntc,bool bending)
    {
        var (model,plan)=SteelMemberPipelineTest.Fixture();
        if(bending)foreach(var sample in ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[250],plan.Results[0]))
            {sample.ResultBeamForces.N=0;sample.ResultBeamForces.M1=1e6;}
        var p=SteelMemberPipelineTest.Options(model,plan);
        foreach(var k in new[]{"InPlaneCurve","LateralCurve","LateralTorsionalCurve"})p.Remove(k);
        p["AnalysisMode"]=bending?"Bending":"Compression";p["CriticalValues"]="UniformFork";p["CriticalValuesSource"]="Explicit full member, forks, free warping, no intermediate restraint";
        if(bending)p["McrBasis"]="UniformMoment";else p["CurveSelection"]="Automatic";
        Assert.AreEqual(0,SteelParameterSchemas.Member.Validate(p).Count,string.Join(";",SteelParameterSchemas.Member.Validate(p).Select(i=>i.Code)));
        var request=SteelMemberPipelineTest.Request(plan,p,ntc?new StandardNTC2018Steel():new StandardEN1993p11());
        var report=new Service().Verify(model,request);var r=report.Jobs[0].Results.Single();
        Assert.AreEqual(EngineeringOutcome.Satisfied,r.Outcome,string.Join(";",r.Diagnostics.Select(d=>d.Message??d.Code)));
        Assert.IsTrue(r.Details.Trace.Any(t=>t.Expression!=null&&t.Expression.Contains(p["CriticalValuesSource"])));
        if(!bending)Assert.IsTrue(r.Details.Trace.Any(t=>t.Expression!=null&&t.Expression.Contains(ntc?"4.2.VIII":"6.2")));
        using var stream=new MemoryStream();CheckReportArchive.Save(report.Jobs,stream);stream.Position=0;
        Assert.AreEqual(EngineeringOutcome.Satisfied,Service.CurrentOutcome(CheckReportArchive.Load(stream),model,request));
    }
    [DataTestMethod] [DataRow("missing-source")] [DataRow("manual-curve")] [DataRow("external-value")] [DataRow("nonuniform")]
    public void InconsistentPreparationNeverPasses(string problem)
    {
        var (model,plan)=SteelMemberPipelineTest.Fixture();
        if(problem=="nonuniform")ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[250],plan.Results[0]).Last().ResultBeamForces.N*=1.1;
        var p=SteelMemberPipelineTest.Options(model,plan);
        foreach(var k in new[]{"InPlaneCurve","LateralCurve","LateralTorsionalCurve"})p.Remove(k);
        p["AnalysisMode"]="Compression";p["CurveSelection"]="Automatic";p["CriticalValues"]="UniformFork";p["CriticalValuesSource"]="fork/free-warping declaration";
        if(problem=="missing-source")p.Remove("CriticalValuesSource");
        if(problem=="manual-curve")p["CompressionCurve1"]="A0";
        if(problem=="external-value") {p["Ncr1.Value"]="1000000";p["Ncr1.Unit"]="N";p["Ncr1.Source"]="other analysis";}
        if(problem!="nonuniform")Assert.IsTrue(SteelParameterSchemas.Member.Validate(p).Count>0);
        var r=new Service().Verify(model,SteelMemberPipelineTest.Request(plan,p)).Jobs[0].Results.Single();
        Assert.AreEqual(EngineeringOutcome.NotEvaluated,r.Outcome);Assert.IsNull(r.Utilization);
    }
}
