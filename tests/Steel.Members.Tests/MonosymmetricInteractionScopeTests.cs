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
public class MonosymmetricInteractionScopeTests
{
    public static IEnumerable<object[]> Cases =>
        from family in new[] { "T", "C", "Hmono" }
        from n in new[] { -10000d, 0d, 10000d }
        select new object[] { family, n };

    [DataTestMethod, DynamicData(nameof(Cases))]
    public void UnsupportedCombinedMemberCheckPreservesComponentsAndInvalidatesTheAggregate(string family, double n)
    {
        Section shape = family switch {
            "T" => new SectionT(400, 200, 40, 50, ""),
            "C" => new SectionC(400, 15, 200, 25, 200, 25, ""),
            _ => new SectionH(300, 15, 300, 14, 150, 14, "")
        };
        var beam = new BeamElement(Point3d.Origin, new Point3d(2000, 0, 0),
            new SteelSection(shape, new SteelMaterial("S355", 210000, 355, 510)));
        beam.Results.Add(new BeamResult(new[] {
            new StationResultBeamForces(new LoadCase("scope "+family, LoadCase.LoadCaseTypes.SelfWeight),
                new ResultBeamForces(n, 0, 0, 0, -5e6, 1e6, CoordinateSystem.Global), 0) }));
        var checker = new EN1993p11Checker(beam,
            new Opt(Opt.LoadConditions.NotDirectlyLoaded, Opt.SupportConditions.HingesAtEnds,
                Opt.LateralSupportConditions.HingesAtEnds, Opt.LateralWarpingConditions.HingesAtEnds, 1, 1,
                loadApplicationPoint: Opt.LoadApplicationPoints.ShearCenter), new StandardEN1993p11());
        checker.PerformCheck();
        Assert.AreEqual(1, checker.GetErrorLog().Count, string.Join("; ", checker.GetErrorLog()));
        StringAssert.Contains(checker.GetErrorLog().Single(), "6.3.3");
        StringAssert.Contains(checker.GetErrorLog().Single(), "doubly symmetric");
        var r = checker.EN1993p11BeamStationResults.Single();
        Assert.IsTrue(double.IsFinite(r.BendingMoment1Capacity) && r.BendingMoment1Capacity > 0);
        Assert.IsTrue(double.IsFinite(r.BendingMoment2Capacity) && r.BendingMoment2Capacity > 0);
        Assert.IsTrue(double.IsFinite(r.CrossSectionInteraction));
        Assert.IsTrue(double.IsFinite(r.MCr) && r.MCr > 0);
        Assert.IsTrue(double.IsNaN(r.WorkingRatio));
        Assert.IsTrue(double.IsNaN(r.BucklingInteraction1Axis));
        Assert.IsTrue(double.IsNaN(r.BucklingInteraction2Axis));
        Assert.IsTrue(double.IsNaN(r.Kxx) && double.IsNaN(r.Kxy) && double.IsNaN(r.Kyx) && double.IsNaN(r.Kyy));
    }
}
