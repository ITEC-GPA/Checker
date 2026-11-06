using System;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.Pipeline
{
    public sealed partial class SteelShearBucklingAssessment
    {
        /// <summary>Typed qualification for the conservative class4 sum of shear
        /// ratios. Each resistance bounds its entire shear field; sharing this
        /// property does not turn an external buckling exclusion into a model.</summary>
        public bool ConservativePlateNormalInteraction { get; private set; }

        public static SteelShearBucklingAssessment EvaluateClass4PlateShear(SteelVerificationInput input,
            int axis,double gammaM1,string supportDetailSource)
        {
            if(input==null)throw new ArgumentNullException(nameof(input));
            if(axis!=1&&axis!=2)throw new ArgumentOutOfRangeException(nameof(axis));
            if(input.Section.GeometryType==typeof(SectionRHS))
                return EvaluateClass4RhsWalls(input,axis,gammaM1,supportDetailSource);
            if(input.Section.GeometryType!=typeof(SectionH))
                throw new NotSupportedException("All-plate interaction is qualified for H with compact flange outstands and sharp RHS with equal opposite walls only.");
            CheckPlateFactor(input,gammaM1);
            return axis==1?EvaluateClass4HFlanges(input,supportDetailSource)
                :EvaluateNonRigidHWeb(input,gammaM1,1,supportDetailSource).WithConservativeBiaxialNormalInteraction();
        }

        /// <summary>H web with continuous flange edges, no longitudinal stiffeners
        /// or patch load, including biaxial normal stress. Flange outstands must
        /// stay within the existing compact-flange shear screen. The input may
        /// have unequal flanges under V2 only: this qualifies a SECTION, not
        /// asymmetric Annex B. Peak web/flange stresses and the elastic shear
        /// critical cap prevent reliance on unspecified postcritical anchorage.</summary>
        public SteelShearBucklingAssessment WithConservativeBiaxialNormalInteraction()
        {
            if(ConservativePlateNormalInteraction)return this;
            var s=Input.Section.ReadOnlySection;
            if(Axis!=2 || !BucklingResistance.HasValue || s.SectionShape.GetType()!=typeof(SectionH)
                || Math.Abs(s.AngleX1)>1e-10 || Input.EffectiveActions.T!=0)
                throw new NotSupportedException("Computed H-web resistance and aligned axes required.");
            if(!EvaluateHFlanges(Input,"Compact-flange domain of the biaxial plate model").IsBucklingExcluded)
                throw new NotSupportedException("Slender flange outstands need their own coupled shear-buckling model.");
            CheckPlateFactor(Input,Trace.Single(v=>v.Name=="GammaM1").Value);
            var h=(SectionH)s.SectionShape;
            if(h.R!=0)throw new NotSupportedException("The plate shear-flow model requires a sharp fabricated H.");
            double gammaM1=Trace.Single(v=>v.Name=="GammaM1").Value;
            double hw=h.HeightWeb,tw=h.ThicknessWeb,cy=h.DistanceYCentroidFromBottom();
            double top=h.Height-h.ThicknessTopFlange/2,bottom=h.ThicknessBottomFlange/2;
            double cut=Math.Max(h.ThicknessBottomFlange,Math.Min(h.Height-h.ThicknessTopFlange,cy));
            double qmax=h.LenghtTopFlange*h.ThicknessTopFlange*(top-cy)
                +tw*(Math.Pow(h.Height-h.ThicknessTopFlange-cy,2)-Math.Pow(cut-cy,2))/2;
            double webPeakFactor=Math.Max(1,hw*qmax/s.J11);
            double tauCr=Ec3WebPanelShear.Calculate(hw,tw,Input.Section.YieldStrength,
                Input.Section.ElasticModulus,Input.Code.SectionCode.GammaM0,gammaM1,1).TauCritical;
            double webResistance=Math.Min(Math.Min(BucklingResistance.Value,tauCr*hw*tw/gammaM1),
                hw*tw*Input.Section.YieldStrength/(Math.Sqrt(3)*Input.Code.SectionCode.GammaM0))/webPeakFactor;
            double flangeStressPerForce=Math.Max(h.LenghtTopFlange*Math.Abs(top-cy),
                h.LenghtBottomFlange*Math.Abs(bottom-cy))/(2*s.J11);
            double flangeResistance=Input.Section.YieldStrength/(Math.Sqrt(3)*Input.Code.SectionCode.GammaM0*flangeStressPerForce);
            double resistance=Math.Min(webResistance,flangeResistance);
            SteelInputValidation.Positive(resistance,"H all-plate shear resistance");
            return new SteelShearBucklingAssessment(Input,2,false,Source,
                Reference+"; all-plate peak shear-flow bound; elastic critical cap; flange shear included",
                Trace.Where(v=>v.Name!="ShearDesignResistance").Concat(new[]{
                    new SectionResistanceValue("WebPeakToAverageShear",webPeakFactor,"1"),
                    new SectionResistanceValue("FlangeShearResistance",flangeResistance,"N"),
                    new SectionResistanceValue("ShearDesignResistance",resistance,"N")}))
                {BucklingResistance=resistance,ConservativePlateNormalInteraction=true};
        }

        public static SteelShearBucklingAssessment EvaluateClass4HFlanges(SteelVerificationInput input,string source)
        {
            if(input==null)throw new ArgumentNullException(nameof(input));
            if(input.EffectiveActions.T!=0 || input.Section.Unperforated!=true)
                throw new NotSupportedException("Unperforated H without applied torque required.");
            var result=EvaluateHFlanges(input,source);
            if(!result.IsBucklingExcluded)
                throw new NotSupportedException("Slender flange outstands are outside the compact-flange interaction.");
            var s=input.Section.ReadOnlySection;var h=(SectionH)s.SectionShape;
            if(h.R!=0 || h.LenghtTopFlange!=h.LenghtBottomFlange || h.ThicknessTopFlange!=h.ThicknessBottomFlange)
                throw new NotSupportedException("Minor-axis shear requires a sharp doubly symmetric H: an unequal-flange shear-centre offset cannot be ignored.");
            double resistance=Math.Min(SteelPlasticShear.Resistance(input,1),
                input.Section.YieldStrength*8*s.J22/(Math.Sqrt(3)*input.Code.SectionCode.GammaM0*h.LenghtTopFlange*h.LenghtTopFlange));
            SteelInputValidation.Positive(resistance,"H flange peak shear resistance");
            return new SteelShearBucklingAssessment(input,1,false,source,result.Reference+"; elastic flange peak shear-flow bound",
                result.Trace.Concat(new[]{new SectionResistanceValue("ShearDesignResistance",resistance,"N")}))
                {BucklingResistance=resistance,ConservativePlateNormalInteraction=true};
        }

        /// <summary>Sharp-corner RHS with equal opposite walls; shear through its centroid/
        /// shear centre. ALL FOUR continuous walls are checked as unstiffened
        /// simply supported panels, k_tau=5.34, non-rigid end posts, eta=1, no
        /// flange contribution or tension-field credit above elastic bifurcation.
        /// Bound the peak shear FLOW everywhere by 1.5 |Vi|/(2 clearDimension_i).
        /// Using min(tauRdi*ti) of the walls and summing both direction ratios bounds the
        /// entire combined field, including the walls normal to either shear.
        /// Source identifies separately checked end diaphragms/force transfer;
        /// it is not a verification of these supports.</summary>
        public static SteelShearBucklingAssessment EvaluateClass4RhsWalls(SteelVerificationInput input,
            int axis,double gammaM1,string supportDetailSource)
        {
            if(input==null)throw new ArgumentNullException(nameof(input));
            if(axis!=1&&axis!=2)throw new ArgumentOutOfRangeException(nameof(axis));
            var s=input.Section.ReadOnlySection;SteelMethodSelector.ValidateGeometry(s);
            if(s.SectionShape.GetType()!=typeof(SectionRHS) || Math.Abs(s.AngleX1)>1e-10
                || input.Section.Unperforated!=true || input.EffectiveActions.T!=0
                || Math.Abs(input.Section.PoissonRatio-.3)>1e-10)
                throw new NotSupportedException("Unperforated sharp RHS, principal axis1 horizontal, nu=.3 and no applied torque required.");
            var h=(SectionRHS)s.SectionShape;double tf=h.ThicknessTop,tw=h.ThicknessWebLeft,t=Math.Min(tf,tw);
            if(h.R!=0 || h.ThicknessBottom!=tf || h.ThicknessWebRight!=tw)
                throw new NotSupportedException("The all-wall shear-flow bound requires equal opposite walls and sharp corners; asymmetric shear-centre offsets are not included.");
            if(tf!=tw && s.SectionType!=Section.SectionTypes.Welded)
                throw new NotSupportedException("Different flange/web thickness requires the fabricated welded-box section type.");
            CheckPlateFactor(input,gammaM1);
            double b=h.BaseInternal,d=h.Heightinternal,fy=input.Section.YieldStrength;
            double gm0=input.Code.SectionCode.GammaM0;
            WebPanelShearResistance Panel(double width,double thickness)=>Ec3WebPanelShear.Calculate(width,thickness,fy,input.Section.ElasticModulus,gm0,gammaM1,1);
            var horizontal=Panel(b,tf);var vertical=Panel(d,tw);
            // No postcritical anchorage inferred from a closed cross-section.
            double Tau(WebPanelShearResistance p)=>Math.Min(p.Resistance/p.Area,p.TauCritical/gammaM1);
            double tau=Math.Min(Tau(horizontal),Tau(vertical));
            double lineResistance=Math.Min(Tau(horizontal)*tf,Tau(vertical)*tw);
            double bound=lineResistance*2*(axis==1?b:d)/1.5;
            double resistance=Math.Min(bound,SteelPlasticShear.Resistance(input,axis));
            SteelInputValidation.Positive(resistance,"all-wall shear resistance");
            return new SteelShearBucklingAssessment(input,axis,false,supportDetailSource,
                "EN1993-1-5:2006 5.3/7.1(5); all four unstiffened continuous RHS walls; maximum shear-flow bound; elastic critical cap",
                new[]{new SectionResistanceValue("GammaM1",gammaM1,"1"),
                    new SectionResistanceValue("ClearWidth",b,"mm"),new SectionResistanceValue("ClearHeight",d,"mm"),
                    new SectionResistanceValue("WallThickness",t,"mm"),new SectionResistanceValue("CheckedWalls",4,"1"),
                    new SectionResistanceValue("HorizontalWallThickness",tf,"mm"),new SectionResistanceValue("VerticalWallThickness",tw,"mm"),
                    new SectionResistanceValue("HorizontalWallTauCritical",horizontal.TauCritical,"MPa"),
                    new SectionResistanceValue("VerticalWallTauCritical",vertical.TauCritical,"MPa"),
                    new SectionResistanceValue("AllWallShearStressResistance",tau,"MPa"),
                    new SectionResistanceValue("AllWallShearFlowResistance",lineResistance,"N/mm"),
                    new SectionResistanceValue("PeakToNominalWebShearBound",1.5,"1"),
                    new SectionResistanceValue("ShearDesignResistance",resistance,"N")})
                {BucklingResistance=resistance,ConservativePlateNormalInteraction=true};
        }

        private static void CheckPlateFactor(SteelVerificationInput input,double gammaM1)
        {
            SteelInputValidation.Positive(gammaM1,nameof(gammaM1));
            if(input.Code.GammaM1.HasValue && input.Code.GammaM1.Value!=gammaM1)
                throw new ArgumentException("Plate and member gammaM1 must agree.");
            if(input.Code.SectionCode.Standard==SectionResistanceStandard.Ntc2018 && !Ntc2018DesignPolicy.IsGammaM1(gammaM1))
                throw new ArgumentException("NTC plate resistance requires gammaM1=1.05 or1.10 for bridges.");
        }
    }
}
