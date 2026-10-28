using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

/// <summary>The welded-I splice bearing follows the policy already declared by IBeamSplicePlates:
/// SCI P398 p55 extends the EN1993-1-8 3.8 long-joint factor to bearing.</summary>
[TestClass]
public class WeldedSpliceBearingLongJointTests
{
    // Independent arithmetic: betaLf = 1 - (Lj - 15d)/(200d), bounded to [0.75, 1].
    private static double Beta(double lj, double d) => Math.Max(.75, Math.Min(1, 1 - (lj - 15 * d) / (200 * d)));

    [TestMethod]
    public void CoverBearingUsesTheLongJointFactorOfItsOwnGroup()
    {
        // Twelve web bolts in one column: the 825 mm group exceeds 15d, so betaLf < 1 there.
        var input = WeldedIBeamSpliceStandardTests.Input(false, BridgeSpliceCompressionInteractionTests.Detail()).Plates.Bolts;
        var r = WeldedIBeamSpliceBolts.Verify(input).ComponentAssessment;
        Assert.IsTrue(r.IsComplete, string.Join(";", r.Diagnostics));
        const string suffix = ".ConservativeJointLength";
        var lengths = r.Trace.Where(t => t.Name.EndsWith(suffix)).ToArray();
        Assert.IsTrue(lengths.Length > 0);
        int bearings = 0; var seen = new List<string>();
        foreach (var length in lengths)
        {
            string group = length.Name.Substring(0, length.Name.Length - suffix.Length);
            double d = group.Contains("Web") ? input.WebBolt.Diameter : input.FlangeBolt.Diameter;
            foreach (var beta in r.Trace.Where(t => t.Name.StartsWith(group + ".Bolt") && t.Name.EndsWith(".Cover.BearingBetaLongJoint")))
            {
                Assert.AreEqual(Beta(length.Value, d), beta.Value, 1e-15, beta.Name);
                bearings++; seen.Add(group + ":" + length.Value + ":" + beta.Value);
            }
        }
        Assert.IsTrue(bearings > 0);
        Assert.IsTrue(seen.Any(s => s.Contains("Web") && !s.EndsWith(":1")), string.Join(" | ", seen.Distinct()));
        Assert.IsTrue(seen.Any(s => s.EndsWith(":1")), "short flange groups keep betaLf=1");
    }
}
