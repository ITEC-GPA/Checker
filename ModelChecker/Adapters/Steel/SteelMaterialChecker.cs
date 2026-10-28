using System;
using System.Collections.Generic;
using System.Threading;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Model.Elements;
using GPC.Model.Core;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Checking.Reports;

namespace GPC.Model.Checker
{
    /// <summary>Qualified native shear and optional uniaxial bending primitives on stocky symmetric H cuts, T=0.
    /// Global buckling and cross-section interactions remain gated by the unresolved legacy regressions.
    /// Superseded by SteelPipelineMaterialChecker for the same section check; kept unchanged, with its engine IDs,
    /// because existing configurations and reports name it and its results, trace and scope differ.</summary>
    [Obsolete("Superseded by SteelPipelineMaterialChecker (engine Steel.Pipeline.Section): the complete EC3:2005/NTC:2018 section "
        + "inventory at one cut, shears, bending and their interactions included. Kept for the configurations and reports that name "
        + "Steel.EN1993.PlasticShear or Steel.EN1993.Section; its results are not those of the pipeline.")]
    public sealed class SteelMaterialChecker : IMaterialChecker
    {
        public StandardEN1993p11 Code { get; }
        public string Edition { get; }
        public string NationalAnnex { get; }
        public bool IncludeUniaxialBending { get; }
        internal SteelSectionCapability Capability => IncludeUniaxialBending ? SteelSectionCapability.Section : SteelSectionCapability.Shear;
        public string Id => Capability.Id;
        public string Version => NativeResults.Version(typeof(SectionResistanceVerifier));
        public string Configuration => ModelValues.Fingerprint(new object[] { Id, Code, Edition, NationalAnnex });
        public CheckStandardContext Standard => new CheckStandardContext(Code.Name, Edition, NationalAnnex, Id, Configuration,
            Capability.Limitations);
        public SteelMaterialChecker(StandardEN1993p11 code, string edition, string nationalAnnex = null)
            : this(code, edition, nationalAnnex, false) { }
        public SteelMaterialChecker(StandardEN1993p11 code, string edition, string nationalAnnex, bool includeUniaxialBending)
        {
            IncludeUniaxialBending = includeUniaxialBending;
            Code = code ?? throw new ArgumentNullException(nameof(code));
            if (string.IsNullOrWhiteSpace(edition)) throw new ArgumentException("ExplicitSteelEditionRequired");
            Edition = edition; NationalAnnex = nationalAnnex;
        }
        public bool Accepts(BeamElement element) => element.BeamProperty is SteelSection;
        public IMaterialCheckSession CreateSession() => new Session(this);
        private sealed class PreparedSection
        {
            internal double Area1 { get; }
            internal double Area2 { get; }
            internal double WebHeight { get; }
            internal double WebThickness { get; }
            internal UniformCompressionClassification Classification { get; }
            internal PreparedSection(SteelSection section, SectionResistanceStandard standard)
            {
                Area1 = ModelShearAreas.GetArea1(section); Area2 = ModelShearAreas.GetArea2(section, standard);
                var h = (SectionH)section.SectionShape;
                WebHeight = h.HeightWeb; WebThickness = h.ThicknessWeb;
                Classification = Ec3UniformCompressionClassification.Evaluate(ModelCompressionGeometry.Create(section), section.SteelMaterial.Fyk);
            }
        }
        private sealed class Session : IMaterialCheckSession
        {
            private readonly SteelMaterialChecker _owner;
            private readonly Dictionary<string, PreparedSection> _prepared = new Dictionary<string, PreparedSection>();
            public int CreatedCheckers => _prepared.Count;
            internal Session(SteelMaterialChecker owner) { _owner = owner; }
            public CheckResult Verify(BeamActionInput input, CheckMechanism mechanism, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                if (input == null) return NativeResults.Missing("SteelSection.Input.Required");
                var capability = _owner.Capability;
                var failure = capability.ContextFailure(_owner.Code, _owner.Edition, mechanism);
                if (failure != null) return failure;
                bool bending = mechanism == CheckMechanism.UlsBiaxialSection;
                var section = input.Property as SteelSection;
                failure = SteelSectionCapability.GeometryFailure(section);
                if (failure != null) return failure;
                if (!(_owner.Code.GammaM0 > 0) || double.IsInfinity(_owner.Code.GammaM0)) return NativeResults.Missing("InvalidGammaM0");
                // The scalar context validates both declared factors, including the currently unused GammaM2.
                if (!(_owner.Code.GammaM2 > 0) || double.IsInfinity(_owner.Code.GammaM2)) return NativeResults.Missing("InvalidGammaM2");
                var code = SectionResistanceCode.Eurocode3(_owner.Code.GammaM0, _owner.Code.GammaM2,
                    string.IsNullOrWhiteSpace(_owner.NationalAnnex) ? "Explicit StandardEN1993p11 parameters" : _owner.NationalAnnex);
                failure = SteelSectionCapability.TorsionFailure(input.Forces);
                if (failure != null) return failure;
                // Uniform-compression classification is a conservative stockiness gate for both flange directions.
                string key = ModelValues.Fingerprint(new object[] { input.Property, _owner.Configuration });
                if (!_prepared.TryGetValue(key, out var prepared))
                {
                    prepared = new PreparedSection(section, code.Standard);
                    _prepared.Add(key, prepared);
                }
                var shear1 = SectionResistanceVerifier.Shear(code, input.Forces.V1, prepared.Area1, section.SteelMaterial.Fyk);
                var shear2 = SectionResistanceVerifier.Shear(code, input.Forces.V2, prepared.Area2, section.SteelMaterial.Fyk);
                failure = SteelSectionResults.Failure("V1", shear1) ?? SteelSectionResults.Failure("V2", shear2);
                if (failure != null) return failure;
                var classification = prepared.Classification;
                failure = SteelSectionResults.ClassificationFailure(classification);
                if (failure != null) return failure;
                int sectionClass = classification.NominalClass.Value;
                failure = SteelSectionCapability.ClassificationFailure(sectionClass);
                if (failure != null) return failure;
                double eta = section.SteelMaterial.Fyk <= 460 ? _owner.Code.NShearBucklingLowGradeOfSteel : _owner.Code.NShearBucklingHighGradeOfSteel;
                if (!(eta > 0) || double.IsInfinity(eta)) return NativeResults.Missing("InvalidShearBucklingEta");
                if (Ec3WebShearBuckling.RequiresCheck(prepared.WebHeight, prepared.WebThickness, section.SteelMaterial.Fyk, eta))
                    return NativeResults.Unavailable("SteelShearBucklingNotQualified");
                if (bending)
                {
                    failure = SteelSectionCapability.BendingActionsFailure(input.Forces, shear1.Resistance.Value, shear2.Resistance.Value);
                    if (failure != null) return failure;
                    var m1 = SectionResistanceVerifier.Bending(code, input.Forces.M1, section.Wpl1, null, section.SteelMaterial.Fyk, (int)sectionClass);
                    var m2 = SectionResistanceVerifier.Bending(code, input.Forces.M2, section.Wpl2, null, section.SteelMaterial.Fyk, (int)sectionClass);
                    failure = SteelSectionResults.Failure("M1", m1) ?? SteelSectionResults.Failure("M2", m2);
                    if (failure != null) return failure;
                    var trace = new List<CheckCalculationValue> {
                        new CheckCalculationValue("GammaM0", _owner.Code.GammaM0, "1", "Explicit code parameter"),
                        new CheckCalculationValue("Web.ClearHeight", prepared.WebHeight, "mm", "Clear height between flanges"),
                        new CheckCalculationValue("Web.Thickness", prepared.WebThickness, "mm", "Web thickness"),
                        new CheckCalculationValue("Web.Eta", eta, "1", "EN1993-1-1:2005 6.2.6(6)") ,
                        new CheckCalculationValue("ShearRatio1", shear1.Utilization, "1", "Low shear <= 0.5"),
                        new CheckCalculationValue("ShearRatio2", shear2.Utilization, "1", "Low shear <= 0.5")
                    };
                    SteelSectionResults.AddClassificationTrace(trace, classification);
                    SteelSectionResults.AddTrace(trace, "M1", m1); SteelSectionResults.AddTrace(trace, "M2", m2);
                    SteelSectionResults.AddTrace(trace, "V1", shear1); SteelSectionResults.AddTrace(trace, "V2", shear2);
                    return NativeResults.Decision(new NativeMethodDetails(_owner.Id + ".UniaxialBending",
                        SteelSectionCapability.BendingScope, key,
                        new[] { SteelSectionResults.Metric("M1", input.Forces.M1, m1), SteelSectionResults.Metric("M2", input.Forces.M2, m2) },
                        trace: trace,
                        warnings: new[] { "Axial resistance, biaxial N-M interaction, net sections, lateral torsional buckling and member stability are not certified by this method." }));
                }
                var shearTrace = new List<CheckCalculationValue> {
                    new CheckCalculationValue("GammaM0", _owner.Code.GammaM0, "1", "Explicit code parameter"),
                        new CheckCalculationValue("Web.ClearHeight", prepared.WebHeight, "mm", "Clear height between flanges"),
                        new CheckCalculationValue("Web.Thickness", prepared.WebThickness, "mm", "Web thickness"),
                        new CheckCalculationValue("Web.Eta", eta, "1", "EN1993-1-1:2005 6.2.6(6)") ,
                    new CheckCalculationValue("Fy", section.SteelMaterial.Fyk, "MPa", "Model steel material")
                };
                SteelSectionResults.AddClassificationTrace(shearTrace, classification);
                SteelSectionResults.AddTrace(shearTrace, "V1", shear1); SteelSectionResults.AddTrace(shearTrace, "V2", shear2);
                return NativeResults.Decision(new NativeMethodDetails(_owner.Id, SteelSectionCapability.ShearScope, key,
                    new[] { SteelSectionResults.Metric("V1", input.Forces.V1, shear1), SteelSectionResults.Metric("V2", input.Forces.V2, shear2) },
                    trace: shearTrace,
                    warnings: new[] { "R02: legacy EN1993 suite has unresolved failures. R03 bulk-result indexing path is not called.",
                        "SectionResistanceVerifier applies GammaM0; scalar web-slenderness applicability excludes shear buckling." }));
            }
        }
    }
}
