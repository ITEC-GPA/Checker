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
            /// <summary>Curve of the method's table (6.4 general, 6.5 rolled); differs from Curve only by a national declaration.</summary>
            internal SteelBucklingCurve TableCurve { get; set; }
            internal double Chi { get; set; }
            internal RolledLateralTorsionalBuckling.Result Rolled { get; set; }
            internal bool IsRolled => Rolled != null;
            internal double Beta => IsRolled ? Method.Beta : 1;
            internal double LambdaLT0 => IsRolled ? Method.LambdaLT0 : .2;
            internal double F => IsRolled ? Rolled.F : 1;
        }

        /// <summary>Null when the selected method is applicable. NotSupported: under NTC 2018 a national curve or the
        /// EC3 6.3.2.2(4) exemption; the rolled method outside exact doubly symmetric rolled/welded I/H with horizontal
        /// major axis (no fallback to another method); a declared curve on a beam-column without LTB reduction.</summary>
        internal static SteelMethodDecision Qualify(SteelMemberAnalysis analysis)
        {
            var method = analysis.LateralTorsionalMethod;
            if (analysis.Input.Code.SectionCode.Standard == SectionResistanceStandard.Ntc2018)
            {
                if (method.DeclaredCurve.HasValue) return Unsupported("Member.LtbCurveFixedByNtc",
                    "NTC 2018 4.2.4.1.3.2 fixes the LTB curves (Tab. 4.2.IX) for the general and the rolled/welded cases: a national curve declaration is an EN 1993-1-1 NDP (Tables 6.4/6.5) only.");
                if (method.LowMomentExemption) return Unsupported("Member.LtbLowMomentExemptionNotInNtc",
                    "The EN 1993-1-1:2005 6.3.2.2(4) criterion MEd/Mcr <= lambdaLT,0^2 has no verified NTC 2018 4.2.4.1.3.2 / Circolare 2019 counterpart: NTC keeps the LTB check (chiLT = 1 for lambdaLT <= lambdaLT,0).");
            }
            if (method.Kind != SteelLateralTorsionalMethodKind.General && !RolledLateralTorsionalBuckling.Curve(analysis.Input.Section.ReadOnlySection).HasValue)
                return Unsupported("Member.LtbMethodGeometry",
                    "EN 1993-1-1:2005 6.3.2.3 / NTC 2018 4.2.4.1.3.2 for rolled or equivalent welded sections requires an exact doubly symmetric rolled or welded I/H with horizontal major axis; other families use the general method.");
            if (method.DeclaredCurve.HasValue && analysis.BeamColumnParameters?.TorsionSusceptible == false)
                return Unsupported("Member.LtbCurveWithoutLtbReduction",
                    "The beam-column is declared not susceptible to lateral-torsional buckling (chiLT = 1): a declared LTB curve would not be used.");
            return null;
        }

        internal static Reduction Reduce(SteelMemberAnalysis analysis, ISteelSection section, double slenderness, bool ntc)
        {
            var method = analysis.LateralTorsionalMethod;
            if (method.Kind == SteelLateralTorsionalMethodKind.General)
            {
                var table = BucklingCurves.LateralTorsional(section, ntc); var general = method.DeclaredCurve ?? table;
                return new Reduction { Method = method, Curve = general, TableCurve = table, Chi = BucklingCurves.Reduction(slenderness, general) };
            }
            var rolledTable = RolledLateralTorsionalBuckling.Curve(section) ?? throw new NotSupportedException("Rolled-section LTB curve not qualified for this section.");
            var curve = method.DeclaredCurve ?? rolledTable;
            var rolled = RolledLateralTorsionalBuckling.Reduce(slenderness, BucklingCurves.Alpha(curve), method.Beta, method.LambdaLT0, method.MomentFactor.Kc, ntc);
            return new Reduction { Method = method, Curve = curve, TableCurve = rolledTable, Chi = rolled.Chi, Rolled = rolled };
        }

        /// <summary>EN 1993-1-1:2005 6.3.2.2(4), second criterion, only when the method adopts it: true for MEd/Mcr &lt;=
        /// lambdaLT0^2, with lambdaLT0 of the method in use (0.2 general, the national value of the rolled method). The
        /// clause refers lambdaLT0 to 6.3.2.3; the general case keeps the 0.2 of its own curve (6.56), the smaller limit.
        /// Writes the criterion to trace in both outcomes.</summary>
        internal static bool LowMomentExempt(SteelLateralTorsionalMethod method, double moment, double mcr, List<SectionResistanceValue> trace)
        {
            if (!method.LowMomentExemption) return false;
            double ratio = moment / mcr, limit = method.LambdaLT0 * method.LambdaLT0; bool exempt = ratio <= limit;
            Add(trace, "LowMoment.MEd", moment, "Nmm"); Add(trace, "LowMoment.Mcr", mcr, "Nmm"); Add(trace, "LowMoment.MEdOverMcr", ratio, "1");
            Add(trace, "LowMoment.LambdaLT0", method.LambdaLT0, "1"); Add(trace, "LowMoment.Limit", limit, "1"); Add(trace, "LowMoment.Exempt", exempt ? 1 : 0, "1");
            return exempt;
        }

        internal static string LowMomentReference(SteelLateralTorsionalMethod method) =>
            "EN 1993-1-1:2005 6.3.2.2(4): for MEd/Mcr <= lambdaLT,0^2 lateral-torsional buckling effects may be ignored and only cross-sectional checks apply; lambdaLT,0 = "
            + method.LambdaLT0.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
            + (method.Kind == SteelLateralTorsionalMethodKind.General ? " of the general case (equation 6.56)" : " of the rolled or equivalent welded method (6.3.2.3; " + method.ParameterSource + ")")
            + "; adopted by: " + method.LowMomentExemptionSource;

        /// <summary>National curve declaration only: flag and the alphaLT the table of the method would give.</summary>
        internal static void AddCurveTrace(List<SectionResistanceValue> trace, Reduction r)
        {
            if (!r.Method.DeclaredCurve.HasValue) return;
            Add(trace, "LtbCurveDeclared", 1, "1"); Add(trace, "AlphaLTTableCurve", BucklingCurves.Alpha(r.TableCurve), "1");
        }

        internal static string CurveReference(Reduction r) => !r.Method.DeclaredCurve.HasValue ? ""
            : "; LTB curve " + r.Curve.ToString().ToLowerInvariant() + " declared by the National Annex (" + r.Method.DeclaredCurveSource + ") in place of curve "
                + r.TableCurve.ToString().ToLowerInvariant() + " of " + (r.IsRolled ? "Table 6.5" : "Table 6.4") + " (EN 1993-1-1:2005 6.3.2.2(2)/6.3.2.3(1)); alphaLT from Table 6.3";

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
        private static SteelMethodDecision Unsupported(string code, string message)
            => new SteelMethodDecision(SteelCheckTarget.MemberStability, SteelSelectionStatus.NotSupported, null, code, message);
    }
}
