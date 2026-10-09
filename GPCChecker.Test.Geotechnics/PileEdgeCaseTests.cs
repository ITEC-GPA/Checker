using GPC.Checkers.Geotechnics.Piles;
using GPC.Model.Data.Sections;
using GPC.Model.Geotechnics;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static GeotechnicsTests.GeotechnicsFixture;

namespace GeotechnicsTests;

/// <summary>Nq, Bustamante-Doix, CHS tube, Broms and axial capacity: closed forms, boundaries of every rule and every rejected input (mm, N, MPa, rad).</summary>
[TestClass]
public class PileEdgeCaseTests
{
    private static Soil Sand(double phi = 30, double gamma = 18, double sat = 20, double c = 0, double? cu = null) => new("Sand", gamma * KN3, sat * KN3, phi * Deg, c * KPa, "test", undrainedShearStrength: cu * KPa);
    private static Soil Clay(double cu, double phi = 25, double c = 0, double gamma = 19, double sat = 20) => new("Clay", gamma * KN3, sat * KN3, phi * Deg, c * KPa, "test", undrainedShearStrength: cu * KPa);
    private static SoilProfile Profile(double? waterDepth, params (Soil Soil, double Thickness)[] layers)
    {
        double top = 0; var list = new List<SoilLayer>();
        foreach (var (soil, t) in layers) { list.Add(new SoilLayer(soil, -top * M, -(top + t) * M)); top += t; }
        return new SoilProfile("P", list, "test", waterDepth.HasValue ? -waterDepth * M : null);
    }
    private static readonly StandardNTC2018Geotechnics Ntc = new();

    // ---------------------------------------------------------------- Nq

    [TestMethod]
    public void NqCurvesPassThroughTheAnchorsAndJoinSmoothly()
    {
        var anchors = new[] { (23.0, 35.6), (24.6, 37), (25.8, 37.8), (27.5, 38.8) };
        for (int i = 0; i < 4; i++)
        {
            Close(10, BearingCapacityFactors.Curve(anchors[i].Item1, i, false), "Nq(φ10) = 10, L/D " + BearingCapacityFactors.MediumRatios[i], 1e-14);
            Close(100, BearingCapacityFactors.Curve(anchors[i].Item2, i, false), "Nq(φ100) = 100", 1e-14);
        }
        // Nq*: continuous with continuous first and second derivatives at 34° and 38°.
        for (int i = 0; i < 2; i++)
            foreach (double knot in new[] { 34.0, 38 })
            {
                double h = 1e-4; Func<double, double> f = x => BearingCapacityFactors.Curve(x, i, true);
                Close(f(knot - 1e-12), f(knot + 1e-12), "C0 at " + knot, 1e-10);
                Close((f(knot) - f(knot - h)) / h, (f(knot + h) - f(knot)) / h, "C1 at " + knot, 1e-3);
                Close((f(knot) - 2 * f(knot - h) + f(knot - 2 * h)) / (h * h), (f(knot + 2 * h) - 2 * f(knot + h) + f(knot)) / (h * h), "C2 at " + knot, 2e-2);
            }
        Assert.AreEqual(800, BearingCapacityFactors.LargeDiameter); Assert.AreEqual("NQ-2026-09-09", BearingCapacityFactors.Version);
    }

    [TestMethod]
    public void NqInterpolationClippingAndErrors()
    {
        // On a curve: weight 0, no interpolation; φ = 30° in rad is read as 30° exactly.
        var on = BearingCapacityFactors.Nq(30 * Deg, 10, false);
        Assert.AreEqual(30, on.FrictionAngleDegrees); Assert.AreEqual(0, on.Weight); Assert.AreEqual(10, on.Ratio1); Assert.AreEqual(10, on.Ratio2); Close(BearingCapacityFactors.Curve(30, 1, false), on.Nq, "on the curve", 1e-15);
        // Medium: geometric interpolation (t = 0.5 at √50 gives the geometric mean); large: arithmetic.
        var mid = BearingCapacityFactors.Nq(30 * Deg, Math.Sqrt(50), false);
        Close(.5, mid.Weight, "t", 1e-12); Close(Math.Sqrt(mid.Value1 * mid.Value2), mid.Nq, "geometric mean", 1e-12);
        var large = BearingCapacityFactors.Nq(36 * Deg, Math.Sqrt(128), true);
        Assert.AreEqual("Nq*", large.Factor); Close((large.Value1 + large.Value2) / 2, large.Nq, "arithmetic mean", 1e-12);
        // z/D outside 5-50 (4-32): border curve with the flag; φ outside the visible part: border value with the flag.
        var shallow = BearingCapacityFactors.Nq(30 * Deg, 2, false); Assert.IsTrue(shallow.SlendernessClipped); Assert.AreEqual(5, shallow.AdoptedSlenderness);
        var deep = BearingCapacityFactors.Nq(30 * Deg, 80, true); Assert.IsTrue(deep.SlendernessClipped); Assert.AreEqual(32, deep.AdoptedSlenderness);
        var low = BearingCapacityFactors.Nq(20 * Deg, 5, false); Assert.IsTrue(low.FrictionAngleClipped); Assert.AreEqual(23.4, low.FrictionAngle1);
        Assert.IsFalse(BearingCapacityFactors.Nq(23.4 * Deg, 5, false).FrictionAngleClipped, "at the border of the visible part");
        Assert.IsTrue(BearingCapacityFactors.Nq(42.5 * Deg, 10, true).FrictionAngleClipped); Assert.IsFalse(BearingCapacityFactors.Nq(42 * Deg, 10, true).FrictionAngleClipped);
        // Monotonic: grows with φ, decreases with the slenderness.
        Assert.IsTrue(BearingCapacityFactors.Nq(33 * Deg, 10, false).Nq > BearingCapacityFactors.Nq(32 * Deg, 10, false).Nq);
        Assert.IsTrue(BearingCapacityFactors.Nq(33 * Deg, 20, false).Nq < BearingCapacityFactors.Nq(33 * Deg, 10, false).Nq);
        Assert.IsTrue(BearingCapacityFactors.Nq(36 * Deg, 32, true).Nq < BearingCapacityFactors.Nq(36 * Deg, 4, true).Nq);
        foreach (var (phi, ratio) in new[] { (double.NaN, 10.0), (double.PositiveInfinity, 10), (30 * Deg, 0), (30 * Deg, -1), (30 * Deg, double.PositiveInfinity), (30 * Deg, double.NaN) })
            Assert.ThrowsException<ArgumentException>(() => BearingCapacityFactors.Nq(phi, ratio, false), $"φ {phi} z/D {ratio}");
    }

    // ---------------------------------------------------------------- Bustamante-Doix

    [TestMethod]
    public void BustamanteDoixChartsRangesAndErrors()
    {
        foreach (var soil in Enum.GetValues<BustamanteDoixSoil>())
            foreach (var injection in new[] { MicropileInjection.IGU, MicropileInjection.IRS })
            {
                string code = BustamanteDoix.Family(soil) + (injection == MicropileInjection.IGU ? "2" : "1"); var chart = BustamanteDoix.Chart(code);
                var first = BustamanteDoix.UnitShaftResistance(soil, injection, chart[0].LimitPressure, 1);
                var last = BustamanteDoix.UnitShaftResistance(soil, injection, chart[chart.Count - 1].LimitPressure, 1);
                Assert.AreEqual(code, first.Curve); Assert.AreEqual(chart[0].UnitResistance, first.UnitResistance); Assert.AreEqual(chart[chart.Count - 1].UnitResistance, last.UnitResistance);
                Assert.ThrowsException<ArgumentException>(() => BustamanteDoix.UnitShaftResistance(soil, injection, chart[0].LimitPressure - 1e-9, 1), "below the chart");
                Assert.ThrowsException<ArgumentException>(() => BustamanteDoix.UnitShaftResistance(soil, injection, chart[chart.Count - 1].LimitPressure + 1e-9, 1), "above the chart");
                var range = BustamanteDoix.AlphaRange(soil, injection); Assert.IsTrue(range.Min <= range.Max && range.Min >= 1.1);
            }
        // Linear between two points: SG2 at 1.5 MPa = (0.10 + 0.20)/2.
        Close(.15, BustamanteDoix.UnitShaftResistance(BustamanteDoixSoil.MediumSand, MicropileInjection.IGU, 1.5, 1.1).UnitResistance, "SG2 1.5 MPa", 1e-15);
        Assert.AreEqual("Sabbia media", BustamanteDoix.Label(BustamanteDoixSoil.MediumSand)); Assert.AreEqual("R", BustamanteDoix.Family(BustamanteDoixSoil.WeatheredRock));
        // α is free (also outside the recommended range) but positive.
        Assert.AreEqual(3, BustamanteDoix.UnitShaftResistance(BustamanteDoixSoil.Clay, MicropileInjection.IRS, 1, 3).Alpha);
        foreach (double a in new[] { 0, -1, double.NaN, double.PositiveInfinity }) Assert.ThrowsException<ArgumentException>(() => BustamanteDoix.UnitShaftResistance(BustamanteDoixSoil.Clay, MicropileInjection.IRS, 1, a));
        Assert.ThrowsException<ArgumentException>(() => BustamanteDoix.UnitShaftResistance(BustamanteDoixSoil.Clay, MicropileInjection.IRS, double.NaN, 1.8));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => BustamanteDoix.AlphaRange((BustamanteDoixSoil)99, MicropileInjection.IGU));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => BustamanteDoix.AlphaRange(BustamanteDoixSoil.Clay, (MicropileInjection)7));
        Assert.ThrowsException<ArgumentException>(() => BustamanteDoix.Chart("XX1"));
    }

    [TestMethod]
    public void BustamanteDoixSegmentsAlongTheGroutedLength()
    {
        var layers = new[] { new MicropileLayer(0, 3 * M, BustamanteDoixSoil.MediumSand, 1.4), new MicropileLayer(3 * M, 8 * M, BustamanteDoixSoil.Silt, 1.5, false),
            new MicropileLayer(8 * M, 20 * M, BustamanteDoixSoil.Marl, 1.8) };
        var s = BustamanteDoix.Segments(layers, 12 * M, 2 * M, 250, MicropileInjection.IRS, 2);
        Assert.AreEqual(3, s.Length);
        Assert.AreEqual(2 * M, s[0].Top, "grouting starts inside the first layer"); Assert.AreEqual(3 * M, s[0].Bottom);
        Assert.IsFalse(s[1].ShaftActive); Assert.AreEqual(0, s[1].Lateral); Assert.IsNull(s[1].DrillDiameter);
        Assert.AreEqual(12 * M, s[2].Bottom, "cut at the depth reached");
        Close(Math.PI * 1.8 * 250 * 4 * M * BustamanteDoix.UnitShaftResistance(BustamanteDoixSoil.Marl, MicropileInjection.IRS, 2, 1.8).UnitResistance, s[2].Lateral, "π α D L s", 1e-14);
        Assert.AreEqual(0, BustamanteDoix.Segments(layers, 2 * M, 2 * M, 250, MicropileInjection.IRS, 2).Length, "depth at the start of the grouting");
        StringAssert.StartsWith(Assert.ThrowsException<ArgumentException>(() => BustamanteDoix.Segments(layers, 12 * M, 0, 250, MicropileInjection.IRS, .2)).Message, "Layer 1:");
        // From a Model profile with the axis inclined by 20°: s = z / cos θ.
        var profile = Profile(null, (Sand(), 3), (Clay(60), 9));
        var axis = MicropileLayer.FromProfile(profile, new[] { (BustamanteDoixSoil.SiltySand, 1.4, true), (BustamanteDoixSoil.Clay, 1.8, true) }, 20 * Deg);
        Close(3 * M / Math.Cos(20 * Deg), axis[0].Bottom, "s = z / cos θ", 1e-14); Close(12 * M / Math.Cos(20 * Deg), axis[1].Bottom, "tip", 1e-14);
        Assert.ThrowsException<ArgumentException>(() => MicropileLayer.FromProfile(profile, new[] { (BustamanteDoixSoil.SiltySand, 1.4, true) }));
        Assert.ThrowsException<ArgumentException>(() => MicropileLayer.FromProfile(profile, new[] { (BustamanteDoixSoil.SiltySand, 1.4, true), (BustamanteDoixSoil.Clay, 1.8, true) }, Math.PI / 2));
    }

    // ---------------------------------------------------------------- CHS tube

    [TestMethod]
    public void TubeWeightClassesAndResistingMoment()
    {
        var tube = new SectionCHS(139.7, 8);
        var w = MicropileTube.Weight(tube, TubeSteel, 240, 25 * KN3);
        Close(tube.Area, w.SteelArea, "steel area = Model SectionCHS", 1e-12); Close(Math.PI * 240 * 240 / 4 - tube.Area, w.GroutArea, "grout", 1e-12);
        Close(77.0085 * KN3 * w.SteelArea + 25 * KN3 * w.GroutArea, w.Total, "q = 77.0085 As + 25 Ac (kN/m³)", 1e-12);
        Assert.ThrowsException<ArgumentException>(() => MicropileTube.Weight(tube, TubeSteel, 139.7, 25 * KN3), "tube as wide as the borehole");
        foreach (var (d, g) in new[] { (0.0, 25.0), (-240.0, 25.0), (double.NaN, 25.0), (240.0, 0.0), (240.0, double.NaN) })
            Assert.ThrowsException<ArgumentException>(() => MicropileTube.Weight(tube, TubeSteel, d, g * KN3));
        Assert.AreEqual(1, MicropileTube.AxisCosine(0)); Assert.ThrowsException<ArgumentException>(() => MicropileTube.AxisCosine(Math.PI / 2)); Assert.ThrowsException<ArgumentException>(() => MicropileTube.AxisCosine(-1e-9));
        // Classes: D/t/(235/fy) ≤ 50, 70, 90.
        foreach (var (t, cls) in new[] { (2.0, 1), (1.99, 2), (100.0 / 70, 2), (1.42, 3), (100.0 / 90, 3), (1.1, 4) }) Assert.AreEqual(cls, MicropileTube.SectionClass(new SectionCHS(100, t), 235), "t " + t);
        Assert.AreEqual(2, MicropileTube.SectionClass(new SectionCHS(100, 2), 236), "ε² < 1 moves the limit");
        // My = Wpl fy/γM0 (1 − |N|/Npl), sign of N ignored; NTC γM0 1.05, EN 1.00.
        var steel = new SteelMaterial("S355", 210000, 355, 510); var ntc = new StandardNTC2018Steel(); var en = new StandardEN1993p11();
        var r = MicropileTube.LateralResistance(tube, steel, ntc, 300e3, 240);
        Close(tube.Wpl1 * 355 / 1.05, r.PlasticMoment, "Mpl", 1e-12); Close(r.PlasticMoment * (1 - 300e3 / r.PlasticAxial), r.ResistingMoment, "My", 1e-12);
        Assert.AreEqual(r.ResistingMoment, MicropileTube.LateralResistance(tube, steel, ntc, -300e3, 240).ResistingMoment);
        Close(1.05 * r.PlasticMoment, MicropileTube.LateralResistance(tube, steel, en, 0, 240).PlasticMoment, "EN γM0 = 1", 1e-12);
        Assert.ThrowsException<ArgumentException>(() => MicropileTube.LateralResistance(tube, steel, ntc, r.PlasticAxial, 240), "N = Npl");
        Assert.IsTrue(MicropileTube.LateralResistance(tube, steel, ntc, .999 * r.PlasticAxial, 240).ResistingMoment > 0);
        Assert.ThrowsException<ArgumentException>(() => MicropileTube.LateralResistance(new SectionCHS(273, 5), steel, ntc, 0, 400), "class 4: no ductility");
        Assert.ThrowsException<ArgumentException>(() => MicropileTube.LateralResistance(tube, steel, new StandardNTC2018Steel { GammaM0 = .99 }, 0, 240));
        Assert.ThrowsException<ArgumentException>(() => MicropileTube.LateralResistance(tube, steel, ntc, 0, 139.7), "tube not smaller than the borehole");
        Assert.ThrowsException<ArgumentException>(() => MicropileTube.LateralResistance(tube, steel, ntc, double.NaN, 240));
        // The catalogue of ModelData gives the same tube.
        var catalogue = (SectionCHS)SectionMappings.CreateSection("CHS 139.7 x 8");
        Assert.AreEqual(r.ResistingMoment, MicropileTube.LateralResistance(catalogue, steel, ntc, 300e3, 240).ResistingMoment);
    }

    // ---------------------------------------------------------------- Broms

    private static LateralPileResult Lateral(SoilProfile profile, SoilBehaviour[] kinds, double d, double l, double e, bool fixedHead, double my, LateralPileMethod method = LateralPileMethod.Broms,
        double h = 100e3, LateralGroupEfficiency? efficiency = null, int profiles = 1)
        => LateralPileCapacity.Calculate(new LateralPile(d, l, e, fixedHead, my, "test", method, h, 100), new[] { new LateralPileSurvey(profile, kinds) },
            LateralPileFactors.FromStandard(Ntc, profiles), efficiency ?? LateralGroupEfficiency.Manual(1));

    [TestMethod]
    public void BromsClosedFormsInClayAndSand()
    {
        double d = 600, l = 6 * M, cu = 50 * KPa, kp = LateralPileCapacity.PassivePressureCoefficient(30 * Deg), gamma = 18 * KN3;
        Close(3, kp, "Kp(30°) = 3", 1e-12);
        var clay = Profile(null, (Clay(50), 30)); var sand = Profile(null, (Sand(30), 30)); var C = new[] { SoilBehaviour.Cohesive }; var G = new[] { SoilBehaviour.Granular };
        // Fixed head, short pile: H = 9 cu d (L − 1.5 d) in clay, 1.5 γ d L² Kp in sand.
        Close(9 * cu * d * (l - 1.5 * d), Lateral(clay, C, d, l, 0, true, 1e12).Capacity, "clay short fixed", 1e-12);
        Close(1.5 * gamma * d * l * l * kp, Lateral(sand, G, d, l, 0, true, 1e12).Capacity, "sand short fixed", 1e-12);
        // Free head, long pile: My = H (1.5 d + f/2), f = H/(9 cu d) in clay; My = H · (2/3) √(H/(1.5 Kp γ d)) in sand.
        double my = 300e6;
        var longClay = Lateral(clay, C, d, 20 * M, 0, false, my); Assert.AreEqual(LateralMechanism.Long, longClay.Mechanism);
        double hc = longClay.Capacity; Close(my, hc * (1.5 * d + hc / (9 * cu * d) / 2), "Broms clay long", 1e-9);
        var longSand = Lateral(sand, G, d, 20 * M, 0, false, my); double hs = longSand.Capacity;
        Close(my, hs * 2.0 / 3 * Math.Sqrt(hs / (1.5 * kp * gamma * d)), "Broms sand long", 1e-9);
        Assert.AreEqual(LateralMechanism.Long, longSand.Mechanism); Assert.AreEqual(2, longSand.Surveys[0].Candidates.Count, "free head: short and long");
        Assert.AreEqual(3, Lateral(sand, G, d, l, 0, true, my).Surveys[0].Candidates.Count, "restrained head: short, intermediate and long");
        // A stronger section turns the long mechanism into the short one; the capacity never exceeds the short one.
        var strong = Lateral(sand, G, d, l, 0, false, 1e12);
        Assert.AreEqual(LateralMechanism.Short, strong.Mechanism); Assert.IsTrue(Lateral(sand, G, d, l, 0, false, my).Capacity <= strong.Capacity);
        // Eccentricity lowers the capacity.
        Assert.IsTrue(Lateral(sand, G, d, l, 1 * M, false, my).Capacity < Lateral(sand, G, d, l, 0, false, my).Capacity);
    }

    [TestMethod]
    public void BromsDiagramsAreInEquilibrium()
    {
        var sand = Profile(3, (Sand(32, 18, 20), 30)); var G = new[] { SoilBehaviour.Granular };
        foreach (var (e, fixedHead) in new[] { (0.0, false), (800.0, false), (0.0, true) })
        {
            var r = Lateral(sand, G, 800, 12 * M, e, fixedHead, 600e6); var s = r.Surveys[0]; string id = $"e {e} fixed {fixedHead}";
            Close(r.Capacity, s.Diagram[0].Shear, id + ": V(0) = H", 1e-12);
            Close(fixedHead ? -600e6 : r.Capacity * e, s.Diagram[0].Moment, id + ": M(0)", 1e-9, 1);
            Assert.IsTrue(s.Diagram.All(p => Math.Abs(p.Moment) <= 600e6 * (1 + 1e-5)), id + ": |M| ≤ My");
            Close(600e6, Math.Abs(s.Diagram.OrderBy(p => Math.Abs(p.Depth - s.ZeroShearDepth)).First().Moment), id + ": plastic hinge at zero shear", 1e-4);
            Assert.IsTrue(Math.Abs(s.ResidualForce) <= 1e-5 * s.LimitDiagram.Sum(x => x.Resultant) && s.Diagram.All(p => Math.Abs(p.Pressure - p.Reaction / 800) <= 1e-15));
            Assert.AreEqual(fixedHead ? 2 : 1, s.Hinges.Count, id);
        }
        // Water: γ' = γsat − γw below 3 m; the profile is no longer uniform in the Broms sense.
        var r2 = Lateral(sand, G, 800, 12 * M, 0, false, 600e6); var below = r2.Surveys[0].LimitDiagram.Single(x => x.Top == 3 * M);
        Close(3 * LateralPileCapacity.PassivePressureCoefficient(32 * Deg) * 800 * (20 - 9.81) * KN3, below.Slope, "slope below the water table", 1e-12);
        Assert.AreEqual("Multistrato sperimentale", r2.ModelName); Assert.IsTrue(r2.Experimental);
        Close((20 - 9.81) * KN3 * 0 + 18 * KN3 * 3 * M, below.EffectiveStressAtTop!.Value, "σ'v at the water table", 1e-12);
    }

    [TestMethod]
    public void BromsModelsGroupFactorsAndVerdict()
    {
        var mixed = Profile(null, (Clay(40), 2), (Sand(32), 30)); var kinds = new[] { SoilBehaviour.Cohesive, SoilBehaviour.Granular };
        Assert.ThrowsException<ArgumentException>(() => Lateral(mixed, kinds, 800, 10 * M, 0, false, 600e6), "mixed sequences need the stratified model");
        var stratified = Lateral(mixed, kinds, 800, 10 * M, 0, false, 600e6, LateralPileMethod.Stratified);
        Assert.AreEqual("Diagramma stratificato · terreno multistrato", stratified.ModelName); Assert.IsTrue(stratified.Surveys[0].Mixed && stratified.Surveys[0].DistributedClosure);
        Assert.AreEqual("Stratificato-ANTHEA-1", stratified.EngineVersion);
        var uniform = Lateral(Profile(null, (Clay(60), 30)), new[] { SoilBehaviour.Cohesive }, 800, 10 * M, 0, false, 600e6);
        Assert.AreEqual("Omogeneo", uniform.ModelName); Assert.IsFalse(uniform.Experimental); Assert.IsFalse(uniform.Surveys[0].StressesAvailable == false && false);
        // The reaction of a cohesive layer starts at 1.5 D.
        Assert.AreEqual(0, uniform.Surveys[0].LimitDiagram[0].InitialReaction); Assert.AreEqual(1.5 * 800, uniform.Surveys[0].LimitDiagram[0].Bottom);
        // Factors: NTC ξ of the profiles and γR = 1.3; EN 1997-1 has no transverse combination.
        Assert.AreEqual(1.3, LateralPileFactors.FromStandard(Ntc, 3).ResistanceFactor); Assert.AreEqual(1.60, LateralPileFactors.FromStandard(Ntc, 3).Xi3);
        Assert.ThrowsException<NotSupportedException>(() => LateralPileFactors.FromStandard(new StandardEN1997p1(), 1));
        // Reese and Van Impe: wide spacings give η = 1; tight ones reduce it; spacing below D is rejected.
        Assert.AreEqual(1, LateralGroupEfficiency.ReeseVanImpeEfficiency(800, 20 * M, 20 * M, 20 * M, 20 * M).Eta);
        var tight = LateralGroupEfficiency.ReeseVanImpeEfficiency(800, 800, 800, 800, 800);
        Close(.7 * .48 * .64 * .64, tight.Front * tight.Back * tight.Left * tight.Right, "s = D", 1e-15); Assert.IsTrue(tight.Eta < tight.Front * tight.Back * tight.Left * tight.Right);
        Assert.ThrowsException<ArgumentException>(() => LateralGroupEfficiency.ReeseVanImpeEfficiency(800, 799, 800, 800, 800));
        Assert.AreEqual(1, LateralGroupEfficiency.Manual(1).Eta); foreach (double eta in new[] { 0, -.1, 1.0001, double.NaN }) Assert.ThrowsException<ArgumentException>(() => LateralGroupEfficiency.Manual(eta));
        // Rd = min(mean/ξ3; min/ξ4)/γR·η; HEd = Rd is satisfied.
        var r = Lateral(Profile(null, (Sand(33), 30)), new[] { SoilBehaviour.Granular }, 800, 10 * M, 0, false, 600e6, h: 1, efficiency: LateralGroupEfficiency.Manual(.9));
        Close(Math.Min(r.Capacity / 1.7, r.Capacity / 1.7) / 1.3 * .9, r.DesignResistance, "Rd", 1e-12);
        var limit = Lateral(Profile(null, (Sand(33), 30)), new[] { SoilBehaviour.Granular }, 800, 10 * M, 0, false, 600e6, h: r.DesignResistance, efficiency: LateralGroupEfficiency.Manual(.9));
        Assert.IsTrue(limit.Satisfied); Close(1, limit.Utilization, "utilisation", 1e-12);
    }

    [TestMethod]
    public void LateralInputsAreChecked()
    {
        var sand = Profile(null, (Sand(), 30)); var G = new[] { SoilBehaviour.Granular }; var f = LateralPileFactors.FromStandard(Ntc, 1); var eta = LateralGroupEfficiency.Manual(1);
        void Reject(LateralPile p, SoilProfile profile, SoilBehaviour[] kinds, string what) => Assert.ThrowsException<ArgumentException>(() => LateralPileCapacity.Calculate(p, new[] { new LateralPileSurvey(profile, kinds) }, f, eta), what);
        LateralPile P(double d = 800, double l = 8000, double e = 0, bool fixedHead = false, double my = 500e6, string source = "s", double step = 100, double tol = 1e-8, double h = 1e5)
            => new(d, l, e, fixedHead, my, source, LateralPileMethod.Broms, h, step, tol);
        LateralPileCapacity.Calculate(P(tol: 1e-12), new[] { new LateralPileSurvey(sand, G) }, f, eta); LateralPileCapacity.Calculate(P(tol: 1e-5), new[] { new LateralPileSurvey(sand, G) }, f, eta);
        Reject(P(tol: 9e-13), sand, G, "tolerance below 1e-12"); Reject(P(tol: 1.1e-5), sand, G, "tolerance above 1e-5"); Reject(P(step: .3999), sand, G, "more than 20000 intervals");
        Reject(P(e: 1, fixedHead: true), sand, G, "eccentricity with a restrained head"); Reject(P(e: -1), sand, G, "negative eccentricity");
        Reject(P(my: 0), sand, G, "zero moment"); Reject(P(source: " "), sand, G, "moment without provenance"); Reject(P(d: 0), sand, G, "diameter"); Reject(P(l: double.NaN), sand, G, "length");
        Reject(P(h: -1), sand, G, "negative action"); Reject(P(l: 31 * M), sand, G, "layers shorter than the pile");
        Reject(P(), Profile(null, (Sand(60), 30)), G, "φ ≥ 60°"); Reject(P(), Profile(null, (Sand(30, c: 5), 30)), G, "c' in a granular layer");
        Reject(P(), Profile(null, (Sand(30), 30)), new[] { SoilBehaviour.Cohesive }, "cohesive layer without cu");
        Reject(P(l: 1000), Profile(null, (Clay(50), 30)), new[] { SoilBehaviour.Cohesive }, "L < 1.5 D in clay");
        Reject(P(), Profile(-1, (Sand(30), 30)), G, "water above the ground");
        Reject(P(), Profile(2, (Sand(30, 9, 9.5), 30)), G, "γsat ≤ γw below the water table");
        Assert.ThrowsException<ArgumentException>(() => new LateralPileSurvey(sand, new[] { SoilBehaviour.Granular, SoilBehaviour.Cohesive }), "one behaviour per layer");
        Assert.ThrowsException<ArgumentException>(() => LateralPileCapacity.Calculate(P(), new LateralPileSurvey[0], f, eta), "no vertical");
        Assert.ThrowsException<ArgumentNullException>(() => LateralPileCapacity.Calculate(P(), null!, f, eta));
    }

    // ---------------------------------------------------------------- Axial capacity

    private static AxialCapacityResult<AxialSurveyResistance> Axial(AxialPile pile, params AxialPileSurvey[] surveys)
        => AxialPileCapacity.Calculate(pile, surveys, PileResistanceFactors.FromStandard(Ntc, surveys.Length), PileGroupEfficiency.None());

    [TestMethod]
    public void AxialPileMatchesTheHandCalculation()
    {
        // Bored pile D = 0.6 m, L = 10 m in one dense sand layer γ = 18, φ' = 32°, no water: σ'v(L) = 180 kPa, shaft π D L K μ σ'v,mean, base A σ'v Nq.
        var survey = new AxialPileSurvey(Profile(null, (Sand(32), 20)), new[] { new AxialPileLayer(SoilBehaviour.Granular, SoilDensity.Dense) });
        var r = Axial(new AxialPile(PileInstallation.Bored, 600, 10 * M, compressionAction: 1000e3, tensionAction: 200e3), survey);
        var tip = r.Depths.Single(x => x.Depth == 10 * M); var s = tip.Surveys[0];
        double sigma = 18 * KN3 * 10 * M, k = .4, mu = Math.Tan(32 * Deg), nq = BearingCapacityFactors.Nq(32 * Deg, 10 * M / 600, false).Nq;
        Close(sigma, s.TipEffectiveStress, "σ'v", 1e-12);
        Close(Math.PI * 600 * 10 * M * k * mu * sigma / 2, s.DrainedShaft, "shaft", 1e-12); Close(Math.PI * 600 * 600 / 4 * sigma * nq, s.DrainedBase, "base", 1e-12);
        Assert.AreEqual(s.DrainedShaft, s.UndrainedShaft, "without water the two conditions coincide");
        // Design: min(mean/ξ3; min/ξ4) with γb 1.35, γs 1.15; one vertical: ξ3 = ξ4 = 1.70.
        double design = (s.DrainedBase / 1.35 + s.DrainedShaft / 1.15) / 1.7;
        Close(design, r.Curves[(AxialCondition.Drained, true)].Design.Single(x => x.Depth == 10 * M).Value, "design compression", 1e-12);
        Close(s.DrainedShaft / (1.7 * 1.25), r.Curves[(AxialCondition.Drained, false)].Design.Single(x => x.Depth == 10 * M).Value, "design tension", 1e-12);
        // Actions with the weight: 25 kN/m³ A L; NEd + 1.3 W; max(0; NEd,t − 1.0 W).
        double w = 25 * KN3 * Math.PI * 600 * 600 / 4 * 10 * M; Close(w, tip.Weight, "weight", 1e-12);
        Close(1000e3 + 1.3 * w, r.CompressionActions.Single(x => x.Depth == 10 * M).Value, "compression action", 1e-12);
        Close(Math.Max(0, 200e3 - w), r.TensionActions.Single(x => x.Depth == 10 * M).Value, "tension action", 1e-12, 1e-9);
        // Depth grid: 101 points, every 0.5 m, 0 and L.
        Assert.IsTrue(r.Depths.Any(x => x.Depth == 0) && r.Depths.Any(x => x.Depth == 2500) && r.Depths.Last().Depth == 10 * M);
        Assert.IsTrue(r.FullCoverage); Assert.AreEqual(10 * M, r.MaximumDepth);
    }

    [TestMethod]
    public void AxialCoefficientsAdhesionWaterAndUndrainedBase()
    {
        // K and μ by installation; α of the adhesion with the thresholds 25 and 70 kPa.
        Assert.AreEqual((.5, .4, "tanphi"), AxialPileCapacity.ShaftCoefficients(PileInstallation.Bored));
        Assert.AreEqual((1.0, 2.0, "tan20"), AxialPileCapacity.ShaftCoefficients(PileInstallation.DrivenClosedSteelTube));
        Close(Math.Tan(20 * Deg), AxialPileCapacity.LateralCoefficients(PileInstallation.DrivenSteelSection, SoilDensity.Loose, 35 * Deg).Mu, "μ steel", 1e-15);
        Close(Math.Tan(.75 * 32 * Deg), AxialPileCapacity.LateralCoefficients(PileInstallation.DrivenPrecastConcrete, SoilDensity.Dense, 32 * Deg).Mu, "μ precast", 1e-14);
        Assert.AreEqual(3, AxialPileCapacity.LateralCoefficients(PileInstallation.DrivenCastInPlace, SoilDensity.Dense, 30 * Deg).K);
        Assert.AreEqual(.7, AxialPileCapacity.Alpha(PileInstallation.Bored, 25 * KPa), 1e-12); Assert.AreEqual(.35, AxialPileCapacity.Alpha(PileInstallation.Bored, 70 * KPa), 1e-12);
        Close(.7 - .008 * 25, AxialPileCapacity.Alpha(PileInstallation.ContinuousFlightAuger, 50 * KPa), "α bored 50 kPa", 1e-12);
        Assert.AreEqual(1, AxialPileCapacity.Alpha(PileInstallation.DrivenSteelSection, 20 * KPa), 1e-12); Assert.AreEqual(.5, AxialPileCapacity.Alpha(PileInstallation.DrivenPrecastConcrete, 100 * KPa), 1e-12);
        Close(1 - .0111 * 25, AxialPileCapacity.Alpha(PileInstallation.DrivenCastInPlace, 50 * KPa), "α driven 50 kPa", 1e-12);
        // Clay below the water table at 2 m: undrained base A (Nc cu + σv) below, drained above; buoyancy reduces the weight, never below zero.
        var clay = new AxialPileSurvey(Profile(2, (Clay(80, 24, 5), 30)), new[] { new AxialPileLayer(SoilBehaviour.Cohesive, SoilDensity.Dense, 9) });
        var r = Axial(new AxialPile(PileInstallation.Bored, 800, 12 * M, buoyancy: true), clay);
        var at = r.Depths.Single(x => x.Depth == 12 * M).Surveys[0]; double sv = 19 * KN3 * 2 * M + 20 * KN3 * 10 * M;
        Close(sv, at.TipTotalStress, "σv", 1e-12); Close(Math.PI * 800 * 800 / 4 * (9 * 80 * KPa + sv), at.UndrainedBase, "undrained base", 1e-12);
        var above = r.Depths.Single(x => x.Depth == 2 * M).Surveys[0]; Assert.AreEqual(above.DrainedBase, above.UndrainedBase, "above the water table the drained base");
        Close(19 * KN3 * 2 * M + (20 * KN3 - SoilUnits.WaterUnitWeight) * 10 * M, at.TipEffectiveStress, "σ'v below the water table", 1e-12);
        double area = Math.PI * 800 * 800 / 4; Close(25 * KN3 * area * 12 * M - SoilUnits.WaterUnitWeight * area * 10 * M, r.Depths.Single(x => x.Depth == 12 * M).Weight, "buoyant weight", 1e-12);
        var light = Axial(new AxialPile(PileInstallation.Bored, 800, 12 * M, unitWeight: 5 * KN3, buoyancy: true), clay);
        Assert.AreEqual(0, light.Depths.Single(x => x.Depth == 12 * M).Weight, "never negative");
        Assert.IsTrue(r.Depths.Any(x => x.Depth == 2 * M), "water depth in the grid");
        // Inactive shaft: zero shear in both conditions; mean stress split at the water table inside a layer.
        var off = new AxialPileSurvey(Profile(null, (Sand(30), 2), (Sand(34), 20)), new[] { new AxialPileLayer(SoilBehaviour.Granular, SoilDensity.Loose, shaftActive: false), new AxialPileLayer(SoilBehaviour.Granular, SoilDensity.Dense) });
        var seg = Axial(new AxialPile(PileInstallation.Bored, 600, 10 * M), off).Depths.Last().Surveys[0].Segments[0];
        Assert.IsFalse(seg.ShaftActive); Assert.AreEqual(0, seg.DrainedLateral); Assert.AreEqual(0, seg.UndrainedLateral);
    }

    [TestMethod]
    public void AxialSurveysEfficiencyAndChecks()
    {
        AxialPileSurvey S(double phi) => new(Profile(null, (Sand(phi), 20)), new[] { new AxialPileLayer(SoilBehaviour.Granular, SoilDensity.Dense) });
        var pile = new AxialPile(PileInstallation.Bored, 600, 10 * M);
        var two = AxialPileCapacity.Calculate(pile, new[] { S(30), S(34) }, PileResistanceFactors.FromStandard(Ntc, 2), PileGroupEfficiency.None());
        var a = two.Depths.Last(); var c = a.Components[(AxialCondition.Drained, true)];
        Close((a.Surveys[0].DrainedShaft + a.Surveys[1].DrainedShaft) / 2, c.Mean.Shaft, "mean of the verticals", 1e-12); Assert.AreEqual(a.Surveys[0].DrainedShaft, c.Minimum.Shaft, "minimum");
        Close(c.Mean.Shaft / (1.65 * 1.15) + c.Mean.Base / (1.65 * 1.35), two.Curves[(AxialCondition.Drained, true)].Mean.Last().Value, "mean branch ξ3 = 1.65", 1e-12);
        // Group efficiencies.
        Assert.AreEqual(1, PileGroupEfficiency.Feld(1, 1).Compression); Close(1 - 2.0 / 2 / 16, PileGroupEfficiency.Feld(2, 1).Compression, "Feld 2×1", 1e-15);
        double theta = Math.Atan(600.0 / 1800) * 180 / Math.PI / 90;
        Close(1 - theta * 2 / 3 - theta * 1 / 2, PileGroupEfficiency.ConverseLabarre(3, 2, 1800, 1800, 600).Compression, "Converse-Labarre", 1e-15);
        Assert.AreEqual(6, PileGroupEfficiency.ConverseLabarre(3, 2, 1800, 1800, 600).Piles);
        Assert.ThrowsException<ArgumentException>(() => PileGroupEfficiency.ConverseLabarre(10, 10, 300, 300, 600), "η ≤ 0");
        Assert.ThrowsException<ArgumentException>(() => PileGroupEfficiency.ConverseLabarre(2, 1, 0, 1, 600)); Assert.ThrowsException<ArgumentException>(() => PileGroupEfficiency.Feld(0, 1));
        Assert.ThrowsException<ArgumentException>(() => PileGroupEfficiency.UserDefined(0, 1)); Assert.ThrowsException<ArgumentException>(() => PileGroupEfficiency.UserDefined(1, double.NaN));
        var eta = AxialPileCapacity.Calculate(pile, new[] { S(32) }, PileResistanceFactors.FromStandard(Ntc, 1), PileGroupEfficiency.UserDefined(.8, .7));
        var plain = Axial(pile, S(32));
        Close(.8 * plain.Curves[(AxialCondition.Drained, true)].Design.Last().Value, eta.Curves[(AxialCondition.Drained, true)].Design.Last().Value, "ηc", 1e-12);
        Close(.7 * plain.Curves[(AxialCondition.Drained, false)].Design.Last().Value, eta.Curves[(AxialCondition.Drained, false)].Design.Last().Value, "ηt", 1e-12);
        // Factors of the Model standards.
        var ntc = PileResistanceFactors.FromStandard(Ntc, 1); Assert.AreEqual((1.35, 1.15, 1.25, 1.3, 1.0, 1.7, 1.7), (ntc.Base, ntc.ShaftCompression, ntc.ShaftTension, ntc.WeightUnfavourable, ntc.WeightFavourable, ntc.Xi3, ntc.Xi4));
        var en = new StandardEN1997p1(); var c1 = PileResistanceFactors.FromStandard(en, 1, 0); var c2 = PileResistanceFactors.FromStandard(en, 1, 1);
        Assert.AreEqual(1.25, c1.Base, "DA1-C1 bored R1"); Assert.AreEqual(1.6, c2.Base, "DA1-C2 bored R4"); Assert.AreEqual(1.35, c1.WeightUnfavourable); Assert.AreEqual(1.0, c2.WeightUnfavourable);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => PileResistanceFactors.FromStandard(Ntc, 1, 1));
        // NTC Tab. 6.4.II by execution of the Model standard: driven γb 1.15, CFA 1.3.
        Assert.AreEqual(1.15, PileResistanceFactors.FromStandard(new StandardNTC2018Geotechnics { PileExecution = PileExecution.Driven }, 1).Base);
        Assert.AreEqual(1.3, PileResistanceFactors.FromStandard(new StandardNTC2018Geotechnics { PileExecution = PileExecution.ContinuousFlightAuger }, 1).Base);
        // Checks.
        Assert.ThrowsException<ArgumentException>(() => Axial(new AxialPile(PileInstallation.Bored, 0, 10 * M), S(30)));
        Assert.ThrowsException<ArgumentException>(() => Axial(new AxialPile(PileInstallation.Bored, 600, 10 * M, unitWeight: 0), S(30)));
        Assert.ThrowsException<ArgumentException>(() => Axial(new AxialPile(PileInstallation.Bored, 600, 10 * M, compressionAction: double.NaN), S(30)));
        Assert.ThrowsException<ArgumentException>(() => AxialPileCapacity.Calculate(pile, new AxialPileSurvey[0], ntc, PileGroupEfficiency.None()));
        var wet = new AxialPileSurvey(Profile(3, (Sand(30), 20)), new[] { new AxialPileLayer(SoilBehaviour.Granular, SoilDensity.Dense) });
        Assert.ThrowsException<ArgumentException>(() => AxialPileCapacity.Calculate(pile, new[] { S(30), wet }, PileResistanceFactors.FromStandard(Ntc, 2), PileGroupEfficiency.None()), "different water tables");
        Assert.ThrowsException<ArgumentException>(() => Axial(pile, new AxialPileSurvey(Profile(-1, (Sand(30), 20)), new[] { new AxialPileLayer(SoilBehaviour.Granular, SoilDensity.Dense) })), "water above ground");
        Assert.ThrowsException<ArgumentException>(() => Axial(pile, new AxialPileSurvey(Profile(3, (Sand(30), 20)), new[] { new AxialPileLayer(SoilBehaviour.Cohesive, SoilDensity.Dense) })), "cohesive below water without cu");
        Assert.ThrowsException<ArgumentException>(() => new AxialPileSurvey(Profile(null, (Sand(30), 20)), new AxialPileLayer[0]));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new AxialPileLayer(SoilBehaviour.Cohesive, SoilDensity.Dense, -1));
        // Shorter profile than the pile: curves up to the covered depth only.
        var shortProfile = Axial(new AxialPile(PileInstallation.Bored, 600, 25 * M), S(30));
        Assert.IsFalse(shortProfile.FullCoverage); Assert.AreEqual(20 * M, shortProfile.MaximumDepth);
    }

    [TestMethod]
    public void MicropileShaftBaseShareInclinationAndChecks()
    {
        var profile = Profile(null, (Sand(), 3), (Sand(), 20));
        MicropileSurvey S() => new(profile, new[] { (BustamanteDoixSoil.MediumSand, 1.4, true), (BustamanteDoixSoil.SandyGravel, 1.6, true) });
        Micropile P(double theta = 0, double start = 0, double? share = null, double pressure = 2, double length = 10 * M, double d = 250, string tube = "CHS 139.7 x 8")
            => new(d, length, theta * Deg, MicropileInjection.IGU, pressure, start, share, (SectionCHS)SectionMappings.CreateSection(tube), TubeSteel);
        var f = PileResistanceFactors.FromStandard(Ntc, 1);
        var r = AxialPileCapacity.Calculate(P(share: 10), new[] { S() }, f, PileGroupEfficiency.None());
        var tip = r.Depths.Last(); var s = tip.Surveys[0];
        double s1 = BustamanteDoix.UnitShaftResistance(BustamanteDoixSoil.MediumSand, MicropileInjection.IGU, 2, 1.4).UnitResistance;
        double s2 = BustamanteDoix.UnitShaftResistance(BustamanteDoixSoil.SandyGravel, MicropileInjection.IGU, 2, 1.6).UnitResistance;
        Close(Math.PI * 250 * (1.4 * 3 * M * s1 + 1.6 * 7 * M * s2), s.Shaft, "Σ π α D L s", 1e-12); Close(.1 * s.Shaft, s.Base, "base 10%", 1e-15);
        Close((s.Shaft / 1.15 + s.Base / 1.35) / 1.7, r.Curves[(AxialCondition.Drained, true)].Design.Last().Value, "design", 1e-12);
        Assert.AreEqual(1, r.Curves.Keys.Count(k => k.Compression), "one condition only");
        var w = MicropileTube.Weight(P().Tube, TubeSteel, 250, 25 * KN3); Close(w.Total * 10 * M, tip.Weight, "weight along the axis", 1e-12);
        // Inclined 20°: the layers along the axis are longer, the weight is projected.
        var inclined = AxialPileCapacity.Calculate(P(theta: 20), new[] { S() }, f, PileGroupEfficiency.None());
        Close(w.Total * 10 * M * Math.Cos(20 * Deg), inclined.Depths.Last().Weight, "projected weight", 1e-12);
        Assert.IsTrue(inclined.Depths.Any(x => Math.Abs(x.Depth - 3 * M / Math.Cos(20 * Deg)) < 1e-9), "layer interface along the axis");
        // Grouting from 2 m; warnings for IRS from the surface and for a grouted length below 4 m.
        var late = AxialPileCapacity.Calculate(P(start: 2 * M), new[] { S() }, f, PileGroupEfficiency.None());
        Assert.IsTrue(late.Depths.Last().Surveys[0].Shaft < s.Shaft); Assert.IsTrue(late.Depths.Any(x => x.Depth == 2 * M));
        Assert.IsTrue(AxialPileCapacity.Calculate(P(start: 7 * M), new[] { S() }, f, PileGroupEfficiency.None()).Warnings.Any(x => x.Contains("4 m")));
        Assert.AreEqual(0, AxialPileCapacity.Calculate(P(share: 15), new[] { S() }, f, PileGroupEfficiency.None()).Depths[0].Surveys[0].Shaft);
        // Checks.
        void Reject(Micropile p, string what) => Assert.ThrowsException<ArgumentException>(() => AxialPileCapacity.Calculate(p, new[] { S() }, f, PileGroupEfficiency.None()), what);
        Reject(P(share: 15.01), "base share above 15%"); Reject(P(share: -1), "negative base share"); Reject(P(start: 10 * M), "grouting from the tip");
        Reject(P(start: -1), "negative start"); Reject(P(theta: 90), "horizontal"); Reject(P(pressure: 0), "pressure"); Reject(P(pressure: 8), "pressure outside the chart");
        Reject(P(d: 139.7), "tube as wide as the borehole"); Reject(P(length: 0), "length");
        Assert.ThrowsException<ArgumentException>(() => new MicropileSurvey(profile, new[] { (BustamanteDoixSoil.MediumSand, 1.4, true) }));
    }
}
