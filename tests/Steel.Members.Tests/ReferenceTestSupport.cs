using GPC.Checkers.Steel.Checkers;
using GPC.Geometry;
using GPC.Model;
using GPC.Model.Elements;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Opt = GPC.Checkers.Steel.Checkers.EN1993p11Checker.EN1993p11Options;
using SC = GPC.Checkers.Steel.Checkers.EN1993p11Checker.SectionClass;

namespace Steel.Members.Tests;

// Test-only adapter. Published section properties are INPUTS to Checker,
// deliberately independent of Model's integration/torsion approximations.
internal sealed class ReferenceSection : SteelSection, ISteelSection
{
    public new double Area { get; init; }
    public new double J11 { get; init; }
    public new double J22 { get; init; }
    public new double Jt { get; init; }
    public new double Jw { get; init; }
    public new double Wpl1 { get; init; }
    public new double Wpl2 { get; init; }
    public new double Wel1 { get; init; }
    public new double Wel2 { get; init; }
    public new double Wel1Min => Wel1;
    public new double Wel2Min => Wel2;
    public double? PublishedRadius1 { get; init; }
    public double? PublishedRadius2 { get; init; }
    public new double R11 => PublishedRadius1 ?? Math.Sqrt(J11 / Area);
    public new double R22 => PublishedRadius2 ?? Math.Sqrt(J22 / Area);
    public new Point2d Centroid => Point2d.Origin;
    public Point2d Offset { get; init; } = Point2d.Origin;
    public new Point2d ShearCenter => Offset;

    public ReferenceSection(Section shape, double fy = 275,
        Section.SectionTypes type = Section.SectionTypes.Rolled)
        : base(shape, new SteelMaterial("Reference input", 210000, fy, 550),
            type, Section.FormedTypes.HotFinished) { }
}

internal sealed class ReferenceProbe : EN1993p11Checker
{
    private readonly ISteelSection section;
    public ReferenceProbe(SteelSection s, double length = 6000, double gamma = 1.05,
        double weakLength = 0, double torsionalLength = 0)
        : base(new BeamElement(Point3d.Origin, new Point3d(length, 0, 0), s),
            new Opt(Opt.LoadConditions.Constant, Opt.SupportConditions.HingesAtEnds,
                Opt.LateralSupportConditions.HingesAtEnds, Opt.LateralWarpingConditions.HingesAtEnds,
                1, 1, effectiveLengthFactorAxialBuck2: weakLength == 0 ? 1 : weakLength / length,
                effectiveLengthFactorLatTorsBuck: torsionalLength == 0 ? 1 : torsionalLength / length),
            new StandardEN1993p11 { GammaM0 = gamma, GammaM1 = gamma })
    { section = s; }

    internal static ResultBeamForces Forces(double n = 0, double m1 = 1e6, double m2 = 0)
        => new(n, 0, 0, 0, m1, m2, CoordinateSystem.Global);
    public double Euler(double length, double inertia) => GetNcrEuler(length, inertia);
    public double Torsion() => GetNcrT(section);
    public double FlexuralTorsion(double ny, double nz, double nt) => GetNcrTF(section, ny, nz, nt);
    public double Axial(int cl = 1) => CalculateAxialBucklingCapacity1Axis(0, (SC)cl, section);
    public double Moment(int axis, int cl = 1) => axis == 1
        ? CalculateMcRd1((SC)cl, section) : CalculateMcRd2((SC)cl, section);
    public double Shear(int axis) => axis == 1 ? CalculateVcRd1(section) : CalculateVcRd2(section);
    public double Nb(int axis, int cl = 1) => axis == 1
        ? CalculateNbRd1(section, (SC)cl) : CalculateNbRd2(section, (SC)cl);
    public string Curve(int axis) => (axis == 1
        ? GetBucklingCurve1Axis(section) : GetBucklingCurve2Axis(section)).ToString();
    public int Class(double n, double m1) => (int)(n == 0
        ? CalculateSectionClassDueToBending(Forces(m1: m1), section)
        : m1 == 0 ? CalculateSectionClassDueToCompression(Forces(n, 0), section)
        : CalculateSectionClassDueToCombinedBendingAndCompression(Forces(n, m1), section));
    public double Mcr(double length, double c1, double c2, bool top) =>
        CalculateMcr(section, Forces(), length, Opt.SupportConditions.HingesAtEnds,
            Opt.LoadConditions.Constant, Opt.LateralSupportConditions.HingesAtEnds,
            Opt.LateralWarpingConditions.HingesAtEnds, 1,
            top ? Opt.LoadApplicationPoints.TopSection : Opt.LoadApplicationPoints.ShearCenter, c1, c2);
    public double AutomaticUdlMcr(double length) =>
        CalculateMcr(section, Forces(), length, Opt.SupportConditions.HingesAtEnds,
            Opt.LoadConditions.Constant, Opt.LateralSupportConditions.HingesAtEnds,
            Opt.LateralWarpingConditions.HingesAtEnds, 1, Opt.LoadApplicationPoints.TopSection);
    public double AutomaticUdlMb() => CalculateLateralTorsionalBucklingMomentCapacity(
        Forces(), SC.Class1, section, EN1993P11Options);
    public (double Phi, double Chi, double F, double Modified) Reduction(
        double lambda, double alpha, double beta, double lambda0, double kc)
    {
        double phi = GetPhiForBuckling(alpha, lambda, beta, lambda0);
        double f = GetFactorFForLTB(kc, lambda);
        return (phi, GetChiLTmod(phi, lambda, beta), f, GetChiLTmod(phi, lambda, beta, f));
    }
}

internal static class ReferenceMath
{
    // Solve the characteristic polynomial independently of production's radical.
    public static double Reduction(double lambda, double alpha, double beta = 1, double lambda0 = .2)
    {
        if (lambda <= lambda0) return 1;
        double lo = 0, hi = Math.Min(1, 1 / (lambda * lambda));
        double b = 1 + alpha * (lambda - lambda0) + beta * lambda * lambda;
        for (int i = 0; i < 100; ++i)
        {
            double x = (lo + hi) / 2;
            if (beta * lambda * lambda * x * x - b * x + 1 > 0) lo = x;
            else hi = x;
        }
        return (lo + hi) / 2;
    }
    public static double Mcr(double length, double iz, double it, double iw,
        double c1, double c2, double zg, double g = 210000 / 2.6)
    {
        double n = Math.PI * Math.PI * 210000 * iz / (length * length);
        double stiffness = g * it + Math.PI * Math.PI * 210000 * iw / (length * length);
        // Positive eigenvalue of the coupled bending/torsion characteristic equation.
        // Rationalized product form avoids copying the SUT expression.
        double a = n * c2 * zg;
        return c1 * n * stiffness / (Math.Sqrt(a * a + n * stiffness) + a);
    }
    public static void Close(double expected, double actual, string label = "") =>
        Assert.AreEqual(expected, actual, Math.Max(1, Math.Abs(expected)) * 2e-12, label);
}
