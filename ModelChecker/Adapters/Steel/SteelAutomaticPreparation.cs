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
                if((critical!="UniformFork"&&critical!="ForkDiagram")||(mode!="Compression"&&mode!="Bending"&&mode!="BeamColumn")||(critical=="ForkDiagram"&&mode=="Compression"))throw new ArgumentException("UnsupportedCriticalValuePreparation");
                Required("CriticalValuesSource");
            } else if(_parameters.ContainsKey("CriticalValuesSource"))throw new ArgumentException("CriticalSourceWithoutPreparation");
            if(new[]{"ForkDiagram","ForkLoadHeight"}.Any(_parameters.ContainsKey) && (!_parameters.TryGetValue("CriticalValues",out var v)||v!="ForkDiagram"))
                throw new ArgumentException("ForkDiagramParametersWithoutPreparation");
        }
        private IReadOnlyList<SteelStabilityValue> PrepareCriticalValues(PhysicalMemberCheckInput member,SteelVerificationInput input,
            SteelPhysicalMemberPreparation physical,bool compression,IReadOnlyList<SteelStabilityValue> external)
        {
            if(!_parameters.ContainsKey("CriticalValues"))return external;
            // Explicit NcrT/NcrFT values or exclusions win over the automatic ones; other explicit values conflict.
            if(external.Any(v=>v.Quantity!=SteelStabilityQuantity.NcrT&&v.Quantity!=SteelStabilityQuantity.NcrFT))
                throw new ArgumentException("AutomaticCriticalValuesConflictWithExternalAssessment");
            // Segment view: Ncr1/Ncr2/NcrT follow the segment lengths in the member input (library Euler/automatic modes).
            if(physical.Segment!=null)return compression?external:new[]{SegmentMoment(member,input,physical)}.Where(v=>v!=null).ToArray();
            if(_parameters["CriticalValues"]=="ForkDiagram")return PrepareForkDiagram(member,input,physical);
            var snapshot=member.Snapshot;var context=snapshot.Context;
            if(snapshot.SpanStart!=0||snapshot.SpanEnd!=snapshot.Length||snapshot.Length!=snapshot.DeformableLength
                ||context.Restraints.Count!=0||context.LateralTorsionalLength.HasValue&&context.LateralTorsionalLength.Value!=input.Member.Length
                ||physical.Cuts.Any(c=>!Same(c.Actions,input.Actions)))
                throw new NotSupportedException("Automatic fork eigenvalues require the full deformable span, no intermediate restraints and uniform actions.");
            if(!compression&&Required("McrBasis")!="UniformMoment")throw new ArgumentException("AutomaticMcrUsesUniformMomentBasis");
            var prepared=SteelAutomaticStability.UniformFork(input,!compression,Required("CriticalValuesSource"));
            bool Explicit(SteelStabilityQuantity q)=>external.Any(v=>v.Quantity==q)
                ||_parameters.ContainsKey(q==SteelStabilityQuantity.NcrT?"TorsionalExclusionSource":"FlexuralTorsionalExclusionSource");
            return external.Count==0&&!Explicit(SteelStabilityQuantity.NcrT)&&!Explicit(SteelStabilityQuantity.NcrFT)?prepared
                :prepared.Where(v=>v.Quantity!=SteelStabilityQuantity.NcrT&&v.Quantity!=SteelStabilityQuantity.NcrFT||!Explicit(v.Quantity)).Concat(external).ToArray();
        }

        /// <summary>Beam-column of a fork segment: Ncr1/Ncr2/NcrT via the segment member lengths; Mcr only when
        /// lateral-torsional buckling is active (TorsionSusceptible and an effective M1 in the segment).</summary>
        private IReadOnlyList<SteelStabilityValue> PrepareBeamColumnCriticalValues(PhysicalMemberCheckInput member,SteelVerificationInput input,
            SteelPhysicalMemberPreparation physical,bool susceptible,IReadOnlyList<SteelStabilityValue> external)
        {
            if(!_parameters.ContainsKey("CriticalValues"))return external;
            if(physical.Segment==null)throw new SteelPreparationRefusal("SteelMember.Segment.Required","Beam-column automatic critical values are prepared per fork segment.");
            if(external.Any(v=>v.Quantity!=SteelStabilityQuantity.NcrT&&v.Quantity!=SteelStabilityQuantity.NcrFT))
                throw new ArgumentException("AutomaticCriticalValuesConflictWithExternalAssessment");
            var mcr=susceptible&&input.EffectiveActions.M1!=0?SegmentMoment(member,input,physical):null;
            return mcr==null?external:external.Concat(new[]{mcr}).ToArray();
        }
    }
}
