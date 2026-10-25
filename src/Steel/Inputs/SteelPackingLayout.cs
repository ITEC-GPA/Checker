using System;
using System.Collections.Generic;
using System.Linq;

namespace GPC.Checkers.Steel.Inputs
{
    /// <summary>Actual connection-centre stations along a packed member, mm, including
    /// both end load-transfer connections at0/L. Every intermediate packing is listed.
    /// Geometry does not prove its restraint or strength: Source must identify the
    /// physical detail; local force envelopes and complete strengths remain explicit.</summary>
    public sealed class SteelPackingLayout
    {
        public SteelVerificationInput Input { get; }
        public IReadOnlyList<double> Stations { get; }
        public IReadOnlyList<double> PanelLengths { get; }
        public double MaximumPitch { get; }
        public int GoverningPanelIndex { get; }
        public string Source { get; }

        public SteelPackingLayout(SteelVerificationInput input, IEnumerable<double> stations, string source)
        {
            Input = input ?? throw new ArgumentNullException(nameof(input));
            if (input.Member == null) throw new ArgumentException("A physical member length is required.");
            if (stations == null) throw new ArgumentNullException(nameof(stations));
            if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Actual end/intermediate connection detail source required.");
            var copy = stations.ToArray();
            if (copy.Length < 2 || copy[0] != 0 || copy[copy.Length - 1] != input.Member.Length)
                throw new ArgumentException("Include the two physical end connections at0 and L; no terminal panel may be omitted.");
            var lengths = new double[copy.Length - 1];
            for (int i = 0; i < copy.Length; i++)
            {
                SteelInputValidation.Finite(copy[i], "connection station");
                if (i > 0)
                {
                    lengths[i - 1] = copy[i] - copy[i - 1];
                    SteelInputValidation.Positive(lengths[i - 1], "successive centre distance");
                }
            }
            Stations = Array.AsReadOnly(copy); PanelLengths = Array.AsReadOnly(lengths);
            MaximumPitch = lengths.Max(); GoverningPanelIndex = Array.IndexOf(lengths, MaximumPitch); Source = source;
        }
    }
}
