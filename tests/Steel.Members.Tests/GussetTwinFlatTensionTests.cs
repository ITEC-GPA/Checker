using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class GussetTwinFlatTensionTests
{
    private static ConnectionDesignCode Ec(double gm0 = 1, double gm2p = 1.25, double gm2b = 1.25)
        => ConnectionDesignCode.Eurocode3(gm0, gm2p, gm2b, "Explicit analytical factors, not an automatic national annex");
    private static GussetTwinFlatTensionInput Input(ConnectionDesignCode? code = null, double n = 100000,
        ConnectionPlate? gusset = null, ConnectionPlate? brace = null, ConnectionBolt? bolt = null,
        int rows = 2, double pitch = 60, double gauge = 80, double gussetEnd = 40, double braceEnd = 50,
        bool exposed = false, string? holeSource = null, PlanarConnectionActions? actions = null)
        => new(code ?? Ec(), gusset ?? new(200, 16, 275, 430), brace ?? new(160, 10, 275, 430),
            bolt ?? new(20, 21, 245, ConnectionBoltGrade.Class8_8, true), actions ?? new(n, 0, 0, 0, 0, 0),
            rows, pitch, gauge, gussetEnd, braceEnd, exposed, holeSource);
    private static ConnectionVerification<GussetTwinFlatTensionInput> Verify(GussetTwinFlatTensionInput? p = null)
        => GussetTwinFlatTension.Verify(p ?? Input());
    private static double T(ConnectionVerification<GussetTwinFlatTensionInput> r, string name)
        => r.Trace.Single(v => v.Name == name).Value;
    private static ConnectionCheck C(ConnectionVerification<GussetTwinFlatTensionInput> r, string id)
        => r.Checks.Single(v => v.Id == id);
    private static void Equal(double expected, double actual)
        => Assert.AreEqual(expected, actual, Math.Max(1e-9, Math.Abs(expected) * 1e-12));
    private static void Unsupported(ConnectionVerification<GussetTwinFlatTensionInput> r, string issue)
    {
        Assert.AreEqual(ConnectionStatus.NotSupported, r.Status);
        Assert.IsFalse(r.IsComplete); Assert.IsNull(r.Utilization); Assert.IsNull(r.GoverningCheck);
        Assert.AreEqual(0, r.Checks.Count);
        Assert.IsTrue(r.Diagnostics.Any(d => d.Contains(issue)), string.Join(";", r.Diagnostics));
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void SingleOverlapMatchesIndependentCompleteLoadTransferCalculation(bool ntc)
    {
        // Constructed analytical case, EN1993-1-8 Table3.4/3.10.2, SCI P358 p179/section8.2.
        // Independent evaluator Fixtures/derive_gusset_twin_flat.py; NOT a published whole joint.
        var r = Verify(Input(code: ntc ? ConnectionDesignCode.Ntc2018() : Ec()));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status);
        Assert.AreEqual(28, r.Checks.Count); // one group: four bolts x four checks, three plates x four checks
        Equal(4, T(r, "BoltCount")); Equal(2, T(r, "ShearPlanes")); Equal(150, T(r, "OverlapLength"));
        Equal(100000, T(r, "GussetForce")); Equal(50000, T(r, "EachBraceForce"));
        for (int i = 1; i <= 4; i++)
        {
            Equal(25000, C(r, $"Bolt{i}.Shear").Demand);
            Equal(188160, C(r, $"Bolt{i}.Shear").Resistance);
            Equal(25000, C(r, $"Bolt{i}.GussetBearing").Demand);
            Equal(12500, C(r, $"Bolt{i}.Brace1Bearing").Demand);
            Equal(12500, C(r, $"Bolt{i}.Brace2Bearing").Demand);
            Equal(i <= 2 ? 174730.15873015873 : 193295.2380952381, C(r, $"Bolt{i}.GussetBearing").Resistance);
            foreach (int side in new[] { 1, 2 })
                Equal(i <= 2 ? 120809.52380952382 : 136507.9365079365, C(r, $"Bolt{i}.Brace{side}Bearing").Resistance);
        }
        Equal(3200, T(r, "Gusset.Ag")); Equal(2528, T(r, "Gusset.Anet"));
        Equal(944, T(r, "Gusset.AntInside")); Equal(1584, T(r, "Gusset.AntOutside")); Equal(2192, T(r, "Gusset.Anv"));
        Equal(ntc ? 838095.2380952381 : 880000, C(r, "Gusset.GrossYielding").Resistance);
        Equal(782668.8, C(r, "Gusset.NetRupture").Resistance);
        Equal(ntc ? 656190.040254768 : 672762.7422675064, C(r, "Gusset.BlockInside").Resistance);
        Equal(ntc ? 876350.040254768 : 892922.7422675064, C(r, "Gusset.BlockOutside").Resistance);
        foreach (int side in new[] { 1, 2 })
        {
            string name = "Brace" + side;
            Equal(1600, T(r, name + ".Ag")); Equal(1180, T(r, name + ".Anet"));
            Equal(590, T(r, name + ".AntInside")); Equal(590, T(r, name + ".AntOutside")); Equal(1570, T(r, name + ".Anv"));
            Equal(ntc ? 419047.61904761905 : 440000, C(r, name + ".GrossYielding").Resistance);
            Equal(365328, C(r, name + ".NetRupture").Resistance);
            Equal(ntc ? 440360.9321167818 : 452230.97872262093, C(r, name + ".BlockInside").Resistance);
            Equal(ntc ? 440360.9321167818 : 452230.97872262093, C(r, name + ".BlockOutside").Resistance);
        }
        Assert.AreEqual("Gusset.BlockInside", r.GoverningCheck!.Id);
    }

    [DataTestMethod]
    [DataRow(1)] [DataRow(2)] [DataRow(3)] [DataRow(6)]
    public void ActualBoltForcesRecoverTheOneGroupResultant(int rows)
    {
        var r = Verify(Input(rows: rows)); Assert.IsTrue(r.IsComplete);
        double fx = 0, fy = 0, mz = 0;
        for (int i = 1; i <= 2 * rows; i++)
        {
            double x = T(r, $"Bolt{i}.X"), y = T(r, $"Bolt{i}.Y");
            double vx = T(r, $"Bolt{i}.Fx"), vy = T(r, $"Bolt{i}.Fy");
            fx += vx; fy += vy; mz += x * vy - y * vx;
        }
        Equal(100000, fx); Equal(0, fy); Equal(0, mz);
        Equal(2 * rows, T(r, "BoltCount"));
    }

    [DataTestMethod]
    [DataRow(false, false, false)] [DataRow(false, false, true)]
    [DataRow(false, true, false)] [DataRow(false, true, true)]
    [DataRow(true, false, false)] [DataRow(true, false, true)]
    [DataRow(true, true, false)] [DataRow(true, true, true)]
    public void GradeAndThreadConditionUseTwoShearPlanesExactlyOnce(bool ntc, bool highGrade, bool threaded)
    {
        var r = Verify(Input(code: ntc ? ConnectionDesignCode.Ntc2018() : Ec(),
            bolt: new(20, 21, 245, highGrade ? ConnectionBoltGrade.Class10_9 : ConnectionBoltGrade.Class8_8, threaded)));
        Assert.IsTrue(r.IsComplete);
        Equal(2 * (highGrade && threaded ? .5 : .6) * (highGrade ? 1000 : 800)
            * (threaded ? 245 : 100 * Math.PI) / 1.25, C(r, "Bolt1.Shear").Resistance);
    }

    [TestMethod]
    public void LongJointReductionActsOnTheOneGroupLength()
    {
        var r = Verify(Input(rows: 8));
        Equal(420, T(r, "JointLength")); Equal(188160 * .97, C(r, "Bolt1.Shear").Resistance);
    }

    [TestMethod]
    public void PlateAndBoltPartialFactorsRemainIndependent()
    {
        var r = Verify(Input(code: Ec(1.1, 1.2, 1.3)));
        Equal(3200 * 275 / 1.1, C(r, "Gusset.GrossYielding").Resistance);
        Equal(.9 * 2528 * 430 / 1.2, C(r, "Gusset.NetRupture").Resistance);
        Equal(2 * .6 * 800 * 245 / 1.3, C(r, "Bolt1.Shear").Resistance);
        Equal(430 * 944 / 1.2 + 275 * 2192 / (Math.Sqrt(3) * 1.1), C(r, "Gusset.BlockInside").Resistance);
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void ZeroLoadAndOverloadRemainCompleteComponentAssessments(bool overload)
    {
        var r = Verify(Input(n: overload ? 1e6 : 0));
        Assert.IsTrue(r.IsComplete);
        Assert.AreEqual(overload ? ConnectionStatus.Failed : ConnectionStatus.Passed, r.Status);
        Assert.IsTrue(r.Checks.All(c => double.IsFinite(c.Resistance) && c.Resistance > 0));
        if (!overload) Equal(0, r.Utilization!.Value);
    }

    [TestMethod]
    public void NtcWiderNormalHoleNeedsServiceabilityEvidence()
    {
        var b = new ConnectionBolt(20, 22, 245, ConnectionBoltGrade.Class8_8, true);
        Unsupported(Verify(Input(code: ConnectionDesignCode.Ntc2018(), bolt: b)), "Serviceability");
        Assert.IsTrue(Verify(Input(code: ConnectionDesignCode.Ntc2018(), bolt: b, holeSource: "SLE displacement check 3a-01")).IsComplete);
        Assert.IsTrue(Verify(Input(bolt: b)).IsComplete);
    }

    [DataTestMethod]
    [DataRow(0)] [DataRow(1)] [DataRow(2)] [DataRow(3)] [DataRow(4)] [DataRow(5)] [DataRow(6)] [DataRow(7)]
    public void UnqualifiedEccentricAndNonTensileActionsFailClosed(int component)
    {
        PlanarConnectionActions a = component switch
        {
            0 => new PlanarConnectionActions(-1, 0, 0, 0, 0, 0),
            1 => new(100000, 1e-9, 0, 0, 0, 0),
            2 => new(100000, 0, 1e-9, 0, 0, 0),
            3 => new(100000, 0, 0, 1e-9, 0, 0),
            4 => new(100000, 0, 0, 0, 1e-9, 0),
            5 => new(100000, 0, 0, 0, 0, 1e-9),
            6 => new(100000, 0, 0, 0, 0, 0, applicationY: 1),
            _ => new(100000, 0, 0, 0, 0, 0, applicationZ: 1)
        };
        Unsupported(Verify(Input(actions: a)), "ConcentricTensionAtGroupCentroidRequired");
    }

    [DataTestMethod]
    [DataRow(-20.0, 15.0)] [DataRow(20.0, -15.0)]
    public void EquivalentWrenchAtAnotherPoleIsTransportedWithSignedMoments(double y, double z)
    {
        var r = Verify(Input(actions: new(100000, 0, 0, 0, -100000 * z, 100000 * y, 500, y, z)));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status);
        Equal(0, T(r, "MomentAtGroupY")); Equal(0, T(r, "MomentAtGroupZ"));
        Equal(25000, C(r, "Bolt1.Shear").Demand);
    }

    [DataTestMethod]
    [DataRow(0)] [DataRow(1)] [DataRow(2)] [DataRow(3)] [DataRow(4)]
    public void InvalidDetailGeometryReturnsNoPartialPass(int defect)
    {
        GussetTwinFlatTensionInput p = defect switch
        {
            0 => Input(gussetEnd: 20),
            1 => Input(braceEnd: 20),
            2 => Input(gauge: 40),
            3 => Input(pitch: 40),
            _ => Input(brace: new(120, 10, 275, 430))
        };
        Unsupported(Verify(p), defect is 2 or 3 ? "MinimumBoltPitch" : "MinimumEndOrEdgeDistance");
    }

    [TestMethod]
    public void ExposureAndMaximumPitchUseThinnerPly()
    {
        Unsupported(Verify(Input(pitch: 141)), "MaximumBoltPitch");
        Unsupported(Verify(Input(exposed: true, gussetEnd: 81)), "MaximumExposedEdgeDistance");
        Assert.IsTrue(Verify(Input(exposed: false, gussetEnd: 81)).IsComplete);
        Assert.IsTrue(Verify(Input(rows: 1, pitch: 10000)).IsComplete, "inactive longitudinal pitch for one row");
    }

    [TestMethod]
    public void NtcAndEcEdgeBearingBranchesStayDistinct()
    {
        var ec = Verify(Input(gauge: 51));
        var ntc = Verify(Input(code: ConnectionDesignCode.Ntc2018(), gauge: 51));
        Equal(1.4 * 51 / 21 - 1.7, T(ec, "Bolt1.GussetBearing.K1"));
        Equal(2.5, T(ntc, "Bolt1.GussetBearing.K1"));
    }

    [TestMethod]
    public void ScopeDoesNotQualifySupportOrConvertFlatPlatesIntoGeneralProfiles()
    {
        var r = Verify();
        StringAssert.Contains(r.Scope, "Support attachment");
        Assert.IsTrue(r.Assumptions.Any(v => v.Contains("No Whitmore")));
        Assert.IsTrue(r.Assumptions.Any(v => v.Contains("two braces are identical")));
        Assert.IsFalse(r.Checks.Any(v => v.Id.Contains("Support")));
    }

    [TestMethod]
    public void NumericOverflowAndUnsupportedSteelFailClosed()
    {
        Unsupported(Verify(Input(actions: new(1e308, 0, 0, 0, 0, 0, applicationY: 2))), "ActionTransportOutsideNumericRange");
        Unsupported(Verify(Input(gusset: new(200, 16, 500, 600))), "CarbonSteelUpToS460Only");
    }

    [DataTestMethod]
    [DataRow(0)] [DataRow(1001)]
    public void InvalidRowCountIsRejectedAtConstruction(int rows)
        => Assert.ThrowsException<ArgumentOutOfRangeException>(() => Input(rows: rows));
}
