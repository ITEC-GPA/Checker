using GPC.Checkers.Steel.ModelAdapters;
using GPC.Model.Sections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

/// <summary>The allocation-free plastic H solver must stay bit-identical to the former
/// allocating implementation, frozen below as captured on 27/10/2026 (Steel 2.122.0.1).</summary>
[TestClass]
public class PlasticHFrozenReferenceTests
{
    [TestMethod]
    public void AllocationFreeSolverIsBitIdenticalToTheFrozenImplementation()
    {
        var sections = new[] {
            new SectionH(300, 12, 150, 18, 150, 18, "Stocky H"), new SectionH(500, 10, 200, 16, 200, 16, "Slender web"),
            new SectionH(600, 12, 300, 25, 180, 15, "Monosymmetric"), new SectionH(400, 8, 160, 12, 260, 20, "Inverted monosymmetric") };
        int compared = 0;
        foreach (var s in sections)
        foreach (double n in new[] { -.6, -.25, 0, .1, .45 })
        foreach (var (m1, m2) in new[] { (1d, 0d), (-1d, 0d), (0d, 1d), (0d, -1d), (1d, .3), (-1d, .7), (.2, -1d), (-.5, -.5) })
        {
            double fy = 355, gamma = 1.05, area = s.LenghtTopFlange * s.ThicknessTopFlange + s.LenghtBottomFlange * s.ThicknessBottomFlange
                + s.ThicknessWeb * (s.Height - s.ThicknessTopFlange - s.ThicknessBottomFlange);
            double normal = n * area * fy / gamma, moment1 = m1 * 1e8, moment2 = m2 * 3e7;
            var actual = Ec3PlasticHSection.Evaluate(s, 1, fy, gamma, normal, moment1, moment2);
            var expected = Frozen.Evaluate(s, fy, gamma, normal, moment1, moment2);
            Assert.AreEqual(expected.M1, actual.Moment1Rd, 0, s.Name + " " + n + " " + m1 + " " + m2);
            Assert.AreEqual(expected.M2, actual.Moment2Rd, 0, s.Name + " " + n + " " + m1 + " " + m2);
            Assert.AreEqual(expected.Utilization, actual.Utilization, 0, s.Name + " " + n + " " + m1 + " " + m2);
            compared++;
        }
        Assert.AreEqual(160, compared);
    }

    // Frozen copy of Ec3PlasticHSection.Evaluate/RectangularPlasticEquilibrium before the allocation-free rewrite.
    private static class Frozen
    {
        internal static (double M1, double M2, double Utilization) Evaluate(SectionH section, double yieldStrength, double gammaM0,
            double normalForce, double moment1, double moment2)
        {
            double h = section.Height, tw = section.ThicknessWeb, bt = section.LenghtTopFlange,
                tt = section.ThicknessTopFlange, bb = section.LenghtBottomFlange, tb = section.ThicknessBottomFlange;
            double area = bt * tt + bb * tb + tw * (h - tt - tb), f = yieldStrength / gammaM0;
            double scale = Math.Max(h, Math.Max(bt, bb));
            double yc = (bb * tb * tb / 2 + tw * (h - tt - tb) * (tb + (h - tt - tb) / 2) + bt * tt * (h - tt / 2)) / area;
            var rectangles = new[] { new[] { bb / scale, -yc / scale, (tb - yc) / scale },
                new[] { tw / scale, (tb - yc) / scale, (h - tt - yc) / scale },
                new[] { bt / scale, (h - tt - yc) / scale, (h - yc) / scale } };
            double targetArea = (area - normalForce / f) / (2 * scale * scale);
            double[] q = Solve(rectangles, targetArea, moment1, moment2);
            double m1 = -2 * f * scale * scale * scale * q[2], m2 = 2 * f * scale * scale * scale * q[1];
            double utilization = Math.Abs(moment1) >= Math.Abs(moment2) ? Math.Abs(moment1 / m1) : Math.Abs(moment2 / m2);
            return (m1, m2, utilization);
        }
        private static double[] Solve(double[][] rectangles, double targetArea, double mx, double my)
        {
            double[] Evaluate(double angle)
            {
                double a = Math.Sin(angle) * (my < 0 ? -1 : 1), b = Math.Cos(angle);
                double lo = -2, hi = 2;
                for (int j = 0; j < 64; j++)
                {
                    double c = (lo + hi) / 2;
                    if (Integrate(rectangles, a, b, c)[0] > targetArea) lo = c; else hi = c;
                }
                return Integrate(rectangles, a, b, (lo + hi) / 2);
            }
            double targetAngle = Math.Atan2(Math.Abs(my), -mx), low = 0, high = Math.PI;
            for (int i = 0; i < 64; i++)
            {
                double angle = (low + high) / 2;
                double[] trial = Evaluate(angle);
                if (Math.Atan2(Math.Abs(trial[1]), trial[2]) < targetAngle) low = angle; else high = angle;
            }
            return Evaluate((low + high) / 2);
        }
        private static double[] Integrate(double[][] rectangles, double a, double b, double c)
        {
            double area = 0, qx = 0, qy = 0;
            foreach (double[] r in rectangles)
            {
                var vertices = new[] { new[] { -r[0] / 2, r[1] }, new[] { r[0] / 2, r[1] },
                    new[] { r[0] / 2, r[2] }, new[] { -r[0] / 2, r[2] } };
                var polygon = new List<double[]>(6);
                double[] previous = vertices[3]; double gp = a * previous[0] + b * previous[1] - c;
                foreach (double[] current in vertices)
                {
                    double g = a * current[0] + b * current[1] - c;
                    if ((g >= 0) != (gp >= 0))
                    {
                        double fraction = gp / (gp - g);
                        polygon.Add(new[] { previous[0] + fraction * (current[0] - previous[0]),
                            previous[1] + fraction * (current[1] - previous[1]) });
                    }
                    if (g >= 0) polygon.Add(current);
                    previous = current; gp = g;
                }
                for (int i = 0; i < polygon.Count; i++)
                {
                    double[] p = polygon[i], q = polygon[(i + 1) % polygon.Count];
                    double cross = p[0] * q[1] - q[0] * p[1];
                    area += cross / 2; qx += (p[0] + q[0]) * cross / 6; qy += (p[1] + q[1]) * cross / 6;
                }
            }
            return new[] { area, qx, qy };
        }
    }
}
