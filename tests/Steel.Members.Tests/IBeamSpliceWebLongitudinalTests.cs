using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Steel.Members.Tests.IBeamSplicePlateTests;

namespace Steel.Members.Tests;

[TestClass]
public class IBeamSpliceWebLongitudinalTests
{
    private const string P = "Web.Longitudinal.";
    private static ConnectionVerification<IBeamSplicePlateInput> Verify(IBeamSplicePlateInput? input = null)
        => IBeamSpliceWebLongitudinal.Verify(input ?? Input());
    private static double T(ConnectionVerification<IBeamSplicePlateInput> result, string name) => Trace(result, P + name);
    private static IBeamSplicePlateInput Actions(double n, double v, double m)
        => Input(forces: IBeamSpliceForceTests.Input(actions: new(n, v, 0, 0, 0, m)));

    [DataTestMethod]
    [DataRow("GrossArea", 3400d)]
    [DataRow("ElasticModulus", 192666.66666666666)]
    [DataRow("CoverHalfLength", 205d)]
    [DataRow("MaximumUnrestrainedPitch", 140d)]
    [DataRow("EffectiveBucklingLength", 84d)]
    [DataRow("Slenderness", .33517894985011226)]
    [DataRow("Chi", .9311194460995771)]
    [DataRow("CutCount", 11d)]
    [DataRow("MaximumCompression.X", -70d)]
    [DataRow("MaximumCompression.ColumnLimit", 1d)]
    [DataRow("MaximumCompression.AxialForce", -31912.280701754386)]
    [DataRow("MaximumCompression.ShearForce", -75000d)]
    [DataRow("MaximumCompression.Moment", -24139551.92743764)]
    [DataRow("MaximumCompression.Stress", 134.6777569232027)]
    public void ExactP398GeometryHasIndependentSectionAndStripValues(string name, double expected)
    {
        var r = Verify(); Assert.AreEqual(ConnectionStatus.Passed, r.Status);
        Equal(expected, T(r, name));
    }

    [TestMethod]
    public void CalculatedBucklingRatioBelongsOnlyToTheLongitudinalComponent()
    {
        var r = Verify();
        Equal(256.0578476773837, Check(r, P + "InterFastenerBuckling").Resistance);
        Equal(.5259661367336336, r.Utilization!.Value);
        Equal(275, Check(r, P + "GrossCompression").Resistance);
        Assert.AreEqual("MPa", Check(r, P + "InterFastenerBuckling").Unit);
        StringAssert.Contains(r.Scope, "LONGITUDINAL WEB-COVER COMPONENT ONLY");
        StringAssert.Contains(r.Scope, "No complete N-V-M plate stability");
        StringAssert.Contains(r.Scope, "block tearing");
        StringAssert.Contains(string.Join(";", r.Assumptions), "concomitant V");
    }

    [TestMethod]
    public void BothLimitsOfEachColumnKeepTheAxialForceCoupleAndConcomitantShear()
    {
        var r = Verify();
        Equal(-155, T(r, "Cut2.X")); Equal(0, T(r, "Cut2.ColumnLimit"));
        Equal(-155, T(r, "Cut3.X")); Equal(1, T(r, "Cut3.ColumnLimit"));
        Equal(0, T(r, "Cut2.Moment"));
        // Independent sum of the three point forces at x=-155 for EACH cover.
        Equal(-15956.140350877187, T(r, "Cut3.AxialForce"));
        Equal(13410.67208399342, T(r, "Cut3.ShearForce"));
        Equal(-11499822.4001491, T(r, "Cut3.Moment"));
        Equal(-12639729.527288541, T(r, "Cut4.Moment"));
        Equal(64.38064197248517, T(r, "Cut3.CompressionStress"));
        Equal(70.29711495071754, T(r, "Cut4.CompressionStress"));
        // The group centroid moment is neither this section moment nor its maximum.
        Assert.IsTrue(Math.Abs(T(r, "MaximumCompression.Moment") - (-27327051.92743764)) > 3e6);
    }

    [TestMethod]
    public void PiecewiseMomentSlopesAndJointCutReproduceStatics()
    {
        var r = Verify();
        Equal(T(r, "Cut3.AxialForce"), T(r, "Cut4.AxialForce"));
        Equal(T(r, "Cut3.ShearForce"), T(r, "Cut4.ShearForce"));
        Equal(-85 * T(r, "Cut3.ShearForce"), T(r, "Cut4.Moment") - T(r, "Cut3.Moment"));
        Equal(0, T(r, "Cut6.X")); Equal(-1, T(r, "Cut6.ColumnLimit"));
        Equal(-18889551.92743764, T(r, "Cut6.Moment"));
        Equal(-70 * T(r, "Cut5.ShearForce"), T(r, "Cut6.Moment") - T(r, "Cut5.Moment"));
        Equal(107.42862197510583, T(r, "Cut6.CompressionStress"));
    }

    [TestMethod]
    public void BothActualFreeEndsAreIncludedAndTheRightOneClosesEquilibrium()
    {
        var r = Verify();
        Equal(-205, T(r, "Cut1.X")); Equal(205, T(r, "Cut11.X"));
        Equal(0, T(r, "Cut1.AxialForce")); Equal(0, T(r, "Cut1.Moment"));
        Assert.IsTrue(Math.Abs(T(r, "RightFreeEdgeResidual.N")) < 1e-8);
        Assert.IsTrue(Math.Abs(T(r, "RightFreeEdgeResidual.V")) < 1e-8);
        Assert.IsTrue(Math.Abs(T(r, "RightFreeEdgeResidual.M")) < 1e-6);
        Assert.AreEqual(0, r.Diagnostics.Count);
    }

    [DataTestMethod]
    [DataRow(-150000d)] [DataRow(150000d)] [DataRow(0d)]
    public void PureAxialForceHasNoInventedBendingOrCompressionFromTension(double axial)
    {
        var r = Verify(Actions(axial, 0, 0));
        double expected = Math.Max(0, -axial * (428 * 8.5 / 8550) / (2 * 3400));
        Equal(expected, T(r, "MaximumCompression.Stress"));
        foreach (var value in r.Trace.Where(t => t.Name.StartsWith(P + "Cut") && t.Name.EndsWith(".Moment")))
            Assert.AreEqual(0, value.Value, 1e-8);
        if (axial >= 0) Equal(0, r.Utilization!.Value);
    }

    [DataTestMethod] [DataRow(-200000000d)] [DataRow(200000000d)]
    public void PureMomentProducesCompressionDespiteZeroTotalAxialForceAndShear(double moment)
    {
        var r = Verify(Actions(0, 0, moment));
        double eachMoment = Math.Abs(moment) * (8.5 * Math.Pow(428, 3) / 12) / 294e6 / 2;
        Equal(eachMoment / (10 * 340 * 340 / 6d), T(r, "MaximumCompression.Stress"));
        Assert.IsTrue(r.Utilization!.Value > 0);
        Equal(0, T(r, "Cut6.ShearForce")); Equal(0, T(r, "Cut6.AxialForce"));
    }

    [TestMethod]
    public void VerticalReflectionReversesShearAndMomentWithoutMovingTheLongitudinalCut()
    {
        var a = Verify(); var b = Verify(Actions(-150000, 150000, 200e6));
        Equal(T(a, "MaximumCompression.Stress"), T(b, "MaximumCompression.Stress"));
        // Reflection Y -> -Y reverses V and M. X and the column limit stay fixed.
        Equal(-70, T(b, "MaximumCompression.X")); Equal(1, T(b, "MaximumCompression.ColumnLimit"));
        Equal(T(a, "MaximumCompression.AxialForce"), T(b, "MaximumCompression.AxialForce"));
        Equal(-T(a, "MaximumCompression.ShearForce"), T(b, "MaximumCompression.ShearForce"));
        Equal(-T(a, "MaximumCompression.Moment"), T(b, "MaximumCompression.Moment"));
    }

    [TestMethod]
    public void LongitudinalReflectionReversesShearAndMovesTheCriticalCutToTheOppositeColumnLimit()
    {
        // Reflection X -> -X also exchanges left/right members. For actions
        // defined TO THE LEFT member, N and M remain fixed while V reverses.
        var a = Verify(); var b = Verify(Actions(-150000, 150000, -200e6));
        Equal(T(a, "MaximumCompression.Stress"), T(b, "MaximumCompression.Stress"));
        Equal(70, T(b, "MaximumCompression.X")); Equal(0, T(b, "MaximumCompression.ColumnLimit"));
        Equal(T(a, "MaximumCompression.AxialForce"), T(b, "MaximumCompression.AxialForce"));
        Equal(-T(a, "MaximumCompression.ShearForce"), T(b, "MaximumCompression.ShearForce"));
        Equal(T(a, "MaximumCompression.Moment"), T(b, "MaximumCompression.Moment"));
    }

    [TestMethod]
    public void PureShearReversalPreservesTheEnvelopeButStillCreatesLocalBending()
    {
        var a = Verify(Actions(0, -150000, 0)); var b = Verify(Actions(0, 150000, 0));
        Equal(T(a, "MaximumCompression.Stress"), T(b, "MaximumCompression.Stress"));
        Assert.IsTrue(T(a, "MaximumCompression.Stress") > 0);
        Assert.IsTrue(Math.Abs(T(a, "Cut6.Moment")) < 1e-6);
    }

    [DataTestMethod] [DataRow(1e-9)] [DataRow(.5)] [DataRow(2d)] [DataRow(1e9)]
    public void LinearLoadScalingHasNoAbsoluteDemandFloor(double scale)
    {
        var r = Verify(Actions(-150000 * scale, -150000 * scale, -200e6 * scale));
        Assert.IsTrue(r.IsComplete, string.Join(";", r.Diagnostics));
        double expected = 134.6777569232027 * scale;
        Assert.AreEqual(expected, T(r, "MaximumCompression.Stress"), Math.Abs(expected) * 1e-10);
        Equal(256.0578476773837, Check(r, P + "InterFastenerBuckling").Resistance);
        Assert.AreEqual(scale * .5259661367336336 > 1 ? ConnectionStatus.Failed : ConnectionStatus.Passed, r.Status);
    }

    [TestMethod]
    public void NtcUsesItsOwnStabilityFactorAndLeavesTheForceEnvelopeUnchanged()
    {
        var r = Verify(Input(true));
        Equal(134.6777569232027, T(r, "MaximumCompression.Stress"));
        Equal(256.0578476773837 / 1.05, Check(r, P + "InterFastenerBuckling").Resistance);
        Equal(275 / 1.05, Check(r, P + "GrossCompression").Resistance);
        Assert.IsTrue(r.Checks.All(c => c.Reference.Contains("NTC2018")));
    }

    [TestMethod]
    public void ExplicitEcGammaM1ChangesOnlyBucklingResistance()
    {
        var r = Verify(Input(gammaM1: 1.2));
        Equal(256.0578476773837 / 1.2, Check(r, P + "InterFastenerBuckling").Resistance);
        Equal(275, Check(r, P + "GrossCompression").Resistance);
        Equal(134.6777569232027, T(r, "MaximumCompression.Stress"));
    }

    [TestMethod]
    public void DoublingThicknessHalvesElasticStressAndActivatesThePitchExemption()
    {
        var r = Verify(Input(web: new(340, 20, 275, 410), gammaM1: 1.2));
        Equal(134.6777569232027 / 2, T(r, "MaximumCompression.Stress"));
        Equal(1, T(r, "BucklingExempt")); Equal(1, T(r, "Chi"));
        Equal(275, Check(r, P + "InterFastenerBuckling").Resistance);
    }

    [DataTestMethod] [DataRow(.99999999, false)] [DataRow(1.00000001, true)]
    public void NineEpsilonThresholdUsesActualPitchAndThickness(double multiplier, bool exempt)
    {
        double thickness = 140 / (9 * Math.Sqrt(235d / 275)) * multiplier;
        var r = Verify(Input(web: new(340, thickness, 275, 410)));
        Assert.IsTrue(r.IsComplete, string.Join(";", r.Diagnostics));
        Equal(exempt ? 1 : 0, T(r, "BucklingExempt"));
    }

    [TestMethod]
    public void CentralSpacingControlsEffectiveLengthRatherThanButtGapOrGroupCentroidDistance()
    {
        var a = Verify();
        var closer = Verify(Input(forces: IBeamSpliceForceTests.Input(web: new(2, 3, 85, 120, 60))));
        Equal(120, T(closer, "MaximumUnrestrainedPitch")); Equal(72, T(closer, "EffectiveBucklingLength"));
        Equal(T(a, "Slenderness") * 6 / 7, T(closer, "Slenderness"));
        Assert.IsTrue(Check(closer, P + "InterFastenerBuckling").Resistance > Check(a, P + "InterFastenerBuckling").Resistance);
        var gap = Verify(Input(gap: 20));
        Equal(T(a, "MaximumCompression.Stress"), T(gap, "MaximumCompression.Stress"));
        Equal(T(a, "MaximumUnrestrainedPitch"), T(gap, "MaximumUnrestrainedPitch"));
    }

    [TestMethod]
    public void FreeCoverEndExtensionDoesNotInventAChangedFastenerBucklingLength()
    {
        var a = Verify(); var b = Verify(Input(webEnd: 60));
        Equal(215, T(b, "CoverHalfLength")); Equal(-215, T(b, "Cut1.X"));
        Equal(T(a, "MaximumCompression.Stress"), T(b, "MaximumCompression.Stress"));
        Equal(T(a, "EffectiveBucklingLength"), T(b, "EffectiveBucklingLength"));
    }

    [TestMethod]
    public void CompressedSlenderOutstandIsUnsupportedEvenWhenCorrosionSpacingIsNotRequired()
    {
        var input = Input(forces: IBeamSpliceForceTests.Input(web: new(2, 2, 85, 60, 70)), exposed: false);
        Unsupported(Verify(input), "OUTSTAND_OUTSIDE_CLASS3");
    }

    [TestMethod]
    public void UncompressedOutstandDoesNotActivateTheCompressionOnlyGeometryGate()
    {
        var forces = IBeamSpliceForceTests.Input(web: new(2, 2, 85, 60, 70), actions: new(150000, 0, 0, 0, 0, 0));
        var r = Verify(Input(forces: forces, exposed: false));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status); Equal(0, r.Utilization!.Value);
        Equal(0, T(r, "CompressionPresent"));
        Assert.IsTrue(T(r, "OutstandThicknessRatio") > T(r, "OutstandClass3Limit"));
    }

    [TestMethod]
    public void ThreeLongitudinalRowsProduceAllFifteenCutLimits()
    {
        var r = Verify(Input(forces: IBeamSpliceForceTests.Input(web: new(3, 3, 60, 120, 70))));
        Assert.IsTrue(r.IsComplete, string.Join(";", r.Diagnostics)); Equal(15, T(r, "CutCount"));
        Equal(-240, T(r, "Cut1.X")); Equal(240, T(r, "Cut15.X"));
        Assert.IsTrue(Math.Abs(T(r, "RightFreeEdgeResidual.M")) < 1e-6);
    }

    [TestMethod]
    public void ZeroActionsHaveZeroFiniteUtilizationAndImmutableTrace()
    {
        var r = Verify(Actions(0, 0, 0));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status); Equal(0, r.Utilization!.Value);
        Assert.IsTrue(r.Trace.All(t => double.IsFinite(t.Value)));
        Assert.ThrowsException<NotSupportedException>(() => ((IList<ConnectionCheck>)r.Checks).Clear());
        Assert.ThrowsException<NotSupportedException>(() => ((IList<GPC.Checkers.Steel.Sections.SectionResistanceValue>)r.Trace).Clear());
    }

    [DataTestMethod]
    [DynamicData(nameof(OutOfScope), typeof(IBeamSplicePlateTests), DynamicDataSourceType.Method)]
    public void ExistingDetailAndActionRejectionsCannotLeaveFavorablePartialChecks(IBeamSplicePlateInput input, string reason)
        => Unsupported(Verify(input), reason);

    [TestMethod]
    public void MissingCombinationAndNullInputAreRejected()
    {
        Unsupported(Verify(Input(source: null)), "COMBINATION");
        Assert.ThrowsException<ArgumentNullException>(() => IBeamSpliceWebLongitudinal.Verify(null!));
    }
}
