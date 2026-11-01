using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Identical back-to-back 2C in tension, connected web-only on opposite
    /// faces of a central gusset, welded to a rectangular transverse collector.
    /// ChannelEnd carries half the total force. Its physical detail must match
    /// Collector.Attachment.Transfer; the shared binding rejects eccentric single C.</summary>
    public sealed class ChannelPairTransverseCollectorInput
    {
        public ChannelWebTensionInput ChannelEnd { get; }
        public GussetTwinFlatTransverseCollectorInput Collector { get; }
        public ChannelPairTransverseCollectorInput(ChannelWebTensionInput channelEnd,
            GussetTwinFlatTransverseCollectorInput collector)
        {
            ChannelEnd=channelEnd??throw new ArgumentNullException(nameof(channelEnd));
            Collector=collector??throw new ArgumentNullException(nameof(collector));
        }
    }

    public static class ChannelPairTransverseCollector
    {
        public static ConnectionVerification<ChannelPairTransverseCollectorInput> Verify(ChannelPairTransverseCollectorInput p)
        {
            if(p==null)throw new ArgumentNullException(nameof(p));
            var checks=new List<ConnectionCheck>();var trace=new List<SectionResistanceValue>();
            var issues=new List<string>();var assumptions=new List<string>{
                "Identical back-to-back channels, equal force shares, ConnectedWebOnly admissible field throughout each member; flanges unstressed. Member verification remains separate.",
                "Aligned backs in direct contact with the central gusset, two bolt lines and through bolts. No single C, unequal shares, packing, compression or eccentric terminal is inferred.",
                "The transverse collector terminates at two full-width simple support lines with actual same-case reactions. Their end attachments and the supporting structures are outside this boundary."};
            ChannelPairGussetBinding.Validate(p.ChannelEnd,p.Collector.Attachment.Transfer,issues,trace);
            if(issues.Count==0)
            {
                var channels=ChannelWebTension.Verify(p.ChannelEnd);
                Add("Channel",channels.Checks,channels.Trace,channels.Diagnostics,channels.Assumptions);
                var continuation=GussetTwinFlatTransverseCollector.Verify(p.Collector);
                var attachment=continuation.Attachment.Verification;var collector=continuation.Collector;
                Add("Attachment",attachment.Checks,attachment.Trace,attachment.Diagnostics,attachment.Assumptions);
                Add("Collector",collector.Checks,collector.Trace,collector.Diagnostics,collector.Assumptions);
            }
            return new ConnectionVerification<ChannelPairTransverseCollectorInput>(p,p.ChannelEnd.Code,
                "Steel.Connection.ChannelPair.TransverseCollector",
                "Static concentric tension of identical back-to-back 2C through web strips, central gusset, two symmetric fillets and a transverse rectangular collector, bounded at its two actual simple support lines.",
                checks,trace,issues,assumptions);

            void Add(string prefix,IEnumerable<ConnectionCheck> values,IEnumerable<SectionResistanceValue> numbers,
                IEnumerable<string> missing,IEnumerable<string> conditions)
            {
                checks.AddRange(values.Select(v=>v.WithId(prefix+"."+v.Id)));
                trace.AddRange(numbers.Select(v=>new SectionResistanceValue(prefix+"."+v.Name,v.Value,v.Unit)));
                issues.AddRange(missing.Select(v=>prefix+":"+v));assumptions.AddRange(conditions);
            }
        }
    }
}
