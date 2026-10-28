using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Members;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.Pipeline
{
    internal static class SteelBeamColumnExecution
    {
        private static SteelMemberAnalysis Compression(SteelMemberAnalysis analysis)
        {
            var i=analysis.Input;var p=analysis.BeamColumnParameters;
            return SteelMemberAnalysis.Compression(new SteelVerificationInput(i.Section,new SteelActions(i.EffectiveActions.N,0,0,0,0,0),i.Code,i.Member),
                analysis.LoadPatternReference,analysis.ParameterSet,p.Curve1,p.Curve2,SteelBucklingCurve.D,
                analysis.Values.Where(v=>v.Quantity!=SteelStabilityQuantity.Mcr),p.TorsionalExclusionSource,p.FlexuralTorsionalExclusionSource,
                analysis.ExternalMemberClass,analysis.ClassificationSource);
        }
        internal static SteelMethodDecision Select(SteelMemberAnalysis analysis)
        {
            var i=analysis.Input;var a=i.EffectiveActions;var p=analysis.BeamColumnParameters;
            if(a.N>=0)return Unsupported("Member.BeamColumnCompressionRequired","Use the tension/section or pure bending route when no compressive force is present.");
            if(a.T!=0)return Unsupported("Member.BeamColumnTorsion","Applied torque/warping is outside this beam-column method.");
            var section=i.Section.ReadOnlySection;
            var shape=section.SectionShape;bool hollow=Hollow(shape);
            if(hollow) {
                if(!shape.IsDoubleSymmetric||p.TorsionSusceptible)
                    return Unsupported("Member.BeamColumnGeometry","The hollow-section route requires double symmetry and an explicit lateral-torsional exclusion. No H plastic coefficients are reused.");
            } else {
                if(shape.GetType()!=typeof(SectionH))return Unsupported("Member.BeamColumnGeometry","Qualified beam-column families are doubly symmetric H and hollow sections. Asymmetric open sections need a different method.");
                var h=(SectionH)shape;
                if(h.LenghtTopFlange!=h.LenghtBottomFlange||h.ThicknessTopFlange!=h.ThicknessBottomFlange||Math.Abs(h.AngleX1)>1e-10||section.J11<section.J22)
                    return Unsupported("Member.BeamColumnGeometry","Doubly symmetric H with horizontal major principal axis required.");
            }
            if(analysis.Values.Any(v=>v.Quantity!=SteelStabilityQuantity.Ncr1&&v.Quantity!=SteelStabilityQuantity.Ncr2&&v.Quantity!=SteelStabilityQuantity.Mcr))
                throw new ArgumentException("Beam-column route accepts flexural Ncr1/Ncr2 and Mcr; other compression modes require explicit applicability assessments.");
            var compression=SteelPrismaticMemberExecution.Select(Compression(analysis));
            if(compression.Status!=SteelSelectionStatus.Ready)return compression;
            bool needsMcr=p.TorsionSusceptible&&a.M1!=0;
            if(needsMcr&&!analysis.Values.Any(v=>v.Quantity==SteelStabilityQuantity.Mcr))return Missing("Mcr");
            if(!needsMcr&&analysis.Values.Any(v=>v.Quantity==SteelStabilityQuantity.Mcr))throw new ArgumentException("Mcr supplied for an inactive or explicitly excluded lateral-torsional mechanism.");
            if(needsMcr&&i.Code.SectionCode.Standard==SectionResistanceStandard.Ntc2018&&p.CriticalMomentBasis!=SteelCriticalMomentBasis.UniformMoment)
                return Unsupported("Member.NtcUniformMcrRequired","NTC conservative general LTB requires a uniform-moment Mcr.");
            if(a.V1!=0||a.V2!=0)
            {
                if(p.ShearBucklingExclusionSource==null)return Missing("BeamColumn.ShearBucklingExclusionSource");
                double ratio1=Math.Abs(a.V1)/SteelPlasticShear.Resistance(i,1);
                double ratio2=Math.Abs(a.V2)/SteelPlasticShear.Resistance(i,2);
                if(ratio1>.5||ratio2>.5)return Unsupported("Member.BeamColumnHighShear","High shear needs reduced interaction resistances; this member route requires each shear <= 0.5 VplRd, with shear buckling excluded.");
            }
            var ltb=SteelLateralTorsionalExecution.Qualify(analysis);if(ltb!=null)return ltb;
            return new SteelMethodDecision(SteelCheckTarget.MemberStability,SteelSelectionStatus.Ready,SteelMethod.HBeamColumn,
                null,null,compression.SectionClass,compression.ClassificationSource);
        }
        internal static SteelCheckExecution Execute(SteelMemberAnalysis analysis,SteelMethodDecision decision)
        {
            var i=analysis.Input;var p=analysis.BeamColumnParameters;var section=i.Section.ReadOnlySection;
            var comp=Compression(analysis);
            var compression=SteelPrismaticMemberExecution.Execute(comp,SteelPrismaticMemberExecution.Select(comp),out var flexural);
            var trace=new List<SectionResistanceValue>(compression.Trace.Where(v=>v.Name!="Resistance"&&v.Name!="Utilization"));
            bool effective=decision.SectionClass==4;
            // Typed compression intermediates: the class4 Aeff exists exactly when the compression route used it.
            double gamma=i.Code.GammaM1.Value,fy=i.Section.YieldStrength,nrk=(effective?flexural.EffectiveArea.Value:section.Area)*fy;
            double lambda1=flexural.Slenderness1,lambda2=flexural.Slenderness2,chi1=flexural.Chi1,chi2=flexural.Chi2;
            bool elastic=decision.SectionClass>=3||Hollow(section.SectionShape),ntc=i.Code.SectionCode.Standard==SectionResistanceStandard.Ntc2018;
            double w1=effective?MemoizedEffectiveProperties.BendingModulus(i,1):(elastic?section.Wel1:section.Wpl1);
            double w2=effective?MemoizedEffectiveProperties.BendingModulus(i,2):(elastic?section.Wel2:section.Wpl2);
            double m1rk=w1*fy,m2rk=w2*fy,chiLT=1;
            if(effective) {Add(trace,"Weff1Min",w1,"mm3");Add(trace,"Weff2Min",w2,"mm3");}
            SteelLateralTorsionalExecution.Reduction ltb=null;
            if(p.TorsionSusceptible&&i.EffectiveActions.M1!=0)
            {
                double mcr=analysis.Values.Single(v=>v.Quantity==SteelStabilityQuantity.Mcr).Value;
                double lambdaLT=Math.Sqrt(m1rk)/Math.Sqrt(mcr);ltb=SteelLateralTorsionalExecution.Reduce(analysis,section,lambdaLT,ntc);var curve=ltb.Curve;
                chiLT=ltb.Chi;
                Add(trace,"Mcr",mcr,"Nmm");Add(trace,"LambdaLT",lambdaLT,"1");Add(trace,"AlphaLT",BucklingCurves.Alpha(curve),"1");
                SteelLateralTorsionalExecution.AddTrace(trace,ltb,curveParameters:true);
            }
            double nb1=chi1*nrk/gamma,nb2=chi2*nrk/gamma,mb=chiLT*m1rk/gamma,mz=m2rk/gamma;
            var result=BeamColumnInteraction.Evaluate(-i.EffectiveActions.N/nb1,-i.EffectiveActions.N/nb2,Math.Abs(i.EffectiveActions.M1)/mb,Math.Abs(i.EffectiveActions.M2)/mz,
                lambda1,lambda2,elastic?3:decision.SectionClass.Value,p.TorsionSusceptible,p.Cm1,p.Cm2,p.CmLT,ntc);
            Add(trace,"ElasticInteraction",elastic?1:0,"1");
            Add(trace,"NRk",nrk,"N");Add(trace,"M1Rk",m1rk,"Nmm");Add(trace,"M2Rk",m2rk,"Nmm");Add(trace,"ChiLT",chiLT,"1");
            Add(trace,"Cm1",p.Cm1,"1");Add(trace,"Cm2",p.Cm2,"1");Add(trace,"CmLT",p.CmLT,"1");
            Add(trace,"K11",result.K11,"1");Add(trace,"K12",result.K12,"1");Add(trace,"K21",result.K21,"1");Add(trace,"K22",result.K22,"1");
            Add(trace,"Interaction1",result.Utilization1,"1");Add(trace,"Interaction2",result.Utilization2,"1");
            double utilization=Math.Max(result.Utilization,compression.Utilization.Value);
            Add(trace,"Utilization",utilization,"1");
            return new SteelCheckExecution(decision,utilization<=1?SteelExecutionStatus.Passed:SteelExecutionStatus.Failed,
                reference:result.Reference+(effective?"; EN1993-1-5:2006 4.3(3)/(4), class4 Aeff/Weff at fy; gross critical values":"")
                    +(ltb?.IsRolled==true?"; chiLT: "+SteelLateralTorsionalExecution.Reference(ntc):""),scope:"Doubly symmetric prismatic H/RHS/SHS/CHS classes1-3, qualified H/RHS class4 with zero uniform-compression centroid shift; hollow sections use conservative elastic moduli and elastic interaction coefficients; N constant, M1/M2 maxima from one design state; explicit Cm, global-analysis and mode assessments. Section/shear verification is separate.",
                utilization:utilization,trace:trace);
        }
        private static bool Hollow(ISectionShape shape) => SteelSectionFamilies.Is(shape,SteelSectionKind.CircularTube)||SteelSectionFamilies.Is(shape,SteelSectionKind.RectangularTube);
        private static void Add(List<SectionResistanceValue> t,string n,double v,string u)
        {SteelInputValidation.Finite(v,n);t.Add(new SectionResistanceValue(n,v,u));}
        private static SteelMethodDecision Unsupported(string code,string message)=>new SteelMethodDecision(SteelCheckTarget.MemberStability,SteelSelectionStatus.NotSupported,null,code,message);
        private static SteelMethodDecision Missing(string name)=>new SteelMethodDecision(SteelCheckTarget.MemberStability,SteelSelectionStatus.MissingInput,null,"Member.BeamColumnInputRequired",name,missing:new[]{name});
    }
}
