using GPC.Checkers.Steel.Checkers;
using GPC.Geometry;
using GPC.Model;
using GPC.Model.Data.Steel;
using GPC.Model.Elements;
using GPC.Model.LoadCases;
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
public class HollowManufacturingConfigurationTests
{
    // EN 1993-1-1:2005 Tables 6.1/6.2; S355 hollow sections.
    // Geometry and Euler force are evaluated independently of the Model properties.
    [DataTestMethod]
    [DataRow("RHS", false, 1, "a", .21)] [DataRow("RHS", false, 2, "a", .21)]
    [DataRow("RHS", true, 1, "c", .49)] [DataRow("RHS", true, 2, "c", .49)]
    [DataRow("CHS", false, 1, "a", .21)] [DataRow("CHS", false, 2, "a", .21)]
    [DataRow("CHS", true, 1, "c", .49)] [DataRow("CHS", true, 2, "c", .49)]
    public void ManufacturingRouteControlsTheCurveAndPhi(
        string family, bool cold, int axis, string curve, double alpha)
    {
        Section shape;
        double area, inertia;
        if (family == "RHS")
        {
            shape = new SectionRHS(400, 200, 8, 8, 15, 15, "");
            area = 400 * 200 - 384 * 170;
            inertia = axis == 1
                ? (200 * Math.Pow(400, 3) - 170 * Math.Pow(384, 3)) / 12
                : (400 * Math.Pow(200, 3) - 384 * Math.Pow(170, 3)) / 12;
        }
        else
        {
            shape = new SectionCHS(400, 10, "");
            area = Math.PI * (400 * 400 - 380 * 380) / 4;
            inertia = Math.PI * (Math.Pow(400, 4) - Math.Pow(380, 4)) / 64;
        }
        var section = new SteelSection(shape, SteelMaterialEN1993Data.S355,
            Section.SectionTypes.Rolled, cold ? Section.FormedTypes.ColdFormed : Section.FormedTypes.HotFinished);
        var checker = new Probe(section);
        checker.PerformCheck();
        Assert.AreEqual(0, checker.GetErrorLog().Count, string.Join("; ", checker.GetErrorLog()));
        Assert.AreEqual(curve, checker.Curve(axis));
        var result = checker.EN1993p11BeamStationResults.Single();
        double ncr = Math.PI * Math.PI * 210000 * inertia / (1000 * 1000);
        double lambda = Math.Sqrt(area * 355 / ncr);
        double phi = .5 * (1 + alpha * (lambda - .2) + lambda * lambda);
        Assert.AreEqual(ncr, axis == 1 ? result.NCr1 : result.NCr2, ncr * 1e-12);
        Assert.AreEqual(phi, axis == 1 ? result.PhiAxialBuckling1 : result.PhiAxialBuckling2, 1e-12);
        // These short members have chi=1 for both curves: capacity alone misses the input mismatch.
        Assert.AreEqual(area * 355, axis == 1 ? result.AxialBuckling1Capacity : result.AxialBuckling2Capacity,
            area * 355 * 1e-12);
    }

    private sealed class Probe : EN1993p11Checker
    {
        public Probe(SteelSection section) : base(
            new BeamElement(Point3d.Origin, new Point3d(1000, 0, 0), section),
            new Opt(Opt.LoadConditions.NotDirectlyLoaded, Opt.SupportConditions.EndsRestrained,
                Opt.LateralSupportConditions.HingesAtEnds, Opt.LateralWarpingConditions.HingesAtEnds, .5, .5),
            new StandardEN1993p11())
        {
            var forces = new ResultBeamForces(100000, 50000, 50000, 0, -100e6, 100e6, CoordinateSystem.Global);
            BeamElement.Results.Add(new BeamResult(new[] {
                new StationResultBeamForces(new LoadCase("Manufacturing comparison", LoadCase.LoadCaseTypes.SelfWeight), forces, 0) }));
        }
        public string Curve(int axis) => (axis == 1
            ? GetBucklingCurve1Axis((SteelSection)BeamElement.BeamProperty)
            : GetBucklingCurve2Axis((SteelSection)BeamElement.BeamProperty)).ToString();
    }
}
