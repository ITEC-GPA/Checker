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

// EN 1993-1-1:2005 + AC:2009, Table A.1, printed p.76:
// npl = NEd / (Npl,Rk / gammaM0). Pure compression isolates this term:
// bLT,cLT,dLT,eLT vanish, so gammaM1 cannot legitimately affect Cij.
[TestClass]
public class AnnexANplNormalizationTests
{
    public static IEnumerable<object[]> Cases =>
        from hollow in new[] { false, true }
        from n in new[] { 0d, 200000d }
        from gammas in new[] { (1d, 1d), (1d, 1.1d), (1.05d, 1.1d), (1.2d, 1d) }
        select new object[] { hollow, n, gammas.Item1, gammas.Item2 };

    [DataTestMethod, DynamicData(nameof(Cases))]
    public void PureCompressionUsesSectionPartialFactor(bool hollow, double n, double gamma0, double gamma1)
    {
        Section shape = hollow ? new SectionRHS(300, 200, 10, 10, 10, 10, "RHS")
            : new SectionH(300, 10, 200, 20, 200, 20, "H");
        var section = new SteelSection(shape, new SteelMaterial("S275", 210000, 275, 430),
            Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished);
        var actual = new Probe(section, gamma0, gamma1).Coefficients(n);
        // Explicit Model geometry inputs; the normative arithmetic below is independent.
        double ny = Math.PI * Math.PI * 210000 * section.J11 / (3000 * 3000);
        double nz = Math.PI * Math.PI * 210000 * section.J22 / (3000 * 3000);
        double lambda = Math.Sqrt(section.Area * 275 / Math.Min(ny, nz));
        double w1 = Math.Min(1.5, section.Wpl1 / section.Wel1);
        double w2 = Math.Min(1.5, section.Wpl2 / section.Wel2);
        // Table A.2, end moments with psi=.5; eccentricity is zero here,
        // so the susceptibility correction leaves Cm equal to Cm0.
        double cm1 = .895 + .0612 * n / ny, cm2 = .895 + .0612 * n / nz;
        double npl = n * gamma0 / (section.Area * 275);
        double Direct(double w, double cm) => 2 - 1.6 * cm * cm * lambda * (1 + lambda) / w;
        double Cross(double w, double cm) => 2 - 14 * cm * cm * lambda * lambda / Math.Pow(w, 5);
        ReferenceMath.Close(Math.Max(1 + (w1 - 1) * Direct(w1, cm1) * npl, section.Wel1 / section.Wpl1), actual.C11, "C11");
        ReferenceMath.Close(Math.Max(1 + (w2 - 1) * Cross(w2, cm2) * npl,
            .6 * Math.Sqrt(w2 / w1) * section.Wel2 / section.Wpl2), actual.C12, "C12");
        ReferenceMath.Close(Math.Max(1 + (w1 - 1) * Cross(w1, cm1) * npl,
            .6 * Math.Sqrt(w1 / w2) * section.Wel1 / section.Wpl1), actual.C21, "C21");
        ReferenceMath.Close(Math.Max(1 + (w2 - 1) * Direct(w2, cm2) * npl, section.Wel2 / section.Wpl2), actual.C22, "C22");
    }

    private sealed class Probe : EN1993p11Checker
    {
        private readonly ISteelSection section;
        internal Probe(SteelSection s, double gamma0, double gamma1)
            : base(new BeamElement(Point3d.Origin, new Point3d(3000, 0, 0), s),
                new Opt(Opt.LoadConditions.NotDirectlyLoaded, Opt.SupportConditions.HingesAtEnds,
                    Opt.LateralSupportConditions.HingesAtEnds, Opt.LateralWarpingConditions.HingesAtEnds, .5, .5),
                new StandardEN1993p11 { GammaM0 = gamma0, GammaM1 = gamma1 })
        { section = s; }

        internal (double C11, double C12, double C21, double C22) Coefficients(double n)
        {
            var forces = new ResultBeamForces(-n, 0, 0, 0, 0, 0, CoordinateSystem.Global);
            CalculateCoefficientsForInteraction(section, SC.Class1, forces,
                Opt.LoadConditions.NotDirectlyLoaded, Opt.SupportConditions.HingesAtEnds,
                Opt.LateralSupportConditions.HingesAtEnds, Opt.LateralWarpingConditions.HingesAtEnds,
                Opt.LoadApplicationPoints.ShearCenter, .5, out _, out _, out _, out _, out _,
                out double c11, out double c12, out double c21, out double c22,
                out _, out _, out _, out _, out _, out _, out _);
            return (c11, c12, c21, c22);
        }
    }
}
