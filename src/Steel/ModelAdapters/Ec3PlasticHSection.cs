using System;
using System.Collections.Generic;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.ModelAdapters
{
    /// <summary>EN 1993-1-1:2005 6.2.9.1, plastic stress equilibrium for sharp
    /// parallel-flange H sections, class 1/2, low shear and no torsion.
    /// N is positive in tension; M1=integral(sigma*y), M2=-integral(sigma*x),
    /// relative to the centroid. Units: N, mm, MPa, Nmm. Axis 1 must be horizontal.
    /// Classification and shear buckling are separate checks.</summary>
    public static class Ec3PlasticHSection
    {
        public static PlasticMomentResistance Evaluate(SectionH section, int sectionClass,
            double yieldStrength, double gammaM0, double normalForce, double moment1, double moment2,
            double shearRatio1 = 0, double shearRatio2 = 0, double torsion = 0)
        {
            if (section == null) throw new ArgumentNullException(nameof(section));
            if (sectionClass != 1 && sectionClass != 2)
                throw new NotSupportedException("Plastic H equilibrium requires class 1 or 2.");
            if (section.GetType() != typeof(SectionH) || section.R != 0 || Math.Abs(section.AngleX1) > 1e-10)
                throw new NotSupportedException("Plastic H equilibrium requires sharp parallel flanges and horizontal principal axis 1.");
            Positive(yieldStrength, nameof(yieldStrength)); Positive(gammaM0, nameof(gammaM0));
            foreach (double v in new[] { normalForce, moment1, moment2, shearRatio1, shearRatio2, torsion }) Finite(v);
            if (Math.Abs(shearRatio1) > .5 || Math.Abs(shearRatio2) > .5 || torsion != 0)
                throw new NotSupportedException("Plastic H equilibrium currently requires low shear (both |V/Vrd|<=0.5) and zero torsion.");
            double h = section.Height, tw = section.ThicknessWeb, bt = section.LenghtTopFlange,
                tt = section.ThicknessTopFlange, bb = section.LenghtBottomFlange, tb = section.ThicknessBottomFlange;
            foreach (double v in new[] { h, tw, bt, tt, bb, tb, h - tt - tb }) Positive(v, "geometry");
            if (tw > Math.Min(bt, bb)) throw new ArgumentException("Web must fit inside both flanges.");
            double area = bt * tt + bb * tb + tw * (h - tt - tb), f = yieldStrength / gammaM0;
            double npl = area * f, scale = Math.Max(h, Math.Max(bt, bb));
            Positive(npl, "NplRd"); Positive(npl * scale, "moment scale");
            double n = Math.Abs(normalForce) / npl;
            if (moment1 == 0 && moment2 == 0) return new PlasticMomentResistance(npl, 0, 0, n);
            if (n >= 1) return new PlasticMomentResistance(npl, 0, 0, double.PositiveInfinity);
            double yc = (bb * tb * tb / 2 + tw * (h - tt - tb) * (tb + (h - tt - tb) / 2)
                + bt * tt * (h - tt / 2)) / area;
            var rectangles = new[] { new[] { bb / scale, -yc / scale, (tb - yc) / scale },
                new[] { tw / scale, (tb - yc) / scale, (h - tt - yc) / scale },
                new[] { bt / scale, (h - tt - yc) / scale, (h - yc) / scale } };
            double targetArea = (area - normalForce / f) / (2 * scale * scale);
            double[] q = RectangularPlasticEquilibrium.Solve(rectangles, targetArea, moment1, moment2);
            double m1 = -2 * f * scale * scale * scale * q[2];
            double m2 = 2 * f * scale * scale * scale * q[1];
            // The computed boundary is parallel to the requested moment vector.
            double utilization = Math.Abs(moment1) >= Math.Abs(moment2) ? Math.Abs(moment1 / m1) : Math.Abs(moment2 / m2);
            return new PlasticMomentResistance(npl, m1, m2, utilization);
        }

        private static void Finite(double v)
        { if (double.IsNaN(v) || double.IsInfinity(v)) throw new ArgumentException("Finite input required."); }
        private static void Positive(double v, string name)
        { if (!(v > 0) || double.IsInfinity(v)) throw new ArgumentOutOfRangeException(name); }
    }

    /// <summary>Moment boundary at the supplied FIXED axial force, in the requested
    /// moment direction. Utilization scales moments only; for pure N it is |N|/NplRd.
    /// Positive infinity means no moment capacity remains. This is not a member check.</summary>
    public sealed class PlasticMomentResistance
    {
        public double NplRd { get; }
        public double Moment1Rd { get; }
        public double Moment2Rd { get; }
        public double Utilization { get; }
        public bool IsSatisfied => Utilization <= 1;
        public string Reference => "EN 1993-1-1:2005 6.2.9.1(2), plastic stress equilibrium";
        internal PlasticMomentResistance(double npl, double m1, double m2, double utilization)
        { NplRd = npl; Moment1Rd = m1; Moment2Rd = m2; Utilization = utilization; }
    }

    // Exact integration of rectangular areas cut by a plastic neutral line.
    // Each rectangle is symmetric about x=0. Coordinates are normalized before entry.
    internal static class RectangularPlasticEquilibrium
    {
        internal static double[] Solve(double[][] rectangles, double targetArea, double mx, double my)
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
            double[] answer = Evaluate((low + high) / 2);
            if (Math.Abs(answer[0] - targetArea) > 1e-11 ||
                Math.Abs(Math.Atan2(Math.Abs(answer[1]), answer[2]) - targetAngle) > 1e-8)
                throw new ArithmeticException("Plastic H equilibrium did not converge.");
            return answer;
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
