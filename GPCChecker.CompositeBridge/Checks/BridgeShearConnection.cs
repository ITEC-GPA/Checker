using System;

namespace GPC.Checkers.CompositeBridge
{
    /// <summary>Pure mm/N/MPa calculations for EN 1993-1-5:2006 and EN 1994-2:2005.
    /// No UI, archive or load combination dependencies. Flange contribution to shear is omitted.</summary>
    public static class BridgeShearConnection
    {
        private static void Positive(double x, string name)
        { if (double.IsNaN(x) || double.IsInfinity(x) || x <= 0) throw new ArgumentException(name + ": positive finite value required."); }

        public static WebShearResistance Web(double height, double thickness, double fy, double young,
            double gammaM0, double gammaM1, double eta, double panelLength = 0, bool rigidEndPost = false)
        {
            Positive(height, "hw"); Positive(thickness, "tw"); Positive(fy, "fy"); Positive(young, "E");
            Positive(gammaM0, "gammaM0"); Positive(gammaM1, "gammaM1"); Positive(eta, "eta");
            if (panelLength < 0 || double.IsNaN(panelLength) || double.IsInfinity(panelLength)) throw new ArgumentException("Invalid panel length.");
            if (eta < 1 || eta > 1.2) throw new ArgumentException("eta must be between 1 and 1.2.");
            double ratio = panelLength / height;
            double kt = panelLength == 0 ? 5.34 : ratio >= 1 ? 5.34 + 4 / (ratio * ratio) : 4 + 5.34 / (ratio * ratio);
            double sigmaE = Math.PI * Math.PI * young / (12 * (1 - .3 * .3)) * Math.Pow(thickness / height, 2);
            double tauCr = kt * sigmaE;
            double lambda = Math.Sqrt(fy / (Math.Sqrt(3) * tauCr));
            double chi = lambda < .83 / eta ? eta : rigidEndPost && lambda >= 1.08 ? 1.37 / (.7 + lambda) : .83 / lambda;
            double area = height * thickness;
            double plastic = area * fy / (Math.Sqrt(3) * gammaM0);
            double buckling = chi * area * fy / (Math.Sqrt(3) * gammaM1);
            return new WebShearResistance { KTau = kt, TauCritical = tauCr, Slenderness = lambda, Chi = chi,
                Area = area, PlasticResistance = plastic, BucklingResistance = buckling, Resistance = Math.Min(plastic, buckling) };
        }

        /// <summary>Symmetric double-sided flat intermediate stiffener. Effective web strip
        /// 15 epsilon tw on each side. Checks stiffness, curve-c flexural buckling, torsional
        /// restraint and a sinusoidal second-order model under web deviation forces.
        /// Both flanges must provide lateral support; no bearing, weld or diaphragm check.</summary>
        public static TransverseStiffenerResistance Stiffener(double hw, double tw, double a, double b, double t,
            double fy, double young, double gammaM1, double vEd, double webCompression, double externalCompression = 0, double span = 0)
        {
            Positive(a, "a"); Positive(b, "bs"); Positive(t, "ts");
            if (double.IsNaN(vEd) || double.IsInfinity(vEd) || double.IsNaN(webCompression) || double.IsInfinity(webCompression)
                || double.IsNaN(externalCompression) || double.IsInfinity(externalCompression) || webCompression < 0 || externalCompression < 0)
                throw new ArgumentException("Finite forces and non-negative compression magnitudes required.");
            if (b < t) throw new ArgumentException("Flat stiffener projection must be at least its thickness.");
            if (span == 0) span = hw;
            Positive(span, "stiffener span");
            if (span < hw) throw new ArgumentException("Stiffener span cannot be smaller than the clear web height.");
            var panel = Web(hw, tw, fy, young, 1, gammaM1, 1.2, a);
            double eps = Math.Sqrt(235 / fy), webWidth = 2 * Math.Min(15 * eps * tw, a / 2);
            double area = 2 * b * t + webWidth * tw;
            double edge = b + tw / 2;
            double inertia = webWidth * Math.Pow(tw, 3) / 12 + 2 * (t * Math.Pow(b, 3) / 12 + b * t * Math.Pow((tw + b) / 2, 2));
            double required = a / hw < Math.Sqrt(2) ? 1.5 * Math.Pow(hw * tw, 3) / (a * a) : .75 * hw * Math.Pow(tw, 3);
            double axial = Math.Max(0, Math.Abs(vEd) - panel.TauCritical * hw * tw / gammaM1) + externalCompression;
            double nCr = Math.PI * Math.PI * young * inertia / Math.Pow(.75 * span, 2);
            double lambda = Math.Sqrt(area * fy / nCr);
            double phi = .5 * (1 + .49 * (lambda - .2) + lambda * lambda);
            double chi = Math.Min(1, 1 / (phi + Math.Sqrt(Math.Max(0, phi * phi - lambda * lambda))));
            double nbRd = chi * area * fy / gammaM1;
            // Lower bound to the rectangular Saint-Venant constant (finite b/t correction).
            double it = b * Math.Pow(t, 3) / 3 * (1 - .63 * t / b);
            double ip = Math.Pow(b, 3) * t / 3 + b * Math.Pow(t, 3) / 12;
            double torsionRatio = 5.3 * fy * ip / (young * it);
            // EN 1993-1-5 9.2.1: use sigma_cr,c/sigma_cr,p = 1, the conservative upper bound.
            // Adjacent panels have the same assigned length; compression integrates the gross web.
            double sigmaM = webCompression / span * (2 / a);
            double initial = Math.Min(span, a) / 300, k = Math.PI / span;
            double destabilizing = sigmaM + axial * k * k;
            double stiffness = young * inertia * Math.Pow(k, 4);
            bool stable = stiffness > destabilizing;
            double deflection = stable ? initial * destabilizing / (stiffness - destabilizing) : 0;
            double moment = young * inertia * k * k * deflection;
            double maxStress = axial / area + moment * edge / inertia;
            return new TransverseStiffenerResistance { Area = area, Inertia = inertia, RequiredInertia = required,
                AxialForce = axial, BucklingResistance = nbRd, Lambda = lambda, Chi = chi,
                TorsionalRatio = torsionRatio, InitialDeflection = initial, AdditionalDeflection = deflection,
                Stress = maxStress, Stable = stable, RigidityRatio = required / inertia,
                BucklingRatio = axial / nbRd, DeflectionRatio = stable ? deflection / (span / 300) : (double?)null,
                StressRatio = stable ? maxStress / (fy / gammaM1) : (double?)null };
        }

        public static HeadedStudResistance Stud(double diameter, double height, double fu, double fck, double ecm,
            double gammaV = 1.25)
        {
            Positive(diameter, "d"); Positive(height, "hsc"); Positive(fu, "fu"); Positive(fck, "fck"); Positive(ecm, "Ecm"); Positive(gammaV, "gammaV");
            if (diameter < 16 || diameter > 25 || height < 3 * diameter || fck < 20 || fck > 60)
                throw new ArgumentException("Stud formula scope: 16 <= d <= 25 mm, hsc >= 3d, C20/25 to C60/75 solid concrete slab.");
            double alpha = height / diameter <= 4 ? .2 * (height / diameter + 1) : 1;
            double steel = .8 * Math.Min(fu, 500) * Math.PI * diameter * diameter / 4 / gammaV;
            double concrete = .29 * alpha * diameter * diameter * Math.Sqrt(fck * ecm) / gammaV;
            return new HeadedStudResistance { Alpha = alpha, SteelResistance = steel, ConcreteResistance = concrete,
                Resistance = Math.Min(steel, concrete), UsedFu = Math.Min(fu, 500) };
        }
    }
    public sealed class WebShearResistance
    {
        public double KTau { get; set; }
        public double TauCritical { get; set; }
        public double Slenderness { get; set; }
        public double Chi { get; set; }
        public double Area { get; set; }
        public double PlasticResistance { get; set; }
        public double BucklingResistance { get; set; }
        public double Resistance { get; set; }
    }
    public sealed class TransverseStiffenerResistance
    {
        public double Area { get; set; }
        public double Inertia { get; set; }
        public double RequiredInertia { get; set; }
        public double AxialForce { get; set; }
        public double BucklingResistance { get; set; }
        public double Lambda { get; set; }
        public double Chi { get; set; }
        public double TorsionalRatio { get; set; }
        public double InitialDeflection { get; set; }
        public double AdditionalDeflection { get; set; }
        public double Stress { get; set; }
        public bool Stable { get; set; }
        public double RigidityRatio { get; set; }
        public double BucklingRatio { get; set; }
        public double? DeflectionRatio { get; set; }
        public double? StressRatio { get; set; }
        public bool Satisfactory { get { return Stable && RigidityRatio <= 1 && BucklingRatio <= 1 && TorsionalRatio <= 1 && DeflectionRatio <= 1 && StressRatio <= 1; } }
    }
    public sealed class HeadedStudResistance
    {
        public double Alpha { get; set; }
        public double UsedFu { get; set; }
        public double SteelResistance { get; set; }
        public double ConcreteResistance { get; set; }
        public double Resistance { get; set; }
    }
}
