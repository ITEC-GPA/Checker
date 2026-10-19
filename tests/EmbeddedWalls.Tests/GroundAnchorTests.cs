using System.Text.Json;
using GPC.Checkers.Geotechnics.Anchors;
using GPC.Model.Checking.Contracts;
using GPC.Model.Geotechnics.Anchors;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EmbeddedWalls.Tests;

[TestClass]
public sealed class GroundAnchorTests
{
    private static GroundAnchorBondLayer Sand(double pi = 3, string source = "PMT profile A, pl=2 MPa")
        => new(0, 12000, GroundAnchorSoilKind.MediumSand, 2, pi, 1.5, source);
    private static GroundAnchorBondLayer Clay => new(12000, 20000, GroundAnchorSoilKind.Clay, 1, 2, 1.8, "PMT profile A, pl=1 MPa");
    private static GroundAnchorBond Bond(double pi = 3, double cover = 6000, string execution = "IRS, injected quantities and grout quality documented",
        string applicability = "Isolated confined bulb in stable ground, complete investigation profile")
        => new("T1/A", 10000, 6000, 200, cover, GroundAnchorInjection.IRS, new[] { Sand(pi), Clay }, execution, applicability);
    private static GroundAnchorResistanceProfile Profile(int n, bool permanent = true, GroundAnchorEvidence evidence = GroundAnchorEvidence.CalculatedProfiles)
        => new StandardNTC2018Geotechnics().GroundAnchorProfile(GeotechnicalSituation.PersistentTransient, permanent, evidence, n);
    private static JsonElement Expected()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "anchor-expected.json")));
        return json.RootElement.Clone();
    }
    private static void Close(double expected, double actual) => Assert.AreEqual(expected, actual, Math.Max(1e-9, Math.Abs(expected) * 2e-12));

    [TestMethod]
    public void BondIntegratesOnlyCementedAxisUsingPressuremeterPressurePerLayer()
    {
        var fixture = Expected(); var actual = GroundAnchorPullout.Predict(Bond());
        Assert.IsTrue(actual.Applicable); Assert.AreEqual(2, actual.Segments.Count);
        Close(fixture.GetProperty("raw_total").GetDouble(), actual.Resistance!.Value);
        for (int i = 0; i < 2; i++) Close(fixture.GetProperty("segments")[i].GetDouble(), actual.Segments[i].Resistance);
        Close(10000, actual.Segments[0].Start); Close(16000, actual.Segments[1].End);
        Close(.25, actual.Segments[0].UnitResistance); Close(.18, actual.Segments[1].UnitResistance);
        Assert.AreEqual("SG1", actual.Segments[0].Curve); Assert.AreEqual("AL1", actual.Segments[1].Curve);
        Close(3, actual.Segments[0].InjectionPressure); Close(2, actual.Segments[0].LimitPressure);
        // Increasing injection pressure does not automatically increase the soil pressuremeter ordinate.
        Close(actual.Resistance.Value, GroundAnchorPullout.Predict(Bond(pi: 6)).Resistance!.Value);
    }

    [TestMethod]
    public void NtcAnchorFactorsAreDistinctFromPileFactorsAndUseWholeSamples()
    {
        var fixture = Expected(); var samples = fixture.GetProperty("calculated_samples").EnumerateArray()
            .Select((v, i) => new GroundAnchorResistanceSample("P" + i, v.GetDouble(), "Complete soil profile " + i)).ToArray();
        var result = GroundAnchorPullout.Check(400000, samples, Profile(2), "Site test confirmation report");
        Close(fixture.GetProperty("calculated_characteristic").GetDouble(), result.CharacteristicResistance!.Value);
        Close(fixture.GetProperty("calculated_permanent_design").GetDouble(), result.DesignResistance!.Value);
        Close(.816, result.Utilization!.Value); Assert.AreEqual(EngineeringOutcome.Satisfied, result.Outcome);
        Close(1.75, result.Profile!.MeanCorrelationFactor); Close(1.70, result.Profile.MinimumCorrelationFactor); Close(1.2, result.Profile.ResistanceFactor);
        samples[0] = samples[0] with { Resistance = 1 }; Assert.AreNotEqual(1, result.Samples[0].Resistance);
    }

    [TestMethod]
    public void PreliminaryTestsUseTheirOwnCorrelationFactorsAndTemporaryResistance()
    {
        var fixture = Expected(); var samples = fixture.GetProperty("measured_samples").EnumerateArray()
            .Select((v, i) => new GroundAnchorResistanceSample("T" + i, v.GetDouble(), "Preliminary test protocol " + i)).ToArray();
        var result = GroundAnchorPullout.Check(400000, samples, Profile(3, false, GroundAnchorEvidence.MeasuredAnchorTests), "Site protocol");
        Close(fixture.GetProperty("measured_characteristic").GetDouble(), result.CharacteristicResistance!.Value);
        Close(fixture.GetProperty("measured_temporary_design").GetDouble(), result.DesignResistance!.Value);
        Close(1.1, result.Profile!.ResistanceFactor); Close(1.3, result.Profile.MeanCorrelationFactor); Close(1.2, result.Profile.MinimumCorrelationFactor);
        for (int n = 1; n <= 7; n++)
        {
            Close(new[] { 1.8, 1.75, 1.7, 1.65, 1.6 }[Math.Min(n, 5) - 1], Profile(n).MeanCorrelationFactor);
            Close(new[] { 1.8, 1.7, 1.65, 1.6, 1.55 }[Math.Min(n, 5) - 1], Profile(n).MinimumCorrelationFactor);
            Close(new[] { 1.5, 1.4, 1.3 }[Math.Min(n, 3) - 1], Profile(n, false, GroundAnchorEvidence.MeasuredAnchorTests).MeanCorrelationFactor);
            Close(new[] { 1.5, 1.3, 1.2 }[Math.Min(n, 3) - 1], Profile(n, false, GroundAnchorEvidence.MeasuredAnchorTests).MinimumCorrelationFactor);
        }
    }

    [TestMethod]
    public void MissingConfirmationUnsupportedNormAndInsufficientCapacityRemainExplicit()
    {
        var samples = new[] { new GroundAnchorResistanceSample("P1", 180000, "Complete profile") };
        var tentative = GroundAnchorPullout.Check(50000, samples, Profile(1), "");
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, tentative.Outcome); Close(100000 / 1.2, tentative.DesignResistance!.Value);
        var fail = GroundAnchorPullout.Check(100000, samples, Profile(1), "");
        Assert.AreEqual(EngineeringOutcome.NotSatisfied, fail.Outcome);
        var unsupported = new StandardNTC2018Geotechnics().GroundAnchorProfile(GeotechnicalSituation.Seismic, true, GroundAnchorEvidence.CalculatedProfiles, 1);
        Assert.IsNull(unsupported);
        var unknown = GroundAnchorPullout.Check(10000, samples, unsupported, "test");
        Assert.AreEqual(EngineeringOutcome.NotEvaluated, unknown.Outcome); Assert.IsNull(unknown.DesignResistance);
    }

    [TestMethod]
    public void ExecutionGeometryAndProvenanceDoNotSilentlyPassApplicability()
    {
        foreach (var bond in new[] { Bond(1), Bond(cover: 4999), Bond(execution: ""), Bond(applicability: "") })
        {
            var prediction = GroundAnchorPullout.Predict(bond); Assert.IsFalse(prediction.Applicable);
            Assert.IsTrue(prediction.Diagnostics.Count > 0); Assert.IsNotNull(prediction.Resistance);
        }
        var missing = new GroundAnchorBond("T1", 10000, 4000, 200, 6000, GroundAnchorInjection.IRS,
            new[] { Sand(source: ""), Clay }, "execution", "scope");
        Assert.IsFalse(GroundAnchorPullout.Predict(missing).Applicable);
        var outside = new GroundAnchorBond("T1", 10000, 4000, 200, 6000, GroundAnchorInjection.IRS,
            new[] { new GroundAnchorBondLayer(10000, 14000, GroundAnchorSoilKind.Silt, 20, 20, 1.4, "PMT") }, "execution", "scope");
        var noChart = GroundAnchorPullout.Predict(outside); Assert.IsFalse(noChart.Applicable); Assert.IsNull(noChart.Resistance);
        var igu = new GroundAnchorBond("T1", 0, 4000, 200, 6000, GroundAnchorInjection.IGU,
            new[] { new GroundAnchorBondLayer(0, 4000, GroundAnchorSoilKind.MediumSand, 2, 1, 1.1, "PMT") }, "execution", "scope");
        var valid = GroundAnchorPullout.Predict(igu); Assert.IsTrue(valid.Applicable); Close(.20, valid.Segments[0].UnitResistance);
    }

    [TestMethod]
    public void InvalidInputsCannotProducePartialTotalsOrArtificialEvidenceCounts()
    {
        Assert.ThrowsException<ArgumentException>(() => new GroundAnchorBond("A", 0, 6000, 200, 6000, GroundAnchorInjection.IRS,
            new[] { new GroundAnchorBondLayer(0, 2000, GroundAnchorSoilKind.Clay, 1, 1, 1.8, "PMT"),
                new GroundAnchorBondLayer(3000, 6000, GroundAnchorSoilKind.Clay, 1, 1, 1.8, "PMT") }, "execution", "scope"));
        var samples = new[] { new GroundAnchorResistanceSample("P1", 100000, "profile") };
        Assert.ThrowsException<ArgumentException>(() => GroundAnchorPullout.Check(1000, samples, Profile(2), "test"));
        Assert.ThrowsException<ArgumentException>(() => GroundAnchorPullout.Check(-1, samples, Profile(1), "test"));
        Assert.ThrowsException<ArgumentException>(() => GroundAnchorPullout.Check(1, new[] { samples[0], samples[0] }, Profile(2), "test"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Profile(0));
        Assert.ThrowsException<ArgumentException>(() => new GroundAnchorBondLayer(0, 1000, GroundAnchorSoilKind.Clay, double.NaN, 1, 1, "PMT"));
    }

    [TestMethod]
    public void PredictionsAreOwnedCancellableAndIndependent()
    {
        var layers = new[] { Sand(), Clay };
        var bond = new GroundAnchorBond("A", 10000, 6000, 200, 6000, GroundAnchorInjection.IRS, layers, "execution", "scope");
        layers[0] = Sand(pi: 1); Assert.AreEqual(3, bond.Layers[0].InjectionPressure);
        Parallel.For(0, 12, _ => Close(Expected().GetProperty("raw_total").GetDouble(), GroundAnchorPullout.Predict(bond).Resistance!.Value));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.ThrowsException<OperationCanceledException>(() => GroundAnchorPullout.Predict(bond, cancellation.Token));
        Assert.ThrowsException<OperationCanceledException>(() => GroundAnchorPullout.Check(0,
            new[] { new GroundAnchorResistanceSample("A", 1000, "source") }, Profile(1), "source", cancellation.Token));
    }
}
