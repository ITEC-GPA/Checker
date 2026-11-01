using System;

namespace GPC.Checkers.Steel.Inputs
{
    /// <summary>Qualified doubly symmetric H/hollow beam-column assumptions. Cm factors refer to the actual
    /// restraint intervals and the same load state; Cm=1 is an explicit conservative
    /// choice, never inferred from a few exported cuts. Global second-order effects
    /// must be accounted for by the declared analysis model where relevant. A null T/FT exclusion
    /// leaves the mode to a supplied value or to the automatic resolution of the compression route.</summary>
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
        /// <summary>Opt-in conservative reduction of all normal-force/bending resistances
        /// for one active principal shear, classes 1-3, with shear buckling excluded.
        /// The original parameter object and the default method remain unchanged.</summary>
        public bool ConservativeHighShear { get; private set; }

        public SteelBeamColumnParameters WithConservativeHighShear()
            => new SteelBeamColumnParameters(Curve1, Curve2, TorsionSusceptible, Cm1, Cm2, CmLT,
                MomentFactorSource, StabilityModelSource, TorsionalExclusionSource,
                FlexuralTorsionalExclusionSource, CriticalMomentBasis,
                LateralTorsionalExclusionSource, ShearBucklingExclusionSource)
                { ConservativeHighShear = true };

        public SteelBeamColumnParameters(SteelBucklingCurve curve1, SteelBucklingCurve curve2,
            bool torsionSusceptible, double cm1, double cm2, double cmLT, string momentFactorSource,
            string stabilityModelSource, string torsionalExclusionSource, string flexuralTorsionalExclusionSource,
            SteelCriticalMomentBasis criticalMomentBasis = SteelCriticalMomentBasis.UniformMoment,
            string lateralTorsionalExclusionSource = null, string shearBucklingExclusionSource = null)
        {
            foreach(var curve in new[]{curve1,curve2})if(!Enum.IsDefined(typeof(SteelBucklingCurve),curve))throw new ArgumentOutOfRangeException(nameof(curve));
            foreach(var cm in new[]{cm1,cm2,cmLT})ValidateMomentFactor(cm);
            foreach(var source in new[]{momentFactorSource,stabilityModelSource})
                if(string.IsNullOrWhiteSpace(source))throw new ArgumentException("Explicit beam-column model and moment-factor assessments required.");
            // Null T/FT exclusion: value or automatic resolution of the compression route (doubly symmetric H, closed hollow).
            foreach(var source in new[]{torsionalExclusionSource,flexuralTorsionalExclusionSource})
                if(source!=null&&string.IsNullOrWhiteSpace(source))throw new ArgumentException("A compression-mode exclusion cannot be empty.");
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
