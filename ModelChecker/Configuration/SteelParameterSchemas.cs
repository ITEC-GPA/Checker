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
            void Require(string code,string description,string[] keys,params EngineParameterCondition[] when)
                =>rules.Add(new EngineParameterRule(code,description,when,new[]{keys}));
            void Together(string code,params string[] keys) {
                foreach(var key in keys) Require(code,"These values must be supplied together: "+string.Join(", ",keys),keys,Has(key));
            }
            Add("Unperforated",EngineParameterKind.Boolean,description:"Explicit absence/presence of holes; never inferred from the outline.");
            Add("ClassificationSource",description:"Source of the externally supplied nominal section/member class.",modes:member?new[]{"ExternalInPlane","Compression","Bending","BeamColumn"}:null);
            if(!member) {
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
            Add("MemberClass",EngineParameterKind.Integer,min:1,max:4,modes:new[]{"ExternalInPlane","Compression","Bending","BeamColumn"});
            Together("MemberClassificationPair","MemberClass","ClassificationSource");
            foreach(var key in new[]{"TorsionalExclusionSource","FlexuralTorsionalExclusionSource"})
                Add(key,description:"Reasoned exclusion of this critical mode; missing critical data is not an exclusion.",modes:new[]{"Compression","BeamColumn"});
            Add("McrBasis",EngineParameterKind.Choice,choices:new[]{"ActualMomentDiagram","UniformMoment"},modes:new[]{"Bending","BeamColumn"});
            Add("TorsionSusceptible",EngineParameterKind.Boolean,modes:new[]{"BeamColumn"});
            foreach(var key in new[]{"Cm1","Cm2","CmLT"}) Add(key,EngineParameterKind.Number,"1",min:.4,max:1,modes:new[]{"BeamColumn"});
            foreach(var key in new[]{"MomentFactorSource","StabilityModelSource","LateralTorsionalExclusionSource","ShearBucklingExclusionSource"})
                Add(key,modes:new[]{"BeamColumn"});
            Require("UniformForkCurves","The uniform fork method needs its three explicit curves.",new[]{"InPlaneCurve","LateralCurve","LateralTorsionalCurve"},Is("AnalysisMode","UniformFork"));
            Require("GeneralMethodInputs","The external general method requires both multipliers and lateral curves.",
                new[]{"LateralCurve","LateralTorsionalCurve","AlphaUltK.Value","AlphaUltK.Unit","AlphaUltK.Source","AlphaCrOp.Value","AlphaCrOp.Unit","AlphaCrOp.Source"},Is("AnalysisMode","ExternalInPlane"));
            Require("CompressionCurves","Compression needs explicit curves for both flexural axes and the torsional mode.",
                new[]{"CompressionCurve1","CompressionCurve2","TorsionalCurve"},Is("AnalysisMode","Compression"));
            Require("BendingCriticalMoment","Bending requires a sourced critical moment and its basis.",new[]{"Mcr.Value","Mcr.Unit","Mcr.Source","McrBasis"},Is("AnalysisMode","Bending"));
            Require("BeamColumnModel","Beam-column checks require curves, moment factors and the explicit stability model.",
                new[]{"CompressionCurve1","CompressionCurve2","TorsionSusceptible","Cm1","Cm2","CmLT","MomentFactorSource","StabilityModelSource", "TorsionalExclusionSource","FlexuralTorsionalExclusionSource","McrBasis"},Is("AnalysisMode","BeamColumn"));
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
                rules.Add(new EngineParameterRule("CompressionMode"+mode,"Supply this critical mode or a reasoned exclusion.",new[]{Is("AnalysisMode","Compression")},
                    new[]{new[]{"Ncr"+mode+".Value","Ncr"+mode+".Unit","Ncr"+mode+".Source"},new[]{source}}));
                rules.Add(new EngineParameterRule("ConflictingCompressionMode"+mode,"A mode cannot be both supplied and excluded.",new[]{Is("AnalysisMode","Compression"),Has(source)},null,new[]{"Ncr"+mode+".Value","Ncr"+mode+".Unit","Ncr"+mode+".Source"}));
            }
            foreach(var key in new[]{"Pitch","Thickness","GussetThickness","ChordCompression","ChordCompressionResistance","ChordTension","ChordTensionResistance","ConnectionDemand","ConnectionResistance"})
                Add("Packed."+key,EngineParameterKind.Number,key=="Pitch"||key.Contains("Thickness")?"mm":"N",min:0,
                    exclusive:key=="Pitch"||key.Contains("Thickness")||key.EndsWith("Resistance",StringComparison.Ordinal),modes:new[]{"Compression"});
            foreach(var key in new[]{"DetailingSource","AnalysisSource","ResistanceSource"}) Add("Packed."+key,modes:new[]{"Compression"});
            Together("PackedAssessmentComplete",fields.Where(f=>f.Key.StartsWith("Packed.",StringComparison.Ordinal)).Select(f=>f.Key).ToArray());
            return new EngineParameterSchema(SteelMemberPipelineChecker.EngineId,fields,rules,new[]{
                "Ncr1/Ncr2 or explicit effective lengths in MemberDesignContext are required; FEM nodes do not imply restraints.",
                "Beam-column Mcr is required when the active major moment and torsional susceptibility demand it; shear assessments depend on the actual actions.",
                "Geometry, full-span actions, norm/edition, mode applicability and connection/detailing qualification remain execution checks.",
                "UniformFork/ExternalInPlane are qualified only for EC3; prismatic modes also expose the distinct NTC policy."});
        }
    }
}
