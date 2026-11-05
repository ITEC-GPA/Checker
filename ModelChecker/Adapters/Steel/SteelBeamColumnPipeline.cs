using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Checking.Contracts;
using GPC.Model.Sections;

namespace GPC.Model.Checker
{
    public sealed partial class SteelMemberPipelineChecker
    {
        private static readonly string[] BeamColumnKeys={"TorsionSusceptible","Cm1","Cm2","CmLT","MomentFactorSource",
            "StabilityModelSource","LateralTorsionalExclusionSource","ShearBucklingExclusionSource","MomentFactorPreparation",
            "SwayBucklingPlanes","SwayModeSource","AxialForcePreparation","AxialForceCoverageSource","HighShearMethod","HWebSupportSource"};

        private SteelMemberAnalysis BeamColumn(PhysicalMemberCheckInput input,SteelVerificationInput common,SteelPhysicalMemberPreparation physical)
        {
            ValidatePrismaticCoefficients(true,true);
            if(new[]{"InPlaneCurve","LateralCurve","LateralTorsionalCurve","TorsionalCurve"}.Any(_parameters.ContainsKey))
                throw new ArgumentException("CurveParametersForAnotherMemberMethod");
            if(!bool.TryParse(Required("TorsionSusceptible"),out bool susceptible))throw new ArgumentException("InvalidTorsionalSusceptibility");
            bool axialEnvelope=ValidateAxialForceEnvelope(common,out int? axialClass);
            var actions=physical.BeamColumnEnvelope(axialEnvelope,Optional("AxialForceCoverageSource"));
            common=new SteelVerificationInput(common.Section,actions,common.Code,common.Member);
            var cm=PrepareMomentFactors(input,common,physical,out var momentSource);
            var data=new SteelBeamColumnParameters(CompressionCurve(common,"CompressionCurve1"),CompressionCurve(common,"CompressionCurve2"),
                susceptible,cm[0],cm[1],cm[2],momentSource,Required("StabilityModelSource"),
                Optional("TorsionalExclusionSource"),Optional("FlexuralTorsionalExclusionSource"),EnumValue<SteelCriticalMomentBasis>("McrBasis"),
                _parameters.TryGetValue("LateralTorsionalExclusionSource",out var excludeLT)?excludeLT:null,
                _parameters.TryGetValue("ShearBucklingExclusionSource",out var excludeV)?excludeV:null);
            if(_parameters.TryGetValue("HighShearMethod",out var highShear))
            {
                if(highShear=="ConservativeUniformReduction")data=data.WithConservativeHighShear();
                else if(highShear=="ConservativeShearSuperposition")data=data.WithConservativeShearSuperposition();
                else if(highShear=="ConservativeClass4WebInteraction")data=data.WithConservativeClass4WebInteraction();
                else throw new ArgumentException("UnsupportedHighShearMethod");
            }
            int? memberClass=axialClass;
            if(_parameters.ContainsKey("MemberClass")) {
                if(!int.TryParse(Required("MemberClass"),NumberStyles.None,CultureInfo.InvariantCulture,out int value))throw new ArgumentException("InvalidMemberClass");
                memberClass=value;
            }
            var values=new List<SteelStabilityValue>();
            foreach(SteelStabilityQuantity q in Enum.GetValues(typeof(SteelStabilityQuantity)))
                if(new[]{".Value",".Unit",".Source"}.Any(s=>_parameters.ContainsKey(q+s)))
                    values.Add(new SteelStabilityValue(q,Number(q+".Value"),EnumValue<SteelStabilityUnit>(q+".Unit"),Required(q+".Source")));
            values=PrepareBeamColumnCriticalValues(input,common,physical,susceptible,values).ToList();
            if(SwayPlane(1)||SwayPlane(2))TraceSwayCriticalForces(input,common,values,physical);
            return SteelMemberAnalysis.BeamColumn(common,Required("LoadPatternReference"),PreparedParameterSet(common),data,values,memberClass,
                axialEnvelope?"Uniform-compression class of the actual I/H section; no N-M relief in the conservative variable-N envelope"
                    :_parameters.TryGetValue("ClassificationSource",out var source)?source:null);
        }

        // A model envelope, not a new Annex-B formula: Nmax is no smaller than the
        // compression at any cut. Constant-section geometric stiffness is therefore
        // bounded by the uniform-N model. Require the exported extrema, conservative
        // moments and Euler critical forces of that model; never reuse an eigenvalue
        // normalized to a different variable-N load pattern.
        private bool ValidateAxialForceEnvelope(SteelVerificationInput input,out int? memberClass)
        {
            memberClass=null;
            if(!_parameters.TryGetValue("AxialForcePreparation",out var method)) {
                if(_parameters.ContainsKey("AxialForceCoverageSource"))throw new ArgumentException("AxialForceSourceWithoutPreparation");
                return false;
            }
            if(method!="UniformCompressionEnvelope")throw new ArgumentException("UnsupportedAxialForcePreparation");
            if(string.IsNullOrWhiteSpace(Required("AxialForceCoverageSource")))throw new ArgumentException("AxialForceExtremaCoverageRequired");
            if(Optional("MomentFactorPreparation")!="ConservativeEnvelope"||Optional("McrBasis")!="UniformMoment"
                ||_parameters.ContainsKey("SwayBucklingPlanes")||new[]{"Cm1","Cm2","CmLT"}.Any(_parameters.ContainsKey))
                throw new NotSupportedException("UniformCompressionEnvelopeRequiresNonswayCmOneAndUniformMcr");
            if(_parameters.ContainsKey("MemberClass")||_parameters.ContainsKey("ClassificationSource"))
                throw new ArgumentException("UniformCompressionEnvelopeComputesItsOwnUniformClass");
            if(new[]{"Ncr1","Ncr2","NcrT","NcrFT"}.Any(q=>new[]{".Value",".Unit",".Source"}.Any(s=>_parameters.ContainsKey(q+s)))
                ||_parameters.TryGetValue("CriticalValues",out var critical)&&critical!="UniformFork")
                throw new NotSupportedException("UniformCompressionEnvelopeRequiresUniformEulerCriticalForces");
            var section=input.Section.CreateSectionCopy();var h=section.SectionShape as SectionH;
            if(h is null||h.GetType()!=typeof(SectionH)||h.LenghtTopFlange!=h.LenghtBottomFlange||h.ThicknessTopFlange!=h.ThicknessBottomFlange)
                throw new NotSupportedException("UniformCompressionEnvelopeRequiresDoublySymmetricIH");
            var geometry=ModelCompressionGeometry.Create(section);
            var classification=input.Code.SectionCode.Standard==SectionResistanceStandard.Ntc2018
                ?Ntc2018CompressionClassification.Evaluate(geometry,input.Section.YieldStrength)
                :Ec3UniformCompressionClassification.Evaluate(geometry,input.Section.YieldStrength);
            if(!classification.IsEvaluated||classification.NominalClass>3)
                throw new NotSupportedException("UniformCompressionEnvelopeRequiresUniformClass1To3");
            memberClass=classification.NominalClass;
            return true;
        }

        // Mirrors the section engine (SteelPipelineMaterialChecker.Assessments) when the beam-column waits for shear
        // assessments, i.e. exact H, RHS or CHS (within its wall screen) without an explicit ShearBucklingExclusionSource.
        // A negative one leaves it unevaluated.
        private IEnumerable<SteelShearBucklingAssessment> BeamColumnShearAssessments(SteelSelectionPlan plan)
        {
            var input=plan.Input;
            if(!plan.Decisions.Any(d=>d.Status==SteelSelectionStatus.RequiresPreparation))yield break;
            if(plan.MemberAnalysis?.BeamColumnParameters?.ConservativeClass4WebInteraction==true)
            {
                double eta=input.Code.SectionCode.Standard==SectionResistanceStandard.Ntc2018 ? 1 : input.Section.YieldStrength<=460
                    ? Code.NShearBucklingLowGradeOfSteel : Code.NShearBucklingHighGradeOfSteel;
                yield return SteelShearBucklingAssessment.EvaluateNonRigidHWeb(input,Code.GammaM1,eta,Required("HWebSupportSource"))
                    .WithConservativeInPlaneNormalInteraction();
                yield break;
            }
            if(SteelPipelineMaterialChecker.ExactRhs(input))
            {
                // RHS walls parallel to each active shear as webs, as on the section route.
                for(int axis=1;axis<=2;axis++)
                    if((axis==1?input.EffectiveActions.V1:input.EffectiveActions.V2)!=0)yield return SteelPipelineMaterialChecker.RhsWebs(input,axis,Code);
                yield break;
            }
            if(SteelPipelineMaterialChecker.ExactChs(input))
            {
                // CHS wall screen for each active shear; the selector waits for it only when the screen passes.
                for(int axis=1;axis<=2;axis++)
                    if((axis==1?input.EffectiveActions.V1:input.EffectiveActions.V2)!=0)yield return SteelPipelineMaterialChecker.ChsWall(input,axis);
                yield break;
            }
            if(input.Section.GeometryType!=typeof(SectionH)||Math.Abs(input.Section.PrincipalAxisAngle)>1e-10)yield break;
            if(input.EffectiveActions.V1!=0)
                yield return SteelShearBucklingAssessment.EvaluateHFlanges(input,"GPC automatic flange outstand screen c/t<=14eps for V1 (not a code clause)");
            if(input.EffectiveActions.V2==0)yield break;
            if(input.Code.SectionCode.Standard==SectionResistanceStandard.Ntc2018)
            { yield return SteelShearBucklingAssessment.EvaluateNtcHWeb(input,"NTC 2018 equation4.2.27 conservative eta=1");yield break; }
            bool lowGrade=input.Section.YieldStrength<=460;
            yield return SteelShearBucklingAssessment.EvaluateEc3HWeb(input,lowGrade?Code.NShearBucklingLowGradeOfSteel:Code.NShearBucklingHighGradeOfSteel,
                "StandardEN1993p11."+(lowGrade?"NShearBucklingLowGradeOfSteel":"NShearBucklingHighGradeOfSteel"));
        }
        // Null: the library resolves the mode from a value or automatically (doubly symmetric H, closed hollow).
        private string Optional(string key)=>_parameters.TryGetValue(key,out var value)?value:null;

        private void ValidatePrismaticCoefficients(bool compression,bool bending)
        {
            if(compression&&(Code.AlphaImperfectionFactorForCurveA0!=.13||Code.AlphaImperfectionFactorForCurveA!=.21
                ||Code.AlphaImperfectionFactorForCurveB!=.34||Code.AlphaImperfectionFactorForCurveC!=.49||Code.AlphaImperfectionFactorForCurveD!=.76))
                throw new NotSupportedException("Prismatic compression requires the qualified standard imperfection factors.");
            // The curve assignment is declared with LtbCurve/LtbCurveSource and the 6.3.2.3 beta/lambdaLT0 come from the *Mod
            // fields: neither is a reason to change these fields. National alphaLT per curve (EC3 6.3.2.2(2) NOTE, an NDP) are
            // admitted only with LtbImperfectionFactorsSource; the library validates them and refuses them under NTC 2018.
            if(bending&&!_parameters.ContainsKey("LtbImperfectionFactorsSource")&&(Code.AlphaLTImperfectionFactorForCurveA!=.21||Code.AlphaLTImperfectionFactorForCurveB!=.34
                ||Code.AlphaLTImperfectionFactorForCurveC!=.49||Code.AlphaLTImperfectionFactorForCurveD!=.76))
                throw new NotSupportedException("LtbImperfectionFactorsNotQualified: the code object's alphaLT per LTB curve differ from EN 1993-1-1:2005 Table 6.3"
                    +" (same values in NTC 2018): a 0.21, b 0.34, c 0.49, d 0.76. National values (EC3 6.3.2.2(2) NOTE, an NDP) need LtbImperfectionFactorsSource"
                    +" with the National Annex clause (EN 1993-1-1 only). A national curve assignment (EC3 Tables 6.4/6.5)"
                    +" is declared with LtbCurve and LtbCurveSource; beta and lambdaLT,0 of 6.3.2.3 come from"
                    +" BetaForLateralTorsionalBucklingMod/LambdaLT0ForLateralTorsionalBucklingMod with LtbMethod=RolledOrEquivalentWelded.");
            if(bending&&(Code.BetaForLateralTorsionalBuckling!=1||Code.LambdaLT0ForLateralTorsionalBuckling!=.2))
                throw new NotSupportedException("LtbGeneralParametersNotQualified: the general case (EN 1993-1-1:2005 6.3.2.2 equation 6.56; NTC 2018 4.2.4.1.3.2 with f = 1)"
                    +" has beta = 1 and lambdaLT,0 = 0.2 (BetaForLateralTorsionalBuckling, LambdaLT0ForLateralTorsionalBuckling). National beta and lambdaLT,0 belong to"
                    +" EC3 6.3.2.3: LtbMethod=RolledOrEquivalentWelded with the *Mod fields.");
        }
    }
}
