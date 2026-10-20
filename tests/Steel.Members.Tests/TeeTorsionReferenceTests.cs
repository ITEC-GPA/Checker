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

namespace Steel.Members.Tests;

[TestClass]
public class TeeTorsionReferenceTests
{
    [TestMethod]
    public void PublishedWt180x67TorsionalProperties()
    {
        // CISC, Torsional section properties of steel shapes, rev.Aug2002,p.9.
        // Pure geometric/mechanical reference, no CSA design rule is used.
        var t = new SectionT(178, 369, 11.2, 18, "WT180x67 idealized");
        Assert.AreEqual(796e3, t.Jt, .5e3, "rounded published J, mm4");
        Assert.AreEqual(2.22e9, t.Jw, .005e9, "rounded published Cw, mm6");
        Assert.AreEqual(184.5, t.ShearCenter.X, 1e-12);
        Assert.AreEqual(169, t.ShearCenter.Y, 1e-12);
    }

    [TestMethod]
    public void PublishedL203x102x13TorsionalProperties()
    {
        // Same primary document,p.8; sharp equal-thickness angle.
        var t = new SectionL(102, 12.7, 203, 12.7, "");
        Assert.AreEqual(200e3, t.Jt, .5e3);
        // The source first rounds d'=196.65 to197 and b'=95.65 to95.7.
        // Transfer that documented intermediate rounding, not a fitted tolerance.
        double sourceRounding = (Math.Pow(197, 3) + Math.Pow(95.7, 3)
            - Math.Pow(196.65, 3) - Math.Pow(95.65, 3)) * Math.Pow(12.7, 3) / 36;
        Assert.AreEqual(.485e9, t.Jw + sourceRounding, .0005e9);
        Assert.AreEqual(6.35, t.ShearCenter.X, 1e-12);
        Assert.AreEqual(6.35, t.ShearCenter.Y, 1e-12);
    }

    [TestMethod]
    public void PublishedC310x31TorsionalProperties()
    {
        // Same document,p.7: zero flange slope assumed by the source.
        var t = new SectionC(305, 7.2, 74, 12.7, 74, 12.7, "");
        // Source first rounds the middle-line depth from292.3 to292 mm.
        double sourceJ = t.Jt - .3 * Math.Pow(7.2, 3) / 3;
        Assert.AreEqual(132e3, sourceJ, .5e3);
        double d = 292, b = 70.4, alpha = 1 / (2 + d * 7.2 / (3 * b * 12.7));
        double sourceCw = d * d * b * b * b * 12.7 / 6 *
            (1 - 3 * alpha + 3 * alpha * alpha + alpha * alpha * d * 7.2 / (2 * b * 12.7));
        Assert.AreEqual(29e9, sourceCw, .05e9);
        // With full precision, the independently published expression equals Model.
        d = 292.3;
        alpha = 1 / (2 + d * 7.2 / (3 * b * 12.7));
        double exactCw = d * d * b * b * b * 12.7 / 6 *
            (1 - 3 * alpha + 3 * alpha * alpha + alpha * alpha * d * 7.2 / (2 * b * 12.7));
        Assert.AreEqual(exactCw, t.Jw, exactCw * 1e-12);
        // The source supplies xbar=17.5 (without its derivation). It is NOT
        // the centroid19.711... of the zero-slope idealization. Qualify the
        // absolute shear-centre coordinate, keeping that source datum explicit.
        Assert.AreEqual(39.2, 17.5 - t.ShearCenter.X, .1);
    }

    public static IEnumerable<object[]> UniformMomentCases =>
        from scale in new[] { .01, 1d, 10d }
        from lengthRatio in new[] { 2.5, 10d, 30d }
        from sign in new[] { -1d, 1d }
        select new object[] { scale, lengthRatio, sign };

    [DataTestMethod, DynamicData(nameof(UniformMomentCases))]
    public void UniformMomentMatchesIndependentGeometricStiffness(double scale, double lengthRatio, double sign)
    {
        double h = 400 * scale, b = 200 * scale, tw = 40 * scale, tf = 50 * scale, length = h * lengthRatio;
        var t = new SectionT(h, b, tw, tf, "");
        var p = new Probe(t, length);
        // Two-mode sine solution for fork ends, uniform moment, free warping.
        // GPC positive-face convention: sigma=M1*(y-yc)/Ix (tension positive).
        // A 3x3 Gauss rule integrates the stress/geometric stiffness exactly;
        // it does not call the production Wagner calculation.
        double area = b * tf + tw * (h - tf);
        double yc = (b * tf * (h - tf / 2) + tw * Math.Pow(h - tf, 2) / 2) / area;
        double ix = Integrate(t, (x, y) => (y - yc) * (y - yc));
        double iz = Integrate(t, (x, y) => x * x);
        double geo = Integrate(t, (x, y) => (y - yc) / ix * (x * x + Math.Pow(y - (h - tf / 2), 2)));
        double ncr = Math.PI * Math.PI * 210000 * iz / (length * length);
        double kt = 210000 / 2.6 * t.Jt + Math.PI * Math.PI * 210000 * t.Jw / (length * length);
        // det K = Ncr*(Kt+M*geo)-M^2 = 0. Solve for the positive magnitude.
        double lo = 0, hi = Math.Sqrt(ncr * kt) + Math.Abs(ncr * geo);
        for (int i = 0; i < 100; i++)
        {
            double m = (lo + hi) / 2;
            if (ncr * (kt + sign * m * geo) - m * m > 0) lo = m; else hi = m;
        }
        double expected = (lo + hi) / 2;
        Assert.AreEqual(expected, p.Mcr(sign), expected * 1e-11);
    }

    [TestMethod]
    public void CompressedStemTipHasTheLowerUniformMomentEigenvalue()
    {
        var p = new Probe(new SectionT(400, 200, 40, 50, ""), 4000);
        Assert.IsTrue(p.Mcr(1) < p.Mcr(-1));
    }

    [TestMethod]
    public void ExactWagnerIntegralMatchesRationalTwoRectangleIntegration()
    {
        // A=24000, yc=775/3, Ix=1135000000/3,
        // integral(x^2+y^2)*y dA=-211400000000/9 -> zj=33530/227 mm.
        var p = new Probe(new SectionT(400, 200, 40, 50, ""), 4000);
        Assert.AreEqual(33530d / 227, p.Wagner(), 1e-11);
    }

    [DataTestMethod]
    [DataRow(true)] [DataRow(false)]
    public void UnqualifiedTeeGeometryCannotReturnAnMcrForTheWrongAxes(bool rounded)
    {
        var t = rounded ? new SectionT(400, 200, 40, 50, "", 10) : new SectionT(160, 500, 8, 40, "");
        Assert.ThrowsException<NotSupportedException>(() => new Probe(t, 4000).Mcr(1));
    }

    private static double Integrate(SectionT t, Func<double, double, double> f)
    {
        double a = Math.Sqrt(3d / 5);
        double[] nodes = { -a, 0, a }, weights = { 5d / 9, 8d / 9, 5d / 9 };
        double total = 0;
        foreach (var r in new[] { (t.ThicknessWeb, 0d, t.HeightWeb), (t.LenghtFlange, t.HeightWeb, t.Height) })
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    total += weights[i] * weights[j] * r.Item1 * (r.Item3 - r.Item2) / 4 *
                        f(nodes[i] * r.Item1 / 2, (r.Item2 + r.Item3) / 2 + nodes[j] * (r.Item3 - r.Item2) / 2);
        return total;
    }

    private sealed class Probe : EN1993p11Checker
    {
        private readonly double length;
        private SteelSection SteelSection => (SteelSection)BeamElement.BeamProperty;
        public Probe(SectionT shape, double length) : base(new BeamElement(Point3d.Origin, new Point3d(length, 0, 0),
            new SteelSection(shape, new SteelMaterial("S355", 210000, 355, 510))),
            new Opt(Opt.LoadConditions.NotDirectlyLoaded, Opt.SupportConditions.HingesAtEnds,
                Opt.LateralSupportConditions.HingesAtEnds, Opt.LateralWarpingConditions.HingesAtEnds, 1, 1),
            new StandardEN1993p11()) { this.length = length; }
        public double Mcr(double sign) => CalculateMcr(SteelSection,
            new ResultBeamForces(0, 0, 0, 0, sign, 0, CoordinateSystem.Global), length,
            Opt.SupportConditions.HingesAtEnds, Opt.LoadConditions.NotDirectlyLoaded,
            Opt.LateralSupportConditions.HingesAtEnds, Opt.LateralWarpingConditions.HingesAtEnds, 1,
            Opt.LoadApplicationPoints.ShearCenter);
        public double Wagner() => GetTeeWagnerCoefficient((SectionT)SteelSection.SectionShape);
    }
}
