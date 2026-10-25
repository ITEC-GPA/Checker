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
            var r = GPC.Checkers.Steel.Sections.Ec3WebStiffener.Stiffener(hw, tw, span, leftPanel, rightPanel,
                availableLeft, availableRight, leftWidth, leftThickness, rightWidth, rightThickness,
                fy, young, gammaM1, compression, webCompression, loadX, loadZ, lengthFactor);
            return new StiffenerDetail { Area = r.Area, PlateArea = r.PlateArea, CentroidX = r.CentroidX, InertiaOut = r.InertiaOut, InertiaIn = r.InertiaIn, WebStrip = r.WebStrip, Compression = r.Compression, CriticalOut = r.CriticalOut, CriticalIn = r.CriticalIn, MomentOut = r.MomentOut, MomentIn = r.MomentIn, Eccentricity = r.Eccentricity, Stress = r.Stress, Deflection = r.Deflection, RigidityRatio = r.RigidityRatio, LocalRatio = r.LocalRatio, TorsionRatio = r.TorsionRatio, MaxThickness = r.MaxThickness, PlateCount = r.PlateCount, Stable = r.Stable, StressRatio = r.StressRatio, DeflectionRatio = r.DeflectionRatio };
        }

        public static double WeldStrength(double fu, double beta, double gammaM2)
            => GPC.Checkers.Steel.Sections.Ec3WebStiffener.WeldStrength(fu, beta, gammaM2);

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
