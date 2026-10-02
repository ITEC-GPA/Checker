using GPC.Checkers.Geotechnics.Foundations;
using GPC.Checkers.Geotechnics.Seismic;
using GPC.Checkers.Geotechnics.Slopes;
using GPC.Checkers.Geotechnics.Walls;
using GPC.Model.Geotechnics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static GeotechnicsTests.GeotechnicsFixture;
using static GeotechnicsTests.WallEdgeCaseTests;
using static GeotechnicsTests.WallsFixture;

namespace GeotechnicsTests;

/// <summary>Retaining walls, phase W3: serviceability and global stability, boundaries of every rule and rejected inputs (mm, N, MPa, rad).</summary>
[TestClass]
public class WallServiceGlobalEdgeTests
{
    static WallResult Solve(WallInput input) => RetainingWallAnalysis.Calculate(input, WallCombinations.Generate(input));
    static SettlementLayer[] Layers(double thick = 12000) => new[] { new SettlementLayer("Limo", 4000, 15), new SettlementLayer("Ghiaia", thick, 60) };

    [TestMethod]
    public void SettlementsFollowTheContactAndTheRules()
    {
        var input = Wall(); var wall = Solve(input);
        var r = WallServiceability.Calculate(input, wall, new WallServiceOptions(new WallSettlementOptions(Layers(), 20 * KPa, rigidBase: true)));
        Assert.AreEqual(3, r.Cases.Count, "one case per SLS combination");
        var c = r.Cases[0]; var qp = wall.Cases[0];
        // The settlement at the centre is the one of the contact trapezoid less the excavation over the base.
        var mid = FoundationSettlement.Calculate(Layers(), 1500, qp.Contact.Start, qp.Contact.End, qp.Contact.Toe, qp.Contact.Heel, 20 * KPa, 3000);
        Close(mid.Settlement, c.CentreSettlement!.Value, "centre", 1e-15);
        Close((c.HeelSettlement!.Value - c.ToeSettlement!.Value) / 3000, c.Rotation!.Value, "rotation", 1e-15);
        Assert.AreEqual("Calcolato", c.Status);
        var settlement = r.Checks.First(k => k.Kind == WallCheckKind.Settlement);
        Close(new[] { c.ToeSettlement.Value, c.CentreSettlement.Value, c.HeelSettlement.Value }.Max(), settlement.Demand, "maximum", 1e-15); Assert.AreEqual(25, settlement.Resistance);
        // A thin profile leaves more than 10% of the net pressure at the bottom: no ratio while the limit is satisfied, unless the base is rigid.
        var thin = WallServiceability.Calculate(input, wall, new WallServiceOptions(new WallSettlementOptions(Layers(500), 20 * KPa)));
        Assert.IsTrue(thin.Checks.Where(k => k.Kind == WallCheckKind.Settlement).All(k => k.Ratio == null && k.Message.StartsWith("Profilo insufficiente")));
        StringAssert.StartsWith(thin.Cases[0].Status, "Profilo insufficiente");
        // A limit exceeded keeps its ratio even when the profile is insufficient.
        var tight = WallServiceability.Calculate(input, wall, new WallServiceOptions(new WallSettlementOptions(Layers(500), 20 * KPa, .01)));
        Assert.IsTrue(tight.Checks.Where(k => k.Kind == WallCheckKind.Settlement).All(k => k.Status == WallCheckStatus.NotSatisfied && k.Ratio > 1));
        // No contact: message, no settlement.
        var force = new WallAction("h", "H", WallActionType.HorizontalForce, WallActionCategory.Q, 1000, 3400);
        var lost = Wall(actions: new[] { Surcharge(10), force }); var lostWall = Solve(lost);
        var none = WallServiceability.Calculate(lost, lostWall, new WallServiceOptions(new WallSettlementOptions(Layers(), 0)));
        Assert.IsTrue(none.Cases.Any(x => x.CentreSettlement == null && x.Status == "Contatto fondazione non disponibile."));
        Assert.IsTrue(none.Checks.Any(k => k.Kind == WallCheckKind.Settlement && k.Resistance == null));
        Throws(() => new WallSettlementOptions(Layers(), -1), "negative removed pressure"); Throws(() => new WallSettlementOptions(Layers(), 0, 0), "zero limit");
        Throws(() => new WallSettlementOptions(Layers(), 0, 25, 1), "rotation limit");
        // Nothing requested: no cases.
        Assert.AreEqual(0, WallServiceability.Calculate(input, wall, new WallServiceOptions()).Cases.Count);
    }

    [TestMethod]
    public void DisplacementsNeedCurvaturesAndConvergedRotation()
    {
        var input = Wall(); var wall = Solve(input);
        var stem = wall.Cases[0].Sections.Where(s => s.Member == WallMember.Stem && (s.Position > 0 || s.N != 0 || s.V != 0 || s.M != 0)).ToArray();
        IReadOnlyList<WallCurvaturePoint> Constant(WallCaseResult c) => c.Sections.Where(s => s.Member == WallMember.Stem && (s.Position > 0 || s.N != 0 || s.V != 0 || s.M != 0))
            .Select(s => new WallCurvaturePoint(3000 - s.Position, 1e-7)).ToArray();
        var options = new WallServiceOptions(new WallSettlementOptions(Layers(), 20 * KPa, rigidBase: true), (20, 50));
        var r = WallServiceability.Calculate(input, wall, options, Constant);
        var c = r.Cases[0];
        // Constant curvature up to the last cut below the top, zero at the top: the cantilever integral of those points.
        Close(WallDisplacementPoint.Integrate(Constant(wall.Cases[0]), 3000)[^1].Displacement, c.StemDisplacement!.Value, "stem", 1e-12); Assert.IsTrue(c.StemDisplacement < 1e-7 * 9e6 / 2 && c.StemDisplacement > .99 * 1e-7 * 9e6 / 2);
        Close(c.StemDisplacement.Value + wall.Cases[0].Horizontal / 50 - c.Rotation!.Value * 3000, c.HeadDisplacement!.Value, "u + H/k − θ H", 1e-12);
        Assert.IsTrue(r.Checks.Any(k => k.Kind == WallCheckKind.StemDisplacement) && r.Checks.Any(k => k.Kind == WallCheckKind.HeadDisplacement));
        // Missing curvatures, wrong count, no rotation.
        var missing = WallServiceability.Calculate(input, wall, options, _ => null);
        Assert.IsTrue(missing.Checks.Any(k => k.Kind == WallCheckKind.HeadDisplacement && k.Message.StartsWith("Selezionare il materiale")));
        var few = WallServiceability.Calculate(input, wall, options, x => Constant(x).Take(3).ToArray());
        Assert.IsTrue(few.Checks.Any(k => k.Message.StartsWith("Curvature mancanti")));
        var noRotation = WallServiceability.Calculate(input, wall, new WallServiceOptions(null, (20, 50)), Constant);
        Assert.IsTrue(noRotation.Checks.Any(k => k.Kind == WallCheckKind.StemDisplacement && k.Ratio != null));
        Assert.IsTrue(noRotation.Checks.Any(k => k.Kind == WallCheckKind.HeadDisplacement && k.Message.StartsWith("Spostamento totale: completare")));
        var badStiffness = WallServiceability.Calculate(input, wall, new WallServiceOptions(new WallSettlementOptions(Layers(), 20 * KPa, rigidBase: true), (20, 0)), Constant);
        Assert.IsTrue(badStiffness.Checks.Any(k => k.Message.StartsWith("horizontal_stiffness")));
        Assert.AreEqual(stem.Length, Constant(wall.Cases[0]).Count);
    }

    [TestMethod]
    public void NewmarkNeedsACompatibleRecordAndAFreeWall()
    {
        var samples = Enumerable.Range(0, 41).Select(i => new AccelerogramSample(i * .05, .25 * Math.Sin(i * .05 * 2 * Math.PI * 1.5))).ToArray();
        var record = new WallAccelerogram("Acc", "SLV", true, .08, 1, 50, samples);
        var r = WallServiceability.Sliding(Wall(), record);
        Close(NewmarkSliding.Calculate(samples, .08).Displacement, r.History!.Displacement, "Newmark", 1e-15);
        Assert.AreEqual(WallCheckKind.Newmark, r.Check.Kind); Assert.AreEqual(50, r.Check.Resistance);
        Assert.IsNull(WallServiceability.Sliding(Wall(), new WallAccelerogram("Acc", "SLC", true, .08, 1, 50, samples)).History, "state");
        Assert.IsNull(WallServiceability.Sliding(Wall(), new WallAccelerogram("Acc", "SLV", false, .08, 1, 50, samples)).History, "not confirmed");
        var wood = WallServiceability.Sliding(Wall(seismic: WallSeismic.Assigned(WallSeismicMethod.Wood, .1, 0)), record);
        StringAssert.StartsWith(wood.Check.Message, "Newmark richiede un muro capace di scorrere");
        Assert.IsNull(WallServiceability.Sliding(Wall(), new WallAccelerogram("Acc", "SLV", true, 0, 1, 50, samples)).History, "zero yield");
        Assert.IsNull(WallServiceability.Sliding(Wall(), new WallAccelerogram("Acc", "SLV", true, .08, 1, 50, samples.Take(1))).History, "one sample");
        // With the service calculation the checks of the records follow the ones of the combinations.
        var all = WallServiceability.Calculate(Wall(), Solve(Wall()), new WallServiceOptions(accelerograms: new[] { record, record }));
        Assert.AreEqual(2, all.Checks.Count); Assert.AreEqual(2, all.Sliding.Count);
    }

    [TestMethod]
    public void GlobalCombinationsAreA2M2R2AndSlv()
    {
        var actions = new[] { Surcharge(10), Surcharge(5, "g1", WallActionCategory.G1), Surcharge(5, "g2", WallActionCategory.G2), new WallAction("u", "U", WallActionType.Impact, WallActionCategory.A, 40, 2000) };
        var input = Wall(actions: actions);
        var rows = WallGlobalStability.Combinations(input, null, false);
        var statics = rows.Where(r => r.Name.StartsWith("Globale A2–M2–R2")).ToArray();
        Assert.IsTrue(statics.All(r => r.TanFrictionAngle == 1.25 && r.EffectiveCohesion == 1.25 && r.UndrainedShearStrength == 1.4 && r.ResistanceFactor == 1.1 && r.SoilWeight == 1 && r.BodyWeight == 1));
        Assert.IsTrue(statics.All(r => r.Loads["g1"] == 1 && r.Loads["u"] == 0));
        CollectionAssert.AreEquivalent(new[] { 0.0, 1.5 * 1.3 / 1.5 }, statics.Select(r => r.Loads["g2"]).Distinct().ToArray(), "G2 0/1.3");
        CollectionAssert.AreEquivalent(new[] { 0.0, 1.5 * 1.3 / 1.5 }, statics.Select(r => r.Loads["q"]).Distinct().ToArray(), "Q 0/1.3");
        Assert.AreEqual(4, statics.Length, "q and g2 present or not; identical rows merged");
        var accidental = rows.Single(r => r.Name.StartsWith("Globale eccezionale"));
        Assert.AreEqual(1, accidental.Loads["u"]); Close(.3, accidental.Loads["q"], "ψ2"); Assert.AreEqual(1, accidental.ResistanceFactor);
        // Seismic: βs = 0.38 of amax, ±kv, quasi permanent actions, γR 1.2.
        var site = NtcSiteAmplification.Create(.2, (NtcSoilCategory.B, 2.5), null, (NtcTopography.Flat, 0, 0, 0), null);
        var seismic = WallGlobalStability.Combinations(input, WallGlobalSeismic.FromSite(site), true).Where(r => r.Name.StartsWith("Globale SLV")).ToArray();
        Assert.AreEqual(2, seismic.Length); Close(.38 * site.AmaxG, seismic[0].Kh, "kh", 1e-15); Close(-.19 * site.AmaxG, seismic[0].Kv, "kv−", 1e-15);
        Assert.IsTrue(seismic.All(r => r.ResistanceFactor == 1.2 && r.TanFrictionAngle == 1 && r.Undrained));
        Assert.AreEqual(2, WallGlobalStability.Combinations(input, WallGlobalSeismic.Assigned(.1, .05), false).Count(r => r.Name.StartsWith("Globale SLV")));
        Assert.AreEqual(1, WallGlobalStability.Combinations(input, WallGlobalSeismic.Assigned(.1, 0), false).Count(r => r.Name.StartsWith("Globale SLV")), "kv = 0: one row");
        Throws(() => WallGlobalSeismic.Assigned(.6, 0), "kh > 0.5");
    }

    [TestMethod]
    public void GlobalProfileIsCheckedAndProposed()
    {
        var input = Wall(); var profile = WallGlobalStability.Propose(input, 5, 30, 2);
        // Proposal: 4 (H + t) = 13.6 m of flat ground on both sides, the columns of the wall, depths to min(bottom, 2 (H + t)).
        Close(-13600, profile.Valley[0].X, "far left"); Close(3000 + 13600, profile.Uphill[^1].X, "far right");
        Close(Math.Min(6600, 2 * 3400), profile.Search.DepthMax, "depth", 1e-12); Close(800 + 300, profile.SoilSplitX, "split", 1e-12);
        var rows = WallGlobalStability.Combinations(input, null, false);
        var r = WallGlobalStability.Calculate(input, profile, rows);
        Assert.AreEqual(rows.Count, r.Cases.Count); Assert.IsTrue(r.Cases.All(c => c.Critical != null));
        Assert.IsTrue(r.Section.Bodies.Single().Polygon.Count == 8);
        // Every circle passes below the whole wall.
        Assert.IsTrue(r.Cases.All(c => c.Critical!.Circle.Left < 0 && c.Critical.Circle.Right > 3000));
        var checks = WallGlobalStability.Checks(r);
        Assert.IsTrue(checks.All(k => k.Kind == WallCheckKind.GlobalStability && k.Demand == 1.1));
        Assert.IsTrue(checks.All(k => k.Ratio == null || Math.Abs(k.Ratio.Value - 1.1 / k.Resistance!.Value) < 1e-12));
        // Rejected profiles.
        Throws(() => WallGlobalStability.Calculate(input, new WallGlobalProfile(new[] { new SlopePoint(-10000, 0), new SlopePoint(-1, 0) }, profile.Uphill, profile.Layers, profile.Search, profile.ValleyLayers, profile.SoilSplitX), rows), "valley not ending at the toe");
        Throws(() => WallGlobalStability.Calculate(input, new WallGlobalProfile(profile.Valley, new[] { new SlopePoint(1100, 3400), new SlopePoint(2000, 3400) }, profile.Layers, profile.Search), rows), "uphill not beyond the base");
        Throws(() => WallGlobalStability.Calculate(input, new WallGlobalProfile(profile.Valley, new[] { new SlopePoint(1100, 3400), new SlopePoint(2000, 3000), new SlopePoint(16600, 3400) }, profile.Layers, profile.Search), rows), "uphill through the fill");
        Throws(() => WallGlobalStability.Calculate(input, new WallGlobalProfile(profile.Valley, profile.Uphill, profile.Layers, profile.Search, undrained: true), rows), "undrained without cu");
        Throws(() => WallGlobalStability.Propose(Wall(fill: Column(3.4, (Fill(), 3.45)), valley: WallValley.Free(Column(0, (Fill(), .05))))), "columns not below the base");
    }

    [TestMethod]
    public void DivisionsKeepTheEndsOfTheArc()
    {
        // A circle whose end Left + (Right − Left)·n/n rounds above Right: the last slice is kept (ANTHEA lost it).
        var input = Wall(); var profile = WallGlobalStability.Propose(input, 5, 30, 2);
        var section = WallGlobalStability.Calculate(input, profile, WallGlobalStability.Combinations(input, null, false)).Section;
        int rounded = 0;
        for (int i = 0; i < 2000; i++)
        {
            double left = -6950 + i * 1.37, right = 9950.000000000002 + i * .73;
            if (left + (right - left) * 30 / 30 > right) rounded++;
            var circle = SlopeGeometry.Through(new SlopePoint(left, SlopeGeometry.Height(section.Surface, left)), new SlopePoint(right, SlopeGeometry.Height(section.Surface, right)), -4000);
            if (circle == null) continue;
            var cuts = SlopeGeometry.Divisions(section, circle, 30);
            Assert.AreEqual(circle.Left, cuts[0]); Assert.IsTrue(Math.Abs(circle.Right - cuts[^1]) <= 1e-4, "end of the arc " + i);
        }
        Assert.IsTrue(rounded > 0, "the rounding happens");
    }
}
