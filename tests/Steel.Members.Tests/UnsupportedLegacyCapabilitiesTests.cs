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

// Negative capability tests for the two still-active legacy comparisons.
// Passing these tests means refusal is explicit, NOT that resistance is implemented.
[TestClass]
public class UnsupportedLegacyCapabilitiesTests
{
    [DataTestMethod]
    [DataRow("Sap_SectionHAsymmetricTest2", "MN,Rd", "not implemented")]
    [DataRow("Sap_SectionLTest", "McrLT", "without symmetry")]
    public void UnavailableCheckHasAnExplicitDiagnosticAndCannotAppearVerified(
        string legacyName, string mechanism, string reason)
    {
        bool angle = legacyName == "Sap_SectionLTest";
        Section shape = angle ? new SectionL(350, 80, 350, 40, "legacy angle")
            : new SectionH(300, 25, 300, 25, 150, 25, "legacy asymmetric H");
        var section = new SteelSection(shape, new SteelMaterial("S355", 210000, 355, 510));
        var beam = new BeamElement(Point3d.Origin, new Point3d(angle ? 1000 : 12000, 0, 0), section);
        var forces = new ResultBeamForces(angle ? 100000 : -100000,
            50000, angle ? -50000 : 50000, 0, -100e6, 100e6, CoordinateSystem.Global);
        beam.Results.Add(new BeamResult(new[] { new StationResultBeamForces(
            new LoadCase(legacyName, LoadCase.LoadCaseTypes.SelfWeight), forces, 0) }));
        var checker = new EN1993p11Checker(beam,
            new Opt(Opt.LoadConditions.NotDirectlyLoaded, Opt.SupportConditions.EndsRestrained,
                Opt.LateralSupportConditions.EndsRestrained, Opt.LateralWarpingConditions.EndsRestrained,
                .5, .5, loadApplicationPoint: Opt.LoadApplicationPoints.ShearCenter),
            new StandardEN1993p11());
        checker.PerformCheck();
        Assert.AreEqual(1, checker.GetErrorLog().Count);
        string error = checker.GetErrorLog().Single().ToString();
        StringAssert.Contains(error, legacyName);
        StringAssert.Contains(error, mechanism);
        StringAssert.Contains(error, reason);
        var result = checker.EN1993p11BeamStationResults.Single();
        Assert.IsTrue(double.IsNaN(result.WorkingRatio), "An unavailable check cannot have a passing aggregate.");
        Assert.IsTrue(double.IsNaN(result.BucklingInteraction1Axis));
        Assert.IsTrue(double.IsNaN(result.BucklingInteraction2Axis));
        Assert.IsTrue(double.IsNaN(result.BendingMoment1Capacity));
    }
}
