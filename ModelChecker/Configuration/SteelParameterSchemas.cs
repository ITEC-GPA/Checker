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
            void Require(string code,string description,string[] keys,params EngineParameterCondition[] when)
                =>rules.Add(new EngineParameterRule(code,description,when,new[]{keys}));
            void Together(string code,params string[] keys) {
                foreach(var key in keys) Require(code,"These values must be supplied together: "+string.Join(", ",keys),keys,Has(key));
            }
            Add("Unperforated",EngineParameterKind.Boolean,description:"Explicit absence/presence of holes; never inferred from the outline.");
            Add("ClassificationSource",description:"Source of the externally supplied nominal section/member class.",modes:member?new[]{"ExternalInPlane","Compression","Bending","BeamColumn"}:null);
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
                Require("NetAreaDeclarationRequired","Net area requires an explicit perforated declaration.",new[]{"Unperforated"},Has("NetArea"));
                rules.Add(new EngineParameterRule("NetAreaRequiresPerforated","Net area cannot accompany an unperforated declaration.",new[]{Is("Unperforated","true")},null,new[]{"NetArea"}));
                return new EngineParameterSchema(SteelPipelineMaterialChecker.EngineId,fields,rules,new[]{
                    "Section geometry, material, steel production type, code edition and concomitant actions are checked at execution.",
                    "Automatic classification/shear assessment is available only for the qualified geometry and production conditions."});
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
            Add("CriticalValues",EngineParameterKind.Choice,choices:new[]{"UniformFork","ForkDiagram"},modes:new[]{"Compression","Bending"});
            Add("ForkDiagram",EngineParameterKind.Choice,choices:new[]{"EndMoments","UniformTransverseLoad","CentralPointLoad","GenericEnvelope"},modes:new[]{"Bending"});
            Add("MomentDiagramCoverageSource",description:"Qualification that every boundary, internal extrema and discontinuity face is exported in the same FEM design state; no peak is inferred between samples.",modes:new[]{"Bending","BeamColumn"});
            Require("GenericMomentCoverage","Generic moment envelopes require explicit export coverage.",new[]{"MomentDiagramCoverageSource"},Is("ForkDiagram","GenericEnvelope"));
            Require("GenericFactorCoverage","Generic Cm envelopes require explicit export coverage.",new[]{"MomentDiagramCoverageSource"},Is("MomentFactorPreparation","ConservativeEnvelope"));
            Add("ForkLoadHeight",EngineParameterKind.Number,"mm",description:"Signed load height: positive toward shear centre (destabilizing); explicit zero at shear centre.",modes:new[]{"Bending"});
            Require("ForkDiagramRequired","Declare the full-span diagram and Mcr basis.",new[]{"ForkDiagram","McrBasis"},Is("CriticalValues","ForkDiagram"));
            foreach(var shape in new[]{"UniformTransverseLoad","CentralPointLoad","GenericEnvelope"})
                Require("ForkLoadHeightRequired","Transverse load height must be explicit.",new[]{"ForkLoadHeight"},Is("ForkDiagram",shape));
            rules.Add(new EngineParameterRule("ForkParametersWithoutPreparation","Fork diagram fields require their preparation method.",new[]{IsNot("CriticalValues","ForkDiagram")},null,new[]{"ForkDiagram","ForkLoadHeight"}));
            rules.Add(new EngineParameterRule("EndMomentHeightConflict","End-moment loading has no transverse force height.",new[]{Is("ForkDiagram","EndMoments")},null,new[]{"ForkLoadHeight"}));
            rules.Add(new EngineParameterRule("CompressionDiagramConflict","ForkDiagram is a bending-only preparation.",new[]{Is("AnalysisMode","Compression"),Is("CriticalValues","ForkDiagram")},null,new[]{"CriticalValues"}));
            Add("CriticalValuesSource",description:"Explicit full-span fork/free-warping declaration, no offsets or intermediate restraints.",modes:new[]{"Compression","Bending"});
            Together("CriticalPreparationPair","CriticalValues","CriticalValuesSource");
            rules.Add(new EngineParameterRule("AutomaticCurvesConflict","Choose automatic or explicit curves.",new[]{Is("CurveSelection","Automatic")},null,new[]{"CompressionCurve1","CompressionCurve2","TorsionalCurve"}));
            rules.Add(new EngineParameterRule("AutomaticCriticalConflict","Automatic critical values cannot accompany external values or mode exclusions.",new[]{Has("CriticalValues")},null,
                new[]{"Ncr1","Ncr2","NcrT","NcrFT","Mcr","AlphaUltK","AlphaCrOp"}.SelectMany(q=>new[]{q+".Value",q+".Unit",q+".Source"}).Concat(new[]{"TorsionalExclusionSource","FlexuralTorsionalExclusionSource"})));
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
            Add("MomentFactorPreparation",EngineParameterKind.Choice,choices:new[]{"LinearDiagrams","ConservativeEnvelope"},description:"Explicit nonsway case; same full braced span for both bending planes and LTB; validates all exported moments.",modes:new[]{"BeamColumn"});
            rules.Add(new EngineParameterRule("AutomaticMomentFactorsConflict","Choose computed or explicit Cm.",new[]{Has("MomentFactorPreparation")},null,new[]{"Cm1","Cm2","CmLT"}));
            Require("ExplicitMomentFactors","Provide Cm or select declared linear-diagram preparation.",new[]{"Cm1","Cm2","CmLT"},Is("AnalysisMode","BeamColumn"),IsNot("MomentFactorPreparation","LinearDiagrams"),IsNot("MomentFactorPreparation","ConservativeEnvelope"));
            foreach(var key in new[]{"Cm1","Cm2","CmLT"}) Add(key,EngineParameterKind.Number,"1",min:.4,max:1,modes:new[]{"BeamColumn"});
            foreach(var key in new[]{"MomentFactorSource","StabilityModelSource","LateralTorsionalExclusionSource","ShearBucklingExclusionSource"})
                Add(key,modes:new[]{"BeamColumn"});
            Require("UniformForkCurves","The uniform fork method needs its three explicit curves.",new[]{"InPlaneCurve","LateralCurve","LateralTorsionalCurve"},Is("AnalysisMode","UniformFork"));
            Require("GeneralMethodInputs","The external general method requires both multipliers and lateral curves.",
                new[]{"LateralCurve","LateralTorsionalCurve","AlphaUltK.Value","AlphaUltK.Unit","AlphaUltK.Source","AlphaCrOp.Value","AlphaCrOp.Unit","AlphaCrOp.Source"},Is("AnalysisMode","ExternalInPlane"));
            Require("CompressionCurves","Compression needs explicit curves for both flexural axes and the torsional mode.",
                new[]{"CompressionCurve1","CompressionCurve2","TorsionalCurve"},Is("AnalysisMode","Compression"),IsNot("CurveSelection","Automatic"));
            Require("BendingCriticalMoment","Bending requires a sourced critical moment and its basis.",new[]{"Mcr.Value","Mcr.Unit","Mcr.Source","McrBasis"},Is("AnalysisMode","Bending"),IsNot("CriticalValues","UniformFork"),IsNot("CriticalValues","ForkDiagram"));
            Require("AutomaticMomentBasis","Automatic Mcr requires its uniform-moment basis.",new[]{"McrBasis"},Is("AnalysisMode","Bending"),Is("CriticalValues","UniformFork"));
            Require("BeamColumnModel","Beam-column checks require curves, moment factors and the explicit stability model.",
                new[]{"TorsionSusceptible","MomentFactorSource","StabilityModelSource", "TorsionalExclusionSource","FlexuralTorsionalExclusionSource","McrBasis"},Is("AnalysisMode","BeamColumn"));
            Require("BeamColumnCurves","Provide the two flexural curves or select automatic preparation.",new[]{"CompressionCurve1","CompressionCurve2"},Is("AnalysisMode","BeamColumn"),IsNot("CurveSelection","Automatic"));
            Require("LateralModeAssessment","A reasoned exclusion is required when lateral-torsional susceptibility is false.",new[]{"LateralTorsionalExclusionSource"},Is("AnalysisMode","BeamColumn"),Is("TorsionSusceptible","false"));
            rules.Add(new EngineParameterRule("ConflictingLateralMode","Susceptibility and exclusion cannot both be declared.",new[]{Is("TorsionSusceptible","true")},null,new[]{"LateralTorsionalExclusionSource"}));
            foreach(var q in new[]{"Ncr1","Ncr2","NcrT","NcrFT","Mcr","AlphaUltK","AlphaCrOp"}) {
                bool alpha=q.StartsWith("Alpha",StringComparison.Ordinal); bool moment=q=="Mcr";
                var modes=alpha?new[]{"ExternalInPlane"}:moment?new[]{"ExternalInPlane","Bending","BeamColumn"}
                    :(q=="NcrT"||q=="NcrFT")?new[]{"ExternalInPlane","Compression"}:new[]{"ExternalInPlane","Compression","BeamColumn"};
                Add(q+".Value",EngineParameterKind.Number,"selected by "+q+".Unit",min:0,exclusive:true,modes:modes);
                Add(q+".Unit",EngineParameterKind.Choice,choices:alpha?new[]{"One"}:moment?new[]{"Nmm","KNm"}:new[]{"N","KN"},modes:modes);
                Add(q+".Source",description:"Provenance of this critical value for the same physical member and load state.",modes:modes);
                Together(q+"Triplet",q+".Value",q+".Unit",q+".Source");
            }
            foreach(var mode in new[]{"T","FT"}) {
                string source=mode=="T"?"TorsionalExclusionSource":"FlexuralTorsionalExclusionSource";
                rules.Add(new EngineParameterRule("CompressionMode"+mode,"Supply this critical mode or a reasoned exclusion.",new[]{Is("AnalysisMode","Compression"),IsNot("CriticalValues","UniformFork")},
                    new[]{new[]{"Ncr"+mode+".Value","Ncr"+mode+".Unit","Ncr"+mode+".Source"},new[]{source}}));
                rules.Add(new EngineParameterRule("ConflictingCompressionMode"+mode,"A mode cannot be both supplied and excluded.",new[]{Is("AnalysisMode","Compression"),Has(source)},null,new[]{"Ncr"+mode+".Value","Ncr"+mode+".Unit","Ncr"+mode+".Source"}));
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
                "Beam-column Mcr is required when the active major moment and torsional susceptibility demand it; shear assessments depend on the actual actions.",
                "Geometry, full-span actions, norm/edition, mode applicability and connection/detailing qualification remain execution checks.",
                "UniformFork is EC3 only. ExternalInPlane also supports EN6.3.4 under NTC4.2.4.1.3.3 with NTC factors; class4 requires effective-analysis evidence."});
        }
    }
}
