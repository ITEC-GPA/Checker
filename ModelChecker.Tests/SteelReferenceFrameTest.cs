using GPC.Examples;
using GPC.Model.Checker;
using GPC.Model.Checker.Configuration;
using GPC.Model.Checking.Contracts;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Persistence;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Queries;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service=GPC.Model.Checker.ModelChecker;

namespace ModelChecker.Tests;

[TestClass]
public class SteelReferenceFrameTest
{
    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void HColumnsAndRhsCrossbarRunBothConcomitantDesignStatesAndPersist(bool ntc)
    {
        var model=SteelReferenceFrame.Create();
        var config=ConfigurationArchive.Copy(SteelReferenceFrame.Configure(model,ntc));
        var request=EngineCatalog.BuiltIn().Compile(config);var report=new Service().Verify(model,request);
        Assert.AreEqual(6,report.Jobs.Count);Assert.AreEqual(36,report.Required);
        Assert.AreEqual(EngineeringOutcome.Satisfied,report.Outcome,string.Join(";",report.Jobs.SelectMany(j=>j.Results).SelectMany(r=>r.Diagnostics).Select(d=>d.Message??d.Code)));
        foreach(var job in report.Jobs) {
            Assert.AreEqual(6,job.Results.Count);Assert.AreEqual(1,job.Results.Count(r=>r.Scope==CheckScope.PhysicalMember));
            var member=job.Results.Single(r=>r.Scope==CheckScope.PhysicalMember);
            bool strong=member.MemberInput.Samples[0].State.ConcomitantState=="ULS-B",beam=member.Target.MemberId=="B1";
            Assert.AreEqual(beam?(strong?30e6:20e6):-(strong?30000:20000),member.Details.Metrics.Single().Demand!.Value,1e-6);
            Assert.IsTrue(member.MemberInput.Samples.All(s=>s.State.ConcomitantState==(strong?"ULS-B":"ULS-A")));
        }
        using var stream=new MemoryStream();CheckReportArchive.Save(report.Jobs,stream);stream.Position=0;
        var restored=CheckReportArchive.Load(stream);Assert.AreEqual(EngineeringOutcome.Satisfied,Service.CurrentOutcome(restored,model,request));
        ResultQueries.Samples<StationResultBeamForces>(model.BeamElements[100],SteelReferenceFrame.Plan("C1","ULS-A").Results[0]).First().ResultBeamForces.N*=2;
        Assert.AreEqual(EngineeringOutcome.NotEvaluated,Service.CurrentOutcome(restored,model,request));
    }

    [TestMethod]
    public void DifferentNormsUseSameActionsButKeepTheirOwnFactors()
    {
        var model=SteelReferenceFrame.Create();var catalog=EngineCatalog.BuiltIn();
        var ec=new Service().Verify(model,catalog.Compile(SteelReferenceFrame.Configure(model,false)));
        var ntc=new Service().Verify(model,catalog.Compile(SteelReferenceFrame.Configure(model,true)));
        for(int j=0;j<6;j++) {
            var a=ec.Jobs[j].Results.Single(r=>r.Scope==CheckScope.PhysicalMember).Details.Metrics.Single();
            var b=ntc.Jobs[j].Results.Single(r=>r.Scope==CheckScope.PhysicalMember).Details.Metrics.Single();
            Assert.AreEqual(a.Demand,b.Demand);Assert.AreEqual(a.Utilization!.Value*1.05,b.Utilization!.Value,1e-10);
        }
    }
}
