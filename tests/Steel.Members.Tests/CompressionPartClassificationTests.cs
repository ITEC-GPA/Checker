using GPC.Checkers.Steel.Checkers;
using GPC.Checkers.Steel.Sections;
using GPC.Geometry;
using GPC.Model.Data.Steel;
using GPC.Model.Elements;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Opt = GPC.Checkers.Steel.Checkers.EN1993p11Checker.EN1993p11Options;

namespace Steel.Members.Tests;

[TestClass]
public class CompressionPartClassificationTests
{
    // Independent oracle: EN1993-1-1:2005 + AC2006/2009 Table5.2,
    // printed pp42-44 / PDF pp44-46, consulted 2026-10-21.
    // https://unaribas.com/wp-content/uploads/2021/04/eurocode-3.pdf
    // These classify individual compressed parts, not complete sections/members.
    private static readonly double[][] PublishedLimits = { new[] { 33d, 38, 42 }, new[] { 9d, 10, 14 }, new[] { 50d, 70, 90 } };

    public static IEnumerable<object[]> BoundaryCases()
    {
        foreach (var kind in Enum.GetValues<CompressionPartKind>())
        foreach (double fy in new[] { 235d, 940d }) // epsilon=1 or1/2: exactly representable thresholds.
        for (int limit = 0; limit < 3; limit++)
        foreach (int side in new[] { -1, 0, 1 })
            yield return new object[] { kind, fy, limit, side };
    }

    [DataTestMethod, DynamicData(nameof(BoundaryCases), DynamicDataSourceType.Method)]
    public void PublishedThresholdIsInclusiveAndBothSidesAreClassified(CompressionPartKind kind, double fy, int limit, int side)
    {
        double scale = kind == CompressionPartKind.CircularHollow ? 235 / fy : Math.Sqrt(235 / fy);
        double ct = PublishedLimits[(int)kind][limit] * scale * (1 + side * 1e-10);
        var result = Ec3CompressionPartClassification.Evaluate(kind, ct * 8, 8, fy);
        Assert.AreEqual(SectionClassificationStatus.Completed, result.Status);
        Assert.AreEqual(limit + 1 + (side > 0 ? 1 : 0), result.NominalClass);
        Assert.AreEqual(ct, result.WidthThicknessRatio!.Value, ct * 1e-14);
    }

    public static IEnumerable<object[]> GradeCases()
    {
        double[][] ratios = { new[] { 20d, 35, 40, 45 }, new[] { 8d, 9.5, 12, 15 }, new[] { 30d, 60, 80, 100 } };
        foreach (var kind in Enum.GetValues<CompressionPartKind>())
        foreach (double fy in new[] { 235d, 275d, 355d, 420d, 460d })
        for (int cl = 1; cl <= 4; cl++)
            yield return new object[] { kind, fy, ratios[(int)kind][cl - 1], cl };
    }

    [DataTestMethod, DynamicData(nameof(GradeCases), DynamicDataSourceType.Method)]
    public void GradeAndGeometricScaleDoNotChangeTheExpectedClass(CompressionPartKind kind, double fy, double normalizedRatio, int expected)
    {
        double ct = normalizedRatio * (kind == CompressionPartKind.CircularHollow ? 235 / fy : Math.Sqrt(235 / fy));
        foreach (double t in new[] { .25, 8, 128 })
        {
            var result = Ec3CompressionPartClassification.Evaluate(kind, ct * t, t, fy);
            Assert.AreEqual(expected, result.NominalClass);
            Assert.AreEqual(fy, result.YieldStrength); Assert.AreEqual(t, result.Thickness);
            Assert.AreEqual(ct * t, result.WidthOrDiameter);
        }
    }

    [DataTestMethod]
    [DataRow(0)] [DataRow(1)] [DataRow(2)]
    public void MissingInputIsNotAClass(int missing)
    {
        double?[] input = { 100d, 10d, 235d }; input[missing] = null;
        var result = Ec3CompressionPartClassification.Evaluate(CompressionPartKind.InternalFlat, input[0], input[1], input[2]);
        Assert.AreEqual(SectionClassificationStatus.MissingInput, result.Status);
        AssertUnavailable(result);
    }

    [DataTestMethod]
    [DataRow(0, 0d)] [DataRow(0, -1d)] [DataRow(0, double.NaN)] [DataRow(0, double.PositiveInfinity)]
    [DataRow(1, 0d)] [DataRow(1, -1d)] [DataRow(1, double.NaN)] [DataRow(1, double.PositiveInfinity)]
    [DataRow(2, 0d)] [DataRow(2, -1d)] [DataRow(2, double.NaN)] [DataRow(2, double.PositiveInfinity)]
    public void InvalidInputDoesNotSilentlyBecomeClassOneOrFour(int invalid, double value)
    {
        double[] input = { 100d, 10d, 235d }; input[invalid] = value;
        var result = Ec3CompressionPartClassification.Evaluate(CompressionPartKind.InternalFlat, input[0], input[1], input[2]);
        Assert.AreEqual(SectionClassificationStatus.InvalidInput, result.Status);
        AssertUnavailable(result);
    }

    [DataTestMethod]
    [DataRow(50d)] [DataRow(60d)]
    public void ChsRequiresAnActualHollowCore(double t)
    {
        var result = Ec3CompressionPartClassification.Evaluate(CompressionPartKind.CircularHollow, 100, t, 235);
        Assert.AreEqual(SectionClassificationStatus.InvalidInput, result.Status);
        Assert.AreEqual("Chs.NoHollowCore", result.DiagnosticCode);
        AssertUnavailable(result);
    }

    [TestMethod]
    public void UnknownKindAndNumericalOverflowRemainExplicit()
    {
        Assert.AreEqual(SectionClassificationStatus.InvalidInput, Ec3CompressionPartClassification.Evaluate((CompressionPartKind)99, 100, 10, 235).Status);
        foreach (var result in new[] {
            Ec3CompressionPartClassification.Evaluate(CompressionPartKind.InternalFlat, double.MaxValue, double.Epsilon, 235),
            Ec3CompressionPartClassification.Evaluate(CompressionPartKind.InternalFlat, double.Epsilon, double.MaxValue, 235),
            Ec3CompressionPartClassification.Evaluate(CompressionPartKind.InternalFlat, 100, 10, double.Epsilon) })
        {
            Assert.AreEqual(SectionClassificationStatus.CalculationError, result.Status);
            AssertUnavailable(result);
        }
    }

    [TestMethod]
    public void CompletedClassFourRetainsAnImmutableReproducibleTrace()
    {
        var result = Ec3CompressionPartClassification.Evaluate(CompressionPartKind.InternalFlat, 430, 10, 235);
        Assert.IsTrue(result.IsEvaluated); Assert.AreEqual(4, result.NominalClass); Assert.IsNull(result.DiagnosticCode);
        Assert.AreEqual(43d, result.WidthThicknessRatio); Assert.AreEqual(1d, result.Epsilon);
        Assert.AreEqual(33d, result.Class1Limit); Assert.AreEqual(38d, result.Class2Limit); Assert.AreEqual(42d, result.Class3Limit);
        Assert.AreEqual("EN 1993-1-1:2005 Table 5.2", result.Reference);
        _ = Ec3CompressionPartClassification.Evaluate(CompressionPartKind.InternalFlat, 100, 10, 355);
        Assert.AreEqual(4, result.NominalClass); Assert.AreEqual(43d, result.WidthThicknessRatio);
    }

    [DataTestMethod]
    [DataRow(235d)] [DataRow(275d)] [DataRow(355d)] [DataRow(420d)] [DataRow(460d)]
    public void FacadeAndNewContractAgreeAcrossAllThreePartKinds(double fy)
    {
        var probe = new Probe(fy);
        foreach (var kind in Enum.GetValues<CompressionPartKind>())
        foreach (double ct in new[] { 3, 8, 9, 10, 14, 20, 33, 38, 42, 50, 70, 90, 120d })
        {
            var result = Ec3CompressionPartClassification.Evaluate(kind, ct * 8, 8, fy);
            Assert.AreEqual(result.NominalClass, probe.Classify(kind, ct * 8, 8));
        }
    }

    private static void AssertUnavailable(CompressionPartClassification result)
    {
        Assert.IsFalse(result.IsEvaluated); Assert.IsNull(result.NominalClass);
        Assert.IsNull(result.WidthThicknessRatio); Assert.IsNull(result.Class3Limit);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.DiagnosticCode));
    }

    private sealed class Probe : EN1993p11Checker
    {
        internal Probe(double fy) : base(new BeamElement(Point3d.Origin, new Point3d(3000, 0, 0),
            new SteelSection(new SectionH(300, 12, 150, 18, 150, 18, "test"), new SteelMaterial("test", 210000, fy, 2 * fy))),
            new Opt(Opt.LoadConditions.Constant, Opt.SupportConditions.HingesAtEnds, Opt.LateralSupportConditions.HingesAtEnds,
                Opt.LateralWarpingConditions.HingesAtEnds, 1, 1), new StandardEN1993p11()) { }
        internal int Classify(CompressionPartKind kind, double c, double t) => (int)(kind switch {
            CompressionPartKind.InternalFlat => GetClassCompressedInnerPlate(c, t),
            CompressionPartKind.OutstandFlat => GetClassCompressedOuterPlate(c, t),
            _ => GetClassCHSBending(c, t)
        });
    }
}
