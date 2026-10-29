using System;
using System.Collections.Generic;
using System.Linq;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Non-structural parallel packing, mm. One or more unspliced full-footprint parallel plates
    /// per cover face and member side; equal packing on the left and right members, except across a
    /// WeldedSpliceSectionTransition, whose RightPacking fills the thickness change of the right member.
    /// Both inner strips share thickness; both web faces share thickness. No taper. Plates have the same
    /// grade as their associated cover. No packing area, bearing, stiffness or butt
    /// contact is credited. ExecutionSource identifies the fitted detail, full contact,
    /// adequate bolt grip/thread position, washers and assembly specification.</summary>
    public sealed class WeldedSplicePacking
    {
        public static WeldedSplicePacking None { get; } = new WeldedSplicePacking(0, 0, 0, 0, 0, null);
        public double TopOuter { get; }
        public double TopInner { get; }
        public double BottomOuter { get; }
        public double BottomInner { get; }
        public double EachWeb { get; }
        public string ExecutionSource { get; }
        public bool HasPacking => TopOuter > 0 || TopInner > 0 || BottomOuter > 0 || BottomInner > 0 || EachWeb > 0;
        /// <summary>Physical layers ordered from cover towards parent. Empty means no packing.</summary>
        public IReadOnlyList<double> TopOuterLayers { get; }
        public IReadOnlyList<double> TopInnerLayers { get; }
        public IReadOnlyList<double> BottomOuterLayers { get; }
        public IReadOnlyList<double> BottomInnerLayers { get; }
        public IReadOnlyList<double> EachWebLayers { get; }
        public WeldedSplicePacking(double topOuter, double topInner, double bottomOuter, double bottomInner,
            double eachWeb, string executionSource)
            : this(Single(topOuter), Single(topInner), Single(bottomOuter), Single(bottomInner), Single(eachWeb), executionSource) { }

        public static WeldedSplicePacking FromLayers(IEnumerable<double> topOuter, IEnumerable<double> topInner,
            IEnumerable<double> bottomOuter, IEnumerable<double> bottomInner, IEnumerable<double> eachWeb, string executionSource)
            => new WeldedSplicePacking(topOuter, topInner, bottomOuter, bottomInner, eachWeb, executionSource);

        private WeldedSplicePacking(IEnumerable<double> topOuter, IEnumerable<double> topInner,
            IEnumerable<double> bottomOuter, IEnumerable<double> bottomInner, IEnumerable<double> eachWeb, string executionSource)
        {
            TopOuterLayers = Copy(topOuter); TopInnerLayers = Copy(topInner);
            BottomOuterLayers = Copy(bottomOuter); BottomInnerLayers = Copy(bottomInner); EachWebLayers = Copy(eachWeb);
            TopOuter = TopOuterLayers.Sum(); TopInner = TopInnerLayers.Sum(); BottomOuter = BottomOuterLayers.Sum();
            BottomInner = BottomInnerLayers.Sum(); EachWeb = EachWebLayers.Sum(); ExecutionSource = executionSource;
        }
        private static double[] Single(double thickness)
        {
            ConnectionValidation.Nonnegative(thickness, nameof(thickness));
            return thickness == 0 ? new double[0] : new[] { thickness };
        }
        private static IReadOnlyList<double> Copy(IEnumerable<double> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var layers = source.ToArray();
            foreach (double value in layers) ConnectionValidation.Positive(value, nameof(source));
            ConnectionValidation.Nonnegative(layers.Sum(), nameof(source));
            return Array.AsReadOnly(layers);
        }
        public IReadOnlyList<double> Layers(WeldedSpliceCover cover)
        {
            switch (cover)
            {
                case WeldedSpliceCover.TopOuter: return TopOuterLayers;
                case WeldedSpliceCover.TopInnerNegative: case WeldedSpliceCover.TopInnerPositive: return TopInnerLayers;
                case WeldedSpliceCover.BottomOuter: return BottomOuterLayers;
                case WeldedSpliceCover.BottomInnerNegative: case WeldedSpliceCover.BottomInnerPositive: return BottomInnerLayers;
                case WeldedSpliceCover.WebNegative: case WeldedSpliceCover.WebPositive: return EachWebLayers;
                default: throw new ArgumentOutOfRangeException(nameof(cover));
            }
        }
        public double Thickness(WeldedSpliceCover cover)
        {
            switch (cover)
            {
                case WeldedSpliceCover.TopOuter: return TopOuter;
                case WeldedSpliceCover.TopInnerNegative: case WeldedSpliceCover.TopInnerPositive: return TopInner;
                case WeldedSpliceCover.BottomOuter: return BottomOuter;
                case WeldedSpliceCover.BottomInnerNegative: case WeldedSpliceCover.BottomInnerPositive: return BottomInner;
                case WeldedSpliceCover.WebNegative: case WeldedSpliceCover.WebPositive: return EachWeb;
                default: throw new ArgumentOutOfRangeException(nameof(cover));
            }
        }
        public double ThickerFace(WeldedSpliceCover cover)
        {
            Thickness(cover); // Reject undefined enum values even on the web branch.
            return cover == WeldedSpliceCover.TopOuter || cover == WeldedSpliceCover.TopInnerNegative || cover == WeldedSpliceCover.TopInnerPositive
                ? Math.Max(TopOuter, TopInner)
                : cover == WeldedSpliceCover.BottomOuter || cover == WeldedSpliceCover.BottomInnerNegative || cover == WeldedSpliceCover.BottomInnerPositive
                ? Math.Max(BottomOuter, BottomInner) : EachWeb;
        }
        public double Grip(WeldedIBeamSpliceForceInput detail, WeldedSpliceCover cover)
        {
            if (detail == null) throw new ArgumentNullException(nameof(detail));
            return Grip(detail, detail.Section, cover);
        }
        /// <summary>Clamped thickness on the member of <paramref name="section"/> (the right one across a transition).</summary>
        internal double Grip(WeldedIBeamSpliceForceInput detail, WeldedIBeamSpliceSection section, WeldedSpliceCover cover)
        {
            Thickness(cover);
            bool top = cover == WeldedSpliceCover.TopOuter || cover == WeldedSpliceCover.TopInnerNegative || cover == WeldedSpliceCover.TopInnerPositive;
            bool bottom = cover == WeldedSpliceCover.BottomOuter || cover == WeldedSpliceCover.BottomInnerNegative || cover == WeldedSpliceCover.BottomInnerPositive;
            var face = top ? detail.Top : detail.Bottom;
            return top || bottom ? (top ? section.TopFlange.Thickness + TopOuter + TopInner
                : section.BottomFlange.Thickness + BottomOuter + BottomInner) + face.Outer.Thickness + face.EachInner.Thickness
                : section.Web.Thickness + 2 * (detail.EachWebCover.Thickness + EachWeb);
        }
        internal bool SamePhysical(WeldedSplicePacking other) => other != null
            && TopOuterLayers.SequenceEqual(other.TopOuterLayers) && TopInnerLayers.SequenceEqual(other.TopInnerLayers)
            && BottomOuterLayers.SequenceEqual(other.BottomOuterLayers) && BottomInnerLayers.SequenceEqual(other.BottomInnerLayers)
            && EachWebLayers.SequenceEqual(other.EachWebLayers);
    }
}
