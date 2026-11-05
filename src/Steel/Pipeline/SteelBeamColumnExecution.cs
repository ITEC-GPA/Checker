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
                if(p.ConservativeClass4WebInteraction && (cl.Class!=4 || hollow || a.V1!=0 || a.M2!=0))
                    return Unsupported("Member.Class4WebInteractionDomain","The explicit class4 web interaction requires doubly symmetric H, class4, N-M1-V2 and no other shear/bending plane.");
                // Automatic screens exist for the exact H and the RHS with principal axis 1 horizontal (walls parallel to each
                // shear as webs), and for the CHS within the wall screen (EvaluateChsWall, EN 1993-1-6 eq. D.40): beyond its
                // limit the CHS keeps the explicit assessment, as rotated hollow sections do.
                bool rhs=SteelSectionFamilies.Is(shape,SteelSectionKind.RectangularTube)&&Math.Abs(shape.AngleX1)<=1e-10;
                bool chs=SteelShearBucklingAssessment.ChsWallExcludesBuckling(i);
                if(p.ShearBucklingExclusionSource==null&&hollow&&!rhs&&!chs)return Missing("BeamColumn.ShearBucklingExclusionSource");
                // Plastic shear areas follow the geometry axes: with rotated principal axes (e.g. a wide RHS, axis 1
                // vertical) they would not belong to V1/V2 and could overestimate VplRd. Same rule as the section route.
                if(!SteelSectionFamilies.HasQualifiedShearAreas(section))return Unsupported("Member.BeamColumnShearAreasNotQualified",
                    "Each shear <= 0.5 VplRd needs code shear areas on principal axes aligned with the section geometry; rotated principal axes are not supported with shear.");
                double ratio1=Math.Abs(a.V1)/SteelPlasticShear.Resistance(i,1);
                double ratio2=Math.Abs(a.V2)/SteelPlasticShear.Resistance(i,2);
                if(p.ConservativeShearSuperposition && cl.Class>3)
                    return Unsupported("Member.ShearSuperpositionClass4NotQualified","Class4 needs a separately qualified plate-buckling interaction; shear superposition alone cannot qualify it.");
                if(!p.ConservativeClass4WebInteraction && !p.ConservativeShearSuperposition && (ratio1>.5||ratio2>.5))
                {
                    if(!p.ConservativeHighShear)return Unsupported("Member.BeamColumnHighShear","High shear needs reduced interaction resistances; select WithConservativeHighShear explicitly for the bounded conservative route.");
                    if(cl.Class>3)return Unsupported("Member.BeamColumnHighShearClass4","High shear with effective class4 properties needs a coupled plate-buckling model.");
                    if(a.V1!=0&&a.V2!=0)return Unsupported("Member.BeamColumnHighShearBiaxial","The conservative high-shear route requires exactly one active principal shear.");
                }
                // Without an explicit source: one assessment per active shear, as on the section route (H web
                // EvaluateEc3HWeb/EvaluateNtcHWeb, flanges EvaluateHFlanges; RHS EvaluateEc3RhsWebs/EvaluateNtcRhsWebs;
                // CHS EvaluateChsWall). A negative one stops the route; no reduced resistance.
                if(p.ConservativeClass4WebInteraction)return new SteelMethodDecision(SteelCheckTarget.MemberStability,SteelSelectionStatus.RequiresPreparation,
                    SteelMethod.HBeamColumn,"Member.Class4WebInteractionAssessmentRequired","Computed same-input web resistance with WithConservativeInPlaneNormalInteraction is required; external exclusion cannot substitute for it.",
                    compression.SectionClass,compression.ClassificationSource,new[]{SteelPreparationRequirement.ShearBucklingAssessment});
                if(p.ShearBucklingExclusionSource==null)return new SteelMethodDecision(SteelCheckTarget.MemberStability,SteelSelectionStatus.RequiresPreparation,
                    SteelMethod.HBeamColumn,"ShearBuckling.AssessmentRequired",rhs?"Without BeamColumn.ShearBucklingExclusionSource every active shear of the rectangular hollow section needs a shear-buckling assessment of the walls parallel to it (EvaluateEc3RhsWebs/EvaluateNtcRhsWebs)."
                        :chs?"Without BeamColumn.ShearBucklingExclusionSource every active shear of the circular hollow section needs the wall screen assessment (EvaluateChsWall)."
                        :"Without BeamColumn.ShearBucklingExclusionSource every active shear of the exact H needs a shear-buckling assessment (web: EvaluateEc3HWeb/EvaluateNtcHWeb; flanges: EvaluateHFlanges).",
                    compression.SectionClass,compression.ClassificationSource,new[]{SteelPreparationRequirement.ShearBucklingAssessment});
            }
            return new SteelMethodDecision(SteelCheckTarget.MemberStability,SteelSelectionStatus.Ready,SteelMethod.HBeamColumn,
                null,null,compression.SectionClass,compression.ClassificationSource);
        }
        internal static SteelCheckExecution Execute(SteelMemberAnalysis analysis,SteelMethodDecision decision,
            IReadOnlyList<SteelShearBucklingAssessment> shearAssessments=null)
        {
            var i=analysis.Input;var p=analysis.BeamColumnParameters;var section=i.Section.ReadOnlySection;
            var comp=Compression(analysis,decision.SectionClass.Value,decision.ClassificationSource);
            var compression=SteelPrismaticMemberExecution.Execute(comp,SteelPrismaticMemberExecution.Select(comp,true),out var flexural);
            var trace=new List<SectionResistanceValue>(compression.Trace.Where(v=>v.Name!="Resistance"&&v.Name!="Utilization"));
            bool effective=decision.SectionClass==4;
            // Typed compression intermediates: the class4 Aeff exists exactly when the compression route used it.
            double gamma=i.Code.GammaM1.Value,fy=i.Section.YieldStrength,nrk=(effective?flexural.EffectiveArea.Value:section.Area)*fy;
            double lambda1=flexural.Slenderness1,lambda2=flexural.Slenderness2,chi1=flexural.Chi1,chi2=flexural.Chi2;
            bool elastic=p.ConservativeShearSuperposition||decision.SectionClass>=3||Hollow(section.SectionShape),ntc=i.Code.SectionCode.Standard==SectionResistanceStandard.Ntc2018;
            double w1=effective?MemoizedEffectiveProperties.BendingModulus(i,1):(elastic?section.Wel1:section.Wpl1);
            double w2=effective?MemoizedEffectiveProperties.BendingModulus(i,2):(elastic?section.Wel2:section.Wpl2);
            double m1rk=w1*fy,m2rk=w2*fy,chiLT=1;
            if(effective) {Add(trace,"Weff1Min",w1,"mm3");Add(trace,"Weff2Min",w2,"mm3");}
            SteelLateralTorsionalExecution.Reduction ltb=null;
            if(p.TorsionSusceptible&&i.EffectiveActions.M1!=0)
            {
                double mcr=analysis.Values.Single(v=>v.Quantity==SteelStabilityQuantity.Mcr).Value;
                double lambdaLT=Math.Sqrt(m1rk)/Math.Sqrt(mcr);ltb=SteelLateralTorsionalExecution.Reduce(analysis,section,lambdaLT,ntc);
                chiLT=ltb.Chi;
                Add(trace,"Mcr",mcr,"Nmm");Add(trace,"LambdaLT",lambdaLT,"1");Add(trace,"AlphaLT",ltb.Alpha,"1");
                SteelLateralTorsionalExecution.AddTrace(trace,ltb,curveParameters:true);SteelLateralTorsionalExecution.AddCurveTrace(trace,ltb);
            }
            double nb1=chi1*nrk/gamma,nb2=chi2*nrk/gamma,mb=chiLT*m1rk/gamma,mz=m2rk/gamma;
            // N>=0: n1=n2=0, so k11=Cm1, k22=Cm2, k12/k21 from Tables B.1-B.2 (C4.2.IV-V) at n=0; CmLT-.25>=.15.
            double compressive=-Math.Min(0,i.EffectiveActions.N);
            if(i.EffectiveActions.N>0)Add(trace,"TensionNeglectedAsFavourable",i.EffectiveActions.N,"N");
            double ratio1=i.EffectiveActions.V1==0?0:Math.Abs(i.EffectiveActions.V1)/SteelPlasticShear.Resistance(i,1);
            double shear2Resistance=p.ConservativeClass4WebInteraction && i.EffectiveActions.V2!=0
                ? shearAssessments.Single(x=>x.Axis==2).BucklingResistance.Value : SteelPlasticShear.Resistance(i,2);
            double ratio2=i.EffectiveActions.V2==0?0:Math.Abs(i.EffectiveActions.V2)/shear2Resistance;
            double shearRatio=Math.Max(ratio1,ratio2);
            double rho=p.ConservativeShearSuperposition || p.ConservativeClass4WebInteraction ? ratio1+ratio2
                : p.ConservativeHighShear&&shearRatio>.5?Math.Pow(2*shearRatio-1,2):0;
            SteelInputValidation.Finite(rho,"normal resistance reduction");
            string shearTrace=p.ConservativeClass4WebInteraction?"Class4WebInteraction":p.ConservativeShearSuperposition?"ShearSuperposition":"HighShear";
            if(p.ConservativeClass4WebInteraction) {
                Add(trace,"Class4WebInteraction.VbwRd",shear2Resistance,"N");
                Add(trace,"Class4WebInteraction.ShearRatio",ratio2,"1");
            }
            if(p.ConservativeShearSuperposition)
            {
                Add(trace,"ShearSuperposition.ShearRatio1",ratio1,"1");
                Add(trace,"ShearSuperposition.ShearRatio2",ratio2,"1");
                Add(trace,"ShearSuperposition.Sum",rho,"1");
            }
            if(rho>0)
            {
                if(!p.ConservativeShearSuperposition && !p.ConservativeClass4WebInteraction) {
                    Add(trace,"HighShear.VEdOverVplRd",shearRatio,"1");
                    Add(trace,"HighShear.Rho",rho,"1");
                }
                Add(trace,shearTrace+".NormalResistanceFactor",Math.Max(0,1-rho),"1");
                if(rho>=1)
                    return new SteelCheckExecution(decision,SteelExecutionStatus.Failed,
                        p.ConservativeClass4WebInteraction?"Member.Class4WebNormalCapacityExhausted":p.ConservativeShearSuperposition?"Member.ShearSuperpositionNormalCapacityExhausted":"Member.HighShearNormalCapacityExhausted",
                        "The conservative whole-section normal resistance is zero; active compression/bending cannot be carried.",
                        reference:p.ConservativeClass4WebInteraction?"Conservative q=1-|V2|/VbwRd with EN1993-1-5 7.1(4)-(5) web assessment":p.ConservativeShearSuperposition?"Conservative pure-action resistance superposition; no double allocation of shear areas":"EN1993-1-1:2005 6.2.10(3); conservative whole-section reduction",scope:"High shear with active normal actions; section and shear checks remain separate.",trace:trace);
                nb1*=1-rho;nb2*=1-rho;mb*=1-rho;mz*=1-rho;
                Add(trace,shearTrace+".Nb1Rd",nb1,"N");Add(trace,shearTrace+".Nb2Rd",nb2,"N");
                Add(trace,shearTrace+".MbRd",mb,"Nmm");Add(trace,shearTrace+".M2Rd",mz,"Nmm");
            }
            var result=BeamColumnInteraction.Evaluate(compressive/nb1,compressive/nb2,Math.Abs(i.EffectiveActions.M1)/mb,Math.Abs(i.EffectiveActions.M2)/mz,
                lambda1,lambda2,elastic?3:decision.SectionClass.Value,p.TorsionSusceptible,p.Cm1,p.Cm2,p.CmLT,ntc);
            if(rho>0)
            {
                // Reduce fy on the WHOLE section, not just Av (a lower bound of
                // 6.2.10). Original chi is no larger than with the reduced fy.
                // Keep original lambda in increasing-k branches; remove favourable
                // decreasing branches, bounding the weaker uniform-material member.
                result.K11=Math.Max(p.Cm1,result.K11);result.K22=Math.Max(p.Cm2,result.K22);
                result.K12=(elastic?1:.6)*result.K22;
                result.K21=p.TorsionSusceptible?1:(elastic?.8:.6)*result.K11;
                result.Utilization1=compressive/nb1+result.K11*Math.Abs(i.EffectiveActions.M1)/mb+result.K12*Math.Abs(i.EffectiveActions.M2)/mz;
                result.Utilization2=compressive/nb2+result.K21*Math.Abs(i.EffectiveActions.M1)/mb+result.K22*Math.Abs(i.EffectiveActions.M2)/mz;
                result.Utilization=Math.Max(Math.Max(compressive/nb1,compressive/nb2),Math.Max(result.Utilization1,result.Utilization2));
            }
            Add(trace,"ElasticInteraction",elastic?1:0,"1");
            Add(trace,"NRk",nrk,"N");Add(trace,"M1Rk",m1rk,"Nmm");Add(trace,"M2Rk",m2rk,"Nmm");Add(trace,"ChiLT",chiLT,"1");
            Add(trace,"Cm1",p.Cm1,"1");Add(trace,"Cm2",p.Cm2,"1");Add(trace,"CmLT",p.CmLT,"1");
            Add(trace,"K11",result.K11,"1");Add(trace,"K12",result.K12,"1");Add(trace,"K21",result.K21,"1");Add(trace,"K22",result.K22,"1");
            Add(trace,"Interaction1",result.Utilization1,"1");Add(trace,"Interaction2",result.Utilization2,"1");
            double utilization=Math.Max(result.Utilization,compression.Utilization.Value/(1-rho));
            Add(trace,"Utilization",utilization,"1");
            return new SteelCheckExecution(decision,utilization<=1?SteelExecutionStatus.Passed:SteelExecutionStatus.Failed,
                reference:result.Reference+(p.ConservativeClass4WebInteraction?"; conservative q=1-|V2|/VbwRd: stronger than 7.1 penalty; effective properties at original fy, gross eigenvalues, original chi and upper-bound k; same-web panel and subpanel assessment"
                    :p.ConservativeShearSuperposition?"; conservative pure-action resistance superposition q=1-|V1|/Vpl1Rd-|V2|/Vpl2Rd; elastic bending; original chi and upper-bound k, not a normative biaxial-shear equation"
                    :rho>0?(ntc?"; NTC2018 4.2.4.1.2.9 eq4.2.40":"; EN1993-1-1:2005 6.2.10(3)")
                    +", conservative whole-section yield-strength reduction and upper-bound interaction coefficients":"")
                    +(effective?"; EN1993-1-5:2006 4.3(3)/(4), class4 Aeff/Weff at fy; gross critical values":"")
                    +(ltb?.IsRolled==true?"; chiLT: "+SteelLateralTorsionalExecution.Reference(ntc):"")+(ltb!=null?SteelLateralTorsionalExecution.CurveReference(ltb):""),scope:"Doubly symmetric prismatic H/RHS/SHS/CHS classes1-3, qualified H/RHS class4 with zero uniform-compression centroid shift; hollow sections use conservative elastic moduli and elastic interaction coefficients; N constant, M1/M2 maxima from one design state; explicit Cm, global-analysis and mode assessments. Section/shear verification is separate."
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
