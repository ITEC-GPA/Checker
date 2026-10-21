using GPC.Model.Checker;
using GPC.Model.Checker.Configuration;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Core;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Persistence;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Queries;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service=GPC.Model.Checker.ModelChecker;

namespace ModelChecker.Tests;

[TestClass]
public class CommonMemberPipelineTest
{
    private static Dictionary<string,string> Options(GPC.Model.Models.Model model,BeamCheckPlanRequest plan,bool bending=false)
    {
        var old=SteelMemberPipelineTest.Options(model,plan);
        foreach(var key in new[]{"InPlaneCurve","LateralCurve","LateralTorsionalCurve"})old.Remove(key);
        old["AnalysisMode"]=bending?"Bending":"Compression";
        old["LoadPatternReference"]="Full physical member LC1; prismatic; independently assessed restraints and whole-span action envelope";
        old["ParameterSet"]="Explicit standard method, reference curves and external critical forces";
        if(bending) {
            old["McrBasis"]="UniformMoment";old["Mcr.Value"]="100";old["Mcr.Unit"]="KNm";old["Mcr.Source"]="Independent uniform moment analysis";
        } else {
            old["CompressionCurve1"]="B";old["CompressionCurve2"]="C";old["TorsionalCurve"]="C";
            old["Ncr1.Value"]="3000";old["Ncr1.Unit"]="KN";old["Ncr1.Source"]="Eigenvalue flexural axis1";
            old["Ncr2.Value"]="1500";old["Ncr2.Unit"]="KN";old["Ncr2.Source"]="Eigenvalue flexural axis2";
            old["TorsionalExclusionSource"]="Continuous twist restraints in this test model";
            old["FlexuralTorsionalExclusionSource"]="Continuous twist restraints exclude coupled mode";
        }
        return old;
    }
    private static VerificationConfiguration Configuration(BeamCheckPlanRequest plan,Dictionary<string,string> options,bool ntc)
        => new() { Contexts=new[]{new DesignContextDefinition { Id="code",Edition=ntc?"2018":"2005",Code=ntc?new StandardNTC2018Steel():new StandardEN1993p11() }},
            Engines=new[]{new EngineDefinition { Id="member",Kind=SteelMemberPipelineChecker.EngineId,ContextId="code",Parameters=options }},
            Jobs=new[]{new ConfiguredCheckJob { Name="Common member",Plan=plan,Routes=new[]{new CheckRouteDefinition { EngineId="member",Mechanisms=new[]{CheckMechanism.Stability} }} }} };

    [DataTestMethod] [DataRow(false,false)] [DataRow(true,false)] [DataRow(false,true)] [DataRow(true,true)]
    public void BothCodesRunAndPersistTheSameMemberWorkflow(bool ntc,bool bending)
    {
        var (model,plan)=SteelMemberPipelineTest.Fixture();
        if(bending) {
            int k=0;foreach(var sample in ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[250],plan.Results[0])) {
                sample.ResultBeamForces.N=0;sample.ResultBeamForces.M1=++k*1e6;sample.ResultBeamForces.V2=100;
            }
        }
        var options=Options(model,plan,bending);var config=ConfigurationArchive.Copy(Configuration(plan,options,ntc));
        var catalog=EngineCatalog.BuiltIn();Assert.IsTrue(catalog.Supports(config.Engines[0],config.Contexts[0],CheckMechanism.Stability));
        var request=catalog.Compile(config);var report=new Service().Verify(model,request);var r=report.Jobs[0].Results.Single();
        Assert.AreEqual(EngineeringOutcome.Satisfied,r.Outcome,string.Join(";",r.Diagnostics.Select(d=>d.Message??d.Code)));
        Assert.IsTrue(r.Details.Metrics.Single().Capacity>0);
        if(bending)Assert.AreEqual(5e6,r.Details.Metrics.Single().Demand);
        else Assert.IsTrue(r.Details.Trace.Any(t=>t.Key=="Member.TorsionalExclusionSource"&&t.Expression==options["TorsionalExclusionSource"]));
        Assert.IsTrue(r.Details.Trace.Any(t=>t.Key=="Member.External."+(bending?"Mcr":"Ncr1")+".Original"&&t.Unit==(bending?"KNm":"KN")));
        using var stream=new MemoryStream();CheckReportArchive.Save(report.Jobs,stream);stream.Position=0;
        var restored=CheckReportArchive.Load(stream);Assert.AreEqual(EngineeringOutcome.Satisfied,Service.CurrentOutcome(restored,model,request));
        config.Engines[0].Parameters["ParameterSet"]="changed assumptions";
        Assert.AreEqual(EngineeringOutcome.NotEvaluated,Service.CurrentOutcome(restored,model,catalog.Compile(config)));
    }

    [DataTestMethod] [DataRow("missing-mode")] [DataRow("wrong-units")] [DataRow("changed-state")] [DataRow("nonconstant")]
    public void MissingOrInconsistentDataNeverProduceAPassingMember(string problem)
    {
        var (model,plan)=SteelMemberPipelineTest.Fixture();
        if(problem=="nonconstant")ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[250],plan.Results[0]).Last().ResultBeamForces.N*=2;
        var options=Options(model,plan);
        if(problem=="missing-mode")options.Remove("TorsionalExclusionSource");
        if(problem=="wrong-units")options["Ncr1.Unit"]="KNm";
        if(problem=="changed-state")options["MemberFingerprint"]="another state";
        var r=new Service().Verify(model,SteelMemberPipelineTest.Request(plan,options,new StandardNTC2018Steel())).Jobs[0].Results.Single();
        Assert.AreEqual(EngineeringOutcome.NotEvaluated,r.Outcome);Assert.IsNull(r.Utilization);
    }

    [TestMethod]
    public void NtcFactorsCannotBeOverriddenAndModesRemainDistinct()
    {
        var (model,plan)=SteelMemberPipelineTest.Fixture();var options=Options(model,plan);
        var ntc=new StandardNTC2018Steel();ntc.GammaM1=1;
        var r=new Service().Verify(model,SteelMemberPipelineTest.Request(plan,options,ntc)).Jobs[0].Results.Single();
        Assert.AreEqual(EngineeringOutcome.NotEvaluated,r.Outcome);
        options["McrBasis"]="UniformMoment";
        r=new Service().Verify(model,SteelMemberPipelineTest.Request(plan,options,new StandardNTC2018Steel())).Jobs[0].Results.Single();
        Assert.AreEqual(EngineeringOutcome.NotEvaluated,r.Outcome);
    }
}
