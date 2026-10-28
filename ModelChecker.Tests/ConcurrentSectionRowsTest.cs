using GPC.Examples;
using GPC.Model.Checker;
using GPC.Model.Checker.Configuration;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Data.Steel;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using GPC.Model.Structure.Assignments;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Service = GPC.Model.Checker.ModelChecker;

namespace ModelChecker.Tests;

/// <summary>Steel pipeline section rows compute their engine part in parallel, ahead of the sequential
/// pass; results, order, cancellation and the session record are those of the sequential run.</summary>
[TestClass]
public class ConcurrentSectionRowsTest
{
    private sealed class Progress : IProgress<VerificationProgress>
    {
        private readonly Action<VerificationProgress> _action;
        public Progress(Action<VerificationProgress> action) => _action = action;
        public void Report(VerificationProgress value) => _action(value);
    }

    [TestMethod]
    public void RepeatedRunsGiveTheSameEvidenceRowByRow()
    {
        var model = SteelBeamColumnFrame.Create(true, true, true);
        var request = EngineCatalog.BuiltIn().Compile(SteelBeamColumnFrame.Configure(model, false));
        string Run()
        {
            var report = new Service().Verify(model, request);
            Assert.AreEqual(EngineeringOutcome.Satisfied, report.Outcome);
            return string.Join("|", report.Jobs.SelectMany(j => j.Results).Select(r => r.PlanItemId + ":" + r.EvidenceFingerprint));
        }
        string first = Run();
        for (int i = 0; i < 3; i++) Assert.AreEqual(first, Run());
    }

    /// <summary>One job over two columns with different sections: the rows of the second section are computed
    /// ahead in the same block, but a cancelled row neither counts as executed nor records its section.</summary>
    [TestMethod]
    public void CancellationAfterTheFirstRowExecutesAndRecordsOnlyThatRow()
    {
        var model = SteelReferenceFrame.Create(false, false, m => {
            var other = new SteelSection(new SectionH(250, 10, 150, 16, 150, 16, "Second column"), SteelMaterialEN1993Data.S355); m.AddProperty(other);
            foreach (var beam in m.BeamElements.Values.Where(b => b.Assignments.LogicalMember == "C2"))
            { beam.BeamProperty = other; beam.Assignments.Sections.Clear(); beam.Assignments.Sections.Add(new BeamSectionAssignment { Start = 0, End = 1, Property = other }); }
        });
        var plan = SteelReferenceFrame.Plan("C1", "ULS-A"); plan.MemberIds = new[] { "C1", "C2" }; plan.MemberChecks = Array.Empty<MemberCheckSpecification>();
        MultiMaterialCheckRequest Request() { var r = new MultiMaterialCheckRequest(); r.Jobs.Add(new MultiMaterialCheckJob { Name = "Columns", Plan = plan, Assignments = new[] {
            new MaterialCheckerAssignment { Checker = new SteelPipelineMaterialChecker(new StandardEN1993p11(), "2005", parameters: new Dictionary<string, string> { ["Unperforated"] = "true" }) } } }); return r; }
        var complete = new Service().Verify(model, Request());
        Assert.AreEqual(2, complete.CreatedCheckers); Assert.AreEqual(complete.Required, complete.Executed);
        var cancel = new CancellationTokenSource(); var events = new List<VerificationProgress>();
        var report = new Service().Verify(model, Request(), cancel.Token, new Progress(p => { events.Add(p); cancel.Cancel(); }));
        Assert.AreEqual(1, report.Executed); Assert.AreEqual(report.Required - 1, report.Summary.Cancelled);
        Assert.AreEqual(1, report.CreatedCheckers); Assert.AreEqual(report.Required, events.Count);
        Assert.IsTrue(events.Select(e => e.Completed).SequenceEqual(Enumerable.Range(1, report.Required)));
        Assert.AreEqual(complete.Jobs[0].Results[0].EvidenceFingerprint, report.Jobs[0].Results[0].EvidenceFingerprint);
    }
}
