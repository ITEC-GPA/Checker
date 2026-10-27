using System;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.Pipeline
{
    internal static class SteelPackedPairExecution
    {
        internal static void Validate(SteelMemberAnalysis analysis)
        {
            var p=analysis.PackedPair;
            if(!ReferenceEquals(p.Input,analysis.Input))throw new ArgumentException("Packed-pair assessment belongs to another state.");
            var shape=analysis.Input.Section.ReadOnlySection.SectionShape as SectionBuiltUp;
            if(shape==null)throw new NotSupportedException("A packed pair requires a SectionBuiltUp.");
            var g=ModelBuiltUpGeometry.Create(shape);
            if(p.ComputedPacking!=null)
            {
                if(!p.ComputedPacking.IsComplete||!ReferenceEquals(p.ComputedPacking.Demand.Layout.Input,analysis.Input))
                    throw new ArgumentException("Incomplete or different-state computed packing.");
                if(analysis.Prismatic.TorsionalExclusionSource==null||analysis.Prismatic.FlexuralTorsionalExclusionSource==null)
                    throw new NotSupportedException("The flexural packing-demand model requires independently excluded global T/FT modes; coupled torsional demands are not represented.");
            }
            if(p.ComputedWeldedPacking!=null)
            {
                if(!p.ComputedWeldedPacking.IsComplete||!ReferenceEquals(p.ComputedWeldedPacking.Demand.Layout.Input,analysis.Input))
                    throw new ArgumentException("Incomplete or different-state computed welded packing.");
                if(analysis.Prismatic.TorsionalExclusionSource==null||analysis.Prismatic.FlexuralTorsionalExclusionSource==null)
                    throw new NotSupportedException("The flexural welded packing model requires independently excluded global T/FT modes.");
            }
            if(p.ComputedTerminal!=null&&(!p.ComputedTerminal.IsComplete
                ||!ReferenceEquals(p.ComputedTerminal.Demand.Layout.Input,analysis.Input)))
                throw new ArgumentException("Incomplete or different-state computed terminal.");
            var a=shape.Parts[0];var b=shape.Parts[1];var chord=a.Section;
            bool horizontal=a.MirrorX!=b.MirrorX;
            if(chord is SectionC c && (!horizontal || c.LengthTop!=c.LengthBottom || c.ThicknessTop!=c.ThicknessBottom))
                throw new NotSupportedException("Packed 2C requires equal flanges and webs back to back.");
            // Backs (x=0 or y=0) must face the gap; toe-to-toe and star pairs are not Figure6.12 layouts here.
            var ac=a.Place(chord.Centroid.X,chord.Centroid.Y);var bc=b.Place(chord.Centroid.X,chord.Centroid.Y);
            var ab=a.Place(0,0);var bb=b.Place(0,0);
            double centerDistance=horizontal?Math.Abs(ac.X-bc.X):Math.Abs(ac.Y-bc.Y);
            double gap=horizontal?Math.Abs(ab.X-bb.X):Math.Abs(ab.Y-bb.Y);
            if(centerDistance<=gap)throw new NotSupportedException("Only backs facing the packing are qualified.");
            double thickness=chord is SectionC cc?Math.Min(cc.ThicknessWeb,Math.Min(cc.ThicknessTop,cc.ThicknessBottom))
                :Math.Min(((SectionL)chord).HorizontalLegThickness,((SectionL)chord).VerticalLegThickness);
            double tolerance=1e-8*Math.Max(1,gap);
            if(Math.Abs(gap-p.PackingThickness)>tolerance || Math.Abs(gap-p.GussetThickness)>tolerance)
                throw new NotSupportedException("Packing and node gussets must match the clear gap.");
            // Circular2019 explicit closeness limit; also a conservative scope restriction for the EC3 implementation.
            if(gap>3*thickness+tolerance)throw new NotSupportedException("Clear gap exceeds three times the minimum component thickness.");
            if(p.MaximumPitch>15*g.MinimumRadius || p.MaximumPitch>analysis.Input.Member.Length)
                throw new NotSupportedException("Interconnection spacing exceeds Table6.9/C4.2.III or member length.");
        }

        internal static SteelCheckExecution Complete(SteelMemberAnalysis analysis,SteelMethodDecision decision,SteelCheckExecution global)
        {
            var p=analysis.PackedPair;var trace=global.Trace.ToList();
            var g=ModelBuiltUpGeometry.Create((SectionBuiltUp)analysis.Input.Section.ReadOnlySection.SectionShape);
            double compression=p.ChordCompression/p.ChordCompressionResistance;
            double tension=p.ChordTension/p.ChordTensionResistance;
            double connection=p.ConnectionDemand/p.ConnectionResistance;
            double u=Math.Max(global.Utilization.Value,Math.Max(compression,Math.Max(tension,connection)));
            if(p.ComputedPacking!=null)
            {
                u=Math.Max(u,p.ComputedPacking.Utilization.Value);
                foreach(var value in p.ComputedPacking.Trace)trace.Add(new SectionResistanceValue("PackedPair.Computed."+value.Name,value.Value,value.Unit));
                foreach(var check in p.ComputedPacking.Checks)trace.Add(new SectionResistanceValue("PackedPair.Computed."+check.Id,check.Utilization,"1"));
            }

            if(p.ComputedWeldedPacking!=null)
            {
                u=Math.Max(u,p.ComputedWeldedPacking.Utilization.Value);
                foreach(var value in p.ComputedWeldedPacking.Trace)trace.Add(new SectionResistanceValue("PackedPair.Welded."+value.Name,value.Value,value.Unit));
                foreach(var check in p.ComputedWeldedPacking.Checks)trace.Add(new SectionResistanceValue("PackedPair.Welded."+check.Id,check.Utilization,"1"));
            }
            if(p.ComputedTerminal!=null)
            {
                u=Math.Max(u,p.ComputedTerminal.Utilization.Value);
                foreach(var value in p.ComputedTerminal.Trace)trace.Add(new SectionResistanceValue("PackedPair.Terminal."+value.Name,value.Value,value.Unit));
                foreach(var check in p.ComputedTerminal.Checks)trace.Add(new SectionResistanceValue("PackedPair.Terminal."+check.Id,check.Utilization,"1"));
            }
            void Add(string name,double value,string unit) {
                SteelInputValidation.Finite(value,name);trace.Add(new SectionResistanceValue("PackedPair."+name,value,unit));
            }
            if(p.ChordCalculation!=null)
                foreach(var value in p.ChordCalculation.Compression.Trace)Add("Chord."+value.Name,value.Value,value.Unit);
            Add("GlobalUtilization",global.Utilization.Value,"1");Add("Pitch",p.MaximumPitch,"mm");
            if(p.Layout!=null) {
                Add("GoverningPanelIndex",p.Layout.GoverningPanelIndex,"1");
                for(int i=0;i<p.Layout.Stations.Count;i++)Add("ConnectionStation"+i,p.Layout.Stations[i],"mm");
                for(int i=0;i<p.Layout.PanelLengths.Count;i++)Add("PanelLength"+i,p.Layout.PanelLengths[i],"mm");
            }
            Add("PitchLimit",15*g.MinimumRadius,"mm");Add("ClearGap",p.PackingThickness,"mm");
            Add("ChordCompression",p.ChordCompression,"N");Add("ChordCompressionResistance",p.ChordCompressionResistance,"N");
            Add("ChordCompressionUtilization",compression,"1");Add("ChordTension",p.ChordTension,"N");
            Add("ChordTensionResistance",p.ChordTensionResistance,"N");Add("ChordTensionUtilization",tension,"1");
            Add("ConnectionDemand",p.ConnectionDemand,"N");Add("ConnectionResistance",p.ConnectionResistance,"N");
            Add("ConnectionUtilization",connection,"1");Add("Utilization",u,"1");
            bool ntc=analysis.Input.Code.SectionCode.Standard==SectionResistanceStandard.Ntc2018;
            return new SteelCheckExecution(decision,u<=1?SteelExecutionStatus.Passed:SteelExecutionStatus.Failed,
                reference:global.Reference+(ntc?"; Circular7/2019 C4.2.4.1.3.1.5, Table C4.2.III":"; EN1993-1-1:2005 6.4.4, Figure6.12, Table6.9"),
                scope:(p.ComputedTerminal!=null?"Computed welded terminal to coplanar collector. ":"")+(p.ComputedWeldedPacking!=null?"Computed intermediate welded packing/wall checks. ":p.ComputedPacking!=null?"Computed standard bolted intermediate packings and wall/lap checks; terminal checks retained separately. ":p.ChordCalculation==null?"External chord resistances. ":"Computed axial fork chord panel resistances; full connection resistance external. ")+"Identical hot-finished packed 2C/2L, constant concentric compression, class1-3; integral global stability plus explicitly qualified local component and complete connection envelopes/resistances. Packing excluded from longitudinal area. No batten/lacing or beam-column method.",
                utilization:u,trace:trace);
        }
    }
}
