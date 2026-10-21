using System;
using System.Collections.Generic;
using System.Threading;
using GPC.Checkers.Steel.Checkers;
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
    /// Global buckling and cross-section interactions remain gated by the unresolved legacy regressions.</summary>
    public sealed class SteelMaterialChecker : IMaterialChecker
    {
        public StandardEN1993p11 Code { get; }
        public string Edition { get; }
        public string NationalAnnex { get; }
        public bool IncludeUniaxialBending { get; }
        internal SteelSectionCapability Capability => IncludeUniaxialBending ? SteelSectionCapability.Section : SteelSectionCapability.Shear;
        public string Id => Capability.Id;
        public string Version => NativeResults.Version(typeof(EN1993p11Checker));
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
        private sealed class NativeSectionProperties : EN1993p11Checker
        {
            // These native constructor fields are not consumed by the section-only primitives below.
            internal NativeSectionProperties(BeamElement beam, StandardEN1993p11 code) : base(beam, new EN1993p11Options(
                EN1993p11Options.LoadConditions.Constant, EN1993p11Options.SupportConditions.HingesAtEnds,
                EN1993p11Options.LateralSupportConditions.HingesAtEnds, EN1993p11Options.LateralWarpingConditions.HingesAtEnds, null, null), code) { }
            internal double ShearArea1(ISteelSection section) => GetShearArea1(section);
            internal double ShearArea2(ISteelSection section) => GetShearArea2(section);
            internal double FullCapacity2(GPC.Model.Results.ResultBeamForces forces, ISteelSection section) => CalculateShear2Capacity(forces, section);
        }
        private sealed class Session : IMaterialCheckSession
        {
            private readonly SteelMaterialChecker _owner;
            private readonly Dictionary<string, NativeSectionProperties> _native = new Dictionary<string, NativeSectionProperties>();
            public int CreatedCheckers => _native.Count;
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
                if (!_native.TryGetValue(key, out var checker))
                {
                    checker = new NativeSectionProperties(new BeamElement(input.Element.StartPoint, input.Element.EndPoint, section), _owner.Code);
                    _native.Add(key, checker);
                }
                var shear1 = SectionResistanceVerifier.Shear(code, input.Forces.V1, checker.ShearArea1(section), section.SteelMaterial.Fyk);
                var shear2 = SectionResistanceVerifier.Shear(code, input.Forces.V2, checker.ShearArea2(section), section.SteelMaterial.Fyk);
                failure = SteelSectionResults.Failure("V1", shear1) ?? SteelSectionResults.Failure("V2", shear2);
                if (failure != null) return failure;
                var classification = Ec3UniformCompressionClassification.Evaluate(
                    ModelCompressionGeometry.Create(section), section.SteelMaterial.Fyk);
                failure = SteelSectionResults.ClassificationFailure(classification);
                if (failure != null) return failure;
                var sectionClass = (EN1993p11Checker.SectionClass)classification.NominalClass.Value;
                failure = SteelSectionCapability.ClassificationFailure(sectionClass);
                if (failure != null) return failure;
                failure = SteelSectionCapability.ShearBucklingFailure(shear2.Resistance.Value, checker.FullCapacity2(input.Forces, section));
                if (failure != null) return failure;
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
                    new CheckCalculationValue("Fy", section.SteelMaterial.Fyk, "MPa", "Model steel material")
                };
                SteelSectionResults.AddClassificationTrace(shearTrace, classification);
                SteelSectionResults.AddTrace(shearTrace, "V1", shear1); SteelSectionResults.AddTrace(shearTrace, "V2", shear2);
                return NativeResults.Decision(new NativeMethodDetails(_owner.Id, SteelSectionCapability.ShearScope, key,
                    new[] { SteelSectionResults.Metric("V1", input.Forces.V1, shear1), SteelSectionResults.Metric("V2", input.Forces.V2, shear2) },
                    trace: shearTrace,
                    warnings: new[] { "R02: legacy EN1993 suite has unresolved failures. R03 bulk-result indexing path is not called.",
                        "SectionResistanceVerifier applies GammaM0; native section properties and shear-buckling applicability remain in use." }));
            }
        }
    }
}
