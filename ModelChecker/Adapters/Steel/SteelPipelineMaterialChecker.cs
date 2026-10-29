using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Checkers.Steel.Serviceability;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Checking.Reports;
using GPC.Model.Core;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Elements;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;

namespace GPC.Model.Checker
{
    /// <summary>Complete selected SECTION inventory at one prepared concomitant cut.
    /// EC3/NTC share the pipeline but retain their separate qualification policies.
    /// Explicit opt-in engine; existing isolated-check engine IDs keep their contracts.
    /// Parameters describe assigned sections, never an implicit absence of holes or
    /// member buckling. No action projection, synthetic load or member model is used.
    /// The typed task Serviceability/StressLimits/Characteristic limits the elastic service stresses
    /// (SteelServiceStressCheck) with the explicit ServiceStress.* basis; legacy mechanism-only
    /// Serviceability tasks, without a declared category, are not supported.</summary>
    public sealed class SteelPipelineMaterialChecker : IMaterialChecker
    {
        public const string EngineId = "Steel.Pipeline.Section";
        internal const string Limitations = "Selected section checks at one concomitant cut, EC3:2005 or NTC:2018; unavailable checks remain incomplete. Class4 N-M1-M2 uses shared iterative effective flat plates, including centroid eccentricity; optional H web shear resistance requires separately assessed supports; unsupported geometry and interactions remain explicit. No member stability, connections, fatigue, fire or seismic checks.";
        private readonly SteelPipelineParameters _parameters;
        public StandardEN1993p11 Code { get; }
        public string Edition { get; }
        public string NationalAnnex { get; }
        public IReadOnlyDictionary<string, string> Parameters { get; }
        public string Id => EngineId;
        public string Version => NativeResults.Version(typeof(SteelVerificationPipeline));
        public string Configuration => ModelValues.Fingerprint(new object[] { Id, Code, Edition, NationalAnnex, _parameters.Values });
        public CheckStandardContext Standard => new CheckStandardContext(Code.Name, Edition, NationalAnnex, Id, Configuration, Limitations);

        public SteelPipelineMaterialChecker(StandardEN1993p11 code, string edition,
            string nationalAnnex = null, IDictionary<string, string> parameters = null)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            if (string.IsNullOrWhiteSpace(edition)) throw new ArgumentException("ExplicitSteelEditionRequired", nameof(edition));
            Edition = edition; NationalAnnex = nationalAnnex;
            _parameters = new SteelPipelineParameters(parameters);
            Parameters = new ReadOnlyDictionary<string, string>(_parameters.Values);
        }

        /// <summary>Scope of the serviceability stress task, added to the catalog metadata (results keep their own scope).</summary>
        internal const string ServiceLimitations = "Serviceability/StressLimits on Characteristic selections only: limits of EN 1993-2 7.3 form with explicit gammaM,ser, its source and the applicability source; doubly symmetric H, RHS, CHS and narrow solid rectangles, no torsion or shear lag.";
        public bool Accepts(BeamElement element) => element?.BeamProperty is ISteelSection;
        public IMaterialCheckSession CreateSession() => new Session(this);
        private bool Qualified => Code.GetType() == typeof(StandardEN1993p11) && Edition == "2005"
            || Code.GetType() == typeof(StandardNTC2018Steel) && Edition == "2018";
        internal bool Supports(CheckMechanism mechanism) => mechanism == CheckMechanism.UlsBiaxialSection && Qualified;
        /// <summary>EN 1993-2 7.3(1) limits the stresses of the characteristic combination: frequent and quasi-permanent
        /// selections have no stress limit of this form and are not supported (no other category is justified).</summary>
        internal bool Supports(SectionCheckSpecification check) => check != null && Qualified
            && check.Mechanism == CheckMechanism.Serviceability && check.Criterion == SectionCheckCriterion.StressLimits
            && check.Direction == SectionCheckDirection.None && check.Category == CombinationCategory.Characteristic;

        private SteelDesignContext Context()
        {
            if (Code.GetType() == typeof(StandardNTC2018Steel))
            {
                var context = SteelDesignContext.Ntc2018();
                if (Code.GammaM0 != context.SectionCode.GammaM0 || Code.GammaM2 != context.SectionCode.GammaM2)
                    throw new NotSupportedException("NtcSectionFactorsDoNotMatchQualifiedPolicy");
                return context.WithNegligibleActions(_parameters.NegligibleActions);
            }
            return new SteelDesignContext(SectionResistanceCode.Eurocode3(Code.GammaM0, Code.GammaM2,
                string.IsNullOrWhiteSpace(NationalAnnex) ? "Explicit StandardEN1993p11 parameters" : NationalAnnex),
                null, _parameters.NegligibleActions);
        }

        private sealed class Session : IConcurrentMaterialSession, ISectionCheckSession
        {
            private readonly SteelPipelineMaterialChecker _owner;
            // One entry per distinct property and engine configuration prepared by this session.
            private readonly HashSet<string> _prepared = new HashSet<string>(StringComparer.Ordinal);
            public int CreatedCheckers => _prepared.Count;
            internal Session(SteelPipelineMaterialChecker owner) { _owner = owner; }

            public CheckResult Verify(BeamActionInput input, CheckMechanism mechanism, CancellationToken token)
            {
                string prepared = null;
                try { return Evaluate(input, mechanism, token, ref prepared); }
                finally { Record(prepared); }
            }

            public bool Supports(SectionCheckSpecification check) => _owner.Supports(check);

            /// <summary>Serviceability stress limits of the cut (the only typed task of this engine). The plan
            /// routes it to selections declared Characteristic; the actions are used as they are, without factors.</summary>
            public CheckResult Verify(BeamActionInput input, SectionCheckSpecification check, CancellationToken token)
            {
                string prepared = null;
                try { return EvaluateServiceStress(input, check, token, ref prepared); }
                finally { Record(prepared); }
            }

            public void Record(string prepared) { if (prepared != null) _prepared.Add(prepared); }

            /// <summary>Thread-safe: reads only the owned copies of the task, the immutable engine
            /// parameters and the shared section snapshots; the session itself is not touched.</summary>
            public CheckResult Evaluate(BeamActionInput input, CheckMechanism mechanism, CancellationToken token, ref string prepared)
            {
                token.ThrowIfCancellationRequested();
                if (input == null) return NativeResults.Missing("SteelPipeline.Input.Required");
                if (!_owner.Supports(mechanism)) return NativeResults.Unavailable("SteelPipeline.ContextNotQualified");
                if (!(input.Property is ISteelSection section)) return NativeResults.Unavailable("SteelPipeline.SteelSectionRequired");
                try
                {
                    var options = _owner._parameters;
                    var context = _owner.Context();
                    string propertyKey = ModelValues.Fingerprint(new object[] { input.Property });
                    if (options.SectionFingerprint != null && options.SectionFingerprint != propertyKey)
                        return NativeResults.Missing("SteelPipeline.AssessmentSectionMismatch");
                    var snapshot = SteelPreparedSections.Get(section, propertyKey, options.Unperforated, options.NetArea);
                    // Recorded once the snapshot exists, even if the calculation then fails (as before).
                    prepared = propertyKey + "\n" + _owner.Configuration;
                    var common = ModelSteelInput.FromSectionAxes(snapshot, input.Forces, context);
                    var classification = options.UniformCompressionClass.HasValue
                        ? new SteelSectionClassification(common, options.UniformCompressionClass.Value,
                            "External nominal uniform-compression classification: " + options.ClassificationSource) : null;
                    var plan = SteelMethodSelector.Select(common, classification: classification);
                    var assessments = Assessments(plan, options);
                    var result = SteelVerificationPipeline.Execute(plan, assessments, token);
                    string inputKey = ModelValues.Fingerprint(new object[] { input.Property, input.Forces, _owner.Configuration });
                    return SteelPipelineResults.Map(result, inputKey);
                }
                catch (NotSupportedException ex)
                { return NativeResults.Unavailable("SteelPipeline.PreparationNotSupported", ex.Message); }
                catch (ArgumentException ex)
                {
                    var result = NativeResults.Missing("SteelPipeline.InvalidInput");
                    result.Diagnostics.Add(ModelDiagnostic.Error("SteelPipeline.InvalidInput.Detail", message: ex.Message));
                    return result;
                }
                catch (ArithmeticException ex)
                {
                    return new CheckResult { Data = DataStatus.Ready, Execution = ExecutionStatus.Error,
                        Diagnostics = new List<ModelDiagnostic> { ModelDiagnostic.Error("SteelPipeline.PreparationNumericalRange", message: ex.Message) } };
                }
            }

            private CheckResult EvaluateServiceStress(BeamActionInput input, SectionCheckSpecification check, CancellationToken token, ref string prepared)
            {
                token.ThrowIfCancellationRequested();
                if (input == null) return NativeResults.Missing("SteelPipeline.Input.Required");
                if (!_owner.Supports(check)) return NativeResults.Unavailable("UnsupportedSectionCheck", check?.Key);
                if (!(input.Property is ISteelSection section)) return NativeResults.Unavailable("SteelPipeline.SteelSectionRequired");
                try
                {
                    var options = _owner._parameters;
                    var context = _owner.Context();
                    string propertyKey = ModelValues.Fingerprint(new object[] { input.Property });
                    if (options.SectionFingerprint != null && options.SectionFingerprint != propertyKey)
                        return NativeResults.Missing("SteelPipeline.AssessmentSectionMismatch");
                    var snapshot = SteelPreparedSections.Get(section, propertyKey, options.Unperforated, options.NetArea);
                    prepared = propertyKey + "\n" + _owner.Configuration;
                    var common = ModelSteelInput.FromSectionAxes(snapshot, input.Forces, context);
                    var classification = options.UniformCompressionClass.HasValue
                        ? new SteelSectionClassification(common, options.UniformCompressionClass.Value,
                            "External nominal uniform-compression classification: " + options.ClassificationSource) : null;
                    var result = SteelServiceStressCheck.Evaluate(common, options.ServiceStress, classification, token);
                    return SteelServiceStressResults.Map(result, check.Category);
                }
                catch (NotSupportedException ex)
                { return NativeResults.Unavailable("SteelPipeline.PreparationNotSupported", ex.Message); }
                catch (ArgumentException ex)
                {
                    var result = NativeResults.Missing("SteelPipeline.InvalidInput");
                    result.Diagnostics.Add(ModelDiagnostic.Error("SteelPipeline.InvalidInput.Detail", message: ex.Message));
                    return result;
                }
                catch (ArithmeticException ex)
                {
                    return new CheckResult { Data = DataStatus.Ready, Execution = ExecutionStatus.Error,
                        Diagnostics = new List<ModelDiagnostic> { ModelDiagnostic.Error("SteelPipeline.PreparationNumericalRange", message: ex.Message) } };
                }
            }

            private IEnumerable<SteelShearBucklingAssessment> Assessments(SteelSelectionPlan plan, SteelPipelineParameters options)
            {
                if (!plan.Decisions.Any(d => d.Status == SteelSelectionStatus.RequiresPreparation)) yield break;
                var input = plan.Input;
                for (int axis = 1; axis <= 2; axis++)
                {
                    if ((axis == 1 ? input.EffectiveActions.V1 : input.EffectiveActions.V2) == 0) continue;
                    if (axis == 2 && options.HWebResistance != null)
                    {
                        double eta = input.Code.SectionCode.Standard == SectionResistanceStandard.Ntc2018 ? 1
                            : input.Section.YieldStrength <= 460 ? _owner.Code.NShearBucklingLowGradeOfSteel : _owner.Code.NShearBucklingHighGradeOfSteel;
                        yield return SteelShearBucklingAssessment.EvaluateNonRigidHWeb(input, _owner.Code.GammaM1, eta, options.HWebSupportSource);
                        continue;
                    }
                    bool? excluded = axis == 1 ? options.ShearExcluded1 : options.ShearExcluded2;
                    if (excluded.HasValue)
                    {
                        yield return SteelShearBucklingAssessment.FromExternal(input, axis, excluded.Value,
                            axis == 1 ? options.ShearSource1 : options.ShearSource2);
                        continue;
                    }
                    bool exactH = input.Section.GeometryType == typeof(SectionH) && Math.Abs(input.Section.PrincipalAxisAngle) <= 1e-10;
                    if (axis == 1 && exactH)
                    {
                        // A GPC screen, not a code clause: flange outstands within the class 3 limit 14 eps.
                        // Beyond it the assessment is negative and the row stays not evaluated.
                        yield return SteelShearBucklingAssessment.EvaluateHFlanges(input,
                            "GPC automatic flange outstand screen c/t<=14eps for V1 (not a code clause)");
                        continue;
                    }
                    if (axis == 2 && exactH)
                    {
                        if (input.Code.SectionCode.Standard == SectionResistanceStandard.Ntc2018)
                        {
                            yield return SteelShearBucklingAssessment.EvaluateNtcHWeb(input, "NTC 2018 equation4.2.27 conservative eta=1");
                            continue;
                        }
                        bool lowGrade = input.Section.YieldStrength <= 460;
                        double eta = lowGrade ? _owner.Code.NShearBucklingLowGradeOfSteel : _owner.Code.NShearBucklingHighGradeOfSteel;
                        yield return SteelShearBucklingAssessment.EvaluateEc3HWeb(input, eta,
                            "StandardEN1993p11." + (lowGrade ? "NShearBucklingLowGradeOfSteel" : "NShearBucklingHighGradeOfSteel"));
                    }
                }
            }
        }
    }
}
