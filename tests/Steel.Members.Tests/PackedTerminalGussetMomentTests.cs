using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

/// <summary>Out-of-plane gusset moment of the packed-pair terminals from one free body (EN 1993-1-1:2005 6.4.4(1)-(2):
/// closely spaced chords as one integral member, interconnections carrying the longitudinal shear). Gusset, packings,
/// fasteners and both chord ends, cut through the chords beyond the joint where each chord carries only its axial force
/// on its centroid line: F1 = N/2 + Q at z = +s/2, F2 = N/2 - Q at z = -s/2, s = gap + 2 e_n (e_n = back-to-centroid
/// distance), gusset mid-surface z = 0. Moment equilibrium about the gusset axis: M = F1 s/2 - F2 s/2 = Q s, and, split at
/// the shear planes z = +-gap/2, (F1 - F2) gap/2 + (F1 - F2) e_n = Q (gap + 2 e_n), the same value. The bolted terminal uses
/// Q s; the welded terminal keeps its envelope 2 Q (s + 2 e_n) = (2 + 4 e_n/s) Q s. Q is the end-station longitudinal shear
/// of the analytical bow model: delta sin(pi x/L) at the end of the first tributary, delta = ShearFlowPeak L/pi.</summary>
[TestClass]
public class PackedTerminalGussetMomentTests
{
    // Sharp C200x75x10/12: A = 2000 + 1560 = 3560, back-to-centroid (2000*5 + 1560*42.5)/3560 = 76300/3560 = 21.433 mm.
    private const double ChannelBackToCentroid = 76300.0 / 3560;
    // Sharp L 150x12 (vertical legs back to back) + 88x12: A = 1800 + 1056 = 2856, (1800*6 + 1056*56)/2856 = 69936/2856.
    private const double AngleBackToCentroid = 69936.0 / 2856;

    private static double FreeBodyMoment(double n, double q, double s)
    {
        double f1 = n / 2 + q, f2 = n / 2 - q;
        return f1 * s / 2 - f2 * s / 2;
    }

    // Channel, gap 10 (gusset only), N = 100 kN, panel 125: end tributary 62.5 mm.
    // Channel, gap 20 = gusset 6 + two 7 mm packings, N = 20 kN, panel 250: end tributary 125 mm.
    // Angle (vertical legs), gap 10, N = 50 kN, panel 125.
    [DataTestMethod]
    [DataRow("C", 100000d, 10d, 125d, 10d, 0d, 3)] [DataRow("C", 20000d, 20d, 250d, 6d, 7d, 7)] [DataRow("L-long", 50000d, 10d, 125d, 10d, 0d, 3)]
    public void BoltedTerminalUsesTheFreeBodyMomentQTimesTheCentroidSpacing(string kind, double n, double gap, double panel, double gusset, double packing, int bolts)
    {
        var m = PackedBoltedTerminalData.Model(kind, n: n, gap: gap, panel: panel);
        var r = SteelPackedPairBoltedTerminal.Verify(m, PackedBoltedTerminalData.Terminal(m, bolts: bolts, gusset: gusset, packing: packing));
        Assert.IsTrue(r.IsComplete, string.Join(";", r.Diagnostics));
        double q = m.ShearFlowPeak * 3000 / Math.PI * Math.Sin(Math.PI * (panel / 2) / 3000);
        Assert.AreEqual(q, PackedBoltedTerminalData.Value(r, "BoltedTerminal.EndTransferEnvelope"), q * 1e-12);
        double en = kind == "C" ? ChannelBackToCentroid : AngleBackToCentroid, s = gap + 2 * en;
        double moment = FreeBodyMoment(n, q, s);
        Assert.AreEqual(q * s, moment, q * s * 1e-12);
        Assert.AreEqual(q * gap + 2 * q * en, moment, q * s * 1e-12); // split at the shear planes
        Assert.AreEqual(moment, PackedBoltedTerminalData.Value(r, "Gusset.OutOfPlaneMomentEnvelope"), moment * 1e-9);
        Assert.AreEqual(en, PackedBoltedTerminalData.Value(r, "BoltedTerminal.NormalLever"), 1e-12);
    }

    [TestMethod]
    public void WeldedTerminalEnvelopeIsUnchangedAndAboveTheFreeBody()
    {
        // Welded fixture: C200x75x10/12, gap 10, N = 1 kN, panel 100: Q = delta sin(pi 50/3000), s = 10 + 2*21.433 = 52.865.
        var m = PackedWeldedCaseData.Model(); var r = SteelPackedPairTerminal.Verify(m, PackedWeldedCaseData.Terminal(m));
        Assert.IsTrue(r.IsComplete, string.Join(";", r.Diagnostics));
        double q = m.ShearFlowPeak * 3000 / Math.PI * Math.Sin(Math.PI * 50 / 3000), en = ChannelBackToCentroid, s = 10 + 2 * en;
        double envelope = r.Trace.Single(x => x.Name == "Terminal.CollectorMomentEnvelope").Value;
        Assert.AreEqual(2 * q * (s + 2 * en), envelope, envelope * 1e-12); // 2Q(s + 2 e_n), as before
        double statics = FreeBodyMoment(1000, q, s);
        Assert.AreEqual(2 + 4 * en / s, envelope / statics, 1e-12); // 3.62 for this channel
        Assert.IsTrue(envelope >= 2 * statics);
    }
}
