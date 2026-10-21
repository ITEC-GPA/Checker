using System.Runtime.Serialization;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Sections;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class SteelVerificationInputTests
{
    private static SteelDesignContext Ec3() => new(SectionResistanceCode.Eurocode3(1, 1.25, "explicit test parameters"));
    private static SteelSection Steel(Section? shape = null) => new(shape ?? new SectionH(300, 12, 150, 12, 150, 12, "H"),
        new SteelMaterial("S355 explicit", 210000, 355, 510));
    private static SteelSectionInput SectionInput() => new(Steel());

    [TestMethod]
    public void SectionOnlyRequestNeedsNoMemberOrStabilityFactor()
    {
        var input = new SteelVerificationInput(SectionInput(), new SteelActions(-100000, 0, 0, 0, 0, 0), Ec3());
        Assert.IsNull(input.Member);
        Assert.IsNull(input.Code.GammaM1);
        Assert.IsNull(input.Section.Unperforated);
        Assert.IsNull(input.Section.NetArea);
        Assert.AreEqual("2005", input.Code.SectionCode.Edition);
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void CommonInputFeedsExistingCompressionPrimitive(bool ntc)
    {
        // A sharp H: 2*150*12 + (300-24)*12 = 6912 mm2, explicitly supplied class 1.
        var input = new SteelVerificationInput(SectionInput(), new SteelActions(-100000, 0, 0, 0, 0, 0),
            ntc ? SteelDesignContext.Ntc2018() : Ec3());
        // The common input selects no classifier, including when the context is NTC.
        var result = SectionResistanceVerifier.Compression(input.Code.SectionCode, input.Actions.N,
            input.Section.Area, input.Section.YieldStrength, 1);
        Assert.AreEqual(SectionResistanceStatus.Passed, result.Status);
        Assert.AreEqual(6912 * 355 / (ntc ? 1.05 : 1), result.Resistance!.Value, 1e-6);
        Assert.AreEqual(100000 / result.Resistance.Value, result.Utilization!.Value, 1e-12);
    }

    [TestMethod]
    public void SourceGeometryAndMaterialChangesCannotChangeSnapshot()
    {
        var source = Steel();
        var originalMaterial = source.SteelMaterial;
        var input = new SteelSectionInput(source, true);
        ((SectionH)source.SectionShape).Height = 600;
        originalMaterial.Ni = .25;
        source.SteelMaterial = new SteelMaterial("replacement", 200000, 235, 360);
        Assert.AreEqual(6912, input.Area, 1e-9);
        Assert.AreEqual(355, input.YieldStrength);
        Assert.AreEqual(210000, input.ElasticModulus);
        Assert.AreEqual(.3, input.PoissonRatio);
        var copy = input.CreateSectionCopy();
        Assert.AreEqual(300, copy.Height);
        Assert.AreEqual(355, copy.SteelMaterial.Fyk);
        Assert.AreEqual(.3, copy.SteelMaterial.Ni);
    }

    [TestMethod]
    public void ExportedGeometryAndMaterialAreIndependentOnEveryAccess()
    {
        var input = SectionInput();
        var first = input.CreateSectionCopy();
        ((SectionH)first.SectionShape).Height = 500;
        first.SteelMaterial.Ni = .2;
        var second = input.CreateSectionCopy();
        Assert.AreNotSame(first, second);
        Assert.AreNotSame(first.SectionShape, second.SectionShape);
        Assert.AreNotSame(first.SteelMaterial, second.SteelMaterial);
        Assert.AreEqual(300, second.Height);
        Assert.AreEqual(.3, second.SteelMaterial.Ni);
        Assert.AreEqual(input.Area, second.Area);
    }

    public static IEnumerable<object[]> Families()
    {
        yield return new object[] { new SectionH(300, 8, 150, 12, 150, 12, "H") };
        yield return new object[] { new SectionT(200, 120, 8, 12, "T") };
        yield return new object[] { new SectionC(200, 8, 80, 12, 80, 12, "C") };
        yield return new object[] { new SectionL(100, 10, 100, 10, "L", 0, 0) };
        yield return new object[] { new SectionCHS(150, 8, "CHS") };
        yield return new object[] { new SectionRHS(200, 100, 8, 8, 8, 8, "RHS") };
        yield return new object[] { new SectionRHSRoundedCorners(200, 100, 8, 16, 8, "RHS rounded") };
    }

    [DataTestMethod, DynamicData(nameof(Families), DynamicDataSourceType.Method)]
    public void SnapshotPreservesFamilyMaterialAndManufacturingWithoutClaimingCoverage(Section shape)
    {
        var source = new SteelSection(shape, new SteelMaterialEN1993("S355", 210000, 355, 510),
            Section.SectionTypes.Welded, Section.FormedTypes.ColdFormed);
        var input = new SteelSectionInput(source);
        var copy = input.CreateSectionCopy();
        Assert.AreEqual(shape.GetType(), input.GeometryType);
        Assert.AreEqual(shape.GetType(), copy.SectionShape.GetType());
        Assert.AreEqual(typeof(SteelMaterialEN1993), copy.SteelMaterial.GetType());
        Assert.AreEqual(source.Area, copy.Area, 1e-9);
        Assert.AreEqual(source.AngleX1, input.PrincipalAxisAngle);
        Assert.AreEqual(Section.SectionTypes.Welded, copy.SectionType);
        Assert.AreEqual(Section.FormedTypes.ColdFormed, copy.FormedType);
    }

    [TestMethod]
    public void UnknownDerivedSectionIsRejectedRatherThanSliced()
    {
        Assert.ThrowsException<SerializationException>(() => new SteelSectionInput(new CustomSteelSection()));
    }

    private sealed class CustomSteelSection : SteelSection
    {
        public CustomSteelSection() : base(new SectionCHS(100, 5), new SteelMaterialEN1993("S235")) { }
    }

    [TestMethod]
    public void GeometricActionsRotateToPrincipalAxesForEqualAngle()
    {
        var section = new SteelSectionInput(Steel(new SectionL(100, 10, 100, 10, "L", 0, 0)));
        Assert.AreEqual(Math.PI / 4, section.PrincipalAxisAngle, 1e-12);
        var forces = new ResultBeamForces(-123, 10, 20, -30, 40, -50, CoordinateSystem.Global);
        var input = ModelSteelInput.FromSectionAxes(section, forces, Ec3());
        Assert.AreEqual(-123, input.Actions.N);
        Assert.AreEqual(-30, input.Actions.T);
        Assert.AreEqual(30 / Math.Sqrt(2), input.Actions.V1, 1e-12);
        Assert.AreEqual(10 / Math.Sqrt(2), input.Actions.V2, 1e-12);
        Assert.AreEqual(-10 / Math.Sqrt(2), input.Actions.M1, 1e-12);
        Assert.AreEqual(-90 / Math.Sqrt(2), input.Actions.M2, 1e-12);
        forces.N = 999; forces.M1 = 999;
        Assert.AreEqual(-123, input.Actions.N);
        Assert.AreEqual(-10 / Math.Sqrt(2), input.Actions.M1, 1e-12);
    }

    [TestMethod]
    public void AlignedAxesAndDirectPrincipalInputPreserveSignsAndUnits()
    {
        var section = SectionInput();
        Assert.AreEqual(0, section.PrincipalAxisAngle, 1e-12);
        var forces = new ResultBeamForces(-1000, -2000, 3000, -4000000, 5000000, -6000000, CoordinateSystem.Global);
        var input = ModelSteelInput.FromSectionAxes(section, forces, Ec3());
        CollectionAssert.AreEqual(new[] { -1000d, -2000, 3000, -4000000, 5000000, -6000000 },
            new[] { input.Actions.N, input.Actions.V1, input.Actions.V2, input.Actions.T, input.Actions.M1, input.Actions.M2 });
        var direct = new SteelVerificationInput(section, input.Actions, Ec3());
        Assert.AreSame(input.Actions, direct.Actions);
    }

    [TestMethod]
    public void MemberDataRemainSeparateAndUnknownValuesRemainUnknown()
    {
        var member = new SteelMemberInput(4000);
        var input = new SteelVerificationInput(SectionInput(), new SteelActions(0, 0, 0, 0, 0, 0), Ec3(), member);
        Assert.AreSame(member, input.Member);
        Assert.AreEqual(4000, member.Length);
        Assert.IsNull(member.BucklingLength1); Assert.IsNull(member.BucklingLength2);
        Assert.IsNull(member.TorsionalBucklingLength);
        Assert.IsNull(member.StartRestraints); Assert.IsNull(member.EndRestraints);
        Assert.IsNull(member.Imperfections);
        Assert.IsNull(input.Code.GammaM1);
        var partial = new SteelEndRestraints(translation1: SteelRestraintState.Fixed);
        Assert.AreEqual(SteelRestraintState.Unknown, partial.Rotation3);
        Assert.AreEqual(SteelRestraintState.Unknown, partial.Warping);
    }

    [TestMethod]
    public void ExplicitMemberDataDoNotInferOtherLengthsOrImperfections()
    {
        var start = new SteelEndRestraints(translation1: SteelRestraintState.Fixed,
            translation2: SteelRestraintState.Fixed, rotation3: SteelRestraintState.Fixed,
            rotation1: SteelRestraintState.Free, rotation2: SteelRestraintState.Free, warping: SteelRestraintState.Free);
        var imperfections = new SteelMemberImperfections(true, "analysis run 42", sinusoidalBow1: -10, sway1: 0);
        var member = new SteelMemberInput(4000, bucklingLength1: 2800, bucklingLength2: 8000,
            startRestraints: start, imperfections: imperfections);
        Assert.AreEqual(2800d, member.BucklingLength1);
        Assert.AreEqual(8000d, member.BucklingLength2); // Effective length need not be <= physical length.
        Assert.IsNull(member.TorsionalBucklingLength);
        Assert.IsNull(member.EndRestraints);
        Assert.IsTrue(member.Imperfections.IncludedInAnalysis);
        Assert.AreEqual(-10d, member.Imperfections.SinusoidalBow1);
        Assert.IsNull(member.Imperfections.SinusoidalBow2);
        Assert.AreEqual(0d, member.Imperfections.Sway1);
        Assert.IsNull(member.Imperfections.Sway2);
    }

    [TestMethod]
    public void CodeContextRetainsProvenanceAndUsesSeparateNtcPolicy()
    {
        var code = SectionResistanceCode.Eurocode3(1, 1.25, "project annex");
        var ec3 = new SteelDesignContext(code, 1.1);
        Assert.AreSame(code, ec3.SectionCode);
        Assert.AreEqual("project annex", ec3.SectionCode.ParameterSet);
        Assert.AreEqual(1.1d, ec3.GammaM1);
        var ntc = SteelDesignContext.Ntc2018();
        Assert.AreEqual(SectionResistanceStandard.Ntc2018, ntc.SectionCode.Standard);
        Assert.AreEqual(1.05, ntc.SectionCode.GammaM0);
        Assert.AreEqual(1.25, ntc.SectionCode.GammaM2);
        Assert.AreEqual(1.05d, ntc.GammaM1);
        Assert.ThrowsException<ArgumentException>(() => new SteelDesignContext(SectionResistanceCode.Ntc2018(), 1));
    }

    [TestMethod]
    public void NetAreaRequiresAnExplicitConsistentHoleDeclaration()
    {
        var source = Steel();
        var unknown = new SteelSectionInput(source, false);
        Assert.IsNull(unknown.NetArea);
        var perforated = new SteelSectionInput(source, false, 5000);
        Assert.AreEqual(5000d, perforated.NetArea);
        Assert.ThrowsException<ArgumentException>(() => new SteelSectionInput(source, true, 5000));
        Assert.ThrowsException<ArgumentException>(() => new SteelSectionInput(source, null, 5000));
        Assert.ThrowsException<ArgumentException>(() => new SteelSectionInput(source, false, 7000));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new SteelSectionInput(source, false, 0));
    }

    public static IEnumerable<object[]> NonFiniteActions()
        => from component in Enumerable.Range(0, 6)
           from value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }
           select new object[] { component, value };

    [DataTestMethod, DynamicData(nameof(NonFiniteActions), DynamicDataSourceType.Method)]
    public void NonFiniteActionComponentsAreRejected(int component, double value)
    {
        var a = new double[6]; a[component] = value;
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new SteelActions(a[0], a[1], a[2], a[3], a[4], a[5]));
    }

    [DataTestMethod]
    [DataRow(0d)] [DataRow(-1d)] [DataRow(double.NaN)] [DataRow(double.PositiveInfinity)]
    public void InvalidLengthsAndMemberFactorsAreRejected(double value)
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new SteelMemberInput(value));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new SteelMemberInput(4000, bucklingLength1: value));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new SteelMemberInput(4000, bucklingLength2: value));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new SteelMemberInput(4000, torsionalBucklingLength: value));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new SteelDesignContext(Ec3().SectionCode, value));
    }

    [TestMethod]
    public void InvalidMaterialRestraintsAndImperfectionsAreRejected()
    {
        var section = Steel(); section.SteelMaterial.Ni = .5;
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new SteelSectionInput(section));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new SteelEndRestraints(warping: (SteelRestraintState)99));
        Assert.ThrowsException<ArgumentException>(() => new SteelMemberImperfections(false, " "));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new SteelMemberImperfections(false, "input", sinusoidalBow1: double.NaN));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new SteelMemberImperfections(false, "input", sway2: double.PositiveInfinity));
    }

    [TestMethod]
    public void MissingMandatoryObjectsAreRejected()
    {
        var section = SectionInput(); var actions = new SteelActions(0, 0, 0, 0, 0, 0); var code = Ec3();
        Assert.ThrowsException<ArgumentNullException>(() => new SteelVerificationInput(null!, actions, code));
        Assert.ThrowsException<ArgumentNullException>(() => new SteelVerificationInput(section, null!, code));
        Assert.ThrowsException<ArgumentNullException>(() => new SteelVerificationInput(section, actions, null!));
        Assert.ThrowsException<ArgumentNullException>(() => new SteelSectionInput(null!));
        Assert.ThrowsException<ArgumentNullException>(() => new SteelDesignContext(null!));
        Assert.ThrowsException<ArgumentNullException>(() => ModelSteelInput.FromSectionAxes(section, null!, code));
    }

    [TestMethod]
    public void AcceptingASectionDoesNotEnableGrossAreaResistanceForClassFour()
    {
        // With an 8 mm web: c/t=276/8=34.5, greater than 42*sqrt(235/355).
        var section = new SteelSectionInput(Steel(new SectionH(300, 8, 150, 12, 150, 12, "slender H")));
        var input = new SteelVerificationInput(section, new SteelActions(-100000, 0, 0, 0, 0, 0), Ec3());
        var classification = Ec3UniformCompressionClassification.Evaluate(
            ModelCompressionGeometry.Create(section.CreateSectionCopy()), section.YieldStrength);
        Assert.AreEqual(4, classification.NominalClass);
        var result = SectionResistanceVerifier.Compression(input.Code.SectionCode, input.Actions.N,
            section.Area, section.YieldStrength, classification.NominalClass);
        Assert.AreEqual(SectionResistanceStatus.NotSupported, result.Status);
        Assert.IsNull(result.Resistance);
        Assert.IsNull(result.Utilization);
    }

    [TestMethod]
    public void RotationCannotIntroduceNonFiniteActions()
    {
        var section = new SteelSectionInput(Steel(new SectionL(100, 10, 100, 10, "L", 0, 0)));
        var forces = new ResultBeamForces(0, double.MaxValue, double.MaxValue, 0, 0, 0, CoordinateSystem.Global);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => ModelSteelInput.FromSectionAxes(section, forces, Ec3()));
    }
}
