using System;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Connections;

namespace GPC.Checkers.Steel.Inputs
{
    /// <summary>Qualification and independently established local design values for a
    /// closely spaced identical 2C/2L, EN1993-1-1 6.4.4 / Circular2019 C4.2.4.1.3.1.5.
    /// mm and N. MaximumPitch includes end panels; packing and node gussets fill the gap.
    /// Local envelopes must include imperfections, second order, load introduction and
    /// all connector force components. Resistances must include component instability,
    /// net sections, connection eccentricity, packing, fasteners/welds and plate failure.
    /// A source is provenance, not certification. No demand is inferred as N/2 or zero.</summary>
    public sealed class SteelPackedPairAssessment
    {
        public SteelVerificationInput Input { get; }
        public SteelPackedChordResistance ChordCalculation { get; private set; }
        public SteelPackingLayout Layout { get; private set; }
        public SteelBoltedPackingResult ComputedPacking { get; private set; }
        public SteelPackedPairTerminalResult ComputedTerminal { get; private set; }

        /// <summary>Computed intermediate bolts and the computed welded terminal on the
        /// SAME demand model; no caller-supplied terminal scalar capacity.</summary>
        public static SteelPackedPairAssessment FromBoltedPacking(SteelBoltedPackingResult packing,
            SteelPackedPairTerminalResult terminal,string endRestraintSource)
        {
            if(packing==null)throw new ArgumentNullException(nameof(packing));
            if(terminal==null)throw new ArgumentNullException(nameof(terminal));
            if(!terminal.IsComplete||!terminal.ForceResistance.HasValue)
                throw new NotSupportedException(string.Join("; ",terminal.Diagnostics));
            if(!ReferenceEquals(packing.Demand,terminal.Demand))throw new ArgumentException("Same immutable packing/terminal demand required.");
            double clearance=(terminal.Detail.Lap.Length+packing.Detail.Length)/2+2*Math.Sqrt(2)*terminal.Detail.Lap.Throat;
            if(packing.Demand.Layout.PanelLengths[0]<clearance||packing.Demand.Layout.PanelLengths[packing.Demand.Layout.PanelLengths.Count-1]<clearance)
                throw new NotSupportedException("Terminal and neighbouring packing weld/plate footprints overlap.");
            var result=FromBoltedPacking(packing,terminal.ForcePerChord,terminal.ForceResistance.Value,
                "Computed welded standard terminal to coplanar collector; complete typed same-case checks",endRestraintSource);
            result.ComputedTerminal=terminal;return result;
        }

        /// <summary>Computed intermediate standard packing detail plus actual terminal
        /// node checks. End demand/resistance include N introduction and the terminal
        /// longitudinal transfer from Demand.Stations[0/last]; they are separate structural
        /// joints and are not replaced by an intermediate packing bolt. Same state/code.
        /// Only the declared normal holes filled by bolts are ignored in gross compression
        /// under EN1993-1-1 6.2.4(3); the wall checks retain their actual net section.</summary>
        public static SteelPackedPairAssessment FromBoltedPacking(SteelBoltedPackingResult result,
            double endConnectionDemand,double endConnectionResistance,string endConnectionSource,string endRestraintSource)
        {
            if(result==null)throw new ArgumentNullException(nameof(result));
            if(!result.IsComplete)throw new NotSupportedException(string.Join("; ",result.Diagnostics));
            if(string.IsNullOrWhiteSpace(endConnectionSource)||string.IsNullOrWhiteSpace(endRestraintSource))
                throw new ArgumentException("Complete terminal joint checks and actual fork restraints required.");
            SteelInputValidation.Positive(endConnectionDemand,nameof(endConnectionDemand));
            SteelInputValidation.Positive(endConnectionResistance,nameof(endConnectionResistance));
            var model=result.Demand;var layout=model.Layout;var input=layout.Input;
            if(endConnectionDemand < -input.Actions.N/2)
                throw new ArgumentException("Terminal connection envelope must include at least each chord's N/2 plus local transfer effects.");
            var gross=new SteelVerificationInput(new SteelSectionInput(input.Section.CreateSectionCopy(),true),input.Actions,input.Code,input.Member);
            var chord=SteelPackedChordResistance.UniformFork(gross,layout.MaximumPitch,model.ChordCompression,
                "Supplementary chord compression check between packing centres; integral member method under6.4.4; "+endRestraintSource);
            double tensile=Math.Min(chord.TensionResistance,.9*input.Section.NetArea.Value/2*input.Section.UltimateStrength/input.Code.SectionCode.GammaM2);
            var p=new SteelPackedPairAssessment(input,layout.MaximumPitch,result.Detail.Thickness,result.Detail.Thickness,
                model.ChordCompression,chord.Compression.Resistance.Value,model.ChordTension,tensile,
                endConnectionDemand,endConnectionResistance,layout.Source,
                "Computed sinusoidal imperfections and second-order longitudinal transfer; "+endRestraintSource,endConnectionSource);
            p.Layout=layout;p.ChordCalculation=chord;p.ComputedPacking=result;return p;
        }


        /// <summary>Same qualified axial chord model, but panel length comes from all
        /// actual packing/end-connection centres. Complete connection resistance is
        /// still external; component-only weld/block results cannot supply it.</summary>
        public static SteelPackedPairAssessment FromLayout(SteelPackingLayout layout,
            double packingThickness, double gussetThickness, double chordCompression, double chordTension,
            double connectionDemand, double connectionResistance, string localAnalysisSource,
            string connectionResistanceSource, string chordModelSource)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            var result = FromUniformForkChords(layout.Input, layout.MaximumPitch, packingThickness, gussetThickness,
                chordCompression, chordTension, connectionDemand, connectionResistance, layout.Source,
                localAnalysisSource, connectionResistanceSource, chordModelSource);
            result.Layout = layout; return result;
        }

        /// <summary>Optional computed class1-3 chord resistances for a qualified axial
        /// fork panel model. Full local force envelopes and complete connection/packing
        /// resistance are still externally established for the same design state.</summary>
        public static SteelPackedPairAssessment FromUniformForkChords(SteelVerificationInput input,double maximumPitch,
            double packingThickness,double gussetThickness,double chordCompression,double chordTension,
            double connectionDemand,double connectionResistance,string detailingSource,string localAnalysisSource,
            string connectionResistanceSource,string chordModelSource)
        {
            var calculated=SteelPackedChordResistance.UniformFork(input,maximumPitch,chordCompression,chordModelSource);
            return new SteelPackedPairAssessment(input,maximumPitch,packingThickness,gussetThickness,chordCompression,
                calculated.Compression.Resistance.Value,chordTension,calculated.TensionResistance,connectionDemand,connectionResistance,
                detailingSource,localAnalysisSource,connectionResistanceSource) { ChordCalculation=calculated };
        }

        public double MaximumPitch { get; }
        public double PackingThickness { get; }
        public double GussetThickness { get; }
        public double ChordCompression { get; }
        public double ChordCompressionResistance { get; }
        public double ChordTension { get; }
        public double ChordTensionResistance { get; }
        public double ConnectionDemand { get; }
        public double ConnectionResistance { get; }
        public string DetailingSource { get; }
        public string LocalAnalysisSource { get; }
        public string ResistanceSource { get; }

        public SteelPackedPairAssessment(SteelVerificationInput input, double maximumPitch, double packingThickness,
            double gussetThickness, double chordCompression, double chordCompressionResistance,
            double chordTension, double chordTensionResistance, double connectionDemand, double connectionResistance,
            string detailingSource, string localAnalysisSource, string resistanceSource)
        {
            Input=input??throw new ArgumentNullException(nameof(input));
            foreach(double v in new[]{maximumPitch,packingThickness,gussetThickness,chordCompression,
                chordCompressionResistance,chordTensionResistance,connectionDemand,connectionResistance})
                SteelInputValidation.Positive(v,"packed pair design value");
            SteelInputValidation.Finite(chordTension,nameof(chordTension));
            if(chordTension<0)throw new ArgumentOutOfRangeException(nameof(chordTension));
            foreach(string s in new[]{detailingSource,localAnalysisSource,resistanceSource})
                if(string.IsNullOrWhiteSpace(s))throw new ArgumentException("Detailing, local analysis and resistance sources are required.");
            if(input.Actions.N>=0 || chordCompression < -input.Actions.N/2)
                throw new ArgumentException("Concentric compression requires a local compression envelope at least N/2.");
            MaximumPitch=maximumPitch;PackingThickness=packingThickness;GussetThickness=gussetThickness;
            ChordCompression=chordCompression;ChordCompressionResistance=chordCompressionResistance;
            ChordTension=chordTension;ChordTensionResistance=chordTensionResistance;
            ConnectionDemand=connectionDemand;ConnectionResistance=connectionResistance;
            DetailingSource=detailingSource;LocalAnalysisSource=localAnalysisSource;ResistanceSource=resistanceSource;
        }
    }
}
