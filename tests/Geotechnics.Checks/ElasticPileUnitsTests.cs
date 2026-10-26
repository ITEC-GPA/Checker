using System.Text.Json;
using GPC.Checkers.Geotechnics.Piles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GeotechnicsTests;

/// <summary>Same physical problems expressed in kN/m and N/mm, independently scaled by force/length dimensions.</summary>
[TestClass]
public class ElasticPileUnitsTests
{
    public TestContext TestContext { get; set; } = null!;
    public static IEnumerable<object[]> Problems()
    {
        foreach (ElasticSoilLaw law in Enum.GetValues<ElasticSoilLaw>())
            for (int boundary = 0; boundary < 4; boundary++)
                foreach (bool eccentricity in new[] { false, true })
                    foreach (double sign in new[] { -1d, 1d }) yield return new object[] { law, boundary, eccentricity, sign };
    }

    static ElasticPileInput Input(ElasticSoilLaw law = ElasticSoilLaw.ConstantKh, int boundary = 0, bool eccentricity = false, double sign = 1)
        => new()
        {
            Length = 12, FreeLength = 1.25, Diameter = 1, EI = 50000, EISource = "F4 dimensional reference", Step = .5,
            Force = sign * 100, HeadMoment = eccentricity ? 0 : sign * 20, Eccentricity = eccentricity ? .3 : 0,
            AxialHeadForce = -100, WeightPerLength = 20, AdditionalNodes = new[] { 2.75, 6.25 },
            Tip = boundary == 2 ? ElasticPileTip.Fixed : boundary == 3 ? ElasticPileTip.Pinned : ElasticPileTip.Free,
            FixedHeadRotation = boundary is 1 or 3,
            Layers = new[] {
                new ElasticPileLayer { Name = "A", OriginalLayer = 7, Thickness = 4.13, Law = law, Value = 10000, Source = "assigned" },
                new ElasticPileLayer { Name = "B", OriginalLayer = 9, Thickness = 6.62, Law = law, Value = 25000, Source = "assigned" } }
        };

    /// <summary>
    /// Head below the ground surface (request of the ANTHEA user of 19/10/2026): the layers are given from the ground and cut at the head. Constant laws
    /// do not depend on the depth, so the head 2 m down below a soft 2 m layer is the pile in the stiff layer alone, bit for bit, with the original
    /// layer number. The linear law uses the depth below the ground: at 3 m from a head 2 m down, k = kh,ref · 5. A free length with a buried head, a
    /// negative depth and layers that end above the head are rejected.
    /// </summary>
    [TestMethod]
    public void HeadBelowTheGroundSurface()
    {
        ElasticPileInput Buried(ElasticSoilLaw law, double head, params (double Thickness, double Value)[] layers) => new()
        {
            Length = 12, FreeLength = 0, HeadDepth = head, Diameter = 1, EI = 50000, EISource = "test", Step = .5, Force = 100, Tip = ElasticPileTip.Free,
            Layers = layers.Select((s, i) => new ElasticPileLayer { Name = "L" + (i + 1), Thickness = s.Thickness, Law = law, Value = s.Value, Source = "assigned" }).ToArray()
        };
        var buried = ElasticPile.Calculate(Buried(ElasticSoilLaw.ConstantKh, 2, (2, 1000), (20, 25000)));
        var alone = ElasticPile.Calculate(Buried(ElasticSoilLaw.ConstantKh, 0, (20, 25000)));
        Assert.AreEqual(alone.HeadDisplacement, buried.HeadDisplacement); Assert.AreEqual(alone.Extrema["M"].AbsoluteMaximum, buried.Extrema["M"].AbsoluteMaximum);
        Assert.IsTrue(buried.Points.All(p => p.Layer == 2), "original layer number kept");
        var point = ElasticPile.Calculate(Buried(ElasticSoilLaw.LinearKhReference, 2, (30, 5000))).Points.First(p => p.Depth == 3);
        Assert.AreEqual(5, point.GroundDepth, 1e-12, "depth below the ground"); Assert.AreEqual(5000 * 5, point.DistributedStiffness, 1e-9, "k = kh,ref (d + x)");
        var free = Buried(ElasticSoilLaw.ConstantKh, 2, (30, 5000)); free.FreeLength = 1;
        Assert.ThrowsException<ArgumentException>(() => ElasticPile.Calculate(free), "free length with a buried head");
        Assert.ThrowsException<ArgumentException>(() => ElasticPile.Calculate(Buried(ElasticSoilLaw.ConstantKh, -1, (30, 5000))), "negative depth");
        Assert.ThrowsException<ArgumentException>(() => ElasticPile.Calculate(Buried(ElasticSoilLaw.ConstantKh, 31, (30, 5000))), "layers above the head only");
    }

    // Dimensional reference in the tests, independent of any production conversion service:
    // x'=L*x; F'=F*F; M'=F*L*M; EI'=F*L²*EI; kh'=F/L³*kh; k'=F/L²*k.
    static ElasticPileInput Scale(ElasticPileInput p, double force = 1000, double length = 1000) => new()
    {
        Length = p.Length * length, FreeLength = p.FreeLength * length, Diameter = p.Diameter * length,
        Step = p.Step * length, EI = p.EI * force * length * length, EISource = p.EISource,
        Force = p.Force * force, HeadMoment = p.HeadMoment * force * length, Eccentricity = p.Eccentricity * length,
        AxialHeadForce = p.AxialHeadForce * force, WeightPerLength = p.WeightPerLength * force / length,
        AdditionalNodes = p.AdditionalNodes.Select(x => x * length).ToArray(), Tip = p.Tip, FixedHeadRotation = p.FixedHeadRotation,
        Layers = p.Layers.Select(x => new ElasticPileLayer { Name = x.Name, OriginalLayer = x.OriginalLayer, Law = x.Law,
            Source = x.Source, Thickness = x.Thickness * length,
            Value = x.Value * force / (length * length * (x.Law == ElasticSoilLaw.ConstantDistributed ? 1 : length)) }).ToArray()
    };

    // Fixed before running the bench: numerical comparison of equivalent FEM systems, not a normative tolerance.
    static void Near(double expected, double actual, string name) => Assert.AreEqual(expected, actual,
        2e-8 * Math.Max(1, Math.Max(Math.Abs(expected), Math.Abs(actual))), name);

    static void Compare(ElasticPileResult a, ElasticPileResult b)
    {
        Assert.AreEqual(a.Elements, b.Elements, "elements");
        Assert.AreEqual(a.Nodes.Count, b.Nodes.Count, "nodes");
        Near(a.HeadDisplacement, b.HeadDisplacement / 1000, "head displacement");
        Near(a.HeadRotation, b.HeadRotation, "head rotation");
        Near(a.AppliedHeadMoment, b.AppliedHeadMoment / 1e6, "applied moment");
        Near(a.HeadReactionMoment, b.HeadReactionMoment / 1e6, "head reaction moment");
        Near(a.TipReactionForce, b.TipReactionForce / 1000, "tip reaction force");
        Near(a.TipReactionMoment, b.TipReactionMoment / 1e6, "tip reaction moment");
        Near(a.SoilForce, b.SoilForce / 1000, "soil force");
        Near(a.SoilMomentAboutHead, b.SoilMomentAboutHead / 1e6, "soil moment");
        Near(a.ForceResidual, b.ForceResidual / 1000, "force residual");
        Near(a.MomentResidual, b.MomentResidual / 1e6, "moment residual");
        for (int i = 0; i < a.Nodes.Count; i++)
        {
            var x = a.Nodes[i]; var y = b.Nodes[i];
            Near(x.Depth, y.Depth / 1000, "node depth"); Near(x.TributaryTop, y.TributaryTop / 1000, "tributary top");
            Near(x.TributaryBottom, y.TributaryBottom / 1000, "tributary bottom"); Near(x.EquivalentSpring, y.EquivalentSpring, "spring F/L");
        }
        // Recovery adds roots of floating-point polynomials. A root arbitrarily close to an
        // endpoint can appear as a second sample in one unit system. Match every point in both
        // directions by physical position and material side; never discard numerical values.
        ElasticPilePoint Match(ElasticPilePoint point, ElasticPileResult other, double depthFactor)
        {
            var candidates = other.Points.Where(p => p.Layer == point.Layer && (point.Side == "Interior" || p.Side == point.Side));
            return candidates.OrderBy(p => Math.Abs(p.Depth / depthFactor - point.Depth)).First();
        }
        var pairs = a.Points.Select(p => (x: p, y: Match(p, b, 1000)))
            .Concat(b.Points.Select(p => (x: Match(p, a, .001), y: p)));
        foreach (var (x, y) in pairs)
        {
            Assert.AreEqual(x.Layer, y.Layer);
            Near(x.Depth, y.Depth / 1000, "point depth"); Near(x.GroundDepth, y.GroundDepth / 1000, "ground depth");
            Near(x.Displacement, y.Displacement / 1000, "point displacement"); Near(x.Rotation, y.Rotation, "rotation");
            Near(x.AxialForce, y.AxialForce / 1000, "axial force"); Near(x.Shear, y.Shear / 1000, "shear");
            Near(x.Moment, y.Moment / 1e6, "moment"); Near(x.SoilReaction, y.SoilReaction, "soil reaction F/L");
            Near(x.Kh, y.Kh * 1e6, "kh F/L3"); Near(x.DistributedStiffness, y.DistributedStiffness * 1000, "k F/L2");
            Assert.AreEqual(x.Nh.HasValue, y.Nh.HasValue); if (x.Nh.HasValue) Near(x.Nh.Value, y.Nh!.Value * 1e6, "nh F/L3");
        }
        foreach (var result in new[] { a, b })
        {
            Assert.AreEqual(result.Points.Count, result.SectionDemands.Count);
            for (int i = 0; i < result.Points.Count; i++)
            {
                var p = result.Points[i]; var d = result.SectionDemands[i];
                Assert.AreEqual(p.Depth, d.Depth); Assert.AreEqual(p.AxialForce, d.AxialForce);
                Assert.AreEqual(p.Shear, d.Shear); Assert.AreEqual(p.Moment, d.Moment); Assert.AreEqual(p.Side, d.Side);
            }
        }
        foreach (string key in a.Extrema.Keys)
        {
            double factor = key == "M" ? 1e6 : key is "N" or "V" or "y" ? 1000 : 1;
            var x = a.Extrema[key]; var y = b.Extrema[key];
            Near(x.Minimum, y.Minimum / factor, key + " min"); Near(x.Maximum, y.Maximum / factor, key + " max");
            Near(x.AbsoluteMaximum, y.AbsoluteMaximum / factor, key + " abs");
            double Value(ElasticPilePoint p) => key switch { "N" => p.AxialForce, "V" => p.Shear, "M" => p.Moment,
                "y" => p.Displacement, "theta" => p.Rotation, "q" => p.SoilReaction, _ => throw new ArgumentException(key) };
            void Attained(ElasticPileResult result, double depth, double value, double lengthFactor, double valueFactor, bool absolute = false)
            {
                var values = result.Points.Where(p => Math.Abs(p.Depth / lengthFactor - depth) <= 2e-8 * Math.Max(1, Math.Abs(depth)))
                    .Select(p => (absolute ? Math.Abs(Value(p)) : Value(p)) / valueFactor).ToArray();
                Assert.IsTrue(values.Length > 0, key + " extremum position represented");
                Near(value, values.OrderBy(v => Math.Abs(v - value)).First(), key + " extremum attained at the other reported position");
            }
            // On a constant shear plateau the argmax is not unique. Require both reported
            // positions to attain the same extreme in the other physical field.
            Attained(b, x.MinimumDepth, x.Minimum, 1000, factor); Attained(a, y.MinimumDepth / 1000, y.Minimum / factor, 1, 1);
            Attained(b, x.MaximumDepth, x.Maximum, 1000, factor); Attained(a, y.MaximumDepth / 1000, y.Maximum / factor, 1, 1);
            Attained(b, x.AbsoluteMaximumDepth, x.AbsoluteMaximum, 1000, factor, true);
            Attained(a, y.AbsoluteMaximumDepth / 1000, y.AbsoluteMaximum / factor, 1, 1, true);
        }
    }

    [DataTestMethod, DynamicData(nameof(Problems), DynamicDataSourceType.Method)]
    public void EveryOutputKeepsItsPhysicalUnits(ElasticSoilLaw law, int boundary, bool eccentricity, double sign)
    {
        var a = Input(law, boundary, eccentricity, sign); var b = Scale(a);
        string beforeA = JsonSerializer.Serialize(a), beforeB = JsonSerializer.Serialize(b);
        var first = ElasticPile.Calculate(a); var second = ElasticPile.Calculate(b);
        if (law == ElasticSoilLaw.LinearKhReference && boundary == 3 && !eccentricity && sign == -1)
        {
            Directory.CreateDirectory(TestContext.TestResultsDirectory!);
            File.WriteAllText(Path.Combine(TestContext.TestResultsDirectory!, "units-kNm.json"), JsonSerializer.Serialize(first));
            File.WriteAllText(Path.Combine(TestContext.TestResultsDirectory!, "units-Nmm.json"), JsonSerializer.Serialize(second));
        }
        Compare(first, second);
        Assert.AreEqual(beforeA, JsonSerializer.Serialize(a)); Assert.AreEqual(beforeB, JsonSerializer.Serialize(b));
    }

    [DataTestMethod]
    [DataRow(ElasticSoilLaw.ConstantKh)]
    [DataRow(ElasticSoilLaw.LinearKhReference)]
    [DataRow(ElasticSoilLaw.ConstantDistributed)]
    public void ConvergenceCriterionIsUnitInvariant(ElasticSoilLaw law)
    {
        var p = Input(law); var a = ElasticPile.CalculateWithConvergence(p); var b = ElasticPile.CalculateWithConvergence(Scale(p));
        Compare(a, b); Assert.AreEqual(a.MeshConverged, b.MeshConverged); Assert.AreEqual(a.ComparisonElements, b.ComparisonElements);
        foreach (string key in a.RelativeMeshChanges.Keys) Near(a.RelativeMeshChanges[key], b.RelativeMeshChanges[key], "mesh change " + key);
    }

    [DataTestMethod]
    [DataRow(0d)]
    [DataRow(-5e-11)]
    [DataRow(-2e-10)]
    [DataRow(5e-11)]
    [DataRow(2e-10)]
    public void LayerCoverageBoundaryHasTheSameDecisionInBothUnits(double relativeGap)
    {
        var p = Input(); p.FreeLength = 0; p.AdditionalNodes = Array.Empty<double>();
        p.Layers = new[] { new ElasticPileLayer { Thickness = p.Length * (1 + relativeGap), Value = 10000 } };
        string Decision(ElasticPileInput input)
        {
            try { ElasticPile.Calculate(input); return "calculated"; }
            catch (ArgumentException ex) { return ex.Message; }
        }
        string first = Decision(p), second = Decision(Scale(p));
        TestContext.WriteLine("Coverage delta {0:R}: {1}", relativeGap, first);
        Assert.AreEqual(first, second, "relative coverage/conditioning threshold");
        if (relativeGap >= -1e-10) Assert.AreEqual("calculated", first);
        if (relativeGap < -1e-10) StringAssert.Contains(first, "Layers must cover");
    }

    [DataTestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public void CoverageRoundingWithinToleranceDoesNotCreateATinyTipElement(bool millimetres, bool stratified)
    {
        var exact = Input(); exact.FreeLength = 0; exact.AdditionalNodes = Array.Empty<double>();
        exact.Layers = new[] { new ElasticPileLayer { Thickness = exact.Length, Value = 10000 } };
        var rounded = Input(); rounded.FreeLength = 0; rounded.AdditionalNodes = Array.Empty<double>();
        rounded.Layers = new[] { new ElasticPileLayer { Thickness = rounded.Length * (1 - 5e-11), Value = 10000 } };
        if (stratified)
        {
            exact.Layers = new[] { new ElasticPileLayer { Thickness = 4.13, Value = 10000 },
                new ElasticPileLayer { Thickness = exact.Length - 4.13, Value = 25000 } };
            rounded.Layers = new[] { new ElasticPileLayer { Thickness = 4.13, Value = 10000 },
                new ElasticPileLayer { Thickness = rounded.Length * (1 - 5e-11) - 4.13, Value = 25000 } };
        }
        if (millimetres) { exact = Scale(exact); rounded = Scale(rounded); }
        string before = JsonSerializer.Serialize(rounded);
        var expected = ElasticPile.Calculate(exact); var actual = ElasticPile.Calculate(rounded);
        // Same physical end within the existing tolerance: not a wider numerical comparison.
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.AreEqual(before, JsonSerializer.Serialize(rounded), "The user's layer must not be edited");
        Assert.AreEqual(stratified ? 25 : 24, actual.Elements);
        if (stratified) Assert.IsTrue(actual.Nodes.Any(n => n.Depth == 4.13 * (millimetres ? 1000 : 1)), "Internal interface retained");
    }
}
