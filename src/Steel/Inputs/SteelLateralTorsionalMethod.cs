using System;
using GPC.Checkers.Steel.Members;

namespace GPC.Checkers.Steel.Inputs
{
    /// <summary>LTB reduction of the prismatic bending and H beam-column routes. General: EN 1993-1-1:2005 6.3.2.2
    /// (Table 6.4) / NTC 2018 4.2.4.1.3.2 with f=beta=1, lambdaLT0=0.2; the default. RolledOrEquivalentWelded:
    /// EN 1993-1-1:2005 6.3.2.3 / NTC 2018 4.2.4.1.3.2 for rolled or welded doubly symmetric I/H, chosen explicitly.</summary>
    public enum SteelLateralTorsionalMethodKind { General, RolledOrEquivalentWelded }

    /// <summary>Moment distribution between the lateral restraints, EN 1993-1-1:2005 Table 6.6 (NTC 2018 kc table,
    /// same rows). Fixed/one-end-fixed name the in-plane diagram shape (hogging end moments), not lateral restraints.
    /// NotEstablished keeps kc=1 (f=1): no favourable shape is assumed.</summary>
    public enum SteelLtbMomentDistribution { NotEstablished, Uniform, Linear, SimplySupportedUniformLoad, FixedEndsUniformLoad,
        OneEndFixedUniformLoad, SimplySupportedCentralLoad, FixedEndsCentralLoad, OneEndFixedCentralLoad }

    /// <summary>Correction factor kc with its provenance. Psi is the end-moment ratio of a linear diagram
    /// (smaller/larger end moment, signed, -1..1); null for the other rows.</summary>
    public sealed class SteelLtbMomentFactor
    {
        public SteelLtbMomentDistribution Distribution { get; }
        public double? Psi { get; }
        public double Kc { get; }
        public string Source { get; }
        private SteelLtbMomentFactor(SteelLtbMomentDistribution distribution, double? psi, string source)
        {
            if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Moment-diagram provenance required.", nameof(source));
            Distribution = distribution; Psi = psi; Source = source; Kc = RolledLateralTorsionalBuckling.Kc(distribution, psi);
        }

        /// <summary>kc=1, f=1: the diagram between the lateral restraints is not established.</summary>
        public static SteelLtbMomentFactor NotEstablished(string reason) => new SteelLtbMomentFactor(SteelLtbMomentDistribution.NotEstablished, null, reason);

        /// <summary>Linear diagram, kc=1/(1.33-0.33 psi); psi=1 is the uniform row (kc=1).</summary>
        public static SteelLtbMomentFactor Linear(double psi, string source)
        {
            SteelInputValidation.Finite(psi, nameof(psi));
            if (psi < -1 || psi > 1) throw new ArgumentOutOfRangeException(nameof(psi), "Table 6.6 linear row requires -1 <= psi <= 1.");
            return new SteelLtbMomentFactor(SteelLtbMomentDistribution.Linear, psi, source);
        }

        /// <summary>Uniform (1.0) or one of the six tabulated shapes (0.94, 0.90, 0.91, 0.86, 0.77, 0.82).</summary>
        public static SteelLtbMomentFactor Tabulated(SteelLtbMomentDistribution distribution, string source)
        {
            if (!Enum.IsDefined(typeof(SteelLtbMomentDistribution), distribution) || distribution == SteelLtbMomentDistribution.Linear
                || distribution == SteelLtbMomentDistribution.NotEstablished)
                throw new ArgumentOutOfRangeException(nameof(distribution), "Use Linear for an end-moment ratio and NotEstablished for an unknown diagram.");
            return new SteelLtbMomentFactor(distribution, null, source);
        }
    }

    /// <summary>Explicit choice of the LTB reduction. RolledOrEquivalentWelded applies only to exact doubly symmetric
    /// rolled or welded I/H (Section.SectionTypes); choosing it for a welded section declares that it is an equivalent
    /// welded section. Beta/LambdaLT0 are the national values of that method: 0.75 &lt;= beta &lt;= 1 (NTC: never below
    /// 0.75), 0.2 &lt;= lambdaLT0 &lt;= 0.4 (EC3 recommended maximum, NTC never above 0.4).
    /// Two EN 1993-1-1 options, both opt-in and refused under NTC 2018 by the routes: WithDeclaredCurve (national curve
    /// assignment) and WithLowMomentExemption (6.3.2.2(4), prismatic bending only).</summary>
    public sealed class SteelLateralTorsionalMethod
    {
        public SteelLateralTorsionalMethodKind Kind { get; }
        public double Beta { get; }
        public double LambdaLT0 { get; }
        public SteelLtbMomentFactor MomentFactor { get; }
        public string ParameterSource { get; }
        /// <summary>LTB curve assigned by the National Annex to this member's section, in place of the recommended
        /// assignment of EN 1993-1-1:2005 Table 6.4 (general) or Table 6.5 (rolled method), both NDPs (6.3.2.2(2),
        /// 6.3.2.3(1)); alphaLT stays the Table 6.3 value of that curve. Null keeps the table of the method.</summary>
        public SteelBucklingCurve? DeclaredCurve { get; private set; }
        /// <summary>National Annex clause of DeclaredCurve; null without a declaration.</summary>
        public string DeclaredCurveSource { get; private set; }
        /// <summary>Design-basis source adopting EN 1993-1-1:2005 6.3.2.2(4), second criterion: for MEd/Mcr &lt;= lambdaLT0^2
        /// LTB effects are ignored and only section checks apply. lambdaLT0 is LambdaLT0 of the method in use (0.2 of
        /// equation (6.56) for the general case, the national value of the rolled method). Null: not adopted.</summary>
        public string LowMomentExemptionSource { get; private set; }
        public bool LowMomentExemption => LowMomentExemptionSource != null;
        private SteelLateralTorsionalMethod(SteelLateralTorsionalMethodKind kind, double beta, double lambdaLT0, SteelLtbMomentFactor factor, string source)
        { Kind = kind; Beta = beta; LambdaLT0 = lambdaLT0; MomentFactor = factor; ParameterSource = source; }

        /// <summary>Same method with the national LTB curve of this member's section (curves a-d of Table 6.3; a0 is not an
        /// LTB curve). EN 1993-1-1 only: NTC 2018 fixes its curves (Tab. 4.2.IX) and the routes refuse the declaration.</summary>
        public SteelLateralTorsionalMethod WithDeclaredCurve(SteelBucklingCurve curve, string nationalAnnexSource)
        {
            if (!Enum.IsDefined(typeof(SteelBucklingCurve), curve) || curve == SteelBucklingCurve.A0)
                throw new ArgumentOutOfRangeException(nameof(curve), "EN 1993-1-1:2005 Table 6.3 defines the LTB curves a, b, c and d.");
            if (string.IsNullOrWhiteSpace(nationalAnnexSource)) throw new ArgumentException("National Annex provenance of the LTB curve required.", nameof(nationalAnnexSource));
            var copy = (SteelLateralTorsionalMethod)MemberwiseClone(); copy.DeclaredCurve = curve; copy.DeclaredCurveSource = nationalAnnexSource; return copy;
        }

        /// <summary>Same method adopting EN 1993-1-1:2005 6.3.2.2(4), MEd/Mcr &lt;= lambdaLT0^2. Prismatic bending only (a beam-column
        /// analysis refuses it); EN 1993-1-1 only, the routes refuse it under NTC 2018.</summary>
        public SteelLateralTorsionalMethod WithLowMomentExemption(string designBasisSource)
        {
            if (string.IsNullOrWhiteSpace(designBasisSource)) throw new ArgumentException("Source adopting the low-moment criterion required.", nameof(designBasisSource));
            var copy = (SteelLateralTorsionalMethod)MemberwiseClone(); copy.LowMomentExemptionSource = designBasisSource; return copy;
        }

        public static SteelLateralTorsionalMethod General { get; } = new SteelLateralTorsionalMethod(SteelLateralTorsionalMethodKind.General, 1, .2,
            SteelLtbMomentFactor.NotEstablished("General case: f=1"), "EN 1993-1-1:2005 6.3.2.2 / NTC 2018 4.2.4.1.3.2 general values");

        public static SteelLateralTorsionalMethod RolledOrEquivalentWelded(double beta, double lambdaLT0, SteelLtbMomentFactor momentFactor, string parameterSource)
        {
            SteelInputValidation.Finite(beta, nameof(beta)); SteelInputValidation.Finite(lambdaLT0, nameof(lambdaLT0));
            if (beta < .75 || beta > 1) throw new ArgumentOutOfRangeException(nameof(beta), "Qualified beta range is 0.75 to 1.");
            if (lambdaLT0 < .2 || lambdaLT0 > .4) throw new ArgumentOutOfRangeException(nameof(lambdaLT0), "Qualified lambdaLT0 range is 0.2 to 0.4.");
            if (string.IsNullOrWhiteSpace(parameterSource)) throw new ArgumentException("National beta/lambdaLT0 provenance required.", nameof(parameterSource));
            return new SteelLateralTorsionalMethod(SteelLateralTorsionalMethodKind.RolledOrEquivalentWelded, beta, lambdaLT0,
                momentFactor ?? throw new ArgumentNullException(nameof(momentFactor)), parameterSource);
        }
    }
}
