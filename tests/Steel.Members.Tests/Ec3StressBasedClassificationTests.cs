using GPC.Checkers.Steel.Checkers;
using GPC.Checkers.Steel.Members;
using GPC.Geometry;
using GPC.Model;
using GPC.Model.Data.Steel;
using GPC.Model.Elements;
using GPC.Model.LoadCases;
using GPC.Model.Results;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Storage;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Opt = GPC.Checkers.Steel.Checkers.EN1993p11Checker.EN1993p11Options;

namespace Steel.Members.Tests;

[TestClass]
public class Ec3StressBasedClassificationTests
{
    public static IEnumerable<object[]> BoundaryCases =>
        from coefficient in new[] { 42d, 124d, 14d }
        from gamma in new[] { 1d, 1.05d }
        from offset in new[] { -1e-8, 0d, 1e-8 }
        select new object[] { coefficient, gamma, offset };

    [DataTestMethod, DynamicData(nameof(BoundaryCases))]
    public void StressBoundaryForInternalBendingAndOutstandParts(double coefficient, double gamma, double offset)
    {
        // fy=235 -> epsilon=1, c/t=2*class-3 limit.
        // Inverting the inequality independently gives sigma_limit=fy/(4*gamma).
        double stress = 235 / (4 * gamma) * (1 + offset);
        var result = Ec3StressBasedClassification.Evaluate(235, gamma,
            new Ec3CompressionPart(4, 2 * coefficient, coefficient, stress));
        Assert.AreEqual(offset <= 0 ? 3 : 4, result.SectionResistanceClass);
        Assert.AreEqual(4, result.NominalClass);
        Assert.AreEqual(4, result.MemberBucklingClass);
        double expectedLimit = 2 * coefficient / Math.Sqrt(1 + offset);
        Assert.AreEqual(expectedLimit, result.Parts[0].IncreasedClass3Limit, expectedLimit * 1e-12);
        Assert.AreEqual(offset <= 0, result.UsesClause5529);
    }

    [DataTestMethod]
    [DataRow(1500000d, 3)]
    [DataRow(2716050d, 3)]
    [DataRow(5727550d, 4)]
    [DataRow(6029000d, 4)]
    public void ExistingRhsLoadsUseStressInEachWall(double compression, int expected)
    {
        // Analytic hollow rectangle A=500*300-475*275=19375 mm2.
        double sigma = compression / 19375;
        var result = Ec3StressBasedClassification.Evaluate(355, 1,
            new Ec3CompressionPart(4, 38, 42, sigma),
            new Ec3CompressionPart(4, 38, 42, sigma),
            new Ec3CompressionPart(1, 22, 42, sigma),
            new Ec3CompressionPart(1, 22, 42, sigma));
        Assert.AreEqual(expected, result.SectionResistanceClass);
        Assert.AreEqual(4, result.MemberBucklingClass);
        // fy cancels in epsilon*sqrt(fy/sigma) for gamma=1.
        Assert.AreEqual(42 * Math.Sqrt(235 / sigma), result.Parts[0].IncreasedClass3Limit, 1e-12);
        Assert.AreEqual(4, result.Parts.Count);
    }

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public void LowStressNeverCreatesClass1Or2(int nominal)
    {
        var result = Ec3StressBasedClassification.Evaluate(235, 1,
            new Ec3CompressionPart(nominal, 20, 42, 1));
        Assert.AreEqual(nominal, result.SectionResistanceClass);
        Assert.AreEqual(nominal, result.MemberBucklingClass);
        Assert.IsFalse(result.UsesClause5529);
    }

    [TestMethod]
    public void OneUnqualifiedPartPreventsTheWholeSectionTreatment()
    {
        var result = Ec3StressBasedClassification.Evaluate(235, 1,
            new Ec3CompressionPart(4, 84, 42, 20),
            new Ec3CompressionPart(4, 84, 42, 100));
        Assert.IsTrue(result.Parts[0].CanTreatAsClass3);
        Assert.IsFalse(result.Parts[1].CanTreatAsClass3);
        Assert.AreEqual(4, result.SectionResistanceClass);
    }

    [TestMethod]
    public void IncreasingGammaM0CanRemoveEligibility()
    {
        var part = new Ec3CompressionPart(4, 84, 42, 58);
        Assert.AreEqual(3, Ec3StressBasedClassification.Evaluate(235, 1, part).SectionResistanceClass);
        Assert.AreEqual(4, Ec3StressBasedClassification.Evaluate(235, 1.05, part).SectionResistanceClass);
    }

    [TestMethod]
    public void StressAtOrAboveYieldDoesNotRelaxClass4()
    {
        foreach (double stress in new[] { 235d, 300d })
        {
            var result = Ec3StressBasedClassification.Evaluate(235, 1,
                new Ec3CompressionPart(4, 43, 42, stress));
            Assert.AreEqual(4, result.SectionResistanceClass);
            Assert.AreEqual(42d, result.Parts[0].IncreasedClass3Limit);
        }
    }

    public static IEnumerable<object[]> InvalidCases =>
        from field in new[] { "fy", "gamma", "ratio", "coefficient", "stress" }
        from value in new[] { 0d, -1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity }
        select new object[] { field, value };

    [DataTestMethod, DynamicData(nameof(InvalidCases))]
    public void RejectInvalidValuesWithoutTakingAbsoluteStress(string field, double value)
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Ec3StressBasedClassification.Evaluate(
            field == "fy" ? value : 235, field == "gamma" ? value : 1,
            new Ec3CompressionPart(4, field == "ratio" ? value : 84,
                field == "coefficient" ? value : 42, field == "stress" ? value : 50)));
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(5)]
    public void RejectInvalidNominalClass(int nominal)
        => Assert.ThrowsException<ArgumentOutOfRangeException>(() => new Ec3CompressionPart(nominal, 84, 42, 50));

    [TestMethod]
    public void RejectMissingParts()
    {
        Assert.ThrowsException<ArgumentNullException>(() => Ec3StressBasedClassification.Evaluate(235, 1, null!));
        Assert.ThrowsException<ArgumentException>(() => Ec3StressBasedClassification.Evaluate(235, 1));
        Assert.ThrowsException<ArgumentException>(() => Ec3StressBasedClassification.Evaluate(235, 1,
            new Ec3CompressionPart[] { null! }));
    }

    [DataTestMethod]
    [DataRow(4, 42d)]
    [DataRow(3, 43d)]
    public void RejectClassInconsistentWithNominalLimit(int nominal, double ratio)
        => Assert.ThrowsException<ArgumentException>(() => Ec3StressBasedClassification.Evaluate(235, 1,
            new Ec3CompressionPart(nominal, ratio, 42, 50)));

    [TestMethod]
    public void SectionTreatmentDoesNotEnableGrossSectionMemberBuckling()
    {
        double stress = 1500000d / 19375;
        var treatment = Ec3StressBasedClassification.Evaluate(355, 1,
            new Ec3CompressionPart(4, 38, 42, stress),
            new Ec3CompressionPart(4, 38, 42, stress),
            new Ec3CompressionPart(1, 22, 42, stress),
            new Ec3CompressionPart(1, 22, 42, stress));
        Assert.AreEqual(3, treatment.SectionResistanceClass);
        Assert.AreEqual(4, treatment.MemberBucklingClass);

        var beam = new BeamElement(Point3d.Origin, new Point3d(1000, 0, 0),
            new SteelSection(new SectionRHS(500, 300, 12.5, 12.5, 12.5, 12.5, ""), SteelMaterialEN1993Data.S355));
        beam.Results.Add(new BeamResult(new[] { new StationResultBeamForces(
            new LoadCase("Clause 5.5.2(10)", LoadCase.LoadCaseTypes.SelfWeight),
            new ResultBeamForces(-1500000, 0, 0, 0, 0, 0, CoordinateSystem.Global), 0) }));
        var checker = new EN1993p11Checker(beam,
            new Opt(Opt.LoadConditions.NotDirectlyLoaded, Opt.SupportConditions.EndsRestrained,
                Opt.LateralSupportConditions.HingesAtEnds, Opt.LateralWarpingConditions.HingesAtEnds, 1, 1),
            new StandardEN1993p11());
        checker.PerformCheck();
        var result = checker.EN1993p11BeamStationResults.Single();
        Assert.AreEqual(EN1993p11Checker.SectionClass.Class4, result.AxialCompressionClass);
        Assert.IsTrue(double.IsNaN(result.AxialBuckling1Capacity));
        Assert.IsTrue(double.IsNaN(result.AxialBuckling2Capacity));
        Assert.AreEqual(1, checker.GetErrorLog().Count);
        StringAssert.Contains(checker.GetErrorLog()[0], "Class 4 requires verified effective section properties");
    }
}
