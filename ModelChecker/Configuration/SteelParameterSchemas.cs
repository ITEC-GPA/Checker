using System;
using System.Collections.Generic;
using System.Linq;

namespace GPC.Model.Checker.Configuration
{
    /// <summary>Single closed parameter vocabulary shared by metadata and the runtime adapters.</summary>
    public static class SteelParameterSchemas
    {
        public static EngineParameterSchema Section { get; }=Build(false);
        public static EngineParameterSchema Member { get; }=Build(true);
        /// <summary>Steel.Serviceability.Member: declared limits, schemes and sources of the member deformation checks.</summary>
        public static EngineParameterSchema MemberServiceability { get; }=BuildServiceability();
        private static EngineParameterSchema BuildServiceability()
        {
            var fields=new List<EngineParameterDescriptor>();var rules=new List<EngineParameterRule>();
            void Add(string key,EngineParameterKind kind=EngineParameterKind.Text,string unit=null,string description=null,
                double? min=null,bool exclusive=false,string[] choices=null)
                =>fields.Add(new EngineParameterDescriptor(key,kind,unit,description??key,min,null,exclusive,choices,null));
            EngineParameterCondition Has(string key)=>new EngineParameterCondition(key);
            EngineParameterCondition Is(string key,string value)=>new EngineParameterCondition(key,value);
            EngineParameterCondition IsNot(string key,string value)=>new EngineParameterCondition(key,value,true);
            void Require(string code,string description,string[] keys,params EngineParameterCondition[] when)
                =>rules.Add(new EngineParameterRule(code,description,when,new[]{keys}));
            void Together(string code,params string[] keys) {
                foreach(var key in keys) Require(code,"These values must be supplied together: "+string.Join(", ",keys),keys,Has(key));
            }
            Add("Deformation.CoverageSource",description:"Qualification that the exported member nodes include the location of the largest deflection of the same state (refined mesh, symmetry); no deflection is inferred between nodes. Vertical deflection and vibration.");
            Add("Deflection.Scheme",EngineParameterKind.Choice,choices:new[]{"SupportedEnds","CantileverAtStart","CantileverAtEnd"},
                description:"Supports at the ends of the design span Start/End: chord through both displaced supports, or the translation of the supported end of a cantilever (its rotation stays in the deflection).");
            Add("Deflection.SchemeSource",description:"Evidence that the members of this engine are supported as declared at the ends of their design span.");
            Together("DeflectionSchemePair","Deflection.Scheme","Deflection.SchemeSource");
            Add("Deflection.MaxRatio",EngineParameterKind.Number,"1",min:0,exclusive:true,description:"n of the limit L/n on delta_max = |delta_tot + precamber| at the node of the largest total deflection (EN 1990 A1.4.3 w_max; NTC 4.2.4.2.1 delta_max).");
            Add("Deflection.MaxSource",description:"Source of the delta_max limit: standard table row, national annex or project agreement.");
            Together("DeflectionMaxPair","Deflection.MaxRatio","Deflection.MaxSource");
            Add("Deflection.VariableRatio",EngineParameterKind.Number,"1",min:0,exclusive:true,description:"n of the limit L/n on the deflection of the variable actions (NTC delta_2, EN 1990 w_3), from the variable state declared by the task.");
            Add("Deflection.VariableSource",description:"Source of the variable-action deflection limit.");
            Together("DeflectionVariablePair","Deflection.VariableRatio","Deflection.VariableSource");
            Add("Deflection.Precamber",EngineParameterKind.Number,"mm",description:"Signed precamber along the checked member axis at the node of the largest total deflection (unloaded offset from the chord); explicit 0 without precamber.");
            Add("Deflection.PrecamberSource",description:"Source of a nonzero precamber (fabrication drawings, camber specification).");
            Require("PrecamberRequired","delta_max needs the explicit precamber, 0 for none.",new[]{"Deflection.Precamber"},Has("Deflection.MaxRatio"));
            Require("PrecamberSourceRequired","A nonzero precamber needs its source.",new[]{"Deflection.PrecamberSource"},Has("Deflection.Precamber"),IsNot("Deflection.Precamber","0"));
            rules.Add(new EngineParameterRule("PrecamberWithoutMaximumDeflection","The precamber belongs to the delta_max check.",new[]{IsNot("Deflection.MaxRatio",null)},null,new[]{"Deflection.Precamber","Deflection.PrecamberSource"}));
            Add("Deflection.CantileverLengthFactor",EngineParameterKind.Number,"1",min:0,exclusive:true,
                description:"Reference length of a cantilever limit as a multiple of the cantilever length (2 where the applicable rule refers the limit of a cantilever to twice its length); declared, never assumed.");
            Add("Deflection.CantileverLengthSource",description:"Source of the cantilever reference length.");
            Together("CantileverReferencePair","Deflection.CantileverLengthFactor","Deflection.CantileverLengthSource");
            foreach(var cantilever in new[]{"CantileverAtStart","CantileverAtEnd"})
                Require("CantileverReferenceRequired","A cantilever needs its declared reference length.",new[]{"Deflection.CantileverLengthFactor","Deflection.CantileverLengthSource"},Is("Deflection.Scheme",cantilever));
            rules.Add(new EngineParameterRule("CantileverReferenceOnSupportedSpan","A span supported at both ends uses L = span.",new[]{Is("Deflection.Scheme","SupportedEnds")},null,
                new[]{"Deflection.CantileverLengthFactor","Deflection.CantileverLengthSource"}));
            foreach(var limit in new[]{"Deflection.MaxRatio","Deflection.VariableRatio"})
                Require("DeflectionSchemeRequired","A deflection limit needs the support scheme, its source and the node coverage.",
                    new[]{"Deflection.Scheme","Deflection.SchemeSource","Deformation.CoverageSource"},Has(limit));
            rules.Add(new EngineParameterRule("DeflectionLimitRequired","A deflection scheme needs at least one declared limit.",new[]{Has("Deflection.Scheme")},
                new[]{new[]{"Deflection.MaxRatio","Deflection.MaxSource"},new[]{"Deflection.VariableRatio","Deflection.VariableSource"}}));
            Add("Drift.Ratio",EngineParameterKind.Number,"1",min:0,exclusive:true,
                description:"n of the limit h/n on the relative transverse displacement of the span ends, h = SpanEnd - SpanStart (EN 1990 A1.4.3 u_i; NTC 4.2.4.2.2 Delta/h, or delta/H with the full-height column line as the member).");
            Add("Drift.Source",description:"Source of the drift limit.");
            Together("DriftPair","Drift.Ratio","Drift.Source");
            Add("Vibration.MinimumFrequency",EngineParameterKind.Number,"Hz",min:0,exclusive:true,description:"Declared minimum natural frequency.");
            Add("Vibration.Source",description:"Source of the minimum frequency: national annex, project agreement or standard clause.");
            Together("VibrationLimitPair","Vibration.MinimumFrequency","Vibration.Source");
            Add("Vibration.Scheme",EngineParameterKind.Choice,choices:new[]{"SimplySupported","DeclaredCoefficient"},
                description:"SimplySupported: f = 17.75/sqrt(delta[mm]), uniform mass, simply supported span. DeclaredCoefficient: f = K/sqrt(delta) with a qualified K for another scheme.");
            Add("Vibration.SchemeSource",description:"Evidence of the support scheme and of a uniformly distributed mass.");
            Add("Vibration.Coefficient",EngineParameterKind.Number,"Hz mm^0.5",min:0,exclusive:true,description:"K of f = K/sqrt(delta[mm]) for the declared scheme.");
            Add("Vibration.CoefficientSource",description:"Derivation of K for the actual scheme and mass distribution.");
            Together("VibrationCoefficientPair","Vibration.Coefficient","Vibration.CoefficientSource");
            Require("VibrationCoefficientRequired","A declared coefficient needs its value and qualification.",new[]{"Vibration.Coefficient","Vibration.CoefficientSource"},Is("Vibration.Scheme","DeclaredCoefficient"));
            rules.Add(new EngineParameterRule("CoefficientOnSimplySupported","The simply supported scheme uses K = 17.75.",new[]{Is("Vibration.Scheme","SimplySupported")},null,new[]{"Vibration.Coefficient","Vibration.CoefficientSource"}));
            Add("Vibration.MassStateSource",description:"Qualification that the task state is the mass state of the estimate: the masses (self weight, permanent, quasi-permanent variable) applied as gravity loads along the checked axis.");
            Add("Vibration.Deflection",EngineParameterKind.Choice,choices:new[]{"RelativeToChord","Total"},
                description:"RelativeToChord: the member alone on rigid supports. Total: absolute displacement, support displacements included (estimate of the combined system).");
            Require("VibrationModelRequired","A frequency limit needs scheme, mass state, deflection basis and node coverage.",
                new[]{"Vibration.Scheme","Vibration.SchemeSource","Vibration.MassStateSource","Vibration.Deflection","Deformation.CoverageSource"},Has("Vibration.MinimumFrequency"));
            Require("VibrationLimitRequired","A vibration scheme needs the declared minimum frequency.",new[]{"Vibration.MinimumFrequency","Vibration.Source"},Has("Vibration.Scheme"));
            rules.Add(new EngineParameterRule("ServiceabilityCriterionRequired","Declare at least one criterion with its limit.",null,
                new[]{new[]{"Deflection.Scheme"},new[]{"Drift.Ratio"},new[]{"Vibration.MinimumFrequency"}}));
            return new EngineParameterSchema(SteelMemberServiceabilityChecker.EngineId,fields,rules,new[]{
                "The plan declares each task: Mechanism Serviceability, a serviceability Category, criterion, member axis and the variable-action basis (MemberCheckSpecification.Category/Deformation); the category is never deduced from case names.",
                "Member-node displacements of one confirmed concomitant state per task (N,mm,rad datasets), rotated into the member axes; only exported nodes are covered.",
                "No limit is built in: the values of NTC Tab. 4.2.XII/XIII and of the EC3 national annexes are not in the repository and are always declared with their source.",
                "Supports, cantilever reference length, precamber, mass state and node coverage are declarations checked only for presence and type: their engineering truth stays with the designer."});
        }
        private static EngineParameterSchema Build(bool member)
        {
            var fields=new List<EngineParameterDescriptor>();var rules=new List<EngineParameterRule>();
            var curves=new[]{"A0","A","B","C","D"};
            var general=new[]{"UniformFork","ExternalInPlane"};
            void Add(string key,EngineParameterKind kind=EngineParameterKind.Text,string unit=null,string description=null,
                double? min=null,double? max=null,bool exclusive=false,string[] choices=null,string[] modes=null)
                =>fields.Add(new EngineParameterDescriptor(key,kind,unit,description??key,min,max,exclusive,choices,modes));
            EngineParameterCondition Has(string key)=>new EngineParameterCondition(key);
            EngineParameterCondition Is(string key,string value)=>new EngineParameterCondition(key,value);
            EngineParameterCondition IsNot(string key,string value)=>new EngineParameterCondition(key,value,true);
            EngineParameterCondition Lacks(string key)=>new EngineParameterCondition(key,null,true);
            void Require(string code,string description,string[] keys,params EngineParameterCondition[] when)
                =>rules.Add(new EngineParameterRule(code,description,when,new[]{keys}));
            void Together(string code,params string[] keys) {
                foreach(var key in keys) Require(code,"These values must be supplied together: "+string.Join(", ",keys),keys,Has(key));
            }
            Add("Unperforated",EngineParameterKind.Boolean,description:"Explicit absence/presence of holes; never inferred from the outline.");
            Add("ClassificationSource",description:"Source of the externally supplied nominal section/member class.",modes:member?new[]{"ExternalInPlane","Compression","Bending","BeamColumn"}:null);
            Add("Negligible.Relative",EngineParameterKind.Number,"1",min:0,max:.01,
                description:"Action components up to this fraction of the matching section resistance count as zero; default 1e-6, 0 for exact zero.");
            Add("Negligible.Force",EngineParameterKind.Number,"N",min:0,
                description:"Absolute limit below which N, V1 and V2 count as zero; the larger of this and the relative limit applies; default 0.");
            Add("Negligible.Moment",EngineParameterKind.Number,"Nmm",min:0,
                description:"Absolute limit below which T, M1 and M2 count as zero; the larger of this and the relative limit applies; default 0.");
            if(!member) {
                Add("HWebResistance",EngineParameterKind.Choice,choices:new[]{"NonRigidEndPost"},description:"Web-only H shear buckling resistance; support stiffeners and attachments verified separately.");
                Add("HWebSupportSource",description:"Evidence for support stiffeners, attachments and end force transfer, same section and load state.");
                Together("HWebSupportPair","HWebResistance","HWebSupportSource");
                Require("HWebFingerprintRequired","Web support assessment must bind to the section.",new[]{"SectionFingerprint"},Has("HWebResistance"));
                rules.Add(new EngineParameterRule("HWebAssessmentConflict","Choose exclusion or computed web resistance.",new[]{Has("HWebResistance")},null,new[]{"ShearBucklingExcluded2","ShearBucklingSource2"}));
                Add("NetArea",EngineParameterKind.Number,"mm²",min:0,exclusive:true);
                Add("UniformCompressionClass",EngineParameterKind.Integer,min:1,max:4);
                Add("SectionFingerprint",description:"Fingerprint of the complete Model steel property to which the assessment applies.");
                Together("ClassificationPair","UniformCompressionClass","ClassificationSource");
                for(int axis=1;axis<=2;axis++) {
                    Add("ShearBucklingExcluded"+axis,EngineParameterKind.Boolean);
                    Add("ShearBucklingSource"+axis,description:"Independent assessment for shear direction "+axis+".");
                    Together("ShearAssessmentPair"+axis,"ShearBucklingExcluded"+axis,"ShearBucklingSource"+axis);
                }
                foreach(var key in new[]{"NetArea","UniformCompressionClass","ShearBucklingExcluded1","ShearBucklingExcluded2"})
                    Require("AssessmentFingerprintRequired","Section-specific values require the property fingerprint.",new[]{"SectionFingerprint"},Has(key));
                // Serviceability/StressLimits/Characteristic tasks: explicit NDP and declared applicability, no default.
                Add("ServiceStress.GammaMSer",EngineParameterKind.Number,"1",min:1,
                    description:"gammaM,ser of the service stress limits fy/gammaM,ser, fy/(sqrt3 gammaM,ser), sqrt(sigma^2+3tau^2)<=fy/gammaM,ser (EN 1993-2:2006 7.3(1), NDP, recommended 1.00); no default.");
                Add("ServiceStress.GammaMSerSource",description:"Provenance of gammaM,ser: national annex or project document.");
                Add("ServiceStress.ApplicabilitySource",description:"Why stress limitation applies (EN 1993-2 7.3 bridge, project requirement): EN 1993-1-1 does not require it for buildings and NTC 2018 4.2.4.2.4 names local yielding without a stress limit (EN 1993-2 7.3 under NTC chapter 12). It also covers shear lag and secondary effects, which are not computed.");
                Together("ServiceStressBasis","ServiceStress.GammaMSer","ServiceStress.GammaMSerSource","ServiceStress.ApplicabilitySource");
                Require("NetAreaDeclarationRequired","Net area requires an explicit perforated declaration.",new[]{"Unperforated"},Has("NetArea"));
                rules.Add(new EngineParameterRule("NetAreaRequiresPerforated","Net area cannot accompany an unperforated declaration.",new[]{Is("Unperforated","true")},null,new[]{"NetArea"}));
                return new EngineParameterSchema(SteelPipelineMaterialChecker.EngineId,fields,rules,new[]{
                    "Section geometry, material, steel production type, code edition and concomitant actions are checked at execution.",
                    "Automatic classification/shear assessment is available only for the qualified geometry and production conditions.",
                    "Service stress tasks require result selections declared Characteristic; the ServiceStress values are checked only when such a task runs."});
            }
            Add("AnalysisMode",EngineParameterKind.Choice,choices:new[]{"UniformFork","ExternalInPlane","Compression","Bending","BeamColumn"});
            Add("LoadPatternReference",description:"Source describing the same member's complete load pattern.");
            Add("ParameterSet",description:"Named parameter/national policy; no implicit choice of critical-mode assumptions.");
            Add("MemberFingerprint",description:"Fingerprint of the prepared physical-member snapshot, including the concomitant state.");
            Require("MemberConfigurationRequired","Explicit mode, member identity, hole declaration and analysis provenance are required.",
                new[]{"AnalysisMode","MemberFingerprint","Unperforated","LoadPatternReference","ParameterSet"});
            foreach(var key in new[]{"InPlaneCurve","LateralCurve","LateralTorsionalCurve"})
                Add(key,EngineParameterKind.Choice,choices:key=="LateralTorsionalCurve"?new[]{"A","B","C","D"}:curves,modes:key=="InPlaneCurve"?new[]{"UniformFork"}:general);
            foreach(var key in new[]{"CompressionCurve1","CompressionCurve2","TorsionalCurve"})
                Add(key,EngineParameterKind.Choice,choices:curves,modes:key=="TorsionalCurve"?new[]{"Compression"}:new[]{"Compression","BeamColumn"});
            Add("CurveSelection",EngineParameterKind.Choice,choices:new[]{"Automatic"},modes:new[]{"Compression","BeamColumn"});
            Add("CriticalValues",EngineParameterKind.Choice,choices:new[]{"UniformFork","ForkDiagram"},modes:new[]{"Compression","Bending","BeamColumn"});
            Add("ForkDiagram",EngineParameterKind.Choice,choices:new[]{"EndMoments","UniformTransverseLoad","CentralPointLoad","GenericEnvelope"},modes:new[]{"Bending","BeamColumn"});
            Add("MomentDiagramCoverageSource",description:"Qualification that every boundary, internal extrema and discontinuity face is exported in the same FEM design state; no peak is inferred between samples.",modes:new[]{"Bending","BeamColumn"});
            Require("GenericMomentCoverage","Generic moment envelopes require explicit export coverage.",new[]{"MomentDiagramCoverageSource"},Is("ForkDiagram","GenericEnvelope"));
            Require("GenericFactorCoverage","Generic Cm envelopes require explicit export coverage.",new[]{"MomentDiagramCoverageSource"},Is("MomentFactorPreparation","ConservativeEnvelope"));
            Add("ForkLoadHeight",EngineParameterKind.Number,"mm",description:"Signed load height: positive toward shear centre (destabilizing); explicit zero at shear centre.",modes:new[]{"Bending","BeamColumn"});
            Require("ForkDiagramRequired","Declare the full-span diagram and Mcr basis.",new[]{"ForkDiagram","McrBasis"},Is("CriticalValues","ForkDiagram"));
            foreach(var shape in new[]{"UniformTransverseLoad","CentralPointLoad","GenericEnvelope"})
                Require("ForkLoadHeightRequired","Transverse load height must be explicit.",new[]{"ForkLoadHeight"},Is("ForkDiagram",shape));
            rules.Add(new EngineParameterRule("ForkParametersWithoutPreparation","Fork diagram fields require their preparation method.",new[]{IsNot("CriticalValues","ForkDiagram")},null,new[]{"ForkDiagram","ForkLoadHeight"}));
            rules.Add(new EngineParameterRule("EndMomentHeightConflict","End-moment loading has no transverse force height.",new[]{Is("ForkDiagram","EndMoments")},null,new[]{"ForkLoadHeight"}));
            rules.Add(new EngineParameterRule("CompressionDiagramConflict","ForkDiagram is a bending-only preparation.",new[]{Is("AnalysisMode","Compression"),Is("CriticalValues","ForkDiagram")},null,new[]{"CriticalValues"}));
            Add("CriticalValuesSource",description:"Explicit fork/free-warping declaration of the member ends, no offsets; declared Model restraints split it into fork segments.",modes:new[]{"Compression","Bending","BeamColumn"});
            Together("CriticalPreparationPair","CriticalValues","CriticalValuesSource");
            rules.Add(new EngineParameterRule("AutomaticCurvesConflict","Choose automatic or explicit curves.",new[]{Is("CurveSelection","Automatic")},null,new[]{"CompressionCurve1","CompressionCurve2","TorsionalCurve"}));
            // Explicit NcrT/NcrFT values or exclusions win over the automatic modes and remain admissible;
            // so does the explicit sway Ncr1 of a declared sway plane 1 (the fork spans give nonsway values).
            rules.Add(new EngineParameterRule("AutomaticCriticalConflict","Automatic critical values cannot accompany external Ncr1/Ncr2/Mcr or general-method values.",new[]{Has("CriticalValues")},null,
                new[]{"Ncr2","Mcr","AlphaUltK","AlphaCrOp"}.SelectMany(q=>new[]{q+".Value",q+".Unit",q+".Source"}).ToArray()));
            rules.Add(new EngineParameterRule("AutomaticCriticalConflict","Automatic critical values cannot accompany external Ncr1/Ncr2/Mcr or general-method values.",new[]{Has("CriticalValues"),IsNot("SwayBucklingPlanes","1")},null,
                new[]{"Ncr1.Value","Ncr1.Unit","Ncr1.Source"}));
            Add("MemberClass",EngineParameterKind.Integer,min:1,max:4,modes:new[]{"ExternalInPlane","Compression","Bending","BeamColumn"});
            Together("MemberClassificationPair","MemberClass","ClassificationSource");
            Add("EffectiveSectionAnalysisSource",description:"External characteristic analysis with effective widths, centroid shifts, imperfections and second order; same load pattern as the critical factor.",modes:new[]{"ExternalInPlane"});
            Require("EffectiveExternalAnalysisRequired","Class4 external general method requires effective-section analysis evidence.",new[]{"EffectiveSectionAnalysisSource"},Is("AnalysisMode","ExternalInPlane"),Is("MemberClass","4"));
            Require("EffectiveExternalClassRequired","Effective analysis requires an explicit class4 member.",new[]{"MemberClass","ClassificationSource"},Has("EffectiveSectionAnalysisSource"));
            rules.Add(new EngineParameterRule("EffectiveExternalClassConflict","Effective analysis evidence is only for class4.",new[]{IsNot("MemberClass","4")},null,new[]{"EffectiveSectionAnalysisSource"}));
            foreach(var key in new[]{"TorsionalExclusionSource","FlexuralTorsionalExclusionSource"})
                Add(key,description:"Reasoned exclusion of this critical mode; missing critical data is not an exclusion.",modes:new[]{"Compression","BeamColumn"});
            Add("McrBasis",EngineParameterKind.Choice,choices:new[]{"ActualMomentDiagram","UniformMoment"},modes:new[]{"Bending","BeamColumn"});
            Add("TorsionSusceptible",EngineParameterKind.Boolean,modes:new[]{"BeamColumn"});
            Add("MomentFactorPreparation",EngineParameterKind.Choice,choices:new[]{"LinearDiagrams","ConservativeEnvelope"},description:"Explicit nonsway case (a plane in SwayBucklingPlanes takes Cm=0.9 instead); same full braced span for both bending planes and LTB; validates all exported moments.",modes:new[]{"BeamColumn"});
            rules.Add(new EngineParameterRule("AutomaticMomentFactorsConflict","Choose computed or explicit Cm.",new[]{Has("MomentFactorPreparation")},null,new[]{"Cm1","Cm2","CmLT"}));
            Require("ExplicitMomentFactors","Provide Cm or select declared linear-diagram preparation.",new[]{"Cm1","Cm2","CmLT"},Is("AnalysisMode","BeamColumn"),IsNot("MomentFactorPreparation","LinearDiagrams"),IsNot("MomentFactorPreparation","ConservativeEnvelope"),Lacks("SwayBucklingPlanes"));
            // Sway buckling planes (EN1993-1-1 Table B.3 note; Circolare C4.2.4.1.3.3.2): Cm=0.9 in the declared plane, the
            // other plane and CmLT keep their preparation or explicit value; never inferred, its Ncr must be a sway value.
            Add("SwayBucklingPlanes",EngineParameterKind.Choice,choices:new[]{"1","2","1,2"},modes:new[]{"BeamColumn"},
                description:"Principal bending planes whose relevant flexural buckling mode is a sway mode (1: M1 with Ncr1, 2: M2 with Ncr2): Cm=0.9 there whatever the diagram (EN1993-1-1 Table B.3 note; NTC: Circolare 2019 C4.2.4.1.3.3.2, alpha_my or alpha_mz = 0.9 for a sway mode). Explicit declaration, never inferred; the plane's Ncr must be an explicit sway value or a declared sway effective length.");
            Add("SwayModeSource",modes:new[]{"BeamColumn"},description:"Provenance of the sway classification of the declared planes (global buckling mode, bracing assessment, analysis method).");
            Together("SwayDeclarationPair","SwayBucklingPlanes","SwayModeSource");
            foreach(var planes in new[]{"1","2","1,2"}) {
                bool first=planes!="2",second=planes!="1";
                var explicitKeys=new List<string>{"CmLT"};if(!first)explicitKeys.Add("Cm1");if(!second)explicitKeys.Add("Cm2");
                Require("ExplicitMomentFactors","Provide Cm or select declared linear-diagram preparation.",explicitKeys.ToArray(),Is("AnalysisMode","BeamColumn"),Is("SwayBucklingPlanes",planes),
                    IsNot("MomentFactorPreparation","LinearDiagrams"),IsNot("MomentFactorPreparation","ConservativeEnvelope"));
                rules.Add(new EngineParameterRule("SwayMomentFactorConflict","A declared sway plane takes Cm=0.9: choose computed or explicit moment factors, not both.",
                    new[]{Is("SwayBucklingPlanes",planes)},null,new[]{"Cm1","Cm2"}.Where(k=>k=="Cm1"?first:second).ToArray()));
                if(second)rules.Add(new EngineParameterRule("SwayForkSegmentsConflict","Fork segments fix the lateral translation: a sway plane 2 needs explicit critical values.",
                    new[]{Is("SwayBucklingPlanes",planes)},null,new[]{"CriticalValues"}));
            }
            foreach(var key in new[]{"Cm1","Cm2","CmLT"}) Add(key,EngineParameterKind.Number,"1",min:.4,max:1,modes:new[]{"BeamColumn"});
            foreach(var key in new[]{"MomentFactorSource","StabilityModelSource","LateralTorsionalExclusionSource","ShearBucklingExclusionSource"})
                Add(key,modes:new[]{"BeamColumn"},description:key!="ShearBucklingExclusionSource"?null
                    :"Explicit shear-buckling exclusion; it wins. Without it exact H are screened like the section engine (web EC3 6.2.6(6)/NTC 4.2.27, flanges c/t<=14eps GPC criterion) and RHS with horizontal axis 1 (walls parallel to each shear as webs, same web criteria), CHS by the GPC wall screen d/t<=2(0.0693E/fy)^(2/3) (EN 1993-1-6 D.1.4); CHS beyond that limit and rotated hollow sections require it.");
            // LTB method: General is the default; the rolled method uses the code object's *Mod beta/lambdaLT0 and f from kc.
            var ltbModes=new[]{"Bending","BeamColumn"};var ltbMoment=new[]{"LtbMomentDistribution","LtbPsi","LtbMomentSource"};
            Add("LtbMethod",EngineParameterKind.Choice,choices:new[]{"General","RolledOrEquivalentWelded"},modes:ltbModes,
                description:"LTB reduction: General (EC3 6.3.2.2; NTC f=beta=1, lambdaLT0=0.2; default) or EC3 6.3.2.3 / NTC 4.2.4.1.3.2 for rolled or equivalent welded doubly symmetric I/H, with BetaForLateralTorsionalBucklingMod/LambdaLT0ForLateralTorsionalBucklingMod of the code object.");
            Add("LtbMomentDistribution",EngineParameterKind.Choice,modes:ltbModes,choices:new[]{"Uniform","Linear","SimplySupportedUniformLoad","FixedEndsUniformLoad",
                "OneEndFixedUniformLoad","SimplySupportedCentralLoad","FixedEndsCentralLoad","OneEndFixedCentralLoad"},
                description:"Declared Table 6.6 kc row of the moment diagram between lateral restraints, only without a diagram preparation; otherwise kc=1.");
            Add("LtbPsi",EngineParameterKind.Number,"1",min:-1,max:1,modes:ltbModes,description:"End-moment ratio of the declared linear diagram, smaller/larger end moment with sign.");
            Add("LtbMomentSource",modes:ltbModes,description:"Provenance of the declared moment diagram between lateral restraints, same member and design state.");
            Together("LtbMomentPair","LtbMomentDistribution","LtbMomentSource");
            Require("LtbPsiRequired","A linear kc diagram needs its end-moment ratio.",new[]{"LtbPsi"},Is("LtbMomentDistribution","Linear"));
            rules.Add(new EngineParameterRule("LtbPsiWithoutLinear","Psi belongs to the linear kc row only.",new[]{IsNot("LtbMomentDistribution","Linear")},null,new[]{"LtbPsi"}));
            rules.Add(new EngineParameterRule("LtbMomentWithoutRolledMethod","kc applies only to the rolled or equivalent welded method.",new[]{IsNot("LtbMethod","RolledOrEquivalentWelded")},null,ltbMoment));
            foreach(var key in new[]{"CriticalValues","MomentFactorPreparation"})
                rules.Add(new EngineParameterRule("LtbMomentConflictsWithPreparation","A prepared diagram establishes kc; do not declare another.",new[]{Has(key)},null,ltbMoment));
            // EN 1993-1-1 options of both LTB methods, opt-in; refused under NTC 2018 at execution (Member.LtbCurveFixedByNtc,
            // Member.LtbLowMomentExemptionNotInNtc).
            Add("LtbCurve",EngineParameterKind.Choice,choices:new[]{"A","B","C","D"},modes:ltbModes,
                description:"EN 1993-1-1 only: LTB curve that the National Annex assigns to this member's section, in place of the recommended Table 6.4 (General) or Table 6.5 (RolledOrEquivalentWelded) assignment, NDPs of 6.3.2.2(2)/6.3.2.3(1); also for the beam-column chiLT. alphaLT stays the Table 6.3 value of the curve. Refused under NTC 2018 (curves fixed by Tab. 4.2.IX).");
            Add("LtbCurveSource",modes:ltbModes,description:"National Annex and clause assigning the declared LTB curve to this section family, h/b range and fabrication.");
            Together("LtbCurvePair","LtbCurve","LtbCurveSource");
            Add("LtbImperfectionFactorsSource",modes:ltbModes,
                description:"EN 1993-1-1 only: National Annex clause giving alphaLT per LTB curve (6.3.2.2(2) NOTE, an NDP). With it the code object's AlphaLTImperfectionFactorForCurveA..D replace the recommended Table 6.3 values (0 < alpha <= 1, non-decreasing a to d) for the curve in use, also in the beam-column chiLT; without it they must equal Table 6.3. Refused under NTC 2018 (Tab. 4.2.IX(a)).");
            Add("LtbLowMomentExemption",EngineParameterKind.Boolean,modes:new[]{"Bending"},
                description:"EN 1993-1-1:2005 6.3.2.2(4), adopted with true: for MEd/Mcr <= lambdaLT,0^2 (0.2 General, LambdaLT0ForLateralTorsionalBucklingMod with RolledOrEquivalentWelded) the LTB check is not required; section checks still apply. Bending only (not the beam-column interaction); refused under NTC 2018.");
            Add("LtbLowMomentExemptionSource",modes:new[]{"Bending"},description:"Design-basis decision adopting the EN 1993-1-1:2005 6.3.2.2(4) low-moment criterion for this member.");
            Require("LtbLowMomentExemptionSourceRequired","The adopted low-moment criterion needs its design-basis source.",new[]{"LtbLowMomentExemptionSource"},Is("LtbLowMomentExemption","true"));
            rules.Add(new EngineParameterRule("LtbLowMomentSourceWithoutExemption","The source belongs to an adopted low-moment criterion.",new[]{IsNot("LtbLowMomentExemption","true")},null,new[]{"LtbLowMomentExemptionSource"}));
            Require("UniformForkCurves","The uniform fork method needs its three explicit curves.",new[]{"InPlaneCurve","LateralCurve","LateralTorsionalCurve"},Is("AnalysisMode","UniformFork"));
            Require("GeneralMethodInputs","The external general method requires both multipliers and lateral curves.",
                new[]{"LateralCurve","LateralTorsionalCurve","AlphaUltK.Value","AlphaUltK.Unit","AlphaUltK.Source","AlphaCrOp.Value","AlphaCrOp.Unit","AlphaCrOp.Source"},Is("AnalysisMode","ExternalInPlane"));
            Require("CompressionCurves","Compression needs explicit curves for both flexural axes and the torsional mode.",
                new[]{"CompressionCurve1","CompressionCurve2","TorsionalCurve"},Is("AnalysisMode","Compression"),IsNot("CurveSelection","Automatic"));
            Require("BendingCriticalMoment","Bending requires a sourced critical moment and its basis.",new[]{"Mcr.Value","Mcr.Unit","Mcr.Source","McrBasis"},Is("AnalysisMode","Bending"),IsNot("CriticalValues","UniformFork"),IsNot("CriticalValues","ForkDiagram"));
            Require("AutomaticMomentBasis","Automatic Mcr requires its uniform-moment basis.",new[]{"McrBasis"},Is("AnalysisMode","Bending"),Is("CriticalValues","UniformFork"));
            Require("BeamColumnModel","Beam-column checks require curves, moment factors and the explicit stability model.",
                new[]{"TorsionSusceptible","MomentFactorSource","StabilityModelSource","McrBasis"},Is("AnalysisMode","BeamColumn"));
            Require("BeamColumnCurves","Provide the two flexural curves or select automatic preparation.",new[]{"CompressionCurve1","CompressionCurve2"},Is("AnalysisMode","BeamColumn"),IsNot("CurveSelection","Automatic"));
            Require("LateralModeAssessment","A reasoned exclusion is required when lateral-torsional susceptibility is false.",new[]{"LateralTorsionalExclusionSource"},Is("AnalysisMode","BeamColumn"),Is("TorsionSusceptible","false"));
            rules.Add(new EngineParameterRule("ConflictingLateralMode","Susceptibility and exclusion cannot both be declared.",new[]{Is("TorsionSusceptible","true")},null,new[]{"LateralTorsionalExclusionSource"}));
            foreach(var q in new[]{"Ncr1","Ncr2","NcrT","NcrFT","Mcr","AlphaUltK","AlphaCrOp"}) {
                bool alpha=q.StartsWith("Alpha",StringComparison.Ordinal); bool moment=q=="Mcr";
                var modes=alpha?new[]{"ExternalInPlane"}:moment?new[]{"ExternalInPlane","Bending","BeamColumn"}
                    :new[]{"ExternalInPlane","Compression","BeamColumn"};
                Add(q+".Value",EngineParameterKind.Number,"selected by "+q+".Unit",min:0,exclusive:true,modes:modes);
                Add(q+".Unit",EngineParameterKind.Choice,choices:alpha?new[]{"One"}:moment?new[]{"Nmm","KNm"}:new[]{"N","KN"},modes:modes);
                Add(q+".Source",description:"Provenance of this critical value for the same physical member and load state.",modes:modes);
                Together(q+"Triplet",q+".Value",q+".Unit",q+".Source");
            }
            foreach(var mode in new[]{"T","FT"}) {
                // Without value or exclusion the mode is resolved at execution (doubly symmetric H with torsional
                // restraints, closed hollow) or reported as missing input: like Ncr1/Ncr2 it may come from the model context.
                string source=mode=="T"?"TorsionalExclusionSource":"FlexuralTorsionalExclusionSource";
                foreach(var route in new[]{"Compression","BeamColumn"})
                    rules.Add(new EngineParameterRule("ConflictingCompressionMode"+mode,"A mode cannot be both supplied and excluded.",new[]{Is("AnalysisMode",route),Has(source)},null,new[]{"Ncr"+mode+".Value","Ncr"+mode+".Unit","Ncr"+mode+".Source"}));
            }
            foreach(var key in new[]{"Pitch","Thickness","GussetThickness","ChordCompression","ChordCompressionResistance","ChordTension","ChordTensionResistance","ConnectionDemand","ConnectionResistance"})
                Add("Packed."+key,EngineParameterKind.Number,key=="Pitch"||key.Contains("Thickness")?"mm":"N",min:0,
                    exclusive:key=="Pitch"||key.Contains("Thickness")||key.EndsWith("Resistance",StringComparison.Ordinal),modes:new[]{"Compression"});
            foreach(var key in new[]{"DetailingSource","AnalysisSource","ResistanceSource"}) Add("Packed."+key,modes:new[]{"Compression"});
            Add("Packed.ChordModel",EngineParameterKind.Choice,choices:new[]{"UniformFork"},modes:new[]{"Compression"});
            Add("Packed.ChordModelSource",description:"Qualification of each axial chord panel: centroidal force, no moments/shear, fork/free warping over maximum pitch.",modes:new[]{"Compression"});
            Together("PackedChordModelPair","Packed.ChordModel","Packed.ChordModelSource");
            var commonPacked=fields.Where(f=>f.Key.StartsWith("Packed.",StringComparison.Ordinal)
                &&!new[]{"Packed.ChordCompressionResistance","Packed.ChordTensionResistance","Packed.ChordModel","Packed.ChordModelSource"}.Contains(f.Key)).Select(f=>f.Key).ToArray();
            foreach(var field in fields.Where(f=>f.Key.StartsWith("Packed.",StringComparison.Ordinal)).ToArray()) {
                Require("PackedAssessmentComplete","Complete local envelopes and connection evidence required.",commonPacked,Has(field.Key));
                Require("PackedExternalResistances","External component resistances or explicit computed chord model required.",
                    new[]{"Packed.ChordCompressionResistance","Packed.ChordTensionResistance"},Has(field.Key),IsNot("Packed.ChordModel","UniformFork"));
            }
            rules.Add(new EngineParameterRule("PackedChordResistanceConflict","Choose computed or external component resistances.",new[]{Has("Packed.ChordModel")},null,
                new[]{"Packed.ChordCompressionResistance","Packed.ChordTensionResistance"}));
            return new EngineParameterSchema(SteelMemberPipelineChecker.EngineId,fields,rules,new[]{
                "Ncr1/Ncr2 or explicit effective lengths in MemberDesignContext are required; FEM nodes do not imply restraints.",
                "NcrT/NcrFT: value, reasoned exclusion or automatic resolution (doubly symmetric H with explicit twist restraints, closed hollow sections); otherwise execution reports missing input.",
                "CriticalValues splits the member at declared fork restraints (lateral+twist); partial, one-flange, unknown or phase-dependent restraints are refused.",
                "Beam-column Mcr is required when the active major moment and torsional susceptibility demand it; shear assessments depend on the actual actions.",
                "SwayBucklingPlanes: the Ncr of a declared sway plane comes from an explicit value or a declared MemberDesignContext effective length; with CriticalValues plane 1 needs one of them and plane 2 is refused.",
                "Geometry, full-span actions, norm/edition, mode applicability and connection/detailing qualification remain execution checks.",
                "UniformFork is EC3 only. ExternalInPlane also supports EN6.3.4 under NTC4.2.4.1.3.3 with NTC factors; class4 requires effective-analysis evidence."});
        }
    }
}
