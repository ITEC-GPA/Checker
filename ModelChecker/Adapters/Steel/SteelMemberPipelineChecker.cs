using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Members;
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
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;

namespace GPC.Model.Checker
{
    /// <summary>Explicit physical-member adapter. Internal route requires one forward
    /// FEM element, full deformable span, uniform section and constant exported actions,
    /// PLUS a source-bound declaration of the qualified fork/first-order load model.
    /// External route uses the EC3 general-method factors; Ncr/Mcr alone never suffice.</summary>
    public sealed class SteelMemberPipelineChecker : IMaterialChecker, IPhysicalMemberVerifier
    {
        public const string EngineId = "Steel.Pipeline.Member";
        internal const string Limitations = "EC3:2005/NTC2018 prismatic compression and major-axis LTB with explicit critical values/mode assessments. EC3 also has uniform H/T fork and external in-plane general methods. No general biaxial member method, torsion, plastic hinges or connections. Section/shear checks are separate.";
        private readonly Dictionary<string, string> _parameters;
        public IReadOnlyDictionary<string, string> Parameters { get; }
        public StandardEN1993p11 Code { get; }
        public string Edition { get; }
        public string NationalAnnex { get; }
        public string Id => EngineId;
        public string Version => NativeResults.Version(typeof(SteelMemberAnalysis));
        public string Configuration => ModelValues.Fingerprint(new object[] { Id, Code, Edition, NationalAnnex, _parameters });
        public CheckStandardContext Standard => new CheckStandardContext(Code.Name, Edition, NationalAnnex, Id, Configuration, Limitations);

        public SteelMemberPipelineChecker(StandardEN1993p11 code, string edition, string nationalAnnex = null,
            IDictionary<string, string> parameters = null)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            if (string.IsNullOrWhiteSpace(edition)) throw new ArgumentException("ExplicitSteelEditionRequired");
            Edition = edition; NationalAnnex = nationalAnnex;
            _parameters = parameters == null ? new Dictionary<string, string>(StringComparer.Ordinal) : new Dictionary<string, string>(parameters, StringComparer.Ordinal);
            var keys = new HashSet<string>(new[] { "AnalysisMode", "LoadPatternReference", "ParameterSet", "MemberFingerprint", "Unperforated",
                "InPlaneCurve", "LateralCurve", "LateralTorsionalCurve", "MemberClass", "ClassificationSource",
                "CompressionCurve1", "CompressionCurve2", "TorsionalCurve", "TorsionalExclusionSource", "FlexuralTorsionalExclusionSource", "McrBasis" }, StringComparer.Ordinal);
            foreach (SteelStabilityQuantity quantity in Enum.GetValues(typeof(SteelStabilityQuantity)))
                foreach (var suffix in new[] { ".Value", ".Unit", ".Source" }) keys.Add(quantity + suffix);
            foreach (var pair in _parameters)
                if (!keys.Contains(pair.Key) || string.IsNullOrWhiteSpace(pair.Value)) throw new ArgumentException("UnknownOrEmptySteelMemberParameter: " + pair.Key);
            Parameters = new ReadOnlyDictionary<string, string>(_parameters);
        }
        public bool Accepts(BeamElement element) => element?.BeamProperty is ISteelSection;
        public bool Supports(string methodId, CheckMechanism mechanism) => methodId == Id && mechanism == CheckMechanism.Stability
            && (Code.GetType() == typeof(StandardEN1993p11) && Edition == "2005"
                || Code.GetType() == typeof(StandardNTC2018Steel) && Edition == "2018"
                    && _parameters.TryGetValue("AnalysisMode", out var mode) && (mode == "Compression" || mode == "Bending"));
        public IMaterialCheckSession CreateSession() => new NoSectionSession();
        private sealed class NoSectionSession : IMaterialCheckSession
        {
            public int CreatedCheckers => 0;
            public CheckResult Verify(BeamActionInput input, CheckMechanism mechanism, CancellationToken token)
            { token.ThrowIfCancellationRequested(); return NativeResults.Unavailable("SteelMember.PhysicalMemberRequired"); }
        }
        private string Required(string key) => _parameters.TryGetValue(key, out var value) ? value : throw new ArgumentException("MissingSteelMemberParameter: " + key);
        private T EnumValue<T>(string key) where T : struct
        {
            string raw = Required(key);
            if (!Enum.TryParse(raw, false, out T value) || !Enum.IsDefined(typeof(T), value) || value.ToString() != raw)
                throw new ArgumentException("InvalidSteelMemberParameter: " + key);
            return value;
        }
        private double Number(string key)
        {
            if (!double.TryParse(Required(key), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                throw new ArgumentException("InvalidSteelMemberNumber: " + key);
            return value;
        }

        public CheckResult Verify(PhysicalMemberCheckInput input, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (input == null || input.Snapshot == null || input.ActionSections.Count == 0)
                return NativeResults.Missing("SteelMember.PreparedActionsRequired");
            if (!Supports(input.MethodId, input.Mechanism)) return NativeResults.Unavailable("SteelMember.MethodOrNormNotQualified");
            try
            {
                string fingerprint = ModelValues.Fingerprint(new object[] { input.Snapshot });
                if (Required("MemberFingerprint") != fingerprint) return NativeResults.Missing("SteelMember.AnalysisSnapshotMismatch");
                if (!bool.TryParse(Required("Unperforated"), out bool unperforated)) throw new ArgumentException("InvalidUnperforatedDeclaration");
                string mode = Required("AnalysisMode");
                var first = mode == "Compression" ? input.ActionSections.OrderByDescending(c => Math.Abs(c.Forces.N)).First()
                    : mode == "Bending" ? input.ActionSections.OrderByDescending(c => Math.Abs(c.Forces.M1)).First()
                    : input.ActionSections.FirstOrDefault(c => c.Forces.N != 0 || c.Forces.M1 != 0 || c.Forces.M2 != 0) ?? input.ActionSections[0];
                var snapshot = input.Snapshot;
                if (!(first.Property is ISteelSection section)) return NativeResults.Unavailable("SteelMember.SteelSectionRequired");
                var context = Code.GetType() == typeof(StandardNTC2018Steel) ? SteelDesignContext.Ntc2018()
                    : new SteelDesignContext(SectionResistanceCode.Eurocode3(Code.GammaM0, Code.GammaM2, Required("ParameterSet")), Code.GammaM1);
                if (Code.GetType() == typeof(StandardNTC2018Steel) && (Code.GammaM0 != context.SectionCode.GammaM0
                    || Code.GammaM1 != context.GammaM1 || Code.GammaM2 != context.SectionCode.GammaM2))
                    return NativeResults.Unavailable("SteelMember.NtcFactorsNotQualified");
                var member = new SteelMemberInput(snapshot.SpanEnd - snapshot.SpanStart,
                    snapshot.Context.EffectiveLength1, snapshot.Context.EffectiveLength2);
                var common = ModelSteelInput.FromSectionAxes(new SteelSectionInput(section, unperforated), first.Forces, context, member);
                SteelMemberAnalysis analysis;
                if (mode == "Compression" || mode == "Bending")
                    analysis = Prismatic(input, common, context, mode);
                else
                {
                if (new[] { "CompressionCurve1", "CompressionCurve2", "TorsionalCurve", "TorsionalExclusionSource", "FlexuralTorsionalExclusionSource", "McrBasis" }.Any(_parameters.ContainsKey))
                    throw new ArgumentException("PrismaticParametersOnGeneralMethodRoute");
                var lateral = EnumValue<Ec3GeneralMethodCurve>("LateralCurve"); var lt = EnumValue<Ec3GeneralMethodCurve>("LateralTorsionalCurve");
                if (mode == "UniformFork")
                {
                    if (_parameters.ContainsKey("MemberClass") || _parameters.ContainsKey("ClassificationSource") || _parameters.Keys.Any(k => k.EndsWith(".Value") || k.EndsWith(".Unit") || k.EndsWith(".Source")))
                        throw new ArgumentException("ExternalInputsOnInternalMemberRoute");
                    if (snapshot.Definition.Parts.Count != 1 || !snapshot.Definition.Parts[0].AlongIJ || snapshot.SpanStart != 0
                        || snapshot.SpanEnd != snapshot.Length || snapshot.Length != snapshot.DeformableLength
                        || snapshot.Context.Restraints.Count != 0 || snapshot.Context.LateralTorsionalLength.HasValue && snapshot.Context.LateralTorsionalLength != member.Length)
                        return NativeResults.Unavailable("SteelMember.InternalSchemeNotQualified", "Use a full single forward deformable element and the explicit fork declaration; other schemes require external analysis.");
                    string sectionKey = ModelValues.Fingerprint(new object[] { first.Property });
                    foreach (var cut in input.ActionSections)
                    {
                        if (ModelValues.Fingerprint(new object[] { cut.Property }) != sectionKey) return NativeResults.Unavailable("SteelMember.NonuniformSection");
                        var cutInput = ModelSteelInput.FromSectionAxes(common.Section, cut.Forces, context);
                        if (cutInput.Actions.N > 0 || cutInput.Actions.V1 != 0 || cutInput.Actions.V2 != 0 || cutInput.Actions.T != 0 || cutInput.Actions.M2 != 0)
                            return NativeResults.Unavailable("SteelMember.InternalPatternNotInPlane");
                        if (!Same(common.Actions, cutInput.Actions)) return NativeResults.Unavailable("SteelMember.NonconstantActions");
                    }
                    analysis = SteelMemberAnalysis.UniformFork(common, Required("LoadPatternReference"), Required("ParameterSet"),
                        EnumValue<Ec3GeneralMethodCurve>("InPlaneCurve"), lateral, lt);
                }
                else if (mode == "ExternalInPlane")
                {
                    if (_parameters.ContainsKey("InPlaneCurve")) throw new ArgumentException("InPlaneCurveIsOwnedByExternalAnalysis");
                    var values = new List<SteelStabilityValue>();
                    foreach (SteelStabilityQuantity quantity in Enum.GetValues(typeof(SteelStabilityQuantity)))
                        if (new[] { ".Value", ".Unit", ".Source" }.Any(s => _parameters.ContainsKey(quantity + s)))
                            values.Add(new SteelStabilityValue(quantity, Number(quantity + ".Value"), EnumValue<SteelStabilityUnit>(quantity + ".Unit"), Required(quantity + ".Source")));
                    int? memberClass = null;
                    if (_parameters.ContainsKey("MemberClass"))
                    { if (!int.TryParse(Required("MemberClass"), NumberStyles.None, CultureInfo.InvariantCulture, out int cls)) throw new ArgumentException("InvalidMemberClass"); memberClass = cls; }
                    // Every exported cut must belong to the in-plane action family.
                    foreach (var cut in input.ActionSections)
                    {
                        if (!(cut.Property is ISteelSection steel)) return NativeResults.Unavailable("SteelMember.NonSteelSection");
                        var a = ModelSteelInput.FromSectionAxes(new SteelSectionInput(steel, unperforated), cut.Forces, context).Actions;
                        if (a.N > 0 || a.V1 != 0 || a.V2 != 0 || a.T != 0 || a.M2 != 0) return NativeResults.Unavailable("SteelMember.ExternalPatternNotInPlane");
                    }
                    analysis = SteelMemberAnalysis.FromExternal(common, Required("LoadPatternReference"), Required("ParameterSet"), lateral, lt, values,
                        memberClass, _parameters.TryGetValue("ClassificationSource", out var source) ? source : null);
                }
                else throw new ArgumentException("UnknownSteelMemberAnalysisMode");
                }
                var result = SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(analysis), cancellationToken: token);
                return SteelPipelineResults.Map(result, fingerprint, Id, Limitations);
            }
            catch (ArgumentException ex)
            {
                var result = NativeResults.Missing("SteelMember.InvalidOrMissingInput");
                result.Diagnostics.Add(ModelDiagnostic.Error("SteelMember.InputDetail", message: ex.Message)); return result;
            }
            catch (NotSupportedException ex) { return NativeResults.Unavailable("SteelMember.NotSupported", ex.Message); }
        }
        private SteelMemberAnalysis Prismatic(PhysicalMemberCheckInput input, SteelVerificationInput common, SteelDesignContext context, string mode)
        {
            bool compression = mode == "Compression";
            if (compression && (Code.AlphaImperfectionFactorForCurveA0 != .13 || Code.AlphaImperfectionFactorForCurveA != .21
                || Code.AlphaImperfectionFactorForCurveB != .34 || Code.AlphaImperfectionFactorForCurveC != .49 || Code.AlphaImperfectionFactorForCurveD != .76))
                throw new NotSupportedException("Prismatic compression requires the qualified standard imperfection factors.");
            if (!compression && (Code.AlphaLTImperfectionFactorForCurveA != .21 || Code.AlphaLTImperfectionFactorForCurveB != .34
                || Code.AlphaLTImperfectionFactorForCurveC != .49 || Code.AlphaLTImperfectionFactorForCurveD != .76
                || Code.BetaForLateralTorsionalBuckling != 1 || Code.LambdaLT0ForLateralTorsionalBuckling != .2))
                throw new NotSupportedException("This LTB route requires the qualified general curve parameters; a modified national curve needs its own method.");
            if (new[] { "InPlaneCurve", "LateralCurve", "LateralTorsionalCurve" }.Any(_parameters.ContainsKey))
                throw new ArgumentException("GeneralMethodCurvesOnPrismaticRoute");
            if (compression ? _parameters.ContainsKey("McrBasis") : new[] { "CompressionCurve1", "CompressionCurve2", "TorsionalCurve", "TorsionalExclusionSource", "FlexuralTorsionalExclusionSource" }.Any(_parameters.ContainsKey))
                throw new ArgumentException("ParametersForAnotherPrismaticMechanism");
            // The Model adapter does not yet reconcile principal frames across multiple
            // FEM elements; the native API remains available for independently prepared data.
            var snapshot = input.Snapshot;
            if (snapshot.Definition.Parts.Count != 1 || !snapshot.Definition.Parts[0].AlongIJ
                || snapshot.SpanStart != 0 || snapshot.SpanEnd != snapshot.Length || snapshot.Length != snapshot.DeformableLength)
                throw new NotSupportedException("Prismatic adapter requires one complete forward deformable FEM member.");
            string sectionKey = ModelValues.Fingerprint(new object[] { input.ActionSections[0].Property });
            foreach (var cut in input.ActionSections)
            {
                if (ModelValues.Fingerprint(new object[] { cut.Property }) != sectionKey) throw new NotSupportedException("Nonuniform prismatic section.");
                var a = ModelSteelInput.FromSectionAxes(common.Section, cut.Forces, context).Actions;
                if (a.T != 0 || a.M2 != 0 || a.V1 != 0 || compression && (a.M1 != 0 || a.V2 != 0 || a.N > 0)
                    || !compression && a.N != 0) throw new NotSupportedException("Actions outside selected prismatic mechanism.");
                if (compression && Math.Abs(a.N - common.Actions.N) > 1e-10 * Math.Max(1, Math.Abs(common.Actions.N)))
                    throw new NotSupportedException("Compression must be constant over the full member.");
                if (!compression && Math.Abs(a.M1) > Math.Abs(common.Actions.M1))
                    common = new SteelVerificationInput(common.Section, a, common.Code, common.Member);
            }
            int? memberClass = null;
            if (_parameters.ContainsKey("MemberClass"))
            { if (!int.TryParse(Required("MemberClass"), NumberStyles.None, CultureInfo.InvariantCulture, out int cls)) throw new ArgumentException("InvalidMemberClass"); memberClass = cls; }
            string source = _parameters.TryGetValue("ClassificationSource", out var supplied) ? supplied : null;
            var values = new List<SteelStabilityValue>();
            foreach (SteelStabilityQuantity q in Enum.GetValues(typeof(SteelStabilityQuantity)))
                if (new[] { ".Value", ".Unit", ".Source" }.Any(s => _parameters.ContainsKey(q + s)))
                    values.Add(new SteelStabilityValue(q, Number(q + ".Value"), EnumValue<SteelStabilityUnit>(q + ".Unit"), Required(q + ".Source")));
            if (compression)
                return SteelMemberAnalysis.Compression(common, Required("LoadPatternReference"), Required("ParameterSet"),
                    EnumValue<SteelBucklingCurve>("CompressionCurve1"), EnumValue<SteelBucklingCurve>("CompressionCurve2"), EnumValue<SteelBucklingCurve>("TorsionalCurve"), values,
                    _parameters.TryGetValue("TorsionalExclusionSource", out var excludeT) ? excludeT : null,
                    _parameters.TryGetValue("FlexuralTorsionalExclusionSource", out var excludeFT) ? excludeFT : null, memberClass, source);
            if (values.Any(v => v.Quantity != SteelStabilityQuantity.Mcr)) throw new ArgumentException("OnlyMcrOnBendingRoute");
            return SteelMemberAnalysis.Bending(common, Required("LoadPatternReference"), Required("ParameterSet"),
                EnumValue<SteelCriticalMomentBasis>("McrBasis"), values.SingleOrDefault(), memberClass, source);
        }

        private static bool Same(SteelActions a, SteelActions b)
        {
            var first = new[] { a.N, a.V1, a.V2, a.T, a.M1, a.M2 }; var second = new[] { b.N, b.V1, b.V2, b.T, b.M1, b.M2 };
            return first.Zip(second, (x, y) => Math.Abs(x - y) <= 1e-10 * Math.Max(1, Math.Max(Math.Abs(x), Math.Abs(y)))).All(v => v);
        }
    }
}
