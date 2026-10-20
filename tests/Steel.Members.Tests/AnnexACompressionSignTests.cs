using GPC.Checkers.Steel.Checkers;
using GPC.Geometry;
using GPC.Model;
using GPC.Model.Elements;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Opt = GPC.Checkers.Steel.Checkers.EN1993p11Checker.EN1993p11Options;
using SC = GPC.Checkers.Steel.Checkers.EN1993p11Checker.SectionClass;

namespace Steel.Members.Tests;

// EN 1993-1-1:2005, Tables A.1/A.2, printed pp.76-78.
// Caffe, Acciaio (2012, reprint 2013), A5.5, printed p.243:
// IPE500, NEd=400 kN, mu_y=1.0, mu_z=.93.
// Only these auxiliary terms are compared with that example. Its two
// different moment diagrams are NOT represented by the simplified loads below.
[TestClass]
public class AnnexACompressionSignTests
{
    private static ReferenceSection Ipe500() => new(
        new SectionH(500, 10.2, 200, 16, 200, 16, "IPE500 reference", 21))
    {
        // Use the stability substitution data, pp.239-245, explicitly:
        // the earlier section table instead prints A=11600 and Wel,y=1930000.
        Area = 11550, J11 = 48200e4, J22 = 2142e4,
        Jt = 89.29e4, Jw = 1249000e6,
        Wpl1 = 2194e3, Wpl2 = 335.9e3, Wel1 = 1928e3, Wel2 = 214.2e3
    };

    [DataTestMethod]
    [DataRow(1, 1d, .05)]
    [DataRow(2, .93, .005)]
    public void CaffePublishedMuThroughSignedCompressionPath(int axis, double printed, double halfUnit)
    {
        var s = Ipe500();
        var p = new Probe(s, 4000);
        var result = p.Factors(-400000, Opt.LoadConditions.Constant, 1);
        double actual = axis == 1 ? result.Mu1 : result.Mu2;
        // Editorial comparison: half the last printed decimal unit.
        Assert.AreEqual(printed, actual, halfUnit, "Caffe A5.5 p.243");
        // Tight comparison uses only declared reference inputs, not GPC outputs.
        ReferenceMath.Close(ExpectedMu(s, 4000, 400000, axis), actual, "mu");
        Assert.IsTrue(actual <= 1, "Positive subcritical compression and chi<=1 require mu<=1.");
    }

    public static IEnumerable<object[]> MomentCases
    {
        get
        {
            foreach (double length in new[] { 200d, 4000d })
            foreach (double compression in new[] { 0d, 400000d })
            {
                yield return new object[] { length, compression, Opt.LoadConditions.Constant, 1d };
                yield return new object[] { length, compression, Opt.LoadConditions.SingleForce, 1d };
                foreach (double psi in new[] { -1d, 0d, .5d, 1d })
                    yield return new object[] { length, compression, Opt.LoadConditions.NotDirectlyLoaded, psi };
            }
        }
    }

    [DataTestMethod, DynamicData(nameof(MomentCases))]
    public void NormativeMomentFactorsUseCompressionMagnitude(
        double length, double compression, Opt.LoadConditions load, double psi)
    {
        var s = Ipe500();
        var actual = new Probe(s, length).Factors(-compression, load, psi);
        double ny = Euler(s.J11, length), nz = Euler(s.J22, length);
        double cm1 = ExpectedCm0(load, psi, compression / ny);
        double cm2 = ExpectedCm0(load, psi, compression / nz);

        // These lengths deliberately exercise both sides of Table A.1's
        // torsional-susceptibility branch. Establish the side independently.
        double nt = (210000 / 2.6 * s.Jt + Euler(s.Jw, length))
            / ((s.J11 + s.J22) / s.Area);
        double lambda0 = Math.Sqrt(s.Wpl1 * 275 /
            ReferenceMath.Mcr(length, s.J22, s.Jt, s.Jw, 1, 0, 0));
        double kc = load == Opt.LoadConditions.Constant ? .94
            : load == Opt.LoadConditions.SingleForce ? .86 : 1 / (1.33 - .33 * psi);
        double limit = .2 / kc * Math.Pow((1 - compression / nz) * (1 - compression / nt), .25);
        Assert.AreEqual(length == 4000, lambda0 > limit, "Independent branch selection");
        if (lambda0 > limit)
        {
            double alpha = 1 - s.Jt / s.J11;
            if (compression == 0) cm1 = 1; // limit for nonzero major-axis moment
            else
            {
                double q = Math.Sqrt(66.67e6 / compression * s.Area / s.Wel1) * alpha;
                cm1 = (cm1 + q) / (1 + q);
            }
        }
        ReferenceMath.Close(cm1, actual.Cm1, "Cm major");
        ReferenceMath.Close(cm2, actual.Cm2, "Cm minor");
        ReferenceMath.Close(ExpectedMu(s, length, compression, 1), actual.Mu1, "mu major");
        ReferenceMath.Close(ExpectedMu(s, length, compression, 2), actual.Mu2, "mu minor");
    }

    private static double Euler(double inertia, double length) =>
        Math.PI * Math.PI * 210000 * inertia / (length * length);

    private static double ExpectedMu(ReferenceSection s, double length, double n, int axis)
    {
        double nc = Euler(axis == 1 ? s.J11 : s.J22, length);
        double chi = ReferenceMath.Reduction(Math.Sqrt(s.Area * 275 / nc), axis == 1 ? .21 : .34);
        double q = n / nc;
        return 1 - (1 - chi) * q / (1 - chi * q);
    }

    private static double ExpectedCm0(Opt.LoadConditions load, double psi, double q) =>
        load == Opt.LoadConditions.Constant ? 1 + .03 * q :
        load == Opt.LoadConditions.SingleForce ? 1 - .18 * q :
        .79 + .21 * psi + .36 * (psi - .33) * q;

    private sealed class Probe : EN1993p11Checker
    {
        private readonly ISteelSection section;
        internal Probe(ReferenceSection s, double length)
            : base(new BeamElement(Point3d.Origin, new Point3d(length, 0, 0), s),
                new Opt(Opt.LoadConditions.Constant, Opt.SupportConditions.HingesAtEnds,
                    Opt.LateralSupportConditions.HingesAtEnds, Opt.LateralWarpingConditions.HingesAtEnds,
                    1, 1), new StandardEN1993p11 { GammaM0 = 1.05, GammaM1 = 1.05 })
        { section = s; }

        internal (double Cm1, double Cm2, double Mu1, double Mu2) Factors(
            double signedN, Opt.LoadConditions load, double psi)
        {
            var forces = new ResultBeamForces(signedN, 0, 0, 0, 66.67e6, 20e6, CoordinateSystem.Global);
            CalculateCmFactorsForInteraction(section, SC.Class1, forces,
                load, Opt.SupportConditions.HingesAtEnds, psi,
                out _, out double cm1, out double cm2, out _, out double mu1,
                out double mu2, out _, out _, out _,
                loadApplicationPoint: Opt.LoadApplicationPoints.ShearCenter);
            return (cm1, cm2, mu1, mu2);
        }
    }
}
