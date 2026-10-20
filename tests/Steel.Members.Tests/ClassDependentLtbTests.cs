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

[TestClass]
public class ClassDependentLtbTests
{
    [DataTestMethod]
    [DataRow(false, 1, 1d)] [DataRow(false, 2, 1d)] [DataRow(false, 3, 1d)]
    [DataRow(true, 1, 1d)] [DataRow(true, 2, 1d)] [DataRow(true, 3, 1d)]
    [DataRow(false, 1, 1.1)] [DataRow(false, 2, 1.1)] [DataRow(false, 3, 1.1)]
    [DataRow(true, 1, 1.1)] [DataRow(true, 2, 1.1)] [DataRow(true, 3, 1.1)]
    public void BothLtbMethodsUseTheModulusOfTheRequestedClass(bool modified, int sectionClass, double gamma)
    {
        var p = new Probe(gamma);
        double mcr = p.Mcr();
        // Gross dimensions are held fixed; only the constitutive resistance
        // modulus changes. EC3 6.3.2.1(3), 6.3.2.2(1) and 6.3.2.3.
        double w = sectionClass == 3 ? 943573.3333333333 : 1068800;
        double lambda = Math.Sqrt(w * 355 / mcr);
        double alpha = modified ? .34 : .21, beta = modified ? .75 : 1;
        double phi = (1 + alpha * (lambda - (modified ? .4 : .2)) + beta * lambda * lambda) / 2;
        double chi = SmallRoot(beta * lambda * lambda, 2 * phi);
        Assert.AreEqual(chi * w * 355 / gamma, p.Capacity(sectionClass, modified), 1e-5);
    }

    [TestMethod]
    public void PublicClass3ReportUsesElasticSlendernessAndTheSameCapacity()
    {
        var p = new Probe(1.1);
        p.BeamElement.Results.Add(new BeamResult(new[] {
            new StationResultBeamForces(new LoadCase("Class 3", LoadCase.LoadCaseTypes.SelfWeight),
                new ResultBeamForces(0, 0, 0, 0, 10e6, 0, CoordinateSystem.Global), 0) }));
        p.PerformCheck();
        Assert.AreEqual(0, p.GetErrorLog().Count, string.Join("; ", p.GetErrorLog()));
        var r = p.EN1993p11BeamStationResults.Single();
        Assert.AreEqual(3, (int)r.BendingCompressionClass);
        Assert.AreEqual(Math.Sqrt(943573.3333333333 * 355 / r.MCr), r.LambdaSignedLTBuckling, 1e-12);
        Assert.AreEqual(r.ChiLTBuckling * 943573.3333333333 * 355 / 1.1,
            r.LateralTosionalBucklingCapacity, 1e-5);
        Assert.AreEqual(r.LambdaSignedLTBuckling, r.Lambda0LTBuckling, 1e-12);
    }

    private static double SmallRoot(double a, double b)
    {
        // Solve a*chi^2-b*chi+1=0 on the admissible EC3 interval.
        double lo = 0, hi = Math.Min(1, 1 / a);
        if (a * hi * hi - b * hi + 1 >= 0) return hi;
        for (int i = 0; i < 100; ++i)
        {
            double x = (lo + hi) / 2;
            if (a * x * x - b * x + 1 > 0) lo = x; else hi = x;
        }
        return (lo + hi) / 2;
    }

    private sealed class Probe : EN1993p11Checker
    {
        private SteelSection Section => (SteelSection)BeamElement.BeamProperty;
        private readonly ResultBeamForces f = new(0, 0, 0, 0, 10e6, 0, CoordinateSystem.Global);
        public Probe(double gamma) : base(new BeamElement(Point3d.Origin, new Point3d(4000, 0, 0),
            new SteelSection(new SectionH(400, 8, 200, 10, 200, 10, ""), new SteelMaterial("S355", 210000, 355, 510))),
            new Opt(Opt.LoadConditions.NotDirectlyLoaded, Opt.SupportConditions.HingesAtEnds,
                Opt.LateralSupportConditions.HingesAtEnds, Opt.LateralWarpingConditions.HingesAtEnds, 1, 1,
                loadApplicationPoint: Opt.LoadApplicationPoints.ShearCenter),
            new StandardEN1993p11 { GammaM1 = gamma }) { }
        public double Mcr() => CalculateMcr(Section, f, 4000,
            Opt.SupportConditions.HingesAtEnds, Opt.LoadConditions.NotDirectlyLoaded,
            Opt.LateralSupportConditions.HingesAtEnds, Opt.LateralWarpingConditions.HingesAtEnds, 1,
            Opt.LoadApplicationPoints.ShearCenter);
        public double Capacity(int sectionClass, bool modified) => modified
            ? CalculateMbRdMod(Section, (SectionClass)sectionClass, f, Opt.SupportConditions.HingesAtEnds,
                Opt.LoadConditions.NotDirectlyLoaded, Opt.LateralSupportConditions.HingesAtEnds,
                Opt.LateralWarpingConditions.HingesAtEnds, 1, Opt.LoadApplicationPoints.ShearCenter)
            : CalculateMbRd(Section, (SectionClass)sectionClass, f, Opt.SupportConditions.HingesAtEnds,
                Opt.LoadConditions.NotDirectlyLoaded, Opt.LateralSupportConditions.HingesAtEnds,
                Opt.LateralWarpingConditions.HingesAtEnds, 1, Opt.LoadApplicationPoints.ShearCenter);
    }
}
