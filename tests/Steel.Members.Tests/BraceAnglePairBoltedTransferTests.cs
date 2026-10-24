using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Inputs;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class BraceAnglePairBoltedTransferTests
{
    private static ConnectionDesignCode Ec() => ConnectionDesignCode.Eurocode3(1, 1.25, 1.25, "Phase3c3 explicit ordinary EC factors");
    private static ConnectionBolt Bolt() => new(16, 18, 157, ConnectionBoltGrade.Class8_8, true);
    private static SteelMaterial Material() => new("Actual S275", 210000, 275, 430);
    private static SteelSectionInput Snapshot(Section section) => new(new SteelSection(section, Material()));
    private static BraceAngleModelAdapterResult Adapt(bool vertical = true, bool reflectToe = false, bool reverse = false,
        double dx = 0, double dy = 0, double first = 100000, double second = 100000,
        int count = 4, double pitch = 60, double toe = 75, double gap = 20, double root = 0,
        double toeRadius = 0, ConnectionDesignCode? code = null, bool shortLeg = false)
    {
        var angle = new SectionL(vertical ? 100 : 150, 10, vertical ? 150 : 100, 10, "Actual sharp/rounded unequal angle", root, toeRadius);
        bool verticalBacks = shortLeg ? !vertical : vertical;
        var pair = SectionBuiltUp.DoubleAngle(angle, gap, verticalBacks
            ? SectionBuiltUp.AnglePairOrientation.VerticalLegsBackToBack
            : SectionBuiltUp.AnglePairOrientation.HorizontalLegsBackToBack, "Identical pair");
        var parts = pair.Parts.Select(p => new SectionBuiltUp.Part(p.Section, p.X + dx, p.Y + dy,
            verticalBacks ? p.MirrorX : reflectToe ? !p.MirrorX : p.MirrorX,
            verticalBacks && reflectToe ? !p.MirrorY : p.MirrorY)).ToArray();
        if (reverse) Array.Reverse(parts);
        pair = new SectionBuiltUp(parts, "Positioned pair");
        var snapshot = Snapshot(pair);
        // Construct the concomitant load case for a gate test; numerical benchmarks
        // below use independently integrated sharp-angle centroids and literal results.
        var c0 = parts[0].Place(angle.Centroid.X, angle.Centroid.Y);
        var c1 = parts[1].Place(angle.Centroid.X, angle.Centroid.Y);
        double cx = c0.X / 2 + c1.X / 2, cy = c0.Y / 2 + c1.Y / 2;
        double mx = (c0.Y - cy) * first + (c1.Y - cy) * second;
        double my = -(c0.X - cx) * first - (c1.X - cx) * second;
        double c = Math.Cos(snapshot.PrincipalAxisAngle), s = Math.Sin(snapshot.PrincipalAxisAngle);
        var actions = new SteelActions(first + second, 0, 0, 0, c * mx + s * my, -s * mx + c * my);
        return BraceAngleModelAdapter.FromPair(snapshot, code ?? Ec(), actions, first, second,
            "Explicit concomitant axial chord forces, case ULS-3c3", Bolt(), count, pitch, toe,
            shortLeg ? Snapshot(new SectionL(100, 10, 100, 10, "Equivalent short-leg model", root, toeRadius)) : null,
            shortLeg ? "Actual equal-short-leg model geometry" : null);
    }
    private static BraceAnglePairBoltedTransferInput Input(BraceAngleModelAdapterResult? angles = null,
        ConnectionPlate? gusset = null, double angleEnd = 40, double gussetEnd = 40, double edge = 100,
        double hardware = 30, bool exposed = false, bool additional = false,
        string geometry = "Actual drawing, normal round holes, hardware and access envelope",
        string distribution = "Conditional equal-stiffness elastic in-plane load path; local3D unresolved",
        string combination = "ULS-3c3 concurrent chord forces", string? holeSource = "Documented NTC hole SLE check")
        => new(angles ?? Adapt(), gusset ?? new ConnectionPlate(200, 20, 275, 430), angleEnd, gussetEnd,
            edge, hardware, exposed, additional, geometry, distribution, combination, holeSource);
    private static void Equal(double expected, double actual) => Assert.AreEqual(expected, actual, Math.Max(1e-9, Math.Abs(expected) * 1e-10));
    private static ConnectionCheck Check(BraceAnglePairBoltedTransferResult result, string id)
        => result.InPlaneComponents.Checks.Single(c => c.Id == id);
    private static void Unsupported(BraceAnglePairBoltedTransferResult result)
    {
        Assert.AreEqual(ConnectionStatus.NotSupported, result.InPlaneComponents.Status);
        Assert.AreEqual(0, result.InPlaneComponents.Checks.Count);
        Assert.IsNull(result.InPlaneComponents.Utilization);
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void OrdinaryEcAndNtcReusePoliciesWithoutPromotingTheLocal3dModel(bool ntc)
    {
        var r = BraceAnglePairBoltedTransfer.Verify(Input(Adapt(code: ntc ? ConnectionDesignCode.Ntc2018() : Ec())));
        Assert.AreEqual(ConnectionStatus.Passed, r.InPlaneComponents.Status);
        Assert.AreEqual(ConnectionStatus.NotSupported, r.Status);
        Assert.AreEqual(ConnectionStatus.NotSupported, r.LocalThreeDimensionalStatus);
        Assert.IsFalse(r.CanQualifyWholeJoint);
        Assert.IsTrue(r.Diagnostics.Any(s => s.Contains("OUT_OF_PLANE_LOCAL_ACTIONS")));
        Assert.IsTrue(r.MissingWholeJointChecks.Contains("GUSSET_BLOCK_TEARING_ONE_LINE"));
        Assert.IsTrue(r.MissingWholeJointChecks.Contains("GUSSET_GROSS_NET_AND_LOAD_SPREAD"));
        Equal(660000 / (ntc ? 1.05 : 1), Check(r, "Angle0.Angle.GrossYielding").Resistance);
        Equal(432752, Check(r, "Angle0.Angle.EffectiveNetRupture").Resistance);
        Equal(120576, Check(r, "Bolt1.Bolt.Shear").Resistance);
        Assert.IsTrue(Check(r, "Bolt1.Gusset.BearingEllipse").Reference.Contains("complementary"));
    }

    [TestMethod]
    public void IndependentOneLineBenchmarkPreservesRealInPlaneEccentricityAndEquilibrium()
    {
        var r = BraceAnglePairBoltedTransfer.Verify(Input()); var g = r.InPlaneBoltGroup;
        Equal(200000, r.ActionsOnGussetAtBoltGroup.Fx); Equal(5250000, r.ActionsOnGussetAtBoltGroup.Mz);
        Assert.AreEqual(1, g.GeometryRank);
        double[] fy = { -26250, -8750, 8750, 26250 };
        for (int i = 0; i < 4; i++) { Equal(50000, g.Forces[i].Fx); Equal(fy[i], g.Forces[i].Fy); }
        Equal(56471.784990382585, g.MaximumShear!.Value);
        Equal(0, g.EquilibriumResidual.Fx); Equal(0, g.EquilibriumResidual.Fy); Equal(0, g.EquilibriumResidual.Mz);
        Assert.IsTrue(g.MaximumShear > 50000, "Dropping Mz would incorrectly reduce the governing bolt force.");
    }

    [DataTestMethod]
    [DataRow(true)] [DataRow(false)]
    public void OppositeLocalCouplesDoNotDisappearWhenTheirAggregateCancels(bool vertical)
    {
        var r = BraceAnglePairBoltedTransfer.Verify(Input(Adapt(vertical: vertical)));
        Equal(0, r.ActionsOnGussetAtBoltGroup.My);
        foreach (var face in r.FaceDemands)
        {
            Equal(1875000, Math.Abs(face.AtBoltLine.My)); Equal(2375000, Math.Abs(face.AtGussetFace.My));
            Equal(2625000, face.AtBoltLine.Mz); Equal(2625000, face.AtGussetFace.Mz);
            Equal(100000, face.AtGussetFace.Fx);
        }
        Equal(0, r.FaceDemands.Sum(f => f.AtGussetFace.My));
        Assert.AreEqual(ConnectionStatus.NotSupported, r.LocalThreeDimensionalStatus);
    }

    [DataTestMethod]
    [DataRow(1)] [DataRow(2)] [DataRow(3)] [DataRow(4)]
    public void GussetCarriesTheFullDemandAndEachAngleCarriesItsOwnOppositeHalf(int bolt)
    {
        var r = BraceAnglePairBoltedTransfer.Verify(Input()); string prefix = "Bolt" + bolt + ".";
        Equal(50000, Check(r, prefix + "Gusset.X.Plate.Bearing").Demand);
        Equal(25000, Check(r, prefix + "Angle0.X.Plate.Bearing").Demand);
        Equal(25000, Check(r, prefix + "Angle1.X.Plate.Bearing").Demand);
        double y = Math.Abs(r.InPlaneBoltGroup.Forces[bolt - 1].Fy);
        Equal(y, Check(r, prefix + "Gusset.Y.Plate.Bearing").Demand);
        Equal(y / 2, Check(r, prefix + "Angle0.Y.Plate.Bearing").Demand);
        Equal(y / 2, Check(r, prefix + "Angle1.Y.Plate.Bearing").Demand);
    }

    [TestMethod]
    public void OppositeLongitudinalEndDirectionsSelectDifferentEndBoltsInAnglesAndGusset()
    {
        var r = BraceAnglePairBoltedTransfer.Verify(Input());
        Equal(101925.92592592593, Check(r, "Bolt1.Angle0.X.Plate.Bearing").Resistance);
        Equal(118488.88888888889, Check(r, "Bolt4.Angle0.X.Plate.Bearing").Resistance);
        Equal(203851.85185185187, Check(r, "Bolt4.Gusset.X.Plate.Bearing").Resistance);
        Equal(236977.77777777778, Check(r, "Bolt1.Gusset.X.Plate.Bearing").Resistance);
    }

    [DataTestMethod]
    [DataRow(0, 166223.95865128943, 162888.85643912738, -33130.422237860665)]
    [DataRow(1, 190817.72310500155, 162888.85643912738, 99391.26671358198)]
    [DataRow(2, 107349.10463481337, 40561.143560872624, -99391.26671358198)]
    [DataRow(3, 52372.046404782115, 40561.143560872624, 33130.422237860665)]
    public void SbeAppendixBTableB13PublishedTwoLineCaseVerifiesSharedForceKernelOnly(int index, double shear, double fx, double fy)
    {
        // SBE_SS5 p5-110/PDF118. This is NOT an accepted one-line/categoryA detail.
        var points = new[] { new ConnectionPoint2D(-16.25, -30), new(48.75, -30), new(-48.75, 30), new(16.25, 30) };
        var g = ElasticBoltGroup.Calculate(points, new PlanarConnectionActions(406900, 0, 0, 0, 0, 18107050));
        Equal(shear, g.Forces[index].Shear); Equal(fx, g.Forces[index].Fx); Equal(fy, g.Forces[index].Fy);
        Equal(406900, g.Forces.Sum(f => f.Fx)); Equal(18107050, g.Forces.Sum(f => f.Position.X * f.Fy - f.Position.Y * f.Fx));
    }

    [DataTestMethod]
    [DataRow(84370.0, 143220.0, 154220.0, 264380.0, .5927525494930365)]
    [DataRow(182860.0, 54540.0, 264380.0, 257600.0, .5232148367002358)]
    public void SbeTablesB18B19DocumentTheComplementaryEllipseWithoutMisattributingItToEc(double fx, double fy, double rx, double ry, double expected)
    {
        // Published rounded capacities/demands, reproduced independently. The actual
        // new-detail implementation is tested below against its own checked capacities.
        Equal(expected, Math.Pow(fx / rx, 2) + Math.Pow(fy / ry, 2));
        Assert.IsTrue(expected < 1);
        var r = BraceAnglePairBoltedTransfer.Verify(Input());
        var x = Check(r, "Bolt1.Gusset.X.Plate.Bearing"); var y = Check(r, "Bolt1.Gusset.Y.Plate.Bearing");
        Equal(Math.Pow(x.Utilization, 2) + Math.Pow(y.Utilization, 2), Check(r, "Bolt1.Gusset.BearingEllipse").Demand);
        StringAssert.Contains(Check(r, "Bolt1.Gusset.BearingEllipse").Reference, "ECCS");
    }

    [DataTestMethod]
    [DataRow(false, 154224.0)] [DataRow(true, 264384.0)]
    public void SbeTablesB16B17Bolt1BearingCapacitiesVerifyTheSharedPlyKernel(bool vertical, double expected)
    {
        // SBE_SS5 pp5-112/113: M24 10.9, gusset t15, fy355/fu510,
        // d0=26, pitch65. Exact arithmetic gives154.224/264.384kN.
        var bolt = new ConnectionBolt(24, 26, 353, ConnectionBoltGrade.Class10_9, true);
        var p = new BoltBearingInput(Ec(), bolt, new ConnectionPlate(260, 15, 355, 510),
            vertical ? 143220 : 84370, 124, 124, vertical ? null : 65, 65, vertical, !vertical);
        var result = BoltBearing.Verify(p);
        Assert.AreEqual(ConnectionStatus.Passed, result.Status); Equal(expected, result.Checks[0].Resistance);
    }

    [TestMethod]
    public void JrcSx034SingleAngleBenchmarkConfirmsNetAndBoltKernelWithoutClaimingACompletePair()
    {
        // JRC SX034a-EN-EU sheets5/6, exact coefficients before the publication's rounding.
        var b = new ConnectionBolt(20, 22, 245, ConnectionBoltGrade.Class8_8, true);
        var angle = new BraceAngleSection(80, 120, 12, 2270, 275, 430);
        var net = BraceAngleTension.Verify(new BraceAngleTensionInput(Ec(), angle, BraceAngleConnectedLeg.Second, b, 250000, 3, 80, 80));
        Equal(407765.09090909094, net.Checks.Single(c => c.Id == "Angle.EffectiveNetRupture").Resistance);
        Equal(624250, net.Checks.Single(c => c.Id == "Angle.GrossYielding").Resistance);
        var shear = BoltStrength.Verify(new BoltStrengthInput(Ec(), b, 250000.0 / 3, 0));
        Equal(94080, shear.Checks.Single(c => c.Id == "Bolt.Shear").Resistance);
        Assert.AreEqual(ConnectionStatus.Passed, net.Status);
    }

    [DataTestMethod]
    [DataRow(true, 156363.63636363638)] [DataRow(false, 198581.81818181818)]
    public void JrcSx034BearingUsesExactAlphaInsteadOfForcingRoundedPublishedNumbers(bool end, double expected)
    {
        var b = new ConnectionBolt(20, 22, 245, ConnectionBoltGrade.Class8_8, true);
        var r = BoltBearing.Verify(new BoltBearingInput(Ec(), b, new ConnectionPlate(120, 12, 275, 430),
            250000.0 / 3, 50, 80, 80, null, end, true));
        Equal(expected, r.Checks[0].Resistance);
        Assert.AreEqual(ConnectionStatus.Passed, r.Status);
    }

    [DataTestMethod]
    [DataRow(true, false)] [DataRow(true, true)] [DataRow(false, false)] [DataRow(false, true)]
    public void PhysicalLongLegAndCommonAxesSurviveReflection(bool vertical, bool reflect)
    {
        var r = BraceAnglePairBoltedTransfer.Verify(Input(Adapt(vertical: vertical, reflectToe: reflect)));
        Assert.AreEqual(ConnectionStatus.Passed, r.InPlaneComponents.Status);
        Equal(reflect ? -5250000 : 5250000, r.ActionsOnGussetAtBoltGroup.Mz);
        Equal(56471.784990382585, r.InPlaneBoltGroup.MaximumShear!.Value);
        Equal(reflect ? 26250 : -26250, r.InPlaneBoltGroup.Forces[0].Fy);
    }

    [DataTestMethod]
    [DataRow(true, false)] [DataRow(true, true)] [DataRow(false, false)] [DataRow(false, true)]
    public void ShortLegPairUsesExplicitEquivalentSectionAndDoesNotRenameThePhysicalLeg(bool verticalLong, bool reflect)
    {
        var a = Adapt(vertical: verticalLong, reflectToe: reflect, shortLeg: true, toe: 50);
        var r = BraceAnglePairBoltedTransfer.Verify(Input(a));
        Assert.AreEqual(ConnectionStatus.Passed, r.InPlaneComponents.Status);
        Equal(1900, a.Components[0].Input.EquivalentShortLegGrossArea!.Value);
        Equal((.5 + .2 * (60.0 / 18 - 2.5) / 2.5) * (1900 - 180) * 430 / 1.25,
            Check(r, "Angle0.Angle.EffectiveNetRupture").Resistance);
        Assert.AreEqual(verticalLong ? BraceAngleConnectedLeg.First : BraceAngleConnectedLeg.Second, a.Components[0].Input.ConnectedLeg);
    }

    [DataTestMethod]
    [DataRow(true, false)] [DataRow(true, true)] [DataRow(false, false)] [DataRow(false, true)]
    public void TranslationAndPartOrderDoNotChangePhysicalPlaneChecks(bool vertical, bool reverse)
    {
        var original = BraceAnglePairBoltedTransfer.Verify(Input(Adapt(vertical: vertical)));
        var r = BraceAnglePairBoltedTransfer.Verify(Input(Adapt(vertical: vertical, reverse: reverse, dx: 400, dy: -250)));
        Assert.AreEqual(ConnectionStatus.Passed, r.InPlaneComponents.Status);
        Equal(original.ActionsOnGussetAtBoltGroup.Mz, r.ActionsOnGussetAtBoltGroup.Mz);
        Equal(original.InPlaneComponents.Utilization!.Value, r.InPlaneComponents.Utilization!.Value);
        Equal(2375000, Math.Abs(r.FaceDemands[0].AtGussetFace.My));
    }

    [DataTestMethod]
    [DataRow(0)] [DataRow(1)] [DataRow(2)] [DataRow(3)] [DataRow(4)] [DataRow(5)] [DataRow(6)]
    [DataRow(7)] [DataRow(8)] [DataRow(9)] [DataRow(10)] [DataRow(11)] [DataRow(12)] [DataRow(13)]
    public void GeometricAndPhysicalOutOfScopeCasesCannotReturnFavourablePartialPlaneChecks(int defect)
    {
        var p = defect switch
        {
            0 => Input(gusset: new ConnectionPlate(200, 18, 275, 430)),
            1 => Input(additional: true), 2 => Input(hardware: 18), 3 => Input(hardware: 100),
            4 => Input(angleEnd: 10), 5 => Input(gussetEnd: 10), 6 => Input(edge: 5), 7 => Input(edge: 201),
            8 => Input(gusset: new ConnectionPlate(200, 20, 500, 550)),
            9 => Input(Adapt(pitch: 201)), 10 => Input(Adapt(pitch: 40)),
            11 => Input(Adapt(toe: 120)),
            12 => Input(gusset: new ConnectionPlate(400, 20, 275, 430), edge: 200, exposed: true),
            _ => Input(Adapt(count: 1))
        };
        var result = BraceAnglePairBoltedTransfer.Verify(p);
        Unsupported(result);
        Assert.AreEqual(0, result.AngleSections.Count, "Invalid detail must not expose an unjustified favourable angle child.");
    }

    [DataTestMethod]
    [DataRow(0)] [DataRow(1)] [DataRow(2)]
    public void GeometryModelAndCombinationEvidenceAreAllRequired(int missing)
    {
        var p = missing == 0 ? Input(geometry: " ") : missing == 1 ? Input(distribution: " ") : Input(combination: " ");
        Unsupported(BraceAnglePairBoltedTransfer.Verify(p));
    }

    [TestMethod]
    public void UnequalButEquilibratedChordTensionsAreNotPretendedToBeEqualDoubleShear()
    {
        var a = Adapt(first: 50000, second: 150000);
        var r = BraceAnglePairBoltedTransfer.Verify(Input(a)); Unsupported(r);
        Assert.IsTrue(r.Diagnostics.Contains("EQUAL_EXPLICIT_CHORD_TENSIONS_REQUIRED_FOR_DOUBLE_SHEAR"));
        Equal(50000, r.FaceDemands[0].AtGussetFace.Fx); Equal(150000, r.FaceDemands[1].AtGussetFace.Fx);
        Assert.AreNotEqual(0d, r.ActionsOnGussetAtBoltGroup.My);
    }

    [TestMethod]
    public void SingleAngleCannotBeReinterpretedAsASymmetricPair()
    {
        var a = BraceAngleModelAdapter.FromSingle(Snapshot(new SectionL(100, 10, 150, 10, "single")),
            Ec(), new SteelActions(200000, 0, 0, 0, 0, 0), BraceAngleConnectedLeg.Second, Bolt(), 4, 60, 75);
        var r = BraceAnglePairBoltedTransfer.Verify(Input(a)); Unsupported(r);
        Assert.AreEqual(0, r.FaceDemands.Count); Assert.IsNull(r.InPlaneBoltGroup);
    }

    [TestMethod]
    public void ZeroForcesStillDoNotQualifyMissingLocalAndGussetChecks()
    {
        var r = BraceAnglePairBoltedTransfer.Verify(Input(Adapt(first: 0, second: 0)));
        Assert.AreEqual(ConnectionStatus.Passed, r.InPlaneComponents.Status);
        Equal(0, r.InPlaneComponents.Utilization!.Value);
        Assert.AreEqual(ConnectionStatus.NotSupported, r.Status); Assert.IsFalse(r.CanQualifyWholeJoint);
        Assert.IsTrue(r.Diagnostics.Contains("LOCAL_3D_CONTACT_PRYING_RESPONSE_NOT_IMPLEMENTED"));
    }

    [TestMethod]
    public void PlaneFailureRemainsVisibleWhileFull3dQualificationIsUnavailable()
    {
        var r = BraceAnglePairBoltedTransfer.Verify(Input(Adapt(first: 500000, second: 500000)));
        Assert.AreEqual(ConnectionStatus.Failed, r.InPlaneComponents.Status);
        Assert.IsTrue(r.InPlaneComponents.Utilization > 1);
        Assert.AreEqual(ConnectionStatus.NotSupported, r.Status);
    }

    [TestMethod]
    public void RealRootAndToeRadiiReduceOnlyTheFlatBearingStripNotTheActualAngleArea()
    {
        var r = BraceAnglePairBoltedTransfer.Verify(Input(Adapt(root: 15, toeRadius: 7.5)));
        Assert.AreEqual(ConnectionStatus.Passed, r.InPlaneComponents.Status,
            string.Join("; ", r.InPlaneComponents.Diagnostics) + "; source M1="
            + r.Input.Angles.SourceActions.M1.ToString("R") + "; source M2=" + r.Input.Angles.SourceActions.M2.ToString("R"));
        Equal(25, r.InPlaneComponents.Trace.Single(v => v.Name == "AngleFlatStripLow").Value);
        Equal(142.5, r.InPlaneComponents.Trace.Single(v => v.Name == "AngleFlatStripHigh").Value);
        Equal(2400 + (1 - Math.PI / 4) * (225 - 2 * 56.25), r.Input.Angles.Components[0].Input.Section.GrossArea);
    }

    [TestMethod]
    public void NtcHoleEvidenceIsNotReplacedByTheExistenceOfAStringForAnotherPurpose()
    {
        var r = BraceAnglePairBoltedTransfer.Verify(Input(Adapt(code: ConnectionDesignCode.Ntc2018()), holeSource: null));
        Unsupported(r);
    }

    [TestMethod]
    public void SourceCaseAndSnapshotArePreservedAndExportedModelMutationsDoNotChangeResults()
    {
        var a = Adapt(); var p = Input(a); var r = BraceAnglePairBoltedTransfer.Verify(p);
        Assert.AreSame(a, r.Input.Angles); Assert.AreEqual(p.ActionCombinationSource, r.Input.ActionCombinationSource);
        Assert.AreEqual(p.GeometrySource, r.Input.GeometrySource);
        var copy = a.SourceSection.CreateSectionCopy();
        ((SectionL)((SectionBuiltUp)copy.SectionShape).Parts[0].Section).Height = 500;
        Equal(150, r.Input.Angles.Components[0].Input.Section.SecondLeg);
        Equal(5250000, r.ActionsOnGussetAtBoltGroup.Mz);
        Assert.ThrowsException<NotSupportedException>(() => ((IList<BraceAnglePairFaceDemand>)r.FaceDemands).Clear());
        Assert.ThrowsException<NotSupportedException>(() => ((IList<string>)r.MissingWholeJointChecks).Clear());
    }

    [DataTestMethod]
    [DataRow(double.NaN)] [DataRow(double.PositiveInfinity)] [DataRow(-1.0)] [DataRow(0.0)]
    public void InvalidDimensionsAreRejectedBeforeAnyFavourableResult(double bad)
        => Assert.ThrowsException<ArgumentOutOfRangeException>(() => Input(angleEnd: bad));
}
