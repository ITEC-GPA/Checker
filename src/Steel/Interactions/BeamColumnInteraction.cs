using System;
using GPC.Checkers.Steel.Inputs;

namespace GPC.Checkers.Steel.Members
{
    public sealed class BeamColumnInteractionResult
    {
        public double K11 { get; internal set; }
        public double K12 { get; internal set; }
        public double K21 { get; internal set; }
        public double K22 { get; internal set; }
        public double Utilization1 { get; internal set; }
        public double Utilization2 { get; internal set; }
        public double Utilization { get; internal set; }
        public string Reference { get; internal set; }
    }

    /// <summary>Scalar H beam-column interaction only. The caller owns section class,
    /// design capacities, global analysis and method applicability. Ratios n1/n2 are
    /// NEd/(chi_i NRk/gammaM1), m1 is |M1|max/(chiLT M1Rk/gammaM1), and m2 is
    /// |M2|max/(M2Rk/gammaM1); all nonnegative, from ONE whole-member load state.
    /// This arithmetic API is not a complete member or section verification.</summary>
    public static class BeamColumnInteraction
    {
        public static BeamColumnInteractionResult Evaluate(double n1,double n2,double m1,double m2,
            double slenderness1,double slenderness2,int sectionClass,bool torsionSusceptible,
            double cm1,double cm2,double cmLT,bool ntc2018=false)
        {
            foreach(var value in new[]{n1,n2,m1,m2,slenderness1,slenderness2})
            { SteelInputValidation.Finite(value,"Interaction input");if(value<0)throw new ArgumentOutOfRangeException("Interaction input"); }
            if(sectionClass<1||sectionClass>3)throw new ArgumentOutOfRangeException(nameof(sectionClass));
            foreach(var cm in new[]{cm1,cm2,cmLT})SteelBeamColumnParameters.ValidateMomentFactor(cm);
            bool plastic=sectionClass<=2;
            double k11=cm1*(1+Math.Min(plastic?slenderness1-.2:.6*slenderness1,plastic?.8:.6)*n1);
            double k22=cm2*(1+Math.Min(plastic?2*slenderness2-.6:.6*slenderness2,plastic?1.4:.6)*n2);
            // Circular 2019 Table C4.2.IV prints y in the kzz row; EC3 B.1 and
            // the Circular's C4.2.V use z. For torsion-restrained H retain the larger
            // coefficient: demand is no smaller than with either reading, without
            // treating a suspected index typo as an official normative correction.
            if(ntc2018&&!torsionSusceptible)
                k22=Math.Max(k22,cm2*(1+Math.Min(plastic?2*slenderness1-.6:.6*slenderness1,plastic?1.4:.6)*n1));
            double k12=(plastic?.6:1)*k22;
            double k21;
            if(!torsionSusceptible)k21=(plastic?.6:.8)*k11;
            else if(plastic&&slenderness2<.4)
                k21=Math.Min(.6+slenderness2,1-.1*slenderness2*n2/(cmLT-.25));
            else k21=Math.Max(1-(plastic?.1:.05)*slenderness2*n2/(cmLT-.25),1-(plastic?.1:.05)*n2/(cmLT-.25));
            // Above the axial resistance the member already fails. Never let a
            // negative interaction factor hide that failure or cancel a bending term.
            k11=Math.Max(0,k11);k22=Math.Max(0,k22);k12=Math.Max(0,k12);k21=Math.Max(0,k21);
            double u1=n1+k11*m1+k12*m2,u2=n2+k21*m1+k22*m2;
            foreach(var v in new[]{k11,k12,k21,k22,u1,u2})SteelInputValidation.Finite(v,"Interaction result");
            return new BeamColumnInteractionResult {K11=k11,K12=k12,K21=k21,K22=k22,Utilization1=u1,Utilization2=u2,
                Utilization=Math.Max(Math.Max(n1,n2),Math.Max(u1,u2)),
                Reference=ntc2018?"NTC 2018 4.2.4.1.3.3; Circolare 2019 C4.2.4.1.3.3.2, equation C4.2.37, "
                    +(torsionSusceptible?"Table C4.2.V":"Table C4.2.IV (restrained kzz conservative index envelope)")
                    :"EN 1993-1-1:2005 6.3.3, equations 6.61-62; Annex B Tables B.1-B.2"};
        }
    }
}
