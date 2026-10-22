using GPC.Examples;
using GPC.Model.Checker;
using GPC.Model.Checker.Configuration;
using GPC.Model.Checking.Contracts;
using GPC.Model.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service=GPC.Model.Checker.ModelChecker;

namespace ModelChecker.Tests;
[TestClass]
public class SteelClass4FrameTest
{
    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void CompleteHeadlessClass4WorkflowSurvivesMeshReversalAndArchives(bool ntc)
    {
        var unsplit=SteelClass4Frame.Create();var split=SteelClass4Frame.Create(true,true);
        var catalog=EngineCatalog.BuiltIn();
        var baseReport=new Service().Verify(unsplit,catalog.Compile(SteelClass4Frame.Configure(unsplit,ntc)));
        var config=ConfigurationArchive.Copy(SteelClass4Frame.Configure(split,ntc));
        var request=catalog.Compile(config);var report=new Service().Verify(split,request);
        foreach(var r in new[]{baseReport,report})
            Assert.AreEqual(EngineeringOutcome.Satisfied,r.Outcome,string.Join(";",r.Jobs.SelectMany(j=>j.Results).SelectMany(s=>s.Diagnostics).Select(d=>d.Code+" "+d.Message)));
        Assert.AreEqual(6,report.Jobs.Count);
        for(int j=0;j<6;j++) {
            var a=baseReport.Jobs[j].Results.Single(r=>r.Scope==CheckScope.PhysicalMember);
            var b=report.Jobs[j].Results.Single(r=>r.Scope==CheckScope.PhysicalMember);
            Assert.AreEqual(a.Utilization!.Value,b.Utilization!.Value,1e-10);
            Assert.IsTrue(b.Details.Trace.Any(t=>t.Key=="MemberStability.MemberClass"&&t.Value==4));
            Assert.IsTrue(b.Details.Trace.Any(t=>t.Key.EndsWith(".AreaEffective")||t.Key.EndsWith(".Weff1Min")));
        }
        Assert.IsTrue(report.Jobs.SelectMany(j=>j.Results).Where(r=>r.Target.MemberId=="B1")
            .Any(r=>r.Details.Trace.Any(t=>t.Key.EndsWith(".VbwRd"))));
        using var archive=new MemoryStream();CheckReportArchive.Save(report.Jobs,archive);archive.Position=0;
        Assert.AreEqual(EngineeringOutcome.Satisfied,Service.CurrentOutcome(CheckReportArchive.Load(archive),split,request));
    }
}
