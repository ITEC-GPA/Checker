using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Steel.Members.Tests;

/// <summary>ActualForcesWithMixedBlockInteraction on the web-12 scenario (web cover 280x12 S355/510,
/// 3x3 M24 per side, holes 26, p1=70, p2=90, first bolt 70, cover end 60, edges 50): the sufficient
/// hierarchy fails, so the old routes are NotSupported. Expected values are hand calculations from the
/// path geometry and the elastic bolt forces of the same case.</summary>
[TestClass]
public class BridgeSpliceMixedBlockInteractionTests
{
    const SpliceBlockVerificationMethod MixedMethod = SpliceBlockVerificationMethod.ActualForcesWithMixedBlockInteraction;
    static WeldedIBeamSpliceDetailInput Web12(bool ntc, PlanarConnectionActions a)
    {
        var d = BridgeSpliceScenarioTests.Detail("web-12");
        return WeldedIBeamSpliceStandardTests.Input(ntc, new(d.Section, d.Top, d.Bottom, d.EachWebCover, d.WebBolts, d.WebCoverEnd, d.ButtGap, a));
    }
    static WeldedIBeamSpliceStandardPlateResult Plates(bool ntc, PlanarConnectionActions a, SpliceBlockVerificationMethod method = MixedMethod)
        => WeldedIBeamSpliceStandardPlates.Verify(Web12(ntc, a), method, SpliceFlangeFaceModel.BalancedEqualFaces, SplicePlateStabilityMethod.CompactCovers);
    static WeldedIBeamSpliceStandardInput With(WeldedIBeamSpliceStandardInput p, SpliceBlockVerificationMethod method)
        => new(p.Cases, p.Envelope.Category, p.Envelope.SlipCases, method, p.FaceModel, p.PlateStability);
    static ElasticBoltForce[] LeftWebForces(WeldedIBeamSpliceDetailInput p)
        => WeldedIBeamSpliceForces.Calculate(p.Plates.Bolts.Detail).CoverGroups
            .Single(g => g.Cover == WeldedSpliceCover.WebPositive && g.Side == SpliceSide.Left).Distribution.Forces.ToArray();
    static void Near(double e, double a) => WeldedIBeamSpliceTests.Close(e, a, 1e-11);

    public static IEnumerable<object[]> Actions()
    {
        foreach (bool ntc in new[] { false, true })
        foreach (double sign in new[] { 1d, -1d })
        {
            yield return new object[] { ntc, sign, 2e5, -1e5, 50e6 }; // N+V+M
            yield return new object[] { ntc, sign, 0d, -1e5, 0d };    // pure V
            yield return new object[] { ntc, sign, 0d, -1e5, 50e6 };  // V+M
        }
    }

    [DataTestMethod, DynamicData(nameof(Actions), DynamicDataSourceType.Method)]
    public void MarginalEdgeLPathMatchesHandInteraction(bool ntc, double sign, double n, double v, double m)
    {
        var p = Web12(ntc, new(sign * n, sign * v, 0, 0, 0, sign * m));
        var r = Plates(ntc, new(sign * n, sign * v, 0, 0, 0, sign * m));
        // Rectangle from the left cover end (x=-270) and the lower cover edge: two columns (x=-210,-140),
        // two rows (lower, middle). End cut x=-140 over 50+90mm through 1.5 holes; side cut along the
        // middle row over 60+70mm through 1.5 holes.
        var check = r.DirectBlockTearing.Checks.Single(c => c.Id == "WebPositive.Axis0.Direction1.Path16.Lmin.MixedInteraction");
        double g0 = ntc ? 1.05 : 1, t = 12, lt = 140 - 1.5 * 26, ls = 130 - 1.5 * 26;
        double rt = (.5 * lt * 510 / 1.25 + ls * 355 / (Math.Sqrt(3) * g0)) * t;
        double rn = (.5 * ls * 510 / 1.25 + lt * 355 / (Math.Sqrt(3) * g0)) * t;
        // Couple of +/-204MPa on each net cut split into equal net halves (hand integrals):
        // end cut [0,37]+[63,127], split 76.5: Z=2146+91.125+1275.125; side cut [0,47]+[73,117], split 45.5.
        double rm = 204 * t * ((2146 + 91.125 + 1275.125) + (1035.125 + 1.125 + 2178));
        if (!ntc) Assert.AreEqual(471.06e3, rt, 10); // 471.1kN < hierarchy demand 4x135.6kN
        var forces = LeftWebForces(p);
        var xs = forces.Select(f => f.Position.X).Distinct().OrderBy(x => x).ToArray();
        var ys = forces.Select(f => f.Position.Y).Distinct().OrderBy(y => y).ToArray();
        Near(70, xs[1] - xs[0]); Near(90, ys[1] - ys[0]);
        var inside = forces.Where(f => f.Position.X <= xs[1] && f.Position.Y <= ys[1]).ToArray();
        Assert.AreEqual(4, inside.Length);
        double gx = (xs[0] + xs[1]) / 2, gy = (ys[0] + ys[1]) / 2;
        double pt = inside.Sum(f => Math.Abs(f.Fx)), pn = inside.Sum(f => Math.Abs(f.Fy));
        double moment = Math.Abs(inside.Sum(f => (f.Position.X - gx) * f.Fy - (f.Position.Y - gy) * f.Fx));
        Assert.IsTrue(pn > 0 && (m == 0 || moment > 0));
        Near(pt / rt + pn / rn + moment / rm, check.Demand);
        Assert.AreEqual(1d, check.Resistance); Assert.AreEqual(ConnectionCheckRole.Resistance, check.Role);
        // The same path under the unchanged hierarchy is an applicability failure, not this check.
        var old = Plates(ntc, new(sign * n, sign * v, 0, 0, 0, sign * m), SpliceBlockVerificationMethod.ActualCollinearForcesWithHierarchyFallback);
        var h = old.BlockHierarchy.Checks.Single(c => c.Id == "WebPositive.Axis0.Direction1.Path16.Lmin");
        Assert.IsFalse(h.Passed); Near(4 * .6 * 800 * 353 / 1.25, h.Demand); Near(rt, h.Resistance);
    }

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void ConfinedTransverseUPathUsesOnlyItsTearingComponent(bool ntc)
    {
        // Pure joint shear. From the upper cover edge, two rows (upper, middle) and the three left columns:
        // tension cut along the middle row between x=-210 and x=-70 (two holes), two shear cuts along the
        // outer columns over 50+90mm through 1.5 holes.
        var p = Web12(ntc, new(0, -1e5, 0, 0, 0, 0));
        var r = Plates(ntc, new(0, -1e5, 0, 0, 0, 0));
        var check = r.DirectBlockTearing.Checks.Single(c => c.Id == "WebPositive.Axis1.Direction-1.Path38.U.MixedInteraction");
        double rt = (.5 * (140 - 2 * 26) * 510 / 1.25 + 2 * (140 - 1.5 * 26) * 355 / (Math.Sqrt(3) * (ntc ? 1.05 : 1))) * 12;
        var forces = LeftWebForces(p);
        double top = forces.Max(f => f.Position.Y);
        double pt = forces.Where(f => f.Position.Y >= top - 90).Sum(f => Math.Abs(f.Fy));
        Near(rt, check.Resistance); Near(pt, check.Demand); Assert.AreEqual("N", check.Unit);
        Assert.IsTrue(6 * .6 * 800 * 353 / 1.25 > rt); // outside the hierarchy
    }

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void SignReversalGivesTheSameDemands(bool ntc)
    {
        var a = Plates(ntc, new(2e5, -1e5, 0, 0, 0, 50e6)).DirectBlockTearing.Checks;
        var b = Plates(ntc, new(-2e5, 1e5, 0, 0, 0, -50e6)).DirectBlockTearing.Checks;
        CollectionAssert.AreEqual(a.Select(c => c.Id).ToArray(), b.Select(c => c.Id).ToArray());
        for (int i = 0; i < a.Count; i++) Near(a[i].Demand, b[i].Demand);
        Assert.IsTrue(a.Count(c => c.Id.EndsWith(".MixedInteraction")) > 0);
    }

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void PureAxialGroupsKeepTheCollinearChecksExactly(bool ntc)
    {
        var a = Plates(ntc, new(2e5, 0, 0, 0, 0, 0));
        var b = Plates(ntc, new(2e5, 0, 0, 0, 0, 0), SpliceBlockVerificationMethod.ActualCollinearForcesWithHierarchyFallback);
        Assert.AreEqual(0, a.DirectBlockTearing.Checks.Count(c => c.Id.EndsWith(".MixedInteraction")));
        CollectionAssert.AreEqual(b.DirectBlockTearing.Checks.Select(c => (c.Id, c.Demand, c.Resistance)).ToArray(),
            a.DirectBlockTearing.Checks.Select(c => (c.Id, c.Demand, c.Resistance)).ToArray());
        Assert.AreEqual(b.BlockHierarchy.Checks.Count, a.BlockHierarchy.Checks.Count);
    }

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void PathsWithinTheHierarchyAreUnchangedAndOnlyFailedPathsAreLoaded(bool ntc)
    {
        var act = new PlanarConnectionActions(2e5, -1e5, 0, 0, 0, 50e6);
        var mixed = Plates(ntc, act); var old = Plates(ntc, act, SpliceBlockVerificationMethod.ActualCollinearForcesWithHierarchyFallback);
        var failed = old.BlockHierarchy.Checks.Where(c => !c.Passed).Select(c => c.Id + ".MixedInteraction").ToArray();
        Assert.IsTrue(failed.Length > 0);
        CollectionAssert.AreEquivalent(failed, mixed.DirectBlockTearing.Checks.Where(c => c.Id.EndsWith(".MixedInteraction")).Select(c => c.Id).ToArray());
        CollectionAssert.AreEqual(old.BlockHierarchy.Checks.Where(c => c.Passed).Select(c => (c.Id, c.Demand, c.Resistance)).ToArray(),
            mixed.BlockHierarchy.Checks.Select(c => (c.Id, c.Demand, c.Resistance)).ToArray());
        Assert.AreEqual(ConnectionStatus.Passed, mixed.BlockHierarchy.Status);
        Assert.AreEqual(ConnectionStatus.Passed, mixed.DirectBlockTearing.Status, string.Join(";", mixed.DirectBlockTearing.Diagnostics));
        Assert.AreEqual(failed.Length, mixed.DirectBlockTearing.Trace.Where(x => x.Name.EndsWith(".MixedInteractionPaths")).Sum(x => x.Value));
    }

    [DataTestMethod][DataRow(false, SpliceBoltCategory.B)][DataRow(false, SpliceBoltCategory.C)][DataRow(true, SpliceBoltCategory.B)][DataRow(true, SpliceBoltCategory.C)]
    public void ThinWebScenarioBecomesEvaluatedWhileOldRoutesStayUnsupported(bool ntc, SpliceBoltCategory category)
    {
        var p = BridgeSpliceScenarioTests.Workflow(ntc, category, "web-12");
        foreach (var method in new[] { SpliceBlockVerificationMethod.FastenerHierarchy, SpliceBlockVerificationMethod.ActualCollinearForcesWithHierarchyFallback })
        {
            var old = WeldedIBeamSpliceStandard.Verify(With(p, method));
            Assert.AreEqual(ConnectionStatus.NotSupported, old.Status);
            Assert.IsTrue(old.MissingRequirements.Any(x => x.Contains("BlockHierarchy")));
        }
        var r = WeldedIBeamSpliceStandard.Verify(With(p, MixedMethod));
        Assert.IsTrue(r.IsStaticResistanceComplete, string.Join(";", r.MissingRequirements));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status);
        Assert.IsTrue(r.CheckSources.Any(x => x.Component == "DirectBlockTearing" && x.ComponentCheck.Id.EndsWith(".MixedInteraction")));
        var report = BridgeSpliceReport.FromBolted(r);
        StringAssert.Contains(report.Scope, "mixed-direction block interaction");
        StringAssert.Contains(report.ToText(), "Block method: ActualForcesWithMixedBlockInteraction");
        StringAssert.Contains(report.ToText(), "WebPositive.MixedInteraction.GoverningUtilization=");
    }

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void P357WebWithM24OutsideTheHierarchyIsVerifiedByActualForces(bool ntc)
    {
        // Same SCI P357-like 925x10 web cover as BridgeSpliceStandardBoltedCoverTests: with M24 the
        // hierarchy fails (NotSupported there); the actual bolt actions are far below every path resistance.
        var p = BridgeSpliceStandardBoltedCoverTests.Workflow(BridgeSpliceStandardBoltedCoverTests.Input(ntc, smallBolt: false));
        Assert.AreEqual(ConnectionStatus.NotSupported, WeldedIBeamSpliceStandard.Verify(p).Status);
        var r = WeldedIBeamSpliceStandard.Verify(With(p, MixedMethod));
        Assert.IsTrue(r.IsStaticResistanceComplete, string.Join(";", r.MissingRequirements));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status);
        var governing = r.Cases[0].AdditionalPlates.DirectBlockTearing.Trace.Single(x => x.Name == "WebPositive.MixedInteraction.GoverningUtilization").Value;
        Assert.IsTrue(governing > 0 && governing < .2, governing.ToString());
    }

    [TestMethod]
    public void QualifiedHierarchyCaseIsIdenticalUnderTheNewMethod()
    {
        var p = WeldedIBeamSpliceStandardTests.Workflow();
        var a = WeldedIBeamSpliceStandard.Verify(With(p, SpliceBlockVerificationMethod.ActualCollinearForcesWithHierarchyFallback));
        var b = WeldedIBeamSpliceStandard.Verify(With(p, MixedMethod));
        Assert.AreEqual(a.Status, b.Status); Assert.AreEqual(ConnectionStatus.Passed, b.Status);
        CollectionAssert.AreEqual(a.CheckSources.Select(x => (x.CaseId, x.Component, x.ComponentCheck?.Id, x.Utilization)).ToArray(),
            b.CheckSources.Select(x => (x.CaseId, x.Component, x.ComponentCheck?.Id, x.Utilization)).ToArray());
    }

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void OverloadIsAKnownFailureOfTheInteraction(bool ntc)
    {
        var r = Plates(ntc, new(10e5, -10e5, 0, 0, 0, 250e6));
        Assert.AreEqual(ConnectionStatus.Failed, r.DirectBlockTearing.Status);
        Assert.IsTrue(r.DirectBlockTearing.Checks.Any(c => c.Id.EndsWith(".Lmin.MixedInteraction") && !c.Passed));
        Assert.AreEqual(0, r.DirectBlockTearing.Diagnostics.Count);
    }

    [TestMethod]
    public void ApplicationBuilderKeepsTheBlockMethod()
    {
        var t = BridgeSpliceApplicationTests.Bridge(Web12(false, new(2e5, -1e5, 0, 0, 0, 50e6)));
        var s = WeldedIBeamSpliceStandardTests.Section(t).Plan.Input.Section;
        var q = BridgeSpliceApplication.CreateBolted(t, s, s, new[] { new SpliceActionCase("U", t.Plates.Bolts.Detail.Actions, "FEM") },
            WeldedIBeamSpliceStandardTests.Welds(), "Global model", SpliceBoltCategory.A, BridgeSpliceApplicationTests.Prepare, blockMethod: MixedMethod);
        Assert.AreEqual(MixedMethod, q.BlockMethod);
        var r = WeldedIBeamSpliceStandard.Verify(q);
        Assert.IsFalse(r.MissingRequirements.Any(x => x.Contains("BlockHierarchy")), string.Join(";", r.MissingRequirements));
        Assert.IsTrue(r.CheckSources.Any(x => x.Component == "DirectBlockTearing" && x.ComponentCheck.Id.EndsWith(".MixedInteraction")));
    }
}
