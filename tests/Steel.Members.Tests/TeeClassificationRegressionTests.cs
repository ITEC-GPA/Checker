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
// References and classification scope: TEE_CLASSIFICATION.txt.
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
    public void PublicCheckPreservesPlasticResistanceAndRefusesUnqualifiedMemberInteraction(double moment)
    {
        var p = LegacyProbe();
        p.AddLoad(Force(100000, moment));
        p.PerformCheck();
        Assert.AreEqual(1, p.GetErrorLog().Count, string.Join("; ", p.GetErrorLog()));
        StringAssert.Contains(p.GetErrorLog().Single(), "6.3.3");
        var result = p.EN1993p11BeamStationResults.Single();
        Assert.AreEqual(moment < 0 ? 1 : 2, (int)result.BendingCompressionClass);
        // Independent integration about the plastic neutral axis of the two rectangles.
        Assert.AreEqual(923e6, result.BendingMoment1Capacity, 1e-5);
        Assert.AreEqual(227.2e6, result.BendingMoment2Capacity, 1e-5);
        Assert.IsTrue(double.IsNaN(result.WorkingRatio));
    }

    [TestMethod]
    public void BiaxialLegacyRemainsClass3WithThroughThicknessEnvelope()
    {
        var p = LegacyProbe();
        var forces = new ResultBeamForces(100000, 50000, -50000, 0, -100e6, 100e6, CoordinateSystem.Global);
        Assert.AreEqual(3, p.Bending(forces));
        Assert.AreEqual(3, p.Combined(forces));
        // The conservative envelope over the web faces still requires class 3.
        // The inverse equilibrium benchmarks are in TeeBiaxialClassificationTests.
    }

    [TestMethod]
    public void FullyCompressedSectionStillUsesTheCompressionClass()
    {
        var p = LegacyProbe();
        Assert.AreEqual(3, p.Bending(Force(-8e6, 1)));
        Assert.AreEqual(3, p.Combined(Force(-8e6, 1)));
        Assert.AreEqual(1, p.Bending(Force(8e6, 1)));
    }

    public static IEnumerable<object[]> ElasticBoundaries
    {
        get
        {
            // Tabulated k_sigma (JRC/ECCS p.50); intermediate values evaluated
            // independently. Root and tip stresses are compression positive.
            double[][] rows = {
                new[] { 1d, 1d, 0d }, // uniform: 14 epsilon from Table 5.2
                new[] { .5, 1d, .4825 },
                new[] { 0d, 1d, .57 },
                new[] { -1d, 1d, .85 },
                new[] { -3d, 1d, 1.83 },
                new[] { -4d, 1d, 1.83 }, // conservative domain endpoint
                new[] { 1d, .5, .6880952380952381 },
                new[] { 1d, 0d, 1.70 },
                new[] { 1d, -.5, 8.475 },
                new[] { 1d, -1d, 23.8 },
                new[] { 1d, -2d, 23.8 } // conservative domain endpoint
            };
            foreach (double fy in new[] { 235d, 355d })
                foreach (double[] row in rows)
                    foreach (double offset in new[] { -1e-6, 1e-6 })
                        yield return new object[] { fy, row[0], row[1], row[2], offset };
        }
    }

    [DataTestMethod, DynamicData(nameof(ElasticBoundaries))]
    public void Class3LimitUsesTheElasticEdgeRatioAndItsSupportedDomain(
        double fy, double root, double tip, double kSigma, double offset)
    {
        double limit = (kSigma == 0 ? 14 : 21 * Math.Sqrt(kSigma)) * Math.Sqrt(235 / fy);
        var p = new Probe(new SectionT(400, 200, 40, 50, ""), fy);
        Assert.AreEqual(offset < 0 ? 3 : 4,
            p.Outstand(limit + offset, 1, 1, tip >= root, root, tip));
    }

    [TestMethod]
    public void SciPublishedSlenderTeeStemRemainsClass4()
    {
        // Brown, SCI/NSC Technical Digest 2019, p.21, Method 3.
        // Published dimensions/stresses; the primitive does not reconstruct
        // the rolled corners or qualify the article's LTB/effective section.
        var p = new Probe(new SectionT(400, 200, 40, 50, ""), 355);
        Assert.AreEqual(4, p.Outstand(227.2 - 13.3 - 10.2, 8.1, 1, true, -34.9, 168.8));
    }

    [DataTestMethod]
    [DataRow(-1d, 1)] [DataRow(1d, 4)]
    public void WideTeeMapsGeometricXBendingToPrincipalAxis2(double sign, int expected)
    {
        // Iyy>Ixx, principal axis1=-Y and axis2=X. The flange is class1;
        // compressed stem has c/t=15 and k_sigma~0.546, hence class4.
        var shape = new SectionT(160, 500, 8, 40, "");
        Assert.IsTrue(shape.Jyy > shape.Jxx);
        Assert.AreEqual(-Math.PI / 2, shape.AngleX1, 1e-12);
        var p = new Probe(shape, 355);
        var force = new ResultBeamForces(0, 0, 0, 0, 0, sign * 1e6, CoordinateSystem.Global);
        Assert.AreEqual(expected, p.Bending(force));
        Assert.AreEqual(expected, p.Combined(force));
    }

    [DataTestMethod]
    [DataRow(1d, 1)] [DataRow(-1d, 4)]
    public void SlenderFlangeIsOmittedOnlyWhenFullyTensile(double sign, int expected)
    {
        var p = new Probe(new SectionT(300, 400, 40, 10, ""), 355);
        Assert.AreEqual(expected, p.Bending(Force(0, sign * 1e6)));
        Assert.AreEqual(expected, p.Combined(Force(0, sign * 1e6)));
    }

    [DataTestMethod]
    [DataRow(1d, 2)] [DataRow(1.2, 3)]
    public void PlasticAxialEquilibriumUsesGammaM0(double gamma, int expected)
    {
        // Ac=(24000+700000*gamma/355)/2.
        // alpha=0.927565... / 0.941649...; class2 limit at alpha=0.929847... .
        var p = new Probe(new SectionT(400, 200, 40, 50, ""), 355, gamma);
        Assert.AreEqual(expected, p.Bending(Force(-700000, 100e6)));
    }

    [DataTestMethod]
    [DataRow(-100e6)] [DataRow(100e6)]
    public void WorkedCornersRetainTheUnqualifiedConservativeFallback(double moment)
    {
        var p = new Probe(new SectionT(400, 200, 40, 50, "", 10), 355);
        Assert.AreEqual(3, p.Bending(Force(0, moment)));
    }

    [TestMethod]
    public void Class4PublicCheckStillRequiresEffectiveProperties()
    {
        var p = new Probe(new SectionT(400, 200, 20, 20, ""), 355);
        p.AddLoad(Force(0, 1e6));
        p.PerformCheck();
        Assert.IsTrue(p.GetErrorLog().Any(e => e.Contains("Class 4 requires verified effective section properties")));
        Assert.IsTrue(double.IsNaN(p.EN1993p11BeamStationResults.Single().WorkingRatio));
    }

    private static Probe LegacyProbe() => new(new SectionT(400, 200, 40, 50, ""), 355);
    private static ResultBeamForces Force(double n, double mx) => new(n, 0, 0, 0, mx, 0, CoordinateSystem.Global);

    private sealed class Probe : EN1993p11Checker
    {
        private SteelSection Section => (SteelSection)BeamElement.BeamProperty;
        public Probe(SectionT shape, double fy, double gamma = 1) : base(
            new BeamElement(Point3d.Origin, new Point3d(1000, 0, 0),
                new SteelSection(shape, new SteelMaterial("Reference", 210000, fy, 510))),
            // Match the SAP case's restraint/load setup; the former hinge +
            // end-moments setup has an independent unimplemented C1/C3 route.
            new Opt(Opt.LoadConditions.NotDirectlyLoaded, Opt.SupportConditions.EndsRestrained,
                Opt.LateralSupportConditions.EndsRestrained, Opt.LateralWarpingConditions.EndsRestrained, .5, .5,
                loadApplicationPoint: Opt.LoadApplicationPoints.ShearCenter),
            new StandardEN1993p11 { GammaM0 = gamma }) { }
        public int Bending(ResultBeamForces f) => (int)CalculateSectionClassDueToBending(f, Section);
        public int Combined(ResultBeamForces f) => (int)CalculateSectionClassDueToCombinedBendingAndCompression(f, Section);
        public int Compression(ResultBeamForces f) => (int)CalculateSectionClassDueToCompression(f, Section);
        public int Outstand(double c, double t, double alpha, bool tipCompressed, double root, double tip)
            => (int)GetClassBendingOuterPlate(c, t, alpha, tipCompressed, root, tip);
        public void AddLoad(ResultBeamForces f) => BeamElement.Results.Add(new BeamResult(new[] {
            new StationResultBeamForces(new LoadCase("T classification", LoadCase.LoadCaseTypes.SelfWeight), f, 0) }));
    }
}
