using System;
using System.Collections.Generic;

namespace GPC.Checkers.CompositeBridge
{
    /// <summary>Local bridge details, mm/N/MPa. Independent of the construction-stage solver.</summary>
    public static class BridgeLocalDetails
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
        public static StiffenerDetail Stiffener(double hw, double tw, double span, double leftPanel, double rightPanel,
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
            return new StiffenerDetail { Area = areaTotal, PlateArea = plateArea, CentroidX = xg,
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

        /// <summary>Straight tension bar, alpha1...alpha5=1. EC2 8.4; additional NTC 4.1.6.1.4 floor.</summary>
        public static double AnchorageLength(double diameter, double stress, double fctk05, double gammaC, bool goodBond, bool ntc)
        {
            Positive(diameter, "diameter"); Nonnegative(stress, "stress"); Positive(fctk05, "fctk05"); Positive(gammaC, "gammaC");
            double eta2 = Math.Min(1, (132 - diameter) / 100);
            if (eta2 <= 0) throw new ArgumentException("Bar diameter outside bond model.");
            double fbd = 2.25 * (goodBond ? 1 : .7) * eta2 * fctk05 / gammaC;
            return Math.Max(diameter * stress / (4 * fbd), Math.Max((ntc ? 20 : 10) * diameter, ntc ? 150 : 100));
        }

        /// <summary>EC2 6.2.4/EC4 6.6.6: one potential surface, demand N/mm and crossing steel mm2/mm.
        /// No concrete cohesion is credited. Required steel also includes transverse bending and minimum reinforcement.</summary>
        public static SlabShearDetail SlabSurface(double flow, double surfaceLength, double reinforcement,
            double fck, double fcd, double fyk, double fyd, double cotTheta, double bendingSteel = 0)
        {
            Nonnegative(flow, "flow"); Positive(surfaceLength, "surface length"); Nonnegative(reinforcement, "reinforcement");
            Positive(fck, "fck"); Positive(fcd, "fcd"); Positive(fyk, "fyk"); Positive(fyd, "fyd");
            Nonnegative(bendingSteel, "transverse bending steel");
            if (cotTheta < 1 || cotTheta > 2 || double.IsNaN(cotTheta)) throw new ArgumentException("1 <= cot theta <= 2 required.");
            double minimum = .08 * Math.Sqrt(fck) / fyk * surfaceLength;
            double shearSteel = flow / (fyd * cotTheta);
            double required = Math.Max(minimum, Math.Max(shearSteel, shearSteel / 2 + bendingSteel));
            double strut = .6 * (1 - fck / 250) * fcd * surfaceLength / (cotTheta + 1 / cotTheta);
            return new SlabShearDetail { Flow = flow, Length = surfaceLength, ProvidedSteel = reinforcement,
                RequiredSteel = required, MinimumSteel = minimum, StrutResistance = strut,
                SteelRatio = reinforcement > 0 ? required / reinforcement : (required == 0 ? 0 : (double?)null),
                StrutRatio = flow / strut };
        }

        /// <summary>EC4-2 6.8.7.2. Input flow range is already equivalent to two million cycles.
        /// No equivalence is inferred from construction stages. Ranges must envelope cracked/uncracked states.</summary>
        public static StudFatigueDetail StudFatigue(double flowRange, double pitch, int studs, double diameter,
            double flangeRange, bool flangeTensile, double gammaFf, double gammaMfStud, double gammaMfFlange)
        {
            Nonnegative(flowRange, "equivalent flow range"); Nonnegative(flangeRange, "equivalent flange stress range");
            Positive(pitch, "pitch"); Positive(diameter, "diameter"); Positive(gammaFf, "gammaFf");
            Positive(gammaMfStud, "gammaMfStud"); Positive(gammaMfFlange, "gammaMfFlange");
            if (studs < 1 || studs > 20 || diameter < 16 || diameter > 25) throw new ArgumentException("Invalid stud fatigue geometry.");
            double tau = flowRange * pitch / studs / (Math.PI * diameter * diameter / 4);
            double etaTau = gammaFf * tau * gammaMfStud / 90, etaSigma = gammaFf * flangeRange * gammaMfFlange / 80;
            return new StudFatigueDetail { StressRange = tau, StudRatio = etaTau, FlangeRatio = etaSigma,
                Interaction = flangeTensile ? (etaTau + etaSigma) / 1.3 : etaTau,
                Ratio = flangeTensile ? Math.Max(Math.Max(etaTau, etaSigma), (etaTau + etaSigma) / 1.3) : etaTau };
        }
    }
    public sealed class StiffenerDetail
    {
        public double Area, PlateArea, CentroidX, InertiaOut, InertiaIn, WebStrip, Compression,
            CriticalOut, CriticalIn, MomentOut, MomentIn, Eccentricity, Stress, Deflection,
            RigidityRatio, LocalRatio, TorsionRatio, MaxThickness;
        public int PlateCount;
        public bool Stable;
        public double? StressRatio, DeflectionRatio;
        public bool Satisfactory => Stable && StressRatio <= 1 && DeflectionRatio <= 1 && RigidityRatio <= 1 && LocalRatio <= 1 && TorsionRatio <= 1;
    }
    public sealed class SlabShearDetail
    {
        public double Flow, Length, ProvidedSteel, RequiredSteel, MinimumSteel, StrutResistance, StrutRatio;
        public double? SteelRatio;
    }
    public sealed class StudFatigueDetail
    { public double StressRange, StudRatio, FlangeRatio, Interaction, Ratio; }
}
