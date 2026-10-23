using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Inputs;
using GPC.Model.Core;
using GPC.Model.Data.Sections;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class BraceAngleModelAdapterTests
{
    private static ConnectionDesignCode Ec() => ConnectionDesignCode.Eurocode3(1, 1.25, 1.25, "Explicit adapter test factors");
    private static ConnectionBolt Bolt() => new(16, 18, 157, ConnectionBoltGrade.Class8_8, true);
    private static SteelMaterial Material() => new("S275 explicit actual-thickness strengths", 210000, 275, 430);
    private static SectionL Angle(double b = 100, double h = 75, double t = 8, double r = 0, double r2 = 0)
        => new(b, t, h, t, "L test", r, r2);
    private static SteelSection Property(Section? shape = null) => new(shape ?? Angle(), Material());
    private static SteelSectionInput Snapshot(Section? shape = null) => new(Property(shape));
    private static SteelSectionInput Equivalent() => Snapshot(Angle(75, 75));
    private static SteelActions Axial(double force = 100000) => new(force, 0, 0, 0, 0, 0);
    private static BraceAngleModelAdapterResult Single(SteelSectionInput? section = null,
        BraceAngleConnectedLeg leg = BraceAngleConnectedLeg.First, SteelActions? actions = null,
        SteelSectionInput? equivalent = null, string? equivalentSource = null, ConnectionDesignCode? code = null)
        => BraceAngleModelAdapter.FromSingle(section ?? Snapshot(), code ?? Ec(), actions ?? Axial(), leg,
            Bolt(), 3, 60, 35, equivalent, equivalentSource);
    private static void Equal(double expected, double actual)
        => Assert.AreEqual(expected, actual, Math.Max(1e-9, Math.Abs(expected) * 1e-11));
    private static SteelSectionInput Pair(bool vertical = true, double gap = 12)
        => Snapshot(SectionBuiltUp.DoubleAngle(Angle(), gap, vertical
            ? SectionBuiltUp.AnglePairOrientation.VerticalLegsBackToBack
            : SectionBuiltUp.AnglePairOrientation.HorizontalLegsBackToBack, "2L"));
    private static BraceAngleModelAdapterResult AdaptPair(SteelSectionInput pair, SteelActions actions,
        double first = 50000, double second = 50000, string? source = "Explicit concomitant chord forces")
        => BraceAngleModelAdapter.FromPair(pair, Ec(), actions, first, second, source!, Bolt(), 3, 60, 35,
            Equivalent(), "Explicit sharp equivalent L75x75x8");

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void SingleModelGeometryMaterialAndPhysicalLegFeedTheNormativeComponent(bool ntc)
    {
        var r = Single(code: ntc ? ConnectionDesignCode.Ntc2018() : Ec());
        var p = r.Components.Single(); var check = BraceAngleTension.Verify(p.Input);
        Assert.AreEqual(ConnectionStatus.Passed, check.Status); Assert.IsFalse(r.IsPair); Assert.IsNull(r.ClearBackGap);
        Equal(100, p.Input.Section.FirstLeg); Equal(75, p.Input.Section.SecondLeg); Equal(8, p.Input.Section.Thickness);
        Equal(1336, p.Input.Section.GrossArea); Equal(275, p.Input.Section.Fy); Equal(430, p.Input.Section.Fu);
        Equal(232360.53333333333, check.Checks.Single(c => c.Id == "Angle.EffectiveNetRupture").Resistance);
        Equal(367400 / (ntc ? 1.05 : 1), check.Checks.Single(c => c.Id == "Angle.GrossYielding").Resistance);
    }

    [DataTestMethod]
    [DataRow(BraceAngleConnectedLeg.First)] [DataRow(BraceAngleConnectedLeg.Second)]
    public void BoltLineAndEccentricMomentsUseGeometricAxesNotPrincipalAxes(BraceAngleConnectedLeg leg)
    {
        var r = Single(leg: leg, equivalent: Equivalent(), equivalentSource: "Sharp equivalent L75x75x8");
        var p = r.Components[0];
        // Independent rectangle integration: horizontal100x8 plus vertical8x67.
        double cx = (800.0 * 50 + 536 * 4) / 1336;
        double cy = (800.0 * 4 + 536 * 41.5) / 1336;
        double bx = leg == BraceAngleConnectedLeg.First ? 65 : 4;
        double by = leg == BraceAngleConnectedLeg.First ? 4 : 40;
        Equal(cx, p.Centroid.X); Equal(cy, p.Centroid.Y);
        Equal(bx, p.BoltLine.X); Equal(by, p.BoltLine.Y);
        Equal((cy - by) * 100000, p.MomentAtBoltLineAboutSectionX);
        Equal(-(cx - bx) * 100000, p.MomentAtBoltLineAboutSectionY);
        Assert.IsTrue(Math.Abs(r.SourceSection.PrincipalAxisAngle) > .01);
        Assert.IsTrue(Math.Abs(p.MomentAtBoltLineAboutSectionX) > 0);
        Assert.IsTrue(Math.Abs(p.MomentAtBoltLineAboutSectionY) > 0);
    }

    [TestMethod]
    public void CommercialUnequalAnglePreservesRadiiAndComputedArea()
    {
        var commercial = SectionCatalogs.Find("L 200 x 100 x 10"); Assert.IsNotNull(commercial);
        var property = SectionMappings.CreateSteelSection(commercial, Material());
        var section = new SteelSectionInput(property);
        var r = Single(section, BraceAngleConnectedLeg.Second);
        // Catalogue dimensions: b100,h200,t10,r15,r2=7.5; exact root minus two toes.
        double area = (100 + 200 - 10) * 10 + (1 - Math.PI / 4) * (15 * 15 - 2 * 7.5 * 7.5);
        Equal(area, r.Components[0].Input.Section.GrossArea);
        Equal(section.Area, r.Components[0].Input.Section.GrossArea);
        Assert.AreNotEqual(2900d, r.Components[0].Input.Section.GrossArea);
        Assert.AreEqual(BraceAngleConnectedLeg.Second, r.Components[0].Input.ConnectedLeg);
    }

    [TestMethod]
    public void ShortLegEquivalentUsesItsOwnActualRootGeometryAndOnlyItsArea()
    {
        var equivalent = new SteelSectionInput(new SteelSection(Angle(75, 75, 8, 9, 4),
            new SteelMaterial("Different material irrelevant to equivalent geometric area", 210000, 235, 360)));
        var r = Single(leg: BraceAngleConnectedLeg.Second, equivalent: equivalent, equivalentSource: "Explicit equal-angle geometry");
        Equal(1136 + (1 - Math.PI / 4) * (81 - 32), r.Components[0].Input.EquivalentShortLegGrossArea!.Value);
        Equal(275, r.Components[0].Input.Section.Fy); Equal(430, r.Components[0].Input.Section.Fu);
    }

    [TestMethod]
    public void SourceAndExportedModelMutationsCannotChangeAdaptedInputOrPositions()
    {
        var original = Property(); var snapshot = new SteelSectionInput(original);
        var r = Single(snapshot); double x = r.Components[0].Centroid.X;
        ((SectionL)original.SectionShape).HorizontalLegLength = 300;
        original.SteelMaterial = new SteelMaterial("changed", 210000, 355, 510);
        var exported = snapshot.CreateSectionCopy(); ((SectionL)exported.SectionShape).Height = 250;
        Equal(100, r.Components[0].Input.Section.FirstLeg); Equal(275, r.Components[0].Input.Section.Fy);
        Equal(1336, r.Components[0].Input.Section.GrossArea); Equal(x, r.Components[0].Centroid.X);
    }

    [DataTestMethod]
    [DataRow(true)] [DataRow(false)]
    public void ExistingDoubleAngleFactoriesSelectTheFacingPhysicalLeg(bool vertical)
    {
        var r = AdaptPair(Pair(vertical), Axial()); Assert.IsTrue(r.IsPair); Equal(12, r.ClearBackGap!.Value);
        Assert.AreEqual(2, r.Components.Count);
        foreach (var p in r.Components)
        {
            Assert.AreEqual(vertical ? BraceAngleConnectedLeg.Second : BraceAngleConnectedLeg.First, p.Input.ConnectedLeg);
            Equal(50000, p.Input.Tension); Equal(1336, p.Input.Section.GrossArea);
            Assert.AreEqual(ConnectionStatus.Passed, BraceAngleTension.Verify(p.Input).Status);
        }
        if (vertical)
        {
            Equal(10, r.Components[0].BoltLine.X); Equal(-10, r.Components[1].BoltLine.X);
            Equal(40, r.Components[0].BoltLine.Y); Equal(40, r.Components[1].BoltLine.Y);
        }
        else
        {
            Equal(65, r.Components[0].BoltLine.X); Equal(65, r.Components[1].BoltLine.X);
            Equal(10, r.Components[0].BoltLine.Y); Equal(-10, r.Components[1].BoltLine.Y);
        }
    }

    [DataTestMethod]
    [DataRow(true, true)] [DataRow(true, false)] [DataRow(false, true)] [DataRow(false, false)]
    public void LongShortSelectionFromModelDoesNotAssumeWhichLegIsVertical(bool verticalLong, bool joinLong)
    {
        var a = verticalLong ? Angle(75, 100) : Angle(100, 75);
        var pair = Snapshot(SectionBuiltUp.DoubleAngle(a, 12, joinLong
            ? SectionBuiltUp.AnglePairJoinedLeg.Long : SectionBuiltUp.AnglePairJoinedLeg.Short, "2L explicit leg"));
        var r = AdaptPair(pair, Axial());
        var input = r.Components[0].Input;
        double connected = input.ConnectedLeg == BraceAngleConnectedLeg.First ? input.Section.FirstLeg : input.Section.SecondLeg;
        Equal(joinLong ? 100 : 75, connected);
        Equal(joinLong ? 232360.53333333333 : 193373.86666666667,
            BraceAngleTension.Verify(input).Checks.Single(c => c.Id == "Angle.EffectiveNetRupture").Resistance);
    }

    [DataTestMethod]
    [DataRow(true, 70000.0, 30000.0)] [DataRow(false, 70000.0, 30000.0)]
    [DataRow(true, 0.0, 100000.0)] [DataRow(false, 100000.0, 0.0)]
    public void UnequalExplicitTensionsMustRecoverBendingWithCorrectPrincipalAxisRotation(bool vertical, double first, double second)
    {
        var section = Pair(vertical);
        double cx = (800.0 * 50 + 536 * 4) / 1336;
        double cy = (800.0 * 4 + 536 * 41.5) / 1336;
        double mx = vertical ? 0 : (6 + cy) * (first - second);
        double my = vertical ? -(6 + cx) * (first - second) : 0;
        double c = Math.Cos(section.PrincipalAxisAngle), s = Math.Sin(section.PrincipalAxisAngle);
        var actions = new SteelActions(first + second, 0, 0, 0, c * mx + s * my, -s * mx + c * my);
        var r = AdaptPair(section, actions, first, second);
        Equal(first, r.Components[0].Input.Tension); Equal(second, r.Components[1].Input.Tension);
        double recoveredX = r.Components.Sum(p => (p.Centroid.Y - r.SectionCentroid.Y) * p.Input.Tension);
        double recoveredY = -r.Components.Sum(p => (p.Centroid.X - r.SectionCentroid.X) * p.Input.Tension);
        Equal(mx, recoveredX); Equal(my, recoveredY);
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void TranslatingAndReversingPartOrderPreservesGeometryAndTensileCapacities(bool reverse)
    {
        var original = SectionBuiltUp.DoubleAngle(Angle(), 12, "2L");
        var parts = original.Parts.Select(p => new SectionBuiltUp.Part(p.Section, p.X + 30, p.Y - 20, p.MirrorX, p.MirrorY));
        if (reverse) parts = parts.Reverse();
        var r = AdaptPair(Snapshot(new SectionBuiltUp(parts, "translated pair")), Axial());
        Equal(30, r.SectionCentroid.X);
        Equal(12, r.ClearBackGap!.Value);
        Equal(reverse ? 20 : 40, r.Components[0].BoltLine.X); Equal(20, r.Components[0].BoltLine.Y);
        foreach (var item in r.Components) Assert.AreEqual(ConnectionStatus.Passed, BraceAngleTension.Verify(item.Input).Status);
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void ReflectionInTheOtherAxisDoesNotRenameTheConnectedLeg(bool horizontalBacks)
    {
        var a = Angle();
        var parts = horizontalBacks
            ? new[] { new SectionBuiltUp.Part(a, 0, 6, true, false), new SectionBuiltUp.Part(a, 0, -6, true, true) }
            : new[] { new SectionBuiltUp.Part(a, 6, 0, false, true), new SectionBuiltUp.Part(a, -6, 0, true, true) };
        var r = AdaptPair(Snapshot(new SectionBuiltUp(parts, "double reflection")), Axial());
        Assert.AreEqual(horizontalBacks ? BraceAngleConnectedLeg.First : BraceAngleConnectedLeg.Second, r.Components[0].Input.ConnectedLeg);
        if (horizontalBacks) Equal(-65, r.Components[0].BoltLine.X); else Equal(-40, r.Components[0].BoltLine.Y);
    }

    [TestMethod]
    public void PairAdaptationDoesNotMutateTheSourceModelOrLoseSharedSectionIdentity()
    {
        var source = Property(SectionBuiltUp.DoubleAngle(Angle(), 12, "2L"));
        string before = ModelValues.Fingerprint(new object[] { source });
        var snapshot = new SteelSectionInput(source); var r = AdaptPair(snapshot, Axial());
        Assert.AreEqual(before, ModelValues.Fingerprint(new object[] { source }));
        var copy = (SectionBuiltUp)snapshot.CreateSectionCopy().SectionShape;
        Assert.AreSame(copy.Parts[0].Section, copy.Parts[1].Section);
        ((SectionL)((SectionBuiltUp)source.SectionShape).Parts[0].Section).Height = 120;
        Equal(75, r.Components[0].Input.Section.SecondLeg);
    }

    [DataTestMethod]
    [DataRow(0)] [DataRow(1)] [DataRow(2)] [DataRow(3)] [DataRow(4)] [DataRow(5)]
    public void SingleAdapterNeverSilentlyDropsCompressionShearTorsionOrBending(int action)
    {
        SteelActions a = action switch
        {
            0 => new(-1, 0, 0, 0, 0, 0), 1 => new(100000, 1e-9, 0, 0, 0, 0),
            2 => new(100000, 0, 1e-9, 0, 0, 0), 3 => new(100000, 0, 0, 1e-9, 0, 0),
            4 => new(100000, 0, 0, 0, 1e-9, 0), _ => new(100000, 0, 0, 0, 0, 1e-9)
        };
        Assert.ThrowsException<NotSupportedException>(() => Single(actions: a));
    }

    [DataTestMethod]
    [DataRow(0)] [DataRow(1)] [DataRow(2)] [DataRow(3)] [DataRow(4)]
    public void PairNeedsConcomitantForceAndMomentEquilibriumAndAnExplicitSource(int defect)
    {
        if (defect == 4)
            Assert.ThrowsException<ArgumentException>(() => AdaptPair(Pair(), Axial(), source: " "));
        else
        {
            SteelActions a = defect == 2 ? new SteelActions(100000, 1, 0, 0, 0, 0)
                : defect == 3 ? new(100000, 0, 0, 0, 100000, 0) : Axial();
            Assert.ThrowsException<NotSupportedException>(() => AdaptPair(Pair(), a,
                first: defect == 0 ? 40000 : defect == 1 ? 70000 : 50000,
                second: defect == 1 ? 30000 : 50000));
        }
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void DifferentPhysicalProfilesAreRejectedEvenWhenDimensionsCoincide(bool sameDimensions)
    {
        var parts = new[] { new SectionBuiltUp.Part(Angle(), 6, 0),
            new SectionBuiltUp.Part(sameDimensions ? Angle() : Angle(120, 75), -6, 0, true) };
        Assert.ThrowsException<NotSupportedException>(() => AdaptPair(Snapshot(new SectionBuiltUp(parts, "unshared pair")), Axial()));
    }

    [TestMethod]
    public void ToeToToeAndChannelsAreNotAutomaticallyEquivalentToAnglesBackToBack()
    {
        var a = Angle();
        var toes = new SectionBuiltUp(new[] { new SectionBuiltUp.Part(a, -106, 0),
            new SectionBuiltUp.Part(a, 106, 0, true) }, "toe to toe");
        Assert.ThrowsException<NotSupportedException>(() => AdaptPair(Snapshot(toes), Axial()));
        var channels = SectionBuiltUp.DoubleChannel(new SectionC(150, 8, 60, 10, 60, 10, "C"), 12, true, "2C");
        Assert.ThrowsException<NotSupportedException>(() => AdaptPair(Snapshot(channels), Axial()));
        Assert.ThrowsException<NotSupportedException>(() => Single(Snapshot(new SectionC(150, 8, 60, 10, 60, 10, "C"))));
    }

    [TestMethod]
    public void UnequalThicknessAndColdFormedSectionsRequireASeparateMethod()
    {
        Assert.ThrowsException<NotSupportedException>(() => Single(Snapshot(new SectionL(100, 8, 75, 10, "unequal thickness"))));
        var cold = new SteelSectionInput(new SteelSection(Angle(), Material(), formedType: Section.FormedTypes.ColdFormed));
        Assert.ThrowsException<NotSupportedException>(() => Single(cold));
    }

    [TestMethod]
    public void EquivalentSectionMustMatchShortSizeThicknessAndManufacturing()
    {
        Assert.ThrowsException<ArgumentException>(() => Single(leg: BraceAngleConnectedLeg.Second));
        Assert.ThrowsException<ArgumentException>(() => Single(leg: BraceAngleConnectedLeg.Second, equivalent: Equivalent()));
        foreach (var equal in new[] { Snapshot(Angle(80, 80)), Snapshot(Angle(75, 75, 10)), Snapshot(Angle(75, 80)) })
            Assert.ThrowsException<ArgumentException>(() => Single(leg: BraceAngleConnectedLeg.Second,
                equivalent: equal, equivalentSource: "Incompatible geometry"));
        var welded = new SteelSectionInput(new SteelSection(Angle(75, 75), Material(), Section.SectionTypes.Welded));
        Assert.ThrowsException<ArgumentException>(() => Single(leg: BraceAngleConnectedLeg.Second,
            equivalent: welded, equivalentSource: "Incompatible manufacturing"));
    }

    [TestMethod]
    public void ExistingNetAreaDeclarationsAreCheckedRatherThanDiscarded()
    {
        var p = Property();
        Assert.ThrowsException<NotSupportedException>(() => Single(new SteelSectionInput(p, false)));
        Assert.ThrowsException<NotSupportedException>(() => Single(new SteelSectionInput(p, false, 1100)));
        var r = Single(new SteelSectionInput(p, false, 1192)); Equal(1336, r.Components[0].Input.Section.GrossArea);
        var pair = Property(SectionBuiltUp.DoubleAngle(Angle(), 12, "2L"));
        var adapted = AdaptPair(new SteelSectionInput(pair, false, 2384), Axial());
        Equal(1336, adapted.Components[0].Input.Section.GrossArea);
    }

    [TestMethod]
    public void AdapterDoesNotCallAGrossOrNetResultACompleteConnection()
    {
        var r = Single(); StringAssert.Contains(r.Scope, "No bolt, gusset, packing, support");
        Assert.IsTrue(r.Components[0].MomentAtBoltLineAboutSectionX != 0);
        Assert.AreEqual(ConnectionStatus.Passed, BraceAngleTension.Verify(r.Components[0].Input).Status);
    }
}
