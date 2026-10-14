using System;
using GPC.Checkers.Concrete.Detailing;
using GPC.Model.Checking.Contracts;
using GPC.Model.Core.Diagnostics;

namespace GPC.Checkers.Concrete.Walls
{
    public enum WallMember { Stem, Toe, Heel }
    public enum WallFace { Top, Bottom }

    /// <summary>Immutable rectangular strip, mm. Cover is clear cover to longitudinal bars, without links.
    /// Actions and capacities are totals on this width, never values per metre implicitly rescaled.</summary>
    public sealed class WallStrip
    {
        public WallMember Member { get; }
        public double Width { get; }
        public double Thickness { get; }
        public double Cover { get; }
        public int TopCount { get; }
        public double TopDiameter { get; }
        public int BottomCount { get; }
        public double BottomDiameter { get; }
        public WallStrip(WallMember member, double width, double thickness, double cover,
            int topCount, double topDiameter, int bottomCount, double bottomDiameter)
        {
            if (!Enum.IsDefined(typeof(WallMember), member)) throw new ArgumentOutOfRangeException(nameof(member));
            Positive(width, nameof(width)); Positive(thickness, nameof(thickness));
            Positive(topDiameter, nameof(topDiameter)); Positive(bottomDiameter, nameof(bottomDiameter));
            if (!Finite(cover) || cover < 0) throw new ArgumentOutOfRangeException(nameof(cover));
            if (topCount < 2 || bottomCount < 2) throw new ArgumentException("At least two bars per face are required.");
            if (2 * cover + topDiameter + bottomDiameter >= thickness ||
                width - 2 * cover - topCount * topDiameter <= 0 || width - 2 * cover - bottomCount * bottomDiameter <= 0)
                throw new ArgumentException("Bars and clear cover do not fit the strip.");
            Member = member; Width = width; Thickness = thickness; Cover = cover;
            TopCount = topCount; TopDiameter = topDiameter; BottomCount = bottomCount; BottomDiameter = bottomDiameter;
        }
        public double ConcreteArea => Width * Thickness;
        public double TopArea => TopCount * Math.PI * Math.Pow(TopDiameter, 2) / 4;
        public double BottomArea => BottomCount * Math.PI * Math.Pow(BottomDiameter, 2) / 4;
        public WallFace TensionFace(double moment)
        {
            if (!Finite(moment)) throw new ArgumentOutOfRangeException(nameof(moment));
            return moment >= 0 ? WallFace.Bottom : WallFace.Top;
        }
        public double EffectiveDepth(WallFace face)
        {
            if (!Enum.IsDefined(typeof(WallFace), face)) throw new ArgumentOutOfRangeException(nameof(face));
            return Thickness - Cover - (face == WallFace.Top ? TopDiameter : BottomDiameter) / 2;
        }
        public double TensionArea(double moment)
        {
            var face = TensionFace(moment);
            // Preserve the evaluation order used by the existing no-link shear check.
            double diameter = face == WallFace.Top ? TopDiameter : BottomDiameter;
            return (face == WallFace.Top ? TopCount : BottomCount) * Math.PI * diameter * diameter / 4;
        }
        public double MaximumBarSpacing => Math.Max((Width - 2 * Cover - TopDiameter) / (TopCount - 1),
            (Width - 2 * Cover - BottomDiameter) / (BottomCount - 1));
        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        internal static void Positive(double value, string name)
        { if (!Finite(value) || value <= 0) throw new ArgumentOutOfRangeException(name); }
    }

    /// <summary>Demand/capacity in the declared unit; unavailable capacity is never a zero or a pass.</summary>
    public sealed class WallSectionCheck
    {
        public string Method { get; }
        public string Unit { get; }
        public double Demand { get; }
        public double? Capacity { get; }
        public double? Ratio { get; }
        public EngineeringOutcome Outcome { get; }
        public ExecutionStatus Execution { get; }
        public DataStatus Data { get; }
        public string Diagnostic { get; }
        internal WallSectionCheck(string method, string unit, double demand, double? capacity,
            ExecutionStatus execution = ExecutionStatus.Completed, string diagnostic = null)
        {
            if (!WallStrip.Finite(demand) || demand < 0 || capacity.HasValue && (!WallStrip.Finite(capacity.Value) || capacity < 0))
                throw new ArgumentException("Finite, nonnegative demand and capacity required.");
            Method = method; Unit = unit; Demand = demand; Capacity = capacity; Execution = execution; Diagnostic = diagnostic;
            // Solver failure does not imply missing input data.
            Data = capacity.HasValue || execution == ExecutionStatus.Error ? DataStatus.Ready : DataStatus.Insufficient;
            Ratio = capacity > 0 ? demand / capacity : demand == 0 && capacity == 0 ? 0 : (double?)null;
            Outcome = execution != ExecutionStatus.Completed || !capacity.HasValue ? EngineeringOutcome.NotEvaluated
                : demand <= capacity ? EngineeringOutcome.Satisfied : EngineeringOutcome.NotSatisfied;
        }
    }

    public sealed class WallReinforcementChecks
    {
        public WallSectionCheck MinimumPerFace { get; }
        public WallSectionCheck MaximumTotal { get; }
        internal WallReinforcementChecks(WallSectionCheck minimum, WallSectionCheck maximum)
        { MinimumPerFace = minimum; MaximumTotal = maximum; }
    }

    public static class WallStripDetailing
    {
        /// <summary>Existing NTC retaining-wall policy: use the larger effective depth for the required
        /// minimum on both faces; a stem also requires half the wall vertical minimum on each face.
        /// This checks only longitudinal areas, not anchorage, laps, horizontal bars or transverse ties.</summary>
        public static WallReinforcementChecks Evaluate(WallStrip strip, double fctm, double fyk)
        {
            if (strip == null) throw new ArgumentNullException(nameof(strip));
            WallStrip.Positive(fctm, nameof(fctm)); WallStrip.Positive(fyk, nameof(fyk));
            double depth = strip.Thickness - strip.Cover - Math.Min(strip.TopDiameter, strip.BottomDiameter) / 2;
            double minimum = LongitudinalReinforcementLimits.MinimumBendingRatio(fctm, fyk) * strip.Width * depth;
            if (strip.Member == WallMember.Stem) minimum = Math.Max(minimum,
                (LongitudinalReinforcementLimits.MinimumWallVerticalRatio / 2) * strip.Width * strip.Thickness);
            return new WallReinforcementChecks(
                new WallSectionCheck("Wall.LongitudinalMinimumPerFace", "mm2", minimum, Math.Min(strip.TopArea, strip.BottomArea)),
                new WallSectionCheck("Wall.LongitudinalMaximum", "mm2", strip.TopArea + strip.BottomArea,
                    LongitudinalReinforcementLimits.MaximumRatioOutsideLaps * strip.Width * strip.Thickness));
        }
    }
}
