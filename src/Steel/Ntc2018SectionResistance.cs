using GPC.Checkers.Steel.Sections;
using System;

namespace GPC.Checkers.Steel.Members
{
    /// <summary>
    /// NTC 2018, Italy, ordinary static structural steel, section resistance primitives.
    /// Units: N, mm, MPa, Nmm. Design actions are already factored; N is positive in tension.
    /// These methods do not constitute a complete member check. Classification, shear areas,
    /// restraint conditions and imperfection curve must be established by the caller.
    /// No bridge, fatigue, fire, seismic or connection provisions are applied.
    /// </summary>
    public static class Ntc2018SectionResistance
    {
        public const double GammaM0 = SectionResistanceCode.NtcGammaM0;
        public const double GammaM1 = Ntc2018DesignPolicy.GammaM1;
        public const double GammaM2 = SectionResistanceCode.NtcGammaM2;

        /// <summary>NTC 2018 4.2.4.1.2.1. Null netArea explicitly means an unperforated section.</summary>
        public static double Tension(double area, double yieldStrength, double ultimateStrength, double? netArea = null)
        {
            Positive(area, nameof(area)); Positive(yieldStrength, nameof(yieldStrength));
            Positive(ultimateStrength, nameof(ultimateStrength));
            if (ultimateStrength < yieldStrength) throw new ArgumentOutOfRangeException(nameof(ultimateStrength));
            double resistance = SectionResistanceArithmetic.Normal(area, yieldStrength, GammaM0);
            if (netArea.HasValue)
            {
                Positive(netArea.Value, nameof(netArea));
                if (netArea.Value > area) throw new ArgumentOutOfRangeException(nameof(netArea));
                resistance = Math.Min(resistance, SectionResistanceArithmetic.NetTension(netArea.Value, ultimateStrength, GammaM2));
            }
            return PositiveResult(resistance);
        }

        /// <summary>NTC 2018 4.2.4.1.2.2. Verified classification for the specified stress state is required.</summary>
        public static double Compression(double area, double yieldStrength, int sectionClass)
        {
            SupportedClass(sectionClass); Positive(area, nameof(area)); Positive(yieldStrength, nameof(yieldStrength));
            return PositiveResult(SectionResistanceArithmetic.Normal(area, yieldStrength, GammaM0));
        }

        /// <summary>NTC 2018 4.2.4.1.2.3, low shear only. Wel is the smaller elastic modulus of the two extreme fibers.</summary>
        public static double Bending(double plasticModulus, double minimumElasticModulus, double yieldStrength, int sectionClass)
        {
            SupportedClass(sectionClass); Positive(yieldStrength, nameof(yieldStrength));
            double modulus = sectionClass <= 2 ? plasticModulus : minimumElasticModulus;
            Positive(modulus, sectionClass <= 2 ? nameof(plasticModulus) : nameof(minimumElasticModulus));
            return PositiveResult(SectionResistanceArithmetic.Normal(modulus, yieldStrength, GammaM0));
        }

        /// <summary>NTC 2018 4.2.4.1.2.4. Caller supplies Av for the specific direction and excludes shear buckling and torsion.</summary>
        public static double Shear(double shearArea, double yieldStrength)
        {
            Positive(shearArea, nameof(shearArea)); Positive(yieldStrength, nameof(yieldStrength));
            return PositiveResult(SectionResistanceArithmetic.Shear(shearArea, yieldStrength, GammaM0));
        }

        /// <summary>NTC 2018 4.2.4.1.3.1. One flexural buckling axis, uniform member, classes 1-3.
        /// Imperfection alpha must be selected from the applicable manufacturing/section/axis case.
        /// This method does not check torsional, flexural-torsional or lateral-torsional buckling.</summary>
        public static FlexuralBucklingResistance FlexuralBuckling(double area, double inertia, double effectiveLength,
            double elasticModulus, double yieldStrength, double imperfection, int sectionClass)
        {
            SupportedClass(sectionClass); Positive(area, nameof(area)); Positive(inertia, nameof(inertia));
            Positive(effectiveLength, nameof(effectiveLength)); Positive(elasticModulus, nameof(elasticModulus));
            Positive(yieldStrength, nameof(yieldStrength));
            if (!Ntc2018DesignPolicy.IsImperfectionFactor(imperfection))
                throw new ArgumentOutOfRangeException(nameof(imperfection), "Select an NTC curve: a0, a, b, c or d.");
            double critical = PositiveResult(ElasticStabilityArithmetic.Euler(effectiveLength, inertia, elasticModulus));
            double slenderness = PositiveResult(BucklingReductionArithmetic.AxialSlenderness(area, yieldStrength, critical));
            double phi = BucklingReductionArithmetic.Phi(imperfection, slenderness);
            double reduction = slenderness <= .2 ? 1 : BucklingReductionArithmetic.AxialChi(phi, slenderness);
            return new FlexuralBucklingResistance(critical, slenderness, reduction,
                PositiveResult(reduction * area * yieldStrength / GammaM1));
        }

        /// <summary>Conservative elastic normal-stress bound |N|/A+|M1|/Wel1+|M2|/Wel2.
        /// NTC 2018 4.2.4.1.2.1-3. Excludes shear/torsion, instability and local class-4 effects.
        /// Wel1/Wel2 refer to principal centroidal axes. This is a sufficient elastic bound,
        /// not the exact plastic biaxial interaction surface.</summary>
        public static double ElasticNormalInteraction(double normalForce, double moment1, double moment2,
            double area, double modulus1, double modulus2, double yieldStrength, int sectionClass)
        {
            SupportedClass(sectionClass); Finite(normalForce, nameof(normalForce));
            Finite(moment1, nameof(moment1)); Finite(moment2, nameof(moment2));
            Positive(area, nameof(area)); Positive(modulus1, nameof(modulus1));
            Positive(modulus2, nameof(modulus2)); Positive(yieldStrength, nameof(yieldStrength));
            double ratio = SectionInteractionArithmetic.ElasticNormalRatio(normalForce, moment1, moment2,
                area, modulus1, modulus2, yieldStrength, GammaM0);
            if (double.IsInfinity(ratio) || double.IsNaN(ratio)) throw new ArithmeticException("Nonfinite interaction.");
            return ratio;
        }

        private static void SupportedClass(int sectionClass)
        {
            if (sectionClass < 1 || sectionClass > 4) throw new ArgumentOutOfRangeException(nameof(sectionClass));
            if (sectionClass == 4) throw new NotSupportedException("Class 4 requires a separately verified effective section or shell resistance.");
        }
        private static void Finite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(name);
        }
        private static void Positive(double value, string name)
        {
            Finite(value, name);
            if (value <= 0) throw new ArgumentOutOfRangeException(name);
        }
        private static double PositiveResult(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
                throw new ArithmeticException("Resistance outside finite positive numerical range.");
            return value;
        }
    }

    public sealed class FlexuralBucklingResistance
    {
        public double CriticalForce { get; }
        public double NormalizedSlenderness { get; }
        public double Reduction { get; }
        public double Resistance { get; }
        internal FlexuralBucklingResistance(double critical, double slenderness, double reduction, double resistance)
        { CriticalForce = critical; NormalizedSlenderness = slenderness; Reduction = reduction; Resistance = resistance; }
    }
}
