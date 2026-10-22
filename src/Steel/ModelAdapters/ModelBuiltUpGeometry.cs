using System;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.ModelAdapters
{
    /// <summary>Snapshot of two identical, mirrored C/L chords. All dimensions mm.
    /// The clear assembly gap and the centroid spacing are deliberately distinct.
    /// No torsional rigidity, connection restraint or composite action is inferred.</summary>
    public sealed class BuiltUpChordGeometry
    {
        public string Family { get; }
        public double Area { get; }
        public double CentroidSpacing { get; }
        /// <summary>One chord's centroidal inertia about the axis normal to the assembly plane.</summary>
        public double InPlaneInertia { get; }
        public double MinimumRadius { get; }
        public bool ChordAxesArePrincipal { get; }
        internal BuiltUpChordGeometry(string family,double area,double spacing,double inertia,double radius,bool principal)
        {Family=family;Area=area;CentroidSpacing=spacing;InPlaneInertia=inertia;MinimumRadius=radius;ChordAxesArePrincipal=principal;}
    }

    public static class ModelBuiltUpGeometry
    {
        public static BuiltUpChordGeometry Create(SectionBuiltUp shape)
        {
            if(shape==null)throw new ArgumentNullException(nameof(shape));
            if(shape.Parts.Count!=2)throw new NotSupportedException("Exactly two identical chords are required.");
            var p=shape.Parts[0];var q=shape.Parts[1];
            // Factory pairs share the component definition. Do not silently identify
            // merely similar sections with different radii, thickness or material data.
            if(!ReferenceEquals(p.Section,q.Section))throw new NotSupportedException("Use a shared chord definition for identical pairs.");
            var s=p.Section;
            if(s.GetType()!=typeof(SectionC)&&s.GetType()!=typeof(SectionL))throw new NotSupportedException("Only exact C/L chords are qualified.");
            bool horizontal=p.MirrorX!=q.MirrorX&&p.MirrorY==q.MirrorY&&p.Y==q.Y;
            bool vertical=p.MirrorY!=q.MirrorY&&p.MirrorX==q.MirrorX&&p.X==q.X;
            if(!horizontal&&!vertical)throw new NotSupportedException("Chords must be paired by one reflection with aligned centroids.");
            var pc=p.Place(s.Centroid.X,s.Centroid.Y);var qc=q.Place(s.Centroid.X,s.Centroid.Y);
            double distance=horizontal?Math.Abs(pc.X-qc.X):Math.Abs(pc.Y-qc.Y);
            double inertia=horizontal?s.Jyy:s.Jxx;
            Sections.Ec3AngleCompression.Positive(distance,"ChordCentroidSpacing");
            Sections.Ec3AngleCompression.Positive(s.Area,"ChordArea");
            Sections.Ec3AngleCompression.Positive(inertia,"ChordInertia");
            double minimumRadius=Math.Sqrt(Math.Min(s.J11,s.J22)/s.Area);
            Sections.Ec3AngleCompression.Positive(minimumRadius,"MinimumChordRadius");
            // Bounds are a sufficient non-overlap test for the supported mirrored
            // layouts. Generic interleaved/overlapping assemblies remain unsupported.
            double lo1=double.MaxValue,hi1=double.MinValue,lo2=double.MaxValue,hi2=double.MinValue;
            foreach(var point in s.Shape.Fill)
            {
                var a=p.Place(point.X,point.Y);var b=q.Place(point.X,point.Y);
                double c1=horizontal?a.X:a.Y,c2=horizontal?b.X:b.Y;
                lo1=Math.Min(lo1,c1);hi1=Math.Max(hi1,c1);lo2=Math.Min(lo2,c2);hi2=Math.Max(hi2,c2);
            }
            if(hi1>lo2&&hi2>lo1)throw new NotSupportedException("Chord regions overlap in the assembly direction.");
            return new BuiltUpChordGeometry(s is SectionL?"2L":"2C",s.Area,distance,inertia,minimumRadius,s.Jxy==0);
        }
    }
}
