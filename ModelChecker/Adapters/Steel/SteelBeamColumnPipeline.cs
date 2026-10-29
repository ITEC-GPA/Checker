using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
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
            "StabilityModelSource","LateralTorsionalExclusionSource","ShearBucklingExclusionSource","MomentFactorPreparation"};

        private SteelMemberAnalysis BeamColumn(PhysicalMemberCheckInput input,SteelVerificationInput common,SteelPhysicalMemberPreparation physical)
        {
            ValidatePrismaticCoefficients(true,true);
            if(new[]{"InPlaneCurve","LateralCurve","LateralTorsionalCurve","TorsionalCurve"}.Any(_parameters.ContainsKey))
                throw new ArgumentException("CurveParametersForAnotherMemberMethod");
            if(!bool.TryParse(Required("TorsionSusceptible"),out bool susceptible))throw new ArgumentException("InvalidTorsionalSusceptibility");
            var actions=physical.BeamColumnEnvelope();
            common=new SteelVerificationInput(common.Section,actions,common.Code,common.Member);
            var cm=PrepareMomentFactors(input,common,physical,out var momentSource);
            var data=new SteelBeamColumnParameters(CompressionCurve(common,"CompressionCurve1"),CompressionCurve(common,"CompressionCurve2"),
                susceptible,cm[0],cm[1],cm[2],momentSource,Required("StabilityModelSource"),
                Optional("TorsionalExclusionSource"),Optional("FlexuralTorsionalExclusionSource"),EnumValue<SteelCriticalMomentBasis>("McrBasis"),
                _parameters.TryGetValue("LateralTorsionalExclusionSource",out var excludeLT)?excludeLT:null,
                _parameters.TryGetValue("ShearBucklingExclusionSource",out var excludeV)?excludeV:null);
            int? memberClass=null;
            if(_parameters.ContainsKey("MemberClass")) {
                if(!int.TryParse(Required("MemberClass"),NumberStyles.None,CultureInfo.InvariantCulture,out int value))throw new ArgumentException("InvalidMemberClass");
                memberClass=value;
            }
            var values=new List<SteelStabilityValue>();
            foreach(SteelStabilityQuantity q in Enum.GetValues(typeof(SteelStabilityQuantity)))
                if(new[]{".Value",".Unit",".Source"}.Any(s=>_parameters.ContainsKey(q+s)))
                    values.Add(new SteelStabilityValue(q,Number(q+".Value"),EnumValue<SteelStabilityUnit>(q+".Unit"),Required(q+".Source")));
            values=PrepareBeamColumnCriticalValues(input,common,physical,susceptible,values).ToList();
            return SteelMemberAnalysis.BeamColumn(common,Required("LoadPatternReference"),PreparedParameterSet(common),data,values,memberClass,
                _parameters.TryGetValue("ClassificationSource",out var source)?source:null);
        }

        // Mirrors the section engine (SteelPipelineMaterialChecker.Assessments) when the beam-column waits for shear
        // assessments, i.e. exact H or RHS without an explicit ShearBucklingExclusionSource. A negative one leaves it unevaluated.
        private IEnumerable<SteelShearBucklingAssessment> BeamColumnShearAssessments(SteelSelectionPlan plan)
        {
            var input=plan.Input;
            if(!plan.Decisions.Any(d=>d.Status==SteelSelectionStatus.RequiresPreparation))yield break;
            if(SteelPipelineMaterialChecker.ExactRhs(input))
            {
                // RHS walls parallel to each active shear as webs, as on the section route.
                for(int axis=1;axis<=2;axis++)
                    if((axis==1?input.EffectiveActions.V1:input.EffectiveActions.V2)!=0)yield return SteelPipelineMaterialChecker.RhsWebs(input,axis,Code);
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
            // fields: neither is a reason to change these fields. Other alphaLT values per curve have no qualified route here.
            if(bending&&(Code.AlphaLTImperfectionFactorForCurveA!=.21||Code.AlphaLTImperfectionFactorForCurveB!=.34
                ||Code.AlphaLTImperfectionFactorForCurveC!=.49||Code.AlphaLTImperfectionFactorForCurveD!=.76))
                throw new NotSupportedException("LtbImperfectionFactorsNotQualified: the code object's alphaLT per LTB curve differ from EN 1993-1-1:2005 Table 6.3"
                    +" (same values in NTC 2018): a 0.21, b 0.34, c 0.49, d 0.76, the only values of this LTB route. A national curve assignment (EC3 Tables 6.4/6.5)"
                    +" is declared with LtbCurve and LtbCurveSource and keeps the Table 6.3 alphaLT of the declared curve; beta and lambdaLT,0 of 6.3.2.3 come from"
                    +" BetaForLateralTorsionalBucklingMod/LambdaLT0ForLateralTorsionalBucklingMod with LtbMethod=RolledOrEquivalentWelded.");
            if(bending&&(Code.BetaForLateralTorsionalBuckling!=1||Code.LambdaLT0ForLateralTorsionalBuckling!=.2))
                throw new NotSupportedException("LtbGeneralParametersNotQualified: the general case (EN 1993-1-1:2005 6.3.2.2 equation 6.56; NTC 2018 4.2.4.1.3.2 with f = 1)"
                    +" has beta = 1 and lambdaLT,0 = 0.2 (BetaForLateralTorsionalBuckling, LambdaLT0ForLateralTorsionalBuckling). National beta and lambdaLT,0 belong to"
                    +" EC3 6.3.2.3: LtbMethod=RolledOrEquivalentWelded with the *Mod fields.");
        }
    }
}
