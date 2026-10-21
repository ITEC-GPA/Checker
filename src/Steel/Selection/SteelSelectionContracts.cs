using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Inputs;

namespace GPC.Checkers.Steel.Selection
{
    public enum SteelCheckScope { Section, Member, SectionAndMember }
    public enum SteelCheckTarget { NormalResistance, Shear1, Shear2, Torsion, MemberStability }
    public enum SteelSelectionStatus { Ready, RequiresPreparation, MissingInput, NotSupported, NotRequired, InvalidInput }
    public enum SteelMethod
    {
        SectionTension, SectionCompression, SectionBending1, SectionBending2,
        SectionShear1, SectionShear2, Ec3PlasticHInteraction, NtcElasticNormalInteraction, Ec3UniformForkMember, Ec3ExternalGeneralMember
    }
    public enum SteelPreparationRequirement { ShearBucklingAssessment }

    /// <summary>Externally established section class for this exact immutable input and
    /// code/action state. Source records provenance, not independent certification.
    /// May be supplied by the preparation pipeline or an explicit engineering assessment.
    /// It does not establish a member class or qualify a new geometry/method.</summary>
    public sealed class SteelSectionClassification
    {
        public SteelVerificationInput Input { get; }
        public int SectionClass { get; }
        public string Source { get; }
        public SteelSectionClassification(SteelVerificationInput input, int sectionClass, string source)
        {
            Input = input ?? throw new ArgumentNullException(nameof(input));
            if (sectionClass < 1 || sectionClass > 4) throw new ArgumentOutOfRangeException(nameof(sectionClass));
            if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("A classification source is required.", nameof(source));
            SectionClass = sectionClass; Source = source;
        }
    }

    /// <summary>Routing decision, never a resistance result. Ready means this method's
    /// inputs are available; RequiresPreparation means the named method remains blocked
    /// by explicit engineering gates. Neither status means the section passes a check.</summary>
    public sealed class SteelMethodDecision
    {
        public SteelCheckTarget Target { get; }
        public SteelSelectionStatus Status { get; }
        public SteelMethod? Method { get; }
        public int? SectionClass { get; }
        public string ClassificationSource { get; }
        public string DiagnosticCode { get; }
        public string Message { get; }
        public IReadOnlyList<SteelPreparationRequirement> RequiredPreparation { get; }
        public IReadOnlyList<string> MissingInputs { get; }

        internal SteelMethodDecision(SteelCheckTarget target, SteelSelectionStatus status,
            SteelMethod? method, string diagnostic, string message, int? sectionClass = null,
            string classificationSource = null, IEnumerable<SteelPreparationRequirement> preparation = null,
            IEnumerable<string> missing = null)
        {
            Target = target; Status = status; Method = method; DiagnosticCode = diagnostic; Message = message;
            SectionClass = sectionClass; ClassificationSource = classificationSource;
            RequiredPreparation = Array.AsReadOnly((preparation ?? Enumerable.Empty<SteelPreparationRequirement>()).ToArray());
            MissingInputs = Array.AsReadOnly((missing ?? Enumerable.Empty<string>()).ToArray());
        }
    }

    /// <summary>Complete routing inventory for the REQUESTED scope and current cut.
    /// Section scope contains normal resistance, both shears and torsion, including
    /// unavailable/non-required items. Member scope cannot infer a diagram from a cut.
    /// This transient plan carries no capacity, utilization or engineering verdict.</summary>
    public sealed class SteelSelectionPlan
    {
        public SteelVerificationInput Input { get; }
        public SteelCheckScope Scope { get; }
        public SteelMemberAnalysis MemberAnalysis { get; }
        public IReadOnlyList<SteelMethodDecision> Decisions { get; }
        public bool HasSelectedMethods => Decisions.Any(d => d.Method.HasValue);
        public bool IsFullyRouted => Decisions.All(d => d.Status == SteelSelectionStatus.Ready
            || d.Status == SteelSelectionStatus.RequiresPreparation || d.Status == SteelSelectionStatus.NotRequired);
        public bool IsReadyForExecution => HasSelectedMethods && Decisions.All(d =>
            d.Status == SteelSelectionStatus.Ready || d.Status == SteelSelectionStatus.NotRequired);

        internal SteelSelectionPlan(SteelVerificationInput input, SteelCheckScope scope, IEnumerable<SteelMethodDecision> decisions, SteelMemberAnalysis memberAnalysis = null)
        { Input = input; Scope = scope; Decisions = Array.AsReadOnly(decisions.ToArray()); MemberAnalysis = memberAnalysis; }
    }
}
