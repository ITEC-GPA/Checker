using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class FilletWeldTests
{
    private static ConnectionDesignCode Ec(double gm2 = 1.25)
        => ConnectionDesignCode.Eurocode3(1, 1.1, gm2, "Explicit weld benchmark parameters, EN1993-1-8:2005");
    private static FilletWeldMaterial Material(FilletWeldSteelGrade grade = FilletWeldSteelGrade.S275,
        double fu = 410, double thickness = 10) => new(grade, fu, thickness);
    private static FilletWeldSegment Segment(double x1, double y1, double x2, double y2, double throat = 6,
        FilletWeldEndCondition ends = FilletWeldEndCondition.FullSize, double angle = Math.PI / 4)
        => new(new(x1, y1), new(x2, y2), throat, ends, angle);
    private static FilletWeldSegment[] Pair(double throat = 6)
        => new[] { Segment(-50, -30, 50, -30, throat), Segment(-50, 30, 50, 30, throat) };
    private static FilletWeldGroupInput Input(ConnectionDesignCode? code = null, IEnumerable<FilletWeldSegment>? segments = null,
        PlanarConnectionActions? actions = null, FilletWeldMethod method = FilletWeldMethod.Simplified,
        FilletWeldMaterial? first = null, FilletWeldMaterial? second = null, double lap = 0,
        string? consumable = "Test hypothesis: compatible matching consumable",
        bool local = true, string? localSource = "Test roots and application point explicitly modelled; no omitted local couple")
        => new(code ?? Ec(), segments ?? Pair(), first ?? Material(), second ?? Material(),
            actions ?? new(12000, 0, 0, 0, 0, 0), method, consumable!, local, localSource, lap);
    private static double Trace(FilletWeldGroupResult r, string name) => r.Trace.Single(v => v.Name == name).Value;
    private static void Equal(double expected, double actual, double relative = 1e-11)
        => Assert.AreEqual(expected, actual, Math.Max(1e-9, Math.Abs(expected) * relative));
    private static void Unsupported(FilletWeldGroupResult r, string reason)
    {
        Assert.AreEqual(ConnectionStatus.NotSupported, r.Status); Assert.IsFalse(r.IsComplete);
        Assert.AreEqual(0, r.Points.Count); Assert.IsNull(r.Utilization); Assert.IsNull(r.GoverningPoint);
        CollectionAssert.Contains(r.Diagnostics.ToArray(), reason);
    }

    [DataTestMethod]
    [DataRow(FilletWeldSteelGrade.S275, 410.0, 223.0, false)]
    [DataRow(FilletWeldSteelGrade.S355, 470.0, 241.0, false)]
    [DataRow(FilletWeldSteelGrade.S275, 410.0, 273.0, true)]
    [DataRow(FilletWeldSteelGrade.S355, 470.0, 295.0, true)]
    public void PublishedSciP358TableG37WeldStrengths(FilletWeldSteelGrade grade, double fu, double published, bool transverse)
    {
        // SCI P358 (2014), Table G.37, printed T-144 (PDF442), with parent fu from G.36.
        // Published component resistances rounded to 1 MPa, NOT a published complete-joint benchmark.
        // Transverse loading of a 90-degree fillet resolves as sigma=tau=1/sqrt(2).
        var material = Material(grade, fu);
        var r = FilletWeldResistance.Verify(Ec(), material, material,
            transverse ? 1 / Math.Sqrt(2) : 0, transverse ? 1 / Math.Sqrt(2) : 0,
            transverse ? 0 : 1, FilletWeldMethod.Directional);
        Assert.IsTrue(r.IsComplete);
        Assert.AreEqual(published, 1 / r.Utilization!.Value, .5, "Publication rounded to nearest MPa.");
    }

    [DataTestMethod]
    [DataRow(false, FilletWeldSteelGrade.S235, .8)] [DataRow(true, FilletWeldSteelGrade.S235, .8)]
    [DataRow(false, FilletWeldSteelGrade.S275, .85)] [DataRow(true, FilletWeldSteelGrade.S275, .85)]
    [DataRow(false, FilletWeldSteelGrade.S355, .9)] [DataRow(true, FilletWeldSteelGrade.S355, .9)]
    [DataRow(false, FilletWeldSteelGrade.S420, 1.0)] [DataRow(true, FilletWeldSteelGrade.S420, 1.0)]
    [DataRow(false, FilletWeldSteelGrade.S460, 1.0)] [DataRow(true, FilletWeldSteelGrade.S460, 1.0)]
    public void NormativeBetaAndCombinedStressHandCalculation(bool ntc, FilletWeldSteelGrade grade, double beta)
    {
        // Independently evaluated EN1993-1-8 eq4.1 / NTC2018 eq4.2.81, actual throat plane.
        var m = Material(grade, 550);
        var r = FilletWeldResistance.Verify(ntc ? ConnectionDesignCode.Ntc2018() : Ec(), m, m, 100, -30, 40, FilletWeldMethod.Directional);
        Equal(beta, r.Beta); Equal(Math.Sqrt(17500), r.Checks[0].Demand);
        Equal(550 / (beta * 1.25), r.Checks[0].Resistance); Equal(396, r.Checks[1].Resistance);
        Equal(Math.Max(Math.Sqrt(17500) * beta * 1.25 / 550, 100.0 / 396), r.Utilization!.Value);
        Assert.IsTrue(r.Checks.All(c => c.Unit == "MPa"));
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void WeakerMaterialControlsFuAndLowerGradeControlsBeta(bool reverse)
    {
        var weak = Material(FilletWeldSteelGrade.S235, 360, 30); var strong = Material(FilletWeldSteelGrade.S460, 540, 10);
        var r = FilletWeldResistance.Verify(Ec(), reverse ? strong : weak, reverse ? weak : strong, 0, 100, 0, FilletWeldMethod.Simplified);
        Equal(.8, r.Beta); Equal(360, r.Fu); Equal(360 / (.8 * 1.25 * Math.Sqrt(3)), r.Checks[0].Resistance);
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void NormalCompressionEntersEquivalentStressButNotTensionCap(bool ntc)
    {
        var code = ntc ? ConnectionDesignCode.Ntc2018() : Ec();
        var tension = FilletWeldResistance.Verify(code, Material(), Material(), 400, 0, 0, FilletWeldMethod.Directional);
        var compression = FilletWeldResistance.Verify(code, Material(), Material(), -400, 0, 0, FilletWeldMethod.Directional);
        Equal(tension.Checks[0].Demand, compression.Checks[0].Demand);
        Equal(0, compression.Checks[1].Demand); Equal(400, tension.Checks[1].Demand);
        Assert.AreEqual(ConnectionStatus.Failed, compression.Status, "Compression still fails the quadratic criterion.");
        Assert.IsTrue(tension.Utilization > compression.Utilization);
    }

    [DataTestMethod]
    [DataRow(FilletWeldMethod.Directional)] [DataRow(FilletWeldMethod.Simplified)]
    public void ExactZeroAndSmallStressesAreRetained(FilletWeldMethod method)
    {
        var zero = FilletWeldResistance.Verify(Ec(), Material(), Material(), 0, 0, 0, method);
        var small = FilletWeldResistance.Verify(Ec(), Material(), Material(), 0, 0, 1e-12, method);
        Equal(0, zero.Utilization!.Value); Assert.AreEqual(ConnectionStatus.Passed, zero.Status);
        Assert.IsTrue(small.Utilization > 0);
        Assert.AreEqual(1e-12 * .85 * 1.25 * Math.Sqrt(3) / 410, small.Utilization!.Value, 1e-26);
    }

    [TestMethod]
    public void BoltAndPlateFactorsCannotLeakIntoWeldResistance()
    {
        var code = ConnectionDesignCode.Eurocode3(1.05, 1.1, 1.5, "Explicit distinct test factors");
        var r = FilletWeldResistance.Verify(code, Material(), Material(), 0, 0, 1, FilletWeldMethod.Simplified);
        Equal(410 / (.85 * 1.5 * Math.Sqrt(3)), r.Checks[0].Resistance);
    }

    [TestMethod]
    public void TwoParallelWeldsMatchSixActionAnalyticalSolution()
    {
        var r = FilletWeldGroup.Verify(Input(actions: new(2400, 3600, 12000, 108000, -200000, 208000)));
        Assert.IsTrue(r.IsComplete); Equal(1200, Trace(r, "Area")); Equal(0, Trace(r, "CentroidX")); Equal(0, Trace(r, "CentroidY"));
        Equal(1080000, Trace(r, "Ix")); Equal(1000000, Trace(r, "Iy")); Equal(0, Trace(r, "Ixy")); Equal(2080000, Trace(r, "J"));
        foreach (var p in r.Points)
        {
            Equal(2 - .1 * p.Position.Y, p.StressX); Equal(3 + .1 * p.Position.X, p.StressY);
            Equal(10 + .2 * p.Position.X + .1 * p.Position.Y, p.StressZ);
            Equal(6 * p.StressX, p.ForcePerLengthX); Equal(6 * p.StressY, p.ForcePerLengthY); Equal(6 * p.StressZ, p.ForcePerLengthZ);
            Equal(Math.Sqrt(p.StressX * p.StressX + p.StressY * p.StressY + p.StressZ * p.StressZ)
                * .85 * 1.25 * Math.Sqrt(3) / 410, p.Verification.Utilization!.Value);
        }
        Equal(108000, Trace(r, "RecoveredMx")); Equal(-200000, Trace(r, "RecoveredMy")); Equal(208000, Trace(r, "RecoveredMz"));
    }

    [DataTestMethod]
    [DataRow(FilletWeldMethod.Directional)] [DataRow(FilletWeldMethod.Simplified)]
    public void AsymmetricUnequalThroatGroupSatisfiesIndependentIntegratedEquilibrium(FilletWeldMethod method)
    {
        var actions = new PlanarConnectionActions(11000, -7000, 9000, 125000, -84000, 310000, 31, -17, 23);
        var r = FilletWeldGroup.Verify(Input(segments: new[] { Segment(0, 0, 120, 0, 4), Segment(0, 0, 0, 80, 8) }, actions: actions, method: method));
        Assert.IsTrue(r.IsComplete); Equal(1120, Trace(r, "Area")); Equal(480.0 * 60 / 1120, Trace(r, "CentroidX"));
        Equal(640.0 * 40 / 1120, Trace(r, "CentroidY"));
        // Independent Simpson integration of returned line forces; exact for linear force and quadratic moment.
        double fx = 0, fy = 0, fz = 0, mx = 0, my = 0, mz = 0;
        foreach (var g in r.Points.GroupBy(p => p.SegmentIndex))
        {
            var ends = g.ToArray(); var p = ends[0]; var q = ends[1];
            double length = Math.Sqrt(Math.Pow(q.Position.X - p.Position.X, 2) + Math.Pow(q.Position.Y - p.Position.Y, 2));
            for (int i = 0; i <= 2; i++)
            {
                double s = i / 2.0, weight = length / 6 * (i == 1 ? 4 : 1);
                double x = p.Position.X * (1 - s) + q.Position.X * s, y = p.Position.Y * (1 - s) + q.Position.Y * s;
                double vx = p.ForcePerLengthX * (1 - s) + q.ForcePerLengthX * s;
                double vy = p.ForcePerLengthY * (1 - s) + q.ForcePerLengthY * s;
                double vz = p.ForcePerLengthZ * (1 - s) + q.ForcePerLengthZ * s;
                fx += vx * weight; fy += vy * weight; fz += vz * weight;
                mx += y * vz * weight; my -= x * vz * weight; mz += (x * vy - y * vx) * weight;
            }
        }
        Equal(actions.Fx, fx); Equal(actions.Fy, fy); Equal(actions.Fz, fz);
        Equal(actions.Mx + actions.ApplicationY * actions.Fz - actions.ApplicationZ * actions.Fy, mx);
        Equal(actions.My + actions.ApplicationZ * actions.Fx - actions.ApplicationX * actions.Fz, my);
        Equal(actions.Mz + actions.ApplicationX * actions.Fy - actions.ApplicationY * actions.Fx, mz);
    }

    [DataTestMethod]
    [DataRow(FilletWeldMethod.Directional)] [DataRow(FilletWeldMethod.Simplified)]
    public void RotatingAxesAndTranslatingOriginPreservesResults(FilletWeldMethod method)
    {
        var roots = new[] { Segment(0, 0, 120, 0, 4), Segment(0, 0, 0, 80, 8) };
        var a = new PlanarConnectionActions(11000, -7000, 9000, 125000, -84000, 310000, 31, -17, 23);
        var baseline = FilletWeldGroup.Verify(Input(segments: roots, actions: a, method: method));
        double angle = .71, c = Math.Cos(angle), s = Math.Sin(angle), ox = 1e6, oy = -2e6;
        ConnectionPoint2D Transform(ConnectionPoint2D p) => new(c * p.X - s * p.Y + ox, s * p.X + c * p.Y + oy);
        var rotated = roots.Select(w => new FilletWeldSegment(Transform(w.Start), Transform(w.End), w.Throat, w.EndCondition, w.ThroatNormalAngleRadians));
        var ap = Transform(new(a.ApplicationX, a.ApplicationY));
        var actions = new PlanarConnectionActions(c * a.Fx - s * a.Fy, s * a.Fx + c * a.Fy, a.Fz,
            c * a.Mx - s * a.My, s * a.Mx + c * a.My, a.Mz, ap.X, ap.Y, a.ApplicationZ);
        var r = FilletWeldGroup.Verify(Input(segments: rotated, actions: actions, method: method));
        Assert.IsTrue(r.IsComplete); Equal(baseline.Utilization!.Value, r.Utilization!.Value, 1e-9);
        for (int i = 0; i < baseline.Points.Count; i++)
        {
            var p = baseline.Points[i]; var q = r.Points[i];
            Equal(c * p.StressX - s * p.StressY, q.StressX, 1e-9);
            Equal(s * p.StressX + c * p.StressY, q.StressY, 1e-9); Equal(p.StressZ, q.StressZ, 1e-9);
            Equal(p.Verification.SigmaPerpendicular, q.Verification.SigmaPerpendicular, 1e-9);
        }
    }

    [TestMethod]
    public void ReversingSegmentAndThroatAnglePreservesPhysicalNormal()
    {
        var original = Segment(-50, 0, 50, 0, angle: .63);
        var reversed = new FilletWeldSegment(original.End, original.Start, 6, FilletWeldEndCondition.FullSize, Math.PI - .63);
        var actions = new PlanarConnectionActions(1000, 2000, 3000, 0, -50000, 60000);
        var a = FilletWeldGroup.Verify(Input(segments: new[] { original }, actions: actions, method: FilletWeldMethod.Directional));
        var b = FilletWeldGroup.Verify(Input(segments: new[] { reversed }, actions: actions, method: FilletWeldMethod.Directional));
        Assert.IsTrue(a.IsComplete); Assert.IsTrue(b.IsComplete); Equal(a.Utilization!.Value, b.Utilization!.Value);
        Equal(a.Points[0].Verification.SigmaPerpendicular, b.Points[1].Verification.SigmaPerpendicular);
    }

    [DataTestMethod]
    [DataRow(false, 100.0)] [DataRow(true, 100.0)]
    public void FullSizeEndsKeepEntireLength(bool ntc, double expected)
    {
        var r = FilletWeldGroup.Verify(Input(code: ntc ? ConnectionDesignCode.Ntc2018() : Ec(), segments: new[] { Segment(0, 0, 100, 0) }));
        Assert.IsTrue(r.IsComplete); Equal(expected, Trace(r, "Segment0.EffectiveLength")); Equal(600, Trace(r, "Area"));
    }

    [DataTestMethod]
    [DataRow(false, 88.0)] [DataRow(true, 100.0)]
    public void EndLengthRulesAreDistinctForEcAndNtc(bool ntc, double expected)
    {
        var r = FilletWeldGroup.Verify(Input(code: ntc ? ConnectionDesignCode.Ntc2018() : Ec(),
            segments: new[] { Segment(0, 0, 100, 0, ends: FilletWeldEndCondition.NominalWithoutFullSizeEvidence) }));
        Assert.IsTrue(r.IsComplete); Equal(expected, Trace(r, "Segment0.EffectiveLength")); Equal(6 * expected, Trace(r, "Area"));
        Equal(ntc ? 0 : 6, r.Points[0].Position.X); Equal(ntc ? 100 : 94, r.Points[1].Position.X);
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void DefectiveEndsAreNeverAssignedResistance(bool ntc)
        => Unsupported(FilletWeldGroup.Verify(Input(code: ntc ? ConnectionDesignCode.Ntc2018() : Ec(),
            segments: new[] { Segment(0, 0, 100, 0, ends: FilletWeldEndCondition.Defective) })), "SEGMENT_0_DEFECTIVE_ENDS");

    [DataTestMethod]
    [DataRow(3.0, 30.0, true)] [DataRow(3.0, 29.999, false)]
    [DataRow(6.0, 36.0, true)] [DataRow(6.0, 35.999, false)]
    public void EffectiveMinimumLengthBoundary(double throat, double length, bool allowed)
    {
        var r = FilletWeldGroup.Verify(Input(segments: new[] { Segment(0, 0, length, 0, throat) }));
        if (allowed) Assert.IsTrue(r.IsComplete);
        else Unsupported(r, "SEGMENT_0_EFFECTIVE_LENGTH_BELOW_SCOPE");
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void MaterialAndThroatScopeAreChecked(bool ntc)
    {
        var code = ntc ? ConnectionDesignCode.Ntc2018() : Ec();
        Unsupported(FilletWeldGroup.Verify(Input(code: code, first: Material(thickness: 3.999))), "PARENT_THICKNESS_BELOW_4_MM_SCOPE");
        Unsupported(FilletWeldGroup.Verify(Input(code: code, segments: Pair(2.999))), "SEGMENT_0_THROAT_BELOW_3_MM_SCOPE");
        Assert.IsTrue(FilletWeldGroup.Verify(Input(code: code, first: Material(thickness: 4), segments: Pair(3))).IsComplete);
    }

    [DataTestMethod]
    [DataRow(60.0, true)] [DataRow(120.0, true)] [DataRow(59.999, false)] [DataRow(120.001, false)]
    public void FusionFaceAngleLimitsAreExplicit(double angle, bool supported)
    {
        var root = new FilletWeldSegment(new(0, 0), new(100, 0), 6, FilletWeldEndCondition.FullSize, fusionAngleDegrees: angle);
        var r = FilletWeldGroup.Verify(Input(segments: new[] { root }));
        if (supported) Assert.IsTrue(r.IsComplete);
        else Unsupported(r, "SEGMENT_0_FUSION_ANGLE_OUTSIDE_FILLET_SCOPE");
    }

    [DataTestMethod]
    [DataRow(false, 899.0, 1.0)] [DataRow(false, 900.0, 1.0)] [DataRow(false, 1800.0, .8)]
    [DataRow(true, 1800.0, .8)] [DataRow(false, 3600.0, .4)]
    public void LongLapReductionMatchesEcEquation49WithoutInventedLowerClamp(bool ntc, double lap, double beta)
    {
        // EC4.11(3) is used as an explicit supplementary model for NTC, not as an NTC equation.
        var r = FilletWeldGroup.Verify(Input(code: ntc ? ConnectionDesignCode.Ntc2018() : Ec(), lap: lap));
        Assert.IsTrue(r.IsComplete); Equal(beta, Trace(r, "Segment0.LongJointFactor"));
        Equal(beta * 410 / (.85 * 1.25 * Math.Sqrt(3)), r.Points[0].Verification.Checks[0].Resistance);
    }

    [TestMethod]
    public void LongLapAppliesEachThroatAndRejectsNonpositiveFactor()
    {
        var roots = new[] { Segment(0, -30, 100, -30, 4), Segment(0, 30, 100, 30, 8) };
        var r = FilletWeldGroup.Verify(Input(segments: roots, lap: 1200));
        Equal(.8, Trace(r, "Segment0.LongJointFactor")); Equal(1, Trace(r, "Segment1.LongJointFactor"));
        Unsupported(FilletWeldGroup.Verify(Input(lap: 6000)), "SEGMENT_0_LONG_LAP_FACTOR_NONPOSITIVE");
    }

    [TestMethod]
    public void LongNonlapWeldNeedsSpecificTransferModel()
    {
        Unsupported(FilletWeldGroup.Verify(Input(segments: new[] { Segment(0, 0, 1700.001, 0) })), "SEGMENT_0_LONG_NONLAP_WELD_REQUIRES_SPECIAL_MODEL");
        Assert.IsTrue(FilletWeldGroup.Verify(Input(segments: new[] { Segment(0, 0, 1700, 0) })).IsComplete);
    }

    [TestMethod]
    public void SingleStraightRootCarriesResolvableBendingButRejectsTwistAboutIt()
    {
        var root = new[] { Segment(-50, 0, 50, 0) };
        var r = FilletWeldGroup.Verify(Input(segments: root, actions: new(0, 0, 6000, 0, -50000, 0)));
        Assert.IsTrue(r.IsComplete); Equal(5, r.Points[0].StressZ); Equal(15, r.Points[1].StressZ);
        Unsupported(FilletWeldGroup.Verify(Input(segments: root, actions: new(0, 0, 0, 1e-12, 0, 0))), "BENDING_NOT_REPRESENTABLE_BY_LINE_GROUP");
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void TinyUnsupportedMomentIsNotHiddenByLargeResolvableMoment(bool vertical)
    {
        var roots = new[] { vertical ? Segment(0, -50, 0, 50) : Segment(-50, 0, 50, 0) };
        var actions = vertical ? new PlanarConnectionActions(0, 0, 0, 1e12, 1e-12, 0)
            : new PlanarConnectionActions(0, 0, 0, 1e-12, 1e12, 0);
        Unsupported(FilletWeldGroup.Verify(Input(segments: roots, actions: actions)), "BENDING_NOT_REPRESENTABLE_BY_LINE_GROUP");
    }

    [TestMethod]
    public void ObliqueSingleRootCarriesCompatibleMomentAndRecoversAllResultants()
    {
        // Root tangent=(2,1)/sqrt(5); target(-My,Mx)=(200000,100000) is parallel to it.
        var r = FilletWeldGroup.Verify(Input(segments: new[] { Segment(-100, -50, 100, 50) },
            actions: new(5000, 7000, 9000, 100000, -200000, 300000)));
        Assert.IsTrue(r.IsComplete, string.Join(";", r.Diagnostics));
        Equal(5000, Trace(r, "RecoveredFx")); Equal(7000, Trace(r, "RecoveredFy")); Equal(9000, Trace(r, "RecoveredFz"));
        Equal(100000, Trace(r, "RecoveredMx")); Equal(-200000, Trace(r, "RecoveredMy")); Equal(300000, Trace(r, "RecoveredMz"));
    }

    [TestMethod]
    public void GenuinelyAlmostCollinearGroupIsNotSilentlyReplacedByRankOne()
    {
        // Two separate roots with an actual offset: determinant is small but rank is genuinely two.
        var roots = new[] { Segment(-100, 0, -50, 0), Segment(50, .00001, 100, .00001) };
        Unsupported(FilletWeldGroup.Verify(Input(segments: roots, actions: new(0, 0, 0, 1, 100000, 0))), "BENDING_NOT_REPRESENTABLE_BY_LINE_GROUP");
    }

    [TestMethod]
    public void CoincidentRootsRejectedButTouchingEndpointsAllowed()
    {
        var a = Segment(0, 0, 100, 0); var same = Segment(100, 0, 0, 0);
        Unsupported(FilletWeldGroup.Verify(Input(segments: new[] { a, same })), "OVERLAPPING_ROOTS_0_1");
        Unsupported(FilletWeldGroup.Verify(Input(segments: new[] { a, Segment(50, 0, 150, 0) })), "OVERLAPPING_ROOTS_0_1");
        Assert.IsTrue(FilletWeldGroup.Verify(Input(segments: new[] { a, Segment(100, 0, 200, 0) })).IsComplete);
    }

    [TestMethod]
    public void MissingExecutionAndEccentricityEvidenceCannotYieldPass()
    {
        Unsupported(FilletWeldGroup.Verify(Input(consumable: null)), "CONSUMABLE_COMPATIBILITY_EVIDENCE_REQUIRED");
        Unsupported(FilletWeldGroup.Verify(Input(local: false)), "LOCAL_ECCENTRICITY_MODEL_REQUIRED");
        Unsupported(FilletWeldGroup.Verify(Input(localSource: "  ")), "LOCAL_ECCENTRICITY_MODEL_REQUIRED");
    }

    [TestMethod]
    public void ElasticLoadScalingChangesUtilizationAndCanProduceFailure()
    {
        var r = FilletWeldGroup.Verify(Input(actions: new(12000, 0, 0, 0, 0, 0)));
        double capacity = 1200 * 410 / (.85 * 1.25 * Math.Sqrt(3));
        var failed = FilletWeldGroup.Verify(Input(actions: new(capacity * 1.001, 0, 0, 0, 0, 0)));
        Equal(12000 / capacity, r.Utilization!.Value); Equal(1.001, failed.Utilization!.Value);
        Assert.AreEqual(ConnectionStatus.Failed, failed.Status);
        Assert.AreEqual(ConnectionStatus.Passed, FilletWeldGroup.Verify(Input(actions: new(0, 0, 0, 0, 0, 0))).Status);
    }

    [TestMethod]
    public void EndpointExtremaBoundEveryInteriorPointOfAffineStressField()
    {
        var r = FilletWeldGroup.Verify(Input(actions: new(24000, -36000, 120000, 1080000, -2000000, 2080000), method: FilletWeldMethod.Directional));
        foreach (var group in r.Points.GroupBy(p => p.SegmentIndex))
        {
            var ends = group.ToArray(); double maximum = ends.Max(p => p.Verification.Utilization!.Value);
            for (int i = 1; i < 10; i++)
            {
                double t = i / 10.0;
                var a = ends[0].Verification; var b = ends[1].Verification;
                double sigma = a.SigmaPerpendicular * (1 - t) + b.SigmaPerpendicular * t;
                double perp = a.TauPerpendicular * (1 - t) + b.TauPerpendicular * t;
                double parallel = a.TauParallel * (1 - t) + b.TauParallel * t;
                double utilization = Math.Max(Math.Sqrt(sigma * sigma + 3 * (perp * perp + parallel * parallel)) / (410 / (.85 * 1.25)),
                    Math.Max(0, sigma) / (.9 * 410 / 1.25));
                Assert.IsTrue(utilization <= maximum + 1e-12);
            }
        }
    }

    [TestMethod]
    public void InputSnapshotAndCollectionsAreImmutable()
    {
        var roots = Pair(); var input = Input(segments: roots); roots[0] = Segment(0, 0, 200, 0);
        var r = FilletWeldGroup.Verify(input); Equal(1200, Trace(r, "Area"));
        Assert.ThrowsException<NotSupportedException>(() => ((IList<FilletWeldSegment>)input.Segments).Clear());
        Assert.ThrowsException<NotSupportedException>(() => ((IList<FilletWeldPointResult>)r.Points).Clear());
    }

    [TestMethod]
    public void InvalidInputIsRejectedBeforeCalculation()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Material(fu: double.NaN));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Material(thickness: double.PositiveInfinity));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Material((FilletWeldSteelGrade)999));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Segment(0, 0, 0, 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Segment(0, 0, 100, 0, throat: 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Segment(0, 0, 100, 0, angle: double.NaN));
        Assert.ThrowsException<ArgumentException>(() => Input(segments: Array.Empty<FilletWeldSegment>()));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Input(lap: -1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => FilletWeldResistance.Verify(Ec(), Material(), Material(), double.NaN, 0, 0, FilletWeldMethod.Simplified));
    }
}
