using GPC.Checkers.Concrete.Detailing;
using GPC.Checkers.Concrete.Durability;
using static GPC.Design.Walls.WallBarShapes;

namespace GPC.Design.Walls;

/// <summary>NTC retaining-wall detail policy, coordinated through shared Concrete services: main bars at the spacing 1000/n of the
/// continuous wall, stem feet crossed in the foundation and resting on the bottom mesh, foundation bars continuous when possible with
/// end bends at the free edges, U-bars at the free head of the stem, cross ties in the lap and longitudinal bars inside the main bars.
/// Geometry/path/length in m; cover, spacing, diameters, anchor and lap lengths in mm.</summary>
public static partial class WallReinforcementDetailing
{
    sealed record Arm(WallRebarZone Zone, WallRebarFace Face, double Diameter, int Count, double Spacing, double Area, AnchorageResult Development,
        double RequiredLap, double LapUsed, double Anchor, double Fbd);

    public static WallReinforcementDetails Calculate(WallDetailInput input, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested(); Validate(input);
        var g = input.Geometry; var r = input.Reinforcement; var opt = input.Options; var material = input.Material;
        double h = g.Height, t = g.Slab, a = g.Toe, s = g.StemBase, b = a + s + g.Heel, back = a + s, top = t + h;
        double k = (s - g.StemTop) / h, skew = Math.Sqrt(1 + k * k);
        bool two = r.TwoZones, hasToe = a > 0, hasHeel = g.Heel > 0; double split = two ? r.LowerHeight : h;
        var checks = new List<WallDetailCheck>(); var arms = new Dictionary<(WallRebarZone, WallRebarFace), Arm>();
        double aggregate = opt.Aggregate, clearLap = opt.LapClearDistance, cover = input.Cover, percent = opt.LapPercent;
        WallRebarZone? currentZone = null; WallRebarFace? currentFace = null;
        void Add(WallDetailRule rule, double demand, double capacity, string unit)
        {
            if (!PortableMath.IsFinite(demand) || !PortableMath.IsFinite(capacity)) throw new ArithmeticException("Wall detail exceeds the numeric range.");
            checks.Add(new(rule, currentZone, currentFace, Math.Abs(demand), capacity, unit));
        }
        void Max(WallDetailRule rule, double actual, double limit) => Add(rule, actual, limit, "mm");
        void Min(WallDetailRule rule, double actual, double required, string unit = "mm") => Add(rule, required, actual, unit);
        AnchorageResult Develop(double diameter, bool lap) => AnchorageCalculator.Calculate(DetailingProfile.Ntc2018,
            new(diameter, material.Fyd, material.Fctk05, material.GammaC, opt.GoodBond, 0, lap, percent, clearLap));
        double Centre(double diameter) => (cover + diameter / 2) / 1000;
        double ValleyFace(double y) => a + k * Math.Max(0, y - t);
        WallZoneBars? Zone(WallRebarZone zone) => zone switch
        {
            WallRebarZone.StemUpper when !two => null, WallRebarZone.Toe when !hasToe => null, WallRebarZone.Heel when !hasHeel => null, _ => r.Zones[zone]
        };

        // Foundation layers, needed by the feet of the stem bars: the bar at each free edge (toe edge, heel edge; one zone serves
        // both edges when the other cantilever is missing), its end bend and the longitudinal bar held in that bend.
        var valleyZone = hasToe ? r.Zones[WallRebarZone.Toe] : hasHeel ? r.Zones[WallRebarZone.Heel] : null;
        var mountainZone = hasHeel ? r.Zones[WallRebarZone.Heel] : hasToe ? r.Zones[WallRebarZone.Toe] : null;
        WallBarPoint EndCorner(WallZoneBars? zone, bool valley, bool bottom)
        {
            if (zone is null) return new(valley ? cover / 1000 : b - cover / 1000, bottom ? cover / 1000 : t - cover / 1000);
            double dia = (bottom ? zone.FirstFace : zone.OppositeFace).Diameter, mandrel = MinimumMandrel(dia), radius = (mandrel + dia) / 2000, c = Centre(dia);
            var centre = new WallBarPoint(valley ? c + radius : b - c - radius, bottom ? c + radius : t - c - radius);
            return Nested(centre, valley ? -1 : 1, bottom ? -1 : 1, mandrel, zone.SecondaryDiameter);
        }
        double footBottom = new[] { WallRebarZone.Toe, WallRebarZone.Heel }.Select(Zone).Where(z => z is not null).Select(z => z!.FirstFace.Diameter).DefaultIfEmpty(0).Max();
        double footMesh = new[] { WallRebarZone.Toe, WallRebarZone.Heel }.Select(Zone).Where(z => z is not null).Select(z => z!.SecondaryDiameter).DefaultIfEmpty(0).Max();
        var valleyCorner = EndCorner(valleyZone, true, true); var mountainCorner = EndCorner(mountainZone, false, true);
        double valleyLimit = valleyCorner.X + (valleyZone?.SecondaryDiameter ?? 0) / 2000, mountainLimit = mountainCorner.X - (mountainZone?.SecondaryDiameter ?? 0) / 2000;

        var bars = new List<WallBarDetail>(); int mark = 0;
        string Next() => (++mark).ToString(System.Globalization.CultureInfo.InvariantCulture);
        foreach (var key in PortableEnum.GetValues<WallRebarZone>())
        {
            var arm = Zone(key); if (arm is null) continue;
            token.ThrowIfCancellationRequested(); bool stem = key is WallRebarZone.Stem or WallRebarZone.StemUpper, upper = key == WallRebarZone.StemUpper;
            double secondaryDia = arm.SecondaryDiameter, secondaryStep = arm.SecondarySpacing;
            for (int face = 0; face < 2; face++)
            {
                var selected = face == 0 ? arm.FirstFace : arm.OppositeFace;
                double dia = selected.Diameter; int count = selected.Count;
                currentZone = key; currentFace = stem ? face == 0 ? WallRebarFace.Upstream : WallRebarFace.Downstream : face == 0 ? WallRebarFace.Bottom : WallRebarFace.Top;
                // Continuous wall: n bars per metre are n intervals of 1000/n, the spacing of As = n·πØ²/4 per metre in the checks.
                double spacing = 1000.0 / count, area = count * Math.PI * dia * dia / 4;
                Min(WallDetailRule.ClearSpacing, spacing - dia, PlateDetailingRules.MinimumClearSpacing(dia, aggregate));
                Max(WallDetailRule.MainSpacing, spacing, stem ? PlateDetailingRules.WallMainSpacing(g.StemTop * 1000) : PlateDetailingRules.SlabMainSpacing(t * 1000, true));
                Min(WallDetailRule.Cover, cover, CoverRequirements.Calculate(opt.CoverProfile, opt.CoverSpecification.WithBarDiameter(Math.Max(dia, secondaryDia))).Nominal);
                var dev = Develop(dia, false);
                var lap = Develop(dia, true);
                double lapUsed = stem && two ? (arm.LapLength == 0 ? Math.Ceiling(lap.RequiredLength / 10) * 10 : arm.LapLength) : 0;
                if (stem && two)
                {
                    var twin = r.Zones[upper ? WallRebarZone.Stem : WallRebarZone.StemUpper];
                    double twinDia = (face == 0 ? twin.FirstFace : twin.OppositeFace).Diameter;
                    double twinRequired = Develop(twinDia, true).RequiredLength;
                    double commonRequired = Math.Max(lap.RequiredLength, twinRequired);
                    if (arm.LapLength == 0) lapUsed = Math.Ceiling(commonRequired / 10) * 10;
                    double twinLength = twin.LapLength == 0 ? Math.Ceiling(commonRequired / 10) * 10 : twin.LapLength;
                    Min(WallDetailRule.CommonLap, (lapUsed + twinLength) / 2, commonRequired);
                    Min(WallDetailRule.LapPairClearSpacing, spacing - dia - twinDia - clearLap, PlateDetailingRules.MinimumClearSpacing(Math.Max(dia, twinDia), aggregate));
                    Max(WallDetailRule.LapClearDistance, clearLap, Math.Min(50, 4 * dia));
                    Min(WallDetailRule.LapPercentAtBoundary, percent, 100, "%");
                    Max(WallDetailRule.LapExtent, lapUsed, Math.Max(0, 2 * Math.Min(split, h - split) * 1000 - 2 * cover));
                    double otherCount = (face == 0 ? twin.FirstFace : twin.OppositeFace).Count;
                    Add(WallDetailRule.BarCountContinuity, Math.Abs(count - otherCount), 0, "bars");
                }
                double secArea = Math.PI * secondaryDia * secondaryDia / 4 * 1000 / secondaryStep;
                double thickness = stem ? (upper ? s - (s - g.StemTop) * split / h : s) : t;
                Min(WallDetailRule.SecondaryArea, secArea, stem ? Math.Max(PlateDetailingRules.WallHorizontalMainRatio * area, PlateDetailingRules.WallHorizontalConcreteRatio / 2 * thickness * 1e6) : PlateDetailingRules.SlabSecondaryArea(area), "mm²/m");
                Max(WallDetailRule.SecondarySpacing, secondaryStep, stem ? PlateDetailingRules.WallHorizontalMaximumSpacing : PlateDetailingRules.SlabSecondarySpacing(t * 1000, true));
                double c = Centre(dia), anchor = arm.AnchorLength == 0 ? Math.Ceiling(dev.RequiredLength / 10) * 10 : arm.AnchorLength;
                if (stem)
                {
                    double y0 = upper ? t + split - lapUsed / 2000 : t;
                    double y1 = upper || !two ? top - c : t + split + lapUsed / 2000;
                    double X(double y) => face == 0 ? back - c : ValleyFace(y) + c * skew;
                    var corners = new List<WallBarPoint> { new(X(y1), y1) }; var radii = new List<double>();
                    double mandrel = 0; var foot = WallFootDirection.None; bool reversed = false;
                    if (!upper)
                    {
                        double ab = Math.Min(cover + dia / 2, spacing / 2);
                        double requiredMandrel = Math.Max(MinimumMandrel(dia), Math.PI * dia * dia / 4 * material.Fyd / material.Fcd * (1 / ab + 1 / (2 * dia)));
                        mandrel = arm.BendDiameter == 0 ? Math.Ceiling(requiredMandrel / 10) * 10 : arm.BendDiameter;
                        Min(WallDetailRule.Mandrel, mandrel, requiredMandrel);
                        // The foot rests on the bottom mesh of the foundation: transverse bars, then longitudinal bars, then the foot.
                        double radius = (mandrel + dia) / 2000, yFoot = (cover + footBottom + footMesh + dia / 2) / 1000, vertical = t - yFoot - radius;
                        Min(WallDetailRule.BendHeight, (t - yFoot - c) * 1000, radius * 1000);
                        double tail = Math.Max(5 * dia / 1000, anchor / 1000 - Math.Max(0, vertical) - radius * Math.PI / 2);
                        // Opening corner stem/heel (EN 1992-1-1 Annex J.2.3): the tensioned upstream bars cross under the stem towards the toe,
                        // the downstream bars towards the heel. The other side only when the preferred one cannot contain the tail.
                        double x = X(t), toValley = x - valleyLimit, toMountain = mountainLimit - x, extent = radius + tail;
                        bool valley = face == 0;
                        if (extent > (valley ? toValley : toMountain) && extent <= (valley ? toMountain : toValley)) { valley = !valley; reversed = true; }
                        foot = valley ? WallFootDirection.Valley : WallFootDirection.Mountain;
                        double direction = valley ? -1 : 1;
                        if (face == 1 && k > 0) { corners.Add(new(X(t), t)); radii.Add(0); }
                        corners.Add(new(x, yFoot)); radii.Add(radius);
                        corners.Add(new(x + direction * extent, yFoot));
                        Max(WallDetailRule.TailExtent, extent * 1000, Math.Max(0, valley ? toValley : toMountain) * 1000);
                        anchor = Math.Max(0, vertical) * 1000 + radius * Math.PI / 2 * 1000 + tail * 1000;
                        Min(WallDetailRule.FootAnchorage, anchor, dev.RequiredLength);
                    }
                    else { corners.Add(new(X(y0), y0)); anchor = 0; }
                    var shape = Build(corners, radii);
                    bars.Add(new WallBarDetail(Next(), key, currentFace.Value, dia, count, spacing, area, shape.Length, upper ? 0 : dev.RequiredLength, anchor,
                        two ? lap.RequiredLength : 0, lapUsed, mandrel, dev.Fbd, shape.Path) { Legs = shape.Legs, Foot = foot, FootReversed = reversed });
                }
                else
                {
                    double root = key == WallRebarZone.Toe ? a : back;
                    Min(WallDetailRule.RootAnchorage, anchor, dev.RequiredLength);
                    Max(WallDetailRule.AnchorageExtent, anchor, Math.Max(0, key == WallRebarZone.Toe ? b - c - root : root - c) * 1000);
                    arms[(key, currentFace.Value)] = new(key, currentFace.Value, dia, count, spacing, area, dev, 0, 0, anchor, dev.Fbd);
                }
            }
        }
        currentZone = null; currentFace = null;

        // Foundation: one bar across the whole width when toe and heel have the same bars and it fits in a stock bar, otherwise each
        // cantilever bar runs past the root by its anchorage. Free edges are closed by end bends to the opposite face (EN 1992-1-1 9.3.1.4).
        foreach (var face in new[] { WallRebarFace.Bottom, WallRebarFace.Top })
        {
            bool bottom = face == WallRebarFace.Bottom;
            WallBarDetail Bar(Arm arm, bool fromValley, double? toX, bool continuous)
            {
                double dia = arm.Diameter, mandrel = MinimumMandrel(dia), radius = (mandrel + dia) / 2000, c = Centre(dia);
                double y = bottom ? c : t - c, tip = bottom ? t - c : c;
                var corners = new List<WallBarPoint>(); var radii = new List<double>();
                if (fromValley) { corners.Add(new(c, tip)); corners.Add(new(c, y)); radii.Add(radius); }
                else { corners.Add(new(b - c, tip)); corners.Add(new(b - c, y)); radii.Add(radius); }
                if (toX is double end) corners.Add(new(end, y));
                else { corners.Add(new(b - c, y)); radii.Add(radius); corners.Add(new(b - c, tip)); }
                var shape = Build(corners, radii);
                return new WallBarDetail(Next(), arm.Zone, face, dia, arm.Count, arm.Spacing, arm.Area, shape.Length, arm.Development.RequiredLength, arm.Anchor,
                    0, 0, mandrel, arm.Fbd, shape.Path) { Legs = shape.Legs, Continuous = continuous };
            }
            arms.TryGetValue((WallRebarZone.Toe, face), out var toe); arms.TryGetValue((WallRebarZone.Heel, face), out var heel);
            if (toe is null && heel is null) continue;
            if (toe is null || heel is null) { bars.Add(Bar((toe ?? heel)!, true, null, false)); continue; }
            if (toe.Diameter == heel.Diameter && toe.Count == heel.Count)
            {
                var whole = Bar(toe, true, null, true);
                if (whole.Length <= opt.StockLength) { bars.Add(whole); continue; }
                mark--;
            }
            bars.Add(Bar(toe, true, a + toe.Anchor / 1000, false));
            bars.Add(Bar(heel, false, back - heel.Anchor / 1000, false));
        }

        // Head of the stem (free edge, EN 1992-1-1 9.3.1.4 and fig. 9.8): U-bars with the diameter of the longitudinal bars of the top zone,
        // one per vertical of the denser face, legs 2·(thickness at the head) from the top, inside the stem.
        var headZone = two ? WallRebarZone.StemUpper : WallRebarZone.Stem; var headArm = r.Zones[headZone];
        double headDia = headArm.SecondaryDiameter, headMandrel = MinimumMandrel(headDia), headRadius = (headMandrel + headDia) / 2000, hc = Centre(headDia);
        int headCount = Math.Max(headArm.FirstFace.Count, headArm.OppositeFace.Count);
        double headTip = Math.Max(top - 2 * g.StemTop, t), headY = top - hc;
        double HeadValley(double y) => ValleyFace(y) + hc * skew;
        var head = Build(new WallBarPoint[] { new(back - hc, headTip), new(back - hc, headY), new(HeadValley(headY), headY), new(HeadValley(headTip), headTip) },
            new[] { headRadius, headRadius });
        var headDetail = new WallHeadDetail(Next(), headZone, headDia, headCount, 1000.0 / headCount, top - headTip, headMandrel, head.Length, head.Path, head.Legs);

        // Cross ties in the lap (two zones): straight between the vertical bars, hooks bent 135° round them and extended by 10Ø
        // (NTC 2018 §7.4.6.2.2; EN 1992-1-1 8.5 fig. 8.5), mandrel 4Ø/7Ø; rows over the lap at no more than the tie spacing.
        WallTieDetail? tieDetail = null; double l0 = bars.Max(x => x.Lap);
        if (two)
        {
            double phi = bars.Where(x => x.Zone is WallRebarZone.Stem or WallRebarZone.StemUpper).Max(x => x.Diameter);
            double ties = opt.TieDiameter, step = opt.TieSpacing;
            Max(WallDetailRule.JunctionTieSpacing, step, Math.Min(150, 12 * phi));
            Min(WallDetailRule.JunctionTieArea, Math.Floor(l0 / 3 / step) * Math.PI * ties * ties / 4, .5 * Math.PI * phi * phi / 4, "mm²");
            double yj = t + split, mandrel = MinimumMandrel(ties), radius = (mandrel + ties) / 2000, hook = 10 * ties, d = radius * Math.Tan(Math.PI * 3 / 8);
            var lower = r.Zones[WallRebarZone.Stem];
            double left = ValleyFace(yj) + Centre(lower.OppositeFace.Diameter) * skew, right = back - Centre(lower.FirstFace.Diameter);
            double reach = d + hook / 1000, diagonal = Math.Sqrt(.5);
            var p1 = new WallBarPoint(left - d, yj); var p2 = new WallBarPoint(right + d, yj);
            var tie = Build(new[] { new WallBarPoint(p1.X + diagonal * reach, yj - diagonal * reach), p1, p2, new WallBarPoint(p2.X - diagonal * reach, yj - diagonal * reach) },
                new[] { radius, radius });
            int rows = checked((int)Math.Ceiling(l0 / step) + 1);
            var levels = Enumerable.Range(0, rows).Select(i => yj - l0 / 2000 + l0 / 1000 * i / (rows - 1)).ToArray();
            tieDetail = new WallTieDetail(Next(), ties, step, rows, mandrel, hook, tie.Length, tie.Path, tie.Legs, Array.AsReadOnly(levels));
        }

        // Longitudinal bars: inner layer, inside the stem verticals and the foundation transverse bars, one in each corner of the head
        // U-bars and of the end bends, the others at no more than the spacing of the zone. The lap is that of Concrete, as for the verticals.
        var longitudinals = new List<WallLongitudinalDetail>();
        IEnumerable<WallBarPoint> Line(Func<double, WallBarPoint> at, double from, double to, double step, WallBarPoint? first, WallBarPoint? last, bool includeEnd)
        {
            int intervals = Math.Max(1, checked((int)Math.Ceiling((to - from) * 1000 / step - 1e-10)));
            for (int i = 0; i < intervals; i++) yield return i == 0 && first is not null ? first : at(from + (to - from) * i / intervals);
            if (includeEnd) yield return last ?? at(to);
        }
        void Longitudinal(WallRebarZone zone, WallRebarFace face, WallZoneBars arm, IEnumerable<WallBarPoint> points)
        {
            var lap = Develop(arm.SecondaryDiameter, true);
            longitudinals.Add(new(Next(), zone, face, arm.SecondaryDiameter, arm.SecondarySpacing, lap.RequiredLength, Math.Ceiling(lap.RequiredLength / 10) * 10, points.ToArray()));
        }
        var headUp = Nested(new(back - hc - headRadius, headY - headRadius), 1, 1, headMandrel, headArm.SecondaryDiameter);
        var headValley = Nested(new(HeadValley(headY) + headRadius, headY - headRadius), -1, 1, headMandrel, headArm.SecondaryDiameter);
        foreach (var key in new[] { WallRebarZone.Stem, WallRebarZone.StemUpper })
        {
            var arm = Zone(key); if (arm is null) continue;
            bool last = key == headZone; double ld = arm.SecondaryDiameter;
            double from = key == WallRebarZone.StemUpper ? t + split : t + (cover + ld / 2) / 1000, to = last ? headUp.Y : t + split;
            double up = back - (cover + arm.FirstFace.Diameter + ld / 2) / 1000, down = (cover + arm.OppositeFace.Diameter + ld / 2) / 1000 * skew;
            Longitudinal(key, WallRebarFace.Upstream, arm, Line(y => new(up, y), from, to, arm.SecondarySpacing, null, last ? headUp : null, last));
            Longitudinal(key, WallRebarFace.Downstream, arm, Line(y => new(ValleyFace(y) + down, y), from, last ? headValley.Y : to, arm.SecondarySpacing, null, last ? headValley : null, last));
        }
        var foundation = new[] { WallRebarZone.Toe, WallRebarZone.Heel }.Where(z => Zone(z) is not null).ToArray();
        if (foundation.Length > 0)
        {
            double middle = a + s / 2;
            foreach (var face in new[] { WallRebarFace.Bottom, WallRebarFace.Top })
            {
                bool bottom = face == WallRebarFace.Bottom;
                double Level(WallZoneBars arm) { double y = (cover + (bottom ? arm.FirstFace : arm.OppositeFace).Diameter + arm.SecondaryDiameter / 2) / 1000; return bottom ? y : t - y; }
                WallBarPoint At(double x) { var arm = Zone(x < middle && hasToe || !hasHeel ? WallRebarZone.Toe : WallRebarZone.Heel)!; return new(x, Level(arm)); }
                var start = EndCorner(valleyZone, true, bottom); var end = EndCorner(mountainZone, false, bottom);
                var first = Zone(foundation[0])!; var second = Zone(foundation[foundation.Length - 1])!;
                if (foundation.Length == 1 || first.SecondaryDiameter == second.SecondaryDiameter && first.SecondarySpacing == second.SecondarySpacing)
                    Longitudinal(foundation[0], face, first, Line(At, start.X, end.X, Math.Min(first.SecondarySpacing, second.SecondarySpacing), start, end, true));
                else
                {
                    Longitudinal(WallRebarZone.Toe, face, first, Line(At, start.X, middle, first.SecondarySpacing, start, null, false));
                    Longitudinal(WallRebarZone.Heel, face, second, Line(At, middle, end.X, second.SecondarySpacing, null, end, true));
                }
            }
        }

        // Steel per metre of wall: bars of the section at their number per metre, ties two per metre and row, longitudinal bars one metre each.
        double kg = bars.Sum(x => x.Area * x.Length) + headCount * Math.PI * headDia * headDia / 4 * head.Length
            + (tieDetail is { } tied ? 2 * tied.Rows * Math.PI * tied.Diameter * tied.Diameter / 4 * tied.Length : 0)
            + longitudinals.Sum(x => x.Points.Count * Math.PI * x.Diameter * x.Diameter / 4);
        kg *= 1e-6 * WallRebarQuantities.SteelKgPerCubicMetre;
        token.ThrowIfCancellationRequested();
        if (!PortableMath.IsFinite(kg)) throw new ArithmeticException("Steel quantity exceeds the numeric range.");
        return new(bars.AsReadOnly(), checks.AsReadOnly(), kg) { Head = headDetail, Tie = tieDetail, Longitudinals = longitudinals.AsReadOnly() };
    }
}
