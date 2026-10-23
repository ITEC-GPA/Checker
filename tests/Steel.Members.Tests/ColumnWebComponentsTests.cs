using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class ColumnWebComponentsTests
{
    private static ColumnWebPolicy Policy(bool ntc = false, double gamma0 = 1, double gamma1 = 1)
        => ntc ? ColumnWebPolicy.Ntc2018(ConnectionDesignCode.Ntc2018())
        : ColumnWebPolicy.Eurocode3(ConnectionDesignCode.Eurocode3(gamma0, 1.1, 1.25, "Explicit component benchmark factors"), gamma1);
    private static ConnectionRolledISection SciColumn() => new(266.7, 258.8, 12.8, 20.5, 12.7, 13600, 265, 410);
    private static ConnectionRolledISection Plain(double h = 300, double tw = 10, double fy = 275)
        => new(h, 200, tw, 20, 0, 8000 + (h - 40) * tw, fy, Math.Max(fy, 410));
    private static double Trace<T>(ConnectionVerification<T> r, string name) where T : class => r.Trace.Single(v => v.Name == name).Value;
    private static double Capacity<T>(ConnectionVerification<T> r, string name) where T : class => r.Checks.Single(c => c.Id == name).Resistance;
    private static ColumnWebCompressionInput Compression(double force = 100000, double beta = 0, double sigma = 0,
        ColumnWebPolicy? policy = null, ConnectionRolledISection? column = null, double width = 280,
        string widthSource = "Independent EN6.2.6.2 load-spread derivation", string betaSource = "EN5.3 equilibrium B1",
        string stressSource = "Column N/M elastic root-stress calculation C1", string restraintSource = "Column flanges restrained against Figure6.7 sway, detail R1",
        string actionSource = "Concomitant ULS state1")
        => new(policy ?? Policy(), column ?? Plain(), new(width, widthSource), force, beta, sigma,
            betaSource, stressSource, restraintSource, actionSource);
    private static ColumnWebTensionInput Tension(double force = 100000, double beta = 0, ColumnWebPolicy? policy = null,
        ConnectionRolledISection? column = null, double width = 280, string source = "EN6.2.6.4 governing equivalent T-stub width")
        => new(policy ?? Policy(), column ?? Plain(), new(width, source), force, beta, "EN5.3 equilibrium B1", "Concomitant ULS state1");
    private static void Unsupported<T>(ConnectionVerification<T> r, string diagnostic) where T : class
    {
        Assert.AreEqual(ConnectionStatus.NotSupported, r.Status); Assert.AreEqual(0, r.Checks.Count); Assert.IsNull(r.Utilization);
        Assert.IsTrue(r.Diagnostics.Any(d => d.Contains(diagnostic)), string.Join("; ", r.Diagnostics));
        Assert.IsTrue(r.Trace.All(v => double.IsFinite(v.Value)));
    }

    [TestMethod]
    public void SciP398C1TransverseCompressionReproducesPublishedCapacityWithoutChangingInputs()
    {
        // P398 C.1 p102, printed beff248,tw12.8,fy265,dc200.3,beta0,kw1.
        var r = ColumnWebComponents.VerifyTransverseCompression(Compression(column: SciColumn(), width: 248));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status);
        Assert.AreEqual(841216, Capacity(r, "ColumnWeb.CompressionYielding"), 1e-6);
        Assert.AreEqual(841216, Capacity(r, "ColumnWeb.CompressionBuckling"), 1e-6);
        Assert.AreEqual(841000, r.GoverningCheck.Resistance, 500); // published rounded841kN
        Assert.AreEqual(.5764817290516947, Trace(r, "PlateSlenderness"), 1e-12);
        Assert.AreEqual(1, Trace(r, "Rho")); Assert.AreEqual(1, Trace(r, "Kw"));
    }

    [TestMethod]
    public void SciP398C1TensionUsesTheDistinctGoverningTStubWidth()
    {
        // SCI P398 C.1 p88 explicitly chooses233mm from governingMode2, not rounded0.233m.
        var r = ColumnWebComponents.VerifyTransverseTension(Tension(column: SciColumn(), width: 233));
        Assert.AreEqual(790336, r.Checks.Single().Resistance, 1e-6);
        Assert.AreEqual(790000, r.Checks.Single().Resistance, 500);
        Assert.AreEqual(3772.3, Trace(r, "ColumnShearArea"), 1e-9);
        Assert.AreEqual(200.3, Trace(r, "ColumnClearWeb"), 1e-10);
    }

    [TestMethod]
    public void SciC1EndPlateDispersionKeepsExactGeometrySeparateFromRoundedBookWidth()
    {
        var column = SciColumn();
        // Plate depth670,top edge50,row-to-top-flange40,beam depth533.1=>46.9mm below flange.
        var width = ColumnWebEffectiveWidth.FromBoltedEndPlate(column, 15.6, 8, 25, 46.9, "SCI P398 C1 pp101-102");
        Assert.AreEqual(247.6, width.Width, 1e-10); // book rounds248
        var r = ColumnWebComponents.VerifyTransverseCompression(new(Policy(), column, width, 100000, 0, 0,
            "Equal opposite moments per C1", "Explicit sigma=0 benchmark assumption", "R1 restraint", "ULS1"));
        Assert.AreEqual(839859.2, Capacity(r, "ColumnWeb.CompressionYielding"), 1e-6);
        Assert.AreEqual(50, Trace(r, "EndPlate.Sp"));
        Assert.AreEqual(46.9, Trace(r, "EndPlate.ProjectionBeyondFlange"));
        Assert.IsTrue(width.Source.Contains("6.11"));
    }

    [DataTestMethod]
    [DataRow(0.0, 1.0, 770000.0, 660189.3427693624)]
    [DataRow(.5, 1.0, 770000.0, 660189.3427693624)]
    [DataRow(.75, .8296902366978935, 638861.482257378, 547752.6520677391)]
    [DataRow(1.0, .659380473395787, 507722.964514756, 435315.9613661157)]
    [DataRow(1.5, .5304950689235182, 408481.203071109, 350227.1908950051)]
    [DataRow(2.0, .40160966445124943, 309239.441627462, 265138.4204238945)]
    public void FullBetaRangeMatchesIndependentTable63Calculation(double beta, double omega, double tension, double compression)
    {
        // Plain I300x200,tw10,tf20,r0,A10600 => Av2800; beff280 => beff*tw/Av=1.
        var t = ColumnWebComponents.VerifyTransverseTension(Tension(beta: beta));
        var c = ColumnWebComponents.VerifyTransverseCompression(Compression(beta: beta));
        Assert.AreEqual(omega, Trace(t, "Omega"), 1e-13); Assert.AreEqual(omega, Trace(c, "Omega"), 1e-13);
        Assert.AreEqual(tension, t.Checks.Single().Resistance, 1e-6);
        Assert.AreEqual(compression, Capacity(c, "ColumnWeb.CompressionBuckling"), 1e-6);
        Assert.AreEqual(.9099935237864791, Trace(c, "PlateSlenderness"), 1e-12);
        Assert.AreEqual(.8573887568433278, Trace(c, "Rho"), 1e-12);
    }

    [DataTestMethod]
    [DataRow(0.0, 1.0)] [DataRow(192.5, 1.0)] [DataRow(220.0, .9)] [DataRow(275.0, .7)]
    public void CoexistingLongitudinalCompressionReducesBothTransverseLimits(double sigma, double kw)
    {
        var r = ColumnWebComponents.VerifyTransverseCompression(Compression(sigma: sigma));
        Assert.AreEqual(kw, Trace(r, "Kw"), 1e-12);
        Assert.AreEqual(770000 * kw, Capacity(r, "ColumnWeb.CompressionYielding"), 1e-7);
        Assert.AreEqual(660189.3427693624 * kw, Capacity(r, "ColumnWeb.CompressionBuckling"), 1e-6);
    }

    [TestMethod]
    public void GammaM0AndGammaM1ApplyToDifferentFailureLimits()
    {
        var r = ColumnWebComponents.VerifyTransverseCompression(Compression(policy: Policy(gamma0: 1.05, gamma1: 1.2)));
        Assert.AreEqual(770000 / 1.05, Capacity(r, "ColumnWeb.CompressionYielding"), 1e-6);
        Assert.AreEqual(660189.3427693624 / 1.2, Capacity(r, "ColumnWeb.CompressionBuckling"), 1e-6);
        Assert.AreEqual("ColumnWeb.CompressionBuckling", r.GoverningCheck.Id);
    }

    [TestMethod]
    public void NationalFactorsDoNotRelabelTheEcComponentModelAsALiteralNtcFormula()
    {
        var p = Policy(ntc: true);
        var c = ColumnWebComponents.VerifyTransverseCompression(Compression(policy: p));
        var t = ColumnWebComponents.VerifyTransverseTension(Tension(policy: p));
        var v = ColumnWebComponents.VerifyPanelShear(new(p, Plain(), 10000, ColumnWebPanelConfiguration.SingleSidedJoint, "ULS equilibrium"));
        Assert.AreEqual(1.05, p.GammaM1); Assert.AreEqual(1.05, Trace(c, "GammaM0"));
        Assert.AreEqual(770000 / 1.05, t.Checks.Single().Resistance, 1e-6);
        Assert.AreEqual(400103.73654841065 / 1.05, v.Checks.Single().Resistance, 1e-6);
        Assert.IsTrue(c.Checks.Concat(t.Checks).Concat(v.Checks).All(x => x.Reference.Contains("supplementary component model")));
    }

    [DataTestMethod]
    [DataRow(0)] [DataRow(1)]
    public void UnstiffenedPanelMatchesIndependentPointNinePlasticShear(int configuration)
    {
        var r = ColumnWebComponents.VerifyPanelShear(new(Policy(), Plain(), 300000, (ColumnWebPanelConfiguration)configuration, "Panel equilibrium P1"));
        Assert.AreEqual(ConnectionStatus.Passed, r.Status);
        Assert.AreEqual(400103.73654841065, r.Checks.Single().Resistance, 1e-6);
        Assert.AreEqual(2800, Trace(r, "ColumnShearArea"), 1e-9);
        Assert.AreEqual(260, Trace(r, "ColumnClearWeb"));
    }

    [DataTestMethod]
    [DataRow(0.0, true)] [DataRow(399000.0, true)] [DataRow(401000.0, false)]
    public void PanelDemandIsEvaluatedWithoutFavourableRescaling(double force, bool pass)
    {
        var r = ColumnWebComponents.VerifyPanelShear(new(Policy(), Plain(), force, ColumnWebPanelConfiguration.SingleSidedJoint, "Panel equilibrium"));
        Assert.AreEqual(pass ? ConnectionStatus.Passed : ConnectionStatus.Failed, r.Status);
    }

    [TestMethod]
    public void UnequalBeamDepthsDoNotUseTheSimilarDepthPanelFormula()
        => Unsupported(ColumnWebComponents.VerifyPanelShear(new(Policy(), Plain(), 0,
            ColumnWebPanelConfiguration.DoubleSidedUnequalBeamDepths, "Unequal beams")), "UnequalBeamDepths");

    [TestMethod]
    public void PanelSlendernessBoundaryAndTheNextValueAreDistinguished()
    {
        var exact = ColumnWebComponents.VerifyPanelShear(new(Policy(), Plain(h: 730, fy: 235), 0,
            ColumnWebPanelConfiguration.SingleSidedJoint, "Geometry"));
        Assert.AreEqual(ConnectionStatus.Passed, exact.Status); Assert.AreEqual(69, Trace(exact, "ColumnWebSlenderness"));
        Unsupported(ColumnWebComponents.VerifyPanelShear(new(Policy(), Plain(h: 730.1, fy: 235), 0,
            ColumnWebPanelConfiguration.SingleSidedJoint, "Geometry")), "SlendernessAbove69Epsilon");
    }

    [TestMethod]
    public void TransverseComponentWithZeroBetaDoesNotClaimShearPanelValidity()
    {
        var c = Plain(h: 730.1, fy: 235);
        Assert.AreEqual(ConnectionStatus.Passed, ColumnWebComponents.VerifyTransverseTension(Tension(column: c, force: 0)).Status);
        Assert.AreEqual(ConnectionStatus.Passed, ColumnWebComponents.VerifyTransverseCompression(Compression(column: c, force: 0)).Status);
        Unsupported(ColumnWebComponents.VerifyTransverseTension(Tension(column: c, force: 0, beta: .1)), "SlendernessAbove69Epsilon");
        Unsupported(ColumnWebComponents.VerifyTransverseCompression(Compression(column: c, force: 0, beta: .1)), "SlendernessAbove69Epsilon");
    }

    [DataTestMethod]
    [DataRow(.719, 1.0)] [DataRow(.72, 1.0)] [DataRow(.721, 1.0)] [DataRow(1.0, .8)] [DataRow(2.0, .45)]
    public void CompressionReductionCoversItsThresholdAndSlenderPlateBranch(double targetLambda, double rho)
    {
        // Invert EN6.13c independently to prescribe lambda; checks the piecewise transition and cap<=1.
        double width = targetLambda * targetLambda * 210000 * 100 / (.932 * .932 * 260 * 275);
        var r = ColumnWebComponents.VerifyTransverseCompression(Compression(width: width));
        Assert.AreEqual(targetLambda, Trace(r, "PlateSlenderness"), 1e-12);
        Assert.AreEqual(rho, Trace(r, "Rho"), 1e-12);
        Assert.IsTrue(Capacity(r, "ColumnWeb.CompressionBuckling") <= Capacity(r, "ColumnWeb.CompressionYielding") * (1 + 1e-12));
    }

    [TestMethod]
    public void FailureOfLocalBucklingIsNotHiddenByGrossYieldPass()
    {
        var r = ColumnWebComponents.VerifyTransverseCompression(Compression(force: 700000));
        Assert.AreEqual(ConnectionStatus.Failed, r.Status);
        Assert.IsTrue(r.Checks.Single(c => c.Id == "ColumnWeb.CompressionYielding").Passed);
        Assert.IsFalse(r.Checks.Single(c => c.Id == "ColumnWeb.CompressionBuckling").Passed);
    }

    [DataTestMethod]
    [DataRow(8.0, 222.6)] [DataRow(18.0, 232.6)] [DataRow(33.0, 247.6)] [DataRow(50.0, 247.6)]
    public void EndPlateSpreadUsesActualProjectionAndCannotExceedTwoPlateThicknesses(double projection, double width)
    {
        var w = ColumnWebEffectiveWidth.FromBoltedEndPlate(SciColumn(), 15.6, 8, 25, projection, "Drawing D1");
        Assert.AreEqual(width, w.Width, 1e-10);
    }

    [TestMethod]
    public void EndPlateCannotEndInsideItsCompressionWeld()
        => Assert.ThrowsException<ArgumentException>(() => ColumnWebEffectiveWidth.FromBoltedEndPlate(SciColumn(), 15.6, 8, 25, 7, "Drawing"));

    [TestMethod]
    public void DerivedCompressionWidthCannotBeReusedForAnotherColumnOrATensionMechanism()
    {
        var column = SciColumn();
        var width = ColumnWebEffectiveWidth.FromBoltedEndPlate(column, 15.6, 8, 25, 46.9, "C1");
        Unsupported(ColumnWebComponents.VerifyTransverseCompression(new(Policy(), Plain(), width, 0, 0, 0,
            "beta", "stress", "restraint", "ULS")), "EffectiveWidthColumnSnapshotMismatch");
        Unsupported(ColumnWebComponents.VerifyTransverseTension(new(Policy(), column, width, 0, 0,
            "beta", "ULS")), "CompressionSpreadCannotRepresentTensionTStub");
    }

    [TestMethod]
    public void CompressionRequiresAllEvidenceEvenAtZeroLoad()
    {
        Unsupported(ColumnWebComponents.VerifyTransverseCompression(Compression(force: 0, widthSource: " ")), "EffectiveWidthDerivationRequired");
        Unsupported(ColumnWebComponents.VerifyTransverseCompression(Compression(force: 0, betaSource: " ")), "TransformationDerivationRequired");
        Unsupported(ColumnWebComponents.VerifyTransverseCompression(Compression(force: 0, stressSource: " ")), "LongitudinalStressDerivationRequired");
        Unsupported(ColumnWebComponents.VerifyTransverseCompression(Compression(force: 0, restraintSource: " ")), "ColumnSwayRestraintRequired");
        Unsupported(ColumnWebComponents.VerifyTransverseCompression(Compression(force: 0, actionSource: " ")), "ConcomitantActionSourceRequired");
    }

    [TestMethod]
    public void PanelAndTensionRequireExplicitEquilibriumAndEffectiveWidth()
    {
        Unsupported(ColumnWebComponents.VerifyPanelShear(new(Policy(), Plain(), 0, ColumnWebPanelConfiguration.SingleSidedJoint, " ")), "PanelEquilibriumSourceRequired");
        Unsupported(ColumnWebComponents.VerifyTransverseTension(Tension(force: 0, source: " ")), "EffectiveWidthDerivationRequired");
        Unsupported(ColumnWebComponents.VerifyTransverseTension(new(Policy(), Plain(), new(280, "Geometry"), 0, 0, " ", "ULS")), "TransformationDerivationRequired");
        Unsupported(ColumnWebComponents.VerifyTransverseTension(new(Policy(), Plain(), new(280, "Geometry"), 0, 0, "Beta", " ")), "ConcomitantActionSourceRequired");
    }

    [TestMethod]
    public void LongitudinalStressAboveYieldIsExplicitlyOutsideTheElasticScope()
        => Unsupported(ColumnWebComponents.VerifyTransverseCompression(Compression(sigma: 275.01)), "LongitudinalStressAboveYield");

    [TestMethod]
    public void HighStrengthMaterialCannotBypassTheDeclaredScope()
        => Unsupported(ColumnWebComponents.VerifyTransverseTension(Tension(column: Plain(fy: 500))), "CarbonSteelUpToS460Only");

    [TestMethod]
    public void OverflowDoesNotExposePartialPassingChecks()
    {
        Unsupported(ColumnWebComponents.VerifyTransverseTension(Tension(width: 1e308)), "OutsideNumericRange");
        Unsupported(ColumnWebComponents.VerifyTransverseCompression(Compression(width: 1e308)), "OutsideNumericRange");
    }

    [DataTestMethod]
    [DataRow(-.1)] [DataRow(2.1)] [DataRow(double.NaN)] [DataRow(double.PositiveInfinity)]
    public void InvalidBetaCannotBeClippedToAFavourableValue(double beta)
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Tension(beta: beta));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Compression(beta: beta));
    }

    [TestMethod]
    public void NegativeForceOrStressAndNullInputsAreRejected()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Tension(force: -1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Compression(sigma: -1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ColumnWebEffectiveWidth(0, "source"));
        Assert.ThrowsException<ArgumentNullException>(() => ColumnWebComponents.VerifyPanelShear(null!));
        Assert.ThrowsException<ArgumentNullException>(() => ColumnWebComponents.VerifyTransverseTension(null!));
        Assert.ThrowsException<ArgumentNullException>(() => ColumnWebComponents.VerifyTransverseCompression(null!));
    }

    [TestMethod]
    public void FactorFactoriesCannotMixNationalPolicies()
    {
        Assert.ThrowsException<ArgumentException>(() => ColumnWebPolicy.Eurocode3(ConnectionDesignCode.Ntc2018(), 1));
        Assert.ThrowsException<ArgumentException>(() => ColumnWebPolicy.Ntc2018(Policy().Code));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => ColumnWebPolicy.Eurocode3(Policy().Code, 0));
    }
}
