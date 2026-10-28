using System.Text.Json;
using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Reporting;
using GPC.Model.Checking.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;
[TestClass]
public class SteelAssessmentSummaryTests
{
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void CompleteSpliceAndReportUseTheSameOutcome(bool ntc)
    {
        var r=WeldedIBeamSpliceStandard.Verify(WeldedIBeamSpliceStandardTests.Workflow(ntc,SpliceBoltCategory.A));
        Assert.AreEqual(ConnectionStatus.Passed,r.Status);
        var s=SteelAssessmentSummary.From(r);var report=SteelAssessmentSummary.From(BridgeSpliceReport.FromBolted(r));
        Assert.IsTrue(s.IsComplete);Assert.IsFalse(s.HasKnownFailure);
        Assert.AreEqual(EngineeringOutcome.Satisfied,s.Outcome);
        Assert.AreEqual(s.Outcome,report.Outcome);Assert.AreEqual(s.Scope,report.Scope);
        Assert.AreEqual(0,s.MissingRequirements.Count);Assert.IsTrue(s.ExternalConditions.Count>0);
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void KnownFailureIsVisibleEvenIfRequiredSlipCaseIsMissing(bool ntc)
    {
        var r=WeldedIBeamSpliceStandard.Verify(WeldedIBeamSpliceStandardTests.Workflow(ntc,SpliceBoltCategory.B,100,slip:false));
        Assert.AreEqual(ConnectionStatus.NotSupported,r.Status); // unchanged legacy status contract
        var s=SteelAssessmentSummary.From(r);
        Assert.IsFalse(s.IsComplete);Assert.IsTrue(s.HasKnownFailure);
        Assert.AreEqual(EngineeringOutcome.NotSatisfied,s.Outcome);
        Assert.IsTrue(s.MissingRequirements.Any(x=>x.Contains("SERVICEABILITY")));
        Assert.AreEqual(s.Outcome,SteelAssessmentSummary.From(BridgeSpliceReport.FromBolted(r)).Outcome);
    }
    [TestMethod]
    public void UnsuccessfulSufficientHierarchyIsNotAResistanceFailure()
    {
        var r=WeldedIBeamSpliceStandard.Verify(BridgeSpliceStandardBoltedCoverTests.Workflow(
            BridgeSpliceStandardBoltedCoverTests.Input(smallBolt:false)));
        Assert.IsFalse(SteelAssessmentSummary.From(r).IsComplete);Assert.IsFalse(SteelAssessmentSummary.From(r).HasKnownFailure);
        Assert.AreEqual(EngineeringOutcome.NotEvaluated,SteelAssessmentSummary.From(r).Outcome);
        Assert.IsTrue(SteelAssessmentSummary.From(r).MissingRequirements.Any(x=>x.Contains("BlockHierarchy")));
    }
    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void SectionSummaryPreservesPipelineOutcomeAndMissingPreparation(bool prepare)
    {
        var r=WeldedIBeamSpliceStandardTests.Section(WeldedIBeamSpliceStandardTests.Input(),prepare:prepare);
        Assert.AreEqual(r.Outcome,SteelAssessmentSummary.From(r).Outcome);Assert.AreEqual(r.IsComplete,SteelAssessmentSummary.From(r).IsComplete);
        if(!prepare) Assert.IsTrue(SteelAssessmentSummary.From(r).MissingRequirements.Count>0);
    }
    [TestMethod]
    public void ComponentScopeAndSuppressedUnqualifiedChecksAreNotPromoted()
    {
        var r=WeldedIBeamSpliceStandardModel.Verify(WeldedIBeamSpliceStandardTests.Input(
            detail:WeldedIBeamSpliceStandardTests.Detail(new(1e5,0,1,0,0,0))));
        Assert.IsFalse(SteelAssessmentSummary.From(r).IsComplete);Assert.AreEqual(EngineeringOutcome.NotEvaluated,SteelAssessmentSummary.From(r).Outcome);
        Assert.AreEqual(r.Scope,SteelAssessmentSummary.From(r).Scope);Assert.AreEqual(0,r.Checks.Count);
    }
    [TestMethod]
    public void TerminalReportDoesNotBecomeAWholeMemberAssessment()
    {
        var m=PackedWeldedCaseData.Model();
        var p=GPC.Checkers.Steel.Inputs.SteelPackedPairAssessment.FromWeldedPacking(
            SteelWeldedPacking.Verify(m,PackedWeldedCaseData.Lap(m)),
            SteelPackedPairTerminal.Verify(m,PackedWeldedCaseData.Terminal(m,free:5000)),"Forks");
        var r=StandardConnectionCaseReport.Create(p);
        Assert.AreEqual(EngineeringOutcome.NotSatisfied,SteelAssessmentSummary.From(r).Outcome);
        Assert.IsFalse(r.CanQualifyWholeMember);Assert.AreEqual(r.Scope,SteelAssessmentSummary.From(r).Scope);
        Assert.IsTrue(SteelAssessmentSummary.From(r).ExternalConditions.Any(x=>x.Contains("T/FT")));
    }
    [TestMethod]
    public void CommonProjectionIsImmutableAndCanBeSerializedWithoutOriginalGraph()
    {
        var s=SteelAssessmentSummary.From(WeldedIBeamSpliceStandard.Verify(WeldedIBeamSpliceStandardTests.Workflow(slip:false)));
        Assert.ThrowsException<NotSupportedException>(()=>((IList<string>)s.MissingRequirements).Clear());
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(s));
        Assert.IsFalse(json.RootElement.GetProperty("IsComplete").GetBoolean());
        Assert.IsTrue(json.RootElement.TryGetProperty("HasKnownFailure",out _));
        Assert.IsFalse(json.RootElement.TryGetProperty("Utilization",out _));
    }
    [TestMethod] public void NullSummaryInputIsRejected()
        =>Assert.ThrowsException<ArgumentNullException>(()=>SteelAssessmentSummary.From((BridgeSpliceReport)null!));
}
