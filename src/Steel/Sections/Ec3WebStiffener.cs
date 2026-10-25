using System;
using System.Collections.Generic;

namespace GPC.Checkers.Steel.Sections
{
    /// <summary>Shared analytical transverse-stiffener kernel, EN1993-1-5:2006 section9. N/mm/MPa.</summary>
    public static class Ec3WebStiffener
    {
        private static void Positive(double x, string name)
        { if (double.IsNaN(x) || double.IsInfinity(x) || x <= 0) throw new ArgumentException(name + ": positive finite value required."); }
        private static void Nonnegative(double x, string name)
        { if (double.IsNaN(x) || double.IsInfinity(x) || x < 0) throw new ArgumentException(name + ": non-negative finite value required."); }

        /// <summary>Welded flats, continuous connection to the web, lateral restraints at both flanges.
        /// Gross class 3 or better flats are required separately. Web strip is clipped symmetrically
        /// to available material, conservatively omitting an unmatched strip at an end.
        /// Elastic second-order member analysis: EN1993-1-1 5.2.2(7)a, 5.3.4 and Table 5.1 (curve c).
        /// Closed-form secant solution for eccentric compression plus sinusoidal bow/deviation loading.
        /// Torsional restraint must independently meet EN1993-1-5 9.2.1(8).
        /// X is normal to the web, Z along the girder, both measured from the stiffener/web intersection.</summary>
        public static WebStiffenerResistance Stiffener(double hw, double tw, double span, double leftPanel, double rightPanel,
            double availableLeft, double availableRight, double leftWidth, double leftThickness,
            double rightWidth, double rightThickness, double fy, double young, double gammaM1,
            double compression, double webCompression, double loadX, double loadZ, double lengthFactor = 1)
        {
            Positive(hw, "hw"); Positive(tw, "tw"); Positive(span, "span"); Positive(fy, "fy");
            Positive(young, "E"); Positive(gammaM1, "gammaM1"); Positive(leftPanel, "aL"); Positive(rightPanel, "aR");
            if (span < hw) throw new ArgumentException("Stiffener span must be at least the clear web height.");
            Nonnegative(availableLeft, "availableLeft"); Nonnegative(availableRight, "availableRight");
            Nonnegative(compression, "Nst"); Nonnegative(webCompression, "web compression");
            if (lengthFactor < .75 || lengthFactor > 2 || double.IsNaN(lengthFactor)) throw new ArgumentException("0.75 <= length factor <= 2 required.");
            if (double.IsNaN(loadX) || double.IsInfinity(loadX) || double.IsNaN(loadZ) || double.IsInfinity(loadZ)) throw new ArgumentException("Finite eccentricities required.");
            var plates = new List<Tuple<double, double, double>>(); // area, x centroid, own Ix
            double torsion = 0, local = 0, plateArea = 0, maxThickness = 0;
            double eps = Math.Sqrt(235 / fy), halfStrip = Math.Min(15 * eps * tw, Math.Min(availableLeft, availableRight));
            double aw = 2 * halfStrip * tw, iz = 2 * halfStrip * Math.Pow(tw, 3) / 12;
            double ix = tw * Math.Pow(2 * halfStrip, 3) / 12, first = 0;
            double edgeLeft = tw / 2, edgeRight = tw / 2, zEdge = halfStrip;
            void Plate(double b, double t, int side)
            {
                Nonnegative(b, "plate width"); Nonnegative(t, "plate thickness");
                if (b == 0 && t == 0) return;
                Positive(b, "plate width"); Positive(t, "plate thickness");
                if (b < t) throw new ArgumentException("Plate projection must be >= thickness.");
                double area = b * t, x = side * (tw + b) / 2;
                plates.Add(Tuple.Create(area, x, t * Math.Pow(b, 3) / 12));
                plateArea += area; first += area * x; ix += b * Math.Pow(t, 3) / 12;
                maxThickness = Math.Max(maxThickness, t); zEdge = Math.Max(zEdge, t / 2);
                if (side < 0) edgeLeft += b; else edgeRight += b;
                double it = b * Math.Pow(t, 3) / 3 * (1 - .63 * t / b);
                double ip = Math.Pow(b, 3) * t / 3 + b * Math.Pow(t, 3) / 12;
                torsion = Math.Max(torsion, 5.3 * fy * ip / (young * it));
                local = Math.Max(local, b / (14 * eps * t));
            }
            Plate(leftWidth, leftThickness, -1); Plate(rightWidth, rightThickness, 1);
            if (plates.Count == 0) throw new ArgumentException("At least one stiffener plate required.");
            double areaTotal = aw + plateArea, xg = first / areaTotal;
            iz += aw * xg * xg;
            foreach (var p in plates) iz += p.Item3 + p.Item1 * Math.Pow(p.Item2 - xg, 2);
            double edge = Math.Max(edgeLeft + xg, edgeRight - xg);
            double length = lengthFactor * span, k = Math.PI / length;
            // Use at least the physical span for deviation loads; length factors < 1 apply only to compression buckling.
            double kd = Math.PI / Math.Max(span, length);
            // EN 1993-1-5 §9.2.1(4)-(6): sigma_m = N/b (1/a1 + 1/a2), w0 = min(a1, a2, b)/300 and w <= b/300 with b the width of the
            // plate, i.e. the clear web height (before, the span between the flange centrelines: slightly smaller sigma_m, larger limits)
            double sm = webCompression / hw * (1 / leftPanel + 1 / rightPanel);
            double ncrX = young * iz * k * k, ncrZ = young * ix * k * k;
            double qRatio = sm / (young * iz * Math.Pow(kd, 4));
            double ratioX = compression / ncrX + qRatio, ratioZ = compression / ncrZ;
            bool stable = ratioX < 1 && ratioZ < 1;
            double bow = length / 200; // elastic equivalent imperfection for curve c
            double panelBow = Math.Min(hw, Math.Min(leftPanel, rightPanel)) / 300;
            double mx = 0, mz = 0, deflection = 0;
            if (stable)
            {
                double secantX = 1 / Math.Cos(Math.PI / 2 * Math.Sqrt(ratioX));
                double secantZ = 1 / Math.Cos(Math.PI / 2 * Math.Sqrt(ratioZ));
                // Absolute envelopes: eccentricity and imperfection are taken in the unfavourable direction.
                mx = compression * Math.Abs(loadX - xg) * secantX
                    + compression * bow / (1 - ratioX)
                    + young * iz * kd * kd * panelBow * qRatio / (1 - ratioX);
                mz = compression * Math.Abs(loadZ) * secantZ + compression * bow / (1 - ratioZ);
                // Deflection check under plate deviation + actual eccentricity, without the residual-stress equivalent bow.
                deflection = panelBow * ratioX / (1 - ratioX) + Math.Abs(loadX - xg) * (secantX - 1);
            }
            double stress = compression / areaTotal + mx * edge / iz + mz * zEdge / ix;
            double required(double a) => a / hw < Math.Sqrt(2) ? 1.5 * Math.Pow(hw * tw, 3) / (a * a) : .75 * hw * Math.Pow(tw, 3);
            return new WebStiffenerResistance { Area = areaTotal, PlateArea = plateArea, CentroidX = xg,
                InertiaOut = iz, InertiaIn = ix, WebStrip = 2 * halfStrip, Compression = compression,
                CriticalOut = ncrX, CriticalIn = ncrZ, MomentOut = mx, MomentIn = mz,
                Eccentricity = loadX - xg, Stress = stress, Stable = stable,
                StressRatio = stable ? stress * gammaM1 / fy : (double?)null,
                Deflection = deflection, DeflectionRatio = stable ? deflection / (hw / 300) : (double?)null,
                RigidityRatio = Math.Max(required(leftPanel), required(rightPanel)) / iz,
                LocalRatio = local, TorsionRatio = torsion, PlateCount = plates.Count, MaxThickness = maxThickness };
        }

        public static double WeldStrength(double fu, double beta, double gammaM2)
        { Positive(fu, "fu"); Positive(beta, "betaW"); Positive(gammaM2, "gammaM2"); return fu / (Math.Sqrt(3) * beta * gammaM2); }

    }
    public sealed class WebStiffenerResistance
    {
        public double Area { get; internal set; }
        public double PlateArea { get; internal set; }
        public double CentroidX { get; internal set; }
        public double InertiaOut { get; internal set; }
        public double InertiaIn { get; internal set; }
        public double WebStrip { get; internal set; }
        public double Compression { get; internal set; }
        public double CriticalOut { get; internal set; }
        public double CriticalIn { get; internal set; }
        public double MomentOut { get; internal set; }
        public double MomentIn { get; internal set; }
        public double Eccentricity { get; internal set; }
        public double Stress { get; internal set; }
        public double Deflection { get; internal set; }
        public double RigidityRatio { get; internal set; }
        public double LocalRatio { get; internal set; }
        public double TorsionRatio { get; internal set; }
        public double MaxThickness { get; internal set; }
        public int PlateCount { get; internal set; }
        public bool Stable { get; internal set; }
        public double? StressRatio { get; internal set; }
        public double? DeflectionRatio { get; internal set; }
        public bool Satisfactory => Stable && StressRatio <= 1 && DeflectionRatio <= 1 && RigidityRatio <= 1 && LocalRatio <= 1 && TorsionRatio <= 1;
    }
}
