using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Inputs;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class PackingFaceActionAdapterTests
{
    private static SectionBuiltUp Pair(string kind = "L-vertical", double gap = 12)
    {
        if (kind == "C") return SectionBuiltUp.DoubleChannel(new SectionC(200, 10, 75, 12, 75, 12, "C"), gap, true, "2C");
        var angle = new SectionL(100, 8, 75, 8, "L");
        return SectionBuiltUp.DoubleAngle(angle, gap, kind == "L-horizontal"
            ? SectionBuiltUp.AnglePairOrientation.HorizontalLegsBackToBack
            : SectionBuiltUp.AnglePairOrientation.VerticalLegsBackToBack, "2L");
    }
    private static SteelVerificationInput Input(Section? shape = null, bool ntc = false)
        => new(new SteelSectionInput(new SteelSection(shape ?? Pair(), new SteelMaterial("S275", 210000, 275, 430))),
            new SteelActions(-100000, 0, 0, 0, 0, 0), ntc ? SteelDesignContext.Ntc2018()
                : new SteelDesignContext(GPC.Checkers.Steel.Sections.SectionResistanceCode.Eurocode3(1, 1.25, "Test"), 1),
            new SteelMemberInput(3000, 3000, 3000));
    private static PlanarConnectionActions Zero() => new(0, 0, 0, 0, 0, 0);
    private static PackingFaceActionState Face(SteelVerificationInput input, int part, PlanarConnectionActions a,
        string state = "ULS01/local-imperfection-positive", string source = "Explicit local equilibrium model, one concomitant state")
        => new(input, state, source, part, a);
    private static PackingFaceActionAdapterResult Adapt(SteelVerificationInput input,
        PlanarConnectionActions a, PlanarConnectionActions b, double station = 100, double transverse = 30,
        double width = 20, double length = 40)
        => PackingFaceActionAdapter.FromModel(station, transverse, width, length, Face(input, 0, a), Face(input, 1, b));
    private static void Equal(double expected, double actual)
        => Assert.AreEqual(expected, actual, Math.Max(1e-9, Math.Abs(expected) * 1e-11));
    private static void Actions(PlanarConnectionActions a, double fx, double fy, double fz, double mx, double my, double mz)
    {
        Equal(fx, a.Fx); Equal(fy, a.Fy); Equal(fz, a.Fz); Equal(mx, a.Mx); Equal(my, a.My); Equal(mz, a.Mz);
        Equal(0, a.ApplicationX); Equal(0, a.ApplicationY); Equal(0, a.ApplicationZ);
    }

    [DataTestMethod]
    [DataRow("L-vertical", false)] [DataRow("L-horizontal", false)] [DataRow("C", false)]
    [DataRow("L-vertical", true)] [DataRow("L-horizontal", true)] [DataRow("C", true)]
    public void PhysicalBacksGiveRightHandedFaceFramesForBothNormContexts(string kind, bool ntc)
    {
        var r = Adapt(Input(Pair(kind), ntc), Zero(), Zero()); bool h = kind == "L-horizontal";
        Equal(12, r.PackingThickness); Equal(20, r.FaceWidth); Equal(40, r.FaceLength);
        Equal(h ? 30 : 0, r.BlockCentre.X); Equal(h ? 0 : 30, r.BlockCentre.Y); Equal(100, r.BlockCentre.Z);
        for (int i = 0; i < 2; ++i)
        {
            int sign = i == 0 ? 1 : -1; var f = r.Faces[i].LocalAxes;
            Equal(h ? 30 : sign * 6, f.Origin.X); Equal(h ? sign * 6 : 30, f.Origin.Y);
            Equal(0, f.V1.X); Equal(0, f.V1.Y); Equal(1, f.V1.Z);
            Equal(h ? sign : 0, f.V2.X); Equal(h ? 0 : -sign, f.V2.Y); Equal(0, f.V2.Z);
            Equal(h ? 0 : sign, f.V3.X); Equal(h ? sign : 0, f.V3.Y); Equal(0, f.V3.Z);
            var cross = f.V1.CrossProduct(f.V2);
            Equal(f.V3.X, cross.X); Equal(f.V3.Y, cross.Y); Equal(f.V3.Z, cross.Z);
        }
    }

    [DataTestMethod]
    [DataRow("L-vertical")] [DataRow("L-horizontal")] [DataRow("C")]
    public void AllSixComponentsAndApplicationOffsetsMatchIndependentHandCalculation(string kind)
    {
        // Unit-free hand cross product at gap12: for the positive X back,
        // Fg=(30,-20,10), Mg=(60,-50,40), r=(10,-3,2),
        // r x F=(10,-40,-110), hence Mg,centre=(70,-90,-70).
        var r = Adapt(Input(Pair(kind)), new(10, 20, 30, 40, 50, 60, 2, 3, 4),
            new(-10, 20, 30, 190, 150, 70));
        Actions(r.Faces[0].ActionsOnBlockAtFaceCentre, 10, 20, 30, 50, 30, 70);
        if (kind == "L-horizontal")
        {
            Actions(r.Faces[0].ActionsOnBlockAtBlockCentre, 20, 30, 10, 90, 70, -70);
            Actions(r.Faces[1].ActionsOnBlockAtBlockCentre, -20, -30, -10, -90, -70, 70);
        }
        else
        {
            Actions(r.Faces[0].ActionsOnBlockAtBlockCentre, 30, -20, 10, 70, -90, -70);
            Actions(r.Faces[1].ActionsOnBlockAtBlockCentre, -30, 20, -10, -70, 90, 70);
        }
        Assert.IsTrue(r.IsInEquilibrium); Actions(r.ResidualAtBlockCentre, 0, 0, 0, 0, 0, 0);
    }

    [DataTestMethod]
    [DataRow("L-vertical")] [DataRow("L-horizontal")] [DataRow("C")]
    public void OppositeAxialFaceForcesRetainTheUnbalancedThicknessCouple(string kind)
    {
        var r = Adapt(Input(Pair(kind)), new(1000, 0, 0, 0, 0, 0), new(-1000, 0, 0, 0, 0, 0));
        Assert.IsFalse(r.IsInEquilibrium);
        Actions(r.ResidualAtBlockCentre, 0, 0, 0, kind == "L-horizontal" ? 12000 : 0,
            kind == "L-horizontal" ? 0 : -12000, 0);
    }

    [DataTestMethod]
    [DataRow("L-vertical")] [DataRow("L-horizontal")] [DataRow("C")]
    public void ExplicitFaceCouplesCanBalanceTheThicknessCoupleWithoutInventingThem(string kind)
    {
        var r = Adapt(Input(Pair(kind)), new(1000, 0, 0, 0, -6000, 0), new(-1000, 0, 0, 0, 6000, 0));
        Assert.IsTrue(r.IsInEquilibrium); Actions(r.ResidualAtBlockCentre, 0, 0, 0, 0, 0, 0);
        Equal(-6000, r.Faces[0].ActionsOnBlockAtFaceCentre.My);
        Equal(6000, r.Faces[1].ActionsOnBlockAtFaceCentre.My);
    }

    [DataTestMethod]
    [DataRow(-1000.0)] [DataRow(1000.0)]
    public void CollinearNormalForcesBalanceButDoNotQualifyContactOrTensionResistance(double normal)
    {
        var r = Adapt(Input(), new(0, 0, normal, 0, 0, 0), new(0, 0, normal, 0, 0, 0));
        Assert.IsTrue(r.IsInEquilibrium); Actions(r.ResidualAtBlockCentre, 0, 0, 0, 0, 0, 0);
        StringAssert.Contains(r.Scope, "No external actions"); StringAssert.Contains(r.Scope, "no resistance");
    }

    [TestMethod]
    public void OppositeTransverseFaceForcesRetainTorqueAboutTheMemberAxis()
    {
        var r = Adapt(Input(), new(0, 1000, 0, 0, 0, 0), new(0, 1000, 0, 0, 0, 0));
        Assert.IsFalse(r.IsInEquilibrium); Actions(r.ResidualAtBlockCentre, 0, 0, 0, 0, 0, -12000);
    }

    [DataTestMethod]
    [DataRow(0)] [DataRow(1)] [DataRow(2)] [DataRow(3)] [DataRow(4)] [DataRow(5)]
    public void EachIndependentUnbalancedForceOrMomentRemainsVisible(int component)
    {
        var v = new double[6]; v[component] = 1;
        var r = Adapt(Input(), new(v[0], v[1], v[2], v[3], v[4], v[5]), Zero());
        Assert.IsFalse(r.IsInEquilibrium);
        var f = r.ResidualAtBlockCentre;
        Assert.IsTrue(new[] { f.Fx, f.Fy, f.Fz, f.Mx, f.My, f.Mz }.Any(x => x != 0));
    }

    [TestMethod]
    public void AForceResidualIsNotHiddenByAnUnrelatedLargeMoment()
    {
        var r = Adapt(Input(), new(1, 0, 0, 1e12, 0, 0), new(0, 0, 0, -1e12, 0, 0));
        Assert.IsFalse(r.IsInEquilibrium); Equal(1, r.ResidualAtBlockCentre.Fz);
    }

    [TestMethod]
    public void ZeroIsOnlyAnExplicitSuppliedStateAndNeverAnInferredHalfMemberForce()
    {
        var r = Adapt(Input(), Zero(), Zero());
        Equal(-100000, r.SourceInput.Actions.N); Assert.IsTrue(r.IsInEquilibrium);
        Actions(r.Faces[0].ActionsOnBlockAtFaceCentre, 0, 0, 0, 0, 0, 0);
        Actions(r.Faces[1].ActionsOnBlockAtFaceCentre, 0, 0, 0, 0, 0, 0);
    }

    [TestMethod]
    public void FaceParameterOrderDoesNotChangePhysicalPartIdentity()
    {
        var input = Input(); var a = Face(input, 0, new(1000, 0, 0, 0, -6000, 0));
        var b = Face(input, 1, new(-1000, 0, 0, 0, 6000, 0));
        var r = PackingFaceActionAdapter.FromModel(100, 30, 20, 40, b, a);
        Assert.AreSame(a, r.Faces[0].Source); Assert.AreSame(b, r.Faces[1].Source); Assert.IsTrue(r.IsInEquilibrium);
    }

    [TestMethod]
    public void ReversingModelPartOrderDoesNotAssumeFirstFaceIsOnPositiveSide()
    {
        var pair = Pair(); var input = Input(new SectionBuiltUp(pair.Parts.Reverse(), "reversed physical parts"));
        var r = Adapt(input, Zero(), Zero());
        Equal(-6, r.Faces[0].LocalAxes.Origin.X); Equal(-1, r.Faces[0].LocalAxes.V3.X);
        Equal(6, r.Faces[1].LocalAxes.Origin.X); Equal(1, r.Faces[1].LocalAxes.V3.X);
        Assert.IsTrue(r.IsInEquilibrium);
    }

    [DataTestMethod]
    [DataRow("input")] [DataRow("state")] [DataRow("source")]
    public void DifferentCombinationsImperfectionStatesOrSourceModelsCannotBeMixed(string difference)
    {
        var input = Input(); var a = Face(input, 0, Zero());
        var b = Face(difference == "input" ? Input() : input, 1, Zero(),
            difference == "state" ? "ULS01/local-imperfection-negative" : a.LocalStateId,
            difference == "source" ? "Independent envelope from a different run" : a.LocalAnalysisSource);
        Assert.ThrowsException<ArgumentException>(() => PackingFaceActionAdapter.FromModel(100, 30, 20, 40, a, b));
    }

    [TestMethod]
    public void BothPhysicalFacesMustBeProvidedExactlyOnce()
    {
        var input = Input(); var a = Face(input, 0, Zero());
        Assert.ThrowsException<ArgumentException>(() => PackingFaceActionAdapter.FromModel(100, 30, 20, 40, a, a));
    }

    [DataTestMethod]
    [DataRow("state")] [DataRow("source")]
    public void LocalModelProvenanceAndStateIdentifierAreMandatory(string missing)
        => Assert.ThrowsException<ArgumentException>(() => Face(Input(), 0, Zero(),
            missing == "state" ? " " : "ULS01", missing == "source" ? " " : "local model"));

    [TestMethod]
    public void TranslatedAndReflectedModelPlacementPreservesDemandAtBlockCentre()
    {
        var shape = Pair(); var parts = shape.Parts.Select(p => new SectionBuiltUp.Part(p.Section,
            p.X + 123.5, 40, p.MirrorX, true)).ToArray();
        var input = Input(new SectionBuiltUp(parts, "translated reflected pair"));
        var r = Adapt(input, new(10, 20, 30, 40, 50, 60, 2, 3, 4), new(-10, 20, 30, 190, 150, 70), station: 150);
        Equal(123.5, r.BlockCentre.X); Equal(10, r.BlockCentre.Y); Equal(150, r.BlockCentre.Z);
        Actions(r.Faces[0].ActionsOnBlockAtBlockCentre, 30, -20, 10, 70, -90, -70);
        Assert.IsTrue(r.IsInEquilibrium);
    }

    [TestMethod]
    public void UnequalAnglePhysicalLongAndShortBacksAreSelectedByModelNotPrincipalAxes()
    {
        var l = new SectionL(100, 8, 75, 8, "unequal");
        var longer = Adapt(Input(SectionBuiltUp.DoubleAngle(l, 12, SectionBuiltUp.AnglePairJoinedLeg.Long, "long")), Zero(), Zero(), transverse: 80);
        Equal(80, longer.BlockCentre.X); Equal(0, longer.BlockCentre.Y);
        Assert.ThrowsException<ArgumentException>(() => Adapt(Input(SectionBuiltUp.DoubleAngle(l, 12,
            SectionBuiltUp.AnglePairJoinedLeg.Short, "short")), Zero(), Zero(), transverse: 80));
    }

    [TestMethod]
    public void MutatingExportedFramesPointsAndOriginalModelCannotAlterTheSnapshot()
    {
        var pair = Pair(); var input = Input(pair); var r = Adapt(input, Zero(), Zero());
        r.BlockCentre.X = 999; var frame = r.Faces[0].LocalAxes; frame.Origin.X = 888; frame.V1.X = 77;
        ((SectionL)pair.Parts[0].Section).HorizontalLegLength = 400;
        Equal(0, r.BlockCentre.X); Equal(6, r.Faces[0].LocalAxes.Origin.X); Equal(0, r.Faces[0].LocalAxes.V1.X);
        Equal(20, r.FaceWidth); Equal(12, r.PackingThickness);
    }

    [TestMethod]
    public void ToeToToeChannelsAreNotReinterpretedAsPackedBacks()
    {
        var c = new SectionC(200, 10, 75, 12, 75, 12, "C");
        Assert.ThrowsException<NotSupportedException>(() => Adapt(Input(SectionBuiltUp.DoubleChannel(c, 12, false, "toe-to-toe")), Zero(), Zero()));
    }

    [TestMethod]
    public void TwoDifferentComponentInstancesAreNotAssumedIdentical()
    {
        var pair = new SectionBuiltUp(new[] { new SectionBuiltUp.Part(new SectionL(100, 8, 75, 8, "a"), 6, 0),
            new SectionBuiltUp.Part(new SectionL(100, 8, 75, 8, "b"), -6, 0, true) }, "unshared");
        Assert.ThrowsException<NotSupportedException>(() => Adapt(Input(pair), Zero(), Zero()));
    }

    [TestMethod]
    public void ContactingProfilesDoNotCreateAFictitiousZeroThicknessPacking()
        => Assert.ThrowsException<ArgumentOutOfRangeException>(() => Adapt(Input(Pair(gap: 0)), Zero(), Zero()));

    [TestMethod]
    public void GeometryOnlyAdapterDoesNotRepeatOrBypassPackedMemberGapQualification()
    {
        var r = Adapt(Input(Pair(gap: 40)), Zero(), Zero());
        Equal(40, r.PackingThickness); StringAssert.Contains(r.Scope, "No external actions");
        StringAssert.Contains(r.Scope, "no resistance, spacing, member-stability");
    }

    [DataTestMethod]
    [DataRow(19.0, 30.0, 20.0, 40.0)] [DataRow(2981.0, 30.0, 20.0, 40.0)]
    [DataRow(100.0, 9.0, 20.0, 40.0)] [DataRow(100.0, 66.0, 20.0, 40.0)]
    public void EntireBlockMustFitThePhysicalBackAndMemberRange(double station, double transverse, double width, double length)
        => Assert.ThrowsException<ArgumentException>(() => Adapt(Input(), Zero(), Zero(), station, transverse, width, length));

    [DataTestMethod]
    [DataRow(0.0)] [DataRow(-1.0)] [DataRow(double.NaN)] [DataRow(double.PositiveInfinity)]
    public void InvalidPackingDimensionsAreRejected(double width)
        => Assert.ThrowsException<ArgumentOutOfRangeException>(() => Adapt(Input(), Zero(), Zero(), width: width));

    [TestMethod]
    public void MissingFaceIsRejectedInsteadOfInferred()
        => Assert.ThrowsException<ArgumentNullException>(() => PackingFaceActionAdapter.FromModel(100, 30, 20, 40, Face(Input(), 0, Zero()), null!));
}
