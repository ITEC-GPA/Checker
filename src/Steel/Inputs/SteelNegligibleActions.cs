using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;

namespace GPC.Checkers.Steel.Inputs
{
    /// <summary>When an action component counts as zero: numerical noise of the analysis,
    /// not an engineering simplification. A component is neglected when
    /// |value| &lt;= max(RelativeToResistance x reference resistance, absolute limit);
    /// absolute limits are in N for N, V1, V2 and in Nmm for T, M1, M2.
    /// References: Npl = A fy/gammaM0; Vpl = Av fy/(sqrt3 gammaM0) on qualified shear
    /// areas only; Mel = Wel fy/gammaM0; torsion fy/(sqrt3 gammaM0) A tmin/3, the
    /// thin-walled open-section lower bound. A missing reference leaves the absolute limit.
    /// All limits zero restore exact-zero routing.</summary>
    public sealed class SteelNegligibleActionPolicy
    {
        public const double DefaultRelativeToResistance = 1e-6;
        public double RelativeToResistance { get; }
        public double AbsoluteForce { get; }
        public double AbsoluteMoment { get; }

        public SteelNegligibleActionPolicy(double relativeToResistance = DefaultRelativeToResistance,
            double absoluteForce = 0, double absoluteMoment = 0)
        {
            NonNegative(relativeToResistance, nameof(relativeToResistance));
            NonNegative(absoluteForce, nameof(absoluteForce));
            NonNegative(absoluteMoment, nameof(absoluteMoment));
            if (relativeToResistance >= 1)
                throw new ArgumentOutOfRangeException(nameof(relativeToResistance), "A negligible action must stay below its resistance.");
            RelativeToResistance = relativeToResistance; AbsoluteForce = absoluteForce; AbsoluteMoment = absoluteMoment;
        }

        public static SteelNegligibleActionPolicy Default { get; } = new SteelNegligibleActionPolicy();
        public static SteelNegligibleActionPolicy ExactZero { get; } = new SteelNegligibleActionPolicy(0);

        private static void NonNegative(double value, string name)
        {
            if (!(value >= 0) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name, "A finite nonnegative limit is required.");
        }
    }

    /// <summary>One action component set to zero, with the limit that admitted it.</summary>
    public sealed class SteelNeglectedAction
    {
        /// <summary>N, V1, V2, T, M1 or M2.</summary>
        public string Component { get; }
        public double Value { get; }
        public double Limit { get; }
        /// <summary>Reference resistance of the relative limit; null when none was available.</summary>
        public double? ReferenceResistance { get; }
        public string Unit { get; }
        internal SteelNeglectedAction(string component, double value, double limit, double? reference, string unit)
        { Component = component; Value = value; Limit = limit; ReferenceResistance = reference; Unit = unit; }
    }

    internal static class SteelNegligibleActions
    {
        internal static SteelActions Apply(SteelVerificationInput input, out SteelNeglectedAction[] neglected)
        {
            var a = input.Actions; var policy = input.Code.NegligibleActions;
            var list = new List<SteelNeglectedAction>();
            if (policy.RelativeToResistance == 0 && policy.AbsoluteForce == 0 && policy.AbsoluteMoment == 0)
            { neglected = list.ToArray(); return a; }
            var section = input.Section.ReadOnlySection;
            double fy = input.Section.YieldStrength, gamma = input.Code.SectionCode.GammaM0;
            double n = Neglect(list, "N", a.N, () => input.Section.Area * fy / gamma, policy.RelativeToResistance, policy.AbsoluteForce, "N");
            double v1 = Neglect(list, "V1", a.V1, () => Shear(input, section, 1), policy.RelativeToResistance, policy.AbsoluteForce, "N");
            double v2 = Neglect(list, "V2", a.V2, () => Shear(input, section, 2), policy.RelativeToResistance, policy.AbsoluteForce, "N");
            double t = Neglect(list, "T", a.T, () => Torsion(input, section), policy.RelativeToResistance, policy.AbsoluteMoment, "Nmm");
            double m1 = Neglect(list, "M1", a.M1, () => section.Wel1 * fy / gamma, policy.RelativeToResistance, policy.AbsoluteMoment, "Nmm");
            double m2 = Neglect(list, "M2", a.M2, () => section.Wel2 * fy / gamma, policy.RelativeToResistance, policy.AbsoluteMoment, "Nmm");
            neglected = list.ToArray();
            return neglected.Length == 0 ? a : new SteelActions(n, v1, v2, t, m1, m2);
        }

        private static double Neglect(ICollection<SteelNeglectedAction> list, string name, double value,
            Func<double> reference, double relative, double absolute, string unit)
        {
            if (value == 0) return 0;
            double? r = relative > 0 ? Reference(reference) : null;
            double limit = Math.Max(relative * (r ?? 0), absolute);
            if (!(Math.Abs(value) <= limit)) return value;
            list.Add(new SteelNeglectedAction(name, value, limit, r, unit));
            return 0;
        }

        // An unavailable or unqualified reference never blocks the check: only the absolute limit remains.
        private static double? Reference(Func<double> reference)
        {
            try
            {
                double r = reference();
                return r > 0 && !double.IsInfinity(r) ? r : (double?)null;
            }
            catch (ArgumentException) { return null; }
            catch (NotSupportedException) { return null; }
            catch (InvalidOperationException) { return null; }
            catch (ArithmeticException) { return null; }
        }

        private static double Shear(SteelVerificationInput input, ISteelSection section, int axis)
        {
            if (!SteelMethodSelector.ShearMappingQualified(section)) return double.NaN;
            return Pipeline.SteelPlasticShear.Resistance(input, axis);
        }

        private static double Torsion(SteelVerificationInput input, ISteelSection section)
        {
            double t = MinimumThickness(section);
            return input.Section.YieldStrength / (Math.Sqrt(3) * input.Code.SectionCode.GammaM0) * input.Section.Area * t / 3;
        }

        private static double MinimumThickness(ISteelSection section)
        {
            var shape = section.SectionShape;
            if (shape is SectionBuiltUp pair)
                return MinimumThickness(new SteelSection(pair.Parts[0].Section, section.SteelMaterial, section.SectionType, section.FormedType));
            var kind = SteelSectionFamilies.Of(shape)?.Kind;
            if (kind == SteelSectionKind.Angle)
            {
                var angle = (SectionL)shape;
                return Math.Min(angle.HorizontalLegThickness, angle.VerticalLegThickness);
            }
            if (kind == SteelSectionKind.Solid)
                return Math.Min(shape.Height, shape.Width);
            if (ModelCompressionGeometry.CanMap(section))
                return ModelCompressionGeometry.Create(section).Parts.Min(p => p.Thickness);
            return double.NaN;
        }
    }
}
