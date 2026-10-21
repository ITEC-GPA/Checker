using System;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Inputs
{
    /// <summary>One concomitant, already factored action state at the section centroid.
    /// Forces are N, moments Nmm. N is positive in tension. V1/V2 act along principal
    /// section axes 1/2; M1/M2 act about those axes, T about longitudinal axis 3,
    /// with the right-hand convention on the positive section face.</summary>
    public sealed class SteelActions
    {
        public double N { get; }
        public double V1 { get; }
        public double V2 { get; }
        public double T { get; }
        public double M1 { get; }
        public double M2 { get; }

        // All six components are required: absence must not silently become zero.
        public SteelActions(double n, double v1, double v2, double t, double m1, double m2)
        {
            SteelInputValidation.Finite(n, nameof(n));
            SteelInputValidation.Finite(v1, nameof(v1));
            SteelInputValidation.Finite(v2, nameof(v2));
            SteelInputValidation.Finite(t, nameof(t));
            SteelInputValidation.Finite(m1, nameof(m1));
            SteelInputValidation.Finite(m2, nameof(m2));
            N = n; V1 = v1; V2 = v2; T = t; M1 = m1; M2 = m2;
        }
    }

    /// <summary>Reuses the section code contract, adding only the optional member factor.
    /// A parameter-set label records provenance, not certification of a national annex.
    /// This is not yet a complete member-method policy (curves and methods are separate).</summary>
    public sealed class SteelDesignContext
    {
        public SectionResistanceCode SectionCode { get; }
        public double? GammaM1 { get; }

        public SteelDesignContext(SectionResistanceCode sectionCode, double? gammaM1 = null)
        {
            SectionCode = sectionCode ?? throw new ArgumentNullException(nameof(sectionCode));
            SteelInputValidation.PositiveOptional(gammaM1, nameof(gammaM1));
            if (sectionCode.Standard == SectionResistanceStandard.Ntc2018 && gammaM1.HasValue
                && gammaM1.Value != Ntc2018DesignPolicy.GammaM1)
                throw new ArgumentException("The NTC 2018 profile requires its own gammaM1.", nameof(gammaM1));
            GammaM1 = gammaM1;
        }

        public static SteelDesignContext Ntc2018()
            => new SteelDesignContext(SectionResistanceCode.Ntc2018(), Ntc2018DesignPolicy.GammaM1);
    }

    /// <summary>Immutable application input for one cut and one concomitant state.
    /// Member data are optional and separate: a section check needs no fictitious member.
    /// Construction validates the input contract; it neither selects nor executes a check.
    /// This transient request is not a Model report, persisted configuration or revision token.</summary>
    public sealed class SteelVerificationInput
    {
        public SteelSectionInput Section { get; }
        public SteelActions Actions { get; }
        public SteelDesignContext Code { get; }
        public SteelMemberInput Member { get; }

        public SteelVerificationInput(SteelSectionInput section, SteelActions actions,
            SteelDesignContext code, SteelMemberInput member = null)
        {
            Section = section ?? throw new ArgumentNullException(nameof(section));
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Member = member;
        }
    }

    internal static class SteelInputValidation
    {
        internal static void Finite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name, "A finite value is required.");
        }

        internal static void Positive(double value, string name)
        {
            Finite(value, name);
            if (value <= 0) throw new ArgumentOutOfRangeException(name, "A positive value is required.");
        }

        internal static void PositiveOptional(double? value, string name)
        {
            if (value.HasValue) Positive(value.Value, name);
        }
    }
}
