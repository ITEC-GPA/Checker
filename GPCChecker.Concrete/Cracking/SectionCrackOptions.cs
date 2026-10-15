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
        /// the nominal cover, so an invalid nominal cover with a valid override is never read by the width formula. Exception: with the DIN profile and
        /// <see cref="EffectiveDepthCover"/> null, the DIN condition of the effective depth reads the nominal cover in every partially compressed section with
        /// tensile bars, so it is validated there, before the branch of the tensile bars outside Ac,eff, also when that branch follows;</item>
        /// <item>the bar stresses after the return of the entirely compressed section (wk = 0 also with missing or non-finite stresses), before the other
        /// branches, with the code <see cref="CrackRejection.BarStresses"/> and the message "Cracking: bar stresses missing or not finite.". With
        /// <see cref="SectionCrackInput.NtcK2FromCompressedBars"/> the bar stresses choose k2 and are checked before any branch, as in 0.0.17.0. Since
        /// 0.0.25.0 this includes the number of stresses (one per ordinary bar), which the constructor checks only without ValidateAtUse; with the rule
        /// before D7-b a wrong number gives the code <see cref="CrackRejection.BarStresses"/> with the message of <see cref="CrackWidthCalculator.K2"/>.</item>
        /// </list>
        /// The other checks of the constructor (null arguments, Es, Ecm, fctm) do not move.
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
        /// width formula of the partially compressed section, computed by <see cref="Homogenization.ModularRatio"/> with <see cref="SectionCrackInput.Es"/>.
        /// Set with <see cref="AnalysisPsiRebar"/> by <see cref="WithAnalysisContext"/>.
        /// </summary>
        public double? AnalysisConcreteModulus { get; private set; }

        /// <summary>Creep coefficient φ of the bars in the stress analysis (analysis context, null by default), see <see cref="AnalysisConcreteModulus"/>.</summary>
        public double? AnalysisPsiRebar { get; private set; }

        /// <summary>
        /// fib Model Code 2010 (§7.6.4.4, Tab. 7.6-2) with the stage of cracking (false by default: the rule of 0.0.17.0, τbms/fctm = 1.8 short and 1.35 long
        /// term with β = kt, kept for the frozen fixtures and the contract K0). With true, σsr = fctm/ρ (1 + αe ρ) chooses the stage: stabilized cracking
        /// (σs ≥ σsr) with τbms = 1.8 fctm, β = 0.6 short and 0.4 long term, εsm − εcm = (σs − β σsr)/Es; single crack formation (σs &lt; σsr) with β = 0.6,
        /// τbms = 1.8 fctm short and 1.35 fctm long term, εsm − εcm = (1 − β) σs/Es. Without bonded bars in Ac,eff the upper bound uses the formation stage
        /// (βmin = 1 − 0.6). Shrinkage (ηr εsh) is not considered. Values from secondary sources (ANTHEA registro-differenze R1, decision of the user 15/10/2026).
        /// </summary>
        public bool ModelCode2010BondStages { get; private set; }

        /// <summary>
        /// DIN EN 1992-1-1/NA: kt = 0.6 short and 0.4 long term, as EN 1992-1-1 7.3.4(2) (false by default: kt = 0.4 for every duration, the fixed value of
        /// DIN 1045-1 kept for the frozen fixtures and the contract K0). ANTHEA registro-differenze R2, decision of the user 15/10/2026.
        /// </summary>
        public bool DinKtByDuration { get; private set; }

        /// <summary>
        /// A tensile zone without reinforcement fails the crack-width check (false by default: no verdict, <see cref="SectionCrackResult.Passed"/> null, as in
        /// 0.0.17.0). With true <see cref="SectionCrackResult.Passed"/> is false, with the same <see cref="SectionCrackResult.Outcome"/> and no width, in three
        /// cases: <see cref="CrackOutcome.NoTensileReinforcement"/> (no tensile bar and the neutral axis not within the cover of a bar of the tensile edge, see
        /// below); a face of the entirely tensile section with concrete but no bar in its effective band (<see cref="CrackReason.FaceWithoutAreaOrSteel"/>); an
        /// inner surface in tension without effective reinforcement (<see cref="CrackOutcome.InnerSurfaceUnreinforced"/>). The cracks of such a zone are not
        /// controlled by any bar (EN 1992-1-1 7.3.2(1)); the tensile strength of the concrete is not used. ANTHEA F2-7 point 3, decision of the user 15/10/2026.
        /// <para>
        /// The neutral axis within the cover of a reinforced face keeps wk = 0. Without the option a face counts as reinforced when the bar nearest to the
        /// tensile edge is closer than h/2 (rule of 0.0.17.0); with the option (ANTHEA bug analysis 15/10/2026, FES-1) a bar must lie within its cover
        /// c + Ø/2 measured along the strain gradient (projected: (c + Ø/2)(|qx| + |qy|) at the corner of a rectangle; on circles plus the sagitta
        /// r (1 − cos(π/n)) of a regular ring of n bars), or the tensile zone must be no deeper than that projected cover with a bar within c + Ø/2 of the
        /// tensile surface; c is <see cref="SectionCrackInput.CoverOverride"/> or the nominal cover (validated there with <see cref="ValidateAtUse"/>).
        /// A detail "a,c" gives the projected cover of the governing bar. Bars at h/2 from the edge, a central mesh or side bars no longer give wk = 0.
        /// </para>
        /// </summary>
        public bool UnreinforcedTensileZoneFails { get; private set; }

        /// <summary>
        /// Cover c of the width formula in the tensile zone of a partially compressed section without <see cref="SectionCrackInput.CoverOverride"/>: with true
        /// the smallest free cover of the effective bars to the tensile face, as the faces of the entirely tensile section already measure it from the bar
        /// positions, so that bars placed deeper than the nominal cover (free bars) enter k3 c, 5 (c + Ø/2) and Δsm with their real cover. The free cover of a
        /// bar is its perpendicular distance, minus Ø/2, to the face of the outline that the strain gradient reaches first from the bar centre: the cover to
        /// the tensile face, not to a side face (a corner bar of a beam with 40 mm side and 60 mm bottom cover bent about x gives 60), and in biaxial bending
        /// the cover to the face the gradient points to, so a bar at the nominal cover keeps the nominal cover. On circular sections
        /// (<see cref="CrackSectionGeometry.Circular"/>) it is radial, R − r − Ø/2 with R the radius of the outline vertices, so a ring at the nominal cover
        /// keeps it (the polygon edges would give up to R (1 − cos(π/N)) less). Holes are not considered (their surfaces have their own bands).
        /// <see cref="SectionCrackInput.NominalCover"/> only when that cover is not positive or not determined. False by default:
        /// <see cref="SectionCrackInput.NominalCover"/>, the rule of 0.0.17.0 kept for the frozen fixtures and the contract K0. The trace marks the value with
        /// <see cref="CrackTraceFlags.BarGeometry"/> and the detail "c" reads "smallest free cover of the effective bars to the tensile face". Automatic
        /// layouts (bars at the nominal cover) give the nominal cover up to rounding. The cover of the DIN condition (<see cref="EffectiveDepthCover"/>) and
        /// of the neutral axis within the cover (<see cref="UnreinforcedTensileZoneFails"/>) do not change. ANTHEA bug analysis 15/10/2026, FES-2.
        /// </summary>
        public bool CoverFromBarGeometry { get; private set; }

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

        /// <summary>Copy with <see cref="ModelCode2010BondStages"/>.</summary>
        public SectionCrackOptions WithModelCode2010BondStages(bool bondStages)
        {
            var copy = Copy(); copy.ModelCode2010BondStages = bondStages; return copy;
        }

        /// <summary>Copy with <see cref="DinKtByDuration"/>.</summary>
        public SectionCrackOptions WithDinKtByDuration(bool ktByDuration)
        {
            var copy = Copy(); copy.DinKtByDuration = ktByDuration; return copy;
        }

        /// <summary>Copy with <see cref="UnreinforcedTensileZoneFails"/>.</summary>
        public SectionCrackOptions WithUnreinforcedTensileZoneFails(bool fails)
        {
            var copy = Copy(); copy.UnreinforcedTensileZoneFails = fails; return copy;
        }

        /// <summary>Copy with <see cref="CoverFromBarGeometry"/>.</summary>
        public SectionCrackOptions WithCoverFromBarGeometry(bool fromBarGeometry)
        {
            var copy = Copy(); copy.CoverFromBarGeometry = fromBarGeometry; return copy;
        }

        private SectionCrackOptions Copy() => (SectionCrackOptions)MemberwiseClone();
    }
}
