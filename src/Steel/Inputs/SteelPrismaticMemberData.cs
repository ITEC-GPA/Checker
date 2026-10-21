using System;

namespace GPC.Checkers.Steel.Inputs
{
    /// <summary>Common curve labels; coefficient policy is selected by the design code.</summary>
    public enum SteelBucklingCurve { A0, A, B, C, D }
    public enum SteelCriticalMomentBasis { ActualMomentDiagram, UniformMoment }

    /// <summary>Explicit additional declarations for constant-section member checks.
    /// Exclusion sources are engineering assessments, never inferred from absent Ncr.
    /// Compression is concentric, constant along the member; effective lengths or
    /// elastic eigenvalues account for the actual restraint/system conditions.</summary>
    public sealed class SteelPrismaticMemberData
    {
        public SteelBucklingCurve Curve1 { get; }
        public SteelBucklingCurve Curve2 { get; }
        public SteelBucklingCurve TorsionalCurve { get; }
        public string TorsionalExclusionSource { get; }
        public string FlexuralTorsionalExclusionSource { get; }
        public SteelCriticalMomentBasis CriticalMomentBasis { get; }
        internal SteelPrismaticMemberData(SteelBucklingCurve first, SteelBucklingCurve second, SteelBucklingCurve torsional,
            string excludeT, string excludeFT, SteelCriticalMomentBasis basis)
        {
            foreach (var curve in new[] { first, second, torsional })
                if (!Enum.IsDefined(typeof(SteelBucklingCurve), curve)) throw new ArgumentOutOfRangeException(nameof(curve));
            if (!Enum.IsDefined(typeof(SteelCriticalMomentBasis), basis)) throw new ArgumentOutOfRangeException(nameof(basis));
            foreach (var source in new[] { excludeT, excludeFT })
                if (source != null && string.IsNullOrWhiteSpace(source)) throw new ArgumentException("A mode-exclusion source cannot be empty.");
            Curve1 = first; Curve2 = second; TorsionalCurve = torsional;
            TorsionalExclusionSource = excludeT; FlexuralTorsionalExclusionSource = excludeFT; CriticalMomentBasis = basis;
        }
    }
}
