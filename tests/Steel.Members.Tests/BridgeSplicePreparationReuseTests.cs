using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Reporting;
using GPC.Checkers.Steel.Sections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class BridgeSplicePreparationReuseTests
{
    [DataTestMethod]
    [DataRow(false, false, false)] [DataRow(true, false, false)]
    [DataRow(false, true, false)] [DataRow(true, true, false)]
    [DataRow(false, false, true)] [DataRow(true, false, true)]
    [DataRow(false, true, true)] [DataRow(true, true, true)]
    public void IdenticalSnapshotsShareOnlyTheirOwnSameCaseAssessment(bool ntc, bool welded, bool shared)
    {
        var template = BridgeSpliceApplicationTests.Bridge(WeldedIBeamSpliceStandardTests.Input(ntc));
        var left = WeldedIBeamSpliceStandardTests.Section(template).Plan.Input.Section;
        var right = shared ? left : new SteelSectionInput(left.CreateSectionCopy(), true);
        var cases = BridgeSpliceApplicationTests.Actions();
        int preparations = 0;
        IEnumerable<SteelShearBucklingAssessment> Prepare(SteelVerificationInput input)
        {
            preparations++;
            return BridgeSpliceApplicationTests.Prepare(input);
        }
        SteelVerificationResult[] l, r;
        BridgeSpliceReport report;
        if (welded)
        {
            SpliceButtWeldDetail Weld(double thickness, double width) => new(SpliceWeldPenetration.Full,
                thickness, width, 355, 510, "Qualified WPS", "Inspection evidence");
            var result = BridgeSpliceApplication.VerifyWelded(template.Plates.Policy.Code, left, right, cases,
                Weld(30, 400), Weld(16, 320), Weld(30, 400), "Same aligned butt plane", Prepare);
            l = result.Select(c => c.Result.LeftSection).ToArray();
            r = result.Select(c => c.Result.RightSection).ToArray();
            report = BridgeSpliceReport.FromWelded(result);
        }
        else
        {
            var input = BridgeSpliceApplication.CreateBolted(template, left, right, cases,
                WeldedIBeamSpliceStandardTests.Welds(), "Same physical section at the joint", SpliceBoltCategory.A,
                Prepare, blockMethod: SpliceBlockVerificationMethod.ActualCollinearForcesWithHierarchyFallback);
            l = input.Cases.Select(c => c.Parents.LeftSection).ToArray();
            r = input.Cases.Select(c => c.Parents.RightSection).ToArray();
            report = BridgeSpliceReport.FromBolted(WeldedIBeamSpliceStandard.Verify(input));
        }
        Assert.AreEqual(ConnectionStatus.Passed, report.Status, string.Join(";", report.MissingRequirements));
        Assert.AreEqual(shared ? 2 : 4, preparations, "Two cases; one preparation per distinct snapshot within each case.");
        Assert.AreNotSame(l[0], l[1], "No reuse across the two concomitant action cases.");
        for (int i = 0; i < cases.Length; i++)
        {
            Assert.AreEqual(shared, ReferenceEquals(l[i], r[i]));
            Assert.AreEqual(i == 0 ? 200000 : -200000, l[i].Plan.Input.Actions.N);
            Assert.AreEqual(i == 0 ? -50e6 : 50e6, l[i].Plan.Input.Actions.M1);
            Assert.AreEqual(l[i].Outcome, r[i].Outcome);
            var explicitResult = BridgeSpliceApplication.EvaluateSection(left, cases[i].Actions,
                template.Plates.Policy.Code, BridgeSpliceApplicationTests.Prepare);
            CollectionAssert.AreEqual(explicitResult.Checks.Select(c => c.Utilization).ToArray(),
                l[i].Checks.Select(c => c.Utilization).ToArray());
        }
        Assert.IsTrue(SteelAssessmentSummary.From(report).IsComplete);
    }
}
