using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

/// <summary>One physical detail prepared once; every action state reuses its demand-independent results.</summary>
[TestClass]
public class PreparedWeldedIBeamSpliceTests
{
    static readonly PlanarConnectionActions[] States = {
        new(2e5, -1e5, 0, 0, 0, 50e6), new(-2e5, 1e5, 0, 0, 0, -50e6), new(0, 3e5, 0, 0, 0, 0), new(5e5, 0, 0, 0, 0, 1e6), new(0, 0, 0, 0, 0, 0) };

    static WeldedIBeamSpliceDetailInput Independent(WeldedIBeamSpliceDetailInput t, PlanarConnectionActions a, string source, double webCoverEnd = double.NaN)
    {
        var b = t.Plates.Bolts; var d = b.Detail;
        var forces = new WeldedIBeamSpliceForceInput(d.Section, d.Top, d.Bottom, d.EachWebCover, d.WebBolts,
            double.IsNaN(webCoverEnd) ? d.WebCoverEnd : webCoverEnd, d.ButtGap, a, d.Packing);
        return new(new(t.Plates.Policy, new(b.Code, forces, b.FlangeBolt, b.WebBolt, b.FlangeHardwareDiameter, b.WebHardwareDiameter,
            source, b.HoleClearanceServiceabilitySource)), t.Exposure);
    }

    static void Same<T>(ConnectionVerification<T> a, ConnectionVerification<T> b) where T : class
    {
        Assert.AreEqual(a.Status, b.Status); CollectionAssert.AreEqual(a.Diagnostics.ToArray(), b.Diagnostics.ToArray());
        CollectionAssert.AreEqual(a.Checks.Select(c => (c.Id, c.Demand, c.Resistance, c.Reference, c.Unit, c.Role)).ToArray(),
            b.Checks.Select(c => (c.Id, c.Demand, c.Resistance, c.Reference, c.Unit, c.Role)).ToArray());
        CollectionAssert.AreEqual(a.Trace.Select(t => (t.Name, t.Value, t.Unit)).ToArray(), b.Trace.Select(t => (t.Name, t.Value, t.Unit)).ToArray());
    }

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void CasesOfOnePreparedDetailMatchIndependentInputsBitForBit(bool ntc)
    {
        var template = BridgeSpliceApplicationTests.Bridge(WeldedIBeamSpliceStandardTests.Input(ntc));
        var prepared = PreparedWeldedIBeamSplice.Prepare(template);
        foreach (var a in States)
        {
            var shared = prepared.CreateCase(a, "combination"); var own = Independent(template, a, "combination");
            Assert.IsTrue(prepared.Owns(shared)); Assert.IsFalse(prepared.Owns(own));
            Assert.AreSame(a, shared.Plates.Bolts.Detail.Actions);
            Same(WeldedIBeamSpliceBolts.Verify(own.Plates.Bolts).ComponentAssessment, WeldedIBeamSpliceBolts.Verify(shared.Plates.Bolts).ComponentAssessment);
            Same(WeldedIBeamSpliceDetailing.Verify(own).Detailing, WeldedIBeamSpliceDetailing.Verify(shared).Detailing);
            var po = WeldedIBeamSplicePlates.Verify(own.Plates); var ps = WeldedIBeamSplicePlates.Verify(shared.Plates);
            CollectionAssert.AreEqual(po.Diagnostics.ToArray(), ps.Diagnostics.ToArray());
            Assert.AreEqual(po.Covers.Count, ps.Covers.Count);
            for (int i = 0; i < po.Covers.Count; i++)
            {
                Same(po.Covers[i].Sections, ps.Covers[i].Sections);
                Same(po.Covers[i].LongitudinalCompression, ps.Covers[i].LongitudinalCompression);
                Same(po.Covers[i].AxialBlockTearing, ps.Covers[i].AxialBlockTearing);
            }
            foreach (bool collinear in new[] { false, true })
            {
                var so = WeldedIBeamSpliceStandardPlates.Verify(own, collinear); var ss = WeldedIBeamSpliceStandardPlates.Verify(shared, collinear);
                Same(so.NetSections, ss.NetSections); Same(so.BlockHierarchy, ss.BlockHierarchy);
                Same(so.DirectBlockTearing, ss.DirectBlockTearing); Same(so.CompactPlateDomain, ss.CompactPlateDomain);
            }
            Same(WeldedIBeamSpliceStandardModel.Verify(own), WeldedIBeamSpliceStandardModel.Verify(shared));
        }
    }

    [TestMethod]
    public void BuilderCasesAndSlipCasesShareOnePreparation()
    {
        var input = BridgeSpliceApplicationTests.Create(false, SpliceBoltCategory.C);
        var first = input.Cases[0].Parents.Detail;
        Assert.IsTrue(input.Cases.All(c => first.SharesPreparation(c.Parents.Detail)));
        Assert.IsTrue(input.Envelope.SlipCases.All(s => first.Plates.Bolts.SharesPreparation(s.Input.Bolts)));
        Assert.AreNotSame(input.Cases[0].Parents.Detail.Plates.Bolts.Detail, input.Cases[1].Parents.Detail.Plates.Bolts.Detail);
    }

    [TestMethod]
    public void AMismatchedDetailIsStillRejectedAndAnEqualOneAccepted()
    {
        var input = BridgeSpliceApplicationTests.Create(false, SpliceBoltCategory.A);
        var t = input.Cases[0].Parents.Detail; var a = input.Cases[1].Parents.Detail.Plates.Bolts.Detail.Actions;
        WeldedIBeamSpliceEnvelopeResult Envelope(WeldedIBeamSpliceDetailInput second)
            => WeldedIBeamSpliceEnvelope.Verify(new(new[] { new WeldedSpliceUltimateCase("A", t), new WeldedSpliceUltimateCase("B", second) }, SpliceBoltCategory.A));
        var mismatched = Envelope(Independent(t, a, "other", t.Plates.Bolts.Detail.WebCoverEnd + 5));
        Assert.IsTrue(mismatched.Diagnostics.Contains("B:ULTIMATE_PHYSICAL_DETAIL_OR_CODE_MISMATCH"));
        var equal = Envelope(Independent(t, a, "other"));
        Assert.IsFalse(equal.Diagnostics.Any(x => x.Contains("MISMATCH")), string.Join(";", equal.Diagnostics));
        var prepared = Envelope(input.Cases[1].Parents.Detail);
        Assert.IsFalse(prepared.Diagnostics.Any(x => x.Contains("MISMATCH")));
    }
}
