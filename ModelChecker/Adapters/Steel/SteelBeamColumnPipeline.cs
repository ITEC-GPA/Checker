using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Model.Checking.Contracts;

namespace GPC.Model.Checker
{
    public sealed partial class SteelMemberPipelineChecker
    {
        private static readonly string[] BeamColumnKeys={"TorsionSusceptible","Cm1","Cm2","CmLT","MomentFactorSource",
            "StabilityModelSource","LateralTorsionalExclusionSource","ShearBucklingExclusionSource"};

        private SteelMemberAnalysis BeamColumn(PhysicalMemberCheckInput input,SteelVerificationInput common,SteelPhysicalMemberPreparation physical)
        {
            ValidatePrismaticCoefficients(true,true);
            if(new[]{"InPlaneCurve","LateralCurve","LateralTorsionalCurve","TorsionalCurve"}.Any(_parameters.ContainsKey))
                throw new ArgumentException("CurveParametersForAnotherMemberMethod");
            if(!bool.TryParse(Required("TorsionSusceptible"),out bool susceptible))throw new ArgumentException("InvalidTorsionalSusceptibility");
            var actions=physical.BeamColumnEnvelope();
            common=new SteelVerificationInput(common.Section,actions,common.Code,common.Member);
            var data=new SteelBeamColumnParameters(EnumValue<SteelBucklingCurve>("CompressionCurve1"),EnumValue<SteelBucklingCurve>("CompressionCurve2"),
                susceptible,Number("Cm1"),Number("Cm2"),Number("CmLT"),Required("MomentFactorSource"),Required("StabilityModelSource"),
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
            return SteelMemberAnalysis.BeamColumn(common,Required("LoadPatternReference"),Required("ParameterSet"),data,values,memberClass,
                _parameters.TryGetValue("ClassificationSource",out var source)?source:null);
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
