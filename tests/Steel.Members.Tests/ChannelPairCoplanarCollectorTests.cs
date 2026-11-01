using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Reporting;
using GPC.Model.Checking.Contracts;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class ChannelPairCoplanarCollectorTests
{
    private static ChannelPairCoplanarCollectorInput Input(bool ntc = false, double totalN = 200000, string change = "")
    {
        var code = ntc ? ConnectionDesignCode.Ntc2018() : ConnectionDesignCode.Eurocode3(1, 1.25, 1.25, "EN recommended");
        var bolt = new ConnectionBolt(20, 22, 245, ConnectionBoltGrade.Class8_8, true);
        var channel = new SectionC(200, 8, 75, 12, 75, 12, "C200");
        Section shape = change == "single" ? channel : SectionBuiltUp.DoubleChannel(channel, change == "gap" ? 12 : 16, change != "toes", "2C200");
        if (change == "offset") shape = new SectionBuiltUp(new[] { new SectionBuiltUp.Part(channel, 8, 0), new SectionBuiltUp.Part(channel, -8, 10, mirrorX: true) }, "offset");
        var section = new SteelSectionInput(new SteelSection(shape, new SteelMaterial("S275", 210000, 275, 430), formedType: Section.FormedTypes.HotFinished));
        var end = new ChannelWebTensionInput(code, section, totalN / 2,
            change == "aisc" ? ChannelWebTensionMethod.AiscShearLagAlternative : ChannelWebTensionMethod.ConnectedWebOnly,
            bolt, 2, 80, 3, 70, 40, 37, "Actual channel/hardware drawing", "SLS hole clearance accepted", "AISC accepted for standalone component only");
        var t = new GussetTwinFlatTensionInput(code, new ConnectionPlate(240, 16, 275, 430),
            new ConnectionPlate(change == "strip-width" ? 201 : 200, change == "strip-thickness" ? 9 : 8, change == "strip-strength" ? 355 : 275, 430),
            change == "bolt" ? new ConnectionBolt(24, 26, 353, ConnectionBoltGrade.Class8_8, true) : bolt,
            new PlanarConnectionActions(change == "share" ? totalN + 1 : totalN, 0, 0, 0, 0, change == "eccentric" ? 1 : 0),
            change == "rows" ? 2 : 3, change == "pitch" ? 75 : 70, change == "gauge" ? 82 : 80, 40, change == "end" ? 45 : 40,
            holeClearanceServiceabilitySource: "SLS hole clearance accepted");
        var c = new GussetTwinFlatCollectorInput(t, new ConnectionPlate(change == "width" ? 260 : 240, 16, change == "weak" ? 10 : 235, change == "weak" ? 15 : 360),
            new SteelActions(change == "cut" ? totalN + 1 : totalN, 0, 0, 0, change == "cut-moment" ? 1e-20 : 0, 0),
            change == "distance" ? 100 : 150, 16, 240, 355, 510, change != "partial", change != "aligned",
            change == "execution" ? "" : "Full-width CJP execution and matching metal", "LC1 actual collector cut");
        return new ChannelPairCoplanarCollectorInput(end, c);
    }

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void SamePhysicalTerminalMatchesIndependentNetSectionsAndEquilibrium(bool ntc)
    {
        var r = ChannelPairCoplanarCollector.Verify(Input(ntc));
        Assert.IsTrue(r.IsComplete, string.Join(";", r.Diagnostics));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status);
        // C200 web strip: Ag=200*8=1600; An=(200-2*22)*8=1248; force100kN per channel.
        // Gusset: Ag=240*16=3840; An=(240-44)*16=3136; force200kN.
        double gm0 = ntc ? 1.05 : 1;
        Check(r, "Channel.Web.GrossYielding", 100000, 1600 * 275 / gm0);
        Check(r, "Channel.Web.NetRupture", 100000, .9 * 1248 * 430 / 1.25);
        Check(r, "Transfer.Gusset.NetRupture", 200000, .9 * 3136 * 430 / 1.25);
        // U block: Ant=(80-22)*16=928, Anv=2*16*(40+140-2.5*22)=4000.
        Check(r, "Transfer.Gusset.BlockInside", 200000, 928 * 430 / 1.25 + 4000 * 275 / (Math.Sqrt(3) * gm0));
        // Six through bolts, two shear planes each: N/6; one plane of the channel check N/12.
        Check(r, "Transfer.Bolt1.Shear", 200000.0 / 6, 2 * .6 * 800 * 245 / 1.25);
        Check(r, "Channel.Bolt.Shear", 100000.0 / 6, .6 * 800 * 245 / 1.25);
        Check(r, "Collector.Continuation.GrossYield", 200000, 3840 * 235 / gm0);
        Check(r, "Collector.ButtWeld.FullStrength", 200000, Math.Min(3840 * 235 / gm0, .9 * 3840 * 360 / 1.25));
        var summary = SteelAssessmentSummary.From(r);
        Assert.AreEqual(EngineeringOutcome.Satisfied, summary.Outcome);
        Assert.AreEqual(r.Checks.Count, r.Checks.Select(v => v.Id).Distinct().Count());
        Assert.AreEqual(r.Trace.Count, r.Trace.Select(v => v.Name).Distinct().Count());
    }

    [DataTestMethod]
    [DataRow("single")][DataRow("toes")][DataRow("offset")][DataRow("gap")][DataRow("aisc")]
    [DataRow("strip-width")][DataRow("strip-thickness")][DataRow("strip-strength")][DataRow("bolt")]
    [DataRow("share")][DataRow("rows")][DataRow("pitch")][DataRow("gauge")][DataRow("end")]
    [DataRow("eccentric")][DataRow("width")][DataRow("cut")][DataRow("cut-moment")][DataRow("distance")]
    [DataRow("partial")][DataRow("aligned")][DataRow("execution")]
    public void MismatchedOrUnsupportedPhysicalDetailsCannotPass(string change)
    {
        var r = ChannelPairCoplanarCollector.Verify(Input(change: change));
        Assert.IsFalse(r.IsComplete); Assert.AreEqual(ConnectionStatus.NotSupported, r.Status, string.Join(";", r.Diagnostics));
        Assert.IsNull(r.Utilization); Assert.AreEqual(0, r.Checks.Count);
    }

    [TestMethod]
    public void ActualWeakCollectorFailureIsRetained()
    {
        var r = ChannelPairCoplanarCollector.Verify(Input(change: "weak"));
        Assert.IsTrue(r.IsComplete); Assert.AreEqual(ConnectionStatus.Failed, r.Status);
        Assert.IsTrue(r.Checks.Any(c => c.Id.StartsWith("Collector.") && !c.Passed));
    }

    [TestMethod]
    public void FailedChannelSurvivesUnsupportedContinuation()
    {
        var r = ChannelPairCoplanarCollector.Verify(Input(totalN: 3e6, change: "aligned"));
        Assert.IsFalse(r.IsComplete); Assert.AreEqual(ConnectionStatus.Failed, r.Status);
        Assert.IsTrue(r.Checks.Any(c => c.Id.StartsWith("Channel.") && !c.Passed));
        Assert.AreEqual(EngineeringOutcome.NotSatisfied, SteelAssessmentSummary.From(r).Outcome);
    }

    [TestMethod]
    public void NullTensionRemainsValidWithoutInventingAnAction()
    {
        var r = ChannelPairCoplanarCollector.Verify(Input(totalN: 0));
        Assert.IsTrue(r.IsComplete, string.Join(";", r.Diagnostics)); Assert.AreEqual(0, r.Utilization);
    }

    private static void Check(ConnectionVerification<ChannelPairCoplanarCollectorInput> r, string id, double n, double resistance)
    {
        var c = r.Checks.Single(v => v.Id == id);
        Assert.AreEqual(n, c.Demand, 1e-8, id); Assert.AreEqual(resistance, c.Resistance, 1e-7, id);
    }
}
