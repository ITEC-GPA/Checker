using GPC.Checkers.Steel.Checkers;
using GPC.Checkers.Steel.Results;
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

// Independent EC3:2005+AC:2009 arithmetic for the DECLARED legacy inputs.
// Sources: 6.3.1, 6.3.2.3, 6.3.3(4), Tables 6.2/6.3/6.5/6.6, A.1/A.2;
// NCCI SN003b for Mcr. This does NOT reproduce the unavailable Cordova pages.
// Geometry/torsion properties from Model are explicit inputs, not qualified here.
[TestClass]
public class LegacyHInteractionArithmeticTests
{
    public TestContext TestContext { get; set; } = null!;

    [DataTestMethod]
    [DataRow(2, false)] [DataRow(3, false)]
    [DataRow(2, true)] [DataRow(3, true)]
    public void DeclaredLegacyInputsAgainstIndependentAnnexAChain(int legacyCase, bool distinctGammas)
    {
        bool ipe = legacyCase == 2;
        var section = new SteelSection(ipe
                ? new SectionH(300, 7.1, 150, 10.7, 150, 10.7, "IPE300", 15)
                : new SectionH(250, 7.5, 260, 12.5, 260, 12.5, "HEA260", 24),
            new SteelMaterial("S235", 210000, 235, 360),
            Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished);
        double length = ipe ? 5000 : 4000, n = ipe ? 190000 : 400000;
        double m1 = ipe ? 78e6 : 71e6, m2 = ipe ? 0 : 30e6;
        double g0 = ipe ? 1.05 : 1, g1 = distinctGammas ? 1.1 : g0;
        var load = ipe ? Opt.LoadConditions.NotDirectlyLoaded : Opt.LoadConditions.Constant;
        var forces = new ResultBeamForces(-n, 0, 0, 0, m1, m2, CoordinateSystem.Global);
        var beam = new BeamElement(Point3d.Origin, new Point3d(length, 0, 0), section);
        beam.Results.Add(new BeamResult(new[] { new StationResultBeamForces(
            new LoadCase("Independent Annex A", LoadCase.LoadCaseTypes.SelfWeight), forces, 0) }));
        var checker = new EN1993p11Checker(beam,
            new Opt(load, Opt.SupportConditions.HingesAtEnds, Opt.LateralSupportConditions.HingesAtEnds,
                Opt.LateralWarpingConditions.HingesAtEnds, 0, 0,
                loadApplicationPoint: Opt.LoadApplicationPoints.ShearCenter),
            new StandardEN1993p11 { GammaM0 = g0, GammaM1 = g1 });
        checker.PerformCheck();
        Assert.AreEqual(0, checker.GetErrorLog().Count, string.Join("; ", checker.GetErrorLog()));
        Assert.IsTrue(section.IsDoubleSymmetric, "No bending-torsion coupling in axial buckling.");
        var expected = Independent(section, ipe, length, n, m1, m2, g0, g1);
        var actual = checker.EN1993p11BeamStationResults.Single();
        var errors = new List<string>();
        foreach (var (name, value) in expected)
        {
            double observed = (double)typeof(EN1993p11BeamStationResult).GetProperty(name)!.GetValue(actual)!;
            TestContext.WriteLine($"{name}: independent={value:R}; checker={observed:R}");
            double tolerance = 2e-11 * Math.Max(1, Math.Abs(value));
            if (!double.IsFinite(observed) || Math.Abs(value - observed) > tolerance)
                errors.Add($"{name}: expected {value:R}, actual {observed:R}");
        }
        Assert.AreEqual(0, errors.Count, string.Join(Environment.NewLine, errors));
    }

    private static Dictionary<string, double> Independent(ISteelSection s, bool ipe,
        double length, double n, double m1, double m2, double g0, double g1)
    {
        const double fy = 235, elastic = 210000;
        double nrk = s.Area * fy, myrk = s.Wpl1 * fy, mzrk = s.Wpl2 * fy;
        double ny = Math.PI * Math.PI * elastic * s.J11 / (length * length);
        double nz = Math.PI * Math.PI * elastic * s.J22 / (length * length);
        double nt = (elastic / 2.6 * s.Jt + Math.PI * Math.PI * elastic * s.Jw / (length * length))
            * s.Area / (s.J11 + s.J22);
        double ly = Math.Sqrt(nrk / ny), lz = Math.Sqrt(nrk / nz), lm = Math.Max(ly, lz);
        // Rolled IPE h/b>1.2: a/b. HEA h/b<=1.2: b/c, flange<=100 mm.
        double chiy = ReferenceMath.Reduction(ly, ipe ? .21 : .34);
        double chiz = ReferenceMath.Reduction(lz, ipe ? .34 : .49);
        double muy = (ny - n) / (ny - chiy * n), muz = (nz - n) / (nz - chiz * n);
        double mcr0 = ReferenceMath.Mcr(length, s.J22, s.Jt, s.Jw, 1, 0, 0);
        // psi=0: C1=1.77 (declared end-moment approximation);
        // simply supported UDL at shear centre: C1=1.127 (SN003b).
        double mcr = mcr0 * (ipe ? 1.77 : 1.127);
        double l0 = Math.Sqrt(myrk / mcr0), lt = Math.Sqrt(myrk / mcr);
        double kc = ipe ? 1 / 1.33 : .94;
        double f = Math.Min(1, 1 - .5 * (1 - kc) * (1 - 2 * Math.Pow(lt - .8, 2)));
        double chiLT = Math.Min(Math.Min(1, 1 / (lt * lt)), ReferenceMath.Reduction(lt, .34, .75, .4) / f);
        double cm10 = ipe ? .79 - .1188 * n / ny : 1 + .03 * n / ny;
        double cm20 = ipe ? .79 - .1188 * n / nz : 1 + .03 * n / nz;
        double limit = .2 / kc * Math.Pow((1 - n / nz) * (1 - n / nt), .25);
        // Both legacy cases are well inside the susceptible branch, even if
        // the implementation misidentifies the axial TF mode at roundoff level.
        Assert.IsTrue(l0 > .2 / kc && l0 > limit, "Annex A susceptible branch");
        double a = 1 - s.Jt / s.J11, epsilon = m1 / n * s.Area / s.Wel1;
        double q = Math.Sqrt(epsilon) * a, cm1 = (cm10 + q) / (1 + q), cm2 = cm20;
        double cmLT = Math.Max(1, cm1 * cm1 * a / Math.Sqrt((1 - n / nz) * (1 - n / nt)));
        double w1 = Math.Min(1.5, s.Wpl1 / s.Wel1), w2 = Math.Min(1.5, s.Wpl2 / s.Wel2);
        double r1 = m1 * g0 / (myrk * chiLT), r2 = m2 * g0 / mzrk;
        double z4 = lz * lz * lz * lz;
        double b = .5 * a * l0 * l0 * r1 * r2;
        double c = 10 * a * l0 * l0 * r1 / ((5 + z4) * cm1);
        double d = 2 * a * l0 * r1 * r2 / ((.1 + z4) * cm1 * cm2);
        double e = 1.7 * a * l0 * r1 / ((.1 + z4) * cm1);
        double npl = n * g0 / nrk;
        double c11 = Math.Max(1 + (w1 - 1) * ((2 - 1.6 / w1 * cm1 * cm1 * lm * (1 + lm)) * npl - b), s.Wel1 / s.Wpl1);
        double c12 = Math.Max(1 + (w2 - 1) * ((2 - 14 * cm2 * cm2 * lm * lm / Math.Pow(w2, 5)) * npl - c), .6 * Math.Sqrt(w2 / w1) * s.Wel2 / s.Wpl2);
        double c21 = Math.Max(1 + (w1 - 1) * ((2 - 14 * cm1 * cm1 * lm * lm / Math.Pow(w1, 5)) * npl - d), .6 * Math.Sqrt(w1 / w2) * s.Wel1 / s.Wpl1);
        double c22 = Math.Max(1 + (w2 - 1) * (2 - 1.6 / w2 * cm2 * cm2 * lm * (1 + lm) - e) * npl, s.Wel2 / s.Wpl2);
        double k11 = cm1 * cmLT * muy * ny / ((ny - n) * c11);
        double k12 = cm2 * muy * nz / ((nz - n) * c12) * .6 * Math.Sqrt(w2 / w1);
        double k21 = cm1 * cmLT * muz * ny / ((ny - n) * c21) * .6 * Math.Sqrt(w1 / w2);
        double k22 = cm2 * muz * nz / ((nz - n) * c22);
        return new()
        {
            ["NCr1"] = ny, ["NCr2"] = nz, ["NcrTorsional"] = nt, ["NcrFlexuralTorsional"] = nt,
            ["LambdaSignedAxialBuckling1"] = ly, ["LambdaSignedAxialBuckling2"] = lz,
            ["ChiAxialBuckling1"] = chiy, ["ChiAxialBuckling2"] = chiz,
            ["MCr"] = mcr, ["Lambda0LTBuckling"] = l0, ["LambdaSignedLTBuckling"] = lt,
            ["ChiLTBuckling"] = chiLT, ["LateralTosionalBucklingCapacity"] = chiLT * myrk / g1,
            ["CmX0"] = cm10, ["CmY0"] = cm20, ["Cmx"] = cm1, ["Cmy"] = cm2, ["CmLT"] = cmLT,
            ["Mux"] = muy, ["Muy"] = muz, ["Wx"] = w1, ["Wy"] = w2, ["EpsilonX"] = epsilon,
            ["BLT"] = b, ["CLT"] = c, ["DLT"] = d, ["ELT"] = e,
            ["Cxx"] = c11, ["Cxy"] = c12, ["Cyx"] = c21, ["Cyy"] = c22,
            ["Kxx"] = k11, ["Kxy"] = k12, ["Kyx"] = k21, ["Kyy"] = k22,
            ["BucklingInteraction1Axis"] = g1 * (n / (chiy * nrk) + k11 * m1 / (chiLT * myrk) + k12 * m2 / mzrk),
            ["BucklingInteraction2Axis"] = g1 * (n / (chiz * nrk) + k21 * m1 / (chiLT * myrk) + k22 * m2 / mzrk)
        };
    }
}
