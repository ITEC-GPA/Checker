using GPC.Checkers.Steel.Checkers;
using GPC.Geometry;
using GPC.Model;
using GPC.Model.Data.Steel;
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

// N-mm-MPa, tension positive. Mx > 0 compresses the bottom/free end of the stem.
// References and the intentionally unqualified biaxial case: TEE_CLASSIFICATION.txt.
[TestClass]
public class TeeClassificationRegressionTests
{
    public static IEnumerable<object[]> PlasticBoundaries =>
        from fy in new[] { 235d, 355d }
        from tipCompressed in new[] { false, true }
        from areaFraction in new[] { .6, .75 }
        from boundary in new[] { 1, 2 }
        from offset in new[] { -1e-6, 1e-6 }
        select new object[] { fy, tipCompressed, areaFraction, boundary, offset };

    [DataTestMethod, DynamicData(nameof(PlasticBoundaries))]
    public void PlasticNeutralAxisUsesAreaEquilibriumAndTheCorrectOutstandLimit(
        double fy, bool tipCompressed, double areaFraction, int boundary, double offset)
    {
        // Aflange=(2q-1)Aweb -> half the total area is q*Aweb.
        // Thus alpha=q for a compressed tip, and alpha=1-q for a tension tip.
        // The flange width is four times its thickness: always class 1.
        double alpha = tipCompressed ? areaFraction : 1 - areaFraction;
        double multiplier = tipCompressed ? alpha : Math.Pow(alpha, 1.5);
        double ratio = (boundary == 1 ? 9 : 10) * Math.Sqrt(235 / fy) / multiplier + offset;
        double tw = 10, c = ratio * tw;
        double flangeArea = (2 * areaFraction - 1) * c * tw;
        double tf = Math.Sqrt(flangeArea / 4), b = 4 * tf;
        var p = new Probe(new SectionT(c + tf, b, tw, tf, ""), fy);
        var forces = Force(0, tipCompressed ? 1e6 : -1e6);
        int expected = offset < 0 ? boundary : boundary + 1;
        Assert.AreEqual(expected, p.Bending(forces));
        Assert.AreEqual(expected, p.Combined(forces));
    }

    [DataTestMethod]
    [DataRow(-100000d, -100e6, 1)]
    [DataRow(0d, -100e6, 1)]
    [DataRow(100000d, -100e6, 1)]
    [DataRow(-100000d, 100e6, 2)]
    [DataRow(0d, 100e6, 2)]
    [DataRow(100000d, 100e6, 2)]
    public void LegacyGeometryWithOneMomentHasDifferentClassesInTheTwoDirections(
        double n, double moment, int expected)
    {
        // A=24000, Aflange=10000, Aweb=14000, c/t=8.75.
        // Ac=(24000-N/355)/2. With N=100kN:
        // alpha(root compressed)=0.13279678068410455;
        // alpha(tip compressed)=0.8470824949698189.
        var p = LegacyProbe();
        Assert.AreEqual(expected, p.Bending(Force(n, moment)));
        Assert.AreEqual(expected, p.Combined(Force(n, moment)));
        Assert.AreEqual(3, p.Compression(Force(-100000, 0)));
    }

    [DataTestMethod]
    [DataRow(20d, 4)] [DataRow(27d, 3)] [DataRow(30d, 3)] [DataRow(32d, 2)]
    public void ElasticStressGradientDistinguishesClass3FromClass4(double tw, int expected)
    {
        // Two rectangles, h=400,b=200,tf=20; free tip compressed.
        // At tw=27: alpha=7130/10260, psi=-0.544081609391919,
        // k=0.704978873809798, c/t=14.074... < 21*epsilon*sqrt(k)=14.345876925196903.
        var p = new Probe(new SectionT(400, 200, tw, 20, ""), 355);
        Assert.AreEqual(expected, p.Bending(Force(0, 1e6)));
        Assert.AreEqual(expected, p.Combined(Force(0, 1e6)));
    }

    [DataTestMethod]
    [DataRow(-100e6)] [DataRow(100e6)]
    public void PublicCheckUsesPlasticResistanceForBothQualifiedMomentDirections(double moment)
    {
        var p = LegacyProbe();
        p.AddLoad(Force(100000, moment));
        p.PerformCheck();
        Assert.AreEqual(0, p.GetErrorLog().Count, string.Join("; ", p.GetErrorLog()));
        var result = p.EN1993p11BeamStationResults.Single();
        Assert.AreEqual(moment < 0 ? 1 : 2, (int)result.BendingCompressionClass);
        // Independent integration about the plastic neutral axis of the two rectangles.
        Assert.AreEqual(923e6, result.BendingMoment1Capacity, 1e-5);
        Assert.AreEqual(227.2e6, result.BendingMoment2Capacity, 1e-5);
    }

    [TestMethod]
    public void BiaxialLegacyRemainsOnTheConservativeFallback()
    {
        var p = LegacyProbe();
        var forces = new ResultBeamForces(100000, 50000, -50000, 0, -100e6, 100e6, CoordinateSystem.Global);
        Assert.AreEqual(3, p.Bending(forces));
        Assert.AreEqual(3, p.Combined(forces));
        // This is a scope guard, not independent validation of biaxial classification.
    }

    [TestMethod]
    public void FullyCompressedSectionStillUsesTheCompressionClass()
    {
        var p = LegacyProbe();
        Assert.AreEqual(3, p.Bending(Force(-8e6, 1)));
        Assert.AreEqual(3, p.Combined(Force(-8e6, 1)));
        Assert.AreEqual(1, p.Bending(Force(8e6, 1)));
    }

    private static Probe LegacyProbe() => new(new SectionT(400, 200, 40, 50, ""), 355);
    private static ResultBeamForces Force(double n, double mx) => new(n, 0, 0, 0, mx, 0, CoordinateSystem.Global);

    private sealed class Probe : EN1993p11Checker
    {
        private SteelSection Section => (SteelSection)BeamElement.BeamProperty;
        public Probe(SectionT shape, double fy) : base(
            new BeamElement(Point3d.Origin, new Point3d(1000, 0, 0),
                new SteelSection(shape, new SteelMaterial("Reference", 210000, fy, 510))),
            new Opt(Opt.LoadConditions.NotDirectlyLoaded, Opt.SupportConditions.HingesAtEnds,
                Opt.LateralSupportConditions.HingesAtEnds, Opt.LateralWarpingConditions.HingesAtEnds, .5, .5),
            new StandardEN1993p11()) { }
        public int Bending(ResultBeamForces f) => (int)CalculateSectionClassDueToBending(f, Section);
        public int Combined(ResultBeamForces f) => (int)CalculateSectionClassDueToCombinedBendingAndCompression(f, Section);
        public int Compression(ResultBeamForces f) => (int)CalculateSectionClassDueToCompression(f, Section);
        public void AddLoad(ResultBeamForces f) => BeamElement.Results.Add(new BeamResult(new[] {
            new StationResultBeamForces(new LoadCase("T classification", LoadCase.LoadCaseTypes.SelfWeight), f, 0) }));
    }
}
