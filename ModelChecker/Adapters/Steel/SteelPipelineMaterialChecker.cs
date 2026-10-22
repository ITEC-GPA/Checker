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
    /// member buckling. No action projection, synthetic load or member model is used.</summary>
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

        public bool Accepts(BeamElement element) => element?.BeamProperty is ISteelSection;
        public IMaterialCheckSession CreateSession() => new Session(this);
        internal bool Supports(CheckMechanism mechanism) => mechanism == CheckMechanism.UlsBiaxialSection
            && (Code.GetType() == typeof(StandardEN1993p11) && Edition == "2005"
                || Code.GetType() == typeof(StandardNTC2018Steel) && Edition == "2018");

        private SteelDesignContext Context()
        {
            if (Code.GetType() == typeof(StandardNTC2018Steel))
            {
                var context = SteelDesignContext.Ntc2018();
                if (Code.GammaM0 != context.SectionCode.GammaM0 || Code.GammaM2 != context.SectionCode.GammaM2)
                    throw new NotSupportedException("NtcSectionFactorsDoNotMatchQualifiedPolicy");
                return context;
            }
            return new SteelDesignContext(SectionResistanceCode.Eurocode3(Code.GammaM0, Code.GammaM2,
                string.IsNullOrWhiteSpace(NationalAnnex) ? "Explicit StandardEN1993p11 parameters" : NationalAnnex));
        }

        private sealed class Session : IMaterialCheckSession
        {
            private readonly SteelPipelineMaterialChecker _owner;
            private readonly Dictionary<string, SteelSectionInput> _sections = new Dictionary<string, SteelSectionInput>();
            public int CreatedCheckers => _sections.Count;
            internal Session(SteelPipelineMaterialChecker owner) { _owner = owner; }

            public CheckResult Verify(BeamActionInput input, CheckMechanism mechanism, CancellationToken token)
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
                    string key = ModelValues.Fingerprint(new object[] { input.Property, _owner.Configuration });
                    if (!_sections.TryGetValue(key, out var snapshot))
                    {
                        snapshot = new SteelSectionInput(section, options.Unperforated, options.NetArea);
                        _sections.Add(key, snapshot);
                    }
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

            private IEnumerable<SteelShearBucklingAssessment> Assessments(SteelSelectionPlan plan, SteelPipelineParameters options)
            {
                if (!plan.Decisions.Any(d => d.Status == SteelSelectionStatus.RequiresPreparation)) yield break;
                var input = plan.Input;
                for (int axis = 1; axis <= 2; axis++)
                {
                    if ((axis == 1 ? input.Actions.V1 : input.Actions.V2) == 0) continue;
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
                    if (axis == 2 && input.Section.GeometryType == typeof(SectionH) && Math.Abs(input.Section.PrincipalAxisAngle) <= 1e-10)
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
