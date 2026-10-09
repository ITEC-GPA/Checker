using GPC.Checkers.Geotechnics.Seismic;
using GPC.Checkers.Geotechnics.Walls;
using GPC.Model.Geotechnics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static GeotechnicsTests.GeotechnicsFixture;
using static GeotechnicsTests.WallsFixture;

namespace GeotechnicsTests;

/// <summary>Retaining walls: closed forms by hand, boundaries of every rule, combinations, seismic site and every rejected input (mm, N, MPa, rad).</summary>
[TestClass]
public class WallEdgeCaseTests
{
    // Benchmark of ANTHEA: H = 3 m, s = 0.3 m, t = 0.4 m, toe 0.8 m, heel 1.9 m; fill γ 18, φ 30°; foundation φ 34°, γ 19, δb 26°; q = 10 kPa; wall γ 25.
    internal static Soil Fill(double phi = 30, double gamma = 18, double sat = 20) => new("Fill", gamma * KN3, sat * KN3, phi * Deg, 0, "test");
    internal static SoilProfile Column(double ground, params (Soil Soil, double Thickness)[] layers)
    {
        double top = ground; var list = new List<SoilLayer>();
        foreach (var (soil, t) in layers) { list.Add(new SoilLayer(soil, top * M, (top - t) * M)); top -= t; }
        return new SoilProfile("column", list, "test");
    }
    internal static WallInput Wall(double h = 3, double s0 = .3, double s1 = .3, double t = .4, double toe = .8, double heel = 1.9, SoilProfile? fill = null, WallValley? valley = null,
        WallWater? water = null, WallInterface? back = null, WallInterface? @base = null, IEnumerable<WallAction>? actions = null, WallSeismic? seismic = null, double phiF = 34, double deltaB = 26,
        WallFamily family = WallFamily.Cantilever, double gamma = 25)
    {
        double ht = h + t;
        return new WallInput(family, new WallGeometry(h * M, s0 * M, s1 * M, t * M, toe * M, heel * M), gamma * KN3, fill ?? Column(ht, (Fill(), 10)),
            new Soil("Foundation", 19 * KN3, 21 * KN3, phiF * Deg, 0, "test"), valley ?? WallValley.Free(Column(0, (Fill(), 10))), back ?? new WallInterface(WallFriction.Assigned),
            @base ?? new WallInterface(WallFriction.Assigned, deltaB * Deg), water, actions ?? new[] { Surcharge(10) }, seismic);
    }
    internal static WallAction Surcharge(double kPa, string id = "q", WallActionCategory category = WallActionCategory.Q, double psi0 = .7, string group = "")
        => new(id, "q", WallActionType.UniformSurcharge, category, kPa * KPa, psi0: psi0, group: group);
    internal static WallCombination Row(WallLimitState state = WallLimitState.Characteristic, double wall = 1, double soil = 1, double water = 1, double mphi = 1, double rs = 1, double ro = 1,
        double rb = 1, double kh = 0, double kv = 0, IReadOnlyDictionary<string, double>? f = null, string name = "C", double? valley = null)
        => new(name, state, wall, soil, valley ?? soil, water, mphi, rs, ro, rb, kh, kv, f ?? new Dictionary<string, double> { ["q"] = 1 });
    static WallCaseResult One(WallInput input, WallCombination row) => RetainingWallAnalysis.Calculate(input, new[] { row }).Cases[0];

    [TestMethod]
    public void BenchmarkMatchesTheHandCalculation()
    {
        var c = One(Wall(), Row());
        // Thrust on the vertical plane behind the heel, Ka = 1/3: ½ γ Ka Ht² + q Ka Ht; moments about the base: γ Ka Ht³/6 + q Ka Ht²/2.
        double ht = 3400, ka = 1.0 / 3, gamma = 18 * KN3, q = 10 * KPa;
        Close(.5 * gamma * ka * ht * ht + q * ka * ht, c.Horizontal, "H", 1e-12);
        Close(gamma * ka * Math.Pow(ht, 3) / 6 + q * ka * ht * ht / 2, c.Overturning, "overturning", 1e-12);
        // Weights: stem 0.3 x 3 x 25 = 22.5, slab 3 x 0.4 x 25 = 30, fill 1.9 x 3 x 18 = 102.6, q on the heel 19 kN/m.
        Close(30 + 22.5 + 102.6 + 19, c.Vertical, "V", 1e-12);
        Close((30 * 1.5 + 22.5 * .95 + (102.6 + 19) * 2.05) * KNm, c.Stabilizing, "stabilising", 1e-12);
        // Contact: x = (Ms − Mr)/N, B = 3 m, trapezoid when |e| ≤ B/6.
        double x = (c.Stabilizing - c.Overturning) / c.Vertical; Close(x, c.X, "x", 1e-14); Close(1500 - x, c.Eccentricity, "e", 1e-12);
        Assert.IsTrue(c.Contact.Valid); Close(c.Vertical / 3000 * (1 + 6 * c.Eccentricity / 3000), c.Contact.Toe, "toe pressure", 1e-12);
        // Stem root: M = γ Ka H³/6 + q Ka H²/2 (H = 3 m).
        var root = c.Sections.Single(s => s.Member == WallMember.Stem && s.Position == 3000);
        Close(gamma * ka * 27e9 / 6 + q * ka * 9e6 / 2, root.M, "stem root", 1e-12); Close(.5 * gamma * ka * 9e6 + q * ka * 3000, root.V, "stem shear", 1e-12);
        Close(22.5, root.N, "stem N", 1e-12);
        // Toe 0.8 m: net upward contact minus the slab, integrated by hand.
        double slope = (c.Contact.Heel - c.Contact.Toe) / 3000;
        Close((c.Contact.Toe - 10e-3) * 800 * 800 / 2 + slope * Math.Pow(800, 3) / 6, c.Sections.Last(s => s.Member == WallMember.Toe).M, "toe root", 1e-11);
        // Heel 1.9 m: downward slab 10, fill 54, q 10 kPa against the contact.
        double heelPressure = c.Contact.Toe + slope * 1100 - (10 + 54 + 10) * KPa;
        Close(heelPressure * 1900 * 1900 / 2 + slope * Math.Pow(1900, 3) / 3, c.Sections.Last(s => s.Member == WallMember.Heel).M, "heel root", 1e-11);
        // Bearing EN 1997-1 Annex D, q' = 0: ½ γ B'² Nγ (1 − H/V)³.
        double phi = 34 * Deg, nq = Math.Exp(Math.PI * Math.Tan(phi)) * Math.Pow(Math.Tan(Math.PI / 4 + phi / 2), 2);
        Close(.5 * 19 * KN3 * c.EffectiveWidth * c.EffectiveWidth * 2 * (nq - 1) * Math.Tan(phi) * Math.Pow(1 - c.Horizontal / c.Vertical, 3), c.BearingResistance!.Value, "bearing", 1e-12);
        Close(c.Vertical * Math.Tan(26 * Deg), c.SlidingResistance, "sliding", 1e-12);
        Assert.AreEqual(21, c.Sections.Count(s => s.Member == WallMember.Toe)); Assert.AreEqual(21, c.Sections.Count(s => s.Member == WallMember.Heel));
        Close(2.1e6 + 0, RetainingWallAnalysis.Calculate(Wall(), new[] { Row() }).Area, "area", 1e-12);
    }

    [TestMethod]
    public void ContactLawKeepsTheEquilibrium()
    {
        foreach (double x in new[] { 100.0, 500, 1000, 1500, 2000, 2500, 2900 })
        {
            var p = WallContact.Law(3000, 100, x);
            var v = WallPressureSegment.Integrate(new[] { new WallPressureSegment(p.Start, p.End, p.Toe, p.Heel) }, 0, 3000, 0);
            Close(100, v.Force, "N x=" + x, 1e-12); Close(100 * x, v.Moment, "M x=" + x, 1e-12);
            Assert.IsTrue(p.Peak >= Math.Max(p.Toe, p.Heel) - 1e-15);
        }
        Assert.IsFalse(WallContact.Law(3000, 100, 0).Valid); Assert.IsFalse(WallContact.Law(3000, -1, 1000).Valid); Assert.IsFalse(WallContact.Law(3000, 100, 3000).Valid);
        // Middle third: trapezoid on the whole base; outside: triangle 3x long.
        Assert.AreEqual(0, WallContact.Law(3000, 100, 1000).Start); Assert.AreEqual(3000, WallContact.Law(3000, 100, 1000).End);
        Close(3 * 800, WallContact.Law(3000, 100, 800).End, "triangle"); Close(3000 - 3 * 600, WallContact.Law(3000, 100, 2400).Start, "triangle at the heel");
        Assert.AreEqual(0, WallContact.Law(3000, 100, 1000).Pressure(-1)); Assert.AreEqual(0, WallContact.Law(3000, 100, 800).Pressure(2500));
    }

    [TestMethod]
    public void WaterAddsHydrostaticPressureAndUplift()
    {
        var dry = One(Wall(), Row()); var wet = One(Wall(water: new WallWater(1400)), Row());
        double gw = SoilUnits.WaterUnitWeight, q = 10 * KPa, ka = 1.0 / 3;
        // Uplift: triangle from 0 at the toe to γw 2 m at the heel (no front head).
        Close(.5 * gw * 2000 * 3000, wet.Uplift, "uplift", 1e-12);
        // Thrust: dry above 1.4 m, γ' = 20 − 9.81 below, water 2 m.
        double expected = 18 * KN3 * 1400 * 1400 * ka / 2 + (18 * KN3 * 1400 * 2000 + .5 * (20 * KN3 - gw) * 4e6) * ka + q * ka * 3400 + .5 * gw * 4e6;
        Close(expected, wet.Horizontal, "H", 1e-12);
        Close(30 + 22.5 + 1.9 * (1.4 * 18 + 1.6 * 20) + 19 - wet.Uplift, wet.Vertical, "V", 1e-12);
        Close(wet.Vertical * wet.X, wet.Stabilizing - wet.Overturning, "equilibrium", 1e-12);
        // A front head of 0.3 m removes ½ γw 0.3² from the thrust and adds to the uplift.
        var front = One(Wall(water: new WallWater(1400, 300)), Row());
        Close(.5 * gw * 300 * 300, wet.Horizontal - front.Horizontal, "front water", 1e-9);
        Close((300 + 2000) * gw * 3000 / 2, front.Uplift, "uplift with front", 1e-12);
        Assert.IsTrue(dry.Uplift == 0 && dry.Horizontal < wet.Horizontal);
        // The water factor multiplies water pressures and uplift only.
        var factored = One(Wall(water: new WallWater(1400)), Row(water: 1.3));
        Close(1.3 * wet.Uplift, factored.Uplift, "factored uplift", 1e-12);
        Close(wet.Horizontal + .3 * .5 * gw * 4e6, factored.Horizontal, "factored water thrust", 1e-12);
    }

    [TestMethod]
    public void SplittingALayerChangesNothing()
    {
        var one = One(Wall(), Row()); var two = One(Wall(fill: Column(3.4, (Fill(), 1.3), (Fill(), 4))), Row());
        Close(one.Horizontal, two.Horizontal, "H", 1e-12); Close(one.Overturning, two.Overturning, "M", 1e-12); Close(one.Vertical, two.Vertical, "V", 1e-12);
        Assert.AreEqual(one.Sections.Count, two.Sections.Count);
        for (int i = 0; i < one.Sections.Count; i++) Close(one.Sections[i].M, two.Sections[i].M, "cut " + i, 1e-10, 1e-6);
        // A stiffer lower layer: discontinuity of the pressure at the interface, Rankine of each layer with the integrated σ'v.
        var layered = One(Wall(fill: Column(3.4, (Fill(30), 1.3), (Fill(36), 4))), Row());
        var d = layered.PressureDetails;
        Close(EarthPressure.RankineActive(36 * Deg) * 18 * KN3 * 1300, d.First(x => Math.Abs(x.Z0 - 1300) < 1e-6).Soil0, "σ'v at the interface", 1e-12);
        Close(EarthPressure.RankineActive(30 * Deg) * 18 * KN3 * 1300, d.First(x => Math.Abs(x.Z1 - 1300) < 1e-6).Soil1, "upper layer", 1e-12);
    }

    [TestMethod]
    public void SeismicPressuresFollowMononobeOkabeAndWood()
    {
        var seismic = WallSeismic.Assigned(WallSeismicMethod.MononobeOkabe, .1, .05);
        var input = Wall(seismic: seismic);
        var stat = One(input, Row()); var mo = One(input, Row(WallLimitState.Seismic, kh: .1, kv: -.05));
        Close(EarthPressure.RankineActive(30 * Deg), EarthPressure.MononobeOkabe(30 * Deg, 0, 0), "MO at kh = 0", 1e-14);
        Close(EarthPressure.ActiveHorizontal(30 * Deg, 0, .1, -.05), mo.PressureDetails[0].Ke, "Ke", 1e-14);
        Assert.IsTrue(mo.Horizontal > stat.Horizontal);
        // Dynamic increment at mid height: (Ke − K)(1 − kv) γ Ht / 2, uniform.
        Close((mo.PressureDetails[0].Ke - mo.PressureDetails[0].K) * 1.05 * 18 * KN3 * 3400 / 2, mo.PressureDetails[0].Dynamic, "dynamic", 1e-12);
        // Inertia kh of the wall, of the fill and of the surcharge on the heel.
        Close(.1 * (22.5 + 30 + 102.6 + 19), mo.Horizontal - WallPressureSegment.Integrate(mo.Pressures, 0, 3400, 3400).Force, "inertia", 1e-12);
        // Wood: K0 = 1 − sin φ = 0.5, ΔP = kh γ Ht² uniform, static K0 q only.
        var wood = Wall(seismic: WallSeismic.Assigned(WallSeismicMethod.Wood, .1, 0));
        var w = One(wood, Row(WallLimitState.Seismic, kh: .1));
        Close(.5, w.PressureDetails[0].K, "K0", 1e-14); Close(.1 * 18 * KN3 * 3400 * 3400, w.PressureDetails[0].Dynamic * 3400, "Wood resultant", 1e-12);
        Close(.5 * 10 * KPa, w.PressureDetails[0].Surcharge, "Wood surcharge", 1e-14);
        Assert.AreEqual(0, w.Soil.WallFriction, "Wood mobilises no friction");
        // The fill factor multiplies the seismic increment too.
        Close(1.3 * w.PressureDetails[0].Dynamic, One(wood, Row(WallLimitState.Seismic, soil: 1.3, kh: .1)).PressureDetails[0].Dynamic, "γ on the increment", 1e-12);
        // Seismic bearing from the site requires the site: assigned coefficients give the message, no capacity.
        Assert.IsNull(mo.BearingResistance); StringAssert.StartsWith(mo.SeismicBearingError, "Portanza sismica: inserire");
        var assigned = One(Wall(seismic: WallSeismic.Assigned(WallSeismicMethod.MononobeOkabe, .1, .05, WallSeismicBearing.Assigned(.25, .12))), Row(WallLimitState.Seismic, rb: 1.2, kh: .1, kv: .05));
        Assert.IsNotNull(assigned.SeismicBearing); Close(assigned.SeismicBearing!.Capacity, assigned.BearingResistance!.Value, "Annex F", 1e-15);
        Throws(() => EarthPressure.MononobeOkabe(15 * Deg, .3, 0), "θ ≥ φ");
    }

    [TestMethod]
    public void PassiveResistanceIsLimitedAndNeverSeismic()
    {
        var front = Column(1, (Fill(30), .6), (Fill(34), 5));
        var none = One(Wall(valley: new WallValley(1000, front)), Row());
        var half = One(Wall(valley: new WallValley(1000, front, true, .5)), Row());
        Assert.AreEqual(0, none.Soil.PassiveAvailable);
        Assert.IsTrue(half.Soil.PassiveAvailable > 0 && half.Soil.PassiveUsed <= half.Soil.PassiveAvailable + 1e-12);
        Close(.5, half.Soil.PassiveFraction, "η");
        Assert.IsTrue(half.Horizontal < none.Horizontal);
        // Kp = 1/Ka on σ'v of the valley bands, cut at the top of the slab.
        var band = RetainingWallAnalysis.ValleyBands(Wall(valley: new WallValley(1000, front, true, .5)));
        Assert.AreEqual(2, band.Count); Close(400, band[0].Bottom, "the layer ends at the top of the slab", 1e-12, 1e-9); Close(18 * KN3 * 600, band[0].SigmaBottom, "σ'v", 1e-12);
        // Never in the seismic combinations.
        var s = Wall(valley: new WallValley(1000, front, true, 1), seismic: WallSeismic.Assigned(WallSeismicMethod.MononobeOkabe, .1, 0));
        Assert.AreEqual(0, One(s, Row(WallLimitState.Seismic, kh: .1)).Soil.PassiveAvailable);
        // The valley soil above the toe adds weight; q' gives Nq in the bearing.
        Assert.IsTrue(none.Soil.ValleyWeight > 0 && none.Soil.Overburden > 0);
        Close(none.Soil.ValleyWeight, RetainingWallAnalysis.FrontSoilMass(Wall().Geometry, band).Weight, "valley weight", 1e-12);
        // A huge passive is used only up to the driving thrust.
        var big = One(Wall(valley: new WallValley(3000, Column(3, (Fill(45), 10)), true, 1)), Row());
        Assert.IsTrue(big.Soil.PassiveScale < 1); Close(0, big.Horizontal - big.Horizontal * 0, "finite");
    }

    [TestMethod]
    public void InterfacesFollowTheirRule()
    {
        Close(20 * Deg, new WallInterface(WallFriction.Assigned, 20 * Deg).Design(), "assigned", 1e-15);
        Close(Math.Atan(Math.Tan(20 * Deg) / 1.25), new WallInterface(WallFriction.Assigned, 20 * Deg).Design(1.25), "γM", 1e-15);
        Close(28 * Deg, new WallInterface(WallFriction.CastInPlace, 0, 28 * Deg).Design(), "cast in place", 1e-15);
        Close(2.0 / 3 * 28 * Deg, new WallInterface(WallFriction.PrecastSmooth, 0, 28 * Deg).Design(), "precast", 1e-15);
        Assert.AreEqual(0, new WallInterface(WallFriction.Smooth).Design());
        Throws(() => new WallInterface(WallFriction.CastInPlace), "φcv missing");
        Throws(() => new WallInterface(WallFriction.Assigned, -1 * Deg), "δ negative");
        Throws(() => RetainingWallAnalysis.Calculate(Wall(back: new WallInterface(WallFriction.CastInPlace, 0, 31 * Deg)), new[] { Row() }), "φcv > φ");
        Throws(() => RetainingWallAnalysis.Calculate(Wall(back: new WallInterface(WallFriction.Assigned, 31 * Deg)), new[] { Row() }), "δ > φ");
        // With a heel the equilibrium plane has δ = 0; without heel the friction of the back acts on it and adds tan δ to the normal force.
        var heel = One(Wall(back: new WallInterface(WallFriction.Assigned, 20 * Deg)), Row());
        var noHeel = One(Wall(toe: 2.7, heel: 0, back: new WallInterface(WallFriction.Assigned, 20 * Deg)), Row());
        Assert.AreEqual(0, heel.Soil.EquilibriumPlaneFriction); Close(20 * Deg, noHeel.Soil.EquilibriumPlaneFriction, "δ on the plane", 1e-15);
        Close(EarthPressure.ActiveHorizontal(30 * Deg, 20 * Deg), noHeel.PressureDetails[0].K, "Coulomb", 1e-14);
        Assert.IsTrue(heel.StemPressureDetails[0].K < heel.PressureDetails[0].K, "the stem has the friction of the back");
        // Rough base: δb ≥ φd/2, else no static bearing.
        var smooth = One(Wall(deltaB: 16.9), Row()); Assert.IsFalse(smooth.Soil.RoughBase); Assert.IsNull(smooth.BearingResistance);
        Assert.IsTrue(One(Wall(deltaB: 17), Row()).Soil.RoughBase);
    }

    [TestMethod]
    public void CombinationsFollowTheNtcPreset()
    {
        // One variable action: 2 SLS (characteristic, frequent) + quasi permanent + 8 ULS (wall, fill, presence of q).
        var rows = WallCombinations.Generate(Wall());
        Assert.AreEqual(11, rows.Count); Assert.AreEqual(8, rows.Count(r => r.State == WallLimitState.Ultimate));
        Assert.IsTrue(rows.Where(r => r.State == WallLimitState.Ultimate).All(r => r.SlidingFactor == 1.1 && r.OverturningFactor == 1.15 && r.BearingFactor == 1.4 && r.Approach == "A1+M1+R3"));
        Assert.AreEqual("SLE 1", rows[0].Name); Assert.AreEqual("Quasi permanente 3", rows[2].Name); Close(.3, rows[2].Coefficients["q"], "ψ2");
        // Two variable actions: the leading one 1.5, the other 1.5 ψ0; 20 ULS.
        var two = WallCombinations.Generate(Wall(actions: new[] { Surcharge(10, "q", psi0: .6), Surcharge(20, "q2", psi0: .8) }));
        Assert.AreEqual(20, two.Count(r => r.State == WallLimitState.Ultimate));
        Assert.IsTrue(two.Any(r => r.Coefficients["q"] == 1.5 && Math.Abs(r.Coefficients["q2"] - 1.2) < 1e-12));
        Assert.IsTrue(two.Any(r => Math.Abs(r.Coefficients["q"] - .9) < 1e-12 && r.Coefficients["q2"] == 1.5));
        // Soil in front doubles the ULS (valley 1/1.3); water adds a factor.
        Assert.AreEqual(16, WallCombinations.Generate(Wall(valley: new WallValley(1000, Column(1, (Fill(), 5))))).Count(r => r.State == WallLimitState.Ultimate));
        Assert.AreEqual(16, WallCombinations.Generate(Wall(water: new WallWater(1000))).Count(r => r.State == WallLimitState.Ultimate));
        // Permanent G2: 0/1.5; correlated group: one key.
        var g2 = WallCombinations.Generate(Wall(actions: new[] { Surcharge(10), Surcharge(5, "g", WallActionCategory.G2) }));
        Assert.IsTrue(g2.Where(r => r.State == WallLimitState.Ultimate).Select(r => r.Coefficients["g"]).Distinct().OrderBy(v => v).SequenceEqual(new[] { 0, 1.5 }));
        var grouped = WallCombinations.Generate(Wall(actions: new[] { Surcharge(10, "a", group: "G"), Surcharge(5, "b", group: "G") }));
        Assert.AreEqual(8, grouped.Count(r => r.State == WallLimitState.Ultimate));
        // Accidental: separate, ψ2 of the variables, γR = 1.
        var impact = new WallAction("u", "Urto", WallActionType.Impact, WallActionCategory.A, 40, 2000);
        var acc = WallCombinations.Generate(Wall(actions: new[] { Surcharge(10), impact }));
        var e = acc.Single(r => r.State == WallLimitState.Exceptional); Assert.AreEqual(1, e.Coefficients["u"]); Close(.3, e.Coefficients["q"], "ψ2");
        Assert.IsTrue(acc.Where(r => r.State != WallLimitState.Exceptional).All(r => r.Coefficients["u"] == 0));
        var withImpact = RetainingWallAnalysis.Calculate(Wall(actions: new[] { Surcharge(10), impact }), acc);
        var qp = withImpact.Cases.Single(c => c.Combination.State == WallLimitState.QuasiPermanent); var ec = withImpact.Cases.Single(c => c.Combination.State == WallLimitState.Exceptional);
        Close(40, ec.Horizontal - qp.Horizontal, "impact force", 1e-12); Close(40 * 2000, ec.Overturning - qp.Overturning, "impact arm", 1e-12);
        // Assigned seismic: both signs of kv, quasi permanent actions; site: general and overturning.
        var manual = WallCombinations.Generate(Wall(seismic: WallSeismic.Assigned(WallSeismicMethod.MononobeOkabe, .1, .05)));
        Assert.AreEqual(2, manual.Count(r => r.State == WallLimitState.Seismic)); Assert.IsTrue(manual.Any(r => r.Name.StartsWith("Sisma kv−")));
        var site = NtcSiteAmplification.Create(.15, (NtcSoilCategory.C, 2.5), null, (NtcTopography.Flat, 0, 0, 0), null);
        var slv = WallCombinations.Generate(Wall(seismic: WallSeismic.FromSite(WallSeismicMethod.MononobeOkabe, site)));
        Assert.AreEqual(4, slv.Count(r => r.State == WallLimitState.Seismic));
        var over = slv.First(r => r.Purpose == WallSeismicPurpose.Overturning); Close(.57 * site.AmaxG, over.Kh, "β overturning", 1e-14);
        Assert.AreEqual(1.2, over.BearingFactor); Assert.AreEqual(1, over.SlidingFactor);
        // Overturning combinations give only the overturning check, general ones no overturning check.
        var checks = RetainingWallAnalysis.Calculate(Wall(seismic: WallSeismic.FromSite(WallSeismicMethod.MononobeOkabe, site)), slv).Checks;
        Assert.IsTrue(checks.Where(c => c.Combination.Contains("Ribaltamento")).All(c => c.Kind == WallCheckKind.Overturning));
        Assert.IsFalse(checks.Any(c => c.Combination.Contains("Generale") && c.Kind == WallCheckKind.Overturning));
        // Site coefficients beyond the field of the engine are not truncated.
        var strong = NtcSiteAmplification.Create(.5, (NtcSoilCategory.B, 2.5), null, (NtcTopography.Flat, 0, 0, 0), null);
        Throws(() => WallCombinations.Generate(Wall(seismic: WallSeismic.FromSite(WallSeismicMethod.Wood, strong))), "kh > 0.4");
        // More than 12 factors: refused.
        var many = Enumerable.Range(0, 11).Select(i => Surcharge(1, "q" + i)).ToArray();
        Throws(() => WallCombinations.Generate(Wall(actions: many)), "too many");
    }

    [TestMethod]
    public void SiteAmplificationFollowsNtcTables()
    {
        Assert.AreEqual(1, NtcSiteAmplification.Stratigraphic(NtcSoilCategory.A, .3, 0).Ss);
        Close(1.4 - .4 * 2.5 * .2, NtcSiteAmplification.Stratigraphic(NtcSoilCategory.B, .2, 2.5).Ss, "B", 1e-15);
        Assert.AreEqual(1.2, NtcSiteAmplification.Stratigraphic(NtcSoilCategory.B, .05, 2.5).Ss, "B upper bound");
        Assert.AreEqual(1, NtcSiteAmplification.Stratigraphic(NtcSoilCategory.B, .6, 3).Ss, "B lower bound");
        Assert.AreEqual(1.5, NtcSiteAmplification.Stratigraphic(NtcSoilCategory.C, .05, 2.5).Ss); Assert.AreEqual(1.8, NtcSiteAmplification.Stratigraphic(NtcSoilCategory.D, .05, 2.5).Ss);
        Assert.AreEqual(.9, NtcSiteAmplification.Stratigraphic(NtcSoilCategory.D, .6, 3).Ss); Assert.AreEqual(1.6, NtcSiteAmplification.Stratigraphic(NtcSoilCategory.E, .05, 2.5).Ss);
        Throws(() => NtcSiteAmplification.Stratigraphic(NtcSoilCategory.B, .2, 2), "F0 < 2.2");
        Throws(() => NtcSiteAmplification.Stratigraphic(NtcSoilCategory.B, 20, 2.5), "ag in percent");
        Assert.AreEqual(1, NtcSiteAmplification.Topographic(NtcTopography.Flat).St);
        Assert.AreEqual(1, NtcSiteAmplification.Topographic(NtcTopography.Slope, 15 * Deg, 50000, 20000).St, "≤ 15°");
        Close(1 + .2 * 25000 / 50000, NtcSiteAmplification.Topographic(NtcTopography.Slope, 20 * Deg, 50000, 25000).St, "T2", 1e-15);
        Close(1 + .2 * 45000 / 60000, NtcSiteAmplification.Topographic(NtcTopography.Ridge, 25 * Deg, 60000, 45000).St, "T3", 1e-15);
        Close(1 + .4 * 45000 / 60000, NtcSiteAmplification.Topographic(NtcTopography.Ridge, 35 * Deg, 60000, 45000).St, "T4", 1e-15);
        Assert.AreEqual(1, NtcSiteAmplification.Topographic(NtcTopography.Slope, 20 * Deg, 25000, 20000).St, "relief ≤ 30 m");
        Throws(() => NtcSiteAmplification.Topographic(NtcTopography.Ridge, 35 * Deg, 60000, 70000), "site above the relief");
        var site = NtcSiteAmplification.Create(.2, null, 1.35, null, 1.1);
        Close(1.35 * 1.1 * .2, site.AmaxG, "assigned", 1e-15);
        Throws(() => NtcSiteAmplification.Create(.2, null, 6, null, 1), "Ss > 5"); Throws(() => NtcSiteAmplification.Create(.2, null, 1.2, null, .9), "St < 1");
        Throws(() => NtcSiteAmplification.Create(.2, null, null, null, 1), "no category");
        var wall = WallSeismic.FromSite(WallSeismicMethod.MononobeOkabe, site);
        Close(.38 * site.AmaxG, wall.Kh, "kh", 1e-15); Close(.19 * site.AmaxG, wall.Kv, "kv", 1e-15); Close(.57, wall.BetaOverturning, "βr", 1e-15);
        var wood = WallSeismic.FromSite(WallSeismicMethod.Wood, site); Assert.AreEqual(1, wood.Beta); Assert.AreEqual(1, wood.BetaOverturning);
        Throws(() => WallSeismic.Assigned(WallSeismicMethod.Wood, .5, 0), "kh > 0.4"); Throws(() => WallSeismic.Assigned(WallSeismicMethod.Wood, .1, .3), "kv > 0.2");
    }

    [TestMethod]
    public void BearingAndContactBoundaries()
    {
        // A horizontal force at the top until the resultant leaves the base: no contact, no bearing, loss of equilibrium in the check.
        var force = new WallAction("h", "H", WallActionType.HorizontalForce, WallActionCategory.Q, 1000, 3400);
        var r = RetainingWallAnalysis.Calculate(Wall(actions: new[] { Surcharge(10), force }), new[] { Row(WallLimitState.Ultimate, rs: 1.1, ro: 1.15, rb: 1.4, f: new Dictionary<string, double> { ["q"] = 1, ["h"] = 1 }) });
        var c = r.Cases[0]; Assert.IsFalse(c.Contact.Valid); Assert.IsNull(c.BearingResistance);
        var contact = r.Checks.Single(k => k.Kind == WallCheckKind.Contact); Assert.AreEqual(WallCheckStatus.LossOfEquilibrium, contact.Status); Assert.IsNull(contact.Ratio);
        var bearing = r.Checks.Single(k => k.Kind == WallCheckKind.Bearing); Assert.AreEqual(WallCheckStatus.Unavailable, bearing.Status);
        StringAssert.StartsWith(bearing.Message, "Fuori campo: e > B/3");
        Assert.IsTrue(r.Warnings.Any(w => w.StartsWith("Perdita di equilibrio")));
        // Zero resistance: satisfied only with zero demand.
        Assert.AreEqual(WallCheckStatus.ZeroResistance, WallCheck.Of(WallCheckKind.Sliding, "x", 10, 0).Status);
        Assert.AreEqual(WallCheckStatus.Satisfied, WallCheck.Of(WallCheckKind.Sliding, "x", 0, 0).Status);
        Assert.AreEqual(WallCheckStatus.NotSatisfied, WallCheck.Of(WallCheckKind.Sliding, "x", 11, 10).Status);
        // The service combinations give no geotechnical checks.
        Assert.AreEqual(0, RetainingWallAnalysis.Calculate(Wall(), new[] { Row() }).Checks.Count);
    }

    [TestMethod]
    public void StemCutsFollowTheLoadsAndTheReinforcement()
    {
        var actions = new[] { Surcharge(10), new WallAction("h", "H", WallActionType.HorizontalForce, WallActionCategory.Q, 10, 1850), new WallAction("m", "M", WallActionType.Moment, WallActionCategory.Q, 8000, 3400),
            new WallAction("p", "P", WallActionType.LateralPressure, WallActionCategory.Q, 4 * KPa, 2850, 850), new WallAction("n", "N", WallActionType.VerticalForce, WallActionCategory.Q, 50, 3400, x: 950) };
        var input = new WallInput(WallFamily.Cantilever, new WallGeometry(3000, 400, 300, 400, 800, 1900), 25 * KN3, Column(3.4, (Fill(), 10)), new Soil("F", 19 * KN3, 21 * KN3, 34 * Deg, 0, "t"),
            WallValley.Free(Column(0, (Fill(), 10))), new WallInterface(WallFriction.Assigned), new WallInterface(WallFriction.Assigned, 26 * Deg), null, actions, null, 1200);
        var all = new Dictionary<string, double> { ["q"] = 1, ["h"] = 1, ["m"] = 1, ["p"] = 1, ["n"] = 1 };
        var none = new Dictionary<string, double> { ["q"] = 1, ["h"] = 0, ["m"] = 0, ["p"] = 0, ["n"] = 0 };
        var withLoads = One(input, Row(f: all)); var without = One(input, Row(f: none));
        var cuts = withLoads.Sections.Where(s => s.Member == WallMember.Stem).Select(s => s.Position).ToArray();
        // The cut at the force (1.55 m below the top) and 1e-4 mm above it; the change of the reinforcement 1.8 m below the top.
        CollectionAssert.Contains(cuts, 1550.0); Assert.IsTrue(cuts.Any(z => Math.Abs(z - (1550 - 1e-4)) < 1e-9)); CollectionAssert.Contains(cuts, 1800.0);
        Assert.AreEqual(cuts.Length, cuts.Distinct().Count());
        // At the root: H 10 at 1.45 m, M 8 kNm, pressure 4 kPa on 2 m with resultant at 0.85 + 1 m, vertical force 50 with arm to the root of the stem.
        var a = withLoads.Sections.Single(s => s.Member == WallMember.Stem && s.Position == 3000); var b = without.Sections.Single(s => s.Member == WallMember.Stem && s.Position == 3000);
        double root = 800 + 400 - 200;
        Close(10 + 4e-3 * 2000, a.V - b.V, "shear of the loads", 1e-12);
        Close(10 * 1450 + 8000 + 4e-3 * 2000 * 1450 - 50 * (950 - root), a.M - b.M, "moment of the loads", 1e-12);
        Close(50, a.N - b.N, "N of the force", 1e-12);
        // Just above the force the horizontal force is not yet there.
        var at = withLoads.Sections.Single(s => s.Member == WallMember.Stem && s.Position == 1550); var above = withLoads.Sections.Single(s => s.Member == WallMember.Stem && Math.Abs(s.Position - (1550 - 1e-4)) < 1e-9);
        Close(10, at.V - above.V, "jump of the shear", 1e-6);
    }

    [TestMethod]
    public void InvalidInputsAreRejected()
    {
        Throws(() => new WallGeometry(3000, 300, 400, 400, 800, 1900), "stem reversed");
        Throws(() => new WallGeometry(0, 300, 300, 400, 800, 1900), "no height");
        Throws(() => new WallGeometry(3000, 300, 300, 400, -1, 1900), "negative toe");
        Throws(() => new WallWater(-1), "negative water");
        Throws(() => new WallValley(-1, Column(0, (Fill(), 5))), "negative valley");
        Throws(() => new WallValley(0, Column(0, (Fill(), 5)), true, 1.2), "mobilisation > 1");
        Throws(() => new WallValley(0, new SoilProfile("w", new[] { new SoilLayer(Fill(), 0, -5000) }, "t", -1000)), "water in the valley column");
        void Calc(WallInput i, string what) => Throws(() => RetainingWallAnalysis.Calculate(i, new[] { Row() }), what);
        Calc(Wall(fill: Column(3.4, (Fill(), 2))), "short fill");
        Calc(Wall(fill: Column(3.0, (Fill(), 10))), "fill below the top");
        Calc(Wall(fill: Column(3.4, (Fill(50), 10))), "φ 50");
        Calc(Wall(fill: Column(3.4, (Fill(30, 8, 9), 10))), "γ < 10");
        Calc(Wall(fill: Column(3.4, (new Soil("c", 18 * KN3, 20 * KN3, 30 * Deg, 5 * KPa, "t"), 10))), "c' > 0");
        Calc(Wall(water: new WallWater(5000)), "water below the base");
        Calc(Wall(water: new WallWater(1000, 600)), "front head above max(t, Dv)");
        Calc(Wall(valley: new WallValley(4000, Column(4, (Fill(), 10)))), "valley above the top");
        Calc(Wall(valley: new WallValley(1000, Column(1, (Fill(), .5)))), "short valley column");
        Calc(Wall(gamma: 0), "zero unit weight");
        Calc(Wall(actions: new[] { new WallAction("h", "H", WallActionType.HorizontalForce, WallActionCategory.Q, 10, 9000) }), "action above the top");
        Calc(Wall(actions: new[] { new WallAction("h", "H", WallActionType.HorizontalForce, WallActionCategory.Q, 10, 100) }), "action in the slab");
        Calc(Wall(actions: new[] { new WallAction("n", "N", WallActionType.VerticalForce, WallActionCategory.Q, 10, 3400, x: 3000) }), "vertical force outside the stem");
        Calc(Wall(actions: new[] { new WallAction("p", "P", WallActionType.LateralPressure, WallActionCategory.Q, .01, 2000, 2500) }), "pressure z0 > z");
        Calc(Wall(actions: new[] { new WallAction("u", "U", WallActionType.Impact, WallActionCategory.Q, 10, 2000) }), "impact not accidental");
        Calc(Wall(actions: new[] { new WallAction("q", "q", WallActionType.UniformSurcharge, WallActionCategory.Q, .01, psi0: .3, psi1: .5, psi2: .2) }), "ψ1 > ψ0");
        Calc(Wall(actions: new[] { Surcharge(10, "a", group: "G"), Surcharge(10, "b", WallActionCategory.G2, group: "G") }), "group of different natures");
        Calc(Wall(actions: new[] { Surcharge(10), Surcharge(10) }), "repeated id");
        Calc(Wall(actions: new[] { Surcharge(-10) }), "negative action");
        Calc(Wall(water: new WallWater(1000), seismic: WallSeismic.Assigned(WallSeismicMethod.MononobeOkabe, .1, 0)), "seismic with water");
        Calc(Wall(fill: Column(3.4, (Fill(30), 1), (Fill(34), 9)), seismic: WallSeismic.Assigned(WallSeismicMethod.MononobeOkabe, .1, 0)), "seismic with layers");
        Calc(Wall(fill: Column(3.4, (Fill(12), 10)), seismic: WallSeismic.Assigned(WallSeismicMethod.MononobeOkabe, .3, .05)), "Mononobe-Okabe beyond φ");
        Throws(() => RetainingWallAnalysis.Calculate(Wall(), new[] { Row(WallLimitState.Seismic, kh: .1) }), "seismic row without seismic options");
        Throws(() => RetainingWallAnalysis.Calculate(Wall(), new[] { Row(), Row() }), "repeated name");
        Throws(() => RetainingWallAnalysis.Calculate(Wall(), new WallCombination[0]), "no combination");
        Throws(() => RetainingWallAnalysis.Calculate(Wall(), new[] { Row(f: new Dictionary<string, double> { ["x"] = 1 }) }), "unknown action");
        Throws(() => RetainingWallAnalysis.Calculate(Wall(actions: new[] { Surcharge(10), new WallAction("u", "U", WallActionType.Impact, WallActionCategory.A, 10, 2000) }),
            new[] { Row(f: new Dictionary<string, double> { ["q"] = 1, ["u"] = 1 }) }), "accidental in an ordinary combination");
        Throws(() => Row(kh: .1), "kh in a static combination"); Throws(() => Row(WallLimitState.Seismic, kh: .5), "kh > 0.4"); Throws(() => Row(WallLimitState.Seismic, kv: .3), "kv > 0.2");
        Throws(() => Row(mphi: 0), "γM < 0.1"); Throws(() => Row(wall: 6), "factor > 5");
        var token = new CancellationTokenSource(); token.Cancel();
        Assert.ThrowsException<OperationCanceledException>(() => RetainingWallAnalysis.Calculate(Wall(), new[] { Row() }, token.Token));
    }

    [TestMethod]
    public void CurvatureIntegrationIsTheCantileverFormula()
    {
        // Constant curvature κ on H: u = κ H²/2, θ = κ H.
        var shape = WallDisplacementPoint.Integrate(new[] { new WallCurvaturePoint(0, 1e-7), new WallCurvaturePoint(3000, 1e-7) }, 3000);
        Close(1e-7 * 9e6 / 2, shape[^1].Displacement, "u", 1e-12); Close(3e-4, shape[^1].Rotation, "θ", 1e-12);
        // Linear from κ0 at the base to zero at the top: u = κ0 H²/3.
        var linear = WallDisplacementPoint.Integrate(new[] { new WallCurvaturePoint(0, 2e-7) }.Concat(new[] { new WallCurvaturePoint(1500, 1e-7) }), 3000);
        Close(2e-7 * 9e6 / 3, linear[^1].Displacement, "u linear", 1e-12);
        Throws(() => WallDisplacementPoint.Integrate(new[] { new WallCurvaturePoint(10, 1e-7), new WallCurvaturePoint(3000, 0) }, 3000), "no curvature at the base");
        Throws(() => WallDisplacementPoint.Integrate(new[] { new WallCurvaturePoint(0, 1e-7) }, 3000), "one point");
    }
}
