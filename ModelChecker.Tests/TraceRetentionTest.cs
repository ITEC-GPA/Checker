using GPC.Examples;
using GPC.Model.Checker;
using GPC.Model.Checker.Configuration;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Checking.Reports;
using GPC.Model.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;

namespace ModelChecker.Tests;

/// <summary>Opt-in trace compaction: governing and not satisfied rows keep their trace, the others keep
/// outcome, utilization and metrics. Full (the default) is unchanged.</summary>
[TestClass]
public class TraceRetentionTest
{
    private static string Metrics(CheckResult r) => string.Join("|", r.Details.Metrics.Select(m =>
        m.Key + "=" + m.Demand?.ToString("R") + "/" + m.Capacity?.ToString("R") + "/" + m.Utilization?.ToString("R") + "/" + m.Passed));
    private static string Trace(CheckResult r) => string.Join("|", r.Details.Trace.Select(t => t.Key + "=" + t.Value?.ToString("R") + t.Unit + t.Expression));

    [TestMethod]
    public void GoverningAndFailedRowsKeepTheirTraceAndOutcomesAreUnchanged()
    {
        var model = SteelBeamColumnFrame.Create(true, true, true); var catalog = EngineCatalog.BuiltIn();
        var request = catalog.Compile(SteelBeamColumnFrame.Configure(model, false));
        var full = new Service().Verify(model, request);
        var compact = new Service { TraceRetention = CheckTraceRetention.GoverningAndFailed }.Verify(model, request);
        Assert.AreEqual(EngineeringOutcome.Satisfied, full.Outcome); Assert.AreEqual(full.Outcome, compact.Outcome);
        Assert.AreEqual(full.Executed, compact.Executed);
        var a = full.Jobs.SelectMany(j => j.Results).ToArray(); var b = compact.Jobs.SelectMany(j => j.Results).ToArray();
        Assert.AreEqual(a.Length, b.Length);
        int omitted = 0;
        for (int i = 0; i < a.Length; i++)
        {
            Assert.AreEqual(a[i].PlanItemId, b[i].PlanItemId); Assert.AreEqual(a[i].Outcome, b[i].Outcome);
            Assert.AreEqual(a[i].Utilization, b[i].Utilization); Assert.AreEqual(Metrics(a[i]), Metrics(b[i]));
            Assert.IsTrue(b[i].HasUnchangedEvidence);
            if (b[i].Details.Trace.Count == 1 && b[i].Details.Trace[0].Key == "Trace.Omitted") { omitted++; Assert.AreEqual(a[i].Details.Trace.Count, (int)b[i].Details.Trace[0].Value!.Value); }
            else Assert.AreEqual(Trace(a[i]), Trace(b[i]));
        }
        Assert.IsTrue(omitted > 0 && omitted < b.Length, omitted + " of " + b.Length);
        // Every metric of every target is still traced on its governing row.
        foreach (var target in b.GroupBy(r => r.Target.BeamId?.ToString() ?? r.Target.MemberId))
            foreach (var metric in target.SelectMany(r => r.Details.Metrics.Where(m => m.Utilization.HasValue).Select(m => (Row: r, m.Key, m.Utilization))).GroupBy(m => m.Key))
                Assert.AreNotEqual("Trace.Omitted", metric.OrderByDescending(m => m.Utilization).First().Row.Details.Trace[0].Key);
        using var stream = new MemoryStream(); CheckReportArchive.Save(compact.Jobs, stream); stream.Position = 0;
        Assert.AreEqual(EngineeringOutcome.Satisfied, Service.CurrentOutcome(CheckReportArchive.Load(stream), model, request));
    }

    [TestMethod]
    public void NotSatisfiedRowsKeepTheirTrace()
    {
        var model = SteelBeamColumnFrame.Create(); var config = SteelBeamColumnFrame.Configure(model, false);
        config.Engines[0].Parameters.Remove("MomentFactorSource");
        var report = new Service { TraceRetention = CheckTraceRetention.GoverningAndFailed }.Verify(model, EngineCatalog.BuiltIn().Compile(config));
        var member = report.Jobs[0].Results.Single(r => r.Scope == CheckScope.PhysicalMember);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, member.Outcome);
        Assert.IsFalse(report.Jobs.SelectMany(j => j.Results).Where(r => r.Outcome != EngineeringOutcome.Satisfied && r.Details != null)
            .Any(r => r.Details.Trace.Any(t => t.Key == "Trace.Omitted")));
    }
}
