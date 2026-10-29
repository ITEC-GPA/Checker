using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Reports;
using GPC.Model.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;

namespace ModelChecker.Tests;

/// <summary>Report archives larger than one XML document (2,000,000 objects): saved by Model as a package of documents,
/// they load back as the same report, which stays current for its model and request.</summary>
[TestClass]
public class LargeReportArchiveTest
{
    /// <summary>7,000 steel pipeline section rows with full traces, about 2,700,000 objects: before the package Save failed
    /// (maximum number of items 2,000,000). Ten columns and 700 cases keep the currency check short; its cost grows with the
    /// rows times the elements of the model.</summary>
    [TestMethod]
    public void LargeSteelReportSurvivesTheArchiveAndStaysCurrent()
    {
        var f = PipelineColumns.Build(10, 700);
        var report = new Service().Verify(f.Model, f.Request);
        Assert.AreEqual(7000, report.Required); Assert.AreEqual(7000, report.Executed);
        Assert.AreEqual(EngineeringOutcome.Satisfied, report.Outcome);
        using var stream = new MemoryStream(); CheckReportArchive.Save(report.Jobs, stream);
        var bytes = stream.ToArray();
        Assert.IsTrue(bytes[0] == 0x50 && bytes[1] == 0x4B, "package");
        stream.Position = 0; var loaded = CheckReportArchive.Load(stream);
        Assert.AreEqual(1, loaded.Count); Assert.IsTrue(loaded[0].HasUnchangedScope);
        CollectionAssert.AreEqual(report.Jobs[0].Results.Select(r => r.PlanItemId + "|" + r.EvidenceFingerprint).ToArray(),
            loaded[0].Results.Select(r => r.PlanItemId + "|" + r.EvidenceFingerprint).ToArray());
        Assert.IsTrue(loaded[0].Results.All(r => r.HasUnchangedEvidence && r.Details.Trace.Count > 1));
        Assert.AreEqual(EngineeringOutcome.Satisfied, Service.CurrentOutcome(loaded, f.Model, f.Request));
        // A changed engine configuration makes the archived report historical.
        f.Code.GammaM0 = 1.05;
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Service.CurrentOutcome(loaded, f.Model, f.Request));
    }
}
