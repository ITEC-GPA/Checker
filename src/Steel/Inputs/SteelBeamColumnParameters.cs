using System;

namespace GPC.Checkers.Steel.Inputs
{
    /// <summary>Qualified H beam-column assumptions. Cm factors refer to the actual
    /// restraint intervals and the same load state; Cm=1 is an explicit conservative
    /// choice, never inferred from a few exported cuts. Global second-order effects
    /// must be accounted for by the declared analysis model where relevant.</summary>
    public sealed class SteelBeamColumnParameters
    {
        public SteelBucklingCurve Curve1 { get; }
        public SteelBucklingCurve Curve2 { get; }
        public bool TorsionSusceptible { get; }
        public double Cm1 { get; }
        public double Cm2 { get; }
        public double CmLT { get; }
        public string MomentFactorSource { get; }
        public string StabilityModelSource { get; }
        public string TorsionalExclusionSource { get; }
        public string FlexuralTorsionalExclusionSource { get; }
        public string LateralTorsionalExclusionSource { get; }
        public string ShearBucklingExclusionSource { get; }
        public SteelCriticalMomentBasis CriticalMomentBasis { get; }

        public SteelBeamColumnParameters(SteelBucklingCurve curve1, SteelBucklingCurve curve2,
            bool torsionSusceptible, double cm1, double cm2, double cmLT, string momentFactorSource,
            string stabilityModelSource, string torsionalExclusionSource, string flexuralTorsionalExclusionSource,
            SteelCriticalMomentBasis criticalMomentBasis = SteelCriticalMomentBasis.UniformMoment,
            string lateralTorsionalExclusionSource = null, string shearBucklingExclusionSource = null)
        {
            foreach(var curve in new[]{curve1,curve2})if(!Enum.IsDefined(typeof(SteelBucklingCurve),curve))throw new ArgumentOutOfRangeException(nameof(curve));
            foreach(var cm in new[]{cm1,cm2,cmLT})ValidateMomentFactor(cm);
            foreach(var source in new[]{momentFactorSource,stabilityModelSource,torsionalExclusionSource,flexuralTorsionalExclusionSource})
                if(string.IsNullOrWhiteSpace(source))throw new ArgumentException("Explicit beam-column model, moment-factor and compression-mode assessments required.");
            if(!Enum.IsDefined(typeof(SteelCriticalMomentBasis),criticalMomentBasis))throw new ArgumentOutOfRangeException(nameof(criticalMomentBasis));
            if(torsionSusceptible ? lateralTorsionalExclusionSource!=null : string.IsNullOrWhiteSpace(lateralTorsionalExclusionSource))
                throw new ArgumentException("Declare either torsional susceptibility or a reasoned lateral-torsional exclusion, never both.");
            if(shearBucklingExclusionSource!=null&&string.IsNullOrWhiteSpace(shearBucklingExclusionSource))throw new ArgumentException("Empty shear-buckling assessment.");
            Curve1=curve1;Curve2=curve2;TorsionSusceptible=torsionSusceptible;Cm1=cm1;Cm2=cm2;CmLT=cmLT;
            MomentFactorSource=momentFactorSource;StabilityModelSource=stabilityModelSource;
            TorsionalExclusionSource=torsionalExclusionSource;FlexuralTorsionalExclusionSource=flexuralTorsionalExclusionSource;
            CriticalMomentBasis=criticalMomentBasis;LateralTorsionalExclusionSource=lateralTorsionalExclusionSource;ShearBucklingExclusionSource=shearBucklingExclusionSource;
        }
        internal static void ValidateMomentFactor(double value)
        { SteelInputValidation.Finite(value,"Cm");if(value<.4||value>1)throw new ArgumentOutOfRangeException("Cm","Qualified equivalent-moment factor range is 0.4 to 1."); }
    }
}
