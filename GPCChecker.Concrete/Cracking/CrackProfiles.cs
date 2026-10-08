using System;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Cracking
{
    /// <summary>Crack-control profiles implemented for ordinary reinforced concrete sections.</summary>
    public enum CrackProfile { Ntc2018, ModelCode2010, EN1992p11, UniEN1992p11, DinEN1992p11, DsEN1992p11, NsEN1992p11, CnrDT200 }

    /// <summary>
    /// Family of the crack-width formula of a profile (0.0.18.0, <see cref="CrackProfiles.WidthFormula"/>): the formula of the Circolare 2019 C4.1.2.2.4.5
    /// (ANTHEA Ntc2018Checks.CalculateCrackWidth) or the Eurocode family, Model Code 2010 included (ANTHEA ConcreteCodeChecks.CrackWidth). The variants of
    /// sr,max inside the family are in the flags of the trace (<see cref="CrackTraceFlags.FormulaStandard"/>, <see cref="CrackTraceFlags.FormulaModelCode2010"/>...).
    /// </summary>
    public enum CrackWidthFormula
    {
        /// <summary>wk = 1.7 Δsm (εsm − εcm), with Δsm,near and Δsm,far: NTC 2018 and CNR-DT 200.</summary>
        Ntc2018,
        /// <summary>wk = sr,max (εsm − εcm), (7.8)-(7.14) with the national annexes, and Model Code 2010: EN, UNI, DIN, DS, NS EN 1992-1-1 and Model Code 2010.</summary>
        Eurocode
    }

    /// <summary>
    /// Maps a typed standard to its crack-control profile by exact type, without fallback. CS-TR34 does not define the crack width of a
    /// beam section (<see cref="NotApplicableReason"/>); CNR-DT 204 is known but its fibre-reinforced crack model is not implemented
    /// (<see cref="NotSupportedReason"/>). American standards (ACI 318, AASHTO) are a future implementation.
    /// </summary>
    public static class CrackProfiles
    {
        public static bool TryResolve(Standard standard, out CrackProfile profile)
        {
            profile = default(CrackProfile);
            if (standard == null) return false;
            var type = standard.GetType();
            if (type == typeof(StandardNTC2018Concrete)) profile = CrackProfile.Ntc2018;
            else if (type == typeof(StandardModelCode2010)) profile = CrackProfile.ModelCode2010;
            else if (type == typeof(StandardEN1992p11)) profile = CrackProfile.EN1992p11;
            else if (type == typeof(StandardUNIEN1992p11)) profile = CrackProfile.UniEN1992p11;
            else if (type == typeof(StandardDINEN1992p11)) profile = CrackProfile.DinEN1992p11;
            else if (type == typeof(StandardDSEN1992p11)) profile = CrackProfile.DsEN1992p11;
            else if (type == typeof(StandardNSEN1992p11)) profile = CrackProfile.NsEN1992p11;
            else if (type == typeof(StandardCNR200)) profile = CrackProfile.CnrDT200;
            else return false;
            return true;
        }

        public static string NotApplicableReason(Standard standard)
        {
            if (standard != null && standard.GetType() == typeof(StandardCSTR34))
                return "CS-TR34 covers ground-supported floor slabs: it does not define the crack width check of a beam section.";
            return null;
        }

        public static string NotSupportedReason(Standard standard)
        {
            if (standard != null && standard.GetType() == typeof(StandardCNR204))
                return "CNR-DT 204: the crack width of fibre-reinforced concrete (residual strength in the tension stiffening) is not implemented.";
            return null;
        }

        public static CrackProfile Resolve(Standard standard)
        {
            if (standard == null) throw new ArgumentNullException(nameof(standard));
            if (!TryResolve(standard, out var profile))
                throw new NotSupportedException(NotApplicableReason(standard) ?? NotSupportedReason(standard) ?? "Cracking: no implemented profile for "
                    + standard.GetType().Name + (standard is StandardACI318 ? " (American standards: future implementation)." : "."));
            return profile;
        }

        /// <summary>The NTC 2018 crack-width formula and requirement table (also CNR-DT 200 without FRP data).</summary>
        internal static bool IsNtc(CrackProfile p) => p == CrackProfile.Ntc2018 || p == CrackProfile.CnrDT200;

        /// <summary>The requirement comes from NTC 2018 Tab. 4.1.IV (NTC 2018, UNI EN 1992-1-1, CNR-DT 200): the design limit is not used.</summary>
        internal static bool HasNtcRequirementTable(CrackProfile p) => IsNtc(p) || p == CrackProfile.UniEN1992p11;

        /// <summary>
        /// Family of the crack-width formula of the profile (0.0.18.0): <see cref="CrackWidthFormula.Ntc2018"/> for NTC 2018 and CNR-DT 200,
        /// <see cref="CrackWidthFormula.Eurocode"/> for the others. It is the formula of <see cref="CrackWidthCalculator"/>,
        /// of the upper bound without bonded bars (1.7 · 0.75 (h − x) or 1.3 (h − x)) and of the entries of the trace (<see cref="CrackTraceCodes"/>: the NTC
        /// formula writes the entries from Es to wk, the Eurocode family ρ, αe, kt, k₂, σs, c, Øeq, s, sr,max, εsm − εcm, β and wk). A profile outside the enumeration
        /// gives <see cref="ArgumentOutOfRangeException"/>.
        /// </summary>
        public static CrackWidthFormula WidthFormula(CrackProfile profile)
        {
            RequireDefined(profile);
            return IsNtc(profile) ? CrackWidthFormula.Ntc2018 : CrackWidthFormula.Eurocode;
        }

        /// <summary>
        /// True when the requirement of the profile reads the design limit wlim (0.0.18.0): Eurocode family and Model Code 2010, in the combination where the
        /// check is required (<see cref="CrackRequirements"/>).
        /// False for NTC 2018, UNI EN 1992-1-1 and CNR-DT 200, whose requirement is NTC 2018 Tab. 4.1.IV by exposure, sensitivity and combination: the design
        /// limit is not used (ANTHEA Ntc2018Checks.CrackRequirement does not read 'limite_fessure'). A profile outside the enumeration gives <see cref="ArgumentOutOfRangeException"/>.
        /// </summary>
        public static bool UsesDesignLimit(CrackProfile profile)
        {
            RequireDefined(profile);
            return !HasNtcRequirementTable(profile);
        }

        /// <summary>
        /// True when the effective depth hc,eff of a partially compressed section reads the cover (0.0.18.0): DIN EN 1992-1-1 only, condition
        /// (h − x)/3 ≥ c + 20 mm of NCI 7.3.2(3) (<see cref="CrackSectionGeometry.EffectiveDepth"/>, <see cref="SectionCrackOptions.EffectiveDepthCover"/>). The other
        /// profiles never read <see cref="SectionCrackOptions.EffectiveDepthCover"/>. A profile outside the enumeration gives <see cref="ArgumentOutOfRangeException"/>.
        /// </summary>
        public static bool EffectiveDepthReadsCover(CrackProfile profile)
        {
            RequireDefined(profile);
            return ReadsDinCover(profile);
        }

        /// <summary>Profile of the DIN condition of hc,eff (no check of the enumeration, for the calculation).</summary>
        internal static bool ReadsDinCover(CrackProfile p) => p == CrackProfile.DinEN1992p11;

        private static void RequireDefined(CrackProfile profile)
        {
            if (!Enum.IsDefined(typeof(CrackProfile), profile)) throw new ArgumentOutOfRangeException(nameof(profile));
        }

        public static string Reference(CrackProfile profile)
        {
            switch (profile)
            {
                case CrackProfile.Ntc2018: return "NTC 2018 §4.1.2.2.4 and Circolare 2019 C4.1.2.2.4";
                case CrackProfile.ModelCode2010: return "fib MC2010 §7.6.4";
                case CrackProfile.EN1992p11: return "EN 1992-1-1 §7.3.2-7.3.4";
                case CrackProfile.UniEN1992p11: return "UNI EN 1992-1-1 §7.3.4 (requirements of NTC 2018)";
                case CrackProfile.DinEN1992p11: return "DIN EN 1992-1-1 §7.3 with NA";
                case CrackProfile.DsEN1992p11: return "DS/EN 1992-1-1 §7.3 with DK NA";
                case CrackProfile.NsEN1992p11: return "NS-EN 1992-1-1 §7.3 with NA";
                case CrackProfile.CnrDT200: return "CNR-DT 200 R1/2013 with NTC 2018 §4.1.2.2.4 for the RC member";
                default: throw new ArgumentOutOfRangeException(nameof(profile));
            }
        }
    }
}
