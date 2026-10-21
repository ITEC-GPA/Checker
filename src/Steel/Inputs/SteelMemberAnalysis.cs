using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Members;

namespace GPC.Checkers.Steel.Inputs
{
    public enum SteelStabilityQuantity { Ncr1, Ncr2, NcrT, NcrFT, Mcr, AlphaUltK, AlphaCrOp }
    public enum SteelStabilityUnit { N, KN, Nmm, KNm, One }
    public enum SteelMemberAnalysisKind { UniformForkHOrT, ExternalInPlane }

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

        private SteelMemberAnalysis(SteelVerificationInput input, SteelMemberAnalysisKind kind,
            string loadPatternReference, string parameterSet, Ec3GeneralMethodCurve inPlane,
            Ec3GeneralMethodCurve lateral, Ec3GeneralMethodCurve lt, IEnumerable<SteelStabilityValue> values,
            int? memberClass, string classificationSource)
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
            Kind = kind; LoadPatternReference = loadPatternReference; ParameterSet = parameterSet;
            InPlaneCurve = inPlane; LateralCurve = lateral; LateralTorsionalCurve = lt;
            Values = Array.AsReadOnly(copy); ExternalMemberClass = memberClass; ClassificationSource = classificationSource;
        }

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
        /// is not the section-only sigma_com,Ed reclassification. No NTC inference.</summary>
        public static SteelMemberAnalysis FromExternal(SteelVerificationInput input, string loadPatternReference,
            string parameterSet, Ec3GeneralMethodCurve lateral, Ec3GeneralMethodCurve lt,
            IEnumerable<SteelStabilityValue> values, int? memberClass = null, string classificationSource = null)
            => new SteelMemberAnalysis(input, SteelMemberAnalysisKind.ExternalInPlane, loadPatternReference, parameterSet,
                Ec3GeneralMethodCurve.A, lateral, lt, values, memberClass, classificationSource);
    }
}
