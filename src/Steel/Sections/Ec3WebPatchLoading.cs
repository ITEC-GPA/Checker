using System;

namespace GPC.Checkers.Steel.Sections
{
    public enum WebPatchLoadType { OneFlangeReactedByShear, OppositeFlanges }
    public sealed class WebPatchLoadingResistance
    {
        public double Kf { get; }
        public double Fcr { get; }
        public double M1 { get; }
        public double M2 { get; }
        public double LoadedLength { get; }
        public double Slenderness { get; }
        public double Chi { get; }
        public double EffectiveLength => Chi*LoadedLength;
        public double Resistance { get; }
        internal WebPatchLoadingResistance(double kf,double fcr,double m1,double m2,double ly,double lambda,double chi,double resistance)
        {Kf=kf;Fcr=fcr;M1=m1;M2=m2;LoadedLength=ly;Slenderness=lambda;Chi=chi;Resistance=resistance;}
    }

    /// <summary>EN1993-1-5:2006 section6, Figure6.1 types(a)/(b). Planar web without
    /// longitudinal stiffeners, perpendicular load through a laterally restrained flange,
    /// effective stiff bearing length ss already established under6.3. Adjacent transverse
    /// stiffeners qualify the panel. No free-end type(c), multiple interacting patches,
    /// openings, flange bending interaction or whole-member qualification in this kernel.
    /// Units mm/N/MPa; gammaM1 explicit. For boxes bf must already satisfy6.5(1).</summary>
    public static class Ec3WebPatchLoading
    {
        public static WebPatchLoadingResistance Calculate(WebPatchLoadType type,double webHeight,double webThickness,
            double flangeWidth,double flangeThickness,double webFy,double flangeFy,double young,
            double panelLength,double stiffBearingLength,double gammaM1)
        {
            if(!Enum.IsDefined(typeof(WebPatchLoadType),type))throw new ArgumentOutOfRangeException(nameof(type));
            foreach(double v in new[]{webHeight,webThickness,flangeWidth,flangeThickness,webFy,flangeFy,young,panelLength,gammaM1})Positive(v);
            if(double.IsNaN(stiffBearingLength)||double.IsInfinity(stiffBearingLength)||stiffBearingLength<0
                ||stiffBearingLength>webHeight||stiffBearingLength>panelLength)
                throw new ArgumentException("Stiff bearing length must be within0..min(hw,a), after6.3 distribution.");
            double kf=(type==WebPatchLoadType.OneFlangeReactedByShear?6:3.5)+2*Math.Pow(webHeight/panelLength,2);
            double fcr=.9*kf*young*Math.Pow(webThickness,3)/webHeight;
            double m1=flangeFy*flangeWidth/(webFy*webThickness);
            double ly=Math.Min(panelLength,stiffBearingLength+2*flangeThickness*(1+Math.Sqrt(m1)));
            double lambda=Math.Sqrt(ly*webThickness*webFy/fcr),m2=0;
            // Start with m2=0. If lambda<=.5 this is a self-consistent, conservative
            // branch even when the other algebraic branch could also be admissible.
            if(lambda>.5) {
                m2=.02*Math.Pow(webHeight/flangeThickness,2);
                ly=Math.Min(panelLength,stiffBearingLength+2*flangeThickness*(1+Math.Sqrt(m1+m2)));
                lambda=Math.Sqrt(ly*webThickness*webFy/fcr);
            }
            double chi=Math.Min(1,.5/lambda),resistance=chi*ly*webThickness*webFy/gammaM1;
            foreach(double v in new[]{kf,fcr,m1,ly,lambda,chi,resistance})
                if(!(v>0)||double.IsInfinity(v))throw new ArithmeticException("Patch loading outside finite numerical range.");
            return new WebPatchLoadingResistance(kf,fcr,m1,m2,ly,lambda,chi,resistance);
        }

        /// <summary>Normalized governing utilization including both individual checks
        /// and EN1993-1-5:2006 7.2: etaF+.8*etaNM<=1.4. etaNM belongs to the SAME
        /// concomitant state and its applicable section resistance, not Mpl for class4.
        /// Shear, attachments and member stability remain separate.</summary>
        public static double Interaction(double etaF,double etaNM)
        {
            if(!(etaF>=0)||!(etaNM>=0)||double.IsInfinity(etaF)||double.IsInfinity(etaNM))
                throw new ArgumentException("Finite non-negative same-state utilizations required.");
            double value=Math.Max(Math.Max(etaF,etaNM),(etaF+.8*etaNM)/1.4);
            if(double.IsInfinity(value))throw new ArithmeticException("Patch interaction outside finite range.");
            return value;
        }
        private static void Positive(double v)
        {if(!(v>0)||double.IsInfinity(v))throw new ArgumentException("Positive finite patch property/factor required.");}
    }
}
