using System;
using System.Collections.Generic;
using GPC.Checkers.Steel.Sections;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.Connections
{
    // One physical back-to-back 2C / through-bolt / central-gusset contract.
    // Shared by coplanar and transverse collectors; no load-transfer calculation.
    internal static class ChannelPairGussetBinding
    {
        internal static void Validate(ChannelWebTensionInput end, GussetTwinFlatTensionInput transfer,
            List<string> issues, List<SectionResistanceValue> trace)
        {
            var pair = end.Section.ReadOnlySection.SectionShape as SectionBuiltUp;
            // Establish the actual opposed, aligned backs before calling the component
            // adapter. Reference identity is the Model paired-section contract.
            if (pair == null || pair.Parts.Count != 2 || pair.Parts[0].Section.GetType() != typeof(SectionC)
                || !ReferenceEquals(pair.Parts[0].Section, pair.Parts[1].Section))
            { issues.Add("IDENTICAL_BACK_TO_BACK_CHANNEL_PAIR_REQUIRED"); return; }
            var first = pair.Parts[0]; var second = pair.Parts[1]; var channel = (SectionC)first.Section;
            var right = first.X > second.X ? first : second; var left = ReferenceEquals(right, first) ? second : first;
            if (right.MirrorX || !left.MirrorX || right.MirrorY != left.MirrorY || right.Y != left.Y)
            { issues.Add("ALIGNED_OUTWARD_FACING_CHANNELS_REQUIRED"); return; }
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
        }
        private static bool Same(double a, double b) => Math.Abs(a - b) <= 1e-10 * Math.Max(1, Math.Max(Math.Abs(a), Math.Abs(b)));
    }
}
