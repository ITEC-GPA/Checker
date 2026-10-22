using System;
using GPC.Checkers.Steel.ModelAdapters;

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
