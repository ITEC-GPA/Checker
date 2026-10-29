using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using GPC.Geometry;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Reports;
using GPC.Model.Core.Coordinates;
using GPC.Model.Sections;
using GPC.Model.Structure.Members;

namespace GPC.Model.Checker
{
    /// <summary>Precise refusal of an automatic preparation outside its qualified field.</summary>
    internal sealed class SteelPreparationRefusal : NotSupportedException
    {
        internal string Code { get; }
        internal SteelPreparationRefusal(string code, string message) : base(message) { Code = code; }
    }

    /// <summary>One evaluation span between consecutive fork restraints (or the declared fork ends), mm.
    /// Ncr2/Mcr use its own length; Ncr1 the longest in-plane span overlapping it; NcrT the longest
    /// twist-restrained sub-span. Continuity with adjacent spans is ignored: a lower bound.</summary>
    internal sealed class SteelForkSegment
    {
        internal int Index { get; }
        internal int Count { get; }
        internal double Start { get; }
        internal double End { get; }
        internal double Length => End - Start;
        internal double InPlaneLength { get; }
        internal double? TorsionalLength { get; }
        internal SteelForkSegment(int index, int count, double start, double end, double inPlane, double? torsional)
        { Index = index; Count = count; Start = start; End = end; InPlaneLength = inPlane; TorsionalLength = torsional; }
    }

    /// <summary>Model MemberRestraint semantics (read-only): Distance along the physical member, six
    /// rigid flags in the restraint frame (translations V1..V3, then rotations), null = unknown, no
    /// stiffness, no warping flag and no application height. Mapped to the principal member axes:
    /// lateral = translation along principal axis 1, in-plane = along axis 2, twist = rotation about
    /// the member axis. Only lateral+twist (fork) ends LTB and minor-axis spans: with zero twist the
    /// whole section is laterally held whatever the height of the lateral restraint. In-plane alone ends
    /// only Ncr1 spans, twist alone only NcrT spans. Warping is free (conservative). Lateral without twist
    /// (one flange or unknown height), twist without lateral for LTB, unknown or phase-dependent flags
    /// and frames skew to the principal axes are refused.</summary>
    internal static class SteelRestraintSegments
    {
        private const double Tolerance = 1e-7;
        internal sealed class Group { internal double Distance; internal bool? Lateral, InPlane, Twist; internal string Source; }

        private static bool? Or(bool? a, bool? b) => a == true || b == true ? true : a == null || b == null ? (bool?)null : false;
        private static string F(double v) => v.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>Restraints grouped by position (flags of coincident restraints combined).</summary>
        internal static List<Group> Groups(MemberInputSnapshot s, CoordinateSystem frame, double angle)
        {
            double c = Math.Cos(angle), sn = Math.Sin(angle); var groups = new List<Group>();
            foreach (var r in s.Context.Restraints.OrderBy(r => r.Distance))
            {
                string at = " at " + F(r.Distance) + " mm (" + r.Source + ")";
                if (r.Phase != null) throw new SteelPreparationRefusal("SteelMember.Restraint.PhaseDependent", "Phase-dependent restraint is not matched to the analysed state" + at + ".");
                var axes = r.Axes; var e = new[] { axes.V1, axes.V2, axes.V3 }; var f = r.Restrained;
                bool? Flag(int principal, bool rotation)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        double d1 = Axes.Dot(e[k], frame.V1), d2 = Axes.Dot(e[k], frame.V2);
                        double d = principal == 0 ? c * d1 + sn * d2 : principal == 1 ? -sn * d1 + c * d2 : Axes.Dot(e[k], frame.V3);
                        if (Math.Abs(Math.Abs(d) - 1) <= 1e-9) return f[k + (rotation ? 3 : 0)];
                    }
                    throw new SteelPreparationRefusal("SteelMember.Restraint.SkewFrame", "Restraint frame not aligned with the principal member axes" + at + ".");
                }
                bool? lateral = Flag(0, false), inPlane = Flag(1, false), twist = Flag(2, true);
                var last = groups.LastOrDefault();
                if (last != null && Math.Abs(last.Distance - r.Distance) <= Tolerance)
                { last.Lateral = Or(last.Lateral, lateral); last.InPlane = Or(last.InPlane, inPlane); last.Twist = Or(last.Twist, twist); last.Source += "; " + r.Source; }
                else groups.Add(new Group { Distance = r.Distance, Lateral = lateral, InPlane = inPlane, Twist = twist, Source = r.Source });
            }
            return groups;
        }

        /// <summary>Fork segments of the full span [0,L]; the ends are the declared forks. flexural: Ncr1/Ncr2,
        /// torsion: NcrT, lateralTorsional: Mcr. Evidence records the role given to every restraint.</summary>
        internal static IReadOnlyList<SteelForkSegment> Plan(MemberInputSnapshot s, CoordinateSystem frame, double angle,
            bool flexural, bool torsion, bool lateralTorsional, List<CheckCalculationValue> evidence)
        {
            double length = s.Length; var inPlane = new List<double> { 0, length }; var fork = new List<double> { 0, length }; var twist = new List<double> { 0, length };
            int index = 0;
            foreach (var g in Groups(s, frame, angle))
            {
                index++; string at = " at " + F(g.Distance) + " mm (" + g.Source + ")"; string role;
                SteelPreparationRefusal Unknown(string flag) => new SteelPreparationRefusal("SteelMember.Restraint.Unknown", "Unknown " + flag + " flag" + at + "; null is never assumed free or fixed.");
                if (g.Distance <= Tolerance || g.Distance >= length - Tolerance)
                {
                    if (g.Lateral == false || g.InPlane == false || g.Twist == false)
                        throw new SteelPreparationRefusal("SteelMember.Restraint.EndConflictsWithFork", "Explicitly free translation or twist conflicts with the declared fork end" + at + ".");
                    role = "declared fork end";
                }
                else
                {
                    var roles = new List<string>();
                    if (flexural)
                    {
                        if (g.InPlane == null) throw Unknown("in-plane translation (principal axis 2)");
                        if (g.InPlane == true) { inPlane.Add(g.Distance); roles.Add("in-plane (Ncr1)"); }
                    }
                    if (flexural || lateralTorsional)
                    {
                        if (g.Lateral == true && g.Twist == false)
                            throw new SteelPreparationRefusal("SteelMember.Restraint.LateralWithoutTwist", "Lateral restraint without twist restraint (one flange or unknown height) is not a fork for LTB or minor-axis buckling" + at + ".");
                        if (g.Lateral == null || g.Lateral == true && g.Twist == null) throw Unknown("lateral translation (principal axis 1) or twist");
                        if (lateralTorsional && g.Lateral == false && g.Twist != false)
                            throw g.Twist == null ? Unknown("twist") : new SteelPreparationRefusal("SteelMember.Restraint.TwistWithoutLateral", "Twist restraint without lateral restraint is a partial LTB restraint" + at + ".");
                        if (g.Lateral == true && g.Twist == true) { fork.Add(g.Distance); roles.Add("fork (Ncr2/Mcr)"); }
                    }
                    if (torsion)
                    {
                        if (g.Twist == null) throw Unknown("twist");
                        if (g.Twist == true) { twist.Add(g.Distance); roles.Add("twist (NcrT)"); }
                    }
                    role = roles.Count == 0 ? "not a segment end for the prepared modes" : string.Join(" + ", roles);
                }
                evidence.Add(new CheckCalculationValue("Member.Restraint." + index + ".Distance", g.Distance, "mm", role + "; " + g.Source));
            }
            var forks = Points(fork, length); var planes = Spans(Points(inPlane, length)); var twists = Spans(Points(twist, length));
            var result = new List<SteelForkSegment>();
            for (int k = 0; k + 1 < forks.Count; k++)
            {
                double a = forks[k], b = forks[k + 1];
                double l1 = planes.Where(x => x.Item1 < b - Tolerance && x.Item2 > a + Tolerance).Max(x => x.Item2 - x.Item1);
                double? lt = torsion ? twists.Where(x => x.Item1 >= a - Tolerance && x.Item2 <= b + Tolerance).Max(x => x.Item2 - x.Item1) : (double?)null;
                result.Add(new SteelForkSegment(k + 1, forks.Count - 1, a, b, l1, lt));
            }
            return result;
        }
        private static List<double> Points(List<double> values, double length)
        {
            var sorted = values.OrderBy(v => v).ToList(); var result = new List<double> { 0 };
            foreach (double v in sorted) if (v > result.Last() + Tolerance && v < length - Tolerance) result.Add(v);
            result.Add(length); return result;
        }
        private static List<Tuple<double, double>> Spans(List<double> points) => points.Zip(points.Skip(1), Tuple.Create).ToList();

        /// <summary>Non-automatic route: longest span between twist restraints inside the checked span,
        /// both span ends explicitly twist-restrained in the Model; null with the reason otherwise.</summary>
        internal static double? TorsionalLength(MemberInputSnapshot s, CoordinateSystem frame, double angle, out string reason)
        {
            List<Group> groups;
            try { groups = Groups(s, frame, angle); }
            catch (SteelPreparationRefusal ex) { reason = ex.Message; return null; }
            groups = groups.Where(g => g.Distance >= s.SpanStart - Tolerance && g.Distance <= s.SpanEnd + Tolerance).ToList();
            bool End(double x) => groups.Any(g => Math.Abs(g.Distance - x) <= Tolerance && g.Twist == true);
            if (!End(s.SpanStart) || !End(s.SpanEnd)) { reason = "No explicit twist restraint at both span ends"; return null; }
            var points = new List<double> { s.SpanStart, s.SpanEnd };
            foreach (var g in groups.Where(g => g.Distance > s.SpanStart + Tolerance && g.Distance < s.SpanEnd - Tolerance))
            {
                if (g.Twist == null) { reason = "Unknown twist flag at " + F(g.Distance) + " mm (" + g.Source + ")"; return null; }
                if (g.Twist == true) points.Add(g.Distance);
            }
            var sorted = points.OrderBy(v => v).ToList(); reason = null;
            return sorted.Zip(sorted.Skip(1), (a, b) => b - a).Max();
        }
    }

    public sealed partial class SteelMemberPipelineChecker
    {
        private static bool DoublySymmetricH(ISectionShape shape) => shape.GetType() == typeof(SectionH) && ((SectionH)shape).IsDoubleSymmetric;
        private static bool ClosedHollow(ISectionShape shape) => (shape.GetType() == typeof(SectionCHS) || shape is SectionRHS) && shape.IsDoubleSymmetric;
        // Every cut carries the same steel property (SteelPhysicalMemberPreparation).
        private static ISectionShape Shape(PhysicalMemberCheckInput input) => ((GPC.Model.Sections.Steel.ISteelSection)input.ActionSections[0].Property).SectionShape;

        /// <summary>Segment-wise automatic route: beam-columns, doubly symmetric H with declared restraints,
        /// closed hollow compression. Open H/C/T/L without restraints keep the historical full-span values.</summary>
        private bool SegmentedPreparation(PhysicalMemberCheckInput input, SteelVerificationInput common, string mode)
        {
            if (!_parameters.ContainsKey("CriticalValues")) return false;
            var shape = Shape(input);
            return mode == "BeamColumn" || DoublySymmetricH(shape) && input.Snapshot.Context.Restraints.Count != 0 || mode == "Compression" && ClosedHollow(shape);
        }

        /// <summary>Non-automatic compression/beam-column of a doubly symmetric H: LcrT from explicit Model twist
        /// restraints when the caller supplied neither NcrT nor its exclusion (EC3 6.3.1.4 / NTC 4.2.4.1.3.1).</summary>
        private SteelMemberInput TorsionalMember(PhysicalMemberCheckInput input, SteelVerificationInput common, SteelPhysicalMemberPreparation physical, string mode)
        {
            var m = common.Member;
            if (mode != "Compression" && mode != "BeamColumn" || _parameters.ContainsKey("CriticalValues") || _parameters.ContainsKey("TorsionalExclusionSource")
                || _parameters.ContainsKey("NcrT.Value") || m.TorsionalBucklingLength.HasValue || !DoublySymmetricH(Shape(input))) return m;
            double? length = SteelRestraintSegments.TorsionalLength(input.Snapshot, physical.Frame, common.Section.PrincipalAxisAngle, out string reason);
            physical.AddEvidence(new[] { new CheckCalculationValue(length.HasValue ? "Member.Torsional.Length" : "Member.Torsional.LengthNotDerived", length, length.HasValue ? "mm" : null,
                length.HasValue ? "Longest span between explicit Model twist restraints, free warping (conservative); NcrT computed automatically" : reason) });
            return length.HasValue ? new SteelMemberInput(m.Length, m.BucklingLength1, m.BucklingLength2, length, m.StartRestraints, m.EndRestraints, m.Imperfections) : m;
        }

        private CheckResult VerifySegments(PhysicalMemberCheckInput input, SteelVerificationInput common, SteelDesignContext context,
            string mode, SteelPhysicalMemberPreparation physical, string fingerprint, CancellationToken token)
        {
            var s = input.Snapshot; var shape = Shape(input);
            bool h = DoublySymmetricH(shape);
            if (!(h || ClosedHollow(shape) && mode != "Bending"))
                throw new SteelPreparationRefusal("SteelMember.Segment.SectionNotQualified", "Segment-wise automatic critical values: doubly symmetric H, or closed doubly symmetric hollow sections without LTB.");
            if (s.SpanStart != 0 || s.SpanEnd != s.Length || Math.Abs(s.Length - s.DeformableLength) > 1e-7)
                throw new SteelPreparationRefusal("SteelMember.Segment.FullSpanRequired", "Segment-wise preparation covers the full deformable member; declared forks at both member ends.");
            bool flexural = mode != "Bending", torsion = flexural && h && !_parameters.ContainsKey("TorsionalExclusionSource") && !_parameters.ContainsKey("NcrT.Value");
            bool ltb = mode == "Bending" || mode == "BeamColumn" && _parameters.TryGetValue("TorsionSusceptible", out var t) && t == "true" && physical.Cuts.Any(x => x.Actions.M1 != 0);
            var evidence = new List<CheckCalculationValue>();
            var segments = SteelRestraintSegments.Plan(s, physical.Frame, common.Section.PrincipalAxisAngle, flexural, torsion, ltb, evidence);
            var declared = s.Context;
            bool Matches(double? length, Func<SteelForkSegment, double> derived) => !length.HasValue || segments.All(x => Math.Abs(length.Value - derived(x)) <= 1e-10 * length.Value);
            if (!Matches(declared.EffectiveLength1, x => x.InPlaneLength) || !Matches(declared.EffectiveLength2, x => x.Length) || !Matches(declared.LateralTorsionalLength, x => x.Length))
                throw new ArgumentException("Declared effective lengths conflict with the restraint-derived fork segments.");
            var outcomes = new List<(SteelForkSegment Segment, SteelPhysicalMemberPreparation View, SteelVerificationResult Result)>();
            foreach (var segment in segments)
            {
                token.ThrowIfCancellationRequested();
                var view = physical.ForSegment(segment);
                var member = new SteelMemberInput(segment.Length, flexural ? segment.InPlaneLength : (double?)null, flexural ? segment.Length : (double?)null, segment.TorsionalLength);
                var segmentInput = new SteelVerificationInput(common.Section, view.Cuts[0].Actions, common.Code, member);
                var analysis = mode == "BeamColumn" ? BeamColumn(input, segmentInput, view) : Prismatic(input, segmentInput, context, mode, view);
                // Same per-segment treatment as the single-span route: LTB method with the segment's own kc, automatic H shear screens.
                var plan = SteelMethodSelector.SelectMember(WithLtbMethod(analysis, mode, view, segments.Count));
                outcomes.Add((segment, view, SteelVerificationPipeline.Execute(plan, mode == "BeamColumn" ? BeamColumnShearAssessments(plan) : null, token)));
            }
            // An unevaluated segment blocks the verdict; otherwise the largest member utilization governs.
            double Utilization(SteelVerificationResult r) => r.Checks.Where(x => x.IsEvaluated).Select(x => x.Utilization ?? 0).DefaultIfEmpty(-1).Max();
            bool Blocking(SteelVerificationResult r) => r.Checks.Any(x => !x.IsEvaluated && x.Status != SteelExecutionStatus.NotRequired);
            var governing = outcomes.FirstOrDefault(o => Blocking(o.Result));
            if (governing.Result == null) governing = outcomes.OrderByDescending(o => Utilization(o.Result)).First();
            var mapped = SteelPipelineResults.Map(governing.Result, fingerprint, Id, Limitations);
            evidence.Add(new CheckCalculationValue("Member.Segment.Count", segments.Count, "1", "Fork-supported spans between qualifying restraints, free warping; continuity between spans ignored (lower bound, conservative); "
                + Required("CriticalValuesSource")));
            evidence.Add(new CheckCalculationValue("Member.Segment.Governing", governing.Segment.Index, "1", "Segment whose own actions and critical values govern the member verdict"));
            evidence.Add(new CheckCalculationValue("Member.Segment.Governing.Start", governing.Segment.Start, "mm", "Physical-member coordinate"));
            evidence.Add(new CheckCalculationValue("Member.Segment.Governing.End", governing.Segment.End, "mm", "Physical-member coordinate"));
            foreach (var o in outcomes)
            {
                string k = "Member.Segment." + o.Segment.Index; var check = o.Result.Checks.FirstOrDefault(x => x.Decision.Target == SteelCheckTarget.MemberStability);
                evidence.Add(new CheckCalculationValue(k + ".Start", o.Segment.Start, "mm", "Physical-member coordinate"));
                evidence.Add(new CheckCalculationValue(k + ".End", o.Segment.End, "mm", "Physical-member coordinate"));
                if (flexural) evidence.Add(new CheckCalculationValue(k + ".InPlaneLength", o.Segment.InPlaneLength, "mm", "Ncr1: longest span between in-plane restraints overlapping this segment"));
                if (o.Segment.TorsionalLength.HasValue) evidence.Add(new CheckCalculationValue(k + ".TorsionalLength", o.Segment.TorsionalLength, "mm", "NcrT: longest span between twist restraints in this segment"));
                foreach (var v in o.Result.Plan.MemberAnalysis.Values.Where(v => v.Quantity == SteelStabilityQuantity.Mcr))
                    evidence.Add(new CheckCalculationValue(k + ".Mcr", v.Value, v.Unit, v.Source));
                evidence.Add(new CheckCalculationValue(k + ".Utilization", check?.Utilization, "1", check?.Status.ToString() ?? "Not selected"));
            }
            governing.View.AddEvidence(evidence); governing.View.AddTrace(mapped);
            return mapped;
        }

        /// <summary>Mcr of one fork segment: UniformFork needs constant M1 in the segment (C1=1), ForkDiagram uses
        /// the segment's own exported diagram. Null when the segment carries no major moment.</summary>
        private SteelStabilityValue SegmentMoment(PhysicalMemberCheckInput member, SteelVerificationInput input, SteelPhysicalMemberPreparation physical)
        {
            var segment = physical.Segment;
            if (physical.Cuts.All(x => x.Actions.M1 == 0)) return null;
            if (_parameters["CriticalValues"] == "ForkDiagram") return PrepareForkDiagram(member, input, physical).Single();
            double m = physical.Cuts[0].Actions.M1;
            if (physical.Cuts.Any(x => !Close(x.Actions.M1, m, Math.Abs(m)) || !Close(x.Actions.V2, 0, 4 * Math.Abs(m) / segment.Length)))
                throw new SteelPreparationRefusal("SteelMember.Segment.UniformMomentRequired", "UniformFork Mcr needs constant M1 within each fork segment (segment " + segment.Index + "); declare ForkDiagram otherwise.");
            if (Required("McrBasis") != "UniformMoment") throw new ArgumentException("AutomaticMcrUsesUniformMomentBasis");
            return SteelMemberSchemePreparation.ForkBending(input.Section, new SteelMemberInput(segment.Length),
                SteelForkMomentDiagram.EndMoments(m, m, Required("LoadPatternReference")), CriticalSource(physical)).UniformMoment;
        }
        private string CriticalSource(SteelPhysicalMemberPreparation physical) => Required("CriticalValuesSource") + (physical.Segment == null ? ""
            : "; fork segment " + physical.Segment.Index + "/" + physical.Segment.Count + " [" + physical.Segment.Start.ToString("R", CultureInfo.InvariantCulture) + ", "
                + physical.Segment.End.ToString("R", CultureInfo.InvariantCulture) + "] mm between qualifying restraints, continuity ignored (lower bound)");
    }
}
