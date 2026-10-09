using GPC.Checkers.Geotechnics.Foundations;
using GPC.Checkers.Geotechnics.Seismic;
using GPC.Checkers.Geotechnics.Slopes;
using GPC.Model.Geotechnics;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static GeotechnicsTests.GeotechnicsFixture;

namespace GeotechnicsTests;

/// <summary>Five worked examples of the general geotechnics (report G), each checked against an independent calculation.</summary>
[TestClass]
public class GeneralGeotechnicsExamplesTests
{
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// 1. Bishop on three slices (b = 2 m; α = −10°, 15°, 40°; W = 60, 150, 110 kN/m; u = 0, 10, 5 kPa), φ'k = 30° and c'k = 10 kPa with M2:
    /// φ'd = 24.79°, c'd = 8 kPa. The classic fixed-point iteration F = Σ[c'd b + (W − u b) tan φ'd]/mα(F) / Σ W sin α gives the same F = 1.894;
    /// with γR = 1.1, η = 0.581.
    /// </summary>
    [TestMethod]
    public void Example1_BishopOnThreeSlices()
    {
        double phi = Math.Atan(Math.Tan(30 * Deg) / 1.25), c = 10 * KPa / 1.25, b = 2 * M;
        var data = new[] { (-10.0, 60.0, 0.0), (15.0, 150.0, 10.0), (40.0, 110.0, 5.0) };
        var slices = data.Select((d, i) => new SlopeSlice(i + 1, i * b, (i + 1) * b, 0, M, d.Item1 * Deg, "Silt", d.Item2, 0, (i + .5) * b, M, 0, 0, d.Item3 * KPa, phi, c, d.Item2,
            d.Item2 * Math.Sin(d.Item1 * Deg))).ToArray();
        var r = BishopSolver.Solve(new SlipCircle(0, 0, 1, 0, 1), slices, 1.1)!;
        double f = 1;
        for (int k = 0; k < 200; k++)
            f = slices.Sum(s => (s.Cohesion * s.Width + (s.Vertical - s.PorePressure * s.Width) * Math.Tan(phi)) / (Math.Cos(s.Alpha) + Math.Sin(s.Alpha) * Math.Tan(phi) / f)) / slices.Sum(s => s.Driving);
        Close(f, r.Factor, "fixed point", 1e-8); Close(1.1 / f, r.Ratio, "η = γR/F", 1e-8);
        Close(1.8935, r.Factor, "F", 1e-4); Close(.5809, r.Ratio, "η", 1e-3); Assert.IsTrue(r.Ratio < 1, "satisfied");
        TestContext.WriteLine($"Example 1: F = {r.Factor:F4}, η = {r.Ratio:F4}, iterations {r.Iterations}");
    }

    /// <summary>
    /// 2. Slope 1:2, 6 m high, silt γ = 19 kN/m³, φ'k = 30°, c'k = 5 kPa, with the NTC 2018 combinations from Model: static A2+M2+R2 (γR = 1.1)
    /// and seismic SLV (§7.11.4: factors 1, γR = 1.2, kh = βs amax/g = 0.38·0.20 = 0.076, kv = ±kh/2).
    /// </summary>
    [TestMethod]
    public void Example2_SlopeWithTheNtcCombinations()
    {
        var silt = new Soil("Silt", 19 * KN3, 20 * KN3, 30 * Deg, 5 * KPa, "example");
        var section = new SlopeSection(new[] { new SlopePoint(-30 * M, 0), new SlopePoint(0, 0), new SlopePoint(12 * M, 6 * M), new SlopePoint(40 * M, 6 * M) },
            new[] { new SlopeLayer(silt, -20 * M) }, null, null, null, 0, 12 * M);
        var search = new SlopeSearch(-20 * M, -.5 * M, 12.5 * M, 35 * M, .5 * M, 15 * M, 7, 40, 2);
        var ntc = new StandardNTC2018Geotechnics();
        var stat = SlopeFactors.FromCombination(ntc.Combinations(GeotechnicalCheck.SlopeStability, GeotechnicalSituation.PersistentTransient).Single(), 0, 0, false);
        var seismic = ntc.Combinations(GeotechnicalCheck.SlopeStability, GeotechnicalSituation.Seismic).Single();
        double kh = .38 * .20;
        var result = SlopeStability.Calculate(section, search, new[] { stat, SlopeFactors.FromCombination(seismic, kh, kh / 2, false), SlopeFactors.FromCombination(seismic, kh, -kh / 2, false) });
        foreach (var c in result.Cases)
        {
            var critical = c.Critical!;
            Close(c.Factors.ResistanceFactor / critical.Factor, critical.Ratio, c.Factors.Name, 1e-12);
            Close(critical.Driving, critical.Resistance / critical.Factor, "moment equilibrium", 1e-8);
            Assert.IsTrue(SlopeGeometry.Admissible(section, critical.Circle));
            TestContext.WriteLine($"Example 2 {c.Factors.Name} kv={c.Factors.Kv}: F = {critical.Factor:F4}, η = {critical.Ratio:F4}, status {c.Status}, circle ({critical.Circle.X / M:F2}; {critical.Circle.Y / M:F2}) R = {critical.Circle.Radius / M:F2} m, exit {critical.Circle.Left / M:F2}, entry {critical.Circle.Right / M:F2}, {c.Tried} circles");
        }
        Assert.AreEqual(1.1, result.Cases[0].Factors.ResistanceFactor); Assert.AreEqual(1.2, result.Cases[1].Factors.ResistanceFactor);
        Assert.IsTrue(result.Cases[1].Critical!.Factor < SlopeStability.Calculate(section, search, new[] { new SlopeFactors("M1 static", 1, 1, 1, 1, 1, 1.2, 0, 0, false) }).Cases[0].Critical!.Factor,
            "the seismic forces lower F with the same strengths");
        // NotConverged here means that with twice the slices the critical circle has no solution (tension at the base of the steep exit slice), the
        // legacy status for both cases: RefinedFactor tells them apart.
        foreach (var c in result.Cases)
        {
            Assert.AreEqual(SlopeSearchStatus.NotConverged, c.Status, c.Factors.Name); Assert.IsNull(c.RefinedFactor, c.Factors.Name);
            Assert.IsNull(BishopSolver.Solve(c.Critical!.Circle, SlopeStability.Slices(section, c.Critical.Circle, c.Factors, 2 * search.Slices), c.Factors.ResistanceFactor));
            Assert.IsFalse(c.IsConclusive, "η < 1 with a failed check is not a verdict");
        }
        Close(1.5143, result.Cases[0].Critical!.Factor, "static F", 1e-3); Close(1.6043, result.Cases[1].Critical!.Factor, "SLV kv+", 1e-3);
        Close(1.5970, result.Cases[2].Critical!.Factor, "SLV kv−", 1e-3); Assert.IsTrue(result.Cases.All(c => c.Ratio < 1));
    }

    /// <summary>
    /// 3. Strip footing B = 2 m, gross pressure 170 kPa with 20 kPa removed by the excavation (net 150 kPa), sand 3 m (Eoed = 15 MPa) on clay 6 m
    /// (Eoed = 4 MPa). Under the centre σz = q/π (α + sin α), α = 2 atan(B/2z): the integral of σz/Eoed gives 71.8 mm.
    /// </summary>
    [TestMethod]
    public void Example3_SettlementOfAStripFooting()
    {
        var layers = new[] { new SettlementLayer("Sand", 3 * M, 15 * 1000 * KPa), new SettlementLayer("Clay", 6 * M, 4 * 1000 * KPa) };
        var r = FoundationSettlement.Calculate(layers, 1 * M, 0, 2 * M, 170 * KPa, 170 * KPa, 20 * KPa, 2 * M, 40);
        double Closed(double z) { double a = 2 * Math.Atan(1 * M / z); return 150 * KPa / Math.PI * (a + Math.Sin(a)); }
        double settlement = 0, top = 0;
        foreach (var l in layers)
        {
            int n = 4000; double h = l.Thickness / n, sum = 0;
            for (int i = 0; i <= n; i++) sum += (i == 0 || i == n ? 1 : i % 2 == 1 ? 4 : 2) * Closed(top + Math.Max(i * h, 1e-9));
            settlement += sum * h / 3 / l.ConstrainedModulus; top += l.Thickness;
        }
        Close(settlement, r.Settlement, "settlement", 1e-6);
        double sand = r.Slices.Where(s => s.Soil == "Sand").Sum(s => s.Settlement), clay = r.Slices.Where(s => s.Soil == "Clay").Sum(s => s.Settlement);
        Close(71.77, r.Settlement, "total settlement, mm", 1e-3); Assert.IsTrue(clay > sand, "the soft clay governs");
        TestContext.WriteLine($"Example 3: settlement {r.Settlement:F2} mm (sand {sand:F2}, clay {clay:F2}), Δσ at 9 m {r.BottomStress / KPa:F2} kPa");
    }

    /// <summary>
    /// 4. Newmark: ground acceleration 0.30 g for 0.5 s, yield acceleration 0.10 g. vmax = 0.2·9.81·0.5 = 0.981 m/s; after the pulse the block stops
    /// in vmax/(0.1 g) = 1 s; d = (A − ay) A g t0²/(2 ay) = 0.2·0.3·9.81·0.25/0.2 = 0.736 m.
    /// </summary>
    [TestMethod]
    public void Example4_NewmarkRectangularPulse()
    {
        var r = NewmarkSliding.Calculate(new[] { new AccelerogramSample(0, .3), new AccelerogramSample(.5, .3) }, .1);
        Close(735.75, r.Displacement, "d, mm", 1e-12); Close(981, r.PeakVelocity, "vmax, mm/s", 1e-12);
        Close(1.5, r.Points[r.Points.Count - 1].Time, "stop", 1e-12); Close(245.25, r.Points[1].Displacement, "during the pulse ½·0.2g·0.5²", 1e-12);
        TestContext.WriteLine($"Example 4: d = {r.Displacement:F2} mm, vmax = {r.PeakVelocity:F0} mm/s, stop at {r.Points[r.Points.Count - 1].Time:F2} s");
    }

    /// <summary>
    /// 5. EN 1998-5 Annex F: strip B = 2 m on dry sand γ = 18 kN/m³, φ'd = 32°, NEd = 500 kN/m, VEd = 60 kN/m, MEd = 40 kNm/m, kh = 0.10, kv = 0,
    /// γRd = 1. Nq = 23.18, Nγ = 27.72, Nmax = ½·18·2²·27.72 = 998 kN/m; F̄ = 0.10/tan 32° = 0.160; capacity along the load ray 515.8 kN/m,
    /// on the boundary of the domain (interaction = 1); NEd/capacity = 0.970.
    /// </summary>
    [TestMethod]
    public void Example5_SeismicBearingCapacityAnnexF()
    {
        var r = ShallowFoundationSeismic.Calculate(2 * M, 18 * KN3, 32 * Deg, 500, 60, 40 * M, .1, 0, 1, 1);
        Close(997.7, r.NMax, "Nmax", 1e-4); Close(515.8, r.Capacity, "capacity", 1e-4); Close(.9695, r.Ratio!.Value, "ratio", 1e-4); Close(.1 / Math.Tan(32 * Deg), r.SoilInertia, "F̄", 1e-12);
        double s = 1 / r.Ratio!.Value, x = r.NBar * s, f = r.SoilInertia;
        double boundary = (Math.Pow(1 - .41 * f, 1.14) * Math.Pow(2.90 * r.VBar * s, 1.14) + Math.Pow(1 - .32 * f, 1.01) * Math.Pow(2.80 * r.MBar * s, 1.01))
            / (Math.Pow(x, .92) * Math.Pow(r.VerticalLimit - x, 1.25));
        Close(1, boundary, "on the boundary", 1e-9);
        Assert.AreEqual(r.Ratio <= 1 ? SeismicBearingStatus.Satisfied : SeismicBearingStatus.NotSatisfied, r.Status);
        TestContext.WriteLine($"Example 5: Nmax = {r.NMax:F1} kN/m, F̄ = {r.SoilInertia:F4}, limit {r.VerticalLimit:F4}, capacity {r.Capacity:F1} kN/m, ratio {r.Ratio:F4}, interaction {r.Interaction:F4}, {r.Status}");
    }
}
