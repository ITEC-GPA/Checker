using System;
using System.Collections.Generic;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Members;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Sections.Steel;

namespace GPC.Checkers.Steel.Pipeline
{
    /// <summary>The LTB reduction of the prismatic bending route and of the beam-column chiLT (EC3 6.3.3 takes chiLT
    /// from 6.3.2.2 or 6.3.2.3; NTC 4.2.4.1.3.3 from 4.2.4.1.3.2). General keeps the historical arithmetic and trace;
    /// RolledOrEquivalentWelded runs only when the analysis selects it explicitly.</summary>
    internal static class SteelLateralTorsionalExecution
    {
        internal sealed class Reduction
        {
            internal SteelLateralTorsionalMethod Method { get; set; }
            internal SteelBucklingCurve Curve { get; set; }
            internal double Chi { get; set; }
            internal RolledLateralTorsionalBuckling.Result Rolled { get; set; }
            internal bool IsRolled => Rolled != null;
            internal double Beta => IsRolled ? Method.Beta : 1;
            internal double LambdaLT0 => IsRolled ? Method.LambdaLT0 : .2;
            internal double F => IsRolled ? Rolled.F : 1;
        }

        /// <summary>Null when the selected method is applicable; NotSupported for the rolled method outside exact
        /// doubly symmetric rolled/welded I/H with horizontal major axis (no fallback to another method).</summary>
        internal static SteelMethodDecision Qualify(SteelMemberAnalysis analysis)
        {
            if (analysis.LateralTorsionalMethod.Kind == SteelLateralTorsionalMethodKind.General
                || RolledLateralTorsionalBuckling.Curve(analysis.Input.Section.ReadOnlySection).HasValue) return null;
            return new SteelMethodDecision(SteelCheckTarget.MemberStability, SteelSelectionStatus.NotSupported, null, "Member.LtbMethodGeometry",
                "EN 1993-1-1:2005 6.3.2.3 / NTC 2018 4.2.4.1.3.2 for rolled or equivalent welded sections requires an exact doubly symmetric rolled or welded I/H with horizontal major axis; other families use the general method.");
        }

        internal static Reduction Reduce(SteelMemberAnalysis analysis, ISteelSection section, double slenderness, bool ntc)
        {
            var method = analysis.LateralTorsionalMethod;
            if (method.Kind == SteelLateralTorsionalMethodKind.General)
            {
                var general = BucklingCurves.LateralTorsional(section, ntc);
                return new Reduction { Method = method, Curve = general, Chi = BucklingCurves.Reduction(slenderness, general) };
            }
            var curve = RolledLateralTorsionalBuckling.Curve(section) ?? throw new NotSupportedException("Rolled-section LTB curve not qualified for this section.");
            var rolled = RolledLateralTorsionalBuckling.Reduce(slenderness, BucklingCurves.Alpha(curve), method.Beta, method.LambdaLT0, method.MomentFactor.Kc, ntc);
            return new Reduction { Method = method, Curve = curve, Chi = rolled.Chi, Rolled = rolled };
        }

        /// <summary>Rolled method only, never on the general route. curveParameters adds BetaLT, LambdaLT0 and
        /// MomentCorrectionF for the routes that do not trace them already.</summary>
        internal static void AddTrace(List<SectionResistanceValue> trace, Reduction r, bool curveParameters = false)
        {
            if (!r.IsRolled) return;
            if (curveParameters) { Add(trace, "BetaLT", r.Beta, "1"); Add(trace, "LambdaLT0", r.LambdaLT0, "1"); Add(trace, "MomentCorrectionF", r.F, "1"); }
            var kc = r.Method.MomentFactor;
            Add(trace, "LtbRolledOrEquivalentWelded", 1, "1"); Add(trace, "PhiLT", r.Rolled.Phi, "1"); Add(trace, "ChiLTBase", r.Rolled.Base, "1");
            Add(trace, "Kc", kc.Kc, "1"); if (kc.Psi.HasValue) Add(trace, "KcPsi", kc.Psi.Value, "1");
            Add(trace, "KcNotEstablished", kc.Distribution == SteelLtbMomentDistribution.NotEstablished ? 1 : 0, "1");
        }

        internal static string Reference(bool ntc) => ntc
            ? "NTC 2018 4.2.4.1.3.2, rolled or welded I/H: chiLT=(1/f)/(PhiLT+sqrt(PhiLT^2-beta lambdaLT^2)) <= 1 and <= 1/(f lambdaLT^2), f from kc; Tab. 4.2.IX curves, Tab. 4.2.X kc; Mcr under uniform moment"
            : "EN 1993-1-1:2005 6.3.2.3, rolled or equivalent welded I/H, equations 6.57-6.58: chiLT,mod=chiLT/f <= 1 and <= 1/lambdaLT^2; Table 6.5 curves, Table 6.6 kc";

        private static void Add(List<SectionResistanceValue> t, string n, double v, string u)
        { SteelInputValidation.Finite(v, n); t.Add(new SectionResistanceValue(n, v, u)); }
    }
}
