using System;
using System.Collections.Generic;
using System.Threading;
using GPC.Checkers.Steel.Checkers;
using GPC.Checkers.Steel.Sections;
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
        public string Id => IncludeUniaxialBending ? "Steel.EN1993.Section" : "Steel.EN1993.PlasticShear";
        public string Version => NativeResults.Version(typeof(EN1993p11Checker));
        public string Configuration => ModelValues.Fingerprint(new object[] { Id, Code, Edition, NationalAnnex });
        public CheckStandardContext Standard => new CheckStandardContext(Code.Name, Edition, NationalAnnex, Id, Configuration,
            "Native section shear and, when explicitly selected, uniaxial bending; no N-M interaction or global stability.");
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
            internal SectionClass CompressionClass(ISteelSection section) => CalculateSectionClassDueToCompression(new GPC.Model.Results.ResultBeamForces(-1, 0, 0, 0, 0, 0, GPC.Geometry.CoordinateSystem.Global), section);
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
                if (_owner.Edition != "2005") return NativeResults.Unavailable("SteelEditionNotQualified");
                bool bending = mechanism == CheckMechanism.UlsBiaxialSection && _owner.IncludeUniaxialBending;
                if (mechanism != CheckMechanism.Shear && !bending) return NativeResults.Unavailable("SteelMethodNotQualified", "EN1993 legacy stability/classification/interaction regressions remain open; this adapter enables only explicitly qualified section primitives.");
                var section = input.Property as SteelSection;
                if (!(section?.SectionShape is SectionH h) || h.GetType() != typeof(SectionH))
                    return NativeResults.Unavailable("SteelRequiresConstantHSection");
                if (h.LenghtTopFlange != h.LenghtBottomFlange || h.ThicknessTopFlange != h.ThicknessBottomFlange)
                    return NativeResults.Unavailable("SteelAsymmetricSectionNotQualified");
                if (_owner.Code.GetType() != typeof(StandardEN1993p11)) return NativeResults.Unavailable("SteelStandardVariantNotQualified");
                if (!(_owner.Code.GammaM0 > 0) || double.IsInfinity(_owner.Code.GammaM0)) return NativeResults.Missing("InvalidGammaM0");
                // The scalar context validates both declared factors, including the currently unused GammaM2.
                if (!(_owner.Code.GammaM2 > 0) || double.IsInfinity(_owner.Code.GammaM2)) return NativeResults.Missing("InvalidGammaM2");
                var code = SectionResistanceCode.Eurocode3(_owner.Code.GammaM0, _owner.Code.GammaM2,
                    string.IsNullOrWhiteSpace(_owner.NationalAnnex) ? "Explicit StandardEN1993p11 parameters" : _owner.NationalAnnex);
                if (input.Forces.T != 0) return NativeResults.Unavailable("SteelTorsionInteractionNotQualified");
                // The native compressed-section classification provides a conservative stockiness gate for both flange directions.
                string key = ModelValues.Fingerprint(new object[] { input.Property, _owner.Configuration });
                if (!_native.TryGetValue(key, out var checker))
                {
                    checker = new NativeSectionProperties(new BeamElement(input.Element.StartPoint, input.Element.EndPoint, section), _owner.Code);
                    _native.Add(key, checker);
                }
                var shear1 = SectionResistanceVerifier.Shear(code, input.Forces.V1, checker.ShearArea1(section), section.SteelMaterial.Fyk);
                var shear2 = SectionResistanceVerifier.Shear(code, input.Forces.V2, checker.ShearArea2(section), section.SteelMaterial.Fyk);
                var failure = SteelSectionResults.Failure("V1", shear1) ?? SteelSectionResults.Failure("V2", shear2);
                if (failure != null) return failure;
                var sectionClass = checker.CompressionClass(section);
                if (sectionClass > EN1993p11Checker.SectionClass.Class2) return NativeResults.Unavailable("SteelSlenderSectionNotQualified");
                double v2 = shear2.Resistance.Value;
                double fullCapacity2 = checker.FullCapacity2(input.Forces, section);
                if (!(fullCapacity2 > 0) || double.IsInfinity(fullCapacity2))
                {
                    var error = NativeResults.Missing("SteelShearBucklingNumericalRange");
                    error.Execution = ExecutionStatus.Error;
                    return error;
                }
                if (Math.Abs(fullCapacity2 - v2) > 1e-9 * Math.Max(1, v2)) return NativeResults.Unavailable("SteelShearBucklingNotQualified");
                if (bending)
                {
                    // Preserve the qualified low-shear branch and the sign-independent applicability gates.
                    if (input.Forces.N != 0 || input.Forces.M1 != 0 && input.Forces.M2 != 0)
                        return NativeResults.Unavailable("SteelCombinedNormalActionsNotQualified");
                    if (Math.Abs(input.Forces.V1) > .5 * shear1.Resistance.Value || Math.Abs(input.Forces.V2) > .5 * v2)
                        return NativeResults.Unavailable("SteelHighShearBendingNotQualified");
                    var m1 = SectionResistanceVerifier.Bending(code, input.Forces.M1, section.Wpl1, null, section.SteelMaterial.Fyk, (int)sectionClass);
                    var m2 = SectionResistanceVerifier.Bending(code, input.Forces.M2, section.Wpl2, null, section.SteelMaterial.Fyk, (int)sectionClass);
                    failure = SteelSectionResults.Failure("M1", m1) ?? SteelSectionResults.Failure("M2", m2);
                    if (failure != null) return failure;
                    var trace = new List<CheckCalculationValue> {
                        new CheckCalculationValue("GammaM0", _owner.Code.GammaM0, "1", "Explicit code parameter"),
                        new CheckCalculationValue("CompressionClass", (int)sectionClass, "1", "Conservative stockiness gate"),
                        new CheckCalculationValue("ShearRatio1", shear1.Utilization, "1", "Low shear <= 0.5"),
                        new CheckCalculationValue("ShearRatio2", shear2.Utilization, "1", "Low shear <= 0.5")
                    };
                    SteelSectionResults.AddTrace(trace, "M1", m1); SteelSectionResults.AddTrace(trace, "M2", m2);
                    SteelSectionResults.AddTrace(trace, "V1", shear1); SteelSectionResults.AddTrace(trace, "V2", shear2);
                    return NativeResults.Decision(new NativeMethodDetails(_owner.Id + ".UniaxialBending",
                        "Uniaxial plastic cross-section bending, N=T=0, low shear, stocky symmetric H; member stability is a separate check.", key,
                        new[] { SteelSectionResults.Metric("M1", input.Forces.M1, m1), SteelSectionResults.Metric("M2", input.Forces.M2, m2) },
                        trace: trace,
                        warnings: new[] { "Axial resistance, biaxial N-M interaction, net sections, lateral torsional buckling and member stability are not certified by this method." }));
                }
                var shearTrace = new List<CheckCalculationValue> {
                    new CheckCalculationValue("GammaM0", _owner.Code.GammaM0, "1", "Explicit code parameter"),
                    new CheckCalculationValue("Fy", section.SteelMaterial.Fyk, "MPa", "Model steel material")
                };
                SteelSectionResults.AddTrace(shearTrace, "V1", shear1); SteelSectionResults.AddTrace(shearTrace, "V2", shear2);
                return NativeResults.Decision(new NativeMethodDetails(_owner.Id, "Two individual section shear checks. Does not certify N-M-V interaction or member stability.", key,
                    new[] { SteelSectionResults.Metric("V1", input.Forces.V1, shear1), SteelSectionResults.Metric("V2", input.Forces.V2, shear2) },
                    trace: shearTrace,
                    warnings: new[] { "R02: legacy EN1993 suite has unresolved failures. R03 bulk-result indexing path is not called.",
                        "SectionResistanceVerifier applies GammaM0; native section properties and shear-buckling applicability remain in use." }));
            }
        }
    }
}
