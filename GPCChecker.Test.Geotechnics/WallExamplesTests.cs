using GPC.Checkers.Geotechnics.Seismic;
using GPC.Checkers.Geotechnics.Walls;
using GPC.Model.Geotechnics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static GeotechnicsTests.GeotechnicsFixture;
using static GeotechnicsTests.WallEdgeCaseTests;

namespace GeotechnicsTests;

/// <summary>Five worked examples of retaining walls (W1), with the governing values checked by hand and printed for the report.</summary>
[TestClass]
public class WallExamplesTests
{
    public TestContext TestContext { get; set; } = null!;

    static WallInput Cantilever(WallWater? water = null, WallValley? valley = null, WallSeismic? seismic = null, IEnumerable<WallAction>? actions = null)
        => new(WallFamily.Cantilever, new WallGeometry(4000, 450, 300, 500, 1000, 2200), 25 * KN3, Column(4.5, (Fill(32, 19, 20), 12)),
            new Soil("Foundation", 19 * KN3, 21 * KN3, 34 * Deg, 0, "example"), valley ?? WallValley.Free(Column(0, (Fill(32, 19, 20), 10))), new WallInterface(WallFriction.Assigned),
            new WallInterface(WallFriction.Assigned, 24 * Deg), water, actions ?? new[] { Surcharge(10) }, seismic);

    static WallCheck Worst(WallResult r, WallCheckKind kind) => r.Checks.Where(c => c.Kind == kind && c.Ratio.HasValue).OrderByDescending(c => c.Ratio).First();

    [TestMethod]
    public void Example1_CantileverDryNtcApproach2()
    {
        // H = 4 m, s 0.45/0.30 m, t 0.5 m, toe 1.0 m, heel 2.2 m; fill φ 32°, γ 19; q = 10 kPa; foundation φ 34°, δb = 24°. A1+M1+R3.
        var input = Cantilever(); var rows = WallCombinations.Generate(input); var r = RetainingWallAnalysis.Calculate(input, rows);
        Assert.AreEqual(11, r.Cases.Count);
        var sliding = Worst(r, WallCheckKind.Sliding);
        // The governing sliding: wall 1, fill 1.3, q 1.5: H = 1.3 (½ γ Ka Ht²) + 1.5 q Ka Ht; V without the favourable surcharge would govern only with q 0.
        var c = r.Cases.Single(x => x.Combination.Name == sliding.Combination);
        double ka = EarthPressure.RankineActive(32 * Deg), ht = 4500, g = 19 * KN3;
        double h = c.Combination.Soil * .5 * g * ka * ht * ht + c.Combination.Coefficients["q"] * 10 * KPa * ka * ht;
        Close(h, c.Horizontal, "H", 1e-12);
        double stem = (450 + 300) * 4000 / 2.0 * 25 * KN3, slab = 3650 * 500 * 25 * KN3, fill = 2200 * 4000 * g;
        double v = c.Combination.Wall * (stem + slab) + c.Combination.Soil * fill + c.Combination.Coefficients["q"] * 10 * KPa * 2200;
        Close(v, c.Vertical, "V", 1e-12);
        Close(v * Math.Tan(24 * Deg) / 1.1, c.SlidingResistance, "Rd sliding", 1e-12);
        Close(h / (v * Math.Tan(24 * Deg) / 1.1), sliding.Ratio!.Value, "ratio", 1e-12);
        var overturning = Worst(r, WallCheckKind.Overturning); var bearing = Worst(r, WallCheckKind.Bearing);
        Assert.IsTrue(r.Checks.All(x => x.Status is WallCheckStatus.Satisfied or WallCheckStatus.ContactInCompression));
        TestContext.WriteLine($"Es.1: {sliding.Combination} H={c.Horizontal:0.00} N/mm V={c.Vertical:0.00} N/mm, η scorrimento {sliding.Ratio:0.000}; ribaltamento {overturning.Ratio:0.000}; portanza {bearing.Ratio:0.000}");
        Close(.72294, sliding.Ratio!.Value, "reported", 1e-4);
    }

    [TestMethod]
    public void Example2_WaterTableBehindTheWall()
    {
        // Same wall, water 2 m below the top of the fill, no water in front: water thrust ½ γw 2.5², triangular uplift, more ULS (water 1/1.3).
        var dry = RetainingWallAnalysis.Calculate(Cantilever(), WallCombinations.Generate(Cantilever()));
        var input = Cantilever(new WallWater(2000)); var rows = WallCombinations.Generate(input); var r = RetainingWallAnalysis.Calculate(input, rows);
        Assert.AreEqual(16 + 3, rows.Count);
        var qp = r.Cases.Single(x => x.Combination.State == WallLimitState.QuasiPermanent);
        double gw = SoilUnits.WaterUnitWeight;
        Close(.5 * gw * 2500 * 3650, qp.Uplift, "uplift", 1e-12);
        var dryQp = dry.Cases.Single(x => x.Combination.State == WallLimitState.QuasiPermanent);
        double ka = EarthPressure.RankineActive(32 * Deg);
        // Below the table: γ' instead of γ in σ'v (−(γ − γ') Ka z² / 2 contribution) and the water ½ γw z².
        double delta = .5 * gw * 2500 * 2500 - (19 - 20 + 9.81) * KN3 * ka * 2500 * 2500 / 2;
        Close(dryQp.Horizontal + delta, qp.Horizontal, "water thrust", 1e-9);
        var sliding = Worst(r, WallCheckKind.Sliding);
        TestContext.WriteLine($"Es.2: U={qp.Uplift:0.00} N/mm, ΔH acqua={qp.Horizontal - dryQp.Horizontal:0.00} N/mm; η scorrimento {sliding.Ratio:0.000} ({sliding.Combination}) contro {Worst(dry, WallCheckKind.Sliding).Ratio:0.000} asciutto");
        Assert.IsTrue(sliding.Ratio > Worst(dry, WallCheckKind.Sliding).Ratio);
        Close(1.19736, sliding.Ratio!.Value, "reported: not satisfied", 1e-4); Assert.AreEqual(WallCheckStatus.NotSatisfied, sliding.Status);
    }

    [TestMethod]
    public void Example3_PassiveInFront()
    {
        // Soil in front 1.2 m above the base, passive Rankine mobilised at 50%, valley factor 1/1.3 doubles the ULS.
        var front = Column(1.2, (Fill(30, 18, 20), 6));
        var input = Cantilever(valley: new WallValley(1200, front, true, .5)); var rows = WallCombinations.Generate(input); var r = RetainingWallAnalysis.Calculate(input, rows);
        Assert.AreEqual(16 + 3, rows.Count);
        var sle = r.Cases[0];
        // Available passive: η Kp ½ γ Dv² on the bands of the valley (above and beside the slab).
        double kp = EarthPressure.RankinePassive(30 * Deg);
        Close(.5 * kp * .5 * 18 * KN3 * 1200 * 1200, sle.Soil.PassiveAvailable, "passive", 1e-12);
        Assert.AreEqual(1, sle.Soil.PassiveScale);
        var sliding = Worst(r, WallCheckKind.Sliding);
        var free = Worst(RetainingWallAnalysis.Calculate(Cantilever(), WallCombinations.Generate(Cantilever())), WallCheckKind.Sliding);
        TestContext.WriteLine($"Es.3: passiva disponibile {sle.Soil.PassiveAvailable:0.00} N/mm, peso a valle {sle.Soil.ValleyWeight:0.00} N/mm, q'={sle.Soil.Overburden * 1000:0.00} kPa; η scorrimento {sliding.Ratio:0.000} contro {free.Ratio:0.000}");
        Assert.IsTrue(sliding.Ratio < free.Ratio);
        Close(.55757, sliding.Ratio!.Value, "reported", 1e-4);
    }

    [TestMethod]
    public void Example4_SeismicFromTheSite()
    {
        // NTC: category C, ag = 0.15 g, F0 = 2.5: Ss = 1.7 − 0.6·2.5·0.15 = 1.475, St = 1, amax = 0.22125 g; βm = 0.38: kh = 0.0841, kv = ±0.0420.
        var site = NtcSiteAmplification.Create(.15, (NtcSoilCategory.C, 2.5), null, (NtcTopography.Flat, 0, 0, 0), null);
        Close(1.475, site.Ss, "Ss", 1e-14); Close(.22125, site.AmaxG, "amax", 1e-14);
        var seismic = WallSeismic.FromSite(WallSeismicMethod.MononobeOkabe, site);
        Close(.38 * .22125, seismic.Kh, "kh", 1e-14); Close(.57 * .22125, seismic.KhOverturning, "kh overturning", 1e-14);
        var input = Cantilever(seismic: seismic); var rows = WallCombinations.Generate(input); var r = RetainingWallAnalysis.Calculate(input, rows);
        var general = r.Cases.Where(x => x.Combination.Purpose == WallSeismicPurpose.General).ToArray();
        Assert.AreEqual(2, general.Length);
        foreach (var c in general)
        {
            double theta = Math.Atan(c.Combination.Kh / (1 - c.Combination.Kv));
            Assert.IsTrue(c.PressureDetails[0].Ke > c.PressureDetails[0].K);
            Close(EarthPressure.ActiveHorizontal(32 * Deg, 0, c.Combination.Kh, c.Combination.Kv), c.PressureDetails[0].Ke, "Ke", 1e-14);
            Assert.IsTrue(theta < 32 * Deg);
            Assert.IsNotNull(c.SeismicBearing, "Annex F with the ground acceleration amax/g");
            Close(.22125, c.SeismicBearing!.SoilInertia / (1.15 / Math.Tan(34 * Deg)), "F̄ = γRd amax/g / tan φ", 1e-12);
        }
        var worst = Worst(r, WallCheckKind.Bearing);
        TestContext.WriteLine($"Es.4: Ss={site.Ss:0.000} amax/g={site.AmaxG:0.0000} kh={seismic.Kh:0.0000} kv=±{seismic.Kv:0.0000}; Ke/K={general[0].PressureDetails[0].Ke / general[0].PressureDetails[0].K:0.000}; " +
            $"portanza sismica η={worst.Ratio:0.000} ({worst.Combination}); ribaltamento SLV η={Worst(r, WallCheckKind.Overturning).Ratio:0.000}");
        Close(.79114, worst.Ratio!.Value, "reported", 1e-4);
    }

    [TestMethod]
    public void Example5_GravityWallAndImpact()
    {
        // Gravity wall: H 3 m, trapezoid 2.20/0.55 m, slab 0.45 m, toe and heel 0.25 m, γ 24 kN/m³; impact 40 kN/m at 2 m above the base (A).
        var impact = new WallAction("u", "Urto", WallActionType.Impact, WallActionCategory.A, 40, 2000);
        var input = new WallInput(WallFamily.Gravity, new WallGeometry(3000, 2200, 550, 450, 250, 250), 24 * KN3, Column(3.45, (Fill(30), 10)), new Soil("F", 19 * KN3, 21 * KN3, 34 * Deg, 0, "e"),
            WallValley.Free(Column(0, (Fill(30), 10))), new WallInterface(WallFriction.Assigned), new WallInterface(WallFriction.Assigned, 26 * Deg), null, new[] { Surcharge(10), impact });
        var rows = WallCombinations.Generate(input); var r = RetainingWallAnalysis.Calculate(input, rows);
        var accident = r.Cases.Single(c => c.Combination.State == WallLimitState.Exceptional); var qp = r.Cases.Single(c => c.Combination.State == WallLimitState.QuasiPermanent);
        Close(40, accident.Horizontal - qp.Horizontal, "impact", 1e-12); Close(40 * 2000, accident.Overturning - qp.Overturning, "arm", 1e-12);
        // Stem weight (2.2 + 0.55)/2 · 3 · 24 and its centroid from the back.
        double stem = (2200 + 550) * 3000 / 2.0 * 24 * KN3;
        Close(stem + 2700 * 450 * 24 * KN3 + 250 * 3000 * 18 * KN3 + 250 * 3 * KPa, qp.Vertical, "V", 1e-12);
        var check = r.Checks.Single(c => c.Combination == accident.Combination.Name && c.Kind == WallCheckKind.Overturning);
        Close(accident.Overturning / accident.Stabilizing, check.Ratio!.Value, "γR = 1 in the accidental combination", 1e-12);
        // The force that would overturn the wall about the toe in the accidental combination: the stabilising moment margin over the arm.
        double limit = (accident.Stabilizing - qp.Overturning) / 2000;
        TestContext.WriteLine($"Es.5: V={qp.Vertical:0.00} N/mm; urto: ribaltamento η={check.Ratio:0.000}, e={accident.Eccentricity:0.0} mm su B/2={r.Width / 2:0} mm; forza di ribaltamento a 2 m {limit:0.0} N/mm");
        Assert.IsTrue(accident.Contact.Valid);
        Close(.52403, check.Ratio!.Value, "reported", 1e-4);
    }
}
