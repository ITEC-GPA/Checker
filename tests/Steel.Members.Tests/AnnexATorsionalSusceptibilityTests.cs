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
using SC = GPC.Checkers.Steel.Checkers.EN1993p11Checker.SectionClass;

namespace Steel.Members.Tests;

[TestClass]
public class AnnexATorsionalSusceptibilityTests
{
    public TestContext TestContext { get; set; } = null!;

    [DataTestMethod]
    [DataRow(250d, 260d, 12.5d, 7.5d, 0d)]
    [DataRow(250d, 260d, 12.5d, 7.5d, 24d)]
    [DataRow(300d, 150d, 10.7d, 7.1d, 15d)]
    public void DoubleSymmetryDecouplesTheTorsionalModeDespiteCoordinateRoundoff(
        double h, double b, double tf, double tw, double radius)
    {
        var s = new SteelSection(new SectionH(h, tw, b, tf, b, tf, "symmetric", radius),
            new SteelMaterial("S235", 210000, 235, 360),
            Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished);
        Assert.IsTrue(s.IsDoubleSymmetric);
        double dx = s.ShearCenter.X - s.Centroid.X, dy = s.ShearCenter.Y - s.Centroid.Y;
        TestContext.WriteLine($"Centroid/shear-centre difference: dx={dx:R}; dy={dy:R} mm");
        Assert.IsTrue(Math.Abs(dx) < 1e-10 && Math.Abs(dy) < 1e-10);
        double ny = Euler(s.J11), nz = Euler(s.J22);
        // Decoupled torsional eigenvalue; not the minimum of the two
        // separately checked flexural modes. Caffe A5.5 pp.239-240.
        double nt = (210000 / 2.6 * s.Jt + Euler(s.Jw)) * s.Area / (s.J11 + s.J22);
        var p = new ReferenceProbe(s, 4000);
        ReferenceMath.Close(nt, p.Torsion(), "torsion");
        ReferenceMath.Close(nt, p.FlexuralTorsion(ny, nz, nt), "double-symmetric TF limit");
    }

    [DataTestMethod]
    [DataRow(400000d, -1)] [DataRow(400000d, 0)] [DataRow(400000d, 1)]
    [DataRow(800000d, -1)] [DataRow(800000d, 0)] [DataRow(800000d, 1)]
    public void AnnexAThresholdUsesMinorAxisEulerAndTorsionalMode(double n, int position)
    {
        // Declared IPE500 reference inputs; different axial and Mcr spans
        // deliberately put lambda0 on either side of Table A.1's threshold.
        var s = new ReferenceSection(new SectionH(500, 10.2, 200, 16, 200, 16, "IPE500 reference", 21))
        {
            Area = 11550, J11 = 48200e4, J22 = 2142e4, Jt = 89.29e4, Jw = 1249000e6,
            Wpl1 = 2194e3, Wpl2 = 335.9e3, Wel1 = 1928e3, Wel2 = 214.2e3
        };
        double ny = Euler(s.J11), nz = Euler(s.J22);
        double nt = (210000 / 2.6 * s.Jt + Euler(s.Jw)) * s.Area / (s.J11 + s.J22);
        const double kc = 1 / 1.33;
        // EC3 Table A.1 p77; doubly symmetric section: Ncr,TF=Ncr,T.
        double limit = .2 / kc * Math.Pow((1 - n / nz) * (1 - n / nt), .25);
        double oldLimit = .2 / kc * Math.Sqrt(1 - n / nt);
        Assert.IsTrue(oldLimit > limit);
        double target = position < 0 ? limit * .99 : position > 0 ? oldLimit * 1.01 : (limit + oldLimit) / 2;
        // Find the critical-moment span from the independent Mcr eigenvalue.
        double lo = 10, hi = 4000;
        for (int i = 0; i < 90; i++)
        {
            double mid = (lo + hi) / 2;
            double lambda = Math.Sqrt(s.Wpl1 * 275 / ReferenceMath.Mcr(mid, s.J22, s.Jt, s.Jw, 1, 0, 0));
            if (lambda < target) lo = mid; else hi = mid;
        }
        var p = new Probe(s, (lo + hi) / 2);
        double cm0 = .79 - .1188 * n / ny, expected = cm0;
        if (position >= 0)
        {
            double q = Math.Sqrt(66.67e6 / n * s.Area / s.Wel1) * (1 - s.Jt / s.J11);
            expected = (cm0 + q) / (1 + q);
        }
        ReferenceMath.Close(expected, p.Cm1(n), $"Cm1 at threshold position {position}");
        var report = p.LtbReport(n);
        ReferenceMath.Close(position < 0 ? .001 : 66.67e6 / report.Capacity,
            report.Ratio, "public check uses the same susceptibility threshold");
    }

    private static double Euler(double inertia) => Math.PI * Math.PI * 210000 * inertia / (4000 * 4000);

    private sealed class Probe : EN1993p11Checker
    {
        private readonly ISteelSection section;
        internal Probe(ReferenceSection s, double mcrLength)
            : base(new BeamElement(Point3d.Origin, new Point3d(4000, 0, 0), s),
                new Opt(Opt.LoadConditions.NotDirectlyLoaded, Opt.SupportConditions.HingesAtEnds,
                    Opt.LateralSupportConditions.HingesAtEnds, Opt.LateralWarpingConditions.HingesAtEnds, 0, 0,
                    effectiveLengthFactorCriticalMoment1: mcrLength / 4000,
                    loadApplicationPoint: Opt.LoadApplicationPoints.ShearCenter), new StandardEN1993p11())
        { section = s; }

        internal double Cm1(double n)
        {
            CalculateCmFactorsForInteraction(section, SC.Class1,
                new ResultBeamForces(-n, 0, 0, 0, 66.67e6, 20e6, CoordinateSystem.Global),
                Opt.LoadConditions.NotDirectlyLoaded, Opt.SupportConditions.HingesAtEnds, 0,
                out _, out double cm1, out _, out _, out _, out _, out _, out _, out _,
                loadApplicationPoint: Opt.LoadApplicationPoints.ShearCenter);
            return cm1;
        }

        internal (double Ratio, double Capacity) LtbReport(double n)
        {
            BeamElement.Results.Add(new BeamResult(new[] { new StationResultBeamForces(
                new LoadCase("Threshold", LoadCase.LoadCaseTypes.SelfWeight),
                new ResultBeamForces(-n, 0, 0, 0, 66.67e6, 20e6, CoordinateSystem.Global), 0) }));
            PerformCheck();
            Assert.AreEqual(0, GetErrorLog().Count, string.Join("; ", GetErrorLog()));
            var result = EN1993p11BeamStationResults.Single();
            return (result.LateralTorsionalBucklingWorkingRatio, result.LateralTosionalBucklingCapacity);
        }
    }
}
