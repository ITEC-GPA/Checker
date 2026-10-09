using GPC.Checkers.Geotechnics.Slopes;
using GPC.Model.Geotechnics;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static GeotechnicsTests.GeotechnicsFixture;

namespace GeotechnicsTests;

/// <summary>Bishop, slope geometry and slope search: analytic cases, equilibrium, boundaries and every rejected input (mm, N/mm, MPa, rad).</summary>
[TestClass]
public class SlopeEdgeCaseTests
{
    private static SlopeSlice Slice(double alphaDeg, double phiDeg, double cKPa, double uKPa, double bM, double v, double d, int i = 1, double left = 0)
        => new(i, left, left + bM * M, 0, M, alphaDeg * Deg, "S", v, 0, left + bM * M / 2, .5 * M, 0, 0, uKPa * KPa, phiDeg * Deg, cKPa * KPa, v, d);
    private static readonly SlipCircle Unit = new(0, 10 * M, 10 * M, -3 * M, 6 * M);
    private static Soil Silt(double c = 5, double phi = 30, double? cu = null, double gamma = 19, double gammaSat = 20)
        => new("Limo", gamma * KN3, gammaSat * KN3, phi * Deg, c * KPa, "test", undrainedShearStrength: cu * KPa);
    private static SlopePoint P(double x, double y) => new(x * M, y * M);
    private static SlopeSection Slope(Soil? soil = null, IEnumerable<SlopePoint>? water = null, IEnumerable<SlopeBody>? bodies = null, IEnumerable<SlopeLoad>? loads = null)
        => new(new[] { P(-30, 0), P(0, 0), P(12, 6), P(40, 6) }, new[] { new SlopeLayer(soil ?? Silt(), -20 * M) }, water, bodies, loads, 0, 12 * M);
    private static readonly SlopeSearch Search = new(-15 * M, -.5 * M, 12.5 * M, 30 * M, .5 * M, 12 * M, 5, 30, 1);
    private static SlopeFactors Static(string name = "A2+M2+R2", bool undrained = false, IReadOnlyDictionary<string, double>? loads = null)
        => new(name, 1, 1, 1.25, 1.25, 1.4, 1.1, 0, 0, undrained, loads);

    [TestMethod]
    public void BishopAnalyticCasesAndEquilibrium()
    {
        // Frictional slice, no cohesion: F = tan φ / tan α = 1 for φ = α = 30°; γR applied once: η = 1.1.
        var friction = BishopSolver.Solve(Unit, new[] { Slice(30, 30, 0, 0, 1, 100, 50) }, 1.1)!;
        Close(1, friction.Factor, "F", 1e-8); Close(1.1, friction.Ratio, "η", 1e-8);
        // φ = 0: F = c l / D = c b / cos α / D = 10 kPa · 1 m / cos 30° / 50 kN/m.
        Close(10 / Math.Cos(Math.PI / 6) / 50, BishopSolver.Solve(Unit, new[] { Slice(30, 0, 10, 0, 1, 100, 50) }, 1)!.Factor, "φ = 0", 1e-8);
        // Equilibrium of the slice: (N' + u l) cos α + T sin α = V with T = R/F; moments: Σ R / F = D.
        var wet = BishopSolver.Solve(Unit, new[] { Slice(30, 30, 5, 10, 1, 100, 50), Slice(10, 25, 0, 20, 1.5, 150, 20, 2, M) }, 1)!;
        foreach (var s in wet.Slices)
        {
            double l = s.Width / Math.Cos(s.Alpha);
            Close(s.Vertical, (s.NormalEffective + s.PorePressure * l) * Math.Cos(s.Alpha) + s.Mobilized * Math.Sin(s.Alpha), "vertical equilibrium " + s.Index, 1e-8);
            Close(Math.Cos(s.Alpha) + Math.Sin(s.Alpha) * Math.Tan(s.FrictionAngle) / wet.Factor, s.MAlpha, "mα " + s.Index, 1e-12);
        }
        Close(wet.Driving, wet.Resistance / wet.Factor, "moment equilibrium", 1e-8);
        Assert.IsTrue(Math.Abs(wet.Residual) <= 1e-9 * Math.Max(1, wet.Factor * wet.Driving) && wet.Iterations <= 100);
        // Pore pressure and external driving moments reduce F; the factor grows with c'.
        double f0 = BishopSolver.Solve(Unit, new[] { Slice(30, 30, 0, 0, 1, 100, 50) }, 1)!.Factor;
        Assert.IsTrue(BishopSolver.Solve(Unit, new[] { Slice(30, 30, 0, 10, 1, 100, 50) }, 1)!.Factor < f0);
        Assert.IsTrue(BishopSolver.Solve(Unit, new[] { Slice(30, 30, 0, 0, 1, 100, 65) }, 1)!.Factor < f0);
        Assert.IsTrue(BishopSolver.Solve(Unit, new[] { Slice(30, 30, 5, 0, 1, 100, 50) }, 1)!.Factor > f0);
        // γR changes the ratio only.
        var r1 = BishopSolver.Solve(Unit, new[] { Slice(30, 30, 5, 0, 1, 100, 50) }, 1)!; var r2 = BishopSolver.Solve(Unit, new[] { Slice(30, 30, 5, 0, 1, 100, 50) }, 2)!;
        Assert.AreEqual(r1.Factor, r2.Factor); Close(2 * r1.Ratio, r2.Ratio, "ratio ∝ γR", 1e-15);
    }

    [TestMethod]
    public void BishopReturnsNoSolutionInsteadOfTensionOrPoles()
    {
        Assert.IsNull(BishopSolver.Solve(Unit, new[] { Slice(30, 30, 0, 0, 1, 100, 0) }, 1), "no driving");
        Assert.IsNull(BishopSolver.Solve(Unit, new[] { Slice(30, 30, 0, 0, 1, 100, -20) }, 1), "stabilising");
        Assert.IsNull(BishopSolver.Solve(Unit, new[] { Slice(30, 30, 0, 0, 1, 100, 1e-9) }, 1), "driving at the 1e-9 N/mm threshold");
        Assert.IsNull(BishopSolver.Solve(Unit, new[] { Slice(30, 30, 0, 0, 1, 100, 1e-6) }, 1), "F above 1e5: no root in the bracket");
        Assert.IsNull(BishopSolver.Solve(Unit, new[] { Slice(30, 30, 0, 500, 1, 100, 50) }, 1), "u·b > V: tension at the base");
        Assert.IsNull(BishopSolver.Solve(Unit, new[] { Slice(60, 0, 100, 0, 1, 5, 4.33), Slice(10, 30, 0, 0, 1, 100, 17.4, 2, M) }, 1), "N' < 0 on the steep cohesive slice: (5 − 100·2·sin 60°/11.9)/0.5");
        // α = −40°, φ = 45°: mα vanishes for F = tan 40° · tan 45° = 0.839; the bisection starts above it.
        var pole = BishopSolver.Solve(Unit, new[] { Slice(-40, 45, 30, 0, 1, 50, -5), Slice(50, 45, 0, 0, 1, 200, 150, 2, M) }, 1);
        Assert.IsTrue(pole == null || pole.Factor > Math.Tan(40 * Deg) && pole.Slices.All(s => s.MAlpha > 0));
        foreach (double bad in new[] { 0, -1, double.NaN, double.PositiveInfinity })
            Assert.ThrowsException<ArgumentException>(() => BishopSolver.Solve(Unit, new[] { Slice(30, 30, 0, 0, 1, 100, 50) }, bad), "γR " + bad);
        Assert.ThrowsException<ArgumentException>(() => BishopSolver.Solve(Unit, new SlopeSlice[0], 1));
        Assert.ThrowsException<ArgumentNullException>(() => BishopSolver.Solve(Unit, null!, 1));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.ThrowsException<OperationCanceledException>(() => BishopSolver.Solve(Unit, new[] { Slice(30, 30, 0, 0, 1, 100, 50) }, 1, cancelled.Token));
    }

    [TestMethod]
    public void CirclesThroughExitEntryAndTangentFamily()
    {
        var c = SlopeGeometry.Through(P(-4, 0), P(12, 3.45), -3 * M)!;
        Close(0, c.Base(-4 * M), "exit", 0, 1e-6); Close(3.45 * M, c.Base(12 * M), "entry", 0, 1e-6); Close(3 * M, c.Depth, "depth", 0, 1e-6);
        Assert.IsTrue(c.X > -4 * M && c.X < 12 * M, "centre between the ends");
        Assert.IsNull(SlopeGeometry.Through(P(5, 0), P(1, 2), -2 * M), "exit right of the entry");
        Assert.IsNull(SlopeGeometry.Through(P(0, 0), P(0, 2), -2 * M), "same abscissa");
        Assert.IsNull(SlopeGeometry.Through(P(0, 0), P(10, 5), 0), "lowest point at an end");
        Assert.IsNull(SlopeGeometry.Through(P(-2, 0), P(4, 3.5), -3 * M), "span not reachable: the smallest arc is wider than 6 m");
        var wide = SlopeGeometry.Through(P(0, 1), P(2, 1), .999 * M)!;
        Close(500.0005 * M, wide.Radius, "1 mm deep arc over 2 m: R = (1000² + 1)/2 mm", 1e-9); Close(1 * M, wide.X, "symmetric", 1e-9);
        Assert.IsNull(SlopeGeometry.Through(P(0, 0), P(1, 8), -.5 * M), "too steep for a lower arc");
        var t = SlopeGeometry.TangentAtEntry(P(-2, 0), P(4, 3.5))!;
        Close(0, t.Base(-2 * M), "tangent exit", 0, 1e-6); Close(3.5 * M, t.Base(4 * M), "tangent entry", 0, 1e-6);
        Assert.AreEqual(3.5 * M, t.Y, "vertical tangent: centre at the entry height"); Close(4 * M, t.X + t.Radius, "entry on the horizontal diameter", 0, 1e-9);
        Assert.IsNotNull(SlopeGeometry.Through(P(-2, 0), P(4, 3.5), t.Y - t.Radius), "the tangent circle is also a Through circle (regression of the legacy comparison)");
        Assert.IsNull(SlopeGeometry.TangentAtEntry(P(0, 0), P(3, 3)), "dy = dx");
        Assert.IsNull(SlopeGeometry.TangentAtEntry(P(0, 0), P(3, -1)), "entry below the exit");
        Assert.IsNull(SlopeGeometry.TangentAtEntry(P(1, 0), P(0, 1)), "entry left of the exit");
        Assert.IsNotNull(SlopeGeometry.TangentAtEntry(P(0, 0), P(10, 0)), "horizontal: half circle");
    }

    [TestMethod]
    public void CrossingsIntervalsHeightsAndProperties()
    {
        var c = new SlipCircle(2 * M, 8 * M, 10 * M, -6 * M, 9 * M);
        Assert.AreEqual(0, SlopeGeometry.Crossings(c, 8.1 * M).Count(), "above the centre");
        Assert.AreEqual(0, SlopeGeometry.Crossings(c, -2.1 * M).Count(), "below the bottom");
        CollectionAssert.AreEqual(new[] { 2 * M, 2 * M }, SlopeGeometry.Crossings(c, -2 * M).ToArray(), "tangent at the bottom: the point twice");
        var two = SlopeGeometry.Crossings(c, 0).ToArray(); Assert.AreEqual(2, two.Length);
        Close(2 * M - 6 * M, two[0], "x = xc − √(R² − 8²)", 1e-12); Close(2 * M + 6 * M, two[1], "x = xc + √(R² − 8²)", 1e-12);
        Assert.AreEqual(1, SlopeGeometry.Crossings(new SlipCircle(2 * M, 8 * M, 10 * M, -6 * M, 8 * M), 0).Count(), "a crossing at the end of the arc is excluded");
        var rectangle = new[] { P(0, 0), P(3, 0), P(3, 2), P(0, 2) };
        Assert.IsNull(SlopeGeometry.VerticalInterval(rectangle, -1 * M)); Assert.IsNull(SlopeGeometry.VerticalInterval(rectangle, 3 * M), "right side excluded");
        Assert.AreEqual((0.0, 2 * M), SlopeGeometry.VerticalInterval(rectangle, 0)!.Value, "left side included"); Assert.AreEqual((0.0, 2 * M), SlopeGeometry.VerticalInterval(rectangle, 1.5 * M)!.Value);
        Assert.ThrowsException<ArgumentException>(() => SlopeGeometry.VerticalInterval(new[] { P(0, 0), P(3, 0), P(3, 3), P(0, 3), P(0, 2), P(2, 1.5), P(0, 1) }, 1 * M));
        var line = new[] { P(-30, 0), P(0, 0), P(0, 5), P(12, 6) };
        Assert.AreEqual(0, SlopeGeometry.Height(line, 0), "vertical step: lower end at the step"); Close(5 * M + 1, SlopeGeometry.Height(line, 12), "12 mm beyond the step: 5000 + 1000·12/12000", 1e-12);
        Close(5.5 * M, SlopeGeometry.Height(line, 6 * M), "interpolation", 1e-15);
        Assert.AreEqual(0, SlopeGeometry.Height(line, -30 * M - 1e-6), "within 1e-5 mm of the start");
        Assert.ThrowsException<ArgumentException>(() => SlopeGeometry.Height(line, -30 * M - 1e-4));
        Assert.ThrowsException<ArgumentException>(() => SlopeGeometry.Height(line, 12 * M + 1e-4));
        Assert.ThrowsException<ArgumentException>(() => SlopeGeometry.Height(new[] { P(0, 0) }, 0));
        var props = SlopeGeometry.Properties(new[] { P(0, 0), P(3, 0), P(0, 6) });
        Close(9 * M * M, props.Area, "triangle area mm²", 1e-12); Close(1 * M, props.Centroid.X, "x", 1e-12); Close(2 * M, props.Centroid.Y, "y", 1e-12);
    }

    [TestMethod]
    public void DivisionsAndAdmissibility()
    {
        var wall = new SlopeBody("Wall", new[] { P(0, -1), P(3, -1), P(3, -.4), P(.6, -.4), P(.6, 5), P(0, 5) }, 25 * KN3);
        var section = new SlopeSection(new[] { P(-20, 0), P(0, 0), P(0, 5), P(25, 5) },
            new[] { new SlopeLayer(Silt(0, 32), -3 * M), new SlopeLayer(Silt(10, 26, 60), -25 * M) }, new[] { P(-20, -2), P(25, 1) }, new[] { wall },
            new[] { new SlopeLoad("Q1", 3 * M, 25 * M, 5 * M, 15 * KPa, 0, 0, true) }, 0, 3 * M);
        var circle = SlopeGeometry.Through(P(-8, 0), P(10, 5), -4 * M)!;
        var cuts = SlopeGeometry.Divisions(section, circle, 12);
        foreach (double x in new[] { -8, 0, .6, 3, 10 }) Assert.IsTrue(cuts.Any(v => Math.Abs(v - x * M) < 1e-6), "cut at " + x + " m");
        Assert.IsTrue(cuts.Any(v => Math.Abs(circle.Base(v) + 3 * M) < 1e-6), "crossing of the layer interface at −3 m");
        Assert.IsTrue(cuts.Zip(cuts.Skip(1), (a, b) => b - a).All(d => d > 1e-4), "merged within 1e-4 mm, increasing");
        Assert.AreEqual(circle.Left, cuts[0]); Assert.AreEqual(circle.Right, cuts[cuts.Length - 1]);
        Assert.IsTrue(SlopeGeometry.Admissible(section, circle));
        Assert.IsFalse(SlopeGeometry.Admissible(section, SlopeGeometry.Through(P(-3, 0), P(6, 5), -.8 * M)!), "cuts the foundation (bottom at −1 m)");
        Assert.IsFalse(SlopeGeometry.Admissible(section, new SlipCircle(circle.X, circle.Y, circle.Radius, 0, circle.Right)), "exit not left of the required abscissa");
        Assert.IsFalse(SlopeGeometry.Admissible(section, new SlipCircle(circle.X, circle.Y, circle.Radius, circle.Left, 3 * M)), "entry not right of the wall");
        Assert.IsFalse(SlopeGeometry.Admissible(section, new SlipCircle(0, 0, 26 * M, -8 * M, 10 * M)), "below the covered bottom");
        var shallow = SlopeGeometry.Through(P(-6, 0), P(16, 6), -3 * M)!;
        var flat = new SlopeSection(new[] { P(-30, 0), P(0, 0), P(12, 6), P(40, 6) }, new[] { new SlopeLayer(Silt(), -20 * M) }, null, null, null, 0, 12 * M);
        Assert.IsTrue(SlopeGeometry.Admissible(flat, shallow));
        var bump = new SlopeSection(new[] { P(-30, 0), P(0, 0), P(5, -2.9), P(12, 6), P(40, 6) }, new[] { new SlopeLayer(Silt(), -20 * M) }, null, null, null, 0, 12 * M);
        Assert.IsFalse(SlopeGeometry.Admissible(bump, shallow), "the arc rises above a vertex of the surface");
    }

    [TestMethod]
    public void SliceWeightsWaterBodiesLoadsAndSeismicForces()
    {
        // Dry flat ground at y = 0 and a circle of radius R with its bottom at −h: the soil weight is the circular segment γ R²(θ − sin θ)/2.
        var soil = Silt(5, 30, 40);
        var flat = new SlopeSection(new[] { P(-50, 0), P(50, 0) }, new[] { new SlopeLayer(soil, -40 * M) }, null, null, null, 0, 0);
        double radius = 10 * M, h = 2 * M; var c = new SlipCircle(0, radius - h, radius, -Math.Sqrt(radius * radius - (radius - h) * (radius - h)), Math.Sqrt(radius * radius - (radius - h) * (radius - h)));
        var slices = SlopeStability.Slices(flat, c, Static(), 60);
        double theta = 2 * Math.Acos((radius - h) / radius), segment = radius * radius * (theta - Math.Sin(theta)) / 2;
        Close(segment * soil.UnitWeight, slices.Sum(s => s.SoilWeight), "segment weight", 1e-5);
        Assert.IsTrue(slices.All(s => s.PorePressure == 0 && s.BodyWeight == 0));
        Close(0, slices.Sum(s => s.Driving), "symmetric circle: no driving moment", 0, 1e-6);
        Close(Math.Atan(Math.Tan(30 * Deg) / 1.25), slices[0].FrictionAngle, "φ'd", 1e-15); Close(5 * KPa / 1.25, slices[0].Cohesion, "c'd", 1e-15);
        // Water line at −0.5 m: u = γw (−0.5 m − y) at the base; undrained: u = 0, φ = 0, c = cu/1.4.
        var wet = new SlopeSection(flat.Surface, flat.Layers, new[] { P(-50, -.5), P(50, -.5) }, null, null, 0, 0);
        var wetSlices = SlopeStability.Slices(wet, c, Static(), 60);
        foreach (var s in wetSlices) Close(SoilUnits.WaterUnitWeight * Math.Max(0, -.5 * M - s.BaseY), s.PorePressure, "u " + s.Index, 1e-12, 1e-15);
        Assert.IsTrue(wetSlices.Sum(s => s.SoilWeight) > slices.Sum(s => s.SoilWeight), "γsat below the water line");
        var undrained = SlopeStability.Slices(wet, c, Static(undrained: true), 60);
        Assert.IsTrue(undrained.All(s => s.PorePressure == 0 && s.FrictionAngle == 0)); Close(40 * KPa / 1.4, undrained[0].Cohesion, "cu,d", 1e-15);
        // Seismic coefficients: V = (1 − kv) W; kh adds kh W (yc − yG)/R to the driving term.
        var quake = SlopeStability.Slices(flat, c, new SlopeFactors("kh", 1, 1, 1, 1, 1, 1.2, .1, .05, false), 60);
        var still = SlopeStability.Slices(flat, c, new SlopeFactors("0", 1, 1, 1, 1, 1, 1.2, 0, 0, false), 60);
        for (int i = 0; i < quake.Length; i++)
        {
            Close(.95 * quake[i].SoilWeight, quake[i].Vertical, "V = 0.95 W", 1e-12);
            Close(.95 * still[i].Driving + .1 * quake[i].SoilWeight * (c.Y - quake[i].WeightY) / c.Radius, quake[i].Driving, "driving " + i, 1e-9, 1e-9);
        }
        // Soil weight factor scales the weight; a rigid body replaces the soil it occupies.
        var heavy = SlopeStability.Slices(flat, c, new SlopeFactors("1.3", 1.3, 1, 1, 1, 1, 1, 0, 0, false), 60);
        Close(1.3 * slices.Sum(s => s.SoilWeight), heavy.Sum(s => s.SoilWeight), "γ factor", 1e-12);
        var block = new SlopeBody("Block", new[] { P(-1, -1), P(1, -1), P(1, 0), P(-1, 0) }, 25 * KN3);
        var withBody = SlopeStability.Slices(new SlopeSection(flat.Surface, flat.Layers, null, new[] { block }, null, 0, 0), c, Static(), 60);
        Close(2 * M * 1 * M * 25 * KN3, withBody.Sum(s => s.BodyWeight), "body weight", 1e-9);
        Close(slices.Sum(s => s.SoilWeight) - 2 * M * 1 * M * soil.UnitWeight, withBody.Sum(s => s.SoilWeight), "displaced soil removed", 1e-9);
    }

    [TestMethod]
    public void LoadsAreFactoredAndPlacedOnce()
    {
        var flat = new SlopeSection(new[] { P(-50, 0), P(50, 0) }, new[] { new SlopeLayer(Silt(), -40 * M) }, null, null,
            new[] { new SlopeLoad("q", -2 * M, 3 * M, 0, 10 * KPa, 0, 0, true), new SlopeLoad("P", 1 * M, 1 * M, 0, 50, 0, 0, false),
                new SlopeLoad("H", 4 * M, 4 * M, 1 * M, 0, 30, 0, false), new SlopeLoad("Mo", 0, 0, 0, 0, 0, 20 * M, false), new SlopeLoad("End", 6 * M, 6 * M, 0, 40, 0, 0, false) }, 0, 0);
        var c = new SlipCircle(0, 8 * M, 10 * M, -6 * M, 6 * M);
        var none = SlopeStability.Slices(flat, c, Static(), 40);
        Assert.IsTrue(none.All(s => s.VerticalLoad == 0 && s.HorizontalLoad == 0), "missing factors: loads ignored");
        var loaded = SlopeStability.Slices(flat, c, Static(loads: new Dictionary<string, double> { ["q"] = 1.3, ["P"] = 1, ["H"] = 1.5, ["Mo"] = 1, ["End"] = 1 }), 40);
        Close(1.3 * 10 * KPa * 5 * M + 50 + 40, loaded.Sum(s => s.VerticalLoad), "vertical loads", 1e-12);
        Close(1.5 * 30, loaded.Sum(s => s.HorizontalLoad), "horizontal load once", 1e-12);
        var withoutP = SlopeStability.Slices(flat, c, Static(loads: new Dictionary<string, double> { ["q"] = 1.3, ["H"] = 1.5, ["Mo"] = 1, ["End"] = 1 }), 40);
        var changed = loaded.Zip(withoutP, (a, b) => a.VerticalLoad - b.VerticalLoad).Where(d => d != 0).ToArray();
        Assert.AreEqual(1, changed.Length, "the concentrated load acts on one slice"); Close(50, changed[0], "P", 1e-12);
        Assert.IsTrue(loaded.Single(s => s.VerticalLoad - withoutP[s.Index - 1].VerticalLoad != 0).Left <= 1 * M);
        Assert.AreEqual(40, loaded[loaded.Length - 1].VerticalLoad, 1e-9, "a load at the right end belongs to the last slice");
        double moments = loaded.Sum(s => s.Driving) - none.Sum(s => s.Driving);
        double expected = (1.3 * 10 * KPa * 5 * M * (.5 * M - 0) + 50 * (1 * M) + 1.5 * 30 * (8 * M - 1 * M) + 20 * M + 40 * 6 * M) / (10 * M);
        Close(expected, moments, "driving of the loads", 1e-9);
    }

    [TestMethod]
    public void FactorsFromTheModelStandards()
    {
        var ntc = new StandardNTC2018Geotechnics();
        var st = SlopeFactors.FromCombination(ntc.Combinations(GeotechnicalCheck.SlopeStability, GeotechnicalSituation.PersistentTransient).Single(), 0, 0, false);
        Assert.AreEqual("A2+M2+R2", st.Name); Assert.AreEqual(1, st.SoilWeight); Assert.AreEqual(1, st.BodyWeight);
        Assert.AreEqual(1.25, st.TanFrictionAngle); Assert.AreEqual(1.25, st.EffectiveCohesion); Assert.AreEqual(1.4, st.UndrainedShearStrength); Assert.AreEqual(1.1, st.ResistanceFactor);
        var eq = SlopeFactors.FromCombination(ntc.Combinations(GeotechnicalCheck.GlobalStability, GeotechnicalSituation.Seismic).Single(), .38 * .25, .5 * .38 * .25, false,
            new Dictionary<string, double> { ["Q"] = .3 });
        Assert.AreEqual(1, eq.TanFrictionAngle); Assert.AreEqual(1.2, eq.ResistanceFactor); Assert.AreEqual(.095, eq.Kh, 1e-15); Assert.AreEqual(.3, eq.Loads["Q"]);
        var en = new StandardEN1997p1(); var da1 = en.Combinations(GeotechnicalCheck.SlopeStability, GeotechnicalSituation.PersistentTransient);
        Assert.ThrowsException<ArgumentException>(() => SlopeFactors.FromCombination(da1[0], 0, 0, false), "A1: γG 1.35 unfavourable, 1.0 favourable");
        Assert.AreEqual(1.0, SlopeFactors.FromCombination(da1[1], 0, 0, false).ResistanceFactor, "A2+M2+R1");
        Assert.ThrowsException<ArgumentException>(() => SlopeFactors.FromCombination(ntc.Combinations(GeotechnicalCheck.ShallowFoundationBearing, GeotechnicalSituation.PersistentTransient).Single(), 0, 0, false));
        Assert.ThrowsException<ArgumentNullException>(() => SlopeFactors.FromCombination(null!, 0, 0, false));
        // The loads of the factors are copied: changing the caller's dictionary has no effect.
        var loads = new Dictionary<string, double> { ["Q"] = 1.3 }; var f = Static(loads: loads); loads["Q"] = 9;
        Assert.AreEqual(1.3, f.Loads["Q"]);
    }

    [TestMethod]
    public void InvalidSectionsSearchesAndFactorsAreRejected()
    {
        var s = Slope(); var ok = new[] { Static() };
        void Reject(SlopeSection section, SlopeSearch search, IReadOnlyList<SlopeFactors> cases, string what)
            => Assert.ThrowsException<ArgumentException>(() => SlopeStability.Calculate(section, search, cases), what);
        SlopeSearch Q(double exitMax = -.5, double entryMin = 12.5, double depthMax = 12, int grid = 5, int slices = 30, int refinements = 1, double depthMin = .5, double exitMin = -15, double entryMax = 30)
            => new(exitMin * M, exitMax * M, entryMin * M, entryMax * M, depthMin * M, depthMax * M, grid, slices, refinements);
        // Search domain.
        Reject(s, Q(exitMax: .5), ok, "exits beyond the required left abscissa"); Reject(s, Q(exitMax: 0), ok, "exit at the required abscissa");
        Reject(s, Q(entryMin: 11), ok, "entries before the required right abscissa"); Reject(s, Q(entryMin: 12), ok, "entry at the required abscissa");
        Reject(s, Q(depthMax: 21), ok, "depth below the layers"); Reject(s, Q(depthMin: 0), ok, "depth 0"); Reject(s, Q(depthMin: 12), ok, "depth min = max");
        Reject(s, Q(exitMin: -31), ok, "exit before the surface"); Reject(s, Q(entryMax: 41), ok, "entry beyond the surface");
        Reject(s, Q(exitMin: -.5), ok, "exit min = max"); Reject(s, Q(depthMax: double.NaN), ok, "NaN depth");
        Reject(s, Q(grid: 2), ok, "grid 2"); Reject(s, Q(grid: 22), ok, "grid 22"); Reject(s, Q(slices: 19), ok, "19 slices"); Reject(s, Q(slices: 201), ok, "201 slices");
        Reject(s, Q(refinements: -1), ok, "refinements −1"); Reject(s, Q(refinements: 5), ok, "refinements 5");
        SlopeStability.Validate(s, Q(depthMax: 20), ok); SlopeStability.Validate(s, Q(grid: 3, slices: 20, refinements: 0), ok); SlopeStability.Validate(s, Q(grid: 21, slices: 200, refinements: 4), ok);
        // Combinations.
        Reject(s, Search, new SlopeFactors[0], "no combination"); Reject(s, Search, Enumerable.Repeat(Static(), 257).ToArray(), "257 combinations");
        Reject(s, Search, new[] { new SlopeFactors("kh", 1, 1, 1, 1, 1, 1.2, .6, 0, false) }, "kh 0.6");
        Reject(s, Search, new[] { new SlopeFactors("kv", 1, 1, 1, 1, 1, 1.2, 0, -.51, false) }, "kv −0.51");
        Reject(s, Search, new[] { new SlopeFactors("R", 1, 1, 1, 1, 1, 0, 0, 0, false) }, "γR 0");
        Reject(s, Search, new[] { new SlopeFactors("M", 1, 1, -1.25, 1, 1, 1, 0, 0, false) }, "negative γφ");
        Reject(s, Search, new[] { new SlopeFactors("W", 0, 1, 1, 1, 1, 1, 0, 0, false) }, "zero soil weight factor");
        Reject(s, Search, new[] { Static(loads: new Dictionary<string, double> { ["Q"] = double.NaN }) }, "NaN load factor");
        Reject(s, Search, new[] { Static(undrained: true) }, "undrained without cu");
        Assert.ThrowsException<ArgumentNullException>(() => SlopeStability.Calculate(s, Search, new SlopeFactors[] { null! }));
        SlopeStability.Validate(s, Search, new[] { new SlopeFactors("kh", 1, 1, 1, 1, 1, 1.2, .5, -.5, false) });
        // Soils: 10 ≤ γ ≤ γsat ≤ 30 kN/m³, φ' ≤ 50°, decreasing bottoms.
        Reject(Slope(Silt(gamma: 9.99)), Search, ok, "γ < 10"); SlopeStability.Validate(Slope(Silt(gamma: 10, gammaSat: 10)), Search, ok);
        Reject(Slope(Silt(gammaSat: 30.01)), Search, ok, "γsat > 30"); SlopeStability.Validate(Slope(Silt(gammaSat: 30)), Search, ok);
        Reject(Slope(Silt(phi: 50.01)), Search, ok, "φ > 50°"); SlopeStability.Validate(Slope(Silt(phi: 50)), Search, ok);
        var layered = new SlopeSection(s.Surface, new[] { new SlopeLayer(Silt(), -5 * M), new SlopeLayer(Silt(), -5 * M) }, null, null, null, 0, 12 * M);
        Reject(layered, Q(depthMax: 4), ok, "equal bottoms");
        Reject(new SlopeSection(s.Surface, new SlopeLayer[0], null, null, null, 0, 12 * M), Search, ok, "no layers");
        // Undrained: cu required in the valley column too.
        var columns = new SlopeSection(s.Surface, new[] { new SlopeLayer(Silt(cu: 50), -20 * M) }, null, null, null, 0, 12 * M, new[] { new SlopeLayer(Silt(), -20 * M) }, -5 * M);
        Reject(columns, Search, new[] { Static(undrained: true) }, "valley column without cu");
        Reject(new SlopeSection(s.Surface, s.Layers, null, null, null, 0, 12 * M, columns.ValleyLayers, double.NaN), Search, ok, "NaN split");
        // Water line.
        Reject(Slope(water: new[] { P(-30, 1), P(40, 7) }), Search, ok, "water above the ground");
        Reject(Slope(water: new[] { P(-20, -1), P(40, 2) }), Search, ok, "water shorter than the surface");
        Reject(Slope(water: new[] { P(-30, -1) }), Search, ok, "water with one point");
        Reject(Slope(water: new[] { P(-30, -1), P(-30, -2), P(40, 2) }), Search, ok, "vertical step in the water line");
        Reject(new SlopeSection(s.Surface, s.Layers, null, null, null, 0, 12 * M, waterUnitWeight: 0), Search, ok, "γw = 0");
        SlopeStability.Validate(Slope(water: new[] { P(-30, 0), P(0, 0), P(12, 6), P(40, 6) }), Search, ok);
        // Surface.
        Reject(new SlopeSection(new[] { P(-30, 0), P(0, 0), P(-1, 6), P(40, 6) }, s.Layers, null, null, null, 0, 12 * M), Search, ok, "decreasing abscissa");
        Reject(new SlopeSection(new[] { P(0, 0) }, s.Layers, null, null, null, 0, 12 * M), Search, ok, "one point");
        Reject(new SlopeSection(Enumerable.Range(0, 101).Select(i => P(-30 + .7 * i, 0)), s.Layers, null, null, null, 0, 12 * M), Search, ok, "101 points");
        // Bodies and loads (checks added in the port).
        Reject(Slope(bodies: new[] { new SlopeBody("B", new[] { P(0, 0), P(1, 0) }, 25 * KN3) }), Search, ok, "body with two vertices");
        Reject(Slope(bodies: new[] { new SlopeBody("B", new[] { P(0, -1), P(1, -1), P(1, 0) }, 0) }), Search, ok, "body without weight");
        Reject(Slope(loads: new[] { new SlopeLoad("q", 5 * M, 4 * M, 0, 10 * KPa, 0, 0, true) }), Search, ok, "distributed load with right < left");
        Reject(Slope(loads: new[] { new SlopeLoad("P", double.NaN, 0, 0, 10, 0, 0, false) }), Search, ok, "NaN load position");
    }

    [TestMethod]
    public void SearchBehavesConsistently()
    {
        var weak = SlopeStability.Calculate(Slope(Silt(2)), Search, new[] { Static() }).Cases[0];
        var strong = SlopeStability.Calculate(Slope(Silt(15)), Search, new[] { Static() }).Cases[0];
        Assert.IsTrue(strong.Critical!.Factor > weak.Critical!.Factor, "c' increases F");
        // Same F with any γR; the ratio scales.
        var a = SlopeStability.Calculate(Slope(), Search, new[] { new SlopeFactors("1", 1, 1, 1.25, 1.25, 1.4, 1, 0, 0, false), new SlopeFactors("2", 1, 1, 1.25, 1.25, 1.4, 2, 0, 0, false) });
        Assert.AreEqual(a.Cases[0].Critical!.Factor, a.Cases[1].Critical!.Factor); Close(2 * a.Cases[0].Ratio!.Value, a.Cases[1].Ratio!.Value, "ratio", 1e-15);
        // Reproducible: the same input gives the same circle.
        var again = SlopeStability.Calculate(Slope(), Search, new[] { new SlopeFactors("1", 1, 1, 1.25, 1.25, 1.4, 1, 0, 0, false) }).Cases[0];
        Assert.AreEqual(a.Cases[0].Critical!.Factor, again.Critical!.Factor); Assert.AreEqual(a.Cases[0].Tried, again.Tried);
        // The critical circle of the search is admissible and its slices balance.
        var critical = a.Cases[0].Critical!;
        Assert.IsTrue(SlopeGeometry.Admissible(Slope(), critical.Circle)); Close(critical.Driving, critical.Resistance / critical.Factor, "moments", 1e-8);
        // Conclusive verdicts: a ratio above 1 always, otherwise only a converged search inside the domain.
        foreach (var c in a.Cases) Assert.AreEqual(c.Critical!.Ratio > 1 || !c.Boundary && c.NumericalFailures == 0, c.IsConclusive);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.ThrowsException<OperationCanceledException>(() => SlopeStability.Calculate(Slope(), Search, new[] { Static() }, cancelled.Token));
        Assert.AreEqual(4, a.Notes.Count); StringAssert.Contains(a.Notes[0], "mα");
    }
}
