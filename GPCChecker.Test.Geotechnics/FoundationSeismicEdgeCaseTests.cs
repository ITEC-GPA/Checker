using GPC.Checkers.Geotechnics.Foundations;
using GPC.Checkers.Geotechnics.Seismic;
using GPC.Model.Geotechnics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static GeotechnicsTests.GeotechnicsFixture;

namespace GeotechnicsTests;

/// <summary>Strip-load stresses and oedometric settlement, Newmark sliding block and EN 1998-5 Annex F bearing capacity: closed forms, limits and invalid data.</summary>
[TestClass]
public class FoundationSeismicEdgeCaseTests
{
    // ---------------------------------------------------------------- Stresses and settlement

    [TestMethod]
    public void StripStressesMatchTheClosedForms()
    {
        double p = 150 * KPa, b = 2 * M;
        // Under the centre of a uniform strip: σz = p/π (α + sin α), α = 2 atan(B/2z).
        foreach (double z in new[] { .1, .5, 1, 2, 5, 20 })
        {
            double alpha = 2 * Math.Atan(b / 2 / (z * M));
            Close(p / Math.PI * (alpha + Math.Sin(alpha)), FoundationSettlement.Stress(0, z * M, -b / 2, b / 2, p, p), "centre z = " + z, 1e-12, 1e-15);
        }
        // Superposition in space and in load, symmetry, shallow and deep limits.
        double x = .7 * M, depth = 1.3 * M;
        Close(FoundationSettlement.Stress(x, depth, -1 * M, .4 * M, p, p) + FoundationSettlement.Stress(x, depth, .4 * M, 1 * M, p, p),
            FoundationSettlement.Stress(x, depth, -1 * M, 1 * M, p, p), "split strip", 1e-12, 1e-15);
        Close(FoundationSettlement.Stress(x, depth, 0, b, 50 * KPa, 50 * KPa) + FoundationSettlement.Stress(x, depth, 0, b, 0, 100 * KPa),
            FoundationSettlement.Stress(x, depth, 0, b, 50 * KPa, 150 * KPa), "uniform + triangle", 1e-12, 1e-15);
        Close(FoundationSettlement.Stress(x, depth, 0, b, 50 * KPa, 150 * KPa), FoundationSettlement.Stress(-x, depth, -b, 0, 150 * KPa, 50 * KPa), "mirror", 1e-12, 1e-15);
        Close(75 * KPa, FoundationSettlement.Stress(.5 * M, .01, 0, b, 50 * KPa, 150 * KPa), "z → 0: the local load 50 + 100·0.5/2", 1e-4);
        Close(0, FoundationSettlement.Stress(3 * M, .01, 0, b, p, p), "z → 0 outside the strip", 0, 1e-8);
        Close(2 * p * b / (Math.PI * 200 * M), FoundationSettlement.Stress(b / 2, 200 * M, 0, b, p, p), "deep: line load 2P/(πz)", 1e-4);
        foreach (double z in new[] { 0, -1 }) Assert.ThrowsException<ArgumentException>(() => FoundationSettlement.Stress(0, z, 0, b, p, p), "z " + z);
        Assert.ThrowsException<ArgumentException>(() => FoundationSettlement.Stress(0, M, b, b, p, p), "right = left");
        Assert.ThrowsException<ArgumentException>(() => FoundationSettlement.Stress(0, M, b, 0, p, p), "right < left");
    }

    private static readonly SettlementLayer[] Layers = { new("Sand", 3 * M, 15000 * KPa), new("Clay", 6 * M, 4000 * KPa) };

    [TestMethod]
    public void SettlementIsTheIntegralOfTheStressOverTheModulus()
    {
        double b = 2 * M, q = 170 * KPa, removed = 20 * KPa;
        var r = FoundationSettlement.Calculate(Layers, b / 2, 0, b, q, q, removed, b, 40);
        // Independent Simpson integration of Δσ(z)/Eoed with 2000 intervals per layer.
        double Delta(double z) => FoundationSettlement.Stress(b / 2, z, 0, b, q, q) - FoundationSettlement.Stress(b / 2, z, 0, b, removed, removed);
        double expected = 0, top = 0;
        foreach (var layer in Layers)
        {
            int n = 2000; double h = layer.Thickness / n, sum = 0;
            for (int i = 0; i <= n; i++) { double z = top + Math.Max(i * h, 1e-9); sum += (i == 0 || i == n ? 1 : i % 2 == 1 ? 4 : 2) * Delta(z); }
            expected += sum * h / 3 / layer.ConstrainedModulus; top += layer.Thickness;
        }
        Close(expected, r.Settlement, "settlement", 1e-6);
        Close(r.Slices.Sum(s => s.Settlement), r.Settlement, "sum of the slices", 1e-12);
        Close(Delta(9 * M), r.BottomStress, "bottom stress", 1e-12);
        Assert.AreEqual(Math.Max(40, (int)Math.Ceiling(3.0 / 2 * 40)) + Math.Max(40, (int)Math.Ceiling(6.0 / 2 * 40)), r.Slices.Count, "60 + 120 slices: thicker layers get more");
        Close(9 * M, r.Slices[r.Slices.Count - 1].Bottom, "last slice at the bottom", 1e-12);
        // Halving the moduli doubles the settlement; removal reduces it; equal load and removal on the footing give zero.
        var soft = FoundationSettlement.Calculate(Layers.Select(l => new SettlementLayer(l.Name, l.Thickness, l.ConstrainedModulus / 2)).ToArray(), b / 2, 0, b, q, q, removed, b, 40);
        Close(2 * r.Settlement, soft.Settlement, "E/2", 1e-12);
        Assert.IsTrue(FoundationSettlement.Calculate(Layers, b / 2, 0, b, q, q, 0, b, 40).Settlement > r.Settlement);
        Assert.AreEqual(0, FoundationSettlement.Calculate(Layers, b / 2, 0, b, q, q, q, b, 40).Settlement, "load fully compensated");
        // Net unloading at some depth needs a recompression modulus.
        Assert.ThrowsException<ArgumentException>(() => FoundationSettlement.Calculate(Layers, b / 2, 0, b, 15 * KPa, 15 * KPa, 20 * KPa, b, 40));
        Assert.ThrowsException<ArgumentException>(() => FoundationSettlement.Calculate(Layers, 6 * M, 0, b, 15 * KPa, 15 * KPa, 20 * KPa, b, 40), "outside the footing the removal still dominates near it");
    }

    [TestMethod]
    public void SettlementDataAreChecked()
    {
        double b = 2 * M, q = 100 * KPa;
        FoundationSettlement.Calculate(Layers, 0, 0, b, q, q, 0, b, 10); FoundationSettlement.Calculate(Layers, 0, 0, b, q, q, 0, b, 1000);
        foreach (int n in new[] { 9, 1001 }) Assert.ThrowsException<ArgumentException>(() => FoundationSettlement.Calculate(Layers, 0, 0, b, q, q, 0, b, n), n + " subdivisions");
        Assert.ThrowsException<ArgumentException>(() => FoundationSettlement.Calculate(new[] { new SettlementLayer("Deep", 2000 * M, 10) }, 0, 0, .1 * M, q, q, 0, .1 * M, 1000), "more than 20000 slices");
        foreach (var layers in new[] { new SettlementLayer[0], new[] { new SettlementLayer("A", 0, 10) }, new[] { new SettlementLayer("A", M, 0) }, new[] { new SettlementLayer("A", -M, 10) },
            new[] { new SettlementLayer("A", double.NaN, 10) }, new[] { new SettlementLayer("A", M, double.PositiveInfinity) }, new SettlementLayer[] { null! } })
            Assert.ThrowsException<ArgumentException>(() => FoundationSettlement.Calculate(layers, 0, 0, b, q, q, 0, b));
        Assert.ThrowsException<ArgumentException>(() => FoundationSettlement.Calculate(Layers, 0, 0, b, q, q, 0, 0), "footing width 0");
        Assert.ThrowsException<ArgumentException>(() => FoundationSettlement.Calculate(Layers, double.NaN, 0, b, q, q, 0, b), "NaN abscissa");
        Assert.ThrowsException<ArgumentException>(() => FoundationSettlement.Calculate(Layers, 0, 0, b, q, double.PositiveInfinity, 0, b), "infinite load");
        Assert.ThrowsException<ArgumentNullException>(() => FoundationSettlement.Calculate(null!, 0, 0, b, q, q, 0, b));
    }

    [TestMethod]
    public void SettlementLayersFromAModelProfile()
    {
        Soil S(string name, double? eoed) => new(name, 18 * KN3, 20 * KN3, 30 * Deg, 0, "report", constrainedModulus: eoed);
        var profile = new SoilProfile("P1", new[] { new SoilLayer(S("Fill", 8), 0, -1 * M), new SoilLayer(S("Sand", 15), -1 * M, -3 * M), new SoilLayer(S("Clay", 4), -3 * M, -9 * M) }, "survey");
        var layers = SettlementLayer.FromProfile(profile, -1.5 * M);
        CollectionAssert.AreEqual(new[] { "Sand", "Clay" }, layers.Select(l => l.Name).ToArray());
        Assert.AreEqual(1.5 * M, layers[0].Thickness, 1e-9); Assert.AreEqual(6 * M, layers[1].Thickness); Assert.AreEqual(15, layers[0].ConstrainedModulus);
        CollectionAssert.AreEqual(new[] { "Sand", "Clay" }, SettlementLayer.FromProfile(profile, -1 * M).Select(l => l.Name).ToArray(), "foundation at an interface");
        Assert.AreEqual(3, SettlementLayer.FromProfile(profile, 0).Length, "foundation at the ground surface");
        Assert.ThrowsException<ArgumentException>(() => SettlementLayer.FromProfile(profile, 1 * M), "above the ground");
        Assert.ThrowsException<ArgumentException>(() => SettlementLayer.FromProfile(profile, -9 * M), "at the base");
        var missing = new SoilProfile("P2", new[] { new SoilLayer(S("Sand", 15), 0, -2 * M), new SoilLayer(S("Clay", null), -2 * M, -9 * M) }, "survey");
        StringAssert.Contains(Assert.ThrowsException<ArgumentException>(() => SettlementLayer.FromProfile(missing, -1 * M)).Message, "Clay");
        Assert.ThrowsException<ArgumentNullException>(() => SettlementLayer.FromProfile(null!, 0));
    }

    // ---------------------------------------------------------------- Newmark

    private static AccelerogramSample[] Step(double a, double duration) => new[] { new AccelerogramSample(0, a), new AccelerogramSample(duration, a) };

    [TestMethod]
    public void NewmarkMatchesTheRectangularPulse()
    {
        // Constant A for t0, then the block stops under zero ground acceleration: d = (A − ay) A g t0² / (2 ay), vmax = (A − ay) g t0.
        foreach (var (a, ay, t0) in new[] { (.3, .1, .5), (.25, .05, .2), (.6, .3, 1.0) })
        {
            var r = NewmarkSliding.Calculate(Step(a, t0), ay);
            Close((a - ay) * a * NewmarkSliding.Gravity * t0 * t0 / (2 * ay), r.Displacement, $"d A={a} ay={ay}", 1e-12);
            Close((a - ay) * NewmarkSliding.Gravity * t0, r.PeakVelocity, "vmax", 1e-12); Assert.AreEqual(a, r.Pga);
            var stop = r.Points[r.Points.Count - 1];
            Close(t0 + (a - ay) * t0 / ay, stop.Time, "stop time", 1e-12); Assert.AreEqual(0, stop.Velocity); Assert.AreEqual(0, stop.Acceleration);
            Close((a - ay) * NewmarkSliding.Gravity * t0 * t0 / 2, r.Points[1].Displacement, "displacement at the end of the record", 1e-12);
        }
        Assert.AreEqual(9810, NewmarkSliding.Gravity);
    }

    [TestMethod]
    public void NewmarkThresholdDirectionScaleAndStops()
    {
        Assert.AreEqual(0, NewmarkSliding.Calculate(Step(.1, 1), .1).Displacement, "at the threshold");
        Assert.AreEqual(0, NewmarkSliding.Calculate(Step(.09, 1), .1).Displacement, "below the threshold");
        var negative = NewmarkSliding.Calculate(Step(-.5, 1), .1);
        Assert.AreEqual(0, negative.Displacement, "one direction only"); Assert.AreEqual(.5, negative.Pga); Assert.AreEqual(2, negative.Points.Count, "no stop point");
        // Scaling the record or the factor is the same.
        var record = Enumerable.Range(0, 200).Select(i => new AccelerogramSample(i * .01, .3 * Math.Sin(i * .2) * Math.Exp(-i * .01))).ToArray();
        var scaled = NewmarkSliding.Calculate(record, .05, 1.5); var direct = NewmarkSliding.Calculate(record.Select(s => new AccelerogramSample(s.Time, 1.5 * s.Acceleration)).ToArray(), .05);
        Close(direct.Displacement, scaled.Displacement, "scale", 1e-12, 1e-12); Close(direct.PeakVelocity, scaled.PeakVelocity, "scale v", 1e-12, 1e-12);
        // Two pulses separated by a quiet interval: the block stops between them (velocity exactly 0) and the displacements add.
        var two = new[] { new AccelerogramSample(0, .3), new AccelerogramSample(.2, .3), new AccelerogramSample(.21, 0), new AccelerogramSample(2, 0), new AccelerogramSample(2.01, .3), new AccelerogramSample(2.21, .3) };
        var r = NewmarkSliding.Calculate(two, .1);
        Assert.AreEqual(0, r.Points.Single(p => p.Time == 2).Velocity, "stopped before the second pulse");
        Assert.IsTrue(r.Points.Zip(r.Points.Skip(1), (p, q) => q.Displacement >= p.Displacement).All(v => v), "displacement never decreases");
        Assert.IsTrue(r.Points.All(p => p.Velocity >= 0), "no negative relative velocity");
        // A residual of the stop never keeps the block sliding: every point after a stop without driving acceleration has zero velocity.
        var late = new[] { new AccelerogramSample(.5, 0), new AccelerogramSample(.6, -.2), new AccelerogramSample(.8, .25), new AccelerogramSample(1, -.1), new AccelerogramSample(1.3, .15) };
        var stopped = NewmarkSliding.Calculate(late, .2);
        Assert.AreEqual(0, stopped.Points[3].Velocity); Assert.AreEqual(0, stopped.Points[4].Velocity); Assert.AreEqual(5, stopped.Points.Count, "no fictitious stop point");
    }

    [TestMethod]
    public void NewmarkDataAreChecked()
    {
        var ok = Step(.3, .5);
        foreach (var (record, ay, scale, what) in new[]
        {
            (new[] { new AccelerogramSample(0, .1) }, .1, 1.0, "one sample"), (new[] { new AccelerogramSample(0, 0), new AccelerogramSample(0, .2) }, .1, 1.0, "equal times"),
            (new[] { new AccelerogramSample(0, 0), new AccelerogramSample(.2, .2), new AccelerogramSample(.1, .2) }, .1, 1.0, "decreasing times"),
            (new[] { new AccelerogramSample(-.1, 0), new AccelerogramSample(.1, .2) }, .1, 1.0, "negative start"),
            (new[] { new AccelerogramSample(0, 0), new AccelerogramSample(.1, double.NaN) }, .1, 1.0, "NaN acceleration"),
            (new[] { new AccelerogramSample(0, 0), new AccelerogramSample(double.PositiveInfinity, .1) }, .1, 1.0, "infinite time"),
            (ok, 0.0, 1.0, "yield 0"), (ok, -.1, 1.0, "negative yield"), (ok, double.NaN, 1.0, "NaN yield"), (ok, .1, 0.0, "scale 0"), (ok, .1, -1.0, "negative scale"),
            (ok, .1, double.PositiveInfinity, "infinite scale")
        })
            Assert.ThrowsException<ArgumentException>(() => NewmarkSliding.Calculate(record, ay, scale), what);
        Assert.ThrowsException<ArgumentException>(() => NewmarkSliding.Calculate(Enumerable.Range(0, 200001).Select(i => new AccelerogramSample(i * .001, 0)).ToArray(), .1), "200001 samples");
        Assert.AreEqual(0, NewmarkSliding.Calculate(Enumerable.Range(0, 200000).Select(i => new AccelerogramSample(i * .001, 0)).ToArray(), .1).Displacement, "200000 samples accepted");
        Assert.ThrowsException<ArgumentNullException>(() => NewmarkSliding.Calculate(null!, .1));
    }

    // ---------------------------------------------------------------- EN 1998-5 Annex F

    private static double Nmax(double b, double gamma, double phi, double kv)
    {
        double nq = Math.Exp(Math.PI * Math.Tan(phi)) * Math.Pow(Math.Tan(Math.PI / 4 + phi / 2), 2);
        return .5 * gamma * (1 - kv) * b * b * 2 * (nq - 1) * Math.Tan(phi);
    }

    private static double Interaction(SeismicBearingResult r, double scale)
    {
        double f = r.SoilInertia, x = r.NBar * scale;
        return (Math.Pow(1 - .41 * f, 1.14) * Math.Pow(2.90 * r.VBar * scale, 1.14) + Math.Pow(1 - .32 * f, 1.01) * Math.Pow(2.80 * r.MBar * scale, 1.01))
            / (Math.Pow(x, .92) * Math.Pow(r.VerticalLimit - x, 1.25));
    }

    [TestMethod]
    public void StaticVerticalCapacityIsNmaxOverTheFactors()
    {
        // φ = 30°: Nq = e^(π tan 30°) tan² 60° = 18.401, Nγ = 2 (Nq − 1) tan 30° = 20.093; B = 2 m, γ = 18 kN/m³: Nmax = ½ 18 · 2² · 20.093 = 723.4 kN/m.
        double b = 2 * M, gamma = 18 * KN3, phi = 30 * Deg;
        var r = ShallowFoundationSeismic.Calculate(b, gamma, phi, 300, 0, 0, 0, 0, 1, 1);
        Close(723.36, r.NMax, "Nmax", 1e-4); Close(Nmax(b, gamma, phi, 0), r.NMax, "Nmax formula", 1e-12);
        Assert.AreEqual(0, r.SoilInertia); Assert.AreEqual(1, r.VerticalLimit); Close(r.NMax, r.Capacity, "capacity = Nmax", 1e-12);
        Close(300 / r.NMax, r.Ratio!.Value, "ratio", 1e-12); Assert.AreEqual(SeismicBearingStatus.Satisfied, r.Status); Assert.AreEqual(0, r.Interaction);
        // γRd and γR divide the capacity; kv reduces Nmax.
        var factored = ShallowFoundationSeismic.Calculate(b, gamma, phi, 300, 0, 0, 0, 0, 1.15, 1.8);
        Close(r.NMax / (1.15 * 1.8), factored.Capacity, "factored capacity", 1e-12); Close(300 * 1.15 * 1.8 / r.NMax, factored.Ratio!.Value, "factored ratio", 1e-12);
        Close(.95 * r.NMax, ShallowFoundationSeismic.Calculate(b, gamma, phi, 300, 0, 0, 0, .05, 1, 1).NMax, "kv = +0.05", 1e-12);
        Close(1.05 * r.NMax, ShallowFoundationSeismic.Calculate(b, gamma, phi, 300, 0, 0, 0, -.05, 1, 1).NMax, "kv = −0.05", 1e-12);
        Assert.AreEqual(SeismicBearingStatus.NotSatisfied, ShallowFoundationSeismic.Calculate(b, gamma, phi, 800, 0, 0, 0, 0, 1, 1).Status, "N > Nmax");
    }

    [TestMethod]
    public void SeismicInteractionDomain()
    {
        double b = 2 * M, gamma = 18 * KN3, phi = 32 * Deg;
        var r = ShallowFoundationSeismic.Calculate(b, gamma, phi, 500, 60, 40 * M, .1, 0, 1, 1);
        Close(.1 / Math.Tan(phi), r.SoilInertia, "F̄ = kh / tan φ", 1e-12); Close(Math.Pow(1 - .96 * r.SoilInertia, .39), r.VerticalLimit, "limit", 1e-12);
        Close(500 / r.NMax, r.NBar, "N̄", 1e-12); Close(60 / r.NMax, r.VBar, "V̄", 1e-12); Close(40 * M / b / r.NMax, r.MBar, "M̄ = M/(B Nmax)", 1e-12);
        Close(1, Interaction(r, 1 / r.Ratio!.Value), "the capacity lies on the boundary of the domain", 1e-9);
        Close(Interaction(r, 1), r.Interaction!.Value, "interaction of the design loads", 1e-12);
        Assert.AreEqual(r.Ratio <= 1, r.Interaction <= 1, "ratio and interaction agree");
        Assert.AreEqual(r.Ratio, ShallowFoundationSeismic.Calculate(b, gamma, phi, 500, -60, -40 * M, .1, 0, 1, 1).Ratio, "signs of V and M are ignored");
        // A tiny vertical load with a horizontal force fails as well: the domain closes at N̄ → 0.
        Assert.AreEqual(SeismicBearingStatus.NotSatisfied, ShallowFoundationSeismic.Calculate(b, gamma, phi, .5, 60, 0, .1, 0, 1, 1).Status);
        // γRd multiplies F̄; when 1 − 0.96 F̄ ≤ 0 the soil can no longer carry its own inertia.
        Close(1.15 * .1 / Math.Tan(phi), ShallowFoundationSeismic.Calculate(b, gamma, phi, 500, 60, 0, .1, 0, 1.15, 1).SoilInertia, "γRd F̄", 1e-12);
        double kh = Math.Tan(phi) / .96;
        var exhausted = ShallowFoundationSeismic.Calculate(b, gamma, phi, 500, 60, 0, kh * 1.0001, 0, 1, 1);
        Assert.AreEqual(SeismicBearingStatus.DomainExhausted, exhausted.Status); Assert.AreEqual(0, exhausted.Capacity); Assert.IsNull(exhausted.Ratio); Assert.IsNull(exhausted.Interaction);
        Assert.AreNotEqual(SeismicBearingStatus.DomainExhausted, ShallowFoundationSeismic.Calculate(b, gamma, phi, 50, 1, 0, kh * .99, 0, 1, 1).Status);
    }

    [TestMethod]
    public void SeismicBearingDataAreChecked()
    {
        double b = 2 * M, g = 19 * KN3, phi = 34 * Deg;
        ShallowFoundationSeismic.Calculate(b, g, 45 * Deg, 300, 20, 10 * M, .1, 0, 1, 1);
        foreach (var (args, what) in new (double[] Args, string What)[]
        {
            (new[] { b, g, 45.001 * Deg, 300, 20, 10, .1, 0, 1, 1 }, "φ > 45°"), (new[] { b, g, 0, 300, 20, 10, .1, 0, 1, 1 }, "φ = 0"),
            (new[] { b, g, phi, 0, 20, 10, .1, 0, 1, 1 }, "N = 0"), (new[] { b, g, phi, -300, 20, 10, .1, 0, 1, 1 }, "tension"),
            (new[] { 0, g, phi, 300, 20, 10, .1, 0, 1, 1 }, "B = 0"), (new[] { b, 0, phi, 300, 20, 10, .1, 0, 1, 1 }, "γ = 0"),
            (new[] { b, g, phi, 300, 20, 10, -.1, 0, 1, 1 }, "kh < 0"), (new[] { b, g, phi, 300, 20, 10, .1, 1, 1, 1 }, "kv = 1"), (new[] { b, g, phi, 300, 20, 10, .1, -1, 1, 1 }, "kv = −1"),
            (new[] { b, g, phi, 300, 20, 10, .1, 0, .999, 1 }, "γRd < 1"), (new[] { b, g, phi, 300, 20, 10, .1, 0, 1, .999 }, "γR < 1"),
            (new[] { b, g, phi, 300, double.NaN, 10, .1, 0, 1, 1 }, "NaN V"), (new[] { b, g, phi, 300, 20, double.PositiveInfinity, .1, 0, 1, 1 }, "infinite M")
        })
            Assert.ThrowsException<ArgumentException>(() => ShallowFoundationSeismic.Calculate(args[0], args[1], args[2], args[3], args[4], args[5], args[6], args[7], args[8], args[9]), what);
    }
}
