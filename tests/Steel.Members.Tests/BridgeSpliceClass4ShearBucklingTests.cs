using GPC.Checkers.Steel.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Steel.Members.Tests;

/// <summary>EffectiveCompressionWithShearBuckling on the 280mm web covers of the low-shear fixture
/// (M12 bolts, holes 13, cover end 60, S355/510). Expected values are independent EN1993-1-5
/// hand calculations: sigma_E=pi^2 E/(12(1-nu^2))(t/b)^2, tau_cr=5.34 sigma_E, Table5.1 and 7.1.</summary>
[TestClass]
public class BridgeSpliceClass4ShearBucklingTests
{
    const SplicePlateStabilityMethod Method = SplicePlateStabilityMethod.EffectiveCompressionWithShearBuckling;
    static WeldedIBeamSpliceDetailInput Cover(bool ntc, double v, double n, double t, int rows, double pitch, double first, bool threaded = true)
    {
        var p = BridgeSpliceEffectiveCompressionTests.Input(ntc, n, v, outerWidth: 180);
        var b = p.Plates.Bolts; var d = b.Detail;
        var f = new WeldedIBeamSpliceForceInput(d.Section, d.Top, d.Bottom, new(280, t, 355, 510), new(3, rows, pitch, pitch, first), 60, d.ButtGap, d.Actions);
        return new(new(p.Plates.Policy, new(b.Code, f, b.FlangeBolt, new ConnectionBolt(12, 13, 84.3, ConnectionBoltGrade.Class8_8, threaded), b.FlangeHardwareDiameter, 24,
            "Concomitant ULS", "NTC SLE clearance")), p.Exposure);
    }
    // Two rows at 70mm (outstands 105) as in the low-shear tests; four rows at 56mm (outstands 56) for t=4.
    static WeldedIBeamSpliceDetailInput Six(bool ntc, double v, double n = -10000) => Cover(ntc, v, n, 6, 2, 70, 35, false);
    static WeldedIBeamSpliceDetailInput Four(bool ntc, double v, double n = 10000) => Cover(ntc, v, n, 4, 4, 56, 28);
    static WeldedIBeamSpliceStandardInput Workflow(WeldedIBeamSpliceDetailInput p, SplicePlateStabilityMethod method,
        SpliceBlockVerificationMethod block = SpliceBlockVerificationMethod.ActualCollinearForcesWithHierarchyFallback)
        => new(new[] { new WeldedSpliceStandardCase("U", WeldedIBeamSpliceStandardTests.Parents(p)) }, SpliceBoltCategory.A, null,
            block, SpliceFlangeFaceModel.EqualThicknessIndependentWidths, method);
    static double T(ConnectionVerification<WeldedIBeamSpliceDetailInput> r, string name) => r.Trace.Single(x => x.Name == "WebNegative." + name).Value;
    static IReadOnlyList<SpliceCoverCut> Cuts(WeldedIBeamSpliceDetailInput p)
        => WeldedIBeamSplicePlates.Verify(p.Plates).Covers.Single(c => c.Cover == WeldedSpliceCover.WebNegative).Cuts;
    static double Lambda(double t) => Math.Sqrt(355 / (Math.Sqrt(3) * 5.34 * Math.PI * Math.PI * 210000 / (12 * (1 - .09)) * Math.Pow(t / 280, 2)));
    static double GammaM1(bool ntc) => ntc ? 1.05 : 1;
    static void Near(double e, double a) => Assert.AreEqual(e, a, Math.Max(1e-8, Math.Abs(e) * 1e-10));

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void ShearJustAboveTheLowShearBoundIsEvaluated(bool ntc)
    {
        // 85kN in the central cuts; the low-shear bound is 0.5*min(...)=80.4kN (EC3).
        var p = Six(ntc, 170000);
        Assert.AreEqual(85000, Cuts(p).Max(c => Math.Abs(c.V)), 1e-6);
        var low = WeldedIBeamSpliceEffectiveCompression.Verify(p, true);
        Assert.AreEqual(ConnectionStatus.NotSupported, low.Status);
        Assert.IsTrue(low.Diagnostics.Any(x => x.EndsWith("CLASS4_SHEAR_EXCEEDS_HALF_RESISTANCE")));
        var r = WeldedIBeamSpliceEffectiveCompression.VerifyWithShearBuckling(p);
        Assert.AreEqual(ConnectionStatus.Passed, r.Status, string.Join(";", r.Diagnostics));
        Assert.AreEqual(1, T(r, "IsLocallyReduced"));
        // b/t=46.7: lambda_w=0.6636<0.83, chi_w=eta=1.
        Near(Lambda(6), T(r, "ShearBuckling.LambdaW")); Assert.AreEqual(.6636, Lambda(6), 1e-4);
        Near(1, T(r, "ShearBuckling.ChiW"));
        double vbw = 280 * 6 * 355 / (Math.Sqrt(3) * GammaM1(ntc));
        var shear = r.Checks.Where(c => c.Id.StartsWith("WebNegative.") && c.Id.EndsWith(".ShearBuckling")).ToArray();
        Assert.AreEqual(Cuts(p).Count, shear.Length);
        foreach (var c in shear) Near(vbw, c.Resistance);
        Near(85000, shear.Max(c => c.Demand));
        Assert.IsFalse(r.Checks.Any(c => c.Id.EndsWith(".ShearNormalInteraction"))); // eta3=0.247: 7.1(1)
    }

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void CompleteWorkflowReplacesTheWholeWidthScreenOnlyForTheNewMethod(bool ntc)
    {
        var p = Six(ntc, 170000);
        var low = WeldedIBeamSpliceStandard.Verify(Workflow(p, SplicePlateStabilityMethod.EffectiveCompressionWithLowShear));
        Assert.AreEqual(ConnectionStatus.NotSupported, low.Status);
        foreach (var block in new[] { SpliceBlockVerificationMethod.ActualCollinearForcesWithHierarchyFallback, SpliceBlockVerificationMethod.ActualForcesWithMixedBlockInteraction })
        {
            var r = WeldedIBeamSpliceStandard.Verify(Workflow(p, Method, block));
            Assert.IsTrue(r.IsStaticResistanceComplete, string.Join(";", r.MissingRequirements));
            Assert.AreEqual(ConnectionStatus.Passed, r.Status, string.Join(";", r.CheckSources.Where(x => x.Failed).Select(x => x.ComponentCheck?.Id)));
            var domain = r.Cases[0].AdditionalPlates.CompactPlateDomain.Checks;
            Assert.IsTrue(domain.Any(c => c.Id == "WebNegative.ShearPanel.MaximumLongitudinalGap" && c.Passed));
            Assert.IsFalse(domain.Any(c => c.Id == "WebNegative.NoShearBucklingDomain"));
            Assert.IsTrue(domain.Any(c => c.Id == "TopOuter.NoShearBucklingDomain")); // unsheared flange covers keep the screen
            var report = BridgeSpliceReport.FromBolted(r);
            StringAssert.Contains(report.Scope, "EffectiveCompressionWithShearBuckling");
            StringAssert.Contains(report.ToText(), "WebNegative.ShearBuckling.VbwRd=");
        }
    }

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void HighShearOnABucklingSensitiveClassFourCoverUsesTheSevenOneInteraction(bool ntc)
    {
        // t=4, b/t=70>60eps: lambda_w=0.9954, non-rigid end post chi_w=0.83/lambda_w=0.8338 (<1/lambda_w^2).
        var p = Four(ntc, 200000);
        double lambda = Lambda(4), chi = .83 / lambda, vbw = chi * 280 * 4 * 355 / (Math.Sqrt(3) * GammaM1(ntc));
        Assert.AreEqual(.99544, lambda, 1e-5); Assert.IsTrue(chi < 1 / (lambda * lambda));
        if (!ntc) Assert.AreEqual(191404, vbw, 1);
        var low = WeldedIBeamSpliceEffectiveCompression.Verify(p, true);
        Assert.IsTrue(low.Diagnostics.Any(x => x.EndsWith("CLASS4_SHEAR_BUCKLING_NOT_EXCLUDED")));
        var r = WeldedIBeamSpliceEffectiveCompression.VerifyWithShearBuckling(p);
        Assert.AreEqual(ConnectionStatus.Passed, r.Status, string.Join(";", r.Diagnostics));
        Near(chi, T(r, "ShearBuckling.ChiW")); Near(vbw, T(r, "ShearBuckling.VbwRd"));
        // Effective section: outstands c=56, k=0.43, rho=(lp-0.188)/lp^2; internal strips 56/4 fully effective.
        double eps = Math.Sqrt(235d / 355), lp = 14 / (28.4 * eps * Math.Sqrt(.43)), rho = (lp - .188) / (lp * lp);
        double aeff = (168 + 2 * 56 * rho) * 4, ieff = 4 * (Math.Pow(168, 3) / 12 + 2 * (Math.Pow(84 + 56 * rho, 3) - Math.Pow(84, 3)) / 3);
        Near(aeff, T(r, "EffectiveArea"));
        var cuts = Cuts(p); int k = 0, interactions = 0;
        foreach (var cut in cuts)
        {
            string id = "WebNegative.Cut" + k++ + ".ShearNormalInteraction";
            double eta3 = Math.Abs(cut.V) / vbw;
            var check = r.Checks.SingleOrDefault(c => c.Id == id);
            Assert.AreEqual(eta3 > .5, check != null, id);
            if (check == null) continue;
            interactions++;
            double eta1 = (Math.Abs(cut.N) / aeff + Math.Abs(cut.M) * 140 / ieff) / (355 / (ntc ? 1.05 : 1));
            Assert.AreEqual(eta1 + Math.Pow(2 * eta3 - 1, 2), check.Demand, 1e-9);
        }
        Assert.AreEqual(3, interactions); // both inner column cuts and the butt plane carry 100kN
    }

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void VerySlenderCoverIsLimitedToTheElasticCriticalShear(bool ntc)
    {
        // t=3, b/t=93.3: lambda_w=1.327>1.205, so tau_cr governs over Table5.1: Vbw=tau_cr*b*t/gammaM1.
        var p = Cover(ntc, 60000, 10000, 3, 5, 42, 21);
        var r = WeldedIBeamSpliceEffectiveCompression.VerifyWithShearBuckling(p);
        double lambda = Lambda(3), tauCr = 355 / (Math.Sqrt(3) * lambda * lambda);
        Assert.AreEqual(1.3272, lambda, 1e-4);
        Near(.83 / lambda, T(r, "ShearBuckling.ChiTable5_1")); Near(1 / (lambda * lambda), T(r, "ShearBuckling.ChiW"));
        Near(tauCr * 280 * 3 / GammaM1(ntc), r.Checks.First(c => c.Id.EndsWith(".ShearBuckling")).Resistance);
    }

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void ShearAboveTheBucklingResistanceIsAKnownFailure(bool ntc)
    {
        var r = WeldedIBeamSpliceEffectiveCompression.VerifyWithShearBuckling(Four(ntc, 420000));
        Assert.AreEqual(ConnectionStatus.Failed, r.Status);
        Assert.IsTrue(r.Checks.Any(c => c.Id.EndsWith(".ShearBuckling") && !c.Passed && c.Utilization > 1.09));
    }

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void WhollyCompressedCutAtHighShearRequiresSectionTen(bool ntc)
    {
        // Compressed cover: the butt cut has M=0 and N<0. Above eta3=0.5, 7.1(5) refers to section10.
        var high = WeldedIBeamSpliceEffectiveCompression.VerifyWithShearBuckling(Four(ntc, 200000, -10000));
        Assert.AreNotEqual(ConnectionStatus.Passed, high.Status);
        Assert.IsTrue(high.Diagnostics.Contains("WebNegative.HIGH_SHEAR_WHOLE_PLATE_COMPRESSED_REQUIRES_EN1993_1_5_SECTION10"));
        // Below eta3=0.5 the normal resistance is not reduced (7.1(1)), also when the cut is compressed.
        var lowShear = WeldedIBeamSpliceEffectiveCompression.VerifyWithShearBuckling(Four(ntc, 160000, -10000));
        Assert.AreEqual(ConnectionStatus.Passed, lowShear.Status, string.Join(";", lowShear.Diagnostics));
    }

    [TestMethod]
    public void PanelRestraintIsAMandatoryDomain()
    {
        // t=4 with the 70mm grid: the gaps exceed 14t=56mm along the restraining outer rows.
        var p = Cover(false, 60000, 10000, 4, 2, 70, 35);
        var r = WeldedIBeamSpliceEffectiveCompression.VerifyWithShearBuckling(p);
        Assert.AreEqual(ConnectionStatus.NotSupported, r.Status);
        Assert.IsTrue(r.Diagnostics.Contains("WebNegative.SHEAR_PANEL_RESTRAINT_NOT_ESTABLISHED"));
        var w = WeldedIBeamSpliceStandard.Verify(Workflow(p, Method));
        Assert.IsFalse(w.Cases[0].AdditionalPlates.CompactPlateDomain.Checks.Single(c => c.Id == "WebNegative.ShearPanel.MaximumLongitudinalGap").Passed);
        Assert.AreEqual(ConnectionStatus.NotSupported, w.Status);
    }

    [DataTestMethod][DataRow(false)][DataRow(true)]
    public void WithoutShearTheNewMethodEqualsEffectiveCompression(bool ntc)
    {
        var p = Six(ntc, 0);
        var a = WeldedIBeamSpliceEffectiveCompression.Verify(p);
        var b = WeldedIBeamSpliceEffectiveCompression.VerifyWithShearBuckling(p);
        Assert.AreEqual(a.Status, b.Status);
        CollectionAssert.AreEqual(a.Checks.Select(c => (c.Id, c.Demand, c.Resistance)).ToArray(), b.Checks.Select(c => (c.Id, c.Demand, c.Resistance)).ToArray());
        CollectionAssert.AreEqual(a.Trace.Select(x => (x.Name, x.Value)).ToArray(), b.Trace.Select(x => (x.Name, x.Value)).ToArray());
    }

    [TestMethod]
    public void UnknownPlateMethodStillRejected()
        => Assert.ThrowsException<ArgumentOutOfRangeException>(() => Workflow(Six(false, 0), (SplicePlateStabilityMethod)99));
}
