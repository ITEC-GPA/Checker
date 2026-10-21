using GPC.Checkers.Steel.ModelAdapters;
using GPC.Model.Sections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class PlasticHResistanceTests
{
    public static IEnumerable<object[]> Cases =>
        from scale in new[] { .1, 1d, 10d }
        from mirror in new[] { false, true }
        from angle in new[] { 10d, 45d, 85d, 95d, 135d, 170d }
        from ordinate in new[] { 20d, 120d, 280d }
        from sign in new[] { -1d, 1d }
        select new object[] { scale, mirror, angle, ordinate, sign };

    [DataTestMethod, DynamicData(nameof(Cases))]
    public void BoundaryMatchesIndependentStressBlockIntegration(double scale, bool mirror, double angle, double ordinate, double sign)
    {
        // EC3 6.2.9.1(2): equilibrium of a fully plastic stress distribution.
        // Independent horizontal-strip Gauss integration, no polygon clipping,
        // no production neutral-axis solution, no copied Model section properties.
        var h = Shape(scale, mirror);
        double a = sign * Math.Sin(angle * Math.PI / 180), b = Math.Cos(angle * Math.PI / 180);
        var q = Integrate(h, a, b, b * ordinate * scale);
        double area = h.LenghtBottomFlange * h.ThicknessBottomFlange + h.LenghtTopFlange * h.ThicknessTopFlange + h.ThicknessWeb * h.HeightWeb;
        double n = 355 * (area - 2 * q[0]), m1 = -710 * q[2], m2 = 710 * q[1];
        var r = Ec3PlasticHSection.Evaluate(h, 1, 355, 1, n, m1, m2);
        Assert.AreEqual(1, r.Utilization, 2e-8);
        Assert.AreEqual(m1, r.Moment1Rd, Math.Max(Math.Abs(m1), area * 355 * h.Height) * 2e-9);
        Assert.AreEqual(m2, r.Moment2Rd, Math.Max(Math.Abs(m2), area * 355 * h.Width) * 2e-9);
        Assert.AreEqual(.8, Ec3PlasticHSection.Evaluate(h, 2, 355, 1, n, .8 * m1, .8 * m2).Utilization, 2e-8);
    }

    [DataTestMethod]
    [DataRow(-.99)] [DataRow(-.7)] [DataRow(-.1)] [DataRow(0d)] [DataRow(.1)] [DataRow(.7)] [DataRow(.99)]
    public void UniaxialCapacitiesRetainSignsAndMirrorSymmetry(double ratio)
    {
        var h = Shape(1, false); var mirror = Shape(1, true);
        double n = h.Area * 355 * ratio;
        foreach (double sign in new[] { -1d, 1d })
        {
            var r = Ec3PlasticHSection.Evaluate(h, 1, 355, 1, n, sign, 0);
            var reflected = Ec3PlasticHSection.Evaluate(mirror, 1, 355, 1, n, -sign, 0);
            Assert.AreEqual(-r.Moment1Rd, reflected.Moment1Rd, Math.Abs(r.Moment1Rd) * 1e-8);
            var reverse = Ec3PlasticHSection.Evaluate(h, 1, 355, 1, -n, -sign, 0);
            Assert.AreEqual(-r.Moment1Rd, reverse.Moment1Rd, Math.Abs(r.Moment1Rd) * 1e-8);
            var minor = Ec3PlasticHSection.Evaluate(h, 1, 355, 1, n, 0, sign);
            Assert.AreEqual(0, minor.Moment1Rd, h.Area * 355 * h.Height * 1e-9);
            Assert.AreEqual(sign, Math.Sign(minor.Moment2Rd));
        }
    }

    [TestMethod]
    public void PureBendingMatchesAnalyticalPlasticModuliAndPartialFactor()
    {
        var h = new SectionH(300, 25, 300, 25, 150, 25, "");
        // Horizontal PNA: 225 mm; Wpl,x=1,796,875 mm3 (three rectangle integrals).
        // Vertical PNA: x=0; Wpl,y=sum(t*b^2/4)=742,187.5 mm3.
        foreach (double sign in new[] { -1d, 1d })
        {
            Assert.AreEqual(sign * 1796875 * 355 / 1.05,
                Ec3PlasticHSection.Evaluate(h, 1, 355, 1.05, 0, sign, 0).Moment1Rd, 1e-5);
            Assert.AreEqual(sign * 742187.5 * 355 / 1.05,
                Ec3PlasticHSection.Evaluate(h, 1, 355, 1.05, 0, 0, sign).Moment2Rd, 1e-5);
        }
    }

    [TestMethod]
    public void AxialLimitsCannotPassWithAdditionalMoments()
    {
        var h = Shape(1, false); double npl = h.Area * 355;
        Assert.AreEqual(0, Ec3PlasticHSection.Evaluate(h, 1, 355, 1, 0, 0, 0).Utilization);
        foreach (double n in new[] { npl, -npl, 2 * npl })
        {
            Assert.AreEqual(Math.Abs(n) / npl, Ec3PlasticHSection.Evaluate(h, 1, 355, 1, n, 0, 0).Utilization);
            Assert.IsFalse(Ec3PlasticHSection.Evaluate(h, 1, 355, 1, n, 1, 0).IsSatisfied);
        }
    }

    [TestMethod]
    public void RejectsUnqualifiedGeometryClassShearTorsionAndInvalidNumbers()
    {
        var h = Shape(1, false);
        Assert.ThrowsException<NotSupportedException>(() => Ec3PlasticHSection.Evaluate(h, 3, 355, 1, 0, 1, 0));
        Assert.ThrowsException<NotSupportedException>(() => Ec3PlasticHSection.Evaluate(h, 1, 355, 1, 0, 1, 0, .50001));
        Assert.ThrowsException<NotSupportedException>(() => Ec3PlasticHSection.Evaluate(h, 1, 355, 1, 0, 1, 0, torsion: 1));
        Assert.ThrowsException<NotSupportedException>(() => Ec3PlasticHSection.Evaluate(new SectionH(300, 25, 300, 25, 150, 25, "", 10), 1, 355, 1, 0, 1, 0));
        Assert.ThrowsException<ArgumentException>(() => Ec3PlasticHSection.Evaluate(h, 1, 355, 1, double.NaN, 1, 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Ec3PlasticHSection.Evaluate(h, 1, 355, 0, 0, 1, 0));
        Assert.IsTrue(Ec3PlasticHSection.Evaluate(h, 1, 355, 1, 0, 1, 0, -.5, .5).IsSatisfied);
    }

    private static SectionH Shape(double s, bool mirror) => new(300*s, 25*s,
        (mirror?150:300)*s, (mirror?20:25)*s, (mirror?300:150)*s, (mirror?25:20)*s, "");

    private static double[] Integrate(SectionH h, double a, double b, double c)
    {
        double area = 0, qx = 0, qy = 0;
        var rects = new[] { (h.LenghtBottomFlange, 0d, h.ThicknessBottomFlange),
            (h.ThicknessWeb, h.ThicknessBottomFlange, h.Height-h.ThicknessTopFlange),
            (h.LenghtTopFlange, h.Height-h.ThicknessTopFlange, h.Height) };
        double total = rects.Sum(r => r.Item1*(r.Item3-r.Item2));
        double yc = rects.Sum(r => r.Item1*(r.Item3*r.Item3-r.Item2*r.Item2)/2)/total;
        foreach (var r in rects)
        {
            var cuts = new List<double> { r.Item2, r.Item3 };
            foreach (double x in new[] { -r.Item1/2, r.Item1/2 })
            {
                double y = (c-a*x)/b;
                if (y > r.Item2 && y < r.Item3) cuts.Add(y);
            }
            cuts.Sort();
            for (int j=1; j<cuts.Count; ++j)
                foreach (double node in new[] { -1/Math.Sqrt(3), 1/Math.Sqrt(3) })
                {
                    double dy=(cuts[j]-cuts[j-1])/2, y=(cuts[j]+cuts[j-1])/2+dy*node;
                    double cut=Math.Clamp((c-b*y)/a, -r.Item1/2, r.Item1/2);
                    double xl=a>0?cut:-r.Item1/2, xr=a>0?r.Item1/2:cut;
                    area+=(xr-xl)*dy; qx+=(xr*xr-xl*xl)*dy/2; qy+=(y-yc)*(xr-xl)*dy;
                }
        }
        return new[] { area, qx, qy };
    }
}
