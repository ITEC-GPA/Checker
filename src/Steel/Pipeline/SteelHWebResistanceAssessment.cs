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
        /// <summary>Qualified web-only design resistance, N; null for an exclusion-only
        /// assessment. A resistance never sets IsBucklingExcluded to true.</summary>
        public double? BucklingResistance { get; private set; }

        /// <summary>H web, support stiffeners only, conservative non-rigid end post,
        /// no flange contribution. The source must identify separately verified support
        /// stiffeners, attachments and end force transfer; it is not their verification.
        /// gammaM1/eta explicit for EC3; NTC2018 requires 1.05 (1.10 for bridges)/1 respectively.</summary>
        public static SteelShearBucklingAssessment EvaluateNonRigidHWeb(SteelVerificationInput input,
            double gammaM1,double eta,string supportDetailSource)
        {
            if(input==null)throw new ArgumentNullException(nameof(input));
            if(input.Section.Unperforated!=true)throw new NotSupportedException("Unperforated web declaration required.");
            if(Math.Abs(input.Section.ElasticModulus-210000)>1e-6||Math.Abs(input.Section.PoissonRatio-.3)>1e-10)
                throw new NotSupportedException("The 86.4 web slenderness coefficient requires E=210000 MPa and nu=0.3.");
            var section=input.Section.ReadOnlySection;SteelMethodSelector.ValidateGeometry(section);
            if(section.SectionShape.GetType()!=typeof(SectionH)||Math.Abs(section.AngleX1)>1e-10||SteelMethodSelector.ColdFormedOpen(section))
                throw new NotSupportedException("Exact H with horizontal principal major axis required for web resistance.");
            bool ntc=input.Code.SectionCode.Standard==SectionResistanceStandard.Ntc2018;
            if(ntc&&(!Ntc2018DesignPolicy.IsGammaM1(gammaM1)||eta!=1))throw new ArgumentException("NTC web resistance requires gammaM1=1.05 (1.10 for bridges) and eta=1.");
            var h=(SectionH)section.SectionShape;
            double vb=Ec3WebShearBuckling.NonRigidEndPostResistance(h.HeightWeb,h.ThicknessWeb,input.Section.YieldStrength,gammaM1,eta);
            double vpl=ModelShearAreas.GetArea2(section,input.Code.SectionCode.Standard)*input.Section.YieldStrength/(Math.Sqrt(3)*input.Code.SectionCode.GammaM0);
            double resistance=Math.Min(vb,vpl);SteelInputValidation.Positive(resistance,"web design resistance");
            return new SteelShearBucklingAssessment(input,2,false,supportDetailSource,
                (ntc?"NTC 2018 4.2.4.1.2.4; ":"")+"EN1993-1-5:2006 5.2/5.3, Table5.1, equation5.5; web only; support detail assessed externally",
                new[]{new SectionResistanceValue("ClearWebHeight",h.HeightWeb,"mm"),new SectionResistanceValue("WebThickness",h.ThicknessWeb,"mm"),
                    new SectionResistanceValue("GammaM1",gammaM1,"1"),new SectionResistanceValue("Eta",eta,"1"),
                    new SectionResistanceValue("VbwRd",vb,"N"),new SectionResistanceValue("VplRd",vpl,"N"),
                    new SectionResistanceValue("ShearDesignResistance",resistance,"N")}) {BucklingResistance=resistance};
        }
    }
}
