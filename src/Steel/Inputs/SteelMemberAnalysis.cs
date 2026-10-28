using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Members;

namespace GPC.Checkers.Steel.Inputs
{
    public enum SteelStabilityQuantity { Ncr1, Ncr2, NcrT, NcrFT, Mcr, AlphaUltK, AlphaCrOp }
    public enum SteelStabilityUnit { N, KN, Nmm, KNm, One }
    public enum SteelMemberAnalysisKind { UniformForkHOrT, ExternalInPlane, PrismaticCompression, PrismaticBending, HBeamColumn }

    /// <summary>Positive analysis value with explicit original units and source.
    /// Ncr/Mcr are elastic bifurcation values, never design resistances.
    /// AlphaUltK includes relevant in-plane second order and imperfections;
    /// AlphaCrOp is the elastic out-of-plane factor of the SAME load pattern.</summary>
    public sealed class SteelStabilityValue
    {
        public SteelStabilityQuantity Quantity { get; }
        public double OriginalValue { get; }
        public SteelStabilityUnit OriginalUnit { get; }
        public double Value { get; }
        public string Unit { get; }
        public string Source { get; }
        public SteelStabilityValue(SteelStabilityQuantity quantity, double value, SteelStabilityUnit unit, string source)
        {
            if (!Enum.IsDefined(typeof(SteelStabilityQuantity), quantity)) throw new ArgumentOutOfRangeException(nameof(quantity));
            if (!Enum.IsDefined(typeof(SteelStabilityUnit), unit)) throw new ArgumentOutOfRangeException(nameof(unit));
            SteelInputValidation.Positive(value, nameof(value));
            if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Analysis source required.", nameof(source));
            bool factor = quantity == SteelStabilityQuantity.AlphaUltK || quantity == SteelStabilityQuantity.AlphaCrOp;
            bool moment = quantity == SteelStabilityQuantity.Mcr;
            if (factor ? unit != SteelStabilityUnit.One : moment ? unit != SteelStabilityUnit.Nmm && unit != SteelStabilityUnit.KNm
                : unit != SteelStabilityUnit.N && unit != SteelStabilityUnit.KN)
                throw new ArgumentException("Analysis quantity and unit are dimensionally inconsistent.");
            Value = value * (unit == SteelStabilityUnit.KN ? 1e3 : unit == SteelStabilityUnit.KNm ? 1e6 : 1);
            SteelInputValidation.Positive(Value, "converted value");
            Quantity = quantity; OriginalValue = value; OriginalUnit = unit;
            Unit = factor ? "1" : moment ? "Nmm" : "N"; Source = source;
        }
    }

    /// <summary>Explicit member-analysis route bound to an immutable input AND an
    /// identified whole-member load pattern. The cut alone never implies uniformity.
    /// External sources are declarations by the caller, not certified analyses.</summary>
    public sealed class SteelMemberAnalysis
    {
        public SteelVerificationInput Input { get; }
        public SteelMemberAnalysisKind Kind { get; }
        public string LoadPatternReference { get; }
        public string ParameterSet { get; }
        public Ec3GeneralMethodCurve InPlaneCurve { get; }
        public Ec3GeneralMethodCurve LateralCurve { get; }
        public Ec3GeneralMethodCurve LateralTorsionalCurve { get; }
        public IReadOnlyList<SteelStabilityValue> Values { get; }
        public int? ExternalMemberClass { get; }
        public string ClassificationSource { get; }
        public string EffectiveSectionAnalysisSource { get; }
        public SteelPrismaticMemberData Prismatic { get; }
        public SteelBeamColumnParameters BeamColumnParameters { get; }
        public SteelPackedPairAssessment PackedPair { get; }
        /// <summary>LTB reduction of the bending and beam-column routes: General unless WithLateralTorsionalMethod selects another.</summary>
        public SteelLateralTorsionalMethod LateralTorsionalMethod { get; private set; } = SteelLateralTorsionalMethod.General;

        /// <summary>Same analysis with an explicit LTB method (EN 1993-1-1:2005 6.3.2.3 / NTC 2018 4.2.4.1.3.2 for
        /// rolled or equivalent welded I/H). Only prismatic bending and H beam-column analyses have an LTB reduction.</summary>
        public SteelMemberAnalysis WithLateralTorsionalMethod(SteelLateralTorsionalMethod method)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));
            if (Kind != SteelMemberAnalysisKind.PrismaticBending && Kind != SteelMemberAnalysisKind.HBeamColumn)
                throw new ArgumentException("The LTB method belongs to prismatic bending and H beam-column analyses.", nameof(method));
            var copy = (SteelMemberAnalysis)MemberwiseClone(); copy.LateralTorsionalMethod = method; return copy;
        }

        private SteelMemberAnalysis(SteelVerificationInput input, SteelMemberAnalysisKind kind,
            string loadPatternReference, string parameterSet, Ec3GeneralMethodCurve inPlane,
            Ec3GeneralMethodCurve lateral, Ec3GeneralMethodCurve lt, IEnumerable<SteelStabilityValue> values,
            int? memberClass, string classificationSource, SteelPrismaticMemberData prismatic = null, SteelBeamColumnParameters beamColumn = null, SteelPackedPairAssessment packedPair = null, string effectiveSectionAnalysisSource = null)
        {
            Input = input ?? throw new ArgumentNullException(nameof(input));
            if (string.IsNullOrWhiteSpace(loadPatternReference) || string.IsNullOrWhiteSpace(parameterSet))
                throw new ArgumentException("Explicit load-pattern and parameter-set references required.");
            foreach (var curve in new[] { inPlane, lateral, lt })
                if (!Enum.IsDefined(typeof(Ec3GeneralMethodCurve), curve)) throw new ArgumentOutOfRangeException(nameof(curve));
            if (lt == Ec3GeneralMethodCurve.A0) throw new ArgumentException("LTB curve a0 is not defined by this EC3 route.");
            var copy = (values ?? Enumerable.Empty<SteelStabilityValue>()).ToArray();
            if (copy.Any(v => v == null) || copy.Select(v => v.Quantity).Distinct().Count() != copy.Length)
                throw new ArgumentException("Supply each analysis quantity at most once.", nameof(values));
            if (memberClass.HasValue && (memberClass < 1 || memberClass > 4)) throw new ArgumentOutOfRangeException(nameof(memberClass));
            if (memberClass.HasValue != !string.IsNullOrWhiteSpace(classificationSource))
                throw new ArgumentException("Member class and classification source must be supplied together.");
            EffectiveSectionAnalysisSource = effectiveSectionAnalysisSource;
            Kind = kind; LoadPatternReference = loadPatternReference; ParameterSet = parameterSet;
            InPlaneCurve = inPlane; LateralCurve = lateral; LateralTorsionalCurve = lt;
            Values = Array.AsReadOnly(copy); ExternalMemberClass = memberClass; ClassificationSource = classificationSource; Prismatic = prismatic; BeamColumnParameters = beamColumn; PackedPair = packedPair;
        }

        /// <summary>EC3/NTC prismatic concentric CONSTANT compression. Curves must
        /// be established for the fabrication/grade/axis. Ncr1/2 may be supplied or
        /// calculated by Euler from explicit effective lengths. NcrT and NcrFT each
        /// require either a value or a reasoned exclusion source. No absence inference.</summary>
        public static SteelMemberAnalysis Compression(SteelVerificationInput input, string loadPatternReference,
            string parameterSet, SteelBucklingCurve curve1, SteelBucklingCurve curve2, SteelBucklingCurve torsionalCurve,
            IEnumerable<SteelStabilityValue> values = null, string torsionalExclusionSource = null,
            string flexuralTorsionalExclusionSource = null, int? memberClass = null, string classificationSource = null)
            => new SteelMemberAnalysis(input, SteelMemberAnalysisKind.PrismaticCompression, loadPatternReference, parameterSet,
                Ec3GeneralMethodCurve.A, Ec3GeneralMethodCurve.A, Ec3GeneralMethodCurve.D, values, memberClass, classificationSource,
                new SteelPrismaticMemberData(curve1, curve2, torsionalCurve, torsionalExclusionSource, flexuralTorsionalExclusionSource,
                    SteelCriticalMomentBasis.ActualMomentDiagram));

        /// <summary>Integral compression of identical packed 2C/2L. Local envelopes and
        /// complete component/connection resistances remain explicit, bound to this input.
        /// Global T/FT modes are never inferred from the sum of the component torsion constants.</summary>
        public static SteelMemberAnalysis PackedCompression(SteelPackedPairAssessment packing, string loadPatternReference,
            string parameterSet, SteelBucklingCurve curve1, SteelBucklingCurve curve2, SteelBucklingCurve torsionalCurve,
            IEnumerable<SteelStabilityValue> values = null, string torsionalExclusionSource = null,
            string flexuralTorsionalExclusionSource = null, int? memberClass = null, string classificationSource = null)
            => new SteelMemberAnalysis(packing?.Input ?? throw new ArgumentNullException(nameof(packing)),
                SteelMemberAnalysisKind.PrismaticCompression, loadPatternReference, parameterSet,
                Ec3GeneralMethodCurve.A, Ec3GeneralMethodCurve.A, Ec3GeneralMethodCurve.D, values, memberClass, classificationSource,
                new SteelPrismaticMemberData(curve1,curve2,torsionalCurve,torsionalExclusionSource,flexuralTorsionalExclusionSource,
                    SteelCriticalMomentBasis.ActualMomentDiagram), packedPair:packing);

        /// <summary>Prismatic pure major-axis bending. M1 is the member maximum,
        /// Mcr describes gross-section elastic bifurcation and the full restraint/load
        /// model. NTC requires UniformMoment basis, using f=beta=Kchi=1,lambda0=.2.
        /// EC3 uses 6.3.2.2 recommended Table6.4 curves; NTC uses its Table4.2.IX.
        /// WithLateralTorsionalMethod selects EC3 6.3.2.3 / NTC f, beta, lambda0 for rolled or welded I/H.
        /// Shear resistance/interaction at every cut must be checked separately.</summary>
        public static SteelMemberAnalysis Bending(SteelVerificationInput input, string loadPatternReference,
            string parameterSet, SteelCriticalMomentBasis basis, SteelStabilityValue criticalMoment,
            int? memberClass = null, string classificationSource = null)
            => new SteelMemberAnalysis(input, SteelMemberAnalysisKind.PrismaticBending, loadPatternReference, parameterSet,
                Ec3GeneralMethodCurve.A, Ec3GeneralMethodCurve.A, Ec3GeneralMethodCurve.D,
                criticalMoment == null ? null : new[] { criticalMoment }, memberClass, classificationSource,
                new SteelPrismaticMemberData(SteelBucklingCurve.A, SteelBucklingCurve.A, SteelBucklingCurve.D, null, null, basis));

        /// <summary>Prismatic doubly symmetric H beam-column, EC3 Annex B / NTC
        /// Circular Method B. N is constant compression; M1/M2 are signed absolute
        /// maxima over ONE whole-member design state, not a concomitant section cut.
        /// Values are Ncr1/Ncr2 (or explicit effective lengths) and Mcr if susceptible.
        /// Cm and global analysis assumptions are explicit. Other compression modes
        /// must be independently assessed as excluded; no generic warping analysis.
        /// Section resistance and shear checks remain separately required; use
        /// SelectMember with Member scope only, never SectionAndMember.</summary>
        public static SteelMemberAnalysis BeamColumn(SteelVerificationInput input, string loadPatternReference,
            string parameterSet, SteelBeamColumnParameters parameters, IEnumerable<SteelStabilityValue> values = null,
            int? memberClass = null, string classificationSource = null)
            => new SteelMemberAnalysis(input, SteelMemberAnalysisKind.HBeamColumn, loadPatternReference, parameterSet,
                Ec3GeneralMethodCurve.A, Ec3GeneralMethodCurve.A, Ec3GeneralMethodCurve.D, values, memberClass, classificationSource,
                beamColumn: parameters ?? throw new ArgumentNullException(nameof(parameters)));

        /// <summary>Caller explicitly declares a uniform sharp H/T, fork ends/free
        /// warping, no sway/intermediate restraints/transverse loads/plastic hinges,
        /// centroidal constant N and constant first-order M1 over Member.Length.
        /// In-plane imperfections are calculated by the existing qualified method.</summary>
        public static SteelMemberAnalysis UniformFork(SteelVerificationInput input, string declaration,
            string parameterSet, Ec3GeneralMethodCurve inPlane, Ec3GeneralMethodCurve lateral, Ec3GeneralMethodCurve lt)
            => new SteelMemberAnalysis(input, SteelMemberAnalysisKind.UniformForkHOrT, declaration, parameterSet,
                inPlane, lateral, lt, null, null, null);

        /// <summary>EC3 general method in-plane route. AlphaUltK must include relevant
        /// second-order effects and imperfections; AlphaCrOp must use the same
        /// proportional load pattern. Ncr/Mcr alone are insufficient. The member class
        /// is not the section-only sigma_com,Ed reclassification. For NTC2018 the
        /// caller selects EN6.3.4 under NTC4.2.4.1.3.3 with NTC gammaM1. ParameterSet
        /// identifies the assessed national applicability, curves and absence of rotated plastic hinges.</summary>
        public static SteelMemberAnalysis FromExternal(SteelVerificationInput input, string loadPatternReference,
            string parameterSet, Ec3GeneralMethodCurve lateral, Ec3GeneralMethodCurve lt,
            IEnumerable<SteelStabilityValue> values, int? memberClass = null, string classificationSource = null)
            => new SteelMemberAnalysis(input, SteelMemberAnalysisKind.ExternalInPlane, loadPatternReference, parameterSet,
                Ec3GeneralMethodCurve.A, lateral, lt, values, memberClass, classificationSource);

        /// <summary>External general method for class4. The source must identify an
        /// in-plane analysis using effective sections, centroid-shift moments and all
        /// relevant imperfections/second order at characteristic resistance. It must
        /// also identify the elastic out-of-plane analysis of the SAME load pattern.
        /// This factory does not compute or certify either external multiplier.</summary>
        public static SteelMemberAnalysis FromExternalEffective(SteelVerificationInput input, string loadPatternReference,
            string parameterSet, Ec3GeneralMethodCurve lateral, Ec3GeneralMethodCurve lt,
            IEnumerable<SteelStabilityValue> values, string classificationSource, string effectiveSectionAnalysisSource)
        {
            if(string.IsNullOrWhiteSpace(effectiveSectionAnalysisSource))
                throw new ArgumentException("Effective-section analysis provenance required.",nameof(effectiveSectionAnalysisSource));
            return new SteelMemberAnalysis(input,SteelMemberAnalysisKind.ExternalInPlane,loadPatternReference,parameterSet,
                Ec3GeneralMethodCurve.A,lateral,lt,values,4,classificationSource,effectiveSectionAnalysisSource:effectiveSectionAnalysisSource);
        }
    }
}
