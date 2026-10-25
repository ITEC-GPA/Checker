using System;

namespace GPC.Checkers.Steel.Sections
{
    public sealed class WebPanelShearResistance
    {
        public double KTau { get; }
        public double TauCritical { get; }
        public double Slenderness { get; }
        public double Chi { get; }
        public double Area { get; }
        public double ShearArea { get; }
        public double PlasticResistance { get; }
        public double BucklingResistance { get; }
        public double Resistance => Math.Min(PlasticResistance, BucklingResistance);
        internal WebPanelShearResistance(double kt, double tau, double lambda, double chi,
            double area, double shearArea, double plastic, double buckling)
        { KTau=kt; TauCritical=tau; Slenderness=lambda; Chi=chi; Area=area; ShearArea=shearArea; PlasticResistance=plastic; BucklingResistance=buckling; }
    }

    /// <summary>Common Steel/CompositeBridge web-panel kernel. EN1993-1-5:2006
    /// 5.2/5.3, Table5.1 and AnnexA.3, nu=.3. N/mm/MPa. No longitudinal stiffeners,
    /// openings or flange contribution. Transverse stiffeners and rigid end posts
    /// must be separately qualified; geometry alone does not establish rigidity.</summary>
    public static class Ec3WebPanelShear
    {
        public static WebPanelShearResistance Calculate(double height, double thickness, double fy, double young,
            double gammaM0, double gammaM1, double eta, double panelLength=0,
            bool rigidEndPost=false, bool etaInShearArea=false)
        {
            foreach(double v in new[]{height,thickness,fy,young,gammaM0,gammaM1}) Positive(v);
            if(double.IsNaN(panelLength)||double.IsInfinity(panelLength)||panelLength<0)throw new ArgumentException("Invalid panel length.");
            if(!(eta>=1&&eta<=1.2))throw new ArgumentException("eta must be between1 and1.2.");
            double ratio=panelLength/height;
            double kt=panelLength==0?5.34:ratio>=1?5.34+4/(ratio*ratio):4+5.34/(ratio*ratio);
            double sigmaE=Math.PI*Math.PI*young/(12*(1-.3*.3))*Math.Pow(thickness/height,2);
            double tauCr=kt*sigmaE, lambda=Math.Sqrt(fy/(Math.Sqrt(3)*tauCr));
            double chi=lambda<.83/eta?eta:rigidEndPost&&lambda>=1.08?1.37/(.7+lambda):.83/lambda;
            double area=height*thickness;
            double plastic=(etaInShearArea?eta:1)*area*fy/(Math.Sqrt(3)*gammaM0);
            double buckling=chi*area*fy/(Math.Sqrt(3)*gammaM1);
            foreach(double v in new[]{kt,tauCr,lambda,chi,area,plastic,buckling})
                if(!(v>0)||double.IsInfinity(v))throw new ArithmeticException("Web panel outside finite numerical range.");
            return new WebPanelShearResistance(kt,tauCr,lambda,chi,area,(etaInShearArea?eta:1)*area,plastic,buckling);
        }
        private static void Positive(double v)
        { if(!(v>0)||double.IsInfinity(v))throw new ArgumentException("Positive finite web property/factor required."); }
    }
}
