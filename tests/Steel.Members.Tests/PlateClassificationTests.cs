using GPC.Checkers.Steel.Checkers;
using GPC.Checkers.Steel.Sections;
using GPC.Geometry;
using GPC.Model;
using GPC.Model.Elements;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Opt = GPC.Checkers.Steel.Checkers.EN1993p11Checker.EN1993p11Options;

namespace Steel.Members.Tests;

[TestClass]
public class PlateClassificationTests
{
    // EN1993-1-1:2005 Table5.2 sheet1. The limits below are independently
    // evaluated at epsilon=1; pure bending is also explicitly tabulated 72/83/124.
    // https://unaribas.com/wp-content/uploads/2021/04/eurocode-3.pdf pp42-43.
    public static IEnumerable<object[]> InternalBoundaries()
    {
        foreach (var row in new[] {
            new[] { .5, -1, 72d, 83, 124 },
            new[] { 1d, 1, 33, 38, 42 },
            new[] { .75, 0, 45.25714285714286, 52.114285714285714, 62.6865671641791 },
            new[] { .25, -2, 144, 166, 263.0437226013957 }
        })
        for (int boundary = 0; boundary < 3; boundary++)
        foreach (int side in new[] { -1, 0, 1 })
        {
            // Decimal tabulation does not represent general binary limits exactly;
            // exact equality is tested only for explicitly tabulated integer limits.
            if (side == 0 && (row[0] == .75 || (row[0] == .25 && boundary == 2))) continue;
            yield return new object[] { row[0], row[1], row[boundary + 2] * (1 + side * 1e-10), boundary + (side > 0 ? 2 : 1) };
        }
    }

    [DataTestMethod]
    [DynamicData(nameof(InternalBoundaries), DynamicDataSourceType.Method)]
    public void InternalPlateLimitsAndInclusiveBoundaries(double alpha, double psi, double ratio, int expected)
    {
        var result = Ec3PlateClassification.Internal(ratio, 1, 235, alpha, psi);
        Assert.IsTrue(result.Nominal.IsEvaluated); Assert.AreEqual(expected, result.Nominal.NominalClass);
        Assert.AreEqual(alpha, result.PlasticCompressedFraction); Assert.AreEqual(psi, result.ElasticStressRatio);
    }

    // EN1993-1-5:2006 Table4.2, reproduced JRC/ECCS EUR22898EN Table4.2 p50.
    // psi=0: k=.57 (free maximum), 1.70 (supported maximum).
    // psi=-1: k=.85 (free maximum), 23.8 (supported maximum).
    [DataTestMethod]
    [DataRow(true, 0d, 1d, .8, 11.25, 12.5, .57)]
    [DataRow(true, -1d, 1d, .5, 18d, 20d, .85)]
    [DataRow(false, 1d, 0d, .8, 12.577882373436317, 13.975424859373684, 1.7)]
    [DataRow(false, 1d, -1d, .5, 25.45584412271571, 28.2842712474619, 23.8)]
    public void OutstandLimitsDistinguishPlasticOrientationAndElasticEdges(bool tip, double root, double end,
        double alpha, double first, double second, double k)
    {
        var r = Ec3PlateClassification.Outstand(100, 10, 235, alpha, tip, root, end);
        Assert.AreEqual(first, r.Nominal.Class1Limit!.Value, 1e-12);
        Assert.AreEqual(second, r.Nominal.Class2Limit!.Value, 1e-12);
        Assert.AreEqual(21 * Math.Sqrt(k), r.Nominal.Class3Limit!.Value, 1e-12);
        Assert.AreEqual(tip, r.PlasticTipInCompression); Assert.AreEqual(root, r.SigmaRoot); Assert.AreEqual(end, r.SigmaTip);
    }

    [TestMethod]
    public void PublishedSciTeeClassificationRemainsClass4()
    {
        // SCI David Brown NSC Technical Digest2019 pp20-21 Method3: c/t25.2,
        // psi=-.207, k=.616 rounded, class3 limit13.3 at fy355. Conservative
        // full compressed plastic block cannot qualify this slender free stem.
        var r = Ec3PlateClassification.Outstand(252, 10, 355, 1, true, -.207, 1);
        Assert.AreEqual(4, r.Nominal.NominalClass);
        // The article prints epsilon=.81 and limit13.3 with coarse intermediate
        // rounding. Compare k to its published three decimals (half-unit tolerance),
        // and the class, without imposing that rounded limit on exact epsilon.
        double k = Math.Pow(r.Nominal.Class3Limit!.Value / (21 * r.Nominal.Epsilon!.Value), 2);
        Assert.AreEqual(.616, k, .0005);
    }

    [DataTestMethod]
    [DataRow(true, -4d, 1d, -3d)] [DataRow(false, 1d, -2d, -1d)]
    public void BeyondElasticDomainUsesAndReportsConservativeEndpoint(bool tip, double root, double end, double used)
    {
        var r = Ec3PlateClassification.Outstand(200, 10, 235, .5, tip, root, end);
        var endpoint = Ec3PlateClassification.Outstand(200, 10, 235, .5, tip, tip ? used : 1, tip ? 1 : used);
        Assert.IsTrue(r.UsesConservativeStressRatio); Assert.AreEqual(used, r.UsedElasticStressRatio);
        Assert.AreEqual(endpoint.Nominal.Class3Limit, r.Nominal.Class3Limit);
    }

    [DataTestMethod]
    [DataRow(235d)] [DataRow(355d)] [DataRow(460d)]
    public void PureBendingAndOutstandAgreeWithFacadeAndScale(double fy)
    {
        var probe = new Probe(fy);
        foreach (double ratio in new[] { 8d, 18, 30, 65, 72, 83, 90, 124, 150 })
        {
            var inner = Ec3PlateClassification.Internal(ratio, 1, fy, .5, -1);
            Assert.AreEqual(probe.Internal(ratio, 1), inner.Nominal.NominalClass);
            var outer = Ec3PlateClassification.Outstand(ratio, 1, fy, .5, false, 100, -100);
            Assert.AreEqual(probe.Outer(ratio, 1), outer.Nominal.NominalClass);
            var scaled = Ec3PlateClassification.Outstand(ratio * 1000, 1000, fy, .5, false, 1, -1);
            Assert.AreEqual(outer.Nominal.NominalClass, scaled.Nominal.NominalClass);
            Assert.AreEqual(outer.Nominal.Class3Limit, scaled.Nominal.Class3Limit);
        }
    }

    [TestMethod]
    public void UniformCompressionDoesNotUseGradientKForTheUniformEndpoint()
    {
        foreach (int ratio in new[] { 9, 10, 14, 15 })
        {
            var r = Ec3PlateClassification.Outstand(ratio, 1, 235, 1, true, 100, 100);
            var u = Ec3CompressionPartClassification.Evaluate(CompressionPartKind.OutstandFlat, ratio, 1, 235);
            Assert.AreEqual(u.NominalClass, r.Nominal.NominalClass); Assert.AreEqual(14d, r.Nominal.Class3Limit);
        }
    }

    [DataTestMethod]
    [DataRow(-.1)] [DataRow(1.1)] [DataRow(double.NaN)] [DataRow(double.PositiveInfinity)]
    public void InvalidPlasticFractionCannotProduceAClass(double alpha)
    {
        AssertInvalid(Ec3PlateClassification.Internal(100, 10, 235, alpha, -1));
        AssertInvalid(Ec3PlateClassification.Outstand(100, 10, 235, alpha, true, -1, 1));
    }

    [TestMethod]
    public void MissingInvalidAndExtremeStressInputsAreDiagnosed()
    {
        Assert.AreEqual(SectionClassificationStatus.MissingInput, Ec3PlateClassification.Internal(100, 10, 235, null, -1).Nominal.Status);
        Assert.AreEqual(SectionClassificationStatus.MissingInput, Ec3PlateClassification.Outstand(100, 10, 235, .5, true, null, 1).Nominal.Status);
        AssertInvalid(Ec3PlateClassification.Internal(100, 10, 235, .5, 1.01));
        AssertInvalid(Ec3PlateClassification.Outstand(100, 10, 235, .5, true, double.NaN, 1));
        AssertInvalid(Ec3PlateClassification.Internal(-100, 10, 235, .5, -1));
        Assert.AreEqual(SectionClassificationStatus.CalculationError,
            Ec3PlateClassification.Internal(100, 10, 235, double.Epsilon, -1).Nominal.Status);
        Assert.AreEqual(SectionClassificationStatus.CalculationError,
            Ec3PlateClassification.Outstand(100, 10, 235, .5, true, -double.MaxValue, double.Epsilon).Nominal.Status);
    }

    [TestMethod]
    public void UncompressedPartsHaveNoInventedCompressionLimits()
    {
        var a = Ec3PlateClassification.Internal(10000, 1, 235, 0, -1).Nominal;
        var b = Ec3PlateClassification.Outstand(10000, 1, 235, 0, true, -100, -10).Nominal;
        Assert.AreEqual(1, a.NominalClass); Assert.AreEqual(1, b.NominalClass);
        Assert.IsNull(a.Class3Limit); Assert.IsNull(b.Class3Limit);
        Assert.ThrowsException<ArgumentException>(() => new ClassifiedCompressionPart("tension", a, 10));
    }

    private static void AssertInvalid(PlateClassification r)
    { Assert.AreEqual(SectionClassificationStatus.InvalidInput, r.Nominal.Status); Assert.IsNull(r.Nominal.NominalClass); }

    private sealed class Probe : EN1993p11Checker
    {
        private readonly SteelSection s;
        internal Probe(double fy) : this(new SteelSection(new SectionH(300, 12, 150, 18, 150, 18, "test"), new SteelMaterial("test", 210000, fy, 2 * fy))) { }
        private Probe(SteelSection section) : base(new BeamElement(Point3d.Origin, new Point3d(3000, 0, 0), section),
            new Opt(Opt.LoadConditions.Constant, Opt.SupportConditions.HingesAtEnds, Opt.LateralSupportConditions.HingesAtEnds,
                Opt.LateralWarpingConditions.HingesAtEnds, 1, 1), new StandardEN1993p11()) { s = section; }
        internal int Internal(double c, double t) => (int)GetClassBendingInnerPlate(new ResultBeamForces(0, 0, 0, 0, 1e6, 0, CoordinateSystem.Global), c, t, s, SectionClass.Class1);
        internal int Outer(double c, double t) => (int)GetClassBendingOuterPlate(c, t, .5, false, 100, -100);
    }
}
