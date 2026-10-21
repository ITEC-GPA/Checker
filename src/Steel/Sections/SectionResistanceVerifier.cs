using System;
using System.Collections.Generic;

namespace GPC.Checkers.Steel.Sections
{
    /// <summary>Four isolated section checks from supplied, already qualified properties.
    /// No Model geometry dependency, automatic classification, combined-action check or
    /// member stability. Actions are already factored. N is positive in tension; bending
    /// and shear can have either sign. N/mm/MPa/Nmm are used throughout.</summary>
    public static class SectionResistanceVerifier
    {
        /// <summary>Explicitly declare an unperforated section, or supply netArea.
        /// Connection-specific reductions and block tearing are outside this primitive.</summary>
        public static SectionResistanceResult Tension(SectionResistanceCode code, double? normalForce,
            double? area, double? yieldStrength, double? ultimateStrength, bool? unperforated, double? netArea = null)
            => Evaluate(SectionResistanceMechanism.Tension, code, normalForce, trace =>
            {
                double a = Positive(area, "Area", "mm2", trace);
                double fy = Positive(yieldStrength, "Fy", "MPa", trace);
                double fu = Positive(ultimateStrength, "Fu", "MPa", trace);
                if (fu < fy) Invalid("Fu.BelowFy", "Ultimate strength must not be below yield strength.");
                if (!unperforated.HasValue) Missing("NetSection.DeclarationRequired", "Declare whether the section is unperforated.");
                if (unperforated.Value && netArea.HasValue) Invalid("NetSection.ConflictingInputs", "An unperforated declaration and an explicit net area cannot be combined.");
                Add(trace, "Unperforated", unperforated.Value ? 1 : 0, "1");
                double npl = FiniteResistance(SectionResistanceArithmetic.Normal(a, fy, code.GammaM0));
                Add(trace, "NplRd", npl, "N");
                if (unperforated.Value) return npl;
                double anet = Positive(netArea, "NetArea", "mm2", trace);
                if (anet > a) Invalid("NetArea.ExceedsGross", "Net area must not exceed gross area.");
                Add(trace, "GammaM2", code.GammaM2, "1");
                double nu = FiniteResistance(SectionResistanceArithmetic.NetTension(anet, fu, code.GammaM2));
                Add(trace, "NuRd", nu, "N");
                return Math.Min(npl, nu);
            });

        /// <summary>Classes 1-3. The supplied class must apply to the compression state.
        /// The result excludes flexural, torsional and flexural-torsional member buckling.</summary>
        public static SectionResistanceResult Compression(SectionResistanceCode code, double? normalForce,
            double? area, double? yieldStrength, int? sectionClass)
            => Evaluate(SectionResistanceMechanism.Compression, code, normalForce, trace =>
            {
                Class(sectionClass, trace);
                double a = Positive(area, "Area", "mm2", trace);
                double fy = Positive(yieldStrength, "Fy", "MPa", trace);
                return SectionResistanceArithmetic.Normal(a, fy, code.GammaM0);
            });

        /// <summary>Uniaxial bending, N=T=0 and low shear. Class is established externally.
        /// Supply the modulus for the selected axis; Wel is its minimum extreme-fibre modulus.
        /// An unused modulus may be absent. Lateral-torsional buckling is not included.</summary>
        public static SectionResistanceResult Bending(SectionResistanceCode code, double? moment,
            double? plasticModulus, double? minimumElasticModulus, double? yieldStrength, int? sectionClass)
            => Evaluate(SectionResistanceMechanism.Bending, code, moment, trace =>
            {
                int cl = Class(sectionClass, trace);
                double w = cl <= 2 ? Positive(plasticModulus, "Wpl", "mm3", trace)
                    : Positive(minimumElasticModulus, "WelMin", "mm3", trace);
                double fy = Positive(yieldStrength, "Fy", "MPa", trace);
                return SectionResistanceArithmetic.Normal(w, fy, code.GammaM0);
            });

        /// <summary>Plastic section shear in one direction, with Av supplied for that
        /// norm/direction. Shear buckling, torsion and high-shear bending interaction excluded.</summary>
        public static SectionResistanceResult Shear(SectionResistanceCode code, double? shearForce,
            double? shearArea, double? yieldStrength)
            => Evaluate(SectionResistanceMechanism.Shear, code, shearForce, trace =>
            {
                double av = Positive(shearArea, "Av", "mm2", trace);
                double fy = Positive(yieldStrength, "Fy", "MPa", trace);
                return SectionResistanceArithmetic.Shear(av, fy, code.GammaM0);
            });

        private static SectionResistanceResult Evaluate(SectionResistanceMechanism mechanism, SectionResistanceCode code,
            double? designAction, Func<List<SectionResistanceValue>, double> capacity)
        {
            var trace = new List<SectionResistanceValue>();
            string unit = mechanism == SectionResistanceMechanism.Bending ? "Nmm" : "N";
            string reference = code == null ? null : Reference(code, mechanism);
            string scope = Scope(mechanism);
            try
            {
                if (code == null) Missing("Code.Required", "An explicit code context is required.");
                double action = Required(designAction, "DesignAction", unit, trace);
                if (mechanism == SectionResistanceMechanism.Tension && action < 0)
                    Invalid("DesignAction.Sign", "Positive N denotes tension; use Compression for negative N.");
                if (mechanism == SectionResistanceMechanism.Compression && action > 0)
                    Invalid("DesignAction.Sign", "Negative N denotes compression; use Tension for positive N.");
                Add(trace, "GammaM0", code.GammaM0, "1");
                double resistance = FiniteResistance(capacity(trace));
                double utilization = Math.Abs(action) / resistance;
                if (double.IsNaN(utilization) || double.IsInfinity(utilization))
                    throw new ArithmeticException("Utilization is outside the finite numerical range.");
                Add(trace, "Resistance", resistance, unit);
                Add(trace, "Utilization", utilization, "1");
                return new SectionResistanceResult(mechanism, utilization <= 1 ? SectionResistanceStatus.Passed : SectionResistanceStatus.Failed,
                    code, reference, scope, null, null, resistance, utilization, trace);
            }
            catch (InputFailure ex)
            {
                return new SectionResistanceResult(mechanism, ex.Status, code, reference, scope, ex.Code,
                    ex.Message, null, null, trace);
            }
            catch (ArithmeticException ex)
            {
                return new SectionResistanceResult(mechanism, SectionResistanceStatus.CalculationError,
                    code, reference, scope, "NumericalRange", ex.Message, null, null, trace);
            }
        }

        private static double Required(double? value, string name, string unit, List<SectionResistanceValue> trace)
        {
            if (!value.HasValue) Missing(name + ".Required", name + " is required.");
            double v = value.Value;
            if (double.IsNaN(v) || double.IsInfinity(v)) Invalid(name + ".NotFinite", name + " must be finite.");
            Add(trace, name, v, unit);
            return v;
        }
        private static double Positive(double? value, string name, string unit, List<SectionResistanceValue> trace)
        {
            double v = Required(value, name, unit, trace);
            if (v <= 0) Invalid(name + ".NotPositive", name + " must be positive.");
            return v;
        }
        private static int Class(int? value, List<SectionResistanceValue> trace)
        {
            if (!value.HasValue) Missing("SectionClass.Required", "A previously established section class is required.");
            int cl = value.Value;
            if (cl < 1 || cl > 4) Invalid("SectionClass.Invalid", "Section class must be 1, 2, 3 or 4.");
            Add(trace, "SectionClass", cl, "1");
            if (cl == 4) throw new InputFailure(SectionResistanceStatus.NotSupported, "Class4.EffectivePropertiesRequired",
                "Class 4 requires a separately qualified effective-section method.");
            return cl;
        }
        private static double FiniteResistance(double value)
        {
            if (!(value > 0) || double.IsInfinity(value)) throw new ArithmeticException("Resistance is outside the finite positive numerical range.");
            return value;
        }
        private static void Add(List<SectionResistanceValue> trace, string name, double value, string unit)
            => trace.Add(new SectionResistanceValue(name, value, unit));
        private static void Missing(string code, string message)
            => throw new InputFailure(SectionResistanceStatus.MissingInput, code, message);
        private static void Invalid(string code, string message)
            => throw new InputFailure(SectionResistanceStatus.InvalidInput, code, message);

        private static string Reference(SectionResistanceCode code, SectionResistanceMechanism mechanism)
        {
            // References already used by the existing EC3/NTC primitives; no new design rule.
            bool ntc = code.Standard == SectionResistanceStandard.Ntc2018;
            switch (mechanism)
            {
                case SectionResistanceMechanism.Tension: return ntc ? "NTC 2018 4.2.4.1.2.1" : "EN 1993-1-1:2005 6.2.3";
                case SectionResistanceMechanism.Compression: return ntc ? "NTC 2018 4.2.4.1.2.2" : "EN 1993-1-1:2005 6.2.4";
                case SectionResistanceMechanism.Bending: return ntc ? "NTC 2018 4.2.4.1.2.3" : "EN 1993-1-1:2005 6.2.5";
                case SectionResistanceMechanism.Shear: return ntc ? "NTC 2018 4.2.4.1.2.4" : "EN 1993-1-1:2005 6.2.6";
                default: throw new ArgumentOutOfRangeException(nameof(mechanism));
            }
        }
        private static string Scope(SectionResistanceMechanism mechanism)
        {
            switch (mechanism)
            {
                case SectionResistanceMechanism.Tension: return "Isolated section tension; excludes connection-specific reductions, block tearing and member stability.";
                case SectionResistanceMechanism.Compression: return "Isolated section compression, classes 1-3; classification and member buckling excluded.";
                case SectionResistanceMechanism.Bending: return "Uniaxial section bending, classes 1-3, N=T=0, low shear; classification and lateral-torsional buckling excluded.";
                default: return "Plastic section shear in one direction; Av supplied; shear buckling, torsion and combined actions excluded.";
            }
        }
        private sealed class InputFailure : Exception
        {
            internal SectionResistanceStatus Status { get; }
            internal string Code { get; }
            internal InputFailure(SectionResistanceStatus status, string code, string message) : base(message)
            { Status = status; Code = code; }
        }
    }
}
