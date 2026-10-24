using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Actual unperforated rolled H cut, N/mm/MPa/Nmm. Principal axis1
    /// is horizontal X, axis2 vertical Y, axis3 along the beam away from the column.
    /// On the positive section face M1>0 tensions the upper flange: the core uses
    /// M1=integral(sigma*y). Actions refer to the cut centroid, not an offset point.
    /// Explicit flange strengths are required because Beam.Fy/Fu describe its WEB.
    /// Source strings record provenance; the assessment/token binds actual inputs.</summary>
    public sealed class EndPlateBeamSectionInput
    {
        public ConnectionDesignCode Code { get; }
        public ConnectionRolledISection Beam { get; }
        public SteelSectionInput Section { get; }
        public SteelActions Actions { get; }
        public double FlangeYieldStrength { get; }
        public double FlangeUltimateStrength { get; }
        public bool HasNotchesOrOpenings { get; }
        public string MaterialAtActualThicknessSource { get; }
        public string GeometrySource { get; }
        public string ActionCombinationSource { get; }
        public double? Ec3ShearEta { get; }
        public string Ec3ShearEtaSource { get; }
        public EndPlateBeamSectionInput(ConnectionDesignCode code, ConnectionRolledISection beam,
            SteelSectionInput section, SteelActions actions, double flangeYieldStrength,
            double flangeUltimateStrength, bool hasNotchesOrOpenings, string materialAtActualThicknessSource,
            string geometrySource, string actionCombinationSource, double? ec3ShearEta = null,
            string ec3ShearEtaSource = null)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Beam = beam ?? throw new ArgumentNullException(nameof(beam));
            Section = section ?? throw new ArgumentNullException(nameof(section));
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));
            ConnectionValidation.Positive(flangeYieldStrength, nameof(flangeYieldStrength));
            ConnectionValidation.Positive(flangeUltimateStrength, nameof(flangeUltimateStrength));
            if (flangeUltimateStrength < flangeYieldStrength) throw new ArgumentException("Flange fu must not be below fy.");
            if (ec3ShearEta.HasValue) ConnectionValidation.Positive(ec3ShearEta.Value, nameof(ec3ShearEta));
            FlangeYieldStrength = flangeYieldStrength; FlangeUltimateStrength = flangeUltimateStrength;
            HasNotchesOrOpenings = hasNotchesOrOpenings; MaterialAtActualThicknessSource = materialAtActualThicknessSource;
            GeometrySource = geometrySource; ActionCombinationSource = actionCombinationSource;
            Ec3ShearEta = ec3ShearEta; Ec3ShearEtaSource = ec3ShearEtaSource;
        }
    }

    public sealed class EndPlateBeamSectionAssessment
    {
        public EndPlateBeamSectionInput Input { get; }
        public SteelVerificationInput CoreInput { get; }
        public SteelSectionClassification Classification { get; }
        public IReadOnlyList<NamedCompressionPartClassification> ClassificationParts { get; }
        public SteelShearBucklingAssessment ShearBucklingAssessment { get; }
        public SteelVerificationResult SectionVerification { get; }
        public EndPlateBeamMomentResistance DerivedResistance { get; }
        public ConnectionStatus Status { get; }
        public bool IsSectionAssessmentComplete => Diagnostics.Count == 0 && DerivedResistance != null;
        public bool CanQualifyWholeMember => false;
        public bool CanQualifyWholeJoint => false;
        public IReadOnlyList<ConnectionCheck> Checks { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public string Scope => "Actual unperforated homogeneous rolled symmetric I/H cut: N=M2=V1=T=0, M1>=0, signed V2. "
            + "Class1/2 major M-V with shear buckling excluded; class3 V=0. No class4, holes, member stability or whole-joint qualification.";
        internal EndPlateBeamSectionAssessment(EndPlateBeamSectionInput input, SteelVerificationInput core,
            SteelSectionClassification classification, IEnumerable<NamedCompressionPartClassification> parts,
            SteelShearBucklingAssessment shear, SteelVerificationResult pipeline, EndPlateBeamMomentResistance resistance,
            IEnumerable<ConnectionCheck> checks, IEnumerable<SectionResistanceValue> trace, IEnumerable<string> diagnostics)
        {
            Input = input; CoreInput = core; Classification = classification;
            ClassificationParts = Array.AsReadOnly(parts.ToArray()); ShearBucklingAssessment = shear;
            SectionVerification = pipeline; DerivedResistance = resistance;
            Checks = Array.AsReadOnly(checks.ToArray()); Trace = Array.AsReadOnly(trace.ToArray());
            Diagnostics = Array.AsReadOnly(diagnostics.Distinct().ToArray());
            // An evaluated failure remains visible even when another result is unavailable.
            Status = Checks.Any(c => !c.Passed) || (pipeline?.Checks.Any(c => c.Status == SteelExecutionStatus.Failed) ?? false)
                ? ConnectionStatus.Failed : IsSectionAssessmentComplete ? ConnectionStatus.Passed : ConnectionStatus.NotSupported;
        }
    }

    public static class EndPlateBeamSectionAdapter
    {
        public const string MethodId = "Steel.Connection.EndPlate.BeamSectionAdapter";
        public static EndPlateBeamSectionAssessment Evaluate(EndPlateBeamSectionInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var issues = new List<string>(); var trace = new List<SectionResistanceValue>();
            var checks = new List<ConnectionCheck>(); var parts = new List<NamedCompressionPartClassification>();
            SteelVerificationInput core = null; SteelSectionClassification classification = null;
            SteelShearBucklingAssessment shear = null; SteelVerificationResult pipeline = null;
            EndPlateBeamMomentResistance resistance = null;
            var a = p.Actions; var s = p.Section; var b = p.Beam;
            Required(p.GeometrySource, "ACTUAL_GEOMETRY_SOURCE_REQUIRED");
            Required(p.MaterialAtActualThicknessSource, "FLANGE_AND_WEB_MATERIAL_SOURCE_REQUIRED");
            Required(p.ActionCombinationSource, "ACTION_COMBINATION_SOURCE_REQUIRED");
            Gate(a.N == 0 && a.V1 == 0 && a.T == 0 && a.M2 == 0 && a.M1 >= 0,
                "UPPER_FLANGE_TENSION_PURE_MAJOR_BENDING_AND_WEB_SHEAR_ONLY");
            Gate(s.Unperforated == true && !s.NetArea.HasValue && !p.HasNotchesOrOpenings,
                "DECLARED_UNPERFORATED_UNNOTCHED_BEAM_REQUIRED");
            Gate(p.FlangeYieldStrength == b.Fy && s.YieldStrength == b.Fy
                && p.FlangeUltimateStrength == b.Fu && s.UltimateStrength == b.Fu
                && b.Fy <= 460, "HOMOGENEOUS_FLANGE_WEB_MODEL_MATERIAL_REQUIRED");
            if (p.Code.IsNtc)
                Gate(!p.Ec3ShearEta.HasValue && string.IsNullOrWhiteSpace(p.Ec3ShearEtaSource), "NTC_USES_OWN_SHEAR_ETA_ONE");
            else
            {
                Gate(p.Ec3ShearEta.HasValue, "EC3_SHEAR_ETA_REQUIRED");
                Required(p.Ec3ShearEtaSource, "EC3_SHEAR_ETA_SOURCE_REQUIRED");
            }
            var names = new[] { "N", "V1", "V2", "T", "M1", "M2" };
            var values = new[] { a.N, a.V1, a.V2, a.T, a.M1, a.M2 };
            for (int i = 0; i < names.Length; i++) Value("Actions." + names[i], values[i], i < 3 ? "N" : "Nmm");
            Value("Material.FlangeFy", p.FlangeYieldStrength, "MPa"); Value("Material.FlangeFu", p.FlangeUltimateStrength, "MPa");
            Value("Material.WebFy", b.Fy, "MPa"); Value("Material.WebFu", b.Fu, "MPa");
            Value("Material.ModelFy", s.YieldStrength, "MPa"); Value("Material.ModelFu", s.UltimateStrength, "MPa");
            Value("Geometry.ModelArea", s.Area, "mm2"); Value("Geometry.ConnectionArea", b.Area, "mm2");
            Value("Geometry.PrincipalAxisAngle", s.PrincipalAxisAngle, "rad");
            Value("GammaM0", p.Code.Plates.GammaM0, "1");
            try
            {
                var model = s.CreateSectionCopy();
                Gate(model.SectionShape.GetType() == typeof(SectionH) && model.SectionType == Section.SectionTypes.Rolled
                    && model.FormedType == Section.FormedTypes.HotFinished, "EXACT_ROLLED_HOT_FINISHED_H_REQUIRED");
                if (model.SectionShape.GetType() != typeof(SectionH)) return Result();
                var h = (SectionH)model.SectionShape;
                Gate(Same(h.Height, b.Height) && Same(h.ThicknessWeb, b.WebThickness)
                    && Same(h.LenghtTopFlange, b.Width) && Same(h.LenghtBottomFlange, b.Width)
                    && Same(h.ThicknessTopFlange, b.FlangeThickness) && Same(h.ThicknessBottomFlange, b.FlangeThickness)
                    && Same(h.R, b.RootRadius), "MODEL_AND_CONNECTION_GEOMETRY_MUST_MATCH");
                Gate(Math.Abs(s.PrincipalAxisAngle) <= 8 * 2.2204460492503131e-16,
                    "PRINCIPAL_AXIS_ONE_MUST_BE_HORIZONTAL");
                // Catalogue A alone may be independently rounded; all resistance properties remain Model values.
                Gate(Math.Abs(s.Area - b.Area) <= .005 * s.Area, "MODEL_AND_CONNECTION_AREA_OUTSIDE_CATALOGUE_ROUNDING");
                if (issues.Count != 0) return Result();
                SteelMethodSelector.ValidateGeometry(model);
                core = new SteelVerificationInput(s, a, new SteelDesignContext(p.Code.Plates));
                string reference = p.Code.IsNtc ? "NTC2018 4.2.3.1 Tables4.2.III-IV" : "EN1993-1-1:2005 Table5.2";
                // Capacity pattern, including at M=0: no fictitious action or stress-dependent relaxation.
                foreach (var part in ModelCompressionGeometry.Create(model).Parts)
                {
                    if (part.Id == "TopFlange") continue; // Upper flange is tensile for the admitted moment sign.
                    CompressionPartClassification result;
                    if (part.Kind == CompressionPartKind.InternalFlat)
                    {
                        if (!p.Code.IsNtc) result = Ec3PlateClassification.Internal(part.WidthOrDiameter, part.Thickness,
                            s.YieldStrength, .5, -1).Nominal;
                        else
                        {
                            var basic = Ntc2018CompressionClassification.EvaluatePart(part.Kind, part.WidthOrDiameter,
                                part.Thickness, s.YieldStrength);
                            result = basic;
                            if (basic.IsEvaluated)
                            {
                                PlateClassificationArithmetic.InternalLimits(.5, -1, basic.Epsilon.Value,
                                    out double first, out double second, out double third);
                                result = new CompressionPartClassification(part.Kind, SectionClassificationStatus.Completed,
                                    nominalClass: PlateClassificationArithmetic.Classify(basic.WidthThicknessRatio.Value, first, second, third),
                                    width: basic.WidthOrDiameter, thickness: basic.Thickness, fy: basic.YieldStrength,
                                    ratio: basic.WidthThicknessRatio, epsilon: basic.Epsilon, first: first, second: second, third: third,
                                    reference: reference);
                            }
                        }
                    }
                    else result = p.Code.IsNtc
                        ? Ntc2018CompressionClassification.EvaluatePart(part.Kind, part.WidthOrDiameter, part.Thickness, s.YieldStrength)
                        : Ec3CompressionPartClassification.Evaluate(part.Kind, part.WidthOrDiameter, part.Thickness, s.YieldStrength);
                    parts.Add(new NamedCompressionPartClassification(part.Id, result));
                    Gate(result.IsEvaluated, "CLASSIFICATION_" + part.Id + ":" + result.DiagnosticCode);
                }
                if (issues.Count != 0) return Result();
                int sectionClass = parts.Max(part => part.Classification.NominalClass.Value);
                classification = new SteelSectionClassification(core, sectionClass,
                    reference + "; major-bending resistance pattern: web alpha0.5/psi-1, lower flange uniform compression; no5.5.2(9) relaxation");
                Value("SectionClass", sectionClass, "1"); Value("Classification.WebAlpha", .5, "1"); Value("Classification.WebPsi", -1, "1");
                Gate(sectionClass <= 3, "CLASS4_BEAM_ADAPTER_NOT_SUPPORTED");
                Gate(sectionClass != 3 || a.V2 == 0, "CLASS3_WITH_SHEAR_NOT_SUPPORTED_BY_THIS_ADAPTER");
                if (issues.Count != 0) return Result();
                shear = p.Code.IsNtc ? SteelShearBucklingAssessment.EvaluateNtcHWeb(core, "NTC2018 4.2.4.1.2.4 eq4.2.27; actual Model H")
                    : SteelShearBucklingAssessment.EvaluateEc3HWeb(core, p.Ec3ShearEta.Value, p.Ec3ShearEtaSource);
                trace.AddRange(shear.Trace.Select(v => new SectionResistanceValue("ShearBuckling." + v.Name, v.Value, v.Unit)));
                pipeline = SteelVerificationPipeline.Execute(SteelMethodSelector.Select(core, SteelCheckScope.Section, classification), new[] { shear });
                // NotRequired at the actual zero action is legitimate; never force a fake M=1 route.
                foreach (var check in pipeline.Checks.Where(c => !c.IsEvaluated && c.Status != SteelExecutionStatus.NotRequired))
                    issues.Add("PIPELINE_" + check.Decision.Target + ":" + check.Status + ":" + check.DiagnosticCode);
                // Class3 at V=0 exports only elastic Wel resistance: no shear action can buckle
                // the web. The plastic M-V route retains its explicit buckling-exclusion gate.
                Gate(sectionClass == 3 && a.V2 == 0 || shear.IsBucklingExcluded,
                    "SHEAR_BUCKLING_EXCLUSION_REQUIRED_FOR_CAPACITY_EXPORT");
                if (issues.Count != 0) return Result();
                double av = ModelShearAreas.GetArea2(model, p.Code.Plates.Standard);
                Value("Geometry.Av2", av, "mm2"); Value("Geometry.Wpl1", model.Wpl1, "mm3");
                Value("Geometry.Wel1", model.Wel1, "mm3"); Value("Geometry.I1", model.J11, "mm4");
                // Bending is only the un-reduced baseline; the actual high-shear check is below and in the pipeline.
                var baseline = SectionResistanceVerifier.Bending(p.Code.Plates, a.M1, model.Wpl1, model.Wel1, s.YieldStrength, sectionClass);
                var plasticShear = SectionResistanceVerifier.Shear(p.Code.Plates, a.V2, av, s.YieldStrength);
                Gate(baseline.IsEvaluated, "BASELINE_BENDING:" + baseline.DiagnosticCode);
                Gate(plasticShear.IsEvaluated, "PLASTIC_SHEAR:" + plasticShear.DiagnosticCode);
                if (issues.Count != 0) return Result();
                double vpl = plasticShear.Resistance.Value;
                // No plastic shear verdict for an inactive slender class3 web: its pipeline
                // shear check remains NotRequired and its buckling assessment remains false.
                if (shear.IsBucklingExcluded)
                    checks.Add(new ConnectionCheck("BeamSection.WebShear", Math.Abs(a.V2), vpl, plasticShear.Reference));
                Value("VplRd", vpl, "N"); Value("UnreducedMomentResistance", baseline.Resistance.Value, "Nmm");
                if (Math.Abs(a.V2) > vpl)
                { issues.Add("SHEAR_EXCEEDS_VPL_NO_MOMENT_CAPACITY_EXPORT"); return Result(); }
                double mc = baseline.Resistance.Value;
                if (sectionClass <= 2)
                {
                    double rho = SectionInteractionArithmetic.ShearReductionRho(a.V2, vpl), aw = h.HeightWeb * h.ThicknessWeb;
                    mc = Math.Min(mc, SectionInteractionArithmetic.MajorIMomentWithShear(model.Wpl1, rho, aw,
                        h.ThicknessWeb, s.YieldStrength, p.Code.Plates.GammaM0));
                    Value("Rho", rho, "1"); Value("WebAreaForInteraction", aw, "mm2");
                }
                Value("MomentResistance", mc, "Nmm"); Value("MomentUtilization", a.M1 / mc, "1");
                Gate(mc > 0, "POSITIVE_MOMENT_RESISTANCE_REQUIRED");
                if (issues.Count != 0) return Result();
                string capacityReference = p.Code.IsNtc ? "NTC2018 4.2.4.1.2.3/4.2.4.1.2.6 eq4.2.30-32"
                    : "EN1993-1-1:2005 6.2.5/6.2.8 eq6.29-30";
                checks.Add(new ConnectionCheck("BeamSection.MajorMomentWithShear", a.M1, mc, capacityReference, "Nmm"));
                resistance = new EndPlateBeamMomentResistance(p.Code, b, mc, Math.Abs(a.V2),
                    MethodId + "; " + capacityReference + "; class" + sectionClass + "; " + p.ActionCombinationSource);
            }
            catch (NotSupportedException ex) { issues.Add("ADAPTER_NOT_SUPPORTED:" + ex.Message); }
            catch (ArgumentException ex) { issues.Add("ADAPTER_INVALID_INPUT:" + ex.Message); }
            catch (ArithmeticException ex) { issues.Add("ADAPTER_NUMERICAL_RANGE:" + ex.Message); }
            return Result();

            void Required(string value, string code) { if (string.IsNullOrWhiteSpace(value)) issues.Add(code); }
            void Gate(bool valid, string code) { if (!valid) issues.Add(code); }
            void Value(string name, double value, string unit)
            { if (!Finite(value)) issues.Add(name + ".OUTSIDE_NUMERIC_RANGE"); else trace.Add(new SectionResistanceValue(name, value, unit)); }
            EndPlateBeamSectionAssessment Result() => new EndPlateBeamSectionAssessment(p, core, classification,
                parts, shear, pipeline, issues.Count == 0 ? resistance : null, checks, trace, issues);
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool Same(double a, double b) => Finite(a) && Finite(b)
            && Math.Abs(a - b) <= 8 * 2.2204460492503131e-16 * Math.Max(Math.Abs(a), Math.Abs(b));
    }

    /// <summary>Retains both the actual section assessment and the existing joint
    /// component result. Provenance text is not a substitute for the derived token.
    /// The joint shear input is a magnitude; the section's signed V2 remains visible.</summary>
    public sealed class EndPlateMomentWithBeamSectionResult
    {
        public EndPlateMomentComponentInput Input { get; }
        public EndPlateBeamSectionAssessment BeamSection { get; }
        public EndPlateMomentComponentResult MomentComponents { get; }
        public ConnectionStatus Status { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public bool IsComplete => Diagnostics.Count == 0 && BeamSection.IsSectionAssessmentComplete
            && MomentComponents != null && MomentComponents.ComponentAssessment.IsComplete;
        public bool CanQualifyWholeJoint => false;
        public bool CanQualifyWholeMember => false;
        internal EndPlateMomentWithBeamSectionResult(EndPlateMomentComponentInput input, EndPlateBeamSectionAssessment beam,
            EndPlateMomentComponentResult joint, IEnumerable<string> diagnostics)
        {
            Input = input; BeamSection = beam; MomentComponents = joint;
            Diagnostics = Array.AsReadOnly(diagnostics.Distinct().ToArray());
            Status = beam.Status == ConnectionStatus.Failed || joint?.ComponentAssessment.Status == ConnectionStatus.Failed
                ? ConnectionStatus.Failed : IsComplete ? ConnectionStatus.Passed : ConnectionStatus.NotSupported;
        }
    }

    public static class EndPlateMomentWithBeamSection
    {
        public static EndPlateMomentWithBeamSectionResult Verify(EndPlateMomentComponentInput joint,
            EndPlateBeamSectionAssessment beam)
        {
            if (joint == null) throw new ArgumentNullException(nameof(joint));
            if (beam == null) throw new ArgumentNullException(nameof(beam));
            var issues = new List<string>(); var p = beam.Input; var a = p.Actions;
            if (beam.DerivedResistance == null || !beam.IsSectionAssessmentComplete)
                issues.Add("BEAM_SECTION_HAS_NO_QUALIFIED_DERIVED_RESISTANCE");
            if (!ReferenceEquals(joint.BeamMomentResistance, beam.DerivedResistance))
                issues.Add("DERIVED_RESISTANCE_TOKEN_MUST_BE_IDENTICAL");
            if (!ReferenceEquals(joint.Policy.Code, p.Code) || !ReferenceEquals(joint.Beam, p.Beam))
                issues.Add("CODE_AND_BEAM_INSTANCES_MUST_MATCH");
            if (joint.Moment != a.M1 || joint.Shear != Math.Abs(a.V2) || joint.Axial != a.N)
                issues.Add("CONCOMITANT_ACTIONS_MUST_MATCH_EXACTLY");
            // Source labels can differ; token + immutable actions/instances provide the actual binding.
            EndPlateMomentComponentResult result = issues.Count == 0 ? EndPlateMomentComponents.Verify(joint) : null;
            if (result != null) issues.AddRange(result.ComponentAssessment.Diagnostics.Select(d => "Joint:" + d));
            return new EndPlateMomentWithBeamSectionResult(joint, beam, result, issues);
        }
    }
}
