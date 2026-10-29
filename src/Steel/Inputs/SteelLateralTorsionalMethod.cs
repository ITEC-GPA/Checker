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
    /// 0.75), 0.2 &lt;= lambdaLT0 &lt;= 0.4 (EC3 recommended maximum, NTC never above 0.4).</summary>
    public sealed class SteelLateralTorsionalMethod
    {
        public SteelLateralTorsionalMethodKind Kind { get; }
        public double Beta { get; }
        public double LambdaLT0 { get; }
        public SteelLtbMomentFactor MomentFactor { get; }
        public string ParameterSource { get; }
        private SteelLateralTorsionalMethod(SteelLateralTorsionalMethodKind kind, double beta, double lambdaLT0, SteelLtbMomentFactor factor, string source)
        { Kind = kind; Beta = beta; LambdaLT0 = lambdaLT0; MomentFactor = factor; ParameterSource = source; }

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
