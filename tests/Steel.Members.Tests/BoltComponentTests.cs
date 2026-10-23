using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class BoltComponentTests
{
    private static ConnectionDesignCode Code(bool ntc = false) => ntc ? ConnectionDesignCode.Ntc2018() : ConnectionDesignCode.Eurocode3(1, 1.25, 1.25, "EN recommended factors; component tests");
    private static ConnectionBolt Bolt(ConnectionBoltGrade grade = ConnectionBoltGrade.Class8_8, bool threaded = true, double hole = 21) => new(20, hole, 245, grade, threaded);
    private static ConnectionPlate Plate(double thickness = 10, double fy = 275, double fu = 410, double width = 150) => new(width, thickness, fy, fu);
    private static double Resistance<T>(ConnectionVerification<T> r, string id) where T : class => r.Checks.Single(c => c.Id == id).Resistance;
    private static double Trace<T>(ConnectionVerification<T> r, string name) where T : class => r.Trace.Single(c => c.Name == name).Value;
    private static void Unsupported<T>(ConnectionVerification<T> r, string diagnostic) where T : class
    { Assert.AreEqual(ConnectionStatus.NotSupported, r.Status); Assert.AreEqual(0, r.Checks.Count); Assert.IsNull(r.Utilization); Assert.IsTrue(r.Diagnostics.Any(d => d.Contains(diagnostic)), string.Join("; ", r.Diagnostics)); }

    [DataTestMethod]
    [DataRow(false, 0, true, 1, 94080.0, 141120.0)] [DataRow(true, 0, true, 1, 94080.0, 141120.0)]
    [DataRow(false, 1, true, 1, 98000.0, 176400.0)] [DataRow(true, 1, true, 1, 98000.0, 176400.0)]
    [DataRow(false, 0, true, 2, 188160.0, 141120.0)] [DataRow(true, 1, true, 2, 196000.0, 176400.0)]
    public void ThreadedBoltStrengthMatchesIndependentHandTable(bool ntc, int grade, bool threaded, int planes, double shear, double tension)
    {
        // M20 As=245 mm2: 8.8 gives 0.6*800*245/1.25=94080 N per plane;
        // 10.9 gives 0.5*1000*245/1.25=98000 N. Tension is independent of shear-plane count.
        var r = BoltStrength.Verify(new(Code(ntc), Bolt((ConnectionBoltGrade)grade, threaded), 1000, 2000, planes));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status); Assert.AreEqual(shear, Resistance(r, "Bolt.Shear"), 1e-8);
        Assert.AreEqual(tension, Resistance(r, "Bolt.Tension"), 1e-8);
    }

    [DataTestMethod]
    [DataRow(false, 0, 1, 38400.0)] [DataRow(true, 0, 2, 76800.0)]
    [DataRow(false, 1, 1, 48000.0)] [DataRow(true, 1, 2, 96000.0)]
    public void UnthreadedPlanesUseGrossAreaAndAlphaPointSixForBothGrades(bool ntc, int grade, int planes, double factorOfPi)
    {
        // M20 gross area=100*pi mm2; class 10.9 also uses alpha=0.6 in an unthreaded plane.
        var r = BoltStrength.Verify(new(Code(ntc), Bolt((ConnectionBoltGrade)grade, false), 0, 0, planes));
        Assert.AreEqual(factorOfPi * Math.PI, Resistance(r, "Bolt.Shear"), 1e-7);
        Assert.AreEqual(.6, Trace(r, "AlphaV")); Assert.AreEqual(100 * Math.PI, Trace(r, "ShearArea"), 1e-12);
    }

    [TestMethod]
    public void PureTensionLimitRemainsSeparateFromInteraction()
    {
        var r = BoltStrength.Verify(new(Code(), Bolt(), 0, 1.1 * 141120));
        Assert.AreEqual(ConnectionStatus.Failed, r.Status);
        Assert.IsTrue(r.Checks.Single(c => c.Id == "Bolt.ShearTensionInteraction").Passed);
        Assert.IsFalse(r.Checks.Single(c => c.Id == "Bolt.Tension").Passed);
    }

    [DataTestMethod]
    [DataRow(0.0, 0.0, true)] [DataRow(.5, .7, true)] [DataRow(.6, .7, false)] [DataRow(1.01, 0.0, false)]
    public void InteractionChecksConcomitantActions(double shearRatio, double tensionRatio, bool passed)
    {
        var r = BoltStrength.Verify(new(Code(), Bolt(), shearRatio * 94080, tensionRatio * 141120));
        Assert.AreEqual(passed ? ConnectionStatus.Passed : ConnectionStatus.Failed, r.Status);
        Assert.AreEqual(shearRatio + tensionRatio / 1.4, r.Checks.Single(c => c.Id == "Bolt.ShearTensionInteraction").Demand, 1e-12);
    }

    [DataTestMethod]
    [DataRow(0.0, 1.0)] [DataRow(300.0, 1.0)] [DataRow(301.0, .99975)]
    [DataRow(700.0, .9)] [DataRow(1300.0, .75)] [DataRow(2000.0, .75)]
    public void LongJointReductionStartsAfter15dAndStopsAtPoint75(double length, double beta)
    {
        var r = BoltStrength.Verify(new(Code(), Bolt(), 1000, 1000, jointLength: length));
        Assert.AreEqual(beta, Trace(r, "BetaLongJoint"), 1e-12);
        Assert.AreEqual(94080 * beta, Resistance(r, "Bolt.Shear"), 1e-8);
        Assert.AreEqual(141120, Resistance(r, "Bolt.Tension"), 1e-8);
    }

    [DataTestMethod]
    [DataRow(0.0, 1.0)] [DataRow(6.0, 1.0)] [DataRow(6.666666666666667, 1.0)]
    [DataRow(10.0, .9473684210526316)] [DataRow(20.0, .8181818181818182)]
    public void PackingReductionAppliesOnlyBeyondOneThirdDiameter(double packing, double beta)
    {
        var r = BoltStrength.Verify(new(Code(), Bolt(), 1000, 1000, packingThickness: packing));
        Assert.AreEqual(beta, Trace(r, "BetaPacking"), 1e-12);
        Assert.AreEqual(94080 * beta, Resistance(r, "Bolt.Shear"), 1e-8);
        Assert.AreEqual(141120, Resistance(r, "Bolt.Tension"), 1e-8);
    }

    [DataTestMethod]
    [DataRow(false, 1)] [DataRow(true, 1)] [DataRow(false, 2)] [DataRow(true, 2)]
    public void PackingAndLongJointReductionsActOnShearIncludingInteraction(bool ntc, int planes)
    {
        // Lj=500 => betaLf=19/20; tp=10 => betaP=18/19; product=9/10.
        var r = BoltStrength.Verify(new(Code(ntc), Bolt(), 84672 * planes, 0, planes, 500, 10));
        Assert.AreEqual(84672 * planes, Resistance(r, "Bolt.Shear"), 1e-8);
        Assert.AreEqual(1, r.Checks.Single(c => c.Id == "Bolt.ShearTensionInteraction").Demand, 1e-12);
        Assert.AreEqual(141120, Resistance(r, "Bolt.Tension"), 1e-8);
    }

    [DataTestMethod]
    [DataRow(false, 1.66, 72597.33333333333)] [DataRow(true, 2.5, 109333.33333333333)]
    public void EdgeBoltTransversePitchDistinguishesEcFromNtc(bool ntc, double k, double resistance)
    {
        // e1=2d0 => alpha=2/3; e2=2d0; p2=2.4d0. EC k=min(3.9,1.66,2.5), NTC=min(3.9,2.5).
        var r = BoltBearing.Verify(new(Code(ntc), Bolt(), Plate(), 50000, 42, 42, 70, 50.4, true, true));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status); Assert.AreEqual(k, Trace(r, "K1"), 1e-12);
        Assert.AreEqual(resistance, r.Checks.Single().Resistance, 1e-7);
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void InnerBoltsUseBothPitchExpressions(bool ntc)
    {
        // p1=2.5*d0 gives alpha=7/12; p2=3*d0 gives k=2.5.
        var r = BoltBearing.Verify(new(Code(ntc), Bolt(), Plate(), 1000, 42, 42, 52.5, 63, false, false));
        Assert.AreEqual(7.0 / 12, Trace(r, "AlphaB"), 1e-12);
        Assert.AreEqual(95666.66666666667, r.Checks.Single().Resistance, 1e-7);
    }

    [TestMethod]
    public void IsolatedEdgeBoltDoesNotInventNeighbouringPitch()
    {
        var r = BoltBearing.Verify(new(Code(), Bolt(), Plate(), 0, 42, 42, null, null, true, true));
        Assert.AreEqual(2.5, Trace(r, "K1")); Assert.AreEqual(109333.33333333333, r.Checks.Single().Resistance, 1e-7);
    }

    [TestMethod]
    public void MaterialStrengthRatioCapsBearingAlpha()
    {
        // Synthetic coefficient-limit case: fub/fu=800/1000=.8; geometry would otherwise give alpha>=1.
        var r = BoltBearing.Verify(new(Code(), Bolt(), Plate(fy: 355, fu: 1000), 0, 100, 100, 100, 100, true, true));
        Assert.AreEqual(.8, Trace(r, "AlphaB")); Assert.AreEqual(320000, r.Checks.Single().Resistance, 1e-8);
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void SingleLapFigure33CapsBearingAndRequiresHardenedWashers(bool ntc)
    {
        var code = Code(ntc); var bolt = Bolt(); var plate = Plate();
        var r = BoltBearing.Verify(new(code, bolt, plate, 90000, 100, 100, null, null, true, true,
            singleLapWithSingleBoltRow: true, hardenedWashersUnderHeadAndNut: true));
        Assert.AreEqual(98400, r.Checks.Single().Resistance, 1e-8); // 1.5*410*20*10/1.25
        Unsupported(BoltBearing.Verify(new(code, bolt, plate, 0, 100, 100, null, null, true, true,
            singleLapWithSingleBoltRow: true)), "RequiresHardenedWashers");
    }

    [DataTestMethod]
    [DataRow(false)] [DataRow(true)]
    public void PunchingMatchesCircularPerimeterHandCalculation(bool ntc)
    {
        var r = BoltPunching.Verify(new(Code(ntc), Plate(), 100000, 21, 30));
        Assert.AreEqual(59040 * Math.PI, r.Checks.Single().Resistance, 1e-7);
        Assert.AreEqual(ConnectionStatus.Passed, r.Status);
    }

    [TestMethod]
    public void NtcCombinedAssemblyUsesPlateMinimaAndPreventsBoltOnlyFalsePositive()
    {
        var input = Assembly(true, 18000, 15000);
        Assert.AreEqual(ConnectionStatus.Passed, BoltStrength.Verify(input.Bolt).Status);
        Assert.AreEqual(ConnectionStatus.Passed, BoltBearing.Verify(input.HeadPlyBearing).Status);
        Assert.AreEqual(ConnectionStatus.Passed, BoltPunching.Verify(input.HeadPlyPunching).Status);
        var r = BoltSingleShearAssembly.Verify(input);
        Assert.AreEqual(ConnectionStatus.Failed, r.Status);
        Assert.AreEqual(21866.66666666667, Trace(r, "AssembledShearResistance"), 1e-7);
        Assert.AreEqual(11808 * Math.PI, Trace(r, "AssembledTensionResistance"), 1e-7);
        double expected = 18000 / (65600.0 / 3) + 15000 / (1.4 * 11808 * Math.PI);
        Assert.AreEqual(expected, r.Checks.Single(c => c.Id == "Assembly.NtcShearTensionInteraction").Demand, 1e-12);
        Assert.IsTrue(expected > 1);
    }

    [TestMethod]
    public void EcKeepsBoltInteractionAndSeparatePlyChecksWithoutImportingNtcRule()
    {
        var r = BoltSingleShearAssembly.Verify(Assembly(false, 18000, 15000));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status);
        Assert.IsFalse(r.Checks.Any(c => c.Id.StartsWith("Assembly.Ntc")));
        Assert.AreEqual(7, r.Checks.Count);
    }

    [TestMethod]
    public void NtcAssemblyAlsoEnforcesSeparateTensionLimitation()
    {
        var r = BoltSingleShearAssembly.Verify(Assembly(true, 0, 1.2 * 11808 * Math.PI));
        Assert.AreEqual(ConnectionStatus.Failed, r.Status);
        Assert.IsTrue(r.Checks.Single(c => c.Id == "Assembly.NtcShearTensionInteraction").Passed);
        Assert.IsFalse(r.Checks.Single(c => c.Id == "Assembly.NtcTensionLimit").Passed);
    }

    [TestMethod]
    public void UnsupportedPlyClearsAllAssemblyChecksAndUtilization()
    {
        var p = Assembly(true, 1000, 1000, headEnd: 20);
        Unsupported(BoltSingleShearAssembly.Verify(p), "HeadPly:MinimumEndOrEdgeDistance");
    }

    [TestMethod]
    public void AssemblyCannotMistakeDoubleShearTotalForOnePlyForce()
    { Unsupported(BoltSingleShearAssembly.Verify(Assembly(true, 1000, 1000, planes: 2)), "AssemblyRequiresSingleShearPlane"); }

    [TestMethod]
    public void AssemblyRejectsDifferentPlyIdentitiesOrNonconcomitantDemands()
    {
        var p = Assembly(true, 1000, 1000);
        var wrongPlate = new BoltPunchingInput(p.Bolt.Code, Plate(2), 1000, 21, 30);
        Assert.ThrowsException<ArgumentException>(() => new BoltSingleShearAssemblyInput(p.Bolt, p.HeadPlyBearing, p.NutPlyBearing, wrongPlate, p.NutPlyPunching));
        var wrongForce = new BoltPunchingInput(p.Bolt.Code, p.HeadPlyBearing.Plate, 2000, 21, 30);
        Assert.ThrowsException<ArgumentException>(() => new BoltSingleShearAssemblyInput(p.Bolt, p.HeadPlyBearing, p.NutPlyBearing, wrongForce, p.NutPlyPunching));
    }

    [TestMethod]
    public void SciP358Example1Pages140And141PublishedComponentValuesAreReproduced()
    {
        // SCI P358 (2014), Example1, printed140-141/PDF150-151. Published values use rounded alpha/k.
        // Tolerances 0.1 kN shear and 1 kN bearing accommodate publication rounding, not algorithm drift.
        var code = Code(); var bolt = Bolt(hole: 22); var plate = Plate();
        var shear = BoltStrength.Verify(new(code, bolt, 0, 0));
        Assert.AreEqual(94000, Resistance(shear, "Bolt.Shear"), 100);
        var vertical = BoltBearing.Verify(new(code, bolt, plate, 0, 40, 50, 70, null, true, true));
        var horizontal = BoltBearing.Verify(new(code, bolt, plate, 0, 50, 40, null, 70, true, true));
        Assert.AreEqual(100000, vertical.Checks.Single().Resistance, 1000);
        Assert.AreEqual(125000, horizontal.Checks.Single().Resistance, 1000);
        Assert.AreEqual(40.0 / 66, Trace(vertical, "AlphaB"), 1e-12);
    }

    [TestMethod]
    public void NtcNormalClearanceRequiresExplicitSlsEvidenceWhenExceeded()
    {
        var code = Code(true); var bolt = Bolt(hole: 22);
        Unsupported(BoltStrength.Verify(new(code, bolt, 0, 0)), "NtcHoleClearanceRequiresServiceabilityEvidence");
        Unsupported(BoltBearing.Verify(new(code, bolt, Plate(), 0, 50, 50, 70, 70, true, true)), "NtcHoleClearanceRequiresServiceabilityEvidence");
        Assert.AreEqual(ConnectionStatus.Passed, BoltStrength.Verify(new(code, bolt, 0, 0, holeClearanceServiceabilitySource: "Project SLS assessment S1")).Status);
        Assert.AreEqual(ConnectionStatus.Passed, BoltBearing.Verify(new(code, bolt, Plate(), 0, 50, 50, 70, 70, true, true, "Project SLS assessment S1")).Status);
    }

    [DataTestMethod]
    [DataRow(12.0, 14.0, 84.3)] [DataRow(14.0, 16.0, 115.0)] [DataRow(20.0, 23.0, 245.0)]
    public void UnqualifiedHoleClearanceHasNoPartialRatio(double diameter, double hole, double area)
    {
        var bolt = new ConnectionBolt(diameter, hole, area, ConnectionBoltGrade.Class8_8, true);
        Unsupported(BoltStrength.Verify(new(Code(), bolt, 0, 0)), "RoundHoleClearanceOutsideQualifiedRange");
    }

    [DataTestMethod]
    [DataRow(24.0, 42.0, 70.0, 70.0)] [DataRow(42.0, 24.0, 70.0, 70.0)]
    [DataRow(42.0, 42.0, 40.0, 70.0)] [DataRow(42.0, 42.0, 70.0, 45.0)]
    public void InsufficientEndEdgeOrPitchIsNotSupported(double end, double edge, double p1, double p2)
    {
        var r = BoltBearing.Verify(new(Code(), Bolt(), Plate(), 0, end, edge, p1, p2, true, true));
        Unsupported(r, "Minimum");
    }

    [TestMethod]
    public void MaterialAndPunchingPerimeterLimitsAreExplicit()
    {
        Unsupported(BoltBearing.Verify(new(Code(), Bolt(), Plate(fy: 500, fu: 600), 0, 50, 50, 70, 70, true, true)), "CarbonSteelUpToS460Only");
        Unsupported(BoltPunching.Verify(new(Code(), Plate(width: 25), 0, 21, 30)), "PunchingDiameterExceedsPlateWidth");
    }

    [TestMethod]
    public void InvalidInputsAreRejectedRatherThanNormalized()
    {
        var code = Code(); var bolt = Bolt(); var plate = Plate();
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new BoltStrengthInput(code, bolt, -1, 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new BoltStrengthInput(code, bolt, 0, -1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new BoltStrengthInput(code, bolt, 0, 0, 3));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new BoltStrengthInput(code, bolt, 0, 0, jointLength: -1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new BoltStrengthInput(code, bolt, 0, 0, packingThickness: double.NaN));
        Assert.ThrowsException<ArgumentException>(() => new BoltBearingInput(code, bolt, plate, 0, 50, 50, null, 70, false, true));
        Assert.ThrowsException<ArgumentException>(() => new BoltBearingInput(code, bolt, plate, 0, 50, 50, 70, null, true, false));
        Assert.ThrowsException<ArgumentException>(() => new BoltPunchingInput(code, plate, 0, 21, 21));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new BoltPunchingInput(code, plate, double.PositiveInfinity, 21, 30));
    }

    private static BoltSingleShearAssemblyInput Assembly(bool ntc, double shear, double tension, int planes = 1, double headEnd = 42)
    {
        var code = Code(ntc); var bolt = Bolt(); var head = Plate(2); var nut = Plate(10);
        return new(new(code, bolt, shear, tension, planes),
            new(code, bolt, head, shear, headEnd, 42, 70, 63, true, true),
            new(code, bolt, nut, shear, 42, 42, 70, 63, true, true),
            new(code, head, tension, 21, 30), new(code, nut, tension, 21, 30));
    }
}
