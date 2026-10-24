using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Sections;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class PackingWeldedStationTests
{
    private static SectionBuiltUp Pair(string kind = "L-short", double gap = 12, bool radii = false)
    {
        if (kind == "C") return SectionBuiltUp.DoubleChannel(new SectionC(200, 10, 75, 12, 75, 12, "C", radii ? 12 : 0, radii ? 5 : 0), gap, true, "2C");
        var l = new SectionL(100, 8, 75, 8, "L100x75", radii ? 8 : 0, radii ? 4 : 0);
        return SectionBuiltUp.DoubleAngle(l, gap, kind == "L-short" ? SectionBuiltUp.AnglePairJoinedLeg.Short
            : SectionBuiltUp.AnglePairJoinedLeg.Long, "2L");
    }
    private static SteelVerificationInput Member(Section? shape = null, bool ntc = false, bool member = true,
        Section.FormedTypes formed = Section.FormedTypes.HotFinished, double fy = 275, double fu = 410)
        => new(new SteelSectionInput(new SteelSection(shape ?? Pair(), new SteelMaterial("S275", 210000, fy, fu), formedType: formed)),
            new SteelActions(-100000, 0, 0, 0, 0, 0), ntc ? SteelDesignContext.Ntc2018()
                : new SteelDesignContext(SectionResistanceCode.Eurocode3(1, 1.25, "Independent packing benchmark"), 1),
            member ? new SteelMemberInput(3000, 3000, 3000) : null);
    private static ConnectionDesignCode Code(bool ntc = false) => ntc ? ConnectionDesignCode.Ntc2018()
        : ConnectionDesignCode.Eurocode3(1, 1.25, 1.25, "Independent packing benchmark");
    private static PlanarConnectionActions Benchmark(double scale = 1) => new(100000 * scale, 0, 0, 0, -600000 * scale, 0);
    private static PlanarConnectionActions Zero() => new(0, 0, 0, 0, 0, 0);
    private static PackingFaceActionAdapterResult Demand(SteelVerificationInput? input = null, PlanarConnectionActions? a = null,
        PlanarConnectionActions? b = null, double gap = 12, double transverse = 37.5, double station = 500,
        double width = 60, double length = 120)
    {
        input ??= Member(); a ??= Benchmark();
        // Independent free-body equilibrium between face frames (x1=x0,y1=-y0,z1=-z0).
        // At face0: M0 + R*M1 + (-g*ez) cross (-F0)=0.
        b ??= new(-a.Fx, a.Fy, a.Fz, -a.Mx + gap * a.Fy, a.My + gap * a.Fx, a.Mz);
        return PackingFaceActionAdapter.FromModel(station, transverse, width, length,
            new PackingFaceActionState(input, "ULS/local-state01", "Independent complete local free body", 0, a),
            new PackingFaceActionState(input, "ULS/local-state01", "Independent complete local free body", 1, b));
    }
    private static PackingWeldedStationInput Input(PackingFaceActionAdapterResult? demand = null, bool ntc = false,
        double throat0 = 3, double throat1 = 3, FilletWeldEndCondition ends = FilletWeldEndCondition.FullSize,
        string? missing = null, ConnectionDesignCode? code = null, double fy = 275, double fu = 410)
        => new(code ?? Code(ntc), demand ?? Demand(Member(ntc: ntc)), fy, fu, FilletWeldSteelGrade.S275, FilletWeldSteelGrade.S275,
            new[] { new PackingFaceWeldDetail(0, throat0, ends, missing == "ends" ? "" : "Qualified full root size/corner return detail"),
                new PackingFaceWeldDetail(1, throat1, ends, "Qualified end detail") },
            missing == "material" ? "" : "Actual products/thicknesses, source material specification",
            missing == "consumable" ? "" : "Compatible consumable specification",
            missing == "footprint" ? "" : "Unperforated sound flat footprints, no root gap, direct fillets and corner execution",
            missing == "distribution" ? "" : "Rigid local distributor, root eccentricity and given six face actions");
    private static double V(IEnumerable<SectionResistanceValue> trace, string name) => trace.Single(t => t.Name == name).Value;
    private static void Equal(double expected, double actual) => Assert.AreEqual(expected, actual, Math.Max(1e-10, Math.Abs(expected) * 2e-11));
    private static void Supported(PackingWeldedStationResult r)
        => Assert.AreNotEqual(ConnectionStatus.NotSupported, r.CoveredComponentsStatus,
            string.Join(";", r.Diagnostics.Concat(r.NominalBlock.Diagnostics).Concat(r.FaceWelds.SelectMany(w => w.Verification.Diagnostics))));

    [DataTestMethod]
    [DataRow("L-short", false)] [DataRow("L-long", false)] [DataRow("C", false)]
    [DataRow("L-short", true)] [DataRow("L-long", true)] [DataRow("C", true)]
    public void TwoActualLoopsAndNominalBlockMatchIndependentAnalyticalBenchmark(string kind, bool ntc)
    {
        var d = Demand(Member(Pair(kind), ntc)); var r = PackingWeldedStationComponents.Verify(Input(d, ntc));
        Supported(r); Assert.AreEqual(ConnectionStatus.Passed, r.CoveredComponentsStatus);
        Assert.AreEqual(2, r.FaceWelds.Count); Assert.IsFalse(r.CanQualifyWholeStation); Assert.IsFalse(r.CanQualifyWholeJoint);
        Equal(7200, V(r.NominalBlock.Trace, "Area")); Equal(8640000, V(r.NominalBlock.Trace, "Iy"));
        Equal(144000, V(r.NominalBlock.Trace, "Wy")); Equal(-600000, V(r.NominalBlock.Trace, "MyAtFace0Cut"));
        Equal(600000, V(r.NominalBlock.Trace, "MyAtFace1Cut")); Equal(4.166666666666667, V(r.NominalBlock.Trace, "SigmaBound"));
        Equal(20.83333333333333, V(r.NominalBlock.Trace, "TauBound"));
        Equal(36.324157862838946, r.NominalBlock.Checks.Single().Demand);
        Equal(ntc ? 261.9047619047619 : 275, r.NominalBlock.Checks.Single().Resistance);
        for (int i = 0; i < 2; i++)
        {
            var f = r.FaceWelds[i]; var w = f.Verification;
            Assert.AreSame(d.Faces[i], f.Face); Assert.AreSame(d.Faces[i].ActionsOnBlockAtFaceCentre, w.Input.Actions);
            Equal(1080, V(w.Trace, "Area")); Equal(756000, V(w.Trace, "Ix")); Equal(2160000, V(w.Trace, "Iy"));
            Equal(i == 0 ? 100000 : -100000, V(w.Trace, "RecoveredFx"));
            Equal(i == 0 ? -600000 : 600000, V(w.Trace, "RecoveredMy"));
            Equal(162.09787217791927, w.Points.Max(p => p.Verification.Checks[0].Demand));
            Equal(0.42007070533912, w.Utilization!.Value);
            Assert.AreEqual(4, w.Input.Segments.Count); Equal(0, w.Input.LapLength);
            Assert.IsTrue(w.Input.Segments.All(s => s.ThroatNormalAngleRadians == 3 * Math.PI / 4));
        }
    }

    [DataTestMethod]
    [DataRow("L-short")] [DataRow("L-long")] [DataRow("C")]
    public void PhysicalModelRadiiDoNotFictitiouslyShortenTheFlatOuterBack(string kind)
    {
        double back = kind == "L-short" ? 75 : kind == "L-long" ? 100 : 200;
        double extent = 30 + Math.Sqrt(2) * 3;
        var d = Demand(Member(Pair(kind, radii: true)), transverse: back - extent);
        var r = PackingWeldedStationComponents.Verify(Input(d)); Supported(r);
        Equal(back, V(r.Trace, "Face0.FootprintHigh"));
    }

    [DataTestMethod]
    [DataRow(-1.0)] [DataRow(1.0)]
    public void LoadReversalPreservesCompleteWrenchAndSameStressEnvelope(double sign)
    {
        var r = PackingWeldedStationComponents.Verify(Input(Demand(a: Benchmark(sign)))); Supported(r);
        Equal(36.324157862838946, r.NominalBlock.Checks.Single().Demand);
        Equal(162.09787217791927, r.FaceWelds[0].Verification.Points.Max(p => p.Verification.Checks[0].Demand));
        Equal(100000 * sign, V(r.Trace, "Face0.Fx")); Equal(-600000 * sign, V(r.Trace, "Face0.My"));
    }

    [DataTestMethod]
    [DataRow(-72000.0)] [DataRow(72000.0)]
    public void NormalForceHasPhysicalTensionSignWithoutInventingContactCredit(double normal)
    {
        var r = PackingWeldedStationComponents.Verify(Input(Demand(a: new(0, 0, normal, 0, 0, 0)))); Supported(r);
        Equal(10, r.NominalBlock.Checks.Single().Demand);
        foreach (var f in r.FaceWelds)
            foreach (var point in f.Verification.Points)
            {
                Equal(normal / 1080, point.StressZ);
                Equal(normal / (1080 * Math.Sqrt(2)), point.Verification.SigmaPerpendicular);
                Equal(-normal / (1080 * Math.Sqrt(2)), point.Verification.TauPerpendicular);
            }
        StringAssert.Contains(r.Scope, "contact");
    }

    [TestMethod]
    public void ThroatNormalAtRightRootIsOutwardTransverseAndTowardChord()
    {
        var r = PackingWeldedStationComponents.Verify(Input()); Supported(r);
        var right = r.FaceWelds[0].Verification.Points.First(x => x.SegmentIndex == 1);
        Equal(100000.0 / 1080, right.StressX); Equal(16.666666666666667, right.StressZ);
        Equal((100000.0 / 1080 + 16.666666666666667) / Math.Sqrt(2), right.Verification.SigmaPerpendicular);
        Equal((100000.0 / 1080 - 16.666666666666667) / Math.Sqrt(2), right.Verification.TauPerpendicular);
    }

    [DataTestMethod]
    [DataRow(-1440000.0)] [DataRow(1440000.0)]
    public void PureMyWithNoForceUsesStressNotAnArtificialScalarForceResistance(double moment)
    {
        var r = PackingWeldedStationComponents.Verify(Input(Demand(a: new(0, 0, 0, 0, moment, 0)))); Supported(r);
        Equal(10, r.NominalBlock.Checks.Single().Demand); Assert.AreEqual("MPa", r.NominalBlock.Checks.Single().Unit);
        Equal(moment, V(r.NominalBlock.Trace, "MyAtFace1Cut")); Equal(0, V(r.NominalBlock.Trace, "TauBound"));
        Assert.IsFalse(r.CanQualifyWholeStation);
    }

    [TestMethod]
    public void BothBlockEndCutsGovernInsteadOfTheZeroCentreMoment()
    {
        var a = new PlanarConnectionActions(100000, 0, 0, 0, 0, 0);
        var r = PackingWeldedStationComponents.Verify(Input(Demand(a: a))); Supported(r);
        Equal(0, V(r.NominalBlock.Trace, "MyAtFace0Cut")); Equal(1200000, V(r.NominalBlock.Trace, "MyAtFace1Cut"));
        Equal(8.333333333333333, V(r.NominalBlock.Trace, "SigmaBound"));
    }

    [TestMethod]
    public void ExplicitZeroStateDoesNotAcquireHalfTheGlobalMemberForce()
    {
        var r = PackingWeldedStationComponents.Verify(Input(Demand(a: Zero()))); Supported(r);
        Equal(-100000, r.Input.Demand.SourceInput.Actions.N); Equal(0, r.NominalBlock.Checks.Single().Demand);
        Assert.IsTrue(r.FaceWelds.All(w => w.Verification.Utilization == 0));
    }

    [DataTestMethod]
    [DataRow("Fy", 1000.0)] [DataRow("Mx", 100000.0)] [DataRow("Mz", 100000.0)] [DataRow("Fy", 1e-200)]
    public void OutOfPlaneActionsRemainInSixDimensionalWeldsButBlockIsUnsupported(string component, double value)
    {
        var a = new PlanarConnectionActions(100000, component == "Fy" ? value : 0, 0,
            component == "Mx" ? value : 0, -600000, component == "Mz" ? value : 0);
        var d = Demand(a: a); Assert.IsTrue(d.IsInEquilibrium);
        var r = PackingWeldedStationComponents.Verify(Input(d));
        Assert.AreEqual(ConnectionStatus.NotSupported, r.NominalBlock.Status);
        Assert.AreEqual(ConnectionStatus.NotSupported, r.CoveredComponentsStatus); Assert.AreEqual(2, r.FaceWelds.Count);
        Assert.AreEqual(value, V(r.NominalBlock.Trace, "Face0." + component)); Assert.AreEqual(value, V(r.Trace, "Face0." + component));
        Assert.IsTrue(r.NominalBlock.Diagnostics.Contains("OUT_OF_PLANE_FY_MX_MZ_BLOCK_RESISTANCE_NOT_IMPLEMENTED"));
        Assert.AreEqual(0, r.NominalBlock.Checks.Count);
        Assert.AreSame(d.Faces[0].ActionsOnBlockAtFaceCentre, r.FaceWelds[0].Verification.Input.Actions);
    }

    [DataTestMethod]
    [DataRow("couple")] [DataRow("force")]
    public void UnbalancedLocalStateCannotQualifyEitherAttachmentOrBlock(string kind)
    {
        var d = kind == "couple" ? Demand(a: new(100000, 0, 0, 0, 0, 0), b: new(-100000, 0, 0, 0, 0, 0))
            : Demand(a: new(1, 0, 0, 0, 0, 0), b: Zero());
        var r = PackingWeldedStationComponents.Verify(Input(d)); Assert.AreEqual(ConnectionStatus.NotSupported, r.CoveredComponentsStatus);
        Assert.AreEqual(0, r.FaceWelds.Count); Assert.IsTrue(r.Diagnostics.Contains("TWO_FACE_STATE_NOT_IN_EQUILIBRIUM"));
        if (kind == "couple") Equal(-1200000, V(r.Trace, "ResidualGlobal.My"));
    }

    [TestMethod]
    public void DistinctThroatsRemainOnTheirRealFacesWithNoPoolingOfWeldArea()
    {
        var r = PackingWeldedStationComponents.Verify(Input(throat0: 3, throat1: 4)); Supported(r);
        Equal(1080, V(r.FaceWelds[0].Verification.Trace, "Area")); Equal(1440, V(r.FaceWelds[1].Verification.Trace, "Area"));
        Equal(.75, r.FaceWelds[1].Verification.Utilization!.Value / r.FaceWelds[0].Verification.Utilization!.Value);
    }

    [TestMethod]
    public void ReflectionTranslationAndPartOrderPreservePhysicalFootprints()
    {
        // Model constructor is (horizontal length100, vertical length75): the SHORT
        // leg is vertical. Reflect every physical point by X'=X+123.5, Y'=40-Y;
        // preserve each Part offset and toggle its existing reflection independently.
        var pair = Pair("L-short");
        Equal(6, pair.Parts[0].X); Equal(-6, pair.Parts[1].X);
        Equal(0, pair.Parts[0].Y); Equal(0, pair.Parts[1].Y);
        var transformed = new SectionBuiltUp(pair.Parts.Reverse().Select(p =>
            new SectionBuiltUp.Part(p.Section, p.X + 123.5, 40 - p.Y, p.MirrorX, !p.MirrorY)), "Translated reflected reversed 2L");
        var r = PackingWeldedStationComponents.Verify(Input(Demand(Member(transformed)))); Supported(r);
        Equal(-1, r.FaceWelds[0].Face.LocalAxes.V3.X);
        Equal(36.324157862838946, r.NominalBlock.Checks.Single().Demand);
        Equal(37.5 - 30 - Math.Sqrt(2) * 3, V(r.Trace, "Face0.FootprintLow"));
    }

    [DataTestMethod]
    [DataRow("low")] [DataRow("high")]
    public void WeldLegCannotRunOffTheConnectedShortLegEvenWhenBlockFits(string edge)
    {
        var d = Demand(transverse: edge == "low" ? 30 : 45);
        var r = PackingWeldedStationComponents.Verify(Input(d));
        Assert.AreEqual(ConnectionStatus.NotSupported, r.CoveredComponentsStatus);
        Assert.IsTrue(r.Diagnostics.Any(x => x.Contains("FOOTPRINT_OUTSIDE_EXACT_MODEL_STRAIGHT_BACK")));
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void LongitudinalWeldLegCannotRunOffMemberEnd(bool far)
    {
        var r = PackingWeldedStationComponents.Verify(Input(Demand(station: far ? 2940 : 60)));
        Assert.AreEqual(ConnectionStatus.NotSupported, r.CoveredComponentsStatus);
        Assert.IsTrue(r.Diagnostics.Any(x => x.Contains("WELD_FOOTPRINT_OUTSIDE_MEMBER_LENGTH")));
    }

    [TestMethod]
    public void InclusiveFootprintBoundariesAbsorbOnlyArithmeticRoundoff()
    {
        double extent = 30 + Math.Sqrt(2) * 3, end = 60 + Math.Sqrt(2) * 3;
        Supported(PackingWeldedStationComponents.Verify(Input(Demand(transverse: extent, station: end))));
        var under = PackingWeldedStationComponents.Verify(Input(Demand(transverse: extent - 1e-8, station: end)));
        Assert.AreEqual(ConnectionStatus.NotSupported, under.CoveredComponentsStatus);
    }

    [DataTestMethod]
    [DataRow(0.0, true)] [DataRow(-0.000001, false)]
    public void OpposingEqualLegFilletsMustFitAcrossActualGap(double delta, bool supported)
    {
        double gap = 6 * Math.Sqrt(2) + delta;
        var r = PackingWeldedStationComponents.Verify(Input(Demand(Member(Pair(gap: gap)), a: Zero(), gap: gap)));
        if (supported) Supported(r);
        else Assert.IsTrue(r.Diagnostics.Contains("OPPOSING_EQUAL_LEG_WELDS_OVERLAP_ACROSS_GAP"));
    }

    [DataTestMethod]
    [DataRow(false, 1008.0)] [DataRow(true, 1080.0)]
    public void EcAndNtcRetainTheirDifferentNominalEndPolicies(bool ntc, double effectiveArea)
    {
        // Four nominal ends deductions: EC each of four roots loses2a -> A=1080-4*6*3=1008.
        var r = PackingWeldedStationComponents.Verify(Input(ntc: ntc, ends: FilletWeldEndCondition.NominalWithoutFullSizeEvidence));
        Supported(r); Equal(effectiveArea, V(r.FaceWelds[0].Verification.Trace, "Area"));
    }

    [DataTestMethod]
    [DataRow("material")] [DataRow("consumable")] [DataRow("footprint")] [DataRow("distribution")] [DataRow("ends")]
    public void MissingEvidenceDoesNotBecomeAnImplicitQualifiedDetail(string missing)
    {
        var r = PackingWeldedStationComponents.Verify(Input(missing: missing));
        Assert.AreEqual(ConnectionStatus.NotSupported, r.CoveredComponentsStatus); Assert.AreEqual(0, r.FaceWelds.Count);
    }

    [TestMethod]
    public void NtcCannotBeCombinedWithAnEcMemberContext()
    {
        var r = PackingWeldedStationComponents.Verify(Input(Demand(), code: ConnectionDesignCode.Ntc2018()));
        Assert.IsTrue(r.Diagnostics.Contains("SOURCE_DESIGN_CONTEXT_MISMATCH"));
    }

    [TestMethod]
    public void ActualStrengthsMayBeReducedForThicknessButNotAboveTheDeclaredGrade()
    {
        var r = PackingWeldedStationComponents.Verify(Input(fy: 265, fu: 410)); Supported(r);
        Equal(265, r.NominalBlock.Checks.Single().Resistance);
        var invalid = PackingWeldedStationComponents.Verify(Input(fy: 300, fu: 410));
        Assert.IsTrue(invalid.Diagnostics.Contains("BLOCK_FY_EXCEEDS_DECLARED_GRADE"));
    }

    [TestMethod]
    public void MemberExtentIsRequiredToQualifyTheCompleteWeldFootprint()
    {
        var r = PackingWeldedStationComponents.Verify(Input(Demand(Member(member: false))));
        Assert.IsTrue(r.Diagnostics.Contains("MEMBER_LENGTH_REQUIRED_FOR_WELD_FOOTPRINT"));
    }

    [TestMethod]
    public void ExcessDemandFailsTheComponentsWithoutQualifyingTheStation()
    {
        var r = PackingWeldedStationComponents.Verify(Input(Demand(a: Benchmark(100)))); Supported(r);
        Assert.AreEqual(ConnectionStatus.Failed, r.CoveredComponentsStatus); Assert.AreEqual(ConnectionStatus.Failed, r.NominalBlock.Status);
        Assert.IsFalse(r.CanQualifyWholeStation); Assert.IsFalse(r.CanQualifyWholeJoint);
    }

    [TestMethod]
    public void ColdFormedBackRequiresItsOwnWeldingQualification()
    {
        var r = PackingWeldedStationComponents.Verify(Input(Demand(Member(formed: Section.FormedTypes.ColdFormed))));
        Assert.IsTrue(r.Diagnostics.Contains("COLD_FORMED_OR_UNQUALIFIED_MANUFACTURING_OUTSIDE_SCOPE"));
        Assert.AreEqual(ConnectionStatus.NotSupported, r.CoveredComponentsStatus);
    }

    [TestMethod]
    public void DefectiveWeldEndsCannotBecomeCompleteEvenIfNominalBlockPasses()
    {
        var r = PackingWeldedStationComponents.Verify(Input(ends: FilletWeldEndCondition.Defective));
        Assert.AreEqual(ConnectionStatus.NotSupported, r.CoveredComponentsStatus);
        Assert.AreEqual(ConnectionStatus.Passed, r.NominalBlock.Status);
        Assert.IsTrue(r.FaceWelds.All(w => w.Verification.Status == ConnectionStatus.NotSupported));
    }

    [TestMethod]
    public void TooShortPhysicalLoopSideIsNotRescuedByItsOtherLongSides()
    {
        var r = PackingWeldedStationComponents.Verify(Input(Demand(width: 25)));
        Assert.AreEqual(ConnectionStatus.NotSupported, r.CoveredComponentsStatus);
        Assert.IsTrue(r.FaceWelds.All(w => w.Verification.Diagnostics.Any(x => x.Contains("EFFECTIVE_LENGTH_BELOW_SCOPE"))));
    }

    [TestMethod]
    public void FiniteInputWhoseLegSumOverflowsCannotProduceAResistance()
    {
        var r = PackingWeldedStationComponents.Verify(Input(throat0: 1e308, throat1: 1e308));
        Assert.AreEqual(ConnectionStatus.NotSupported, r.CoveredComponentsStatus);
        Assert.AreEqual(0, r.FaceWelds.Count); Assert.AreEqual(0, r.NominalBlock.Checks.Count);
        Assert.IsTrue(r.Trace.All(t => double.IsFinite(t.Value)));
    }

    [TestMethod]
    public void NominalSectionOverflowRetainsFiniteActionsWithoutFavourableZeroStress()
    {
        var ordinary = Member(); var source = new SteelVerificationInput(ordinary.Section, ordinary.Actions,
            ordinary.Code, new SteelMemberInput(1e151, 1e151, 1e151));
        var r = PackingWeldedStationComponents.Verify(Input(Demand(source, a: Zero(), station: 5e150, length: 1e150)));
        Assert.AreEqual(ConnectionStatus.NotSupported, r.NominalBlock.Status); Assert.AreEqual(0, r.NominalBlock.Checks.Count);
        Assert.IsTrue(r.NominalBlock.Diagnostics.Any(x => x.StartsWith("BLOCK_NONFINITE_")));
        Assert.IsTrue(r.NominalBlock.Trace.All(t => double.IsFinite(t.Value)));
    }

    [TestMethod]
    public void InvalidOrDuplicatePhysicalFaceDetailsAreRejectedAtConstruction()
    {
        var p = Input(); var w = new PackingFaceWeldDetail(0, 3, FilletWeldEndCondition.FullSize, "source");
        Assert.ThrowsException<ArgumentException>(() => new PackingWeldedStationInput(p.Code, p.Demand, 275, 410,
            p.BlockGrade, p.ChordGrade, new[] { w, w }, p.MaterialSource, p.ConsumableCompatibilitySource, p.FootprintExecutionSource, p.LocalDistributionSource));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new PackingFaceWeldDetail(2, 3, FilletWeldEndCondition.FullSize, "source"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new PackingFaceWeldDetail(0, double.PositiveInfinity, FilletWeldEndCondition.FullSize, "source"));
    }
}
