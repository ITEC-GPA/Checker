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

// EC3:2005 + AC:2009, 6.3.2.2/3, Tables 6.3-6.6 and A.1, pp.61-63,76-77.
// NCCI SN003b: elastic Mcr for fork supports, load at the shear centre.
// Normative arithmetic, not a published full beam-column worked example.
[TestClass]
public class InteractionLtbConsistencyTests
{
    public static IEnumerable<object[]> Cases =>
        from family in new[] { "RolledWide", "RolledNarrow", "WeldedWide", "WeldedNarrow", "RHS" }
        from load in new[] { Opt.LoadConditions.NotDirectlyLoaded, Opt.LoadConditions.Constant, Opt.LoadConditions.SingleForce }
        select new object[] { family, load, false };

    [DataTestMethod, DynamicData(nameof(Cases))]
    [DataRow("RolledWide", Opt.LoadConditions.Constant, true)]
    [DataRow("RHS", Opt.LoadConditions.Constant, true)]
    public void AuxiliaryTermsAndCapacityUseTheSameLtbMethod(string family, Opt.LoadConditions load, bool custom)
    {
        bool modified = family != "RHS", rolled = family.StartsWith("Rolled");
        bool wide = family.EndsWith("Wide");
        Section shape = modified
            ? new SectionH(300, 10, wide ? 200 : 100, 20, wide ? 200 : 100, 20, family)
            : new SectionRHS(300, 100, 10, 10, 10, 10, family);
        var section = new SteelSection(shape, new SteelMaterial("S275", 210000, 275, 430),
            rolled ? Section.SectionTypes.Rolled : Section.SectionTypes.Welded,
            Section.FormedTypes.HotFinished);
        // Geometry is an explicit Model input; this test does not qualify its integration.
        const double length = 8000, n = 10000, m1 = 10e6, m2 = 3e6, gamma = 1.05;
        var standard = new StandardEN1993p11 { GammaM0 = gamma, GammaM1 = gamma };
        if (custom)
        {
            // Configuration propagation only; not prescribed National Annex values.
            standard.BetaForLateralTorsionalBucklingMod = .9;
            standard.LambdaLT0ForLateralTorsionalBucklingMod = .3;
            standard.BetaForLateralTorsionalBuckling = .95;
            standard.LambdaLT0ForLateralTorsionalBuckling = .15;
        }
        var forces = new ResultBeamForces(-n, 0, 0, 0, m1, m2, CoordinateSystem.Global);
        var actual = new Probe(section, length, standard).Evaluate(forces, load);
        double c1 = load == Opt.LoadConditions.Constant ? 1.127 :
            load == Opt.LoadConditions.SingleForce ? 1.348 : 1;
        double kc = load == Opt.LoadConditions.Constant ? .94 :
            load == Opt.LoadConditions.SingleForce ? .86 : 1;
        double mcr0 = ReferenceMath.Mcr(length, section.J22, section.Jt, section.Jw, 1, 0, 0);
        double lambda0 = Math.Sqrt(section.Wpl1 * 275 / mcr0);
        double lambda = lambda0 / Math.Sqrt(c1);
        double alpha = modified ? (rolled ? (wide ? .34 : .49) : (wide ? .49 : .76)) : .76;
        double beta = modified ? (custom ? .9 : .75) : (custom ? .95 : 1);
        double plateau = modified ? (custom ? .3 : .4) : (custom ? .15 : .2);
        double f = modified ? Math.Min(1, 1 - .5 * (1 - kc) * (1 - 2 * Math.Pow(lambda - .8, 2))) : 1;
        double chi = Math.Min(Math.Min(1, 1 / (lambda * lambda)),
            ReferenceMath.Reduction(lambda, alpha, beta, plateau) / f);
        double mc1 = section.Wpl1 * 275 / gamma, mc2 = section.Wpl2 * 275 / gamma;
        ReferenceMath.Close(chi * mc1, actual.Mb, "independent LTB capacity");

        double ny = Math.PI * Math.PI * 210000 * section.J11 / (length * length);
        double nz = Math.PI * Math.PI * 210000 * section.J22 / (length * length);
        double nt = (210000 / 2.6 * section.Jt + Math.PI * Math.PI * 210000 * section.Jw / (length * length))
            / ((section.J11 + section.J22) / section.Area);
        double limit = .2 / kc * Math.Pow((1 - n / nz) * (1 - n / nt), .25);
        Assert.IsTrue(lambda0 > limit && lambda0 > .2 / kc, "susceptible branch, independent of threshold rounding");
        double a = Math.Max(0, 1 - section.Jt / section.J11);
        Assert.IsTrue(a > 0, "The fixture must be sensitive to the LTB auxiliary terms.");
        double Cm0(double nc) => load == Opt.LoadConditions.Constant ? 1 + .03 * n / nc :
            load == Opt.LoadConditions.SingleForce ? 1 - .18 * n / nc :
            1 + .36 * (1 - .33) * n / nc;
        double q = Math.Sqrt(m1 / n * section.Area / section.Wel1) * a;
        double cm1 = (Cm0(ny) + q) / (1 + q), cm2 = Cm0(nz);
        double lambdaZ4 = Math.Pow(section.Area * 275 / nz, 2);
        double ry = m1 / (chi * mc1), rz = m2 / mc2;
        // Table A.1, expressed in dimensionless moment ratios.
        ReferenceMath.Close(.5 * a * lambda0 * lambda0 * ry * rz, actual.B, "bLT");
        ReferenceMath.Close(10 * a * lambda0 * lambda0 * ry / ((5 + lambdaZ4) * cm1), actual.C, "cLT");
        ReferenceMath.Close(2 * a * lambda0 * ry * rz / ((.1 + lambdaZ4) * cm1 * cm2), actual.D, "dLT");
        ReferenceMath.Close(1.7 * a * lambda0 * ry / ((.1 + lambdaZ4) * cm1), actual.E, "eLT");
    }

    private sealed class Probe : EN1993p11Checker
    {
        private readonly ISteelSection section;
        internal Probe(SteelSection section, double length, StandardEN1993p11 standard)
            : base(new BeamElement(Point3d.Origin, new Point3d(length, 0, 0), section),
                new Opt(Opt.LoadConditions.Constant, Opt.SupportConditions.HingesAtEnds,
                    Opt.LateralSupportConditions.HingesAtEnds, Opt.LateralWarpingConditions.HingesAtEnds, 1, 1,
                    loadApplicationPoint: Opt.LoadApplicationPoints.ShearCenter), standard)
        { this.section = section; }

        internal (double B, double C, double D, double E, double Mb) Evaluate(ResultBeamForces forces, Opt.LoadConditions load)
        {
            EN1993P11Options.LoadCondition = load;
            CalculateCoefficientsForInteraction(section, SC.Class1, forces, load,
                Opt.SupportConditions.HingesAtEnds, Opt.LateralSupportConditions.HingesAtEnds,
                Opt.LateralWarpingConditions.HingesAtEnds, Opt.LoadApplicationPoints.ShearCenter, 1,
                out _, out double b, out double c, out double d, out double e,
                out _, out _, out _, out _, out _, out _, out _, out _, out _, out _, out _);
            return (b, c, d, e, CalculateLateralTorsionalBucklingMomentCapacity(forces, SC.Class1, section, EN1993P11Options));
        }
    }
}
