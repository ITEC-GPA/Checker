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
public class LtbCurveTableTests
{
    // EN1993-1-1:2005 Tables6.3/6.4/6.5, printed pp.61-62.
    // Geometry boundary h/b=2 is INCLUSIVE for rolled AND welded I sections.
    [DataTestMethod]
    [DataRow(true, 399.999, "a", .21, "b", .34)]
    [DataRow(true, 400d, "a", .21, "b", .34)]
    [DataRow(true, 400.001, "b", .34, "c", .49)]
    [DataRow(false, 399.999, "c", .49, "c", .49)]
    [DataRow(false, 400d, "c", .49, "c", .49)]
    [DataRow(false, 400.001, "d", .76, "d", .76)]
    public void GeneralAndModifiedTablesAreAppliedWithoutShiftingWeldedCurves(
        bool rolled, double height, string general, double generalAlpha, string modified, double modifiedAlpha)
    {
        var p = new Probe(rolled, height);
        Assert.AreEqual(general, p.GeneralCurve());
        Assert.AreEqual(generalAlpha, p.GeneralAlpha(), 1e-12);
        Assert.AreEqual(modifiedAlpha, p.ModifiedAlpha(), 1e-12);
        p.PerformCheck();
        Assert.AreEqual(0, p.GetErrorLog().Count, string.Join("; ", p.GetErrorLog()));
        var result = p.EN1993p11BeamStationResults.Single();
        Assert.AreEqual(modified, result.LateraltorsionalBucklingCurve.ToString());
        Assert.AreEqual(modifiedAlpha, result.AlphaLTBuckling, 1e-12);
    }

    private sealed class Probe : EN1993p11Checker
    {
        private SteelSection SteelSection => (SteelSection)BeamElement.BeamProperty;
        public Probe(bool rolled, double height) : base(
            new BeamElement(Point3d.Origin, new Point3d(6000, 0, 0),
                new SteelSection(new SectionH(height, 15, 200, 25, 200, 25, "table"),
                    new SteelMaterial("S355", 210000, 355, 510),
                    rolled ? Section.SectionTypes.Rolled : Section.SectionTypes.Welded, Section.FormedTypes.HotFinished)),
            new Opt(Opt.LoadConditions.NotDirectlyLoaded, Opt.SupportConditions.HingesAtEnds,
                Opt.LateralSupportConditions.HingesAtEnds, Opt.LateralWarpingConditions.HingesAtEnds, 1, 1,
                loadApplicationPoint: Opt.LoadApplicationPoints.ShearCenter), new StandardEN1993p11())
        {
            BeamElement.Results.Add(new BeamResult(new[] {
                new StationResultBeamForces(new LoadCase("curve table", LoadCase.LoadCaseTypes.SelfWeight),
                    new ResultBeamForces(-10000, 0, 0, 0, 1e6, 0, CoordinateSystem.Global), 0) }));
        }
        public string GeneralCurve() => GetLateralTorsionalBucklingCurve(SteelSection).ToString();
        public double GeneralAlpha() => GetImperfectionFactorLT(SteelSection);
        public double ModifiedAlpha() => GetImperfectionFactorLTMod(SteelSection);
    }
}
