using System.IO.Compression;
using GPC.Model.Checker.Configuration;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Checking.Reports;
using GPC.Model.Core;
using GPC.Model.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service=GPC.Model.Checker.ModelChecker;

namespace ModelChecker.Tests;

/// <summary>Archives written before the member serviceability contracts (GPCModel 5.23.0.2, GPCModelChecker 2.33.0.0, master
/// 70caf3b3/853b56ef): the split and reversed SteelReferenceFrame, job C1-ULS-A (member stability and section rows).
/// The model, the configuration and the report load; the stored scope, task identities, member snapshot fingerprint (the
/// MemberFingerprint parameter of the configuration) and evidence seals are the ones the current code computes.
/// Saving the loaded report and configuration writes the same text. The engine versions are not compared: any rebuild of an engine changes them by design.</summary>
[TestClass]
public class MemberServiceabilityLegacyArchiveTest
{
    private static string Path(string name) => System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", "member-legacy-5.23.0.2", name);
    private static Stream Fixture(string name)
    {
        var plain = new MemoryStream();
        using (var file = File.OpenRead(Path(name + ".xml.gz"))) using (var zip = new GZipStream(file, CompressionMode.Decompress)) zip.CopyTo(plain);
        plain.Position = 0; return plain;
    }

    [TestMethod]
    public void ArchivesWrittenBeforeMemberServiceabilityKeepTheirScopesSnapshotsAndSeals()
    {
        GPC.Model.Models.Model model; VerificationConfiguration config; IReadOnlyList<CheckReport> reports;
        using (var s = File.OpenRead(Path("model.zip"))) model = ModelArchive.Load(s);
        using (var s = Fixture("configuration")) config = ConfigurationArchive.Load(s);
        using (var s = Fixture("reports")) reports = CheckReportArchive.Load(s);
        var report = reports.Single();
        // Saving what was loaded writes the same documents: the new optional members are not emitted when absent.
        string Text(Stream s) { using var r = new StreamReader(s); return r.ReadToEnd(); }
        string WithoutGuids(string xml) => System.Text.RegularExpressions.Regex.Replace(xml,
            "(<Guid [^>]*>)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}(</Guid>)", "$1$2");
        using (var saved = new MemoryStream()) using (var original = Fixture("reports"))
        { CheckReportArchive.Save(reports, saved); saved.Position = 0; Assert.AreEqual(WithoutGuids(Text(original)), WithoutGuids(Text(saved))); }
        using (var saved = new MemoryStream()) using (var original = Fixture("configuration"))
        { ConfigurationArchive.Save(config, saved); saved.Position = 0; Assert.AreEqual(Text(original), Text(saved)); }
        Assert.IsTrue(report.HasUnchangedScope);
        Assert.IsTrue(report.Results.All(r => r.HasUnchangedEvidence));
        Assert.AreEqual(EngineeringOutcome.Satisfied, report.Outcome);
        // Same scope digest and task identities from the archived request on the archived model.
        var plan = BeamCheckPlan.PrepareActions(model, report.BeamScope);
        Assert.AreEqual(report.ScopeFingerprint, plan.ScopeFingerprint);
        CollectionAssert.AreEqual(report.Results.Select(r => r.PlanItemId).ToArray(), plan.WorkItems.Select(w => w.Id).ToArray());
        var member = plan.WorkItems.Single(w => w.Scope == CheckScope.PhysicalMember);
        var stored = report.Results.Single(r => r.Scope == CheckScope.PhysicalMember);
        string fingerprint = ModelValues.Fingerprint(new object[] { member.Member.Snapshot });
        Assert.AreEqual(ModelValues.Fingerprint(new object[] { stored.MemberInput }), fingerprint);
        Assert.AreEqual(config.Engines.Single(e => e.Kind == GPC.Model.Checker.SteelMemberPipelineChecker.EngineId).Parameters["MemberFingerprint"], fingerprint);
        Assert.IsTrue(plan.IsCurrent);
        // The archived configuration still runs, with the stored member utilization.
        var again = new Service().Verify(model, EngineCatalog.BuiltIn().Compile(config));
        Assert.AreEqual(EngineeringOutcome.Satisfied, again.Outcome);
        var rerun = again.Jobs.Single().Results.Single(r => r.Scope == CheckScope.PhysicalMember);
        Assert.AreEqual(stored.Utilization!.Value, rerun.Utilization!.Value, 1e-12);
        Assert.AreEqual(report.ScopeFingerprint == again.Jobs.Single().ScopeFingerprint, rerun.EngineVersion == stored.EngineVersion);
    }
}
