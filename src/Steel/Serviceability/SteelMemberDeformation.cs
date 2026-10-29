using System;
using System.Collections.Generic;
using System.Linq;

namespace GPC.Checkers.Steel.Serviceability
{
    /// <summary>Support scheme of a design span for the deflection reference. SupportedEnds: chord through the displaced
    /// supports at both span ends. Cantilevers: the translation of the supported end (its rotation remains in the deflection).</summary>
    public enum SteelDeflectionScheme { SupportedEnds, CantileverAtStart, CantileverAtEnd }

    /// <summary>One displacement component at a point of a member: distance from the member origin and value, mm.</summary>
    public sealed class SteelDisplacementPoint
    {
        public double Distance { get; }
        public double Value { get; }
        /// <summary>Identity of the source point, for example the FE node.</summary>
        public string Label { get; }
        public SteelDisplacementPoint(double distance, double value, string label)
        {
            Distance = Finite(distance, nameof(distance)); Value = Finite(value, nameof(value));
            if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("DisplacementPointLabelRequired", nameof(label));
            Label = label;
        }
        internal static double Finite(double value, string name)
            => double.IsNaN(value) || double.IsInfinity(value) ? throw new ArgumentOutOfRangeException(name, "A finite value is required.") : value;
    }

    /// <summary>Deflections relative to the reference of the scheme at the given points only: no value is interpolated between them,
    /// so the maximum is the one of the points (the caller qualifies that they include the location of the maximum).</summary>
    public sealed class SteelDeflectionProfile
    {
        private const double Tolerance = 1e-7;
        public SteelDeflectionScheme Scheme { get; }
        public double Start { get; }
        public double End { get; }
        public double Span => End - Start;
        /// <summary>Values relative to the chord (SupportedEnds) or to the supported end (cantilevers), in member order.</summary>
        public IReadOnlyList<SteelDisplacementPoint> Relative { get; }
        /// <summary>The point of largest absolute relative value; the first in member order on a tie.</summary>
        public SteelDisplacementPoint Governing { get; }
        /// <summary>Reference values at the span ends (support translations of the chord, or the supported end twice).</summary>
        public double ReferenceAtStart { get; }
        public double ReferenceAtEnd { get; }

        private SteelDeflectionProfile(SteelDeflectionScheme scheme, double start, double end, IReadOnlyList<SteelDisplacementPoint> relative, double atStart, double atEnd)
        {
            Scheme = scheme; Start = start; End = end; Relative = relative; ReferenceAtStart = atStart; ReferenceAtEnd = atEnd;
            Governing = relative.Aggregate((a, b) => Math.Abs(b.Value) > Math.Abs(a.Value) ? b : a);
        }

        /// <summary>Points in increasing distance on [start, end], with a point at each span end; a supported span needs at least
        /// one interior point, otherwise its relative deflection would be zero by construction.</summary>
        public static SteelDeflectionProfile Create(IEnumerable<SteelDisplacementPoint> points, double start, double end, SteelDeflectionScheme scheme)
        {
            if (!Enum.IsDefined(typeof(SteelDeflectionScheme), scheme)) throw new ArgumentOutOfRangeException(nameof(scheme));
            SteelDisplacementPoint.Finite(start, nameof(start)); SteelDisplacementPoint.Finite(end, nameof(end));
            if (!(end - start > Tolerance)) throw new ArgumentException("PositiveDeflectionSpanRequired");
            var p = (points ?? throw new ArgumentNullException(nameof(points))).ToArray();
            if (p.Any(x => x == null)) throw new ArgumentException("NullDisplacementPoint");
            for (int i = 1; i < p.Length; i++)
                if (!(p[i].Distance - p[i - 1].Distance > Tolerance)) throw new ArgumentException("DisplacementPointsMustIncreaseAlongTheMember");
            if (p.Length == 0 || Math.Abs(p[0].Distance - start) > Tolerance || Math.Abs(p[p.Length - 1].Distance - end) > Tolerance)
                throw new ArgumentException("DisplacementPointsRequiredAtBothSpanEnds");
            if (scheme == SteelDeflectionScheme.SupportedEnds && p.Length < 3) throw new ArgumentException("InteriorDisplacementPointRequired");
            if (p.Length < 2) throw new ArgumentException("TwoDisplacementPointsRequired");
            double a = p[0].Value, b = p[p.Length - 1].Value;
            double atStart = scheme == SteelDeflectionScheme.CantileverAtEnd ? b : a, atEnd = scheme == SteelDeflectionScheme.CantileverAtStart ? a : b;
            var relative = p.Select(x => new SteelDisplacementPoint(x.Distance,
                x.Value - (atStart + (atEnd - atStart) * (x.Distance - start) / (end - start)), x.Label)).ToArray();
            return new SteelDeflectionProfile(scheme, start, end, Array.AsReadOnly(relative), atStart, atEnd);
        }

        /// <summary>Linear superposition at the same points: this profile minus <paramref name="deducted"/> (for example the total state
        /// minus the permanent actions alone). Both profiles are relative to their own reference, which is linear as well.</summary>
        public SteelDeflectionProfile Minus(SteelDeflectionProfile deducted)
        {
            if (deducted == null) throw new ArgumentNullException(nameof(deducted));
            if (deducted.Scheme != Scheme || Math.Abs(deducted.Start - Start) > Tolerance || Math.Abs(deducted.End - End) > Tolerance
                || deducted.Relative.Count != Relative.Count || Relative.Zip(deducted.Relative, (x, y) => Math.Abs(x.Distance - y.Distance) <= Tolerance && x.Label == y.Label).Any(same => !same))
                throw new ArgumentException("SuperpositionRequiresTheSamePoints");
            var relative = Relative.Zip(deducted.Relative, (x, y) => new SteelDisplacementPoint(x.Distance, x.Value - y.Value, x.Label)).ToArray();
            return new SteelDeflectionProfile(Scheme, Start, End, Array.AsReadOnly(relative), ReferenceAtStart - deducted.ReferenceAtStart, ReferenceAtEnd - deducted.ReferenceAtEnd);
        }
    }

    /// <summary>Declared limit L/n with its source. The reference length is the span, or a declared multiple of a cantilever length.</summary>
    public sealed class SteelDeformationLimit
    {
        public double Ratio { get; }
        public double ReferenceLength { get; }
        public string Source { get; }
        public double Value => ReferenceLength / Ratio;
        public SteelDeformationLimit(double ratio, double referenceLength, string source)
        {
            if (!(SteelDisplacementPoint.Finite(ratio, nameof(ratio)) > 0) || !(SteelDisplacementPoint.Finite(referenceLength, nameof(referenceLength)) > 0))
                throw new ArgumentOutOfRangeException(nameof(ratio), "Positive ratio and reference length required.");
            if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("DeformationLimitSourceRequired", nameof(source));
            Ratio = ratio; ReferenceLength = referenceLength; Source = source;
        }
    }

    /// <summary>Demand against capacity with utilization = demand/capacity (limit 1). Deformations: demand the displacement, capacity
    /// the declared limit. Frequency: demand the declared minimum frequency, capacity the estimated natural frequency.</summary>
    public sealed class SteelDeformationCheck
    {
        public string Key { get; }
        public double Demand { get; }
        public double Capacity { get; }
        public string Unit { get; }
        public double Utilization => Demand / Capacity;
        public bool Passed => Utilization <= 1;
        /// <summary>Distance of the governing point from the member origin, mm; null for end-to-end quantities.</summary>
        public double? Location { get; }
        public string LocationLabel { get; }
        public string Clause { get; }
        public string Source { get; }
        internal SteelDeformationCheck(string key, double demand, double capacity, string unit, double? location, string label, string clause, string source)
        {
            if (!(SteelDisplacementPoint.Finite(demand, nameof(demand)) >= 0) || !(SteelDisplacementPoint.Finite(capacity, nameof(capacity)) > 0))
                throw new ArgumentOutOfRangeException(nameof(capacity));
            Key = key; Demand = demand; Capacity = capacity; Unit = unit; Location = location; LocationLabel = label; Clause = clause; Source = source;
        }
    }

    /// <summary>
    /// Member deformation checks of EN 1993-1-1:2005 7.2 with EN 1990 A1.4 and NTC 2018 4.2.4.2, on displacements already relative to
    /// the declared reference. N, mm. The limits are always declared values with their source: no table value is built in.
    /// </summary>
    public static class SteelMemberDeformation
    {
        /// <summary>K of f = K/sqrt(delta[mm]) for a simply supported member with uniformly distributed mass m, delta the mid-span
        /// deflection under the weight of that mass: f = (pi/2) sqrt(EI/(m L^4)) and delta = 5 m g L^4/(384 EI), so
        /// K = (pi/2) sqrt(5 g/384) = 17.75 with g = 9806.65 mm/s^2.</summary>
        public const double SimplySupportedFrequencyCoefficient = 17.75;

        /// <summary>wmax = wtot - wc (EN 1990 A1.4.3, Fig. A1.1; NTC delta_max = delta_tot - delta_C): the remaining deflection at the
        /// point of the largest total deflection, with the signed precamber offset along the same axis (the unloaded position of
        /// that point relative to the chord). The absolute value is checked: a residual hog counts as a deflection.</summary>
        public static SteelDeformationCheck MaximumDeflection(SteelDeflectionProfile total, double precamber, SteelDeformationLimit limit, string clause)
        {
            if (total == null || limit == null) throw new ArgumentNullException(total == null ? nameof(total) : nameof(limit));
            SteelDisplacementPoint.Finite(precamber, nameof(precamber));
            var g = total.Governing;
            return new SteelDeformationCheck("Deflection.Max", Math.Abs(g.Value + precamber), limit.Value, "mm", g.Distance, g.Label, clause, limit.Source);
        }

        /// <summary>Deflection due to the variable actions (NTC delta_2, EN 1990 w3): the largest absolute value of its profile.</summary>
        public static SteelDeformationCheck VariableDeflection(SteelDeflectionProfile variable, SteelDeformationLimit limit, string clause)
        {
            if (variable == null || limit == null) throw new ArgumentNullException(variable == null ? nameof(variable) : nameof(limit));
            var g = variable.Governing;
            return new SteelDeformationCheck("Deflection.Variable", Math.Abs(g.Value), limit.Value, "mm", g.Distance, g.Label, clause, limit.Source);
        }

        /// <summary>Relative transverse displacement of the span ends (storey drift Delta of EN 1990 A1.4.3 u_i, NTC Delta): the
        /// difference of one component, or the magnitude of the difference of both, against h/n.</summary>
        public static SteelDeformationCheck RelativeDisplacement(double startValue1, double endValue1, double? startValue2, double? endValue2,
            SteelDeformationLimit limit, string clause)
        {
            if (limit == null) throw new ArgumentNullException(nameof(limit));
            if (startValue2.HasValue != endValue2.HasValue) throw new ArgumentException("BothSecondComponentsRequired");
            double d1 = SteelDisplacementPoint.Finite(endValue1, nameof(endValue1)) - SteelDisplacementPoint.Finite(startValue1, nameof(startValue1));
            double d2 = startValue2.HasValue ? SteelDisplacementPoint.Finite(endValue2.Value, nameof(endValue2)) - SteelDisplacementPoint.Finite(startValue2.Value, nameof(startValue2)) : 0;
            return new SteelDeformationCheck("Drift", Math.Sqrt(d1 * d1 + d2 * d2), limit.Value, "mm", null, null, clause, limit.Source);
        }

        /// <summary>f = K/sqrt(delta), delta in mm and f in Hz. K = 17.75 for the simply supported member with uniform mass; another K
        /// only with a qualified derivation for the actual scheme.</summary>
        public static double NaturalFrequency(double deflection, double coefficient)
        {
            if (!(SteelDisplacementPoint.Finite(coefficient, nameof(coefficient)) > 0)) throw new ArgumentOutOfRangeException(nameof(coefficient));
            if (!(SteelDisplacementPoint.Finite(deflection, nameof(deflection)) > 0)) throw new ArgumentException("PositiveMassDeflectionRequired");
            return coefficient / Math.Sqrt(deflection);
        }

        /// <summary>Utilization f_min/f: the declared minimum frequency is the demand, the natural frequency the capacity.</summary>
        public static SteelDeformationCheck Frequency(double frequency, double minimumFrequency, double? location, string label, string clause, string source)
        {
            if (!(SteelDisplacementPoint.Finite(minimumFrequency, nameof(minimumFrequency)) > 0)) throw new ArgumentOutOfRangeException(nameof(minimumFrequency));
            if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("FrequencyLimitSourceRequired", nameof(source));
            return new SteelDeformationCheck("Vibration.Frequency", minimumFrequency, frequency, "Hz", location, label, clause, source);
        }
    }
}
