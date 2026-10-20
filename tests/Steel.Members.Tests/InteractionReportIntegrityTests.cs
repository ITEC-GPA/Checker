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

// EC3:2005 + AC:2009, Tables A.1/A.2, pp.76-78.
// Test the public report against independent Cm arithmetic and algebraic
// reconstruction of kij, not against production getters for expected Cm.
[TestClass]
public class InteractionReportIntegrityTests
{
    public static IEnumerable<object[]> Cases =>
        from length in new[] { 200d, 4000d }
        from n in new[] { 0d, 400000d }
        from load in new[] { Opt.LoadConditions.Constant, Opt.LoadConditions.SingleForce, Opt.LoadConditions.NotDirectlyLoaded }
        select new object[] { length, n, load };

    [DataTestMethod, DynamicData(nameof(Cases))]
    public void ReportSeparatesInitialAndModifiedMomentFactorsAndReconstructsKij(
        double length, double compression, Opt.LoadConditions load)
    {
        const double m1 = 66.67e6, m2 = 20e6;
        var section = new SteelSection(new SectionH(500, 10.2, 200, 16, 200, 16, "IPE500", 21),
            new SteelMaterial("S275", 210000, 275, 430),
            Section.SectionTypes.Rolled, Section.FormedTypes.HotFinished);
        var forces = new ResultBeamForces(-compression, 0, 0, 0, m1, m2, CoordinateSystem.Global);
        var beam = new BeamElement(Point3d.Origin, new Point3d(length, 0, 0), section);
        beam.Results.Add(new BeamResult(new[] { new StationResultBeamForces(
            new LoadCase("Report integrity", LoadCase.LoadCaseTypes.SelfWeight), forces, 0) }));
        var options = new Opt(load, Opt.SupportConditions.HingesAtEnds,
            Opt.LateralSupportConditions.HingesAtEnds, Opt.LateralWarpingConditions.HingesAtEnds,
            0, 0, loadApplicationPoint: Opt.LoadApplicationPoints.ShearCenter);
        var checker = new EN1993p11Checker(beam, options, new StandardEN1993p11 { GammaM0 = 1.05, GammaM1 = 1.05 });
        checker.PerformCheck();
        Assert.AreEqual(0, checker.GetErrorLog().Count, string.Join("; ", checker.GetErrorLog()));
        var result = checker.EN1993p11BeamStationResults.Single();

        // Model properties are declared inputs here, not independent geometry oracles.
        double ny = Math.PI * Math.PI * 210000 * section.J11 / (length * length);
        double nz = Math.PI * Math.PI * 210000 * section.J22 / (length * length);
        double Cm0(double nc) => load == Opt.LoadConditions.Constant ? 1 + .03 * compression / nc :
            load == Opt.LoadConditions.SingleForce ? 1 - .18 * compression / nc :
            .79 - .36 * .33 * compression / nc; // psi=0 in both planes
        double cm10 = Cm0(ny), cm20 = Cm0(nz), cm1 = cm10;
        double nt = (210000 / 2.6 * section.Jt + Math.PI * Math.PI * 210000 * section.Jw / (length * length))
            / ((section.J11 + section.J22) / section.Area);
        double lambda0 = Math.Sqrt(section.Wpl1 * 275 /
            ReferenceMath.Mcr(length, section.J22, section.Jt, section.Jw, 1, 0, 0));
        double kc = load == Opt.LoadConditions.Constant ? .94 : load == Opt.LoadConditions.SingleForce ? .86 : 1 / 1.33;
        double limit = .2 / kc * Math.Pow((1 - compression / nz) * (1 - compression / nt), .25);
        bool susceptible = lambda0 > limit;
        Assert.AreEqual(length == 4000, susceptible, "independent branch selection");
        double a = Math.Max(0, 1 - section.Jt / section.J11), cmLT = 1;
        if (susceptible)
        {
            if (compression == 0) cm1 = 1; // limit for nonzero moment as N tends to zero
            else
            {
                double q = Math.Sqrt(m1 / compression * section.Area / section.Wel1) * a;
                cm1 = (cm10 + q) / (1 + q);
            }
            cmLT = Math.Max(1, cm1 * cm1 * a /
                Math.Sqrt((1 - compression / nz) * (1 - compression / nt)));
        }
        ReferenceMath.Close(cm1, result.Cmx, "Cmx is the equivalent moment factor, not Cyx");
        ReferenceMath.Close(cm20, result.Cmy, "Cmy is the equivalent moment factor, not Cyy");
        ReferenceMath.Close(cm10, result.CmX0, "initial major-axis Cm0");
        ReferenceMath.Close(cm20, result.CmY0, "initial minor-axis Cm0");
        ReferenceMath.Close(cmLT, result.CmLT, "CmLT");

        // This is a report consistency identity, not independent qualification
        // of Cij, mu or the full beam-column resistance.
        ReferenceMath.Close(result.Cmx * result.CmLT * result.Mux / (1 - compression / ny) / result.Cxx,
            result.Kxx, "report reconstructs kxx");
        ReferenceMath.Close(result.Cmy * result.Mux / (1 - compression / nz) / result.Cxy * .6 * Math.Sqrt(result.Wy / result.Wx),
            result.Kxy, "report reconstructs kxy");
        ReferenceMath.Close(result.Cmx * result.CmLT * result.Muy / (1 - compression / ny) / result.Cyx * .6 * Math.Sqrt(result.Wx / result.Wy),
            result.Kyx, "report reconstructs kyx");
        ReferenceMath.Close(result.Cmy * result.Muy / (1 - compression / nz) / result.Cyy,
            result.Kyy, "report reconstructs kyy");
    }
}
