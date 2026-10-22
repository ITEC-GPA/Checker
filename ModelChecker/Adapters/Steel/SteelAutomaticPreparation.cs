using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Model.Checking.Contracts;

namespace GPC.Model.Checker
{
    public sealed partial class SteelMemberPipelineChecker
    {
        private bool AutomaticCurves => _parameters.TryGetValue("CurveSelection",out var value)&&value=="Automatic";
        private SteelBucklingCurve CompressionCurve(SteelVerificationInput input,string key)
        {
            if(!AutomaticCurves)return EnumValue<SteelBucklingCurve>(key);
            if(new[]{"CompressionCurve1","CompressionCurve2","TorsionalCurve"}.Any(_parameters.ContainsKey))
                throw new ArgumentException("ExplicitCurvesConflictWithAutomaticSelection");
            var curves=SteelAutomaticStability.CompressionCurves(input.Section,input.Code.SectionCode.Standard);
            return key=="CompressionCurve1"?curves.First:key=="CompressionCurve2"?curves.Second:curves.Torsional;
        }
        private string PreparedParameterSet(SteelVerificationInput input) => Required("ParameterSet")+(AutomaticCurves
            ?"; "+SteelAutomaticStability.CompressionCurves(input.Section,input.Code.SectionCode.Standard).Source:"");
        private void ValidatePreparationOptions(string mode)
        {
            if(_parameters.TryGetValue("CurveSelection",out var curves)&&(curves!="Automatic"||(mode!="Compression"&&mode!="BeamColumn")))
                throw new ArgumentException("UnsupportedAutomaticCurveSelection");
            if(_parameters.TryGetValue("CriticalValues",out var critical)) {
                if(critical!="UniformFork"||(mode!="Compression"&&mode!="Bending"))throw new ArgumentException("UnsupportedCriticalValuePreparation");
                Required("CriticalValuesSource");
            } else if(_parameters.ContainsKey("CriticalValuesSource"))throw new ArgumentException("CriticalSourceWithoutPreparation");
        }
        private IReadOnlyList<SteelStabilityValue> PrepareCriticalValues(PhysicalMemberCheckInput member,SteelVerificationInput input,
            SteelPhysicalMemberPreparation physical,bool compression,IReadOnlyList<SteelStabilityValue> external)
        {
            if(!_parameters.ContainsKey("CriticalValues"))return external;
            if(external.Count!=0||_parameters.ContainsKey("TorsionalExclusionSource")||_parameters.ContainsKey("FlexuralTorsionalExclusionSource"))
                throw new ArgumentException("AutomaticCriticalValuesConflictWithExternalAssessment");
            var snapshot=member.Snapshot;var context=snapshot.Context;
            if(snapshot.SpanStart!=0||snapshot.SpanEnd!=snapshot.Length||snapshot.Length!=snapshot.DeformableLength
                ||context.Restraints.Count!=0||context.LateralTorsionalLength.HasValue&&context.LateralTorsionalLength.Value!=input.Member.Length
                ||physical.Cuts.Any(c=>!Same(c.Actions,input.Actions)))
                throw new NotSupportedException("Automatic fork eigenvalues require the full deformable span, no intermediate restraints and uniform actions.");
            if(!compression&&Required("McrBasis")!="UniformMoment")throw new ArgumentException("AutomaticMcrUsesUniformMomentBasis");
            return SteelAutomaticStability.UniformFork(input,!compression,Required("CriticalValuesSource"));
        }
    }
}
