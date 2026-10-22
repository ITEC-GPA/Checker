using System;
using GPC.Checkers.Steel.Inputs;

namespace GPC.Checkers.Steel.EffectiveSections
{
    /// <summary>Unstiffened plate only; mm/MPa, negative compression. EffectiveAtStart/End
    /// include any adjacent tension zone. Removed material is the interval between them.</summary>
    public sealed class EffectivePlate
    {
        public double Width { get; }
        public double Thickness { get; }
        public double Psi { get; }
        public double KSigma { get; }
        public double Lambda { get; }
        public double Rho { get; }
        public double CompressedWidth { get; }
        public double EffectiveAtStart { get; }
        public double EffectiveAtEnd { get; }
        public double StartStress { get; }
        public double EndStress { get; }
        public bool StressRatioBounded { get; }
        internal EffectivePlate(double b,double t,double psi,double k,double lambda,double rho,double bc,
            double start,double end,double s1,double s2,bool bounded=false)
        {
            foreach(var v in new[]{psi,k,lambda,rho,bc,start,end})SteelInputValidation.Finite(v,"effective plate result");
            Width=b;Thickness=t;Psi=psi;KSigma=k;Lambda=lambda;Rho=rho;CompressedWidth=bc;
            EffectiveAtStart=start;EffectiveAtEnd=end;StartStress=s1;EndStress=s2;StressRatioBounded=bounded;
        }
    }

    /// <summary>EN1993-1-5:2006 (AC2009), 4.4, Tables4.1/4.2; E=210000MPa, nu=.3.
    /// Plate-local buckling only; no stiffener, shear lag, shear buckling or shell method.
    /// Common kernel used by Steel and CompositeBridge. No load phases or Model geometry here.</summary>
    public static class EffectivePlateReduction
    {
        public static EffectivePlate InternalPlate(double b,double t,double startStress,double endStress,double fy)
        {
            Validate(b,t,startStress,endStress,fy);
            bool startCompressed=startStress<=endStress;
            double s1=Math.Min(startStress,endStress),s2=Math.Max(startStress,endStress);
            if(s1>=-1e-9||b==0)return new EffectivePlate(b,t,0,0,0,1,0,b/2,b/2,startStress,endStress);
            double psi=s2/s1,bounded=Clamp(psi,-3,1);
            // Use the explicitly tabulated psi=0 value, not the rounded rational limit.
            double k=bounded==-1?23.9:bounded==0?7.81:bounded>0?8.2/(1.05+bounded):bounded>=-1?7.81-6.29*bounded+9.78*bounded*bounded:5.98*Math.Pow(1-bounded,2);
            double lambda=Slenderness(b,t,fy,k),limit=.5+Math.Sqrt(.085-.055*bounded);
            double rho=lambda<=limit?1:Clamp((lambda-.055*(3+bounded))/(lambda*lambda),0,1);
            double bc=psi<0?b/(1-psi):b,beff=rho*bc;
            double first=psi<0?.4*beff:2*beff/(5-psi),second=beff-first+b-bc;
            return new EffectivePlate(b,t,psi,k,lambda,rho,bc,startCompressed?first:second,startCompressed?second:first,startStress,endStress,psi!=bounded);
        }

        public static EffectivePlate Outstand(double b,double t,double stress,double fy)
            => Outstand(b,t,stress,stress,fy);

        /// <summary>The start edge is supported, the end edge is free. Stress may vary
        /// linearly across the width. Outside the tabulated psi interval, use its least
        /// beneficial endpoint k; keep the actual tension/compression boundary and report it.</summary>
        public static EffectivePlate Outstand(double b,double t,double supportStress,double freeStress,double fy)
        {
            Validate(b,t,supportStress,freeStress,fy);
            double s1=Math.Min(supportStress,freeStress),s2=Math.Max(supportStress,freeStress);
            if(s1>=-1e-9||b==0)return new EffectivePlate(b,t,1,.43,Slenderness(b,t,fy,.43),1,s1<0?b:0,b,0,supportStress,freeStress);
            bool supportCompressed=supportStress<=freeStress;
            double psi=s2/s1,bounded=Clamp(psi,supportCompressed?-1:-3,1);
            double k=bounded==1?.43:!supportCompressed?.57-.21*bounded+.07*bounded*bounded:
                bounded>0?.578/(bounded+.34):1.7-5*bounded+17.1*bounded*bounded;
            double lambda=Slenderness(b,t,fy,k),rho=lambda<=.748?1:Clamp((lambda-.188)/(lambda*lambda),0,1);
            double bc=psi<0?b/(1-psi):b,bt=b-bc,beff=rho*bc;
            // With maximum compression at the free edge, the effective compression
            // strip is adjacent to the supported side / tension zone (Table4.2 sketch).
            return new EffectivePlate(b,t,psi,k,lambda,rho,bc,supportCompressed?beff:bt+beff,supportCompressed?bt:0,
                supportStress,freeStress,psi!=bounded);
        }
        private static void Validate(double b,double t,double a,double z,double fy)
        {
            SteelInputValidation.Finite(b,nameof(b));if(b<0)throw new ArgumentOutOfRangeException(nameof(b));
            SteelInputValidation.Positive(t,nameof(t));SteelInputValidation.Positive(fy,nameof(fy));
            SteelInputValidation.Finite(a,"start stress");SteelInputValidation.Finite(z,"end stress");
        }
        private static double Slenderness(double b,double t,double fy,double k)
        {double value=b/t/(28.4*Math.Sqrt(235/fy)*Math.Sqrt(k));SteelInputValidation.Finite(value,"plate slenderness");return value;}
        private static double Clamp(double v,double lo,double hi)=>Math.Max(lo,Math.Min(hi,v));
    }
}
