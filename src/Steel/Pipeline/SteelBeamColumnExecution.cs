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
            return SteelMemberAnalysis.Compression(new SteelVerificationInput(i.Section,new SteelActions(i.Actions.N,0,0,0,0,0),i.Code,i.Member),
                analysis.LoadPatternReference,analysis.ParameterSet,p.Curve1,p.Curve2,SteelBucklingCurve.D,
                analysis.Values.Where(v=>v.Quantity!=SteelStabilityQuantity.Mcr),p.TorsionalExclusionSource,p.FlexuralTorsionalExclusionSource,
                analysis.ExternalMemberClass,analysis.ClassificationSource);
        }
        internal static SteelMethodDecision Select(SteelMemberAnalysis analysis)
        {
            var i=analysis.Input;var a=i.Actions;var p=analysis.BeamColumnParameters;
            if(a.N>=0)return Unsupported("Member.BeamColumnCompressionRequired","Use the tension/section or pure bending route when no compressive force is present.");
            if(a.T!=0)return Unsupported("Member.BeamColumnTorsion","Applied torque/warping is outside this beam-column method.");
            var section=i.Section.CreateSectionCopy();
            if(section.SectionShape.GetType()!=typeof(SectionH))return Unsupported("Member.BeamColumnGeometry","This release qualifies doubly symmetric H only.");
            var h=(SectionH)section.SectionShape;
            if(h.LenghtTopFlange!=h.LenghtBottomFlange||h.ThicknessTopFlange!=h.ThicknessBottomFlange||Math.Abs(h.AngleX1)>1e-10||section.J11<section.J22)
                return Unsupported("Member.BeamColumnGeometry","Doubly symmetric H with horizontal major principal axis required.");
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
                double ratio1=Math.Abs(a.V1)/(ModelShearAreas.GetArea1(section)*i.Section.YieldStrength/(Math.Sqrt(3)*i.Code.SectionCode.GammaM0));
                double ratio2=Math.Abs(a.V2)/(ModelShearAreas.GetArea2(section,i.Code.SectionCode.Standard)*i.Section.YieldStrength/(Math.Sqrt(3)*i.Code.SectionCode.GammaM0));
                if(ratio1>.5||ratio2>.5)return Unsupported("Member.BeamColumnHighShear","High shear needs reduced interaction resistances; this member route requires each shear <= 0.5 VplRd, with shear buckling excluded.");
            }
            return new SteelMethodDecision(SteelCheckTarget.MemberStability,SteelSelectionStatus.Ready,SteelMethod.HBeamColumn,
                null,null,compression.SectionClass,compression.ClassificationSource);
        }
        internal static SteelCheckExecution Execute(SteelMemberAnalysis analysis,SteelMethodDecision decision)
        {
            var i=analysis.Input;var p=analysis.BeamColumnParameters;var section=i.Section.CreateSectionCopy();
            var comp=Compression(analysis);var compression=SteelPrismaticMemberExecution.Execute(comp,SteelPrismaticMemberExecution.Select(comp));
            var trace=new List<SectionResistanceValue>(compression.Trace.Where(v=>v.Name!="Resistance"&&v.Name!="Utilization"));
            double Value(string name)=>compression.Trace.Single(v=>v.Name==name).Value;
            double gamma=i.Code.GammaM1.Value,fy=i.Section.YieldStrength,nrk=section.Area*fy;
            double lambda1=Value("Ncr1.Slenderness"),lambda2=Value("Ncr2.Slenderness"),chi1=Value("Ncr1.Chi"),chi2=Value("Ncr2.Chi");
            bool elastic=decision.SectionClass==3,ntc=i.Code.SectionCode.Standard==SectionResistanceStandard.Ntc2018;
            double m1rk=(elastic?section.Wel1:section.Wpl1)*fy,m2rk=(elastic?section.Wel2:section.Wpl2)*fy,chiLT=1;
            if(p.TorsionSusceptible&&i.Actions.M1!=0)
            {
                double mcr=analysis.Values.Single(v=>v.Quantity==SteelStabilityQuantity.Mcr).Value;
                double lambdaLT=Math.Sqrt(m1rk)/Math.Sqrt(mcr);var curve=SteelPrismaticMemberExecution.LtbCurve(section,ntc);
                chiLT=SteelPrismaticMemberExecution.Reduction(lambdaLT,curve);
                Add(trace,"Mcr",mcr,"Nmm");Add(trace,"LambdaLT",lambdaLT,"1");Add(trace,"AlphaLT",SteelPrismaticMemberExecution.Alpha(curve),"1");
            }
            double nb1=chi1*nrk/gamma,nb2=chi2*nrk/gamma,mb=chiLT*m1rk/gamma,mz=m2rk/gamma;
            var result=BeamColumnInteraction.Evaluate(-i.Actions.N/nb1,-i.Actions.N/nb2,Math.Abs(i.Actions.M1)/mb,Math.Abs(i.Actions.M2)/mz,
                lambda1,lambda2,decision.SectionClass.Value,p.TorsionSusceptible,p.Cm1,p.Cm2,p.CmLT,ntc);
            Add(trace,"NRk",nrk,"N");Add(trace,"M1Rk",m1rk,"Nmm");Add(trace,"M2Rk",m2rk,"Nmm");Add(trace,"ChiLT",chiLT,"1");
            Add(trace,"Cm1",p.Cm1,"1");Add(trace,"Cm2",p.Cm2,"1");Add(trace,"CmLT",p.CmLT,"1");
            Add(trace,"K11",result.K11,"1");Add(trace,"K12",result.K12,"1");Add(trace,"K21",result.K21,"1");Add(trace,"K22",result.K22,"1");
            Add(trace,"Interaction1",result.Utilization1,"1");Add(trace,"Interaction2",result.Utilization2,"1");
            double utilization=Math.Max(result.Utilization,compression.Utilization.Value);
            Add(trace,"Utilization",utilization,"1");
            return new SteelCheckExecution(decision,utilization<=1?SteelExecutionStatus.Passed:SteelExecutionStatus.Failed,
                reference:result.Reference,scope:"Doubly symmetric prismatic H, classes 1-3; N constant, M1/M2 maxima from one design state; explicit Cm, global-analysis and mode assessments. Section/shear verification is separate.",
                utilization:utilization,trace:trace);
        }
        private static void Add(List<SectionResistanceValue> t,string n,double v,string u)
        {SteelInputValidation.Finite(v,n);t.Add(new SectionResistanceValue(n,v,u));}
        private static SteelMethodDecision Unsupported(string code,string message)=>new SteelMethodDecision(SteelCheckTarget.MemberStability,SteelSelectionStatus.NotSupported,null,code,message);
        private static SteelMethodDecision Missing(string name)=>new SteelMethodDecision(SteelCheckTarget.MemberStability,SteelSelectionStatus.MissingInput,null,"Member.BeamColumnInputRequired",name,missing:new[]{name});
    }
}
