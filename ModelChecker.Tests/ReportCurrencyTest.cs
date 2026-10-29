using System.Diagnostics;
using System.Reflection;
using GPC.Model.Checker;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Reports;
using GPC.Model.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;

namespace ModelChecker.Tests;

/// <summary>"Is this archived report still current for this model?" compares the provenance that the rows share with the model once
/// per call, not once per row (row by row: 4,000 rows on 1,000 beams took about six minutes), and still sees every change: a changed
/// input makes exactly the jobs that read it historical, a change made after a call is seen by the next one, a row with another
/// provenance is compared on its own.</summary>
[TestClass]
public class ReportCurrencyTest
{
    /// <summary>The keys of the values GPCModel computes from the model during <paramref name="call"/>, through its thread-local
    /// test hook (ValidationReadScope.Computed): "analysis" is the analysis fingerprint, a walk of the whole model.</summary>
    internal static List<string> Computed(Action call)
    {
        var hook = typeof(GPC.Model.Models.Model).Assembly.GetType("GPC.Model.Checking.Preparation.ValidationReadScope", true)!
            .GetField("Computed", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(hook, "GPCModel test hook ValidationReadScope.Computed");
        var keys = new List<string>(); hook!.SetValue(null, new Action<string>(keys.Add));
        try { call(); }
        finally { hook.SetValue(null, null); }
        return keys;
    }

    private static MultiMaterialCheckRequest Only(MultiMaterialCheckRequest request, int job)
    {
        var only = new MultiMaterialCheckRequest(); only.Jobs.Add(request.Jobs[job]);
        return only;
    }

    [TestMethod]
    public void TheModelIsReadAFixedNumberOfTimesPerCallWhateverTheRows()
    {
        int AnalysisWalks(int cases)
        {
            var f = PipelineColumns.Build(20, cases); var report = new Service().Verify(f.Model, f.Request);
            Assert.AreEqual(20 * cases, report.Required); Assert.AreEqual(EngineeringOutcome.Satisfied, report.Outcome);
            var keys = Computed(() => Assert.AreEqual(EngineeringOutcome.Satisfied, Service.CurrentOutcome(report, f.Model, f.Request)));
            Console.WriteLine($"{20 * cases} rows: {keys.Count(k => k == "analysis")} analysis walks, {keys.Count} values computed");
            return keys.Count(k => k == "analysis");
        }
        int few = AnalysisWalks(1), many = AnalysisWalks(8);
        Assert.AreEqual(few, many);
        Assert.IsTrue(few <= 4, few.ToString());
    }

    /// <summary>4,000 rows on 1,000 beams: row by row about 360 s, now about 1 s. The bound is generous on purpose: it fails only if the
    /// cost grows again with the rows times the elements.</summary>
    [TestMethod]
    public void FourThousandRowsOnAThousandBeamsAreCurrentInSeconds()
    {
        var f = PipelineColumns.Build(1000, 4); var report = new Service().Verify(f.Model, f.Request);
        Assert.AreEqual(4000, report.Required); Assert.AreEqual(EngineeringOutcome.Satisfied, report.Outcome);
        var watch = Stopwatch.StartNew();
        var keys = Computed(() => Assert.AreEqual(EngineeringOutcome.Satisfied, Service.CurrentOutcome(report, f.Model, f.Request)));
        Console.WriteLine($"4,000 rows, 1,000 beams: {watch.Elapsed.TotalSeconds:F2} s, {keys.Count(k => k == "analysis")} analysis walks");
        Assert.IsTrue(keys.Count(k => k == "analysis") <= 4);
        Assert.IsTrue(watch.Elapsed.TotalSeconds < 60, watch.Elapsed.ToString());
    }

    [TestMethod]
    public void AChangedSampleMakesExactlyItsJobHistoricalAndEachCallReadsTheModelAgain()
    {
        // Cases C0, C2 in job 0 and C1, C3 in job 1.
        var f = PipelineColumns.Build(10, 4, jobs: 2); var report = new Service().Verify(f.Model, f.Request);
        Assert.AreEqual(40, report.Required); Assert.AreEqual(EngineeringOutcome.Satisfied, report.Outcome);
        EngineeringOutcome Job(int j) => Service.CurrentOutcome(new[] { report.Jobs[j] }, f.Model, Only(f.Request, j));
        Assert.AreEqual(EngineeringOutcome.Satisfied, Service.CurrentOutcome(report, f.Model, f.Request));
        Assert.AreEqual(EngineeringOutcome.Satisfied, Job(0)); Assert.AreEqual(EngineeringOutcome.Satisfied, Job(1));

        // After the first calls the C1 sample of the first column changes: the next calls see it, in job 1 only.
        var sample = f.Samples[1];
        Assert.AreEqual("C1", sample.Case.Name);
        sample.ResultBeamForces.M1 += 1000;
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Service.CurrentOutcome(report, f.Model, f.Request));
        Assert.AreEqual(EngineeringOutcome.Satisfied, Job(0)); Assert.AreEqual(EngineeringOutcome.NotEvaluated, Job(1));
        // Changed back: current again, nothing of the earlier calls is kept.
        sample.ResultBeamForces.M1 -= 1000;
        Assert.AreEqual(EngineeringOutcome.Satisfied, Service.CurrentOutcome(report, f.Model, f.Request));

        // An element input of the analysis model is in the provenance of every row: both jobs.
        f.Model.NodesElements[11].Position.Z += 1;
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Job(0)); Assert.AreEqual(EngineeringOutcome.NotEvaluated, Job(1));
        f.Model.NodesElements[11].Position.Z -= 1;
        Assert.AreEqual(EngineeringOutcome.Satisfied, Service.CurrentOutcome(report, f.Model, f.Request));
    }

    [TestMethod]
    public void ARowWithAnotherProvenanceIsComparedOnItsOwn()
    {
        var f = PipelineColumns.Build(10, 2); var report = new Service().Verify(f.Model, f.Request);
        var rows = report.Jobs[0].Results; var shared = rows[0].Provenance;
        Assert.AreEqual(20, rows.Count); Assert.IsTrue(rows.All(r => ReferenceEquals(r.Provenance, shared)));
        // Equal values in another object (as read back from an archive): current.
        rows[7].Provenance = ModelArchive.CopyValue(shared); rows[7].SealEvidence();
        Assert.AreEqual(EngineeringOutcome.Satisfied, Service.CurrentOutcome(report, f.Model, f.Request));
        // The provenance of another model's run on one row only: that row is compared on its own and the report is historical.
        var foreign = new Service().Verify(PipelineColumns.Build(10, 2).Model, f.Request).Jobs[0].Results[0].Provenance;
        Assert.IsFalse(foreign.IsCurrent(f.Model));
        rows[13].Provenance = foreign; rows[13].SealEvidence();
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, Service.CurrentOutcome(report, f.Model, f.Request));
        rows[13].Provenance = shared; rows[13].SealEvidence();
        Assert.AreEqual(EngineeringOutcome.Satisfied, Service.CurrentOutcome(report, f.Model, f.Request));
    }
}
