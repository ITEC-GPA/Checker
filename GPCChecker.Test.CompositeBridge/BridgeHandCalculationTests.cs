using GPC.Checkers.CompositeBridge;
using GPC.Checkers.CompositeBridge.History;
using GPC.Model.Data.Concrete;
using GPC.Model.Data.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CompositeBridgeTests;

/// <summary>
/// Fifty checks of the bridge section against hand calculations: every expected value is computed in the test from the formulas of
/// EN 1993-1-5, EN 1994-2, EN 1992-1-1 or from the equilibrium of the section, never with the code under test. Units: mm, N, MPa.
/// </summary>
[TestClass, DoNotParallelize]
public class BridgeHandCalculationTests
{
    private const double Fy = 355, Ea = 210000;
    private static readonly double Epsilon = Math.Sqrt(235 / Fy);
    /// <summary>sigma_E = pi² E / (12 (1 - nu²)) (t / b)², EN 1993-1-5 A.1</summary>
    private static double SigmaE(double t, double b) => Math.PI * Math.PI * Ea / (12 * (1 - .3 * .3)) * Math.Pow(t / b, 2);

    private static void Rel(double expected, double actual, double tolerance = 1e-9, string message = "") =>
        Assert.AreEqual(expected, actual, tolerance * Math.Max(1e-12, Math.Abs(expected)), message);

    #region Effective widths, EN 1993-1-5 4.4 (tables 4.1 and 4.2)

    [TestMethod]
    public void InternalPlateInUniformCompression()
    {
        var p = EffectivePlateReduction.InternalPlate(1000, 10, -100, -100, Fy);
        double k = 4, lambda = 100 / (28.4 * Epsilon * Math.Sqrt(k)), rho = (lambda - .055 * (3 + 1)) / (lambda * lambda);
        Rel(1, p.Psi); Rel(k, p.KSigma); Rel(lambda, p.Lambda); Rel(rho, p.Rho);
        Rel(rho * 1000 / 2, p.EffectiveAtStart); Rel(rho * 1000 / 2, p.EffectiveAtEnd);
    }

    [TestMethod]
    public void StockyInternalPlateIsFullyEffective()
    {
        // lambda = 15 / (28.4 eps 2) = 0.325 < 0.5 + sqrt(0.085 - 0.055) = 0.673
        var p = EffectivePlateReduction.InternalPlate(300, 20, -200, -200, Fy);
        Rel(1, p.Rho); Rel(150, p.EffectiveAtStart); Rel(150, p.EffectiveAtEnd);
    }

    [TestMethod]
    public void InternalPlateInPureBending()
    {
        // psi = -1: k = 7.81 + 6.29 + 9.78 = 23.88, bc = b / 2, be1 = 0.4 beff at the compressed edge, be2 = 0.6 beff + tension part
        var p = EffectivePlateReduction.InternalPlate(2000, 12, -300, 300, Fy);
        double k = 23.88, lambda = 2000 / 12d / (28.4 * Epsilon * Math.Sqrt(k)), rho = (lambda - .055 * 2) / (lambda * lambda);
        Assert.IsTrue(lambda > .5 + Math.Sqrt(.085 + .055));
        double beff = rho * 1000;
        Rel(-1, p.Psi); Rel(k, p.KSigma); Rel(rho, p.Rho); Rel(1000, p.CompressedWidth);
        Rel(.4 * beff, p.EffectiveAtStart); Rel(.6 * beff + 1000, p.EffectiveAtEnd);
    }

    [TestMethod]
    public void InternalPlateWithStressRatioOneHalf()
    {
        // psi = 0.5: k = 8.2 / 1.55, be1 = 2 beff / (5 - psi), be2 = beff - be1
        var p = EffectivePlateReduction.InternalPlate(1500, 10, -200, -100, Fy);
        double psi = .5, k = 8.2 / (1.05 + psi), lambda = 150 / (28.4 * Epsilon * Math.Sqrt(k)), rho = (lambda - .055 * (3 + psi)) / (lambda * lambda);
        double beff = rho * 1500, be1 = 2 * beff / (5 - psi);
        Rel(psi, p.Psi); Rel(k, p.KSigma); Rel(rho, p.Rho); Rel(be1, p.EffectiveAtStart); Rel(beff - be1, p.EffectiveAtEnd);
    }

    [TestMethod]
    public void InternalPlateInTensionIsFullyEffective()
    {
        var p = EffectivePlateReduction.InternalPlate(1000, 10, 50, 150, Fy);
        Rel(1, p.Rho); Rel(500, p.EffectiveAtStart); Rel(500, p.EffectiveAtEnd);
    }

    [TestMethod]
    public void FirstEffectiveZoneIsAtTheCompressedEdge()
    {
        var start = EffectivePlateReduction.InternalPlate(2000, 12, -300, 300, Fy);
        var end = EffectivePlateReduction.InternalPlate(2000, 12, 300, -300, Fy);
        Rel(start.EffectiveAtStart, end.EffectiveAtEnd); Rel(start.EffectiveAtEnd, end.EffectiveAtStart);
    }

    [TestMethod]
    public void StrongStressGradientUsesTheLastBranchOfTable41()
    {
        // psi = -5: k with psi limited to -3, k = 5.98 (1 + 3)² = 95.68; lambda < 0.5 + sqrt(0.085 + 0.165) = 1 so rho = 1
        var p = EffectivePlateReduction.InternalPlate(2000, 12, -100, 500, Fy);
        double k = 5.98 * 16, lambda = 2000 / 12d / (28.4 * Epsilon * Math.Sqrt(k)), bc = 2000 / 6d;
        Rel(k, p.KSigma); Rel(lambda, p.Lambda); Assert.IsTrue(lambda < 1); Rel(1, p.Rho);
        Rel(bc, p.CompressedWidth); Rel(.4 * bc, p.EffectiveAtStart); Rel(.6 * bc + 2000 - bc, p.EffectiveAtEnd);
    }

    [TestMethod]
    public void CompressedOutstand()
    {
        // table 4.2, psi = 1: k = 0.43, rho = (lambda - 0.188) / lambda² for lambda > 0.748
        var p = EffectivePlateReduction.Outstand(200, 10, -150, Fy);
        double lambda = 20 / (28.4 * Epsilon * Math.Sqrt(.43)), rho = (lambda - .188) / (lambda * lambda);
        Rel(.43, p.KSigma); Rel(lambda, p.Lambda); Rel(rho, p.Rho); Rel(rho * 200, p.EffectiveAtStart);
    }

    [TestMethod]
    public void StockyOrTensionedOutstandIsFullyEffective()
    {
        Rel(1, EffectivePlateReduction.Outstand(100, 10, -150, Fy).Rho); // lambda = 0.66 < 0.748
        Rel(200, EffectivePlateReduction.Outstand(200, 10, 100, Fy).EffectiveAtStart);
    }

    #endregion

    #region Shear buckling of the web, EN 1993-1-5 5

    [TestMethod]
    public void UnstiffenedWebCriticalStressAndSlenderness()
    {
        var w = BridgeShearConnection.Web(1800, 14, Fy, Ea, 1.05, 1.1, 1.2);
        double tau = 5.34 * SigmaE(14, 1800);
        Rel(5.34, w.KTau); Rel(tau, w.TauCritical); Rel(Math.Sqrt(Fy / (Math.Sqrt(3) * tau)), w.Slenderness);
        Rel(.76 * Math.Sqrt(Fy / tau), w.Slenderness, 1e-3, "(5.3): lambda_w = 0.76 sqrt(fyw / tau_cr)");
    }

    [TestMethod]
    public void LongPanelBucklingCoefficient()
    {
        // a / hw = 1.5 >= 1: k_tau = 5.34 + 4 (hw / a)²
        Rel(5.34 + 4 / 2.25, BridgeShearConnection.Web(1800, 14, Fy, Ea, 1.05, 1.1, 1.2, 2700).KTau);
    }

    [TestMethod]
    public void ShortPanelBucklingCoefficient()
    {
        // a / hw = 0.5 < 1: k_tau = 4 + 5.34 (hw / a)²
        Rel(4 + 5.34 * 4, BridgeShearConnection.Web(1800, 14, Fy, Ea, 1.05, 1.1, 1.2, 900).KTau);
    }

    [TestMethod]
    public void SlenderWebWithNonRigidEndPost()
    {
        var w = BridgeShearConnection.Web(2000, 10, Fy, Ea, 1.05, 1.1, 1.2);
        double lambda = Math.Sqrt(Fy / (Math.Sqrt(3) * 5.34 * SigmaE(10, 2000))), chi = .83 / lambda;
        Assert.IsTrue(lambda >= 1.08);
        Rel(chi, w.Chi); Rel(chi * 2000 * 10 * Fy / (Math.Sqrt(3) * 1.1), w.BucklingResistance); Rel(w.BucklingResistance, w.Resistance);
    }

    [TestMethod]
    public void SlenderWebWithRigidEndPost()
    {
        var w = BridgeShearConnection.Web(2000, 10, Fy, Ea, 1.05, 1.1, 1.2, rigidEndPost: true);
        double lambda = Math.Sqrt(Fy / (Math.Sqrt(3) * 5.34 * SigmaE(10, 2000)));
        Rel(1.37 / (.7 + lambda), w.Chi);
    }

    [TestMethod]
    public void IntermediateSlendernessIsTheSameForBothEndPosts()
    {
        // 0.83 / eta <= lambda < 1.08: chi = 0.83 / lambda for both end posts. Thickness for lambda = 0.9
        double tau = Fy / (Math.Sqrt(3) * .81), t = 1800 * Math.Sqrt(tau / (5.34 * SigmaE(1, 1)));
        var a = BridgeShearConnection.Web(1800, t, Fy, Ea, 1.05, 1.1, 1.2);
        var b = BridgeShearConnection.Web(1800, t, Fy, Ea, 1.05, 1.1, 1.2, rigidEndPost: true);
        Rel(.9, a.Slenderness, 1e-9); Rel(.83 / .9, a.Chi); Rel(.83 / .9, b.Chi);
    }

    [TestMethod]
    public void StockyWebGivesThePlasticResistance()
    {
        // lambda < 0.83 / eta: chi = eta, and the resistance is Vpl,Rd = hw tw fy / (sqrt 3 gammaM0)
        var w = BridgeShearConnection.Web(500, 20, Fy, Ea, 1.05, 1.1, 1.2);
        Rel(1.2, w.Chi); Rel(500 * 20 * Fy / (Math.Sqrt(3) * 1.05), w.Resistance); Rel(w.PlasticResistance, w.Resistance);
    }

    #endregion

    #region Transverse stiffeners, EN 1993-1-5 9

    private static double TauCritical(double hw, double tw, double panel) =>
        (panel / hw >= 1 ? 5.34 + 4 * Math.Pow(hw / panel, 2) : 4 + 5.34 * Math.Pow(hw / panel, 2)) * SigmaE(tw, hw);

    [TestMethod]
    public void StiffenerForceUsesThePanelWithTheStiffenerRemoved()
    {
        // 9.3.3(3): Nst = VEd - fyw hw t / (sqrt 3 gammaM1 lambda_w²) = VEd - tau_cr hw t / gammaM1, lambda_w of the panel 2a
        // (before, the panel a: 217 kN instead of 501 kN)
        var s = BridgeShearConnection.Stiffener(1800, 14, 3000, 150, 20, Fy, Ea, 1.1, 2e6, 0);
        Rel(2e6 - TauCritical(1800, 14, 6000) * 1800 * 14 / 1.1, s.AxialForce);
    }

    [TestMethod]
    public void RigidStiffenerRequirement()
    {
        // 9.3.3(3): a / hw < sqrt 2: Ist >= 1.5 hw³ t³ / a², otherwise Ist >= 0.75 hw t³
        Rel(1.5 * Math.Pow(1800 * 14, 3) / 1e6, BridgeShearConnection.Stiffener(1800, 14, 1000, 150, 20, Fy, Ea, 1.1, 0, 0).RequiredInertia);
        Rel(.75 * 1800 * Math.Pow(14, 3), BridgeShearConnection.Stiffener(1800, 14, 3000, 150, 20, Fy, Ea, 1.1, 0, 0).RequiredInertia);
    }

    [TestMethod]
    public void StiffenerTorsionalAndLocalRatios()
    {
        // 9.2.1(8): IT / Ip >= 5.3 fy / E; outstand c / t <= 14 eps
        var s = BridgeLocalDetails.Stiffener(1800, 14, 1830, 3000, 3000, 1500, 1500, 150, 20, 150, 20, Fy, Ea, 1.1, 0, 0, 0, 0);
        double it = 150 * Math.Pow(20, 3) / 3 * (1 - .63 * 20 / 150), ip = Math.Pow(150, 3) * 20 / 3 + 150 * Math.Pow(20, 3) / 12;
        Rel(5.3 * Fy * ip / (Ea * it), s.TorsionRatio); Rel(150 / (14 * Epsilon * 20), s.LocalRatio);
    }

    [TestMethod]
    public void StiffenerDeviationForcesAndDeflectionUseTheClearWebHeight()
    {
        // 9.2.1(4)-(6): sigma_m = N / b (1/a1 + 1/a2), w0 = min(a1, a2, b) / 300, w <= b / 300 with b = hw (before, the span 1830)
        double hw = 1800, tw = 14, span = 1830, n = 2e6;
        var s = BridgeLocalDetails.Stiffener(hw, tw, span, 2000, 3000, 1000, 1500, 150, 20, 150, 20, Fy, Ea, 1.1, 0, n, 0, 0);
        double half = 15 * Epsilon * tw, iz = 2 * half * Math.Pow(tw, 3) / 12 + 2 * (20 * Math.Pow(150, 3) / 12 + 3000 * Math.Pow((tw + 150) / 2, 2));
        double k = Math.PI / span, sm = n / hw * (1 / 2000d + 1 / 3000d), q = sm / (Ea * iz * Math.Pow(k, 4)), w0 = hw / 300;
        double w = w0 * q / (1 - q), moment = Ea * iz * k * k * w0 * q / (1 - q);
        Rel(iz, s.InertiaOut); Rel(w, s.Deflection); Rel(w / (hw / 300), s.DeflectionRatio!.Value);
        Rel(moment * (tw / 2 + 150) / iz, s.Stress);
    }

    [TestMethod]
    public void IntermediateStiffenerForceInTheBridgeSection()
    {
        // the same Nst in the section check, with panels of 3000 mm on both sides (removed: 6000 mm)
        var d = Input(new BridgePhase { Name = "V", MomentKNm = 1000, ShearKN = 2500 }) with
        {
            Intermediate = new BridgeIntermediateStiffener { Enabled = true, LeftPanel = 3000, EqualPanels = true, Width = 150, Thickness = 20, LengthFactor = 1 }
        };
        var details = HBridgeSection.Calculate(d).Stages.Last().Shear!.Details!;
        double expected = (2500e3 - TauCritical(1800, 14, 6000) * 1800 * 14 / 1.1) / 1000;
        Rel(expected, details.Single(v => v.Name == "Intermedio · Nst").Value);
    }

    #endregion

    #region Headed studs, EN 1994-2 6.6.3.1

    [TestMethod]
    public void StudSteelFailureGoverns()
    {
        var s = BridgeShearConnection.Stud(22, 150, 450, 35, 34077, 1.25);
        double steel = .8 * 450 * Math.PI * 22 * 22 / 4 / 1.25, concrete = .29 * 1 * 22 * 22 * Math.Sqrt(35 * 34077) / 1.25;
        Rel(1, s.Alpha); Rel(steel, s.SteelResistance); Rel(concrete, s.ConcreteResistance); Rel(steel, s.Resistance);
    }

    [TestMethod]
    public void ShortStudReducesAlpha()
    {
        // 3 <= hsc / d <= 4: alpha = 0.2 (hsc / d + 1)
        Rel(.2 * (3.5 + 1), BridgeShearConnection.Stud(19, 66.5, 500, 30, 33000).Alpha);
    }

    [TestMethod]
    public void StudUltimateStrengthIsLimitedTo500()
    {
        var s = BridgeShearConnection.Stud(22, 150, 600, 35, 34077);
        Rel(500, s.UsedFu); Rel(.8 * 500 * Math.PI * 22 * 22 / 4 / 1.25, s.SteelResistance);
    }

    [TestMethod]
    public void StudConcreteFailureGoverns()
    {
        var s = BridgeShearConnection.Stud(25, 125, 450, 20, 30000);
        Rel(.29 * 625 * Math.Sqrt(20 * 30000) / 1.25, s.Resistance); Assert.IsTrue(s.ConcreteResistance < s.SteelResistance);
    }

    #endregion

    #region Longitudinal shear in the slab and anchorage, EN 1992-1-1 6.2.4, 8.4, 9.2.2

    [TestMethod]
    public void SlabSurfaceReinforcementAndStrut()
    {
        // Asf / sf >= vEd hf / (fyd cot theta); vEd <= nu fcd sin theta cos theta, nu = 0.6 (1 - fck / 250)
        double fcd = .85 * 35 / 1.5, fyd = 450 / 1.15;
        var r = BridgeLocalDetails.SlabSurface(500, 250, 2, 35, fcd, 450, fyd, 1);
        double steel = 500 / fyd, strut = .6 * (1 - 35 / 250d) * fcd * 250 / 2;
        Rel(steel, r.RequiredSteel); Rel(strut, r.StrutResistance); Rel(steel / 2, r.SteelRatio!.Value); Rel(500 / strut, r.StrutRatio);
    }

    [TestMethod]
    public void SlabSurfaceWithTransverseBending()
    {
        // 6.2.4(5): the greater of the shear steel and half of it plus the bending steel; strut with cot theta = 1.25
        double fcd = .85 * 35 / 1.5, fyd = 450 / 1.15;
        var r = BridgeLocalDetails.SlabSurface(500, 250, 2, 35, fcd, 450, fyd, 1.25, 1);
        double steel = 500 / (fyd * 1.25);
        Rel(steel / 2 + 1, r.RequiredSteel); Rel(.6 * (1 - 35 / 250d) * fcd * 250 / (1.25 + 1 / 1.25), r.StrutResistance);
    }

    [TestMethod]
    public void SlabSurfaceMinimumReinforcement()
    {
        // 9.2.2(5): rho_min = 0.08 sqrt(fck) / fyk
        var r = BridgeLocalDetails.SlabSurface(10, 250, 2, 35, 19.83, 450, 391.3, 1);
        Rel(.08 * Math.Sqrt(35) / 450 * 250, r.MinimumSteel); Rel(r.MinimumSteel, r.RequiredSteel);
    }

    [TestMethod]
    public void AnchorageLengthWithGoodBond()
    {
        // lb,rqd = phi / 4 sigma_sd / fbd, fbd = 2.25 eta1 eta2 fctd, fctd = fctk,0.05 / gamma_c
        double fctk = .7 * .3 * Math.Pow(35, 2d / 3), fbd = 2.25 * fctk / 1.5, stress = 450 / 1.15;
        Rel(16 * stress / (4 * fbd), BridgeLocalDetails.AnchorageLength(16, stress, fctk, 1.5, true, false));
    }

    [TestMethod]
    public void AnchorageMinimaAndPoorBond()
    {
        double fctk = .7 * .3 * Math.Pow(35, 2d / 3);
        Rel(160, BridgeLocalDetails.AnchorageLength(16, 50, fctk, 1.5, true, false)); // EC2: max(10 phi, 100 mm)
        Rel(320, BridgeLocalDetails.AnchorageLength(16, 50, fctk, 1.5, true, true)); // NTC: max(20 phi, 150 mm)
        double poor = 16 * 391.3 / (4 * 2.25 * .7 * fctk / 1.5); // eta1 = 0.7
        Rel(poor, BridgeLocalDetails.AnchorageLength(16, 391.3, fctk, 1.5, false, false));
    }

    [TestMethod]
    public void AnchorageOfLargeBars()
    {
        // phi > 32 mm: eta2 = (132 - phi) / 100
        double fctk = 2.2, fbd = 2.25 * .92 * fctk / 1.5;
        Rel(40 * 391.3 / (4 * fbd), BridgeLocalDetails.AnchorageLength(40, 391.3, fctk, 1.5, true, false));
    }

    #endregion

    #region Fatigue of the studs, EN 1994-2 6.8.7.2

    [TestMethod]
    public void StudFatigueWithCompressedFlange()
    {
        var r = BridgeLocalDetails.StudFatigue(300, 200, 2, 22, 0, false, 1, 1.25, 1.35);
        double tau = 300 * 200 / 2 / (Math.PI * 22 * 22 / 4), eta = tau * 1.25 / 90;
        Rel(tau, r.StressRange); Rel(eta, r.StudRatio); Rel(eta, r.Ratio);
    }

    [TestMethod]
    public void StudFatigueWithTensionedFlange()
    {
        // (6.57): gamma_Ff sigma / (sigma_c / gamma_Mf) + gamma_Ff tau / (tau_c / gamma_Mf,s) <= 1.3, each term <= 1
        var r = BridgeLocalDetails.StudFatigue(200, 200, 2, 22, 50, true, 1, 1.25, 1.35);
        double tau = 200 * 200 / 2 / (Math.PI * 22 * 22 / 4), a = tau * 1.25 / 90, b = 50 * 1.35 / 80;
        Rel(b, r.FlangeRatio); Rel((a + b) / 1.3, r.Interaction); Rel(Math.Max(Math.Max(a, b), (a + b) / 1.3), r.Ratio);
    }

    #endregion

    #region Plastic resistance and bending-shear interaction, EN 1994-2 6.2.2.4, EN 1993-1-5 7.1

    [TestMethod]
    public void PlasticMomentOfARectangle()
    {
        var block = new BridgeBendingShear.Block(-500, 500, 20, Fy, Fy);
        Rel(Fy * 20 * 1000 * 1000 / 4, BridgeBendingShear.PlasticMoment([block], true));
        Rel(Fy * 20 * 1000 * 1000 / 4, BridgeBendingShear.PlasticMoment([block], false));
    }

    private static (double Bottom, double Top, double Width)[] SteelParts() => [(-25, 0, 500), (-1825, -25, 14), (-1855, -1825, 700)];

    [TestMethod]
    public void PlasticMomentOfTheCompositeSectionInSagging()
    {
        // slab 0.85 fcd, steel fyd. The slab (14.9 MN) is weaker than the steel (19.8 MN): the plastic neutral axis is in the top flange
        double fyd = Fy / 1.05, fc = .85 * .85 * 35 / 1.5;
        var blocks = SteelParts().Select(p => new BridgeBendingShear.Block(p.Bottom, p.Top, p.Width, fyd, fyd)).ToList();
        blocks.Add(new(0, 250, 3000, fc, 0));
        double slab = 3000 * 250 * fc, steel = SteelParts().Sum(p => (p.Top - p.Bottom) * p.Width) * fyd;
        double above = (steel - slab) / 2, depth = above / (500 * fyd); // steel in compression, from the top of the top flange
        Assert.IsTrue(depth > 0 && depth < 25);
        double moment = slab * 125 + above * (-depth / 2) // compression: + F y
            - 500 * (25 - depth) * fyd * (-depth - 25) / 2 - 14 * 1800 * fyd * -925d - 700 * 30 * fyd * -1840d; // tension: - F y
        Rel(moment, BridgeBendingShear.PlasticMoment(blocks, true), 1e-9);
    }

    [TestMethod]
    public void PlasticMomentInHoggingIgnoresTheConcrete()
    {
        // the slab has no tensile strength: the moment is the one of the steel about its equal-area axis
        double fyd = Fy / 1.05;
        var blocks = SteelParts().Select(p => new BridgeBendingShear.Block(p.Bottom, p.Top, p.Width, fyd, fyd)).ToList();
        blocks.Add(new(0, 250, 3000, 19.83, 0));
        double half = SteelParts().Sum(p => (p.Top - p.Bottom) * p.Width) / 2, axis = -25 - (half - 500 * 25) / 14;
        double moment = SteelParts().Sum(p =>
        {
            double lo = p.Bottom, hi = p.Top, a1 = Math.Max(lo, axis), a2 = Math.Min(hi, axis);
            double upper = Math.Max(0, hi - a1), lower = Math.Max(0, a2 - lo); // parts above and below the axis
            return fyd * p.Width * (upper * ((hi + a1) / 2 - axis) + lower * (axis - (a2 + lo) / 2));
        });
        Rel(moment, BridgeBendingShear.PlasticMoment(blocks, false), 1e-9);
    }

    [TestMethod]
    public void PlasticMomentOfARectangleWithAxialForce()
    {
        // M = Mpl (1 - n²) for a rectangle
        var r = BridgeBendingShear.AtAxialForce([new BridgeBendingShear.Block(-500, 500, 20, Fy, Fy)], .5 * Fy * 20 * 1000);
        double mpl = Fy * 20 * 1e6 / 4;
        Assert.IsTrue(r.Feasible); Rel(.75 * mpl, r.Maximum, 1e-9); Rel(-.75 * mpl, r.Minimum, 1e-9);
    }

    [TestMethod]
    public void AxialForceBeyondTheSquashLoadIsNotFeasible()
    {
        var r = BridgeBendingShear.AtAxialForce([new BridgeBendingShear.Block(-500, 500, 20, Fy, Fy)], -1.01 * Fy * 20 * 1000);
        Assert.IsFalse(r.Feasible); Rel(Fy * 20 * 1000, r.Compression); Rel(Fy * 20 * 1000, r.Tension);
    }

    [TestMethod]
    public void BendingShearInteraction()
    {
        // eta1 + (1 - Mf / Mpl)(2 eta3 - 1)² only when eta3 > 0.5 and M > Mf
        Rel(.8 + .6 * .25, BridgeBendingShear.Interaction(80, 100, 40, 75, 100));
        Rel(.4, BridgeBendingShear.Interaction(40, 100, 40, 90, 100));
        Rel(.8, BridgeBendingShear.Interaction(80, 100, 40, 40, 100));
    }

    [TestMethod]
    public void ElasticBendingShearEnvelope()
    {
        Rel(.7 + .36, BridgeBendingShear.ElasticInteraction(.7, 80, 100));
        Rel(.7, BridgeBendingShear.ElasticInteraction(.7, 40, 100));
    }

    #endregion

    #region Elastic stresses of the section

    private static HBridgeInput Input(params BridgePhase[] phases) => new()
    {
        Materials = new(ConcreteMaterialEN1992Data.C35_45, SteelMaterialEN1993Data.S355, SteelMaterialEN1992Data.B450C),
        Geometry = new() { SlabWidth = 3000, SlabHeight = 250, WebHeight = 1800, WebThickness = 14, TopWidth = 500, TopThickness = 25, BottomWidth = 700, BottomThickness = 30 },
        TopRebars = new() { Enabled = true, Diameter = 16, Pitch = 150, AxisDistance = 45 },
        BottomRebars = new() { Enabled = true, Diameter = 16, Pitch = 150, AxisDistance = 45 },
        Options = new() { Class4 = false },
        Phases = phases
    };

    /// <summary>Area, centroid and inertia (with the own inertias) of the steel H</summary>
    private static (double A, double Y, double I) Steel(bool ownFlanges = true)
    {
        var p = SteelParts();
        double a = p.Sum(x => (x.Top - x.Bottom) * x.Width), y = p.Sum(x => (x.Top - x.Bottom) * x.Width * (x.Top + x.Bottom) / 2) / a;
        double i = p.Sum(x => (ownFlanges || x.Width == 14 ? x.Width * Math.Pow(x.Top - x.Bottom, 3) / 12 : 0) + (x.Top - x.Bottom) * x.Width * Math.Pow((x.Top + x.Bottom) / 2 - y, 2));
        return (a, y, i);
    }

    /// <summary>Transformed section in steel units: slab b tc / n, bars (Es / Ea - 1 / n) As; own inertias optional (the Checker
    /// integration treats the flanges as lines and the bars as points)</summary>
    private static (double A, double Y, double I) Composite(double n, bool own)
    {
        var bars = HBridgeSection.Geometry(Input(new BridgePhase())).Bars;
        double es = SteelMaterialEN1992Data.B450C.ElasticModulusTension, ratio = es / Ea - 1 / n;
        var (sa, sy, _) = Steel();
        double a = sa + 3000 * 250 / n + bars.Sum(b => b.Area) * ratio;
        double y = (sa * sy + 3000 * 250 / n * 125 + bars.Sum(b => b.Area * b.Y) * ratio) / a;
        double i = SteelParts().Sum(x => (own || x.Width == 14 ? x.Width * Math.Pow(x.Top - x.Bottom, 3) / 12 : 0) + (x.Top - x.Bottom) * x.Width * Math.Pow((x.Top + x.Bottom) / 2 - y, 2))
            + 3000 * Math.Pow(250, 3) / 12 / n + 3000 * 250 / n * Math.Pow(125 - y, 2)
            + bars.Sum(b => ((own ? Math.PI * Math.Pow(b.Diameter, 4) / 64 : 0) + b.Area * Math.Pow(b.Y - y, 2)) * ratio);
        return (a, y, i);
    }

    private static double N0 => Ea / ConcreteMaterialEN1992Data.C35_45.ElasticModulusCompression;

    [TestMethod]
    public void SteelOnlyPhaseFollowsNavierOnTheSteelSection()
    {
        var c = HBridgeSection.Calculate(Input(new BridgePhase { Kind = BridgePhaseKind.SteelOnly, MomentKNm = 1500 })).Stages[0].Contributions[0];
        var (a, y, i) = Steel();
        Rel(a, c.Area); Rel(y, c.Centroid); Rel(i, c.Inertia);
        foreach (double fibre in new[] { 0, -25, -1825, -1855d })
            Rel(-1500e6 * (fibre - y) / i, c.SteelStress(fibre), 1e-9, "sigma = -M (y - yG) / I at " + fibre);
        Rel(0, c.Stress("CLS", 250)); Rel(0, c.Stress("Armatura", 205));
    }

    [TestMethod]
    public void AxialForceAtTheGrossCentroidGivesUniformStress()
    {
        var c = HBridgeSection.Calculate(Input(new BridgePhase { Kind = BridgePhaseKind.SteelOnly, ForceKN = -1000, Reference = BridgeLoadReference.GrossCentroid })).Stages[0].Contributions[0];
        double a = Steel().A;
        Rel(-1e6 / a, c.SteelStress(0)); Rel(-1e6 / a, c.SteelStress(-1855)); Assert.AreEqual(0, c.StressSlope, 1e-12);
    }

    [TestMethod]
    public void AxialForceAtACommonElevationAddsItsMoment()
    {
        // N applied at y = 100 on the steel section: M about the centroid = N (yG - yN)
        var d = Input(new BridgePhase { Kind = BridgePhaseKind.SteelOnly, ForceKN = 500, Reference = BridgeLoadReference.CommonElevation });
        d = d with { Options = d.Options with { CommonLoadY = 100 } };
        var c = HBridgeSection.Calculate(d).Stages[0].Contributions[0];
        var (a, y, i) = Steel();
        double m = 500e3 * (y - 100);
        Rel(500e3 / a - m * (-1855 - y) / i, c.SteelStress(-1855)); Rel(500e3 / a - m * (0 - y) / i, c.SteelStress(0));
    }

    [TestMethod]
    public void CompositePhaseTransformedSection()
    {
        var c = HBridgeSection.Calculate(Input(new BridgePhase { Name = "Q", MomentKNm = 3000 })).Stages[0].Contributions[0];
        var bars = HBridgeSection.Geometry(Input(new BridgePhase())).Bars;
        Assert.AreEqual(40, bars.Length); Assert.AreEqual(20, bars.Count(b => b.Y == 250 - 45)); Assert.AreEqual(20, bars.Count(b => b.Y == 45));
        var (a, y, i) = Composite(N0, true);
        Rel(N0, c.HomogenizationN); Rel(a, c.Area); Rel(y, c.Centroid); Rel(i, c.Inertia);
    }

    [TestMethod]
    public void CompositePhaseStressesInSteelConcreteAndBars()
    {
        var c = HBridgeSection.Calculate(Input(new BridgePhase { Name = "Q", MomentKNm = 3000 })).Stages[0].Contributions[0];
        var (_, y, i) = Composite(N0, false); // integration inertia (flanges as lines, bars as points)
        double Sigma(double fibre) => -3000e6 * (fibre - y) / i;
        double es = SteelMaterialEN1992Data.B450C.ElasticModulusTension;
        Rel(Sigma(-1855), c.SteelStress(-1855), 1e-6); Rel(Sigma(0), c.SteelStress(0), 1e-6);
        Rel(Sigma(250) / N0, c.Stress("CLS", 250), 1e-6, "concrete = steel equivalent / n");
        Rel(Sigma(205) * es / Ea, c.Stress("Armatura", 205), 1e-6, "bars = steel equivalent Es / Ea");
        Assert.IsTrue(c.EquilibriumResidual < 1e-6);
    }

    [TestMethod]
    public void LongTermModularRatioFromCreep()
    {
        // EN 1994-2 5.4.2.2: nL = n0 (1 + psiL phi_t)
        var c = HBridgeSection.Calculate(Input(new BridgePhase { MomentKNm = 2000, Phi = 2, PsiL = 1.1 })).Stages[0].Contributions[0];
        Rel(N0 * (1 + 1.1 * 2), c.HomogenizationN); Rel(Composite(N0 * 3.2, true).I, c.Inertia);
    }

    [TestMethod]
    public void ShrinkageIsSelfEquilibrated()
    {
        // primary effects only: the integrals of the stresses on concrete, bars and steel must vanish
        var c = HBridgeSection.Calculate(Input(new BridgePhase { Kind = BridgePhaseKind.Shrinkage, ShrinkageMicrostrain = -300, Phi = 2, PsiL = .55 })).Stages[0].Contributions[0];
        var bars = HBridgeSection.Geometry(Input(new BridgePhase())).Bars;
        double a0 = c.UniformStress - c.StressSlope * c.Centroid, a1 = c.StressSlope, n = c.HomogenizationN, es = SteelMaterialEN1992Data.B450C.ElasticModulusTension;
        (double N, double S) Rectangle(double w, double y0, double y1, double b0, double b1) =>
            (w * (b0 * (y1 - y0) + b1 * (y1 * y1 - y0 * y0) / 2), w * (b0 * (y1 * y1 - y0 * y0) / 2 + b1 * (y1 * y1 * y1 - y0 * y0 * y0) / 3));
        (double N, double S) Line(double area, double y, double b0, double b1) => (area * (b0 + b1 * y), area * (b0 + b1 * y) * y);
        double force = 0, moment = 0;
        foreach (var p in SteelParts())
        {
            // the Checker integration takes the flanges as lines at their mid-plane (no own inertia) and the web along its height
            var r = p.Width == 14 ? Rectangle(p.Width, p.Bottom, p.Top, a0, a1) : Line(p.Width * (p.Top - p.Bottom), (p.Top + p.Bottom) / 2, a0, a1);
            force += r.N; moment -= r.S;
        }
        var concrete = Rectangle(3000, 0, 250, a0 / n + c.ConcreteStressOffset, a1 / n);
        force += concrete.N; moment -= concrete.S;
        foreach (var b in bars)
        {
            double sc = c.Stress("CLS", b.Y), sb = c.Stress("Armatura", b.Y);
            force += b.Area * (sb - sc); moment -= b.Area * (sb - sc) * b.Y;
        }
        Assert.IsTrue(concrete.N > 1e6, "the restrained shrinkage puts the slab in tension");
        Assert.AreEqual(0, force, 1e-4 * concrete.N); Assert.AreEqual(0, moment, 1e-4 * concrete.N * 250);
        Rel(-Ea / n * -300e-6, c.ConcreteStressOffset);
    }

    [TestMethod]
    public void IntegrationInertiaDoesNotDependOnTheLoad()
    {
        // before, an unloaded composite phase reported the Model inertia instead of the integration one
        var zero = HBridgeSection.Calculate(Input(new BridgePhase { MomentKNm = 0 })).Stages[0].Contributions[0];
        var loaded = HBridgeSection.Calculate(Input(new BridgePhase { MomentKNm = 1000 })).Stages[0].Contributions[0];
        Rel(loaded.SolverInertia, zero.SolverInertia); Rel(Composite(N0, false).I, zero.SolverInertia, 1e-9);
    }

    [TestMethod]
    public void EffectiveSteelSectionOfTheReducedWeb()
    {
        // class 4 web in bending: the effective steel area is the gross one minus the ineffective zone of the web
        var d = Input(new BridgePhase { Kind = BridgePhaseKind.SteelOnly, MomentKNm = 4000 });
        var s = HBridgeSection.Calculate(d with { Options = d.Options with { Class4 = true } }).Stages[0];
        var e = s.Effective;
        Assert.IsTrue(e.WebTop + e.WebBottom < 1800);
        double top = 14 + 2 * e.Top.EffectiveAtStart, bottom = 14 + 2 * e.Bottom.EffectiveAtStart;
        Rel(top, e.TopWidth); Rel(top * 25 + (e.WebTop + e.WebBottom) * 14 + bottom * 30, s.EffectiveSteel.Area, 1e-9);
        var web = EffectivePlateReduction.InternalPlate(1800, 14, s.Contributions.Sum(c => c.SteelStress(-25)), s.Contributions.Sum(c => c.SteelStress(-1825)), Fy);
        Rel(web.EffectiveAtStart, e.WebTop, 1e-5); Rel(web.EffectiveAtEnd, e.WebBottom, 1e-5); // converged to 1e-7 hw
    }

    #endregion

    #region History

    [TestMethod]
    public void LinearHistoryAgreesWithTheCumulativeMethod()
    {
        // steel only (1500 kNm), then composite (3000 kNm): the concrete is born stress-free on the deformed steel
        var d = Input(new BridgePhase { Name = "G1", Kind = BridgePhaseKind.SteelOnly, MomentKNm = 1500 }, new BridgePhase { Name = "Q", MomentKNm = 3000 });
        var cumulative = HBridgeSection.Calculate(d).Stages[1].Contributions;
        var history = HBridgeHistoryAnalysis.Calculate(d, new HBridgeHistoryOptions { WebLayers = 400, FlangeLayers = 8, ConcreteLayers = 64 }).Stages;
        double ec = ConcreteMaterialEN1992Data.C35_45.ElasticModulusCompression;
        Rel(cumulative.Sum(c => c.SteelStress(-1855)), Ea * history[1].TotalPlane.At(-1855), 5e-4);
        Rel(cumulative.Sum(c => c.SteelStress(0)), Ea * history[1].TotalPlane.At(0), 5e-4);
        Rel(cumulative.Sum(c => c.Stress("CLS", 250)), ec * (history[1].TotalPlane.At(250) - history[0].TotalPlane.At(250)), 5e-4);
    }

    [TestMethod]
    public void LinearHistoryShrinkageIsSelfEquilibratedAndModuliAreAtTheSteelFibres()
    {
        var d = Input(new BridgePhase { Kind = BridgePhaseKind.Shrinkage, ShrinkageMicrostrain = -300, Phi = 2, PsiL = .55 });
        var stage = HBridgeHistoryAnalysis.Calculate(d).Stages[0];
        Assert.AreEqual(0, stage.N); Assert.AreEqual(0, stage.IntegratedN, 1); Assert.AreEqual(0, stage.IntegratedMomentAtOrigin, 1e3);
        Assert.IsTrue(stage.Fibers.Where(f => f.ComponentId == HBridgeHistoryAnalysis.Concrete).All(f => f.Stress > 0), "restrained shrinkage: slab in tension");
        var c = HBridgeHistoryResults.Calculate(d).Stages[0].Contributions[0];
        // before, the top modulus of the history method was at the top of the slab, the one of the cumulative method at the top of the steel
        Rel(c.Inertia / Math.Abs(c.Centroid), c.WTop!.Value); Rel(c.Inertia / Math.Abs(-1855 - c.Centroid), c.WBottom);
    }

    [TestMethod]
    public void BilinearSteelWithIsotropicHardening()
    {
        // sigma = fy + Et (eps - eps_y) on loading, elastic unloading, reverse yielding at fy + H alpha, H = E Et / (E - Et)
        var law = new HistoryBilinearSteelLaw(Ea, Fy, 2100);
        double h = Ea * 2100 / (Ea - 2100);
        var loaded = law.Evaluate(.005, law.InitialState(), 1);
        double peak = Fy + 2100 * (.005 - Fy / Ea), plastic = .005 - peak / Ea;
        Rel(peak, loaded.Stress); Rel(Ea * h / (Ea + h), loaded.Tangent); Rel(plastic, ((HistoryPlasticState)loaded.State).PlasticStrain);
        var unloaded = law.Evaluate(.004, loaded.State, 1);
        Rel(peak - Ea * .001, unloaded.Stress); Rel(Ea, unloaded.Tangent);
        var reversed = law.Evaluate(-.002, unloaded.State, 1);
        double trial = Ea * (-.002 - plastic), dp = (Math.Abs(trial) - (Fy + h * plastic)) / (Ea + h);
        Rel(-(Fy + h * (plastic + dp)), reversed.Stress);
    }

    [TestMethod]
    public void MomentCurvatureOfAnElasticPlasticRectangle()
    {
        // rectangle 20 x 1000: EI in the elastic range, M = Mp (1 - (kappa_y / kappa)² / 3) beyond the yield
        var section = new HistorySection([new HistoryComponent("steel", new HistoryBilinearSteelLaw(Ea, Fy),
            HBridgeHistoryAnalysis.Rectangle("steel", 0, -500, 20, 1000, 400), true)]);
        double ky = Fy / Ea / 500, mp = Fy * 20 * 1e6 / 4;
        var r = BridgeSectionResponseAnalysis.Calculate(section, [.5 * ky, 5 * ky],
            new SectionResponseOptions { Control = SectionResponseControl.MomentCurvature, AxialForce = 0, SubstepsPerTarget = 20 });
        Assert.IsTrue(r.Completed, r.Message);
        Rel(Ea * 20 * 1e9 / 12, r.Points[1].BendingTangentAtConstantN!.Value, 1e-9);
        Rel(Ea * 20 * 1e9 / 12 * .5 * ky, r.Points[1].MomentAtReference, 1e-9);
        Rel(mp * (1 - 1 / 75d), r.Points[2].MomentAtReference, 1e-4);
    }

    #endregion
}
