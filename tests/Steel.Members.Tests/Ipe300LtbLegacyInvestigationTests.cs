using GPC.Checkers.Steel.Checkers;
using GPC.Geometry;
using GPC.Model;
using GPC.Model.Elements;
using GPC.Model.LoadCases;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Storage;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Opt = GPC.Checkers.Steel.Checkers.EN1993p11Checker.EN1993p11Options;

namespace Steel.Members.Tests;

// Independent arithmetic for the declared legacy inputs, NOT qualification of
// Cordova p.190 (not available). See tools/Steel.LegacyDiagnostics/H_LTB_STUDY.txt.
[TestClass]
public class Ipe300LtbLegacyInvestigationTests
{
    public TestContext TestContext { get; set; } = null!;

    [DataTestMethod]
    [DataRow(1, 355d, true)]
    [DataRow(2, 355d, true)]
    [DataRow(1, 275d, true)]
    [DataRow(2, 275d, true)]
    [DataRow(1, 355d, false)]
    [DataRow(2, 355d, false)]
    [DataRow(1, 275d, false)]
    [DataRow(2, 275d, false)]
    public void DeclaredInputsMatchIndependentSectionAndLtbArithmetic(int legacyCase, double fy, bool top)
    {
        var (checker, section) = Create(legacyCase, fy, top);
        checker.PerformCheck();
        Assert.AreEqual(0, checker.GetErrorLog().Count);
        var actual = checker.EN1993p11BeamStationResults.Single();
        var expected = Independent(section, legacyCase, fy, top, modified: true);
        Assert.AreEqual(EN1993p11Checker.SectionClass.Class1, actual.BendingCompressionClass);
        Close(expected.Mc, actual.BendingMoment1Capacity, "Mc");
        Close(expected.Mcr, actual.MCr, "Mcr");
        Close(expected.Lambda, actual.LambdaSignedLTBuckling, "lambdaLT");
        Close(expected.Mb, actual.LateralTosionalBucklingCapacity, "Mb modified");
        Close(legacyCase == 1 ? 5000 : 4000, checker.GetLengthCriticalMoment1(), "Mcr length");
        // The general method is a diagnostic alternative, never a changed production default.
        var general = Independent(section, legacyCase, fy, top, modified: false);
        Close(general.Mb, checker.GeneralCapacity(section), "Mb general");
        TestContext.WriteLine(
            $"case={legacyCase}; fy={fy}; top={top}; Wpl={expected.Mc / fy:R}; " +
            $"Mc={expected.Mc:R}; Mcr={expected.Mcr:R}; lambda={expected.Lambda:R}; " +
            $"PhiMod={expected.Phi:R}; chiMod={expected.Chi:R}; f={expected.F:R}; " +
            $"MbMod={expected.Mb:R}; MbGeneral={general.Mb:R}");
    }

    [DataTestMethod]
    [DataRow(1, 355d)]
    [DataRow(2, 355d)]
    [DataRow(1, 275d)]
    [DataRow(2, 275d)]
    public void ReportDescribesTheMethodUsedForLtbCapacity(int legacyCase, double fy)
    {
        var (checker, section) = Create(legacyCase, fy, top: true);
        checker.PerformCheck();
        Assert.AreEqual(0, checker.GetErrorLog().Count);
        var actual = checker.EN1993p11BeamStationResults.Single();
        var expected = Independent(section, legacyCase, fy, top: true, modified: true);
        Assert.AreEqual(Opt.LateralTorsionalBuckingCurves.b, actual.LateraltorsionalBucklingCurve);
        Close(.34, actual.AlphaLTBuckling, "reported alpha");
        Close(expected.Phi, actual.PhiLTBuckling, "reported Phi");
        Close(expected.Chi, actual.ChiLTBuckling, "reported modified chi");
        Close(actual.LateralTosionalBucklingCapacity,
            actual.ChiLTBuckling * expected.Mc, "report reconstructs Mb with gammaM1=1");
    }

    [TestMethod]
    public void HalvingTheMcrSpanIsNotOnlyHalvingTheLateralBucklingLength()
    {
        var (full, section) = Create(2, 355, top: true);
        full.EN1993P11Options.EffectiveLengthFactorCriticalMoment1 = 1;
        full.PerformCheck();
        var (half, _) = Create(2, 355, top: true);
        half.PerformCheck();
        Assert.AreEqual(0, full.GetErrorLog().Count);
        Assert.AreEqual(0, half.GetErrorLog().Count);
        Close(4000, full.GetLengthLatTorsBuckling(), "torsional length unchanged");
        Close(8000, full.GetLengthCriticalMoment1(), "full Mcr span");
        Close(4000, half.GetLengthCriticalMoment1(), "half Mcr span");
        double expectedFull = CriticalMoment(section, 8000, 1.348, .630, 150);
        Close(expectedFull, full.EN1993p11BeamStationResults.Single().MCr, "full-span Mcr");
        Assert.IsTrue(half.EN1993p11BeamStationResults.Single().MCr > expectedFull);
    }

    [DataTestMethod]
    [DataRow(200d, true, Opt.LateralTorsionalBuckingCurves.b, .34)]
    [DataRow(100d, true, Opt.LateralTorsionalBuckingCurves.c, .49)]
    [DataRow(200d, false, Opt.LateralTorsionalBuckingCurves.c, .49)]
    [DataRow(100d, false, Opt.LateralTorsionalBuckingCurves.d, .76)]
    public void ReportCurveAndReductionReconstructOtherISectionCapacities(
        double width, bool rolled, Opt.LateralTorsionalBuckingCurves curve, double alpha)
    {
        var (checker, section) = Create(1, 275, true, width, rolled);
        checker.EN1993P11.GammaM1 = 1.1;
        checker.PerformCheck();
        Assert.AreEqual(0, checker.GetErrorLog().Count);
        var actual = checker.EN1993p11BeamStationResults.Single();
        Assert.IsTrue(actual.BendingCompressionClass is
            EN1993p11Checker.SectionClass.Class1 or EN1993p11Checker.SectionClass.Class2);
        Assert.AreEqual(curve, actual.LateraltorsionalBucklingCurve);
        Close(alpha, actual.AlphaLTBuckling, "curve imperfection");
        Close(actual.LateralTosionalBucklingCapacity,
            actual.ChiLTBuckling * section.Wpl1 * 275 / 1.1, "report reconstructs capacity");
    }

    private static (LtbProbe Checker, SteelSection Section) Create(
        int legacyCase, double fy, bool top, double width = 150, bool rolled = true)
    {
        var section = new SteelSection(new SectionH(300, 7.1, width, 10.7, width, 10.7, "LTB diagnostic", rolled ? 15 : 0),
            new SteelMaterial("Diagnostic fy", 210000, fy, 490),
            rolled ? Section.SectionTypes.Rolled : Section.SectionTypes.Welded, Section.FormedTypes.HotFinished);
        double length = legacyCase == 1 ? 5000 : 8000;
        double moment = legacyCase == 1 ? 64.4e6 : 66e6;
        var forces = new ResultBeamForces(0, 0, 0, 0, moment, 0, CoordinateSystem.Global);
        var beam = new BeamElement(Point3d.Origin, new Point3d(length, 0, 0), section);
        beam.Results.Add(new BeamResult(new[] { new StationResultBeamForces(
            new LoadCase("Legacy LTB diagnostic", LoadCase.LoadCaseTypes.SelfWeight), forces, 0) }));
        var options = new Opt(legacyCase == 1 ? Opt.LoadConditions.Constant : Opt.LoadConditions.SingleForce,
            Opt.SupportConditions.HingesAtEnds, Opt.LateralSupportConditions.HingesAtEnds,
            Opt.LateralWarpingConditions.HingesAtEnds, legacyCase == 1 ? null : 1, 1,
            effectiveLengthFactorAxialBuck1: legacyCase == 1 ? 2 : 1,
            effectiveLengthFactorLatTorsBuck: legacyCase == 1 ? 1 : .5,
            effectiveLengthFactorCriticalMoment1: legacyCase == 1 ? 1 : .5,
            effectiveLengthFactorCriticalMoment2: legacyCase == 1 ? 1 : .5,
            loadApplicationPoint: top ? Opt.LoadApplicationPoints.TopSection : Opt.LoadApplicationPoints.ShearCenter);
        return (new LtbProbe(beam, options, forces), section);
    }

    private record Arithmetic(double Mc, double Mcr, double Lambda, double Phi, double Chi, double F)
    {
        public double Mb => Mc * Chi; // gammaM1=1 in these comparisons.
    }

    private static Arithmetic Independent(SteelSection section, int legacyCase, double fy, bool top, bool modified)
    {
        // Integrate rectangles and four square-minus-quarter-circle fillets.
        const double h = 300, b = 150, tf = 10.7, tw = 7.1, r = 15;
        double hw = h - 2 * tf;
        double af = (1 - Math.PI / 4) * r * r;
        double qf = (5d / 6 - Math.PI / 4) * r * r * r;
        double jf = (1 - 5 * Math.PI / 16) * Math.Pow(r, 4);
        double area = 2 * b * tf + tw * hw + 4 * af;
        double iz = 2 * tf * Math.Pow(b, 3) / 12 + hw * Math.Pow(tw, 3) / 12
            + 4 * (tw * tw / 4 * af + tw * qf + jf);
        double wpl = b * tf * (h - tf) + tw * hw * hw / 4 + 4 * ((h / 2 - tf) * af - qf);
        Close(area, section.Area, "area integration");
        Close(iz, section.J22, "weak-axis integration");
        // Model approximates each quarter circle with 64 chords. The four missing
        // circular-segment areas times the maximum lever arm bound the Wpl error.
        // Keep this geometric bound separate from the tight Checker arithmetic check.
        double areaExcess = 4 * r * r / 2 * (Math.PI / 2 - 64 * Math.Sin(Math.PI / 128));
        Assert.AreEqual(wpl, section.Wpl1, areaExcess * h / 2, "64-chord plastic modulus bound");
        wpl = section.Wpl1; // Explicit Model input to the subsequent Checker-only arithmetic.
        double mcr = CriticalMoment(section, legacyCase == 1 ? 5000 : 4000,
            legacyCase == 1 ? 1.127 : 1.348, legacyCase == 1 ? .454 : .630, top ? 150 : 0);
        double lambda = Math.Sqrt(wpl * fy / mcr);
        double beta = modified ? .75 : 1, alpha = modified ? .34 : .21, lambda0 = modified ? .4 : .2;
        double phi = (1 + alpha * (lambda - lambda0) + beta * lambda * lambda) / 2;
        // Solve the characteristic polynomial, avoiding the production radical formula.
        double lo = 0, hi = Math.Min(1, 1 / (lambda * lambda));
        for (int i = 0; i < 100; i++)
        {
            double x = (lo + hi) / 2;
            if (beta * lambda * lambda * x * x - 2 * phi * x + 1 > 0) lo = x;
            else hi = x;
        }
        double kc = legacyCase == 1 ? .94 : .86;
        double f = modified ? Math.Min(1, 1 - .5 * (1 - kc) * (1 - 2 * Math.Pow(lambda - .8, 2))) : 1;
        double chi = Math.Min(Math.Min(1, 1 / (lambda * lambda)), (lo + hi) / (2 * f));
        return new(wpl * fy, mcr, lambda, phi, chi, f);
    }

    private static double CriticalMoment(SteelSection section, double length, double c1, double c2, double zg)
    {
        // NCCI SN003b eq.(1), k=kw=1. Jt and Jw are explicit Model inputs:
        // this verifies their use by Checker, not the accuracy of Model's torsion approximation.
        double ncr = Math.PI * Math.PI * 210000 * section.J22 / (length * length);
        double q = section.Jw / section.J22 + (210000 / 2.6) * section.Jt / ncr;
        double d = c2 * zg;
        // Rationalized positive root of t^2 + 2*d*t - q = 0, Mcr=C1*Ncr*t.
        return c1 * ncr * q / (Math.Sqrt(q + d * d) + d);
    }

    private static void Close(double expected, double actual, string label) =>
        Assert.AreEqual(expected, actual, Math.Max(1, Math.Abs(expected)) * 1e-11, label);

    private sealed class LtbProbe : EN1993p11Checker
    {
        private readonly ResultBeamForces forces;
        public LtbProbe(BeamElement beam, Opt options, ResultBeamForces forces)
            : base(beam, options, new StandardEN1993p11()) => this.forces = forces;
        public double GeneralCapacity(SteelSection section) => CalculateMbRd(section, SectionClass.Class1, forces,
            EN1993P11Options.SupportCondition, EN1993P11Options.LoadCondition,
            EN1993P11Options.LateralSupportCondition, EN1993P11Options.LateralWarpingCondition,
            EN1993P11Options.Psi1Axis, EN1993P11Options.LoadApplicationPoint);
    }
}
