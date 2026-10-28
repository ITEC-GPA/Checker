using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

/// <summary>The calculators share the owned section snapshot read-only instead of an XML copy
/// per call. Running them must leave the snapshot bit-identical, so later checks of the same cut
/// still see the original section.</summary>
[TestClass]
public class SectionSnapshotSharingTests
{
    private static string Snapshot(SteelSectionInput s) => ModelValues.Fingerprint(new object[] { s.CreateSectionCopy() });

    [DataTestMethod]
    [DataRow("H")] [DataRow("C")] [DataRow("T")] [DataRow("L")] [DataRow("RHS")] [DataRow("CHS")] [DataRow("Rectangular")] [DataRow("Circular")]
    public void SelectionPipelineAndStabilityLeaveTheSnapshotUnchanged(string family)
    {
        foreach (bool ntc in new[] { false, true })
        foreach (var (n, m1, m2) in new[] { (-1e5, 0d, 0d), (0d, 2e7, 0d), (-1e5, 2e7, 5e6), (1e5, 0d, 0d) })
        {
            var b = CommonSectionCoverageTests.Input(CommonSectionCoverageTests.Shape(family), ntc, n, m1, m2);
            string before = Snapshot(b.Section);
            SteelVerificationPipeline.Execute(SteelMethodSelector.Select(b));
            var shear = new SteelVerificationInput(b.Section, new SteelActions(0, 0, 1e4, 0, 0, 0), b.Code);
            SteelVerificationPipeline.Execute(SteelMethodSelector.Select(shear));
            try { SteelAutomaticStability.CompressionCurves(b.Section, b.Code.SectionCode.Standard); } catch (NotSupportedException) { }
            var member = new SteelVerificationInput(b.Section, new SteelActions(n < 0 ? n : 0, 0, 0, 0, n < 0 ? 0 : m1, 0), b.Code, new SteelMemberInput(3200, 3200, 3200));
            try { SteelAutomaticStability.UniformFork(member, n >= 0 && m1 != 0, "declared two forks and free warping"); }
            catch (Exception e) when (e is NotSupportedException || e is ArgumentException) { }
            Assert.AreEqual(before, Snapshot(b.Section), family + " ntc=" + ntc + " N=" + n + " M1=" + m1 + " M2=" + m2);
        }
    }
}
