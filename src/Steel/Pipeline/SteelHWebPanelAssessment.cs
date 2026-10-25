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
        /// <summary>Web-only shear resistance between qualified transverse stiffeners.
        /// Source covers rigidity, strength, attachments, supports and end force transfer
        /// for this same state. This calculation does NOT verify those details.</summary>
        public static SteelShearBucklingAssessment EvaluateTransverselyStiffenedHWeb(SteelVerificationInput input,
            double panelLength, bool rigidEndPost, double gammaM1, double eta, string stiffenerDetailSource)
        {
            if(input==null)throw new ArgumentNullException(nameof(input));
            SteelInputValidation.Positive(panelLength,nameof(panelLength));
            if(input.Section.Unperforated!=true||Math.Abs(input.Section.PoissonRatio-.3)>1e-10)
                throw new NotSupportedException("Unperforated web and nu=.3 required.");
            var section=input.Section.CreateSectionCopy();SteelMethodSelector.ValidateGeometry(section);
            if(section.SectionShape.GetType()!=typeof(SectionH)||Math.Abs(section.AngleX1)>1e-10||SteelMethodSelector.ColdFormedOpen(section))
                throw new NotSupportedException("Exact H with horizontal principal major axis required.");
            if(input.Member!=null&&panelLength>input.Member.Length)throw new ArgumentException("Panel exceeds physical member length.");
            bool ntc=input.Code.SectionCode.Standard==SectionResistanceStandard.Ntc2018;
            if(ntc&&(gammaM1!=Ntc2018DesignPolicy.GammaM1||eta!=1))throw new ArgumentException("NTC requires gammaM1=1.05 and eta=1.");
            var h=(SectionH)section.SectionShape;
            var p=Ec3WebPanelShear.Calculate(h.HeightWeb,h.ThicknessWeb,input.Section.YieldStrength,input.Section.ElasticModulus,
                input.Code.SectionCode.GammaM0,gammaM1,eta,panelLength,rigidEndPost);
            double vpl=ModelShearAreas.GetArea2(section,input.Code.SectionCode.Standard)*input.Section.YieldStrength/(Math.Sqrt(3)*input.Code.SectionCode.GammaM0);
            double resistance=Math.Min(vpl,p.BucklingResistance);
            SteelInputValidation.Positive(resistance,"web resistance");
            return new SteelShearBucklingAssessment(input,2,false,stiffenerDetailSource,
                (ntc?"NTC2018 with EN1993-1-5 supplementary web-panel method; ":"")+"EN1993-1-5:2006 5.2/5.3, Table5.1, AnnexA.3; stiffeners and end post checked externally",
                new[]{V("ClearWebHeight",h.HeightWeb,"mm"),V("WebThickness",h.ThicknessWeb,"mm"),V("PanelLength",panelLength,"mm"),
                    V("KTau",p.KTau,"1"),V("TauCritical",p.TauCritical,"MPa"),V("LambdaWeb",p.Slenderness,"1"),V("ChiWeb",p.Chi,"1"),
                    V("RigidEndPost",rigidEndPost?1:0,"1"),V("GammaM1",gammaM1,"1"),V("Eta",eta,"1"),
                    V("VbwRd",p.BucklingResistance,"N"),V("VplRd",vpl,"N"),V("ShearDesignResistance",resistance,"N")}){BucklingResistance=resistance};
        }
        private static SectionResistanceValue V(string name,double value,string unit)=>new SectionResistanceValue(name,value,unit);
    }
}
