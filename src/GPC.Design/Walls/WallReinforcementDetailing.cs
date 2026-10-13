using GPC.Checkers.Concrete.Detailing;
using GPC.Checkers.Concrete.Durability;

namespace GPC.Design.Walls;

/// <summary>Existing NTC retaining-wall detail policy, coordinated through shared Concrete services.
/// Geometry/path/length in m; cover, spacing, diameters, anchor and lap lengths in mm. No shop-detail certification.</summary>
public static partial class WallReinforcementDetailing
{
    public static WallReinforcementDetails Calculate(WallDetailInput input, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested(); Validate(input);
        var g = input.Geometry; var r = input.Reinforcement; var opt = input.Options; var material = input.Material;
        double h=g.Height, t=g.Slab, a=g.Toe, s=g.StemBase, b=a+s+g.Heel, back=a+s;
        bool two=r.TwoZones; double split=two?r.LowerHeight:h;
        var bars=new List<WallBarDetail>(); var checks=new List<WallDetailCheck>(); double kg=0;
        double aggregate=opt.Aggregate, clearLap=opt.LapClearDistance, cover=input.Cover, percent=opt.LapPercent;
        WallRebarZone? currentZone=null; WallRebarFace? currentFace=null;
        void Add(WallDetailRule rule, double demand, double capacity, string unit)
        {
            if (!double.IsFinite(demand) || !double.IsFinite(capacity)) throw new ArithmeticException("Wall detail exceeds the numeric range.");
            checks.Add(new(rule,currentZone,currentFace,Math.Abs(demand),capacity,unit));
        }
        void Max(WallDetailRule rule, double actual, double limit) => Add(rule,actual,limit,"mm");
        void Min(WallDetailRule rule, double actual, double required, string unit="mm") => Add(rule,required,actual,unit);
        AnchorageResult Develop(double diameter, bool lap) => AnchorageCalculator.Calculate(DetailingProfile.Ntc2018,
            new(diameter,material.Fyd,material.Fctk05,material.GammaC,opt.GoodBond,0,lap,percent,clearLap));
        foreach (var key in Enum.GetValues<WallRebarZone>())
        {
            if (key == WallRebarZone.StemUpper && !two || key == WallRebarZone.Toe && a == 0 || key == WallRebarZone.Heel && g.Heel == 0) continue;
            var arm = r.Zones[key]; token.ThrowIfCancellationRequested(); bool stem = key is WallRebarZone.Stem or WallRebarZone.StemUpper, upper = key == WallRebarZone.StemUpper;
            double secondaryDia = arm.SecondaryDiameter, secondaryStep = arm.SecondarySpacing;
            for (int face = 0; face < 2; face++)
            {
                var selected = face == 0 ? arm.FirstFace : arm.OppositeFace;
                double dia = selected.Diameter; int count = selected.Count;
                currentZone = key; currentFace = stem ? face == 0 ? WallRebarFace.Upstream : WallRebarFace.Downstream : face == 0 ? WallRebarFace.Bottom : WallRebarFace.Top;
                double spacing = (1000 - 2 * cover - dia) / (count - 1), area = count * Math.PI * dia * dia / 4;
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
                    var other = twin;
                    double otherCount = (face == 0 ? other.FirstFace : other.OppositeFace).Count;
                    Add(WallDetailRule.BarCountContinuity, Math.Abs(count - otherCount), 0, "bars");
                }
                double secArea = Math.PI * secondaryDia * secondaryDia / 4 * 1000 / secondaryStep;
                double thickness = stem ? (upper ? s - (s - g.StemTop) * split / h : s) : t;
                Min(WallDetailRule.SecondaryArea, secArea, stem ? Math.Max(PlateDetailingRules.WallHorizontalMainRatio * area, PlateDetailingRules.WallHorizontalConcreteRatio / 2 * thickness * 1e6) : PlateDetailingRules.SlabSecondaryArea(area), "mm²/m");
                Max(WallDetailRule.SecondarySpacing, secondaryStep, stem ? PlateDetailingRules.WallHorizontalMaximumSpacing : PlateDetailingRules.SlabSecondarySpacing(t * 1000, true));
                double c = (cover + dia / 2) / 1000, anchor = arm.AnchorLength == 0 ? Math.Ceiling(dev.RequiredLength / 10) * 10 : arm.AnchorLength;
                var path = new List<WallBarPoint>(); double mandrel = 0;
                if (stem)
                {
                    double y0 = upper ? t + split - lapUsed / 2000 : t;
                    double y1 = upper || !two ? t + h - c : t + split + lapUsed / 2000;
                    double X(double y) => face == 0 ? back - c : a + (s - g.StemTop) * Math.Max(0, y - t) / h + c * Math.Sqrt(1 + Math.Pow((s - g.StemTop) / h, 2));
                    path.Add(new(X(y1), y1)); path.Add(new(X(y0), y0));
                    if (!upper)
                    {
                        double ab = Math.Min(cover + dia / 2, spacing / 2);
                        double requiredMandrel = Math.Max((dia <= 16 ? 4 : 7) * dia, Math.PI * dia * dia / 4 * material.Fyd / material.Fcd * (1 / ab + 1 / (2 * dia)));
                        mandrel = arm.BendDiameter == 0 ? Math.Ceiling(requiredMandrel / 10) * 10 : arm.BendDiameter;
                        Min(WallDetailRule.Mandrel, mandrel, requiredMandrel);
                        double radius = (mandrel + dia) / 2000, vertical = t - c - radius, direction = face == 0 ? 1 : -1;
                        Min(WallDetailRule.BendHeight, (t - 2 * c) * 1000, radius * 1000);
                        double tail = Math.Max(5 * dia / 1000, anchor / 1000 - Math.Max(0, vertical) - radius * Math.PI / 2);
                        double x = X(t), y = c + radius;
                        path.Add(new(x, y));
                        for (int j = 1; j <= 12; j++) { double angle = j * Math.PI / 24; path.Add(new(x + direction * radius * (1 - Math.Cos(angle)), y - radius * Math.Sin(angle))); }
                        path.Add(new(x + direction * (radius + tail), c));
                        double available = face == 0 ? b - c - x : x - c;
                        Max(WallDetailRule.TailExtent, (radius + tail) * 1000, Math.Max(0, available) * 1000);
                        anchor = Math.Max(0, vertical) * 1000 + radius * Math.PI / 2 * 1000 + tail * 1000;
                        Min(WallDetailRule.FootAnchorage, anchor, dev.RequiredLength);
                    }
                    else anchor = 0;
                }
                else
                {
                    double root = key == WallRebarZone.Toe ? a : back, direction = key == WallRebarZone.Toe ? 1 : -1, end = key == WallRebarZone.Toe ? c : b - c;
                    double y = face == 0 ? c : t - c;
                    path.Add(new(end, y)); path.Add(new(root + direction * anchor / 1000, y));
                    Min(WallDetailRule.RootAnchorage, anchor, dev.RequiredLength);
                    Max(WallDetailRule.AnchorageExtent, anchor, Math.Max(0, key == WallRebarZone.Toe ? b - c - root : root - c) * 1000);
                }
                double length = path.Zip(path.Skip(1), (p, q) => double.Hypot(p.X - q.X, p.Y - q.Y)).Sum();
                // The sampled arc is only for drawing; cut length uses its exact centre-line development.
                if (mandrel > 0) length += (mandrel + dia) / 2000 * (Math.PI / 2 - 24 * Math.Sin(Math.PI / 48));
                bars.Add(new("P" + (bars.Count + 1), key, currentFace.Value, dia, count, spacing, area, length, upper ? 0 : dev.RequiredLength, anchor, stem && two ? lap.RequiredLength : 0, lapUsed, mandrel, dev.Fbd, path.AsReadOnly()));
                kg += area * 1e-6 * length * WallRebarQuantities.SteelKgPerCubicMetre;
                double regionLength = stem ? (upper ? h - split : split) : key == WallRebarZone.Toe ? a + s / 2 : g.Heel + s / 2;
                kg += secArea * 1e-6 * regionLength * WallRebarQuantities.SteelKgPerCubicMetre;
            }
        }
        if (two)
        {
            currentZone = null; currentFace = null;
            double phi = bars.Where(x => x.Zone is WallRebarZone.Stem or WallRebarZone.StemUpper).Max(x => x.Diameter), l0 = bars.Max(x => x.Lap);
            double ties = opt.TieDiameter, step = opt.TieSpacing;
            Max(WallDetailRule.JunctionTieSpacing, step, Math.Min(150, 12 * phi));
            Min(WallDetailRule.JunctionTieArea, Math.Floor(l0 / 3 / step) * Math.PI * ties * ties / 4, .5 * Math.PI * phi * phi / 4, "mm²");
            kg += 2 * (s + .1) * Math.Ceiling(l0 / step) * Math.PI * ties * ties / 4 * 1e-6 * WallRebarQuantities.SteelKgPerCubicMetre;
        }
        token.ThrowIfCancellationRequested();
        if (!double.IsFinite(kg)) throw new ArithmeticException("Steel quantity exceeds the numeric range.");
        return new(bars.AsReadOnly(),checks.AsReadOnly(),kg);
    }
}
