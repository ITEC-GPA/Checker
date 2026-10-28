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
                Required("TorsionalExclusionSource"),Required("FlexuralTorsionalExclusionSource"),EnumValue<SteelCriticalMomentBasis>("McrBasis"),
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
            return SteelMemberAnalysis.BeamColumn(common,Required("LoadPatternReference"),PreparedParameterSet(common),data,values,memberClass,
                _parameters.TryGetValue("ClassificationSource",out var source)?source:null);
        }

        // Mirrors the section engine (SteelPipelineMaterialChecker.Assessments) when the beam-column waits for shear
        // assessments, i.e. exact H without an explicit ShearBucklingExclusionSource. A negative one leaves it unevaluated.
        private IEnumerable<SteelShearBucklingAssessment> BeamColumnShearAssessments(SteelSelectionPlan plan)
        {
            var input=plan.Input;
            if(!plan.Decisions.Any(d=>d.Status==SteelSelectionStatus.RequiresPreparation)
                ||input.Section.GeometryType!=typeof(SectionH)||Math.Abs(input.Section.PrincipalAxisAngle)>1e-10)yield break;
            if(input.EffectiveActions.V1!=0)
                yield return SteelShearBucklingAssessment.EvaluateHFlanges(input,"GPC automatic flange outstand screen c/t<=14eps for V1 (not a code clause)");
            if(input.EffectiveActions.V2==0)yield break;
            if(input.Code.SectionCode.Standard==SectionResistanceStandard.Ntc2018)
            { yield return SteelShearBucklingAssessment.EvaluateNtcHWeb(input,"NTC 2018 equation4.2.27 conservative eta=1");yield break; }
            bool lowGrade=input.Section.YieldStrength<=460;
            yield return SteelShearBucklingAssessment.EvaluateEc3HWeb(input,lowGrade?Code.NShearBucklingLowGradeOfSteel:Code.NShearBucklingHighGradeOfSteel,
                "StandardEN1993p11."+(lowGrade?"NShearBucklingLowGradeOfSteel":"NShearBucklingHighGradeOfSteel"));
        }

        private void ValidatePrismaticCoefficients(bool compression,bool bending)
        {
            if(compression&&(Code.AlphaImperfectionFactorForCurveA0!=.13||Code.AlphaImperfectionFactorForCurveA!=.21
                ||Code.AlphaImperfectionFactorForCurveB!=.34||Code.AlphaImperfectionFactorForCurveC!=.49||Code.AlphaImperfectionFactorForCurveD!=.76))
                throw new NotSupportedException("Prismatic compression requires the qualified standard imperfection factors.");
            if(bending&&(Code.AlphaLTImperfectionFactorForCurveA!=.21||Code.AlphaLTImperfectionFactorForCurveB!=.34
                ||Code.AlphaLTImperfectionFactorForCurveC!=.49||Code.AlphaLTImperfectionFactorForCurveD!=.76
                ||Code.BetaForLateralTorsionalBuckling!=1||Code.LambdaLT0ForLateralTorsionalBuckling!=.2))
                throw new NotSupportedException("This LTB route requires the qualified general curve parameters; a modified national curve needs its own method.");
        }
    }
}
