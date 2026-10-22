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
using GPC.Model.Checker.Configuration;
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
    /// <summary>Physical-member adapter with a common frame over complete or explicit
    /// partial straight prismatic spans, including reversed FEM connectivity.
    /// The historical UniformFork route retains its single-element qualification.
    /// Critical values, restraints and concomitant states remain source-bound.</summary>
    public sealed partial class SteelMemberPipelineChecker : IMaterialChecker, IPhysicalMemberVerifier
    {
        public const string EngineId = "Steel.Pipeline.Member";
        internal const string Limitations = "EC3:2005/NTC2018 prismatic compression and major-axis LTB with explicit critical values/mode assessments. EC3 also has uniform H/T fork. External in-plane EN6.3.4 supports EC3/NTC with mono/double symmetry and explicit analysis; class4 requires effective-analysis provenance. Doubly symmetric H and hollow beam-columns use EC3 Annex B / NTC Circular Method B with explicit model and equivalent-moment factors; hollow sections use conservative elastic properties. Class4 H/RHS double symmetry uses Aeff/Weff at fy and gross elastic critical values; asymmetric class4 needs a separate eccentric-member method. Packed identical 2C/2L compression requires explicit detailing, complete local envelopes and design resistances; connection design is external. No general arbitrary-section biaxial method, torsion or plastic hinges. Section/shear checks are separate.";
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
            var keys = new HashSet<string>(SteelParameterSchemas.Member.Parameters.Select(p => p.Key), StringComparer.Ordinal);
            foreach (var pair in _parameters)
                if (!keys.Contains(pair.Key) || string.IsNullOrWhiteSpace(pair.Value)) throw new ArgumentException("UnknownOrEmptySteelMemberParameter: " + pair.Key);
            Parameters = new ReadOnlyDictionary<string, string>(_parameters);
        }
        public bool Accepts(BeamElement element) => element?.BeamProperty is ISteelSection;
        public bool Supports(string methodId, CheckMechanism mechanism) => methodId == Id && mechanism == CheckMechanism.Stability
            && (Code.GetType() == typeof(StandardEN1993p11) && Edition == "2005"
                || Code.GetType() == typeof(StandardNTC2018Steel) && Edition == "2018"
                    && _parameters.TryGetValue("AnalysisMode", out var mode) && (mode == "Compression" || mode == "Bending" || mode == "BeamColumn" || mode == "ExternalInPlane"));
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
                ValidatePreparationOptions(mode);
                if(_parameters.ContainsKey("EffectiveSectionAnalysisSource") && (mode!="ExternalInPlane"||Required("MemberClass")!="4"))
                    throw new ArgumentException("EffectiveExternalAnalysisRequiresClass4ExternalMode");
                if (mode != "BeamColumn" && BeamColumnKeys.Any(_parameters.ContainsKey)) throw new ArgumentException("BeamColumnParametersOnAnotherRoute");
                if(mode!="Compression" && PackedPairKeys.Any(_parameters.ContainsKey))throw new ArgumentException("PackedPairRequiresCompression");
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
                SteelPhysicalMemberPreparation physical = null;
                if (mode == "Compression" || mode == "Bending" || mode == "BeamColumn")
                    {
                    physical = SteelPhysicalMemberPreparation.Create(input, common.Section, context);
                    common = new SteelVerificationInput(common.Section, physical.Cuts[0].Actions, common.Code, common.Member);
                    analysis = mode == "BeamColumn" ? BeamColumn(input, common, physical) : Prismatic(input, common, context, mode, physical);
                }
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
                        var sectionInput = new SteelSectionInput(steel, unperforated);
                        if(memberClass.HasValue)SteelGeneralMethodApplicability.QualifySection(sectionInput,memberClass.Value);
                        var a = ModelSteelInput.FromSectionAxes(sectionInput, cut.Forces, context).Actions;
                        if (a.N > 0 || a.V1 != 0 || a.V2 != 0 || a.T != 0 || a.M2 != 0) return NativeResults.Unavailable("SteelMember.ExternalPatternNotInPlane");
                    }
                    string source = _parameters.TryGetValue("ClassificationSource", out var supplied) ? supplied : null;
                    analysis = _parameters.TryGetValue("EffectiveSectionAnalysisSource",out var effectiveSource)
                        ? SteelMemberAnalysis.FromExternalEffective(common,Required("LoadPatternReference"),Required("ParameterSet"),lateral,lt,values,source,effectiveSource)
                        : SteelMemberAnalysis.FromExternal(common,Required("LoadPatternReference"),Required("ParameterSet"),lateral,lt,values,memberClass,source);
                }
                else throw new ArgumentException("UnknownSteelMemberAnalysisMode");
                }
                var result = SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(analysis), cancellationToken: token);
                var mapped = SteelPipelineResults.Map(result, fingerprint, Id, Limitations);
                physical?.AddTrace(mapped);
                return mapped;
            }
            catch (ArgumentException ex)
            {
                var result = NativeResults.Missing("SteelMember.InvalidOrMissingInput");
                result.Diagnostics.Add(ModelDiagnostic.Error("SteelMember.InputDetail", message: ex.Message)); return result;
            }
            catch (NotSupportedException ex) { return NativeResults.Unavailable("SteelMember.NotSupported", ex.Message); }
        }
        private SteelMemberAnalysis Prismatic(PhysicalMemberCheckInput input, SteelVerificationInput common, SteelDesignContext context, string mode, SteelPhysicalMemberPreparation physical)
        {
            bool compression = mode == "Compression";
            ValidatePrismaticCoefficients(compression, !compression);
            if (new[] { "InPlaneCurve", "LateralCurve", "LateralTorsionalCurve" }.Any(_parameters.ContainsKey))
                throw new ArgumentException("GeneralMethodCurvesOnPrismaticRoute");
            if (compression ? _parameters.ContainsKey("McrBasis") : new[] { "CompressionCurve1", "CompressionCurve2", "TorsionalCurve", "TorsionalExclusionSource", "FlexuralTorsionalExclusionSource" }.Any(_parameters.ContainsKey))
                throw new ArgumentException("ParametersForAnotherPrismaticMechanism");
            foreach (var cut in physical.Cuts)
            {
                var a = cut.Actions;
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
            values = PrepareCriticalValues(input,common,physical,compression,values).ToList();
            if(compression && PackedPairKeys.Any(_parameters.ContainsKey))
                return SteelMemberAnalysis.PackedCompression(Packing(common),Required("LoadPatternReference"),PreparedParameterSet(common),
                    CompressionCurve(common,"CompressionCurve1"),CompressionCurve(common,"CompressionCurve2"),CompressionCurve(common,"TorsionalCurve"),values,
                    _parameters.TryGetValue("TorsionalExclusionSource",out var pt)?pt:null,
                    _parameters.TryGetValue("FlexuralTorsionalExclusionSource",out var pft)?pft:null,memberClass,source);
            if (compression)
                return SteelMemberAnalysis.Compression(common, Required("LoadPatternReference"), PreparedParameterSet(common),
                    CompressionCurve(common,"CompressionCurve1"), CompressionCurve(common,"CompressionCurve2"), CompressionCurve(common,"TorsionalCurve"), values,
                    _parameters.TryGetValue("TorsionalExclusionSource", out var excludeT) ? excludeT : null,
                    _parameters.TryGetValue("FlexuralTorsionalExclusionSource", out var excludeFT) ? excludeFT : null, memberClass, source);
            if (values.Any(v => v.Quantity != SteelStabilityQuantity.Mcr)) throw new ArgumentException("OnlyMcrOnBendingRoute");
            return SteelMemberAnalysis.Bending(common, Required("LoadPatternReference"), PreparedParameterSet(common),
                EnumValue<SteelCriticalMomentBasis>("McrBasis"), values.SingleOrDefault(), memberClass, source);
        }

        private static readonly string[] PackedPairKeys = {"Packed.Pitch","Packed.Thickness","Packed.GussetThickness",
            "Packed.ChordCompression","Packed.ChordCompressionResistance","Packed.ChordTension","Packed.ChordTensionResistance",
            "Packed.ConnectionDemand","Packed.ConnectionResistance","Packed.DetailingSource","Packed.AnalysisSource","Packed.ResistanceSource"};
        private SteelPackedPairAssessment Packing(SteelVerificationInput input)
            => new SteelPackedPairAssessment(input,Number("Packed.Pitch"),Number("Packed.Thickness"),Number("Packed.GussetThickness"),
                Number("Packed.ChordCompression"),Number("Packed.ChordCompressionResistance"),Number("Packed.ChordTension"),Number("Packed.ChordTensionResistance"),
                Number("Packed.ConnectionDemand"),Number("Packed.ConnectionResistance"),Required("Packed.DetailingSource"),Required("Packed.AnalysisSource"),Required("Packed.ResistanceSource"));

        private static bool Same(SteelActions a, SteelActions b)
        {
            var first = new[] { a.N, a.V1, a.V2, a.T, a.M1, a.M2 }; var second = new[] { b.N, b.V1, b.V2, b.T, b.M1, b.M2 };
            return first.Zip(second, (x, y) => Math.Abs(x - y) <= 1e-10 * Math.Max(1, Math.Max(Math.Abs(x), Math.Abs(y)))).All(v => v);
        }
    }
}
