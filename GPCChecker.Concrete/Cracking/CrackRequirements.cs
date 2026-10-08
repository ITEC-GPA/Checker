using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GPC.Checkers.Concrete.Serviceability;

namespace GPC.Checkers.Concrete.Cracking
{
    /// <summary>What the standard requires for a serviceability combination.</summary>
    public enum CrackCriterion
    {
        /// <summary>No crack check for this combination (it is required for another one).</summary>
        NotRequired,
        /// <summary>The exposure class is required to select the criterion.</summary>
        ExposureRequired,
        /// <summary>A design limit wlim is required (no default of the standard for these data).</summary>
        DesignLimitRequired,
        /// <summary>Decompression: no tension in the uncracked homogenized section.</summary>
        Decompression,
        /// <summary>Crack formation: tension of the uncracked section within fctm/1.2.</summary>
        CrackFormation,
        /// <summary>Crack width wk ≤ wlim.</summary>
        CrackWidth
    }

    public sealed class CrackRequirement
    {
        public CrackCriterion Criterion { get; }
        /// <summary>wlim, mm, for <see cref="CrackCriterion.CrackWidth"/>; null otherwise.</summary>
        public double? Limit { get; }
        /// <summary>For NotRequired: the combination where the standard requires the check.</summary>
        public ServiceabilityCombination? RequiredCombination { get; }
        internal CrackRequirement(CrackCriterion criterion, double? limit = null, ServiceabilityCombination? required = null)
        { Criterion = criterion; Limit = limit; RequiredCombination = required; }
    }

    /// <summary>
    /// Crack requirement per standard, transferred from ANTHEA (Ntc2018Checks.CrackRequirement and ConcreteCodeChecks.CrackRequirement, commit fe4652c).
    /// NTC 2018 / UNI: Tab. 4.1.III-IV by environment (ordinary, aggressive, very aggressive), reinforcement sensitivity and frequent or
    /// quasi-permanent combination; the design limit is not used. Eurocode family and Model Code 2010: quasi-permanent combination (NS: frequent
    /// for XD3/XS3), a design limit overrides the table; Model Code 2010 needs it. Tables: EN Table 7.1N (also DIN), DK NA, NS NA.
    /// </summary>
    public static class CrackRequirements
    {
        // The list the requirements read (0.0.18.0): private, so a change of the public array Exposures no longer moves the NTC environment groups.
        // The NTC groups are the index ranges of this order (see Ntc).
        private static readonly string[] ExposureOrder = { "X0", "XC1", "XC2", "XC3", "XF1", "XC4", "XD1", "XS1", "XA1", "XA2", "XF2", "XF3", "XD2", "XD3", "XS2", "XS3", "XA3", "XF4" };

        /// <summary>
        /// Exposure classes in the order of the NTC environment groups: X0..XF1 ordinary, XC4..XF3 aggressive, XD2..XF4 very aggressive (0.0.18.0).
        /// Read-only view of the private list that the requirements read.
        /// </summary>
        public static IReadOnlyList<string> ExposureClasses { get; } = new ReadOnlyCollection<string>(ExposureOrder);

        /// <summary>
        /// The classes of <see cref="ExposureClasses"/> as a writable array, kept for binary compatibility with 0.0.17.0. Since 0.0.18.0 the library no
        /// longer reads it: changing its elements does not change the requirements. Use <see cref="ExposureClasses"/>.
        /// </summary>
        public static readonly string[] Exposures = ExposureOrder.ToArray();

        /// <param name="exposure">Exposure class of <see cref="ExposureClasses"/>; null when not given.</param>
        /// <param name="sensitive">Reinforcement sensitive to corrosion (NTC 2018 §4.1.2.2.4).</param>
        /// <param name="designLimit">Design wlim, mm, when assigned (Eurocode family and Model Code 2010).</param>
        public static CrackRequirement For(CrackProfile profile, ServiceabilityCombination combination, string exposure, bool sensitive, double? designLimit = null)
            => For(profile, combination, exposure, sensitive, designLimit, SectionCrackOptions.Default);

        /// <summary>
        /// Requirement with the options of the crack check (0.0.18.0). With <see cref="SectionCrackOptions.ValidateAtUse"/> the design limit is validated
        /// only where it is used (Eurocode family and Model Code 2010 in the required combination), otherwise always, as the overload without options.
        /// </summary>
        /// <param name="options">Required: <see cref="SectionCrackOptions.Default"/> for the behaviour of 0.0.17.0.</param>
        public static CrackRequirement For(CrackProfile profile, ServiceabilityCombination combination, string exposure, bool sensitive, double? designLimit,
            SectionCrackOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (exposure != null && !ExposureOrder.Contains(exposure)) throw new ArgumentException("Unknown exposure class: " + exposure);
            if (!options.ValidateAtUse) RequireDesignLimit(designLimit);
            if (CrackProfiles.IsNtc(profile) || profile == CrackProfile.UniEN1992p11) return Ntc(combination, exposure, sensitive);
            var required = profile == CrackProfile.NsEN1992p11 && (exposure == "XD3" || exposure == "XS3") ? ServiceabilityCombination.Frequent : ServiceabilityCombination.QuasiPermanent;
            if (combination != required) return new CrackRequirement(CrackCriterion.NotRequired, null, required);
            if (designLimit.HasValue)
            {
                RequireDesignLimit(designLimit);
                return new CrackRequirement(CrackCriterion.CrackWidth, designLimit);
            }
            if (profile == CrackProfile.ModelCode2010) return new CrackRequirement(CrackCriterion.DesignLimitRequired);
            double? limit;
            if (profile == CrackProfile.DsEN1992p11)
            {
                switch (exposure)
                {
                    case "XD2": case "XD3": case "XS3": limit = .2; break;
                    case "XD1": case "XS1": case "XS2": limit = .3; break;
                    case "XC2": case "XC3": case "XC4": limit = .4; break;
                    default: return new CrackRequirement(CrackCriterion.DesignLimitRequired);
                }
            }
            else if (profile == CrackProfile.NsEN1992p11)
            {
                switch (exposure)
                {
                    case "X0": limit = .4; break;
                    case "XC1": case "XC2": case "XC3": case "XC4": case "XD1": case "XD2": case "XD3": case "XS1": case "XS2": case "XS3": limit = .3; break;
                    default: return new CrackRequirement(exposure == null ? CrackCriterion.ExposureRequired : CrackCriterion.DesignLimitRequired);
                }
            }
            else
            {
                switch (exposure)
                {
                    case "X0": case "XC1": limit = .4; break;
                    case "XC2": case "XC3": case "XC4": case "XD1": case "XD2": case "XD3": case "XS1": case "XS2": case "XS3": limit = .3; break;
                    default: return new CrackRequirement(exposure == null ? CrackCriterion.ExposureRequired : CrackCriterion.DesignLimitRequired);
                }
            }
            return new CrackRequirement(CrackCriterion.CrackWidth, limit);
        }

        private static void RequireDesignLimit(double? designLimit)
        {
            if (designLimit.HasValue && (double.IsNaN(designLimit.Value) || double.IsInfinity(designLimit.Value) || designLimit <= 0))
                throw new ArgumentOutOfRangeException(nameof(designLimit));
        }

        private static CrackRequirement Ntc(ServiceabilityCombination combination, string exposure, bool sensitive)
        {
            if (combination == ServiceabilityCombination.Characteristic) return new CrackRequirement(CrackCriterion.NotRequired);
            int index = exposure == null ? -1 : Array.IndexOf(ExposureOrder, exposure);
            if (index < 0) return new CrackRequirement(CrackCriterion.ExposureRequired);
            bool qp = combination == ServiceabilityCombination.QuasiPermanent;
            int environment = index <= 4 ? 0 : index <= 11 ? 1 : 2; // legacy list starts with a placeholder: ≤5 / ≤12 there
            if (sensitive && environment >= 1 && qp) return new CrackRequirement(CrackCriterion.Decompression);
            if (sensitive && environment == 2 && !qp) return new CrackRequirement(CrackCriterion.CrackFormation);
            double limit = environment == 0 ? sensitive ? qp ? .2 : .3 : qp ? .3 : .4 : environment == 1 ? sensitive ? .2 : qp ? .2 : .3 : .2;
            return new CrackRequirement(CrackCriterion.CrackWidth, limit);
        }
    }
}
