using GPC.Checkers.Steel.EffectiveSections;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Checkers.Steel.Serviceability;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Criterion = GPC.Checkers.Steel.Serviceability.SteelServiceStressCriterion;
using Status = GPC.Checkers.Steel.Pipeline.SteelExecutionStatus;

namespace Steel.Members.Tests;

/// <summary>Serviceability stress limits (EN 1993-2:2006 7.3 form) with hand-computed stresses: closed-form gross
/// properties, sigma = N/A + Mx y/Ixx - My x/Iyy, tau = V S/(I t) of the thin-walled models, written out here.</summary>
[TestClass]
public class SteelServiceStressTests
{
    private static readonly SteelServiceStressBasis Basis = new(1.0, "EN 1993-2:2006 7.3(1) note: recommended value", "Road bridge main girder, EN 1993-2 7.3");
    private static SteelVerificationInput Input(Section shape, double n = 0, double m1 = 0, double m2 = 0, double v1 = 0, double v2 = 0,
        double t = 0, bool ntc = false, bool? unperforated = true, double fy = 355, Section.FormedTypes formed = Section.FormedTypes.HotFinished)
        => new(new SteelSectionInput(new SteelSection(shape, new SteelMaterial("S355 test", 210000, fy, 510), Section.SectionTypes.Welded, formed), unperforated),
            new SteelActions(n, v1, v2, t, m1, m2), ntc ? SteelDesignContext.Ntc2018() : new SteelDesignContext(SectionResistanceCode.Eurocode3(1, 1.25, "test")));
    private static SteelServiceStressResult Run(SteelVerificationInput input, SteelServiceStressBasis? basis = null)
        => SteelServiceStressCheck.Evaluate(input, basis ?? Basis);
    private static void Close(double expected, double actual, double relative = 1e-9)
        => Assert.AreEqual(expected, actual, Math.Max(1e-9, Math.Abs(expected) * relative));

    [TestMethod]
    public void SolidRectangleNormalForceBiaxialBendingAndShearByHand()
    {
        // 50 x 200 (X x Y): A = 10000, Ixx = 50*200^3/12, Iyy = 200*50^3/12. N = 200 kN, M1 = 20 kNm, M2 = 0.5 kNm, V2 = 100 kN.
        // sigma = 20 + 0.6 y - 0.24 x: 20 + 60 + 6 = 86 MPa at (-25, 100); tau = 1.5 V/A = 15 MPa at y = 0 (sigma 26 at x = -25).
        // Along a side sigma^2 + 3 tau^2 grows from y = 0 to the corner, so the equivalent stress is the corner 86 MPa.
        var r = Run(Input(new SectionRectangular(200, 50, "flat"), n: 2e5, m1: 2e7, m2: 5e5, v2: 1e5));
        Assert.AreEqual(Status.Passed, r.Status, r.Message); Assert.AreEqual("GrossElastic", r.StressModel); Assert.AreEqual(3, r.SectionClass);
        var normal = r.Value(Criterion.Normal); Close(86, normal.Stress); Close(-25, normal.X); Close(100, normal.Y); Close(355, normal.Limit);
        var shear = r.Value(Criterion.Shear); Close(15, shear.Stress); Close(0, shear.Y, 0); Close(26, shear.Sigma);
        Close(355 / Math.Sqrt(3), shear.Limit); Close(15 * Math.Sqrt(3) / 355, shear.Utilization);
        var equivalent = r.Value(Criterion.Equivalent); Close(86, equivalent.Stress); Assert.IsFalse(equivalent.IsBound);
        Close(86 / 355.0, r.Utilization!.Value);
        Assert.AreEqual("Side.Left", equivalent.Location);
        StringAssert.Contains(r.Reference, "EN 1993-2:2006 7.3(1)");
    }

    [TestMethod]
    public void SolidRectangleEquivalentMaximumInsideTheSideIsExact()
    {
        // N = -500 kN, M1 = 2 kNm, V2 = 400 kN: sigma = -50 + 0.06 y, tau = 60 (1 - y^2/10^4). f' = -6 - 4.3128 y + 4.32e-4 y^3 = 0
        // near y = -1.39 mm: the maximum is inside the side, slightly above sqrt(50^2 + 3 60^2) = 115.33 at the centroid.
        var r = Run(Input(new SectionRectangular(200, 50, "flat"), n: -5e5, m1: 2e6, v2: 4e5));
        var e = r.Value(Criterion.Equivalent);
        double best = 0;
        for (int i = 0; i <= 200000; i++)
        {
            double y = 100 - i * 1e-3, sigma = -50 + 0.06 * y, tau = 60 * (1 - y * y / 1e4);
            best = Math.Max(best, Math.Sqrt(sigma * sigma + 3 * tau * tau));
        }
        Assert.IsTrue(e.Stress >= best - 1e-9 && e.Stress <= best + 1e-6, e.Stress + " vs " + best);
        Assert.IsTrue(e.Stress > Math.Sqrt(50 * 50 + 3 * 60 * 60));
        Assert.IsTrue(e.Y < -1.3 && e.Y > -1.5, e.Y.ToString());
        Close(-50 + 0.06 * e.Y, e.Sigma); Close(60 * (1 - e.Y * e.Y / 1e4), e.Tau);
    }

    private static readonly double WA = 2 * 200 * 20 + 360 * 10, WIxx = 2 * (200 * 20.0 * 20 * 20 / 12 + 4000 * 190.0 * 190) + 10 * 360.0 * 360 * 360 / 12;
    // Thin-walled midline model of the W400: flanges at +-190, web line 380 long.
    private static readonly double WIxxShear = 2 * 200 * 20 * 190.0 * 190 + 10 * 380.0 * 380 * 380 / 12;

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void WeldedHUnderNormalForceMajorBendingAndShearByHand(bool ntc)
    {
        // W400: h 400, web 360 x 10, flanges 200 x 20, no fillets. N = -200 kN, M1 = 300 kNm, V2 = 400 kN; class 1 (web c/t 36).
        var shape = new SectionH(400, 10, 200, 20, 200, 20, "W400");
        var input = Input(shape, n: -2e5, m1: 3e8, v2: 4e5, ntc: ntc);
        Close(WA, input.Section.Area); Close(WIxx, input.Section.ReadOnlySection.Jxx);
        var gamma = ntc ? new SteelServiceStressBasis(1.05, "Project specification S-01 par. 4", "Railway bridge, project requirement") : Basis;
        var r = Run(input, gamma);
        Assert.AreEqual(Status.Passed, r.Status, r.Message); Assert.AreEqual(1, r.SectionClass); Assert.AreEqual("H", r.Family);
        double limit = 355 / gamma.GammaMSer, sigmaN = -2e5 / WA, k = 3e8 / WIxx;
        // sigma: bottom outer fibre -17.24 - 182.96 = -200.20 MPa.
        var normal = r.Value(Criterion.Normal); Close(sigmaN - k * 200, normal.Stress); Close(-200, normal.Y); Close(limit, normal.Limit);
        Assert.AreEqual(-200.198, normal.Stress, 1e-3);
        // tau at the centroid: S = 200*20*190 + 10*190^2/2 = 940500 -> 4e5 S/(I' 10) = 112.457 MPa.
        var shear = r.Value(Criterion.Shear); Close(4e5 * 940500 / (WIxxShear * 10), shear.Stress); Close(0, shear.Y, 0);
        Assert.AreEqual(112.457, shear.Stress, 1e-3); Close(sigmaN, shear.Sigma);
        // Equivalent at the bottom web face end y = -180: S = 760000 + 10 (190^2 - 180^2)/2 = 778500 -> tau 93.09, sigma -181.90 -> 243.07.
        double tauJ = 4e5 * 778500 / (WIxxShear * 10), sigmaJ = sigmaN - k * 180;
        var e = r.Value(Criterion.Equivalent); Close(Math.Sqrt(sigmaJ * sigmaJ + 3 * tauJ * tauJ), e.Stress); Close(-180, e.Y);
        Assert.AreEqual(243.07, e.Stress, 1e-2); Assert.AreEqual("Web.Left", e.Location); Close(e.Stress / limit, r.Utilization!.Value);
        StringAssert.Contains(r.Reference, ntc ? "under NTC 2018 chapter 12 (NTC 4.2.2.1 and 4.2.4.2.4" : "EN 1993-2:2006 7.3(1)");
        StringAssert.Contains(r.ClassificationSource, ntc ? "NTC 2018" : "EN 1993-1-1");
    }

    [TestMethod]
    public void RectangularHollowSectionNormalForceBiaxialBendingAndShearByHand()
    {
        // RHS 200 x 100 x 8 (sharp corners): A = 4544, Ixx = (100*200^3 - 84*184^3)/12, Iyy = (200*100^3 - 184*84^3)/12.
        // N = -100 kN, M1 = 30 kNm, M2 = 5 kNm, V2 = 100 kN. Corner (50, -100): -22.01 - 130.09 - 32.99 = -185.09 MPa.
        double a = 4544, ixx = (100 * 200.0 * 200 * 200 - 84 * 184.0 * 184 * 184) / 12, iyy = (200 * 100.0 * 100 * 100 - 184 * 84.0 * 84 * 84) / 12;
        var input = Input(new SectionRHS(200, 100, 8, 8, 8, 8, "RHS"), n: -1e5, m1: 3e7, m2: 5e6, v2: 1e5);
        Close(a, input.Section.Area); Close(ixx, input.Section.ReadOnlySection.Jxx); Close(iyy, input.Section.ReadOnlySection.Jyy);
        var r = Run(input);
        Assert.AreEqual(Status.Passed, r.Status, r.Message); Assert.AreEqual(1, r.SectionClass);
        double sigma = -1e5 / a - 3e7 * 100 / ixx - 5e6 * 50 / iyy;
        var normal = r.Value(Criterion.Normal); Close(sigma, normal.Stress); Close(50, normal.X); Close(-100, normal.Y);
        Assert.AreEqual(-185.089, normal.Stress, 1e-3); Assert.AreEqual("Corner.BottomRight", normal.Location);
        // Midline 92 x 192: I' = 2*92*8*96^2 + 2*8*192^3/12. Web centre: S = 46*8*96 + 8*96^2/2 = 72192 -> 39.23 MPa.
        double iShear = 2 * 92 * 8 * 96.0 * 96 + 2 * 8 * 192.0 * 192 * 192 / 12;
        var shear = r.Value(Criterion.Shear); Close(1e5 * 72192 / (iShear * 8), shear.Stress); Close(0, shear.Y, 0); Close(50, Math.Abs(shear.X));
        Assert.AreEqual(39.23, shear.Stress, 1e-2);
        // Equivalent at the same corner with the corner flow 1e5*46*8*96/I' over t = 8 (19.20 MPa): 188.05 MPa.
        double tauCorner = 1e5 * 46 * 8 * 96 / iShear / 8;
        var e = r.Value(Criterion.Equivalent); Close(Math.Sqrt(sigma * sigma + 3 * tauCorner * tauCorner), e.Stress); Assert.AreEqual("Corner.BottomRight", e.Location);
        Assert.AreEqual(188.05, e.Stress, 1e-2);
    }

    [TestMethod]
    public void RectangularHollowSectionWithBothShearsPeaksOffTheWebCentre()
    {
        // V1 = 20 kN, V2 = 100 kN on the same RHS. Right web (x = 46), upward flow: V2 part 153.58 + 160.26 (1 - y^2/96^2),
        // V1 part -V1 46 8 y/I'y: the sum peaks at y = -0.976*96^2/(2*160.26) = -28.1 mm, 40.94 MPa.
        double ix = 2 * 92 * 8 * 96.0 * 96 + 2 * 8 * 192.0 * 192 * 192 / 12, iy = 2 * 192 * 8 * 46.0 * 46 + 2 * 8 * 92.0 * 92 * 92 / 12;
        var r = Run(Input(new SectionRHS(200, 100, 8, 8, 8, 8, "RHS"), v1: 2e4, v2: 1e5));
        double c0 = 1e5 * 46 * 8 * 96 / ix, c2 = 1e5 * 8 * 96.0 * 96 / 2 / ix, slope = 2e4 * 46 * 8 / iy;
        double y = -slope * 96 * 96 / (2 * c2), q = c0 + c2 * (1 - y * y / (96 * 96)) - slope * y;
        var shear = r.Value(Criterion.Shear);
        // The left web peaks at the point-symmetric (-x, -y): either face of either web, the first one found governs.
        Close(q / 8, shear.Stress, 1e-9); Close(Math.Abs(y), Math.Abs(shear.Y), 1e-6); Assert.AreEqual(-Math.Sign(shear.X), Math.Sign(shear.Y));
        Assert.IsTrue(Math.Abs(shear.X) == 50 || Math.Abs(shear.X) == 42, shear.X.ToString());
        Assert.AreEqual(40.94, shear.Stress, 1e-2);
    }

    [TestMethod]
    public void CircularHollowSectionInteriorEquivalentMaximumByClosedForm()
    {
        // CHS 219.1 x 8, NTC with gammaM,ser = 1.05: N = -200 kN, Mx = 10 kNm, Vy = 300 kN. On the outer surface
        // sigma = a + m cos(alpha), tau = tau0 sin(alpha) (alpha from the top fibre), m = M R/I, tau0 = 2V/A: stationary at
        // cos(alpha) = a m/(3 tau0^2 - m^2) = -0.0377, equivalent 199.60 MPa, above sqrt(a^2 + 3 tau0^2) = 199.47 at the sides.
        double d = 219.1, t = 8, area = Math.PI * (d - t) * t, i = Math.PI * (Math.Pow(d, 4) - Math.Pow(d - 2 * t, 4)) / 64;
        var input = Input(new SectionCHS(d, t, "CHS"), n: -2e5, m1: 1e7, v2: 3e5, ntc: true);
        Close(area, input.Section.Area, 1e-6); Close(i, input.Section.ReadOnlySection.Jxx, 1e-6);
        var r = Run(input, new SteelServiceStressBasis(1.05, "Project specification S-01 par. 4", "Footbridge, project requirement"));
        Assert.AreEqual(Status.Passed, r.Status, r.Message); Assert.AreEqual("CHS", r.Family);
        double a = -2e5 / area, m = 1e7 * d / 2 / i, tau0 = 2 * 3e5 / area, c = a * m / (3 * tau0 * tau0 - m * m);
        double expected = Math.Sqrt((a + m * c) * (a + m * c) + 3 * tau0 * tau0 * (1 - c * c));
        var e = r.Value(Criterion.Equivalent); Close(expected, e.Stress, 1e-9); Assert.AreEqual(199.60, e.Stress, 1e-2);
        Assert.IsTrue(e.Stress > Math.Sqrt(a * a + 3 * tau0 * tau0));
        Close(Math.Abs(a) + m, Math.Abs(r.Value(Criterion.Normal).Stress), 1e-9); Close(tau0, r.Value(Criterion.Shear).Stress, 1e-9);
        Close(355 / 1.05, e.Limit); Close(expected * 1.05 / 355, r.Utilization!.Value);
    }

    [TestMethod]
    public void FailedLimitIsReportedWithAllThreeCriteria()
    {
        // M1 = 600 kNm: 6e8*200/I = 365.9 > 355 MPa at the flanges.
        var r = Run(Input(new SectionH(400, 10, 200, 20, 200, 20, "W400"), m1: 6e8, v2: 4e5));
        Assert.AreEqual(Status.Failed, r.Status);
        Assert.AreEqual(3, r.Values.Count); Assert.IsFalse(r.Value(Criterion.Equivalent).Passed);
        Assert.IsTrue(r.Utilization > 1); Close(r.Values.Max(v => v.Utilization), r.Utilization!.Value);
    }

    [TestMethod]
    public void ThinWalledFlowsBalanceBothShearsAndLineMaximaMatchDenseSampling()
    {
        foreach (var shape in new Section[] { new SectionH(400, 10, 200, 20, 200, 20, "W400"), new SectionRHS(200, 100, 10, 10, 6, 6, "RHS"),
            new SectionRHSRoundedCorners(200, 120, 8, 16, 8, "RHSr") })
        {
            var input = Input(shape, n: -1e5, m1: 6e7, m2: -2e7, v1: 3e4, v2: -9e4);
            var model = ServiceStressModel.Create(input.Section.ReadOnlySection, 3e4, -9e4, out _, out _)!;
            double fx = 0, fy = 0;
            foreach (var w in model.Walls) { double f = w.Flow.Integral().At(w.Length); fx += f * w.Ex; fy += f * w.Ey; }
            Close(3e4, fx, 1e-9); Close(-9e4, fy, 1e-9);
            var r = Run(input); var section = input.Section.ReadOnlySection;
            double Sigma(double x, double y) => -1e5 / section.Area + 6e7 * y / section.Jxx + 2e7 * x / section.Jyy;
            double sampled = 0, sampledTau = 0;
            foreach (var line in model.Lines)
                for (int k = 0; k <= 20000; k++)
                {
                    double u = k / 20000.0, x = line.X0 + (line.X1 - line.X0) * u, y = line.Y0 + (line.Y1 - line.Y0) * u;
                    double s = Sigma(x, y), tau = line.Tau.At(u);
                    sampled = Math.Max(sampled, Math.Sqrt(s * s + 3 * tau * tau)); sampledTau = Math.Max(sampledTau, Math.Abs(tau));
                }
            var e = r.Value(Criterion.Equivalent).Stress; var ts = r.Value(Criterion.Shear).Stress;
            Assert.IsTrue(e >= sampled - 1e-9 && e <= sampled * (1 + 1e-7), shape.Name + ": " + e + " vs " + sampled);
            Assert.IsTrue(ts >= sampledTau - 1e-9 && ts <= sampledTau * (1 + 1e-7), shape.Name + ": " + ts + " vs " + sampledTau);
        }
    }

    [TestMethod]
    public void CircleMaximumMatchesDenseSamplingForGeneralActions()
    {
        double d = 323.9, t = 10;
        var input = Input(new SectionCHS(d, t, "CHS"), n: 3e5, m1: 4e7, m2: -3e7, v1: 2e5, v2: 1.5e5);
        var section = input.Section.ReadOnlySection; double rm = (d - t) / 2, r = d / 2;
        double best = 0;
        for (int k = 0; k < 360000; k++)
        {
            double th = 2 * Math.PI * k / 360000, x = r * Math.Cos(th), y = r * Math.Sin(th);
            double s = 3e5 / section.Area + 4e7 * y / section.Jxx + 3e7 * x / section.Jyy, tau = (1.5e5 * Math.Cos(th) - 2e5 * Math.Sin(th)) / (Math.PI * rm * t);
            best = Math.Max(best, Math.Sqrt(s * s + 3 * tau * tau));
        }
        var e = Run(input).Value(Criterion.Equivalent).Stress;
        Assert.IsTrue(e >= best - 1e-9 && e <= best * (1 + 1e-8), e + " vs " + best);
    }

    [TestMethod]
    public void Class4SectionUsesTheEffectiveSectionBoundAndCombinesMaxima()
    {
        // Welded plate girder, web 1000 x 6 under compression and bending: class 4.
        var shape = new SectionH(1040, 6, 300, 20, 300, 20, "PG");
        var input = Input(shape, n: -1e6, m1: 5e8, v2: 1e5);
        var r = Run(input);
        Assert.AreEqual(Status.Passed, r.Status, r.Message); Assert.AreEqual(4, r.SectionClass); Assert.AreEqual("EffectiveClass4Bound", r.StressModel);
        var effective = SteelEffectiveSectionSolver.Solve(new SteelVerificationInput(input.Section, new SteelActions(-1e6, 0, 0, 0, 5e8, 0), input.Code));
        var normal = r.Value(Criterion.Normal); Close(effective.MaximumAbsoluteStress, Math.Abs(normal.Stress)); Assert.IsTrue(normal.IsBound);
        var e = r.Value(Criterion.Equivalent); var shear = r.Value(Criterion.Shear);
        Assert.IsTrue(e.IsBound); Close(Math.Sqrt(normal.Stress * normal.Stress + 3 * shear.Stress * shear.Stress), e.Stress);
        // The gross elastic sigma would be smaller: the bound is conservative.
        double gross = 1e6 / input.Section.Area + 5e8 * 520 / input.Section.ReadOnlySection.Jxx;
        Assert.IsTrue(Math.Abs(normal.Stress) > gross);
    }

    [TestMethod]
    public void TensionOnlyNeedsNoClassificationAndSmallTorsionIsNeglected()
    {
        // Slender web that would be class 4 in compression: no compressed fibre, gross elastic stresses.
        var r = Run(Input(new SectionH(1040, 6, 300, 20, 300, 20, "PG"), n: 1e6, t: 1));
        Assert.AreEqual(Status.Passed, r.Status, r.Message); Assert.IsNull(r.SectionClass);
        StringAssert.Contains(r.ClassificationSource, "No compressed fibre");
        Assert.IsTrue(r.Trace.Any(v => v.Name == "Neglected.T"));
    }

    [DataTestMethod]
    [DataRow("basis", "ServiceStress.BasisRequired", Status.MissingInput)]
    [DataRow("torsion", "ServiceStress.Torsion.NotIntegrated", Status.NotSupported)]
    [DataRow("tee", "ServiceStress.Geometry.NotQualified", Status.NotSupported)]
    [DataRow("channel", "ServiceStress.Geometry.NotQualified", Status.NotSupported)]
    [DataRow("monosymmetric", "ServiceStress.Geometry.NotDoublySymmetric", Status.NotSupported)]
    [DataRow("coldformed", "ServiceStress.ColdFormedOpenSection.NotQualified", Status.NotSupported)]
    [DataRow("wide", "ServiceStress.SolidShear.NotQualified", Status.NotSupported)]
    [DataRow("biaxial", "ServiceStress.SolidShear.NotQualified", Status.NotSupported)]
    [DataRow("holes", "ServiceStress.NetSection.DeclarationRequired", Status.MissingInput)]
    [DataRow("perforated", "ServiceStress.PerforatedSection.NotQualified", Status.NotSupported)]
    [DataRow("chs4", "ServiceStress.Class4.NotQualified", Status.NotSupported)]
    [DataRow("zero", "ServiceStress.NoActions", Status.NotRequired)]
    public void UnqualifiedStatesAreRefusedWithAPreciseCode(string kind, string code, Status status)
    {
        var h = new SectionH(400, 10, 200, 20, 200, 20, "W400");
        var input = kind switch
        {
            "torsion" => Input(h, m1: 1e8, t: 1e6),
            "tee" => Input(new SectionT(300, 200, 10, 15, "T"), m1: 1e7),
            "channel" => Input(new SectionC(300, 10, 100, 15, 100, 15, "C"), m1: 1e7),
            "monosymmetric" => Input(new SectionH(400, 10, 300, 20, 200, 20, "Mono"), m1: 1e8),
            "coldformed" => Input(h, m1: 1e8, formed: Section.FormedTypes.ColdFormed),
            "wide" => Input(new SectionRectangular(200, 120, "block"), v2: 1e4),
            "biaxial" => Input(new SectionRectangular(200, 50, "flat"), v1: 1e3, v2: 1e4),
            "holes" => Input(h, m1: 1e8, unperforated: null),
            "perforated" => Input(h, m1: 1e8, unperforated: false),
            "chs4" => Input(new SectionCHS(1000, 4, "thin CHS"), n: -1e6),
            "zero" => Input(h),
            _ => Input(h, m1: 1e8)
        };
        var r = SteelServiceStressCheck.Evaluate(input, kind == "basis" ? null : Basis);
        Assert.AreEqual(status, r.Status, r.DiagnosticCode + " " + r.Message);
        Assert.AreEqual(code, r.DiagnosticCode, r.Message);
        Assert.IsFalse(r.IsEvaluated); Assert.AreEqual(0, r.Values.Count); Assert.IsNull(r.Utilization);
        Assert.IsTrue(r.Trace.All(v => double.IsFinite(v.Value)));
    }

    [TestMethod]
    public void BasisRequiresAFactorOfAtLeastOneAndBothSources()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new SteelServiceStressBasis(.95, "NA", "bridge"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new SteelServiceStressBasis(double.NaN, "NA", "bridge"));
        Assert.ThrowsException<ArgumentException>(() => new SteelServiceStressBasis(1, " ", "bridge"));
        Assert.ThrowsException<ArgumentException>(() => new SteelServiceStressBasis(1, "NA", null!));
        var input = Input(new SectionH(400, 10, 200, 20, 200, 20, "W400"), m1: 1e8);
        var other = Input(new SectionH(400, 10, 200, 20, 200, 20, "W400"), m1: 1e8);
        Assert.ThrowsException<ArgumentException>(() => SteelServiceStressCheck.Evaluate(input, Basis, new SteelSectionClassification(other, 1, "elsewhere")));
    }
}
