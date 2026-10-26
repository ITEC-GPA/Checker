using System;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Non-structural parallel packing, mm. One unspliced full-footprint plate
    /// per cover face and member side; equal packing on the left and right members.
    /// Both inner strips share thickness; both web faces share thickness. No taper,
    /// stacks or transition between different parent sections. Plates have the same
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
        public WeldedSplicePacking(double topOuter, double topInner, double bottomOuter, double bottomInner,
            double eachWeb, string executionSource)
        {
            foreach (double v in new[] { topOuter, topInner, bottomOuter, bottomInner, eachWeb })
                ConnectionValidation.Nonnegative(v, nameof(v));
            TopOuter = topOuter; TopInner = topInner; BottomOuter = bottomOuter; BottomInner = bottomInner;
            EachWeb = eachWeb; ExecutionSource = executionSource;
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
            Thickness(cover);
            bool top = cover == WeldedSpliceCover.TopOuter || cover == WeldedSpliceCover.TopInnerNegative || cover == WeldedSpliceCover.TopInnerPositive;
            bool bottom = cover == WeldedSpliceCover.BottomOuter || cover == WeldedSpliceCover.BottomInnerNegative || cover == WeldedSpliceCover.BottomInnerPositive;
            var face = top ? detail.Top : detail.Bottom;
            return top || bottom ? (top ? detail.Section.TopFlange.Thickness + TopOuter + TopInner
                : detail.Section.BottomFlange.Thickness + BottomOuter + BottomInner) + face.Outer.Thickness + face.EachInner.Thickness
                : detail.Section.Web.Thickness + 2 * (detail.EachWebCover.Thickness + EachWeb);
        }
        internal bool SamePhysical(WeldedSplicePacking other) => TopOuter == other.TopOuter && TopInner == other.TopInner
            && BottomOuter == other.BottomOuter && BottomInner == other.BottomInner && EachWeb == other.EachWeb;
    }
}
