using GPC.Design.Walls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Design.Checks.WallDetailTests;

namespace Design.Checks;

/// <summary>Reinforcement of the cantilever wall as on a reinforcement drawing (ANTHEA 1.16.0). Expected values computed by hand in
/// the comments from the geometry, EN 1992-1-1/NTC 2018 rules and the bond of the fixture material (fbd = 2,25·0,7·1,7955/1,5 = 1,8852 MPa,
/// fyd = 391,30 MPa, fcd = 14,167 MPa, cover 40 mm), never copied from the code.</summary>
[TestClass]
public class WallTavolaTests
{
    // H 4,0 m, slab 0,6 m, toe 1,0 m, stem 0,4 m (vertical unless stated), heel 2,0 m: B = 3,4 m.
    // Stem 6 Ø16/m on both faces, Ø10/200 longitudinal; toe and heel 5 Ø14/m on both faces, Ø10/200.
    internal static WallDetailInput Wall(double toe = 1, double stemTop = .4, double heel = 2, bool two = false, WallZoneBars? heelBars = null, double stock = 12)
    {
        var p = Input(Fixture()[0]!);
        var stem = new WallZoneBars(new(16, 6), new(16, 6), 10, 200);
        var slab = new WallZoneBars(new(14, 5), new(14, 5), 10, 200);
        var zones = new Dictionary<WallRebarZone, WallZoneBars> { [WallRebarZone.Stem] = stem, [WallRebarZone.StemUpper] = stem, [WallRebarZone.Toe] = slab, [WallRebarZone.Heel] = heelBars ?? slab };
        return p with { Geometry = new(4, .6, toe, .4, stemTop, heel), Reinforcement = new(two, two ? 2 : 0, zones), Cover = 40,
            Options = p.Options with { TieDiameter = 8, TieSpacing = 200, StockLength = stock, LapPercent = 100, GoodBond = false } };
    }
    static WallScheduleOptions Panel(double length, double stock = 12) => new(length, 40, stock, 2, 0, true);
    static double Kg(double d) => Math.PI * d * d / 4 * 7.85e-3;

    [TestMethod]
    public void MainBarsAreAtOneThousandOverNAndTenMetresCountTheBarsOfTheContinuousWall()
    {
        var d = WallReinforcementDetailing.Calculate(Wall());
        var up = d.Bars.Single(b => b.Zone == WallRebarZone.Stem && b.Face == WallRebarFace.Upstream);
        Same(1000.0 / 6, up.Spacing);                                                    // 166,67 mm, not (1000-80-16)/5 = 180,8
        var clear = d.Checks.First(c => c.Rule == WallDetailRule.ClearSpacing && c.Zone == WallRebarZone.Stem && c.Face == WallRebarFace.Upstream);
        Same(1000.0 / 6 - 16, clear.Capacity);                                           // 150,67 mm
        var main = d.Checks.First(c => c.Rule == WallDetailRule.MainSpacing && c.Zone == WallRebarZone.Stem);
        Same(1000.0 / 6, main.Demand);
        var s = WallBarScheduling.Calculate(Wall(), Panel(10));
        // 10 m: span 10000-2·40-16 = 9904 mm, 9904/166,67 = 59,42 -> 60 intervals, 61 bars (56 with 180,8 mm); 6/m·10 m = 60 on average.
        Assert.AreEqual(61, s.Bars.Single(b => b.Mark == up.Mark).Quantity);
        // Foundation Ø14 5/m: 9906/200 = 49,53 -> 50 intervals, 51 bars.
        Assert.AreEqual(51, s.Bars.Single(b => b.Kind == WallBarKind.Main && b.Face == WallRebarFace.Bottom).Quantity);
        Same(1000.0 / 6, s.Bars.Single(b => b.Mark == up.Mark).Spacing);
    }

    [TestMethod]
    public void StemFeetCrossTowardsTheOtherCantileverAndRestOnTheBottomMesh()
    {
        var d = WallReinforcementDetailing.Calculate(Wall());
        var up = d.Bars.Single(b => b.Zone == WallRebarZone.Stem && b.Face == WallRebarFace.Upstream);
        var down = d.Bars.Single(b => b.Zone == WallRebarZone.Stem && b.Face == WallRebarFace.Downstream);
        Assert.AreEqual(WallFootDirection.Valley, up.Foot); Assert.IsFalse(up.FootReversed);
        Assert.AreEqual(WallFootDirection.Mountain, down.Foot);
        // Mandrel: ab = min(40+8; 166,7/2) = 48; πØ²/4·fyd/fcd·(1/48+1/32) = 201,06·27,621·0,052083 = 289,2 -> 290 mm; r = (290+16)/2 = 153 mm.
        Same(290, up.Mandrel);
        // Foot on the bottom mesh: 40 + Ø14 transverse + Ø10 longitudinal + 16/2 = 72 mm.
        Same(.072, up.Path[up.Path.Count - 1].Y); Same(.072, down.Path[down.Path.Count - 1].Y);
        // Anchorage 830,2 -> 840 mm: vertical 600-72-153 = 375, arc π/2·153 = 240,3, tail 840-375-240,3 = 224,7 mm (> 5Ø = 80).
        // Upstream bar at x = 1,4-0,048 = 1,352 m: tail end 1,352-0,153-0,2247 = 0,9743 m (towards the toe); downstream at 1,048 m: 1,4257 m.
        Near(.97433, up.Path[up.Path.Count - 1].X, 1e-5); Near(1.42567, down.Path[down.Path.Count - 1].X, 1e-5);
        Near(840, up.Anchor, 1e-6);
        // Length: 4,552-0,225 = 4,327 + 0,24033 + 0,22467 = 4,79200 m.
        Near(4.79200, up.Length, 1e-5);
        var tail = d.Checks.First(c => c.Rule == WallDetailRule.TailExtent && c.Face == WallRebarFace.Upstream);
        // Available towards the toe: 1,352 - (corner bar of the toe bend 0,06574 + 0,005) = 1,28126 m.
        Near(377.67, tail.Demand, 1e-2); Near(1281.26, tail.Capacity, 1e-2);
    }

    [TestMethod]
    public void FootTurnsBackWithAWarningWhenTheOtherSideCannotContainIt()
    {
        // No toe: the upstream bar at x = 0,352 m has 0,352-0,0707 = 0,281 m towards the free edge, less than the 0,378 m of the foot.
        var input = Wall(toe: 0);
        var d = WallReinforcementDetailing.Calculate(input);
        var up = d.Bars.Single(b => b.Zone == WallRebarZone.Stem && b.Face == WallRebarFace.Upstream);
        Assert.AreEqual(WallFootDirection.Mountain, up.Foot); Assert.IsTrue(up.FootReversed);
        var s = WallBarScheduling.Calculate(input, Panel(1));
        Assert.IsTrue(s.Warnings.Any(w => w.Code == WallScheduleWarningCode.FootReversed && w.Mark == up.Mark));
    }

    [TestMethod]
    public void FoundationBarsAreContinuousWithEndBendsOrSeparatePastTheRoot()
    {
        var d = WallReinforcementDetailing.Calculate(Wall());
        var bottom = d.Bars.Single(b => b.Face == WallRebarFace.Bottom);
        Assert.IsTrue(bottom.Continuous); Assert.AreEqual(WallRebarZone.Toe, bottom.Zone);
        // Ø14, c = 47 mm, mandrel 4Ø = 56, r = 35 mm. Corners (0,047; 0,553) (0,047; 0,047) (3,353; 0,047) (3,353; 0,553):
        // legs 0,506-0,035 = 0,471; 3,306-0,070 = 3,236; 0,471; arcs 2·π/2·0,035 = 0,10996 -> 4,28796 m.
        Near(4.287956, bottom.Length, 1e-6); Same(56, bottom.Mandrel);
        Assert.AreEqual(.553, bottom.Path[0].Y, 1e-12); Assert.AreEqual(.553, bottom.Path[bottom.Path.Count - 1].Y, 1e-12);
        var top = d.Bars.Single(b => b.Face == WallRebarFace.Top);
        Assert.IsTrue(top.Continuous); Same(.047, top.Path[0].Y);                         // top bars bent down to the bottom cover line
        var s = WallBarScheduling.Calculate(Wall(), Panel(1));
        var row = s.Bars.Single(b => b.Mark == bottom.Mark);
        CollectionAssert.AreEqual(new[] { .47, .05, 3.24, .05, .47 }, row.Legs.Select(l => l.Length).ToArray()); // cm on the shape
        Same(4.28, row.CutLength!.Value);
        // Stock 4 m < 4,288 m: two bars past the roots by the anchorage of Ø14 (lbd = 14·391,3/(4·1,8852) = 726,5 -> 730 mm).
        var split = WallReinforcementDetailing.Calculate(Wall(stock: 4));
        var toe = split.Bars.Single(b => b.Zone == WallRebarZone.Toe && b.Face == WallRebarFace.Bottom);
        var heel = split.Bars.Single(b => b.Zone == WallRebarZone.Heel && b.Face == WallRebarFace.Bottom);
        Assert.IsFalse(toe.Continuous); Same(1 + .73, toe.Path[toe.Path.Count - 1].X); Same(1.4 - .73, heel.Path[heel.Path.Count - 1].X);
        Same(.553, toe.Path[0].Y); Same(.553, heel.Path[0].Y);                           // end bends at the free edges only
        // Different bars on the heel: separate even with a long stock bar.
        var other = WallReinforcementDetailing.Calculate(Wall(heelBars: new(new(16, 6), new(14, 5), 10, 200)));
        Assert.AreEqual(2, other.Bars.Count(b => b.Face == WallRebarFace.Bottom)); Assert.AreEqual(1, other.Bars.Count(b => b.Face == WallRebarFace.Top));
    }

    [TestMethod]
    public void HeadIsClosedByUBarsWithLegsOfTwiceTheThickness()
    {
        var d = WallReinforcementDetailing.Calculate(Wall());
        var head = d.Head!;
        // Ø10 (longitudinal of the stem), 6/m as the verticals, mandrel 4Ø = 40, r = 25 mm, c = 45 mm; legs 2·0,4 = 0,8 m from the top.
        Same(10, head.Diameter); Assert.AreEqual(6, head.Count); Same(.8, head.LegLength);
        // 0,755-0,025 = 0,730 twice, top 0,310-0,050 = 0,260, arcs 2·π/2·0,025 = 0,07854: 1,79854 m.
        Near(1.79854, head.Length, 1e-5);
        var s = WallBarScheduling.Calculate(Wall(), Panel(10));
        // 10 m: (10000-80-10)/166,67 = 59,46 -> 61 U-bars; legs on the shape 73 / 4 / 26 / 4 / 73 cm = 1,80 m.
        var row = s.Bars.Single(b => b.Kind == WallBarKind.Head);
        Assert.AreEqual(61, row.Quantity); Same(1.80, row.CutLength!.Value);
    }

    [TestMethod]
    public void LapTiesHaveACalculatedShapeAndTheAssignedLengthOverrides()
    {
        var input = Wall(two: true);
        var d = WallReinforcementDetailing.Calculate(input);
        var tie = d.Tie!;
        // Lap Ø16: 1,5·830,2 = 1245,4 -> 1250 mm; rows ceil(1250/200)+1 = 8.
        Assert.AreEqual(8, tie.Rows);
        // Between the verticals 1,352-1,048 = 0,304 m; hooks 135° at r = (32+8)/2 = 20 mm: 3π/4·0,020 = 0,04712, plus 10Ø = 0,080:
        // 0,304 + 2·0,12712 = 0,55825 m.
        Near(.558248, tie.Length, 1e-6);
        var s = WallBarScheduling.Calculate(input, Panel(10));
        var row = s.Bars.Single(b => b.Kind == WallBarKind.Tie);
        Assert.AreEqual(8 * 20, row.Quantity); Same(.56, row.CutLength!.Value); Assert.AreEqual(WallBarNote.Tie, row.Note);
        var forced = WallBarScheduling.Calculate(input, Panel(10) with { TieCutLength = 900 }).Bars.Single(b => b.Kind == WallBarKind.Tie);
        Same(.9, forced.CutLength!.Value); Assert.AreEqual(WallBarNote.TieAssignedLength, forced.Note);
        Assert.IsTrue(s.CompleteQuantities);
    }

    [TestMethod]
    public void LongitudinalBarsSitInsideTheMainBarsWithOneInEachCorner()
    {
        var d = WallReinforcementDetailing.Calculate(Wall());
        var up = d.Longitudinals.Single(l => l.Zone == WallRebarZone.Stem && l.Face == WallRebarFace.Upstream);
        // Inside the Ø16 verticals: x = 1,4-(40+16+5)/1000 = 1,339 m; from 0,645 m to the corner of the U-bar.
        Assert.IsTrue(up.Points.Take(up.Points.Count - 1).All(p => Math.Abs(p.X - 1.339) < 1e-12)); Same(.645, up.Points[0].Y);
        // Corner of the U-bar: bend centre (1,330; 4,530), bar Ø10 against the mandrel 40: + (20-5)/√2 mm on both axes = (1,34061; 4,54061).
        Near(1.340607, up.Points[up.Points.Count - 1].X, 1e-6); Near(4.540607, up.Points[up.Points.Count - 1].Y, 1e-6);
        // (4,54061-0,645)/0,2 = 19,48 -> 20 intervals, 21 bars.
        Assert.AreEqual(21, up.Points.Count);
        var bottom = d.Longitudinals.Single(l => l.Face == WallRebarFace.Bottom);
        // On the Ø14 transverse bars: y = (40+14+5)/1000 = 0,059 m; corners in the end bends: centre (0,082; 0,082) - (28-5)/√2 mm = 0,06574 m.
        Near(.065737, bottom.Points[0].X, 1e-6); Near(.065737, bottom.Points[0].Y, 1e-6); Same(.059, bottom.Points[1].Y);
        // (3,33426-0,06574)/0,2 = 16,34 -> 17 intervals, 18 bars.
        Assert.AreEqual(18, bottom.Points.Count);
        var top = d.Longitudinals.Single(l => l.Face == WallRebarFace.Top);
        Same(.6 - .059, top.Points[1].Y); Assert.AreEqual(18, top.Points.Count);
    }

    [TestMethod]
    public void LongitudinalBarsBeyondTheStockLengthAreEqualPiecesLappedByTheConcreteRule()
    {
        // 15 m: 15-2·0,04 = 14,92 m > 12 m. l0 Ø10 = 1,5·(10·391,3/(4·1,8852)) = 778,4 -> 780 mm.
        // Pieces ceil((14,92-0,78)/(12-0,78)) = 2, each (14,92+0,78)/2 = 7,85 m.
        var s = WallBarScheduling.Calculate(Wall(), Panel(15));
        var up = s.Bars.Single(b => b.Kind == WallBarKind.Secondary && b.Zone == WallRebarZone.Stem && b.Face == WallRebarFace.Upstream);
        Assert.AreEqual(2, up.Pieces); Assert.AreEqual(21, up.Lines); Assert.AreEqual(42, up.Quantity);
        Same(7.85, up.CutLength!.Value); Same(780, up.Lap);
        // 30 m: 29,92 m -> ceil(29,14/11,22) = 3 pieces of (29,92+2·0,78)/3 = 10,4933 -> 10,50 m.
        var long3 = WallBarScheduling.Calculate(Wall(), Panel(30)).Bars.Single(b => b.Mark == up.Mark);
        Assert.AreEqual(3, long3.Pieces); Same(10.50, long3.CutLength!.Value);
        // 10 m: one piece of 9,92 m, no lap.
        var one = WallBarScheduling.Calculate(Wall(), Panel(10)).Bars.Single(b => b.Mark == up.Mark);
        Assert.AreEqual(1, one.Pieces); Same(9.92, one.CutLength!.Value); Same(0, one.Lap);
    }

    [TestMethod]
    public void InclinedStemBarHasTheKnownBendOfTheBatter()
    {
        // Batter (0,4-0,3)/4 = 0,025: the downstream bar follows it and turns vertical at the top of the slab by atan(0,025) = 1,4321°.
        var d = WallReinforcementDetailing.Calculate(Wall(stemTop: .3));
        var down = d.Bars.Single(b => b.Zone == WallRebarZone.Stem && b.Face == WallRebarFace.Downstream);
        Assert.AreEqual(WallBarLegKind.Straight, down.Legs[1].Kind);
        Assert.AreEqual(Math.Atan(.025) * 180 / Math.PI, down.Legs[1].Angle, 1e-9);
        Same(.6, down.Legs[0].To!.Y);
    }

    [TestMethod]
    public void TavolaTotalsByDiameterWeightAndIncidence()
    {
        var s = WallBarScheduling.Calculate(Wall(), Panel(10));
        // Marks 1, 2, … in drawing order: stem bars, foundation, U-bars, longitudinal bars.
        CollectionAssert.AreEqual(Enumerable.Range(1, s.Bars.Count).Select(i => i.ToString()).ToArray(), s.Bars.Select(b => b.Mark).ToArray());
        foreach (var t in s.Diameters)
        {
            var group = s.Bars.Where(b => b.Diameter == t.Diameter).ToArray();
            Same(group.Sum(b => b.Quantity * b.CutLength!.Value), t.Length);
            Near(Kg(t.Diameter), t.KgPerMetre, 1e-9); Near(t.Length * Kg(t.Diameter), t.Weight, 1e-9);
        }
        Same(s.Diameters.Sum(x => x.Weight), s.KnownWeight);
        // Concrete: 3,4·0,6 + 0,4·4,0 = 3,64 m²/m, 36,4 m³ on 10 m.
        Same(36.4, s.ConcreteVolume); Same(s.KnownWeight / 36.4, s.Incidence);
        Assert.IsFalse(s.Warnings.Any());
    }

    static void Near(double expected, double actual, double tolerance) => Assert.AreEqual(expected, actual, tolerance);
}
