using System;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.Pipeline
{
    public sealed partial class SteelShearBucklingAssessment
    {
        /// <summary>Computed RHS web contribution only, not a qualification of the
        /// entire shear field for simultaneous shear/normal-stress interaction.</summary>
        public bool RhsWebsOnly { get; private set; }

        /// <summary>Uniform RHS, one principal shear direction, non-rigid end posts,
        /// eta=1, no flange contribution. Rounded walls must be concentric. Uses
        /// SSAB Domex Tube (2016) 2.7.1.2, eqs2.79-2.80 / EN1993-1-5 5.3.
        /// c=max(d-3t,actual flat width); sharp fabricated walls use d-2t.
        /// The reference must identify separately verified support stiffeners,
        /// attachments and force introduction. No patch loading, holes, torque,
        /// normal interaction, combined shears or member stability is qualified.
        /// The existing common shear-area mapping is required (rounded: hot finished).</summary>
        public static SteelShearBucklingAssessment EvaluateNonRigidRhsWebs(
            SteelVerificationInput input,int axis,double gammaM1,string supportDetailSource)
        {
            if(input==null)throw new ArgumentNullException(nameof(input));
            if(axis!=1&&axis!=2)throw new ArgumentOutOfRangeException(nameof(axis));
            var section=input.Section.ReadOnlySection;
            SteelMethodSelector.ValidateGeometry(section);
            if(!SteelSectionFamilies.Is(section.SectionShape,SteelSectionKind.RectangularTube)
                || !SteelSectionFamilies.HasQualifiedShearAreas(section)
                || input.Section.Unperforated!=true || input.EffectiveActions.T!=0)
                throw new NotSupportedException("Unperforated RHS with qualified principal shear axes and no torque required.");
            if(Math.Abs(input.Section.ElasticModulus-210000)>1e-6 || Math.Abs(input.Section.PoissonRatio-.3)>1e-10)
                throw new NotSupportedException("The 86.4 web coefficient requires E=210000 MPa and nu=0.3.");
            CheckPlateFactor(input,gammaM1);
            var rhs=(SectionRHS)section.SectionShape;
            double t=rhs.ThicknessTop;
            if(rhs.ThicknessBottom!=t||rhs.ThicknessWebLeft!=t||rhs.ThicknessWebRight!=t)
                throw new NotSupportedException("This RHS web model requires uniform wall thickness.");
            double d=axis==2?rhs.Height:rhs.Base;
            double flat=d-2*t;
            double panel=flat;
            if(rhs is SectionRHSRoundedCorners rounded) {
                if(Math.Abs(rounded.OutsideRadius-rounded.InsideRadius-t)>1e-9*t)
                    throw new NotSupportedException("Concentric corner radii and uniform thickness required.");
                flat=d-2*rounded.OutsideRadius;
                // Do not gain resistance by selecting a radius smaller than the
                // nominal 1.5t used in the handbook's d-3t panel width.
                panel=Math.Max(d-3*t,flat);
            }
            SteelInputValidation.Positive(panel,"RHS shear-buckling panel width");
            double av=SteelPlasticShear.Area(input,axis),fy=input.Section.YieldStrength;
            // Fabricated sharp boxes: keep only the two clear web panels.
            // The larger plastic flange area under V1 is not a buckling area.
            if(section.SectionType==Section.SectionTypes.Welded)av=Math.Min(av,2*panel*t);
            double vb=Ec3WebShearBuckling.NonRigidEndPostResistance(panel,t,fy,gammaM1,1)*av/(panel*t);
            double vpl=SteelPlasticShear.Resistance(input,axis),resistance=Math.Min(vb,vpl);
            SteelInputValidation.Positive(resistance,"RHS web design resistance");
            bool ntc=input.Code.SectionCode.Standard==SectionResistanceStandard.Ntc2018;
            return new SteelShearBucklingAssessment(input,axis,false,supportDetailSource,
                (ntc?"NTC2018 4.2.4.1.2.4 / chapter12; ":"")+"EN1993-1-5:2006 5.3 Table5.1; SSAB Domex Tube2016 2.7.1.2 eqs2.79-2.80; RHS web contribution only",
                new[]{new SectionResistanceValue("ShearAxis",axis,"1"),
                    new SectionResistanceValue("OuterDepth",d,"mm"),new SectionResistanceValue("FlatWidth",flat,"mm"),
                    new SectionResistanceValue("BucklingPanelWidth",panel,"mm"),new SectionResistanceValue("WallThickness",t,"mm"),
                    new SectionResistanceValue("ShearArea",av,"mm2"),new SectionResistanceValue("Eta",1,"1"),
                    new SectionResistanceValue("WebSlenderness",panel/(86.4*t*Math.Sqrt(235/fy)),"1"),
                    new SectionResistanceValue("GammaM1",gammaM1,"1"),new SectionResistanceValue("VbwRd",vb,"N"),
                    new SectionResistanceValue("VplRd",vpl,"N"),new SectionResistanceValue("ShearDesignResistance",resistance,"N")})
                {BucklingResistance=resistance,RhsWebsOnly=true};
        }
    }
}
