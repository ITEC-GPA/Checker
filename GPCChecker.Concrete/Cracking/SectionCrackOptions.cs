using System;
using GPC.Checkers.Concrete.Serviceability;

namespace GPC.Checkers.Concrete.Cracking
{
    /// <summary>
    /// Options of the section crack check (0.0.18.0), passed with the constructor of <see cref="SectionCrackInput"/> that takes them.
    /// <see cref="Default"/> is the behaviour of 0.0.17.0, used by the constructor without options: Details, Status, Outcome and the exceptions
    /// (type, message, parameter) do not change. Immutable: start from <see cref="Default"/> and use the With methods, which return a copy.
    /// </summary>
    public sealed class SectionCrackOptions
    {
        /// <summary>Behaviour of 0.0.17.0: validation in the constructor, DIN condition with <see cref="SectionCrackInput.NominalCover"/>, no trace.</summary>
        public static SectionCrackOptions Default { get; } = new SectionCrackOptions();

        /// <summary>
        /// Validates the data where they enter the calculation instead of up front (false by default), as ANTHEA does. With true:
        /// <list type="bullet">
        /// <item>the design limit only where it is used, i.e. Eurocode family and Model Code 2010 in the required combination
        /// (<see cref="CrackRequirements.For(CrackProfile, ServiceabilityCombination, string, bool, double?, SectionCrackOptions)"/>):
        /// NTC 2018 and UNI ignore it, a combination where the check is not required gives NotRequired;</item>
        /// <item><see cref="SectionCrackInput.NominalCover"/>, <see cref="SectionCrackInput.CoverOverride"/> and <see cref="SectionCrackInput.SpacingOverride"/>
        /// in the branch that uses them (same criteria as the constructor, same <see cref="ArgumentOutOfRangeException"/> and parameter name), not in the
        /// constructor: decompression, crack formation, not-required states and the branches that stop earlier do not read them; the cover override replaces
        /// the nominal cover, so an invalid nominal cover with a valid override is never read by the width formula;</item>
        /// <item>the bar stresses after the return of the entirely compressed section (wk = 0 also with missing or non-finite stresses), before the other
        /// branches, with the code <see cref="CrackRejection.BarStresses"/> and the message "Cracking: bar stresses missing or not finite.". With
        /// <see cref="SectionCrackInput.NtcK2FromCompressedBars"/> the bar stresses choose k2 and are checked before any branch, as in 0.0.17.0.</item>
        /// </list>
        /// The other checks of the constructor (null arguments, one stress per bar, Es, Ecm, fctm) do not move.
        /// </summary>
        public bool ValidateAtUse { get; private set; }

        /// <summary>
        /// Cover c of the DIN condition (h − x)/3 ≥ c + 20 mm of the effective depth (DIN EN 1992-1-1 NCI 7.3.2(3), <see cref="CrackSectionGeometry.EffectiveDepth"/>),
        /// mm; null (default) = <see cref="SectionCrackInput.NominalCover"/>, as in 0.0.17.0. It changes only that condition, for the DIN profile, in a
        /// partially compressed section: the width formula keeps <see cref="SectionCrackInput.CoverOverride"/> or the nominal cover. Any finite value is
        /// accepted, also negative, since it only enters the comparison (ANTHEA reads it with its own fallback).
        /// </summary>
        public double? EffectiveDepthCover { get; private set; }

        /// <summary>
        /// Fills <see cref="SectionCrackResult.Trace"/> (the calculated entries of ANTHEA in their order, with the stable codes of <see cref="CrackTraceCodes"/>)
        /// and <see cref="SectionCrackResult.RegionOutcomes"/> (false by default: both empty). Nothing else changes: Details, Status, Outcome, the numbers
        /// and the exceptions are those without the trace.
        /// </summary>
        public bool Trace { get; private set; }

        /// <summary>
        /// Modulus of the concrete in the stress analysis, MPa (analysis context, null by default): with <see cref="Trace"/> the trace adds "Ecls analisi" and
        /// "n analisi" = Es (1 + φ)/Ecls (<see cref="CrackTraceCodes.AnalysisConcreteModulus"/>, <see cref="CrackTraceCodes.AnalysisModularRatio"/>) before the
        /// width formula of the partially compressed section. Set with <see cref="AnalysisPsiRebar"/> by <see cref="WithAnalysisContext"/>.
        /// </summary>
        public double? AnalysisConcreteModulus { get; private set; }

        /// <summary>Creep coefficient φ of the bars in the stress analysis (analysis context, null by default), see <see cref="AnalysisConcreteModulus"/>.</summary>
        public double? AnalysisPsiRebar { get; private set; }

        private SectionCrackOptions() { }

        /// <summary>Copy with <see cref="Trace"/>.</summary>
        public SectionCrackOptions WithTrace(bool trace)
        {
            var copy = Copy(); copy.Trace = trace; return copy;
        }

        /// <summary>
        /// Copy with the analysis context: both values or none (null, null removes it). The modulus must be finite and positive, φ finite; otherwise
        /// <see cref="ArgumentOutOfRangeException"/>; only one of the two gives <see cref="ArgumentException"/>.
        /// </summary>
        public SectionCrackOptions WithAnalysisContext(double? concreteModulus, double? psiRebar)
        {
            if (concreteModulus.HasValue != psiRebar.HasValue) throw new ArgumentException("Cracking: the analysis context needs both the concrete modulus and the creep coefficient.");
            if (concreteModulus.HasValue && !CrackSectionGeometry.Positive(concreteModulus.Value)) throw new ArgumentOutOfRangeException(nameof(concreteModulus));
            if (psiRebar.HasValue && (double.IsNaN(psiRebar.Value) || double.IsInfinity(psiRebar.Value))) throw new ArgumentOutOfRangeException(nameof(psiRebar));
            var copy = Copy(); copy.AnalysisConcreteModulus = concreteModulus; copy.AnalysisPsiRebar = psiRebar; return copy;
        }

        /// <summary>Copy with <see cref="ValidateAtUse"/>.</summary>
        public SectionCrackOptions WithValidateAtUse(bool validateAtUse)
        {
            var copy = Copy(); copy.ValidateAtUse = validateAtUse; return copy;
        }

        /// <summary>Copy with <see cref="EffectiveDepthCover"/> (null = nominal cover); a NaN or infinite value gives <see cref="ArgumentOutOfRangeException"/>.</summary>
        public SectionCrackOptions WithEffectiveDepthCover(double? effectiveDepthCover)
        {
            if (effectiveDepthCover.HasValue && (double.IsNaN(effectiveDepthCover.Value) || double.IsInfinity(effectiveDepthCover.Value)))
                throw new ArgumentOutOfRangeException(nameof(effectiveDepthCover));
            var copy = Copy(); copy.EffectiveDepthCover = effectiveDepthCover; return copy;
        }

        private SectionCrackOptions Copy() => (SectionCrackOptions)MemberwiseClone();
    }
}
