using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Identical back-to-back 2C, connected by their webs on opposite faces of
    /// a rectangular gusset, followed by the matching coplanar CJP collector. Two bolt
    /// lines across the channel height. ChannelEnd carries N/2; Collector.Transfer carries N.
    /// The transfer's EachBrace is the h*tw WEB STRIP of one channel, not its gross area.
    /// The same Code and Bolt instances bind both descriptions; dimensions and actions
    /// are checked separately. Only the explicit ConnectedWebOnly lower-bound field is
    /// admitted. No single-lap channel, packing, compression or T-support is inferred.</summary>
    public sealed class ChannelPairCoplanarCollectorInput
    {
        public ChannelWebTensionInput ChannelEnd { get; }
        public GussetTwinFlatCollectorInput Collector { get; }
        public ChannelPairCoplanarCollectorInput(ChannelWebTensionInput channelEnd, GussetTwinFlatCollectorInput collector)
        {
            ChannelEnd = channelEnd ?? throw new ArgumentNullException(nameof(channelEnd));
            Collector = collector ?? throw new ArgumentNullException(nameof(collector));
        }
    }

    /// <summary>Bounded static terminal verification, from the two connected channel
    /// web strips to the supplied collector cut. The same web-only admissible stress
    /// field used by ChannelWebTension permits reuse of the twin-flat transfer kernel.
    /// Full-penetration continuation follows EN1993-1-8 4.7.1 / NTC2018 4.2.8.2.1.
    /// The physical member and the structure beyond the collector cut remain separate.</summary>
    public static class ChannelPairCoplanarCollector
    {
        public const string MethodId = "Steel.Connection.ChannelPair.CoplanarCollector";

        public static ConnectionVerification<ChannelPairCoplanarCollectorInput> Verify(ChannelPairCoplanarCollectorInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var end = p.ChannelEnd; var collector = p.Collector; var transfer = collector.Transfer;
            var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>(); var issues = new List<string>();
            var pair = end.Section.ReadOnlySection.SectionShape as SectionBuiltUp;
            // Establish the actual opposed, aligned backs before calling the component
            // adapter. Reference identity is the Model paired-section contract.
            if (pair == null || pair.Parts.Count != 2 || pair.Parts[0].Section.GetType() != typeof(SectionC)
                || !ReferenceEquals(pair.Parts[0].Section, pair.Parts[1].Section))
            { issues.Add("IDENTICAL_BACK_TO_BACK_CHANNEL_PAIR_REQUIRED"); return Result(); }
            var first = pair.Parts[0]; var second = pair.Parts[1]; var channel = (SectionC)first.Section;
            var right = first.X > second.X ? first : second; var left = ReferenceEquals(right, first) ? second : first;
            if (right.MirrorX || !left.MirrorX || right.MirrorY != left.MirrorY || right.Y != left.Y)
            { issues.Add("ALIGNED_OUTWARD_FACING_CHANNELS_REQUIRED"); return Result(); }
            double gap = right.X - left.X;
            if (!(gap > 0) || !Same(gap, transfer.Gusset.Thickness)) issues.Add("CHANNEL_BACK_GAP_MUST_EQUAL_GUSSET_THICKNESS_NO_PACKING");
            if (end.Method != ChannelWebTensionMethod.ConnectedWebOnly) issues.Add("CONNECTED_WEB_ONLY_FIELD_REQUIRED");
            if (!ReferenceEquals(end.Code, transfer.Code) || !ReferenceEquals(end.Bolt, transfer.Bolt))
                issues.Add("SHARED_DESIGN_CODE_AND_THROUGH_BOLT_REQUIRED");
            if (end.Lines != 2 || end.BoltsPerLine != transfer.Rows || end.Pitch != transfer.Pitch
                || end.LineSpacing != transfer.Gauge || end.EndDistance != transfer.BraceEnd)
                issues.Add("ONE_PHYSICAL_BOLT_LAYOUT_REQUIRED");
            var strip = transfer.EachBrace;
            if (strip.Width != channel.Height || strip.Thickness != channel.ThicknessWeb
                || strip.Fy != end.Section.YieldStrength || strip.Fu != end.Section.UltimateStrength)
                issues.Add("TRANSFER_BRACES_MUST_BE_ACTUAL_CHANNEL_WEB_STRIPS");
            if (transfer.Actions.Fx != 2 * end.TensionPerChannel)
                issues.Add("TOTAL_FORCE_MUST_EQUAL_TWO_IDENTICAL_CHANNEL_SHARES");
            trace.Add(new SectionResistanceValue("Geometry.ClearBackGap", gap, "mm"));
            trace.Add(new SectionResistanceValue("Actions.TensionPerChannel", end.TensionPerChannel, "N"));
            trace.Add(new SectionResistanceValue("Actions.TotalTension", transfer.Actions.Fx, "N"));
            // A geometry mismatch is not a valid basis for merging checks of different
            // joints. Once matched, retain known failures even if a subsequent mechanism
            // is unsupported, using the standard ConnectionVerification outcome contract.
            if (issues.Count != 0) return Result();
            var channels = ChannelWebTension.Verify(end);
            Add("Channel", channels.Checks, channels.Trace, channels.Diagnostics);
            var continuation = GussetTwinFlatCollector.Verify(collector);
            Add("Transfer", continuation.Transfer.Checks, continuation.Transfer.Trace, continuation.Transfer.Diagnostics);
            Add("Collector", continuation.CollectorAndWeld.Checks, continuation.CollectorAndWeld.Trace, continuation.CollectorAndWeld.Diagnostics);
            return Result();

            void Add(string prefix, IEnumerable<ConnectionCheck> values, IEnumerable<SectionResistanceValue> numbers, IEnumerable<string> missing)
            {
                checks.AddRange(values.Select(v => v.WithId(prefix + "." + v.Id)));
                trace.AddRange(numbers.Select(v => new SectionResistanceValue(prefix + "." + v.Name, v.Value, v.Unit)));
                issues.AddRange(missing.Select(v => prefix + ":" + v));
            }
            ConnectionVerification<ChannelPairCoplanarCollectorInput> Result() => new ConnectionVerification<ChannelPairCoplanarCollectorInput>(
                p, end.Code, MethodId,
                "Static concentric tension of identical back-to-back 2C through their web strips, a central rectangular gusset and matching coplanar CJP collector, bounded at its actual cut.",
                checks, trace, issues, new[] {
                    "Each channel carries exactly half the total axial tension. The h*tw web strip carries it throughout the member; flanges are unstressed in this lower-bound field. Whole-member verification remains separate.",
                    "Direct contact on opposite gusset faces, two aligned longitudinal bolt lines, identical through bolts and normal round holes. No packing or single-lap eccentricity, taper-flange/cold-formed channels or AISC shear-lag alternative.",
                    "Gusset and collector retain constant equal width/thickness and aligned mid-surfaces. The full-width matching butt weld clears the entire overlap. No T-support, load spreading or thickness transition is qualified.",
                    "Collector actions are the same-case internal forces, positive N in tension; they must equal the total channel transfer. Member instability, supporting structure beyond the collector cut, fatigue and deformation requirements remain external.",
                    "Block tearing uses the declared EN1993-1-8 supplementary method under NTC; material factors remain those of the selected code. All component applicability and detailing conditions remain mandatory." });
        }
        private static bool Same(double a, double b) => Math.Abs(a - b) <= 1e-10 * Math.Max(1, Math.Max(Math.Abs(a), Math.Abs(b)));
    }
}
