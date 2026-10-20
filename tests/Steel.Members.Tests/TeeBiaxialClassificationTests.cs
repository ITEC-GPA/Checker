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

namespace Steel.Members.Tests;

[TestClass]
public class TeeBiaxialClassificationTests
{
    public static IEnumerable<object[]> EquilibriumCases =>
        from scale in new[] { .1, 10d }
        from mirror in new[] { -1d, 1d }
        from degrees in new[] { 10d, 45d, 85d, 95d, 135d, 170d }
        from ordinate in new[] { 100d, 200d, 300d }
        select new object[] { scale, mirror, degrees, ordinate };

    [DataTestMethod, DynamicData(nameof(EquilibriumCases))]
    public void PlasticPlaneRecoversIndependentStripEquilibrium(double scale, double mirror, double degrees, double ordinate)
    {
        var tee = new SectionT(400 * scale, 200 * scale, 40 * scale, 50 * scale, "");
        double angle = degrees * Math.PI / 180, a = mirror * Math.Sin(angle), b = Math.Cos(angle), c = b * ordinate * scale;
        double[] q = StripIntegration(tee, a, b, c);
        // Independent stress resultants, compression positive half-plane:
        // N=fy*(A-2Ac), Mx=-2fy*Qy, My=2fy*Qx.
        double[] actual = Probe.Plane(tee, q[0], -710 * q[2], 710 * q[1]);
        double norm = Math.Sqrt(actual[0] * actual[0] + actual[1] * actual[1]);
        Assert.AreEqual(a, actual[0] / norm, 1e-9);
        Assert.AreEqual(b, actual[1] / norm, 1e-9);
        Assert.AreEqual(c, actual[2] / norm, 400 * scale * 1e-9);
    }

    public static IEnumerable<object[]> SmallSecondaryMoments =>
        from n in new[] { -100000d, 0d, 100000d }
        from sign in new[] { -1d, 1d }
        from secondary in new[] { -100d, 100d }
        select new object[] { n, sign, secondary };

    [DataTestMethod, DynamicData(nameof(SmallSecondaryMoments))]
    public void SmallSecondaryMomentDoesNotRestoreUniformCompressionClassification(double n, double sign, double secondary)
    {
        // Table5.2 limits are far from the boundary for these dimensions:
        // the uniaxial limiting classes are1(top compressed)/2(tip compressed).
        var p = new Probe(new SectionT(400, 200, 40, 50, ""));
        Assert.AreEqual(sign < 0 ? 1 : 2, p.Class(n, sign * 100e6, secondary));
    }

    [DataTestMethod]
    [DataRow(-100d, 3)] [DataRow(100d, 3)]
    public void SlenderStemUsesItsElasticGradientWithBothMoments(double secondary, int expected)
    {
        // Independent uniaxial limit: c/t=380/27=14.074, ksigma=.7049788738,
        // limit3=14.3458769252, so the tiny perturbation stays in class3.
        Assert.AreEqual(expected, new Probe(new SectionT(400, 200, 27, 20, "")).Class(0, 1e6, secondary));
    }

    [DataTestMethod]
    [DataRow(-100d)] [DataRow(100d)]
    public void FullyTensileSlenderFlangeIsOmittedUnderBiaxialBending(double secondary)
    {
        Assert.AreEqual(1, new Probe(new SectionT(300, 400, 40, 10, "")).Class(0, 100e6, secondary));
    }

    [DataTestMethod]
    [DataRow(-100e6)] [DataRow(100e6)]
    public void OriginalLegacyKeepsClass3WithTheThroughThicknessEnvelope(double secondary)
    {
        // A mid-line-only plastic alpha~.411 would miss compression across
        // the thick web. The conservative face envelope keeps alpha=1 there.
        Assert.AreEqual(3, new Probe(new SectionT(400, 200, 40, 50, "")).Class(100000, -100e6, secondary));
    }

    [DataTestMethod]
    [DataRow(-100d)] [DataRow(100d)]
    public void WideTeeMapsBothPrincipalMomentsToGeometricAxes(double secondary)
    {
        // Axis1=-Y, axis2=X; slender stem is tensile, stocky flange compressed.
        Assert.AreEqual(1, new Probe(new SectionT(160, 500, 8, 40, "")).Class(0, secondary, -100e6));
    }

    private static double[] StripIntegration(SectionT tee, double a, double b, double c)
    {
        // Test-only integration by horizontal strips, split at every plane/
        // rectangle intersection; 2-point Gauss is exact on each interval.
        // This does not clip polygons or use the production shoelace formula.
        double area = 0, qx = 0, qy = 0;
        double hw = tee.HeightWeb, tf = tee.ThicknessFlange, w = tee.LenghtFlange;
        double yc = (tee.ThicknessWeb * hw * hw / 2 + w * tf * (hw + tf / 2)) /
            (tee.ThicknessWeb * hw + w * tf);
        foreach (var rect in new[] { (tee.ThicknessWeb, 0d, hw), (w, hw, tee.Height) })
        {
            var cuts = new List<double> { rect.Item2, rect.Item3 };
            foreach (double x in new[] { -rect.Item1 / 2, rect.Item1 / 2 })
            {
                double y = (c - a * x) / b;
                if (y > rect.Item2 && y < rect.Item3) cuts.Add(y);
            }
            cuts.Sort();
            for (int j = 1; j < cuts.Count; ++j)
                foreach (double node in new[] { -1 / Math.Sqrt(3), 1 / Math.Sqrt(3) })
                {
                    double dy = (cuts[j] - cuts[j - 1]) / 2;
                    double y = (cuts[j] + cuts[j - 1]) / 2 + dy * node;
                    double cut = Math.Clamp((c - b * y) / a, -rect.Item1 / 2, rect.Item1 / 2);
                    double xl = a > 0 ? cut : -rect.Item1 / 2, xr = a > 0 ? rect.Item1 / 2 : cut;
                    area += (xr - xl) * dy;
                    qx += (xr * xr - xl * xl) * dy / 2;
                    qy += (y - yc) * (xr - xl) * dy;
                }
        }
        return new[] { area, qx, qy };
    }

    private sealed class Probe : EN1993p11Checker
    {
        private SteelSection SteelSection => (SteelSection)BeamElement.BeamProperty;
        public Probe(SectionT shape) : base(new BeamElement(Point3d.Origin, new Point3d(1000, 0, 0),
            new SteelSection(shape, new SteelMaterial("S355", 210000, 355, 510))),
            new Opt(Opt.LoadConditions.NotDirectlyLoaded, Opt.SupportConditions.HingesAtEnds,
                Opt.LateralSupportConditions.HingesAtEnds, Opt.LateralWarpingConditions.HingesAtEnds, 1, 1),
            new StandardEN1993p11()) { }
        public int Class(double n, double m1, double m2) =>
            (int)CalculateSectionClassDueToCombinedBendingAndCompression(new ResultBeamForces(n, 0, 0, 0, m1, m2, CoordinateSystem.Global), SteelSection);
        public static double[] Plane(SectionT t, double ac, double mx, double my) => CalculateTeePlasticStressPlane(t, ac, mx, my);
    }
}
