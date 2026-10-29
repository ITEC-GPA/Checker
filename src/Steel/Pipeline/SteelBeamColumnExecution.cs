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
        // Tension is favourable to flexural and lateral-torsional buckling: the axial route sees N<=0 only (n=0 under
        // tension, traced); the N-M resistance stays a section check. The class is the beam-column's own (Class).
        private static SteelMemberAnalysis Compression(SteelMemberAnalysis analysis,int cl,string source)
        {
            var i=analysis.Input;var p=analysis.BeamColumnParameters;
            return SteelMemberAnalysis.Compression(new SteelVerificationInput(i.Section,new SteelActions(Math.Min(0,i.EffectiveActions.N),0,0,0,0,0),i.Code,i.Member),
                analysis.LoadPatternReference,analysis.ParameterSet,p.Curve1,p.Curve2,SteelBucklingCurve.D,
                analysis.Values.Where(v=>v.Quantity!=SteelStabilityQuantity.Mcr),p.TorsionalExclusionSource,p.FlexuralTorsionalExclusionSource,
                cl,source);
        }
        /// <summary>EC3 6.3.3 Table 6.7 / NTC 4.2.4.1.3.3: NRk and MiRk follow the class of the section under the
        /// beam-column's own N-M (EC3 5.5.2, NTC 4.2.3.1 via ModelStressClassification), tension neglected. The envelope
        /// maxima may come from different cuts: (N,M1,M2), (N,M1,0) and (N,0,M2) are classified and the highest class
        /// governs. With compression only alpha-based classes 1-2 with M1 are kept, otherwise the uniform-compression
        /// class. Families without a stress-based route keep the uniform-compression class; a supplied class wins.</summary>
        private static SteelMethodSelector.ClassificationInfo Class(SteelMemberAnalysis analysis)
        {
            var i=analysis.Input;var a=i.EffectiveActions;
            if(analysis.ExternalMemberClass.HasValue)return new SteelMethodSelector.ClassificationInfo(analysis.ExternalMemberClass.Value,analysis.ClassificationSource);
            double n=Math.Min(0,a.N);var states=new List<(string Name,double M1,double M2)>{("N,M1,M2",a.M1,a.M2)};
            if(a.M1!=0&&a.M2!=0){states.Add(("N,M1,0",a.M1,0));states.Add(("N,0,M2",0,a.M2));}
            SteelMethodSelector.ClassificationInfo worst=null;string state=null;
            foreach(var s in states)
            {
                var info=SteelMethodSelector.Classification(new SteelVerificationInput(i.Section,new SteelActions(n,0,0,0,s.M1,s.M2),i.Code,i.Member),
                    i.Section.ReadOnlySection,null,concomitantActions:true);
                if(info.Failure!=null)return info;
                if(worst==null||info.Class>worst.Class){worst=info;state=s.Name;}
            }
            // With compression only alpha-based classes 1-2 under N and M1 hold along the whole member: alpha depends on N
            // alone and (N,M1,0) keeps the flanges uniformly compressed. Class 3 rests on psi, i.e. on M/N, which varies along
            // the member, and with M1=0 the flange alpha depends on M2: the uniform-compression class then governs.
            if(n<0&&(worst.Class>2||a.M1==0))
            {
                var uniform=SteelMethodSelector.Classification(new SteelVerificationInput(i.Section,new SteelActions(n,0,0,0,0,0),i.Code,i.Member),
                    i.Section.ReadOnlySection,null);
                if(uniform.Failure!=null)return uniform;
                if(uniform.Class>worst.Class)return new SteelMethodSelector.ClassificationInfo(uniform.Class.Value,uniform.Source
                    +"; beam-column with compression: the N-M class "+worst.Class+" ("+state+") depends on the moment level along the member");
            }
            return new SteelMethodSelector.ClassificationInfo(worst.Class.Value,worst.Source+"; beam-column envelope state "+state
                +(states.Count>1?" (highest of (N,M1,M2), (N,M1,0), (N,0,M2): maxima of different cuts are not taken as concomitant)":"")
                +(a.N>0?"; tension neglected":""));
        }
        internal static SteelMethodDecision Select(SteelMemberAnalysis analysis)
        {
            var i=analysis.Input;var a=i.EffectiveActions;var p=analysis.BeamColumnParameters;
            if(a.T!=0)return Unsupported("Member.BeamColumnTorsion","Applied torque/warping is outside this beam-column method.");
            if(a.N>=0&&a.M1==0&&a.M2==0)return new SteelMethodDecision(SteelCheckTarget.MemberStability,SteelSelectionStatus.NotRequired,null,
                "Member.NoActiveDemand","No compression and no bending: no beam-column stability demand; tension is a section check.");
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
            // NcrT/NcrFT feed the compression sub-check; without value or exclusion it resolves them automatically where qualified.
            if(analysis.Values.Any(v=>v.Quantity==SteelStabilityQuantity.AlphaUltK||v.Quantity==SteelStabilityQuantity.AlphaCrOp))
                throw new ArgumentException("Beam-column route accepts the compression critical forces Ncr1/Ncr2/NcrT/NcrFT and Mcr only.");
            var cl=Class(analysis);
            if(cl.Failure!=null)return new SteelMethodDecision(SteelCheckTarget.MemberStability,cl.Failure.Status,null,
                cl.Failure.DiagnosticCode,cl.Failure.Message,missing:cl.Failure.MissingInputs);
            var compression=SteelPrismaticMemberExecution.Select(Compression(analysis,cl.Class.Value,cl.Source),true);
            if(compression.Status!=SteelSelectionStatus.Ready)return compression;
            bool needsMcr=p.TorsionSusceptible&&a.M1!=0;
            if(needsMcr&&!analysis.Values.Any(v=>v.Quantity==SteelStabilityQuantity.Mcr))return Missing("Mcr");
            if(!needsMcr&&analysis.Values.Any(v=>v.Quantity==SteelStabilityQuantity.Mcr))throw new ArgumentException("Mcr supplied for an inactive or explicitly excluded lateral-torsional mechanism.");
            if(needsMcr&&i.Code.SectionCode.Standard==SectionResistanceStandard.Ntc2018&&p.CriticalMomentBasis!=SteelCriticalMomentBasis.UniformMoment)
                return Unsupported("Member.NtcUniformMcrRequired","NTC conservative general LTB requires a uniform-moment Mcr.");
            var ltb=SteelLateralTorsionalExecution.Qualify(analysis);if(ltb!=null)return ltb;
            if(a.V1!=0||a.V2!=0)
            {
                // Automatic screens exist for the exact H and the RHS with principal axis 1 horizontal (walls parallel to each
                // shear as webs); CHS and rotated hollow sections keep the explicit assessment.
                bool rhs=SteelSectionFamilies.Is(shape,SteelSectionKind.RectangularTube)&&Math.Abs(shape.AngleX1)<=1e-10;
                if(p.ShearBucklingExclusionSource==null&&hollow&&!rhs)return Missing("BeamColumn.ShearBucklingExclusionSource");
                double ratio1=Math.Abs(a.V1)/SteelPlasticShear.Resistance(i,1);
                double ratio2=Math.Abs(a.V2)/SteelPlasticShear.Resistance(i,2);
                if(ratio1>.5||ratio2>.5)return Unsupported("Member.BeamColumnHighShear","High shear needs reduced interaction resistances; this member route requires each shear <= 0.5 VplRd, with shear buckling excluded.");
                // Without an explicit source: one assessment per active shear, as on the section route (H web
                // EvaluateEc3HWeb/EvaluateNtcHWeb, flanges EvaluateHFlanges; RHS EvaluateEc3RhsWebs/EvaluateNtcRhsWebs).
                // A negative one stops the route; no reduced resistance.
                if(p.ShearBucklingExclusionSource==null)return new SteelMethodDecision(SteelCheckTarget.MemberStability,SteelSelectionStatus.RequiresPreparation,
                    SteelMethod.HBeamColumn,"ShearBuckling.AssessmentRequired",rhs?"Without BeamColumn.ShearBucklingExclusionSource every active shear of the rectangular hollow section needs a shear-buckling assessment of the walls parallel to it (EvaluateEc3RhsWebs/EvaluateNtcRhsWebs)."
                        :"Without BeamColumn.ShearBucklingExclusionSource every active shear of the exact H needs a shear-buckling assessment (web: EvaluateEc3HWeb/EvaluateNtcHWeb; flanges: EvaluateHFlanges).",
                    compression.SectionClass,compression.ClassificationSource,new[]{SteelPreparationRequirement.ShearBucklingAssessment});
            }
            return new SteelMethodDecision(SteelCheckTarget.MemberStability,SteelSelectionStatus.Ready,SteelMethod.HBeamColumn,
                null,null,compression.SectionClass,compression.ClassificationSource);
        }
        internal static SteelCheckExecution Execute(SteelMemberAnalysis analysis,SteelMethodDecision decision)
        {
            var i=analysis.Input;var p=analysis.BeamColumnParameters;var section=i.Section.ReadOnlySection;
            var comp=Compression(analysis,decision.SectionClass.Value,decision.ClassificationSource);
            var compression=SteelPrismaticMemberExecution.Execute(comp,SteelPrismaticMemberExecution.Select(comp,true),out var flexural);
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
            // N>=0: n1=n2=0, so k11=Cm1, k22=Cm2, k12/k21 from Tables B.1-B.2 (C4.2.IV-V) at n=0; CmLT-.25>=.15.
            double compressive=-Math.Min(0,i.EffectiveActions.N);
            if(i.EffectiveActions.N>0)Add(trace,"TensionNeglectedAsFavourable",i.EffectiveActions.N,"N");
            var result=BeamColumnInteraction.Evaluate(compressive/nb1,compressive/nb2,Math.Abs(i.EffectiveActions.M1)/mb,Math.Abs(i.EffectiveActions.M2)/mz,
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
                    +(ltb?.IsRolled==true?"; chiLT: "+SteelLateralTorsionalExecution.Reference(ntc):""),scope:"Doubly symmetric prismatic H/RHS/SHS/CHS classes1-3, qualified H/RHS class4 with zero uniform-compression centroid shift; hollow sections use conservative elastic moduli and elastic interaction coefficients; N constant, M1/M2 maxima from one design state; explicit Cm, global-analysis and mode assessments. Section/shear verification is separate."
                    +(i.EffectiveActions.N>=0?" N>=0: interaction at NEd=0, tension neglected as favourable to flexural and lateral-torsional buckling; N-M resistance is a section check.":""),
                utilization:utilization,trace:trace);
        }
        private static bool Hollow(ISectionShape shape) => SteelSectionFamilies.Is(shape,SteelSectionKind.CircularTube)||SteelSectionFamilies.Is(shape,SteelSectionKind.RectangularTube);
        private static void Add(List<SectionResistanceValue> t,string n,double v,string u)
        {SteelInputValidation.Finite(v,n);t.Add(new SectionResistanceValue(n,v,u));}
        private static SteelMethodDecision Unsupported(string code,string message)=>new SteelMethodDecision(SteelCheckTarget.MemberStability,SteelSelectionStatus.NotSupported,null,code,message);
        private static SteelMethodDecision Missing(string name)=>new SteelMethodDecision(SteelCheckTarget.MemberStability,SteelSelectionStatus.MissingInput,null,"Member.BeamColumnInputRequired",name,missing:new[]{name});
    }
}
