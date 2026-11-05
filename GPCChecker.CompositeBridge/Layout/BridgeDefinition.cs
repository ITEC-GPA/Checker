#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using GPC.Geometry;
using GPC.Model.Core;

namespace GPC.Checkers.CompositeBridge.Layout
{
    /// <summary>Persistent construction inputs before meshing. Lengths and curve coordinates are mm; skew is rad,
    /// crossfall is a dimensionless slope (not percent). Geometry generation and Rhino conversion are consumers.
    /// Bridge vocabulary: moved from GPCModel (GPC.Model.Structure.Bridges) by lane L12; persisted with <see cref="Save"/>.</summary>
    [Serializable,DataContract]
    public sealed class BridgeDefinition
    {
        /// <summary>The XML of the definition (data contracts; the curve, points and frames with the vocabulary of GPCModel).</summary>
        public byte[] Save(){Validate();return LayoutContracts.Save(this);}
        /// <summary>Reads <see cref="Save"/> (at most 256 MiB) and validates the definition.</summary>
        public static BridgeDefinition Read(byte[] bytes)=>LayoutContracts.Read<BridgeDefinition>(bytes,"MissingBridgeDefinition");
        /// <summary>A detached copy.</summary>
        public BridgeDefinition Copy()=>LayoutContracts.Copy(this);
        [DataMember(Order=0,IsRequired=true)] private int schema=1;
        [field:DataMember(Order=1)] public Guid Id {get;set;}
        [field:DataMember(Order=2)] public Curve3d Alignment {get;set;}
        [field:DataMember(Order=3)] public BridgeSupportLayout Supports {get;set;}=new BridgeSupportLayout();
        [field:DataMember(Order=4)] public BridgeGirderLayout Girders {get;set;}=new BridgeGirderLayout();
        [field:DataMember(Order=5)] public BridgeDeckLayout Deck {get;set;}=new BridgeDeckLayout();
        [field:DataMember(Order=6)] public BridgeFabricationPlan Fabrication {get;set;}
        [field:DataMember(Order=7)] public BridgeDiaphragmLayout Diaphragms {get;set;}=new BridgeDiaphragmLayout();
        [field:DataMember(Order=8)] public BridgeMeshSettings Mesh {get;set;}=new BridgeMeshSettings();
        /// <summary>Alternative to <see cref="Alignment"/> for a host curve that Curve3d cannot represent (e.g. a Rhino NURBS):
        /// exactly one of the two is set. Null keeps the historical archive text and fingerprint (review B4).</summary>
        [field:OptionalField][field:DataMember(Order=9,EmitDefaultValue=false)] public BridgeAlignmentTable AlignmentTable {get;set;}
        /// <summary>The alignment length in mm: the host curve length of the table, or the length of the Geometry curve.</summary>
        public double AlignmentLength => AlignmentTable != null ? AlignmentTable.Length : Alignment.Length;
        public void Validate()
        {
            if(schema!=1)throw new SerializationException("UnsupportedBridgeDefinitionSchema");
            if(Id==Guid.Empty||(Alignment==null)==(AlignmentTable==null)||Alignment!=null&&Alignment.IsClosed)throw new ArgumentException("InvalidBridgeAlignmentIdentity");
            AlignmentTable?.Validate();
            double length=AlignmentLength;BridgeDimensions.Positive(length);
            if(Supports==null||Girders==null||Deck==null||Fabrication==null||Diaphragms==null||Mesh==null)throw new ArgumentException("MissingBridgeDefinitionPart");
            BridgeDimensions.Stations(Supports.Stations,length,2);BridgeDimensions.Expand(Supports.SkewRadians,Supports.Stations.Length);
            if(Supports.SkewRadians.Any(s=>Math.Abs(s)>=Math.PI/2))throw new ArgumentException("TransverseSupportDirectionRequired");
            BridgeDimensions.Values(Girders.Offsets);if(Girders.Offsets.Length<2||Girders.Offsets.Length>32
                ||Girders.Offsets.Zip(Girders.Offsets.Skip(1),(a,b)=>b-a).Any(v=>v<=BridgeDimensions.Tolerance))throw new ArgumentException("InvalidGirderOffsets");
            var levels=BridgeDimensions.Expand(Girders.Levels,Girders.Offsets.Length);
            var widths=BridgeDimensions.Expand(Deck.Widths,2);foreach(var w in widths)BridgeDimensions.Positive(w);
            BridgeDimensions.Positive(Deck.Thickness);
            if(Girders.Offsets[0]<-widths[1]||Girders.Offsets[Girders.Offsets.Length-1]>widths[0])throw new ArgumentException("GirderOutsideDeck");
            if(levels.Any(l=>l>=-Deck.Thickness))throw new ArgumentException("GirderReferenceMustBeBelowSlabSoffit");
            if(Deck.BeamLevel.HasValue){BridgeDimensions.Finite(Deck.BeamLevel.Value);if(Deck.BeamLevel < -Deck.Thickness||Deck.BeamLevel>0)throw new ArgumentException("DeckBeamOutsideSlab");}
            BridgeDimensions.Values(Deck.Slopes);
            if(Deck.Slopes.Length==0||Deck.SlopeStations==null)throw new ArgumentException("MissingDeckCrossfall");
            if(Deck.Slopes.Length==1){if(Deck.SlopeStations.Length!=0)throw new ArgumentException("ConstantCrossfallHasNoStationPairs");}
            else
            {
                BridgeDimensions.Stations(Deck.SlopeStations,length,2);
                if(Deck.Slopes.Length!=Deck.SlopeStations.Length||Math.Abs(Deck.SlopeStations[0])>BridgeDimensions.Tolerance
                    ||Math.Abs(Deck.SlopeStations[Deck.SlopeStations.Length-1]-length)>BridgeDimensions.Tolerance)throw new ArgumentException("CrossfallMustCoverAlignment");
            }
            Fabrication.Validate();
            if(Math.Abs(Fabrication.Length-length)>BridgeDimensions.Tolerance||Fabrication.GirderCount!=Girders.Offsets.Length)
                throw new ArgumentException("BridgeFabricationGeometryMismatch");
            Mesh.Validate(length);Diaphragms.Validate(length);
            if(Diaphragms.Truss&&levels.Any(l=>l+Diaphragms.TrussHeight>=-Deck.Thickness))throw new ArgumentException("TrussReachesSlab");
        }
        public string Fingerprint(){Validate();return ModelValues.Fingerprint(new object[]{this});}
        [OnDeserialized]private void OnRead(StreamingContext context)=>Validate();
    }
    [Serializable,DataContract] public sealed class BridgeSupportLayout
    {
        [field:DataMember(Order=0)]public double[] Stations {get;set;}=Array.Empty<double>();
        [field:DataMember(Order=1)]public double[] SkewRadians {get;set;}=new[]{0d};
    }
    [Serializable,DataContract] public sealed class BridgeGirderLayout
    {
        /// <summary>Signed transverse offsets, positive towards the alignment's local left.</summary>
        [field:DataMember(Order=0)]public double[] Offsets {get;set;}=Array.Empty<double>();
        /// <summary>Signed normal offsets from the deck top reference, one value or one per girder.</summary>
        [field:DataMember(Order=1)]public double[] Levels {get;set;}=new[]{-1500d};
    }
    [Serializable,DataContract] public sealed class BridgeDeckLayout
    {
        /// <summary>Left and right widths, or one width used on both sides.</summary>
        [field:DataMember(Order=0)]public double[] Widths {get;set;}=new[]{6000d,6000d};
        [field:DataMember(Order=1)]public double Thickness {get;set;}=250;
        [field:DataMember(Order=2)]public double[] SlopeStations {get;set;}=Array.Empty<double>();
        [field:DataMember(Order=3)]public double[] Slopes {get;set;}=new[]{0d};
        [field:DataMember(Order=4)]public double? BeamLevel {get;set;}
    }
    [Serializable,DataContract] public sealed class BridgeMeshSettings
    {
        [field:DataMember(Order=0)]public double MaximumLength {get;set;}=2500;
        [field:DataMember(Order=1)]public double ChordTolerance {get;set;}=5;
        [field:DataMember(Order=2)]public double SlabSpacing {get;set;}
        [field:DataMember(Order=3)]public double[] SlabStations {get;set;}=Array.Empty<double>();
        public void Validate(double length){BridgeDimensions.Positive(MaximumLength);BridgeDimensions.Positive(ChordTolerance);BridgeDimensions.Nonnegative(SlabSpacing);BridgeDimensions.Stations(SlabStations,length,0);}
    }
    public enum BridgeTrussPattern {X,V,Warren}
    [Serializable,DataContract] public sealed class BridgeDiaphragmLayout
    {
        [field:DataMember(Order=0)]public double MaximumSpacing {get;set;}=5000;
        [field:DataMember(Order=1)]public double[] Stations {get;set;}=Array.Empty<double>();
        [field:DataMember(Order=2)]public bool AtSupports {get;set;}=true;
        [field:DataMember(Order=3)]public double TrussHeight {get;set;}=1500;
        [field:DataMember(Order=4)]public int PanelsPerBay {get;set;}=2;
        [field:DataMember(Order=5)]public BridgeTrussPattern Pattern {get;set;}
        [field:DataMember(Order=6)]public bool ConnectCrossings {get;set;}
        [field:DataMember(Order=7)]public bool Truss {get;set;}
        public void Validate(double length)
        {
            BridgeDimensions.Nonnegative(MaximumSpacing);BridgeDimensions.Stations(Stations,length,0);BridgeDimensions.Positive(TrussHeight);
            if(PanelsPerBay<1||PanelsPerBay>100||!Enum.IsDefined(typeof(BridgeTrussPattern),Pattern))throw new ArgumentException("InvalidBridgeTrussLayout");
        }
    }
    [Serializable,DataContract] public sealed class BridgeSegmentSettings
    {
        [field:DataMember(Order=0)]public double MaximumLength {get;set;}=12000;
        /// <summary>0 means common joints; keys 1..GirderCount add joints on an individual girder.</summary>
        [field:DataMember(Order=1)]public Dictionary<int,double[]> Joints {get;set;}=new Dictionary<int,double[]>();
    }
    [Serializable,DataContract] public sealed class BridgeSegmentRange
    {
        [field:DataMember(Order=0)]public int Girder {get;set;}
        [field:DataMember(Order=1)]public int Segment {get;set;}
        [field:DataMember(Order=2)]public double Start {get;set;}
        [field:DataMember(Order=3)]public double End {get;set;}
    }
    /// <summary>Fabrication identities are defined before FEM subdivision. Explicit joints take precedence over MaximumLength.
    /// This preserves the source generator rule: an explicitly jointed girder is not subdivided again by the automatic maximum.</summary>
    [Serializable,DataContract] public sealed class BridgeFabricationPlan
    {
        [field:DataMember(Order=0)]public double Length {get;private set;}
        [field:DataMember(Order=1)]public int GirderCount {get;private set;}
        [field:DataMember(Order=2)]public BridgeSegmentSettings Settings {get;private set;}
        [field:DataMember(Order=3)]public BridgeSegmentRange[] Ranges {get;private set;}
        private BridgeFabricationPlan(){}
        public static BridgeFabricationPlan Create(double length,int girderCount,BridgeSegmentSettings settings)
        {
            var plan=new BridgeFabricationPlan {Length=length,GirderCount=girderCount,Settings=settings==null?null:LayoutContracts.Copy(settings)};
            plan.Ranges=plan.BuildRanges();return plan;
        }
        public void Validate()
        {
            var expected=BuildRanges();
            if(Ranges==null||Ranges.Length!=expected.Length||Ranges.Where((r,i)=>r==null||r.Girder!=expected[i].Girder||r.Segment!=expected[i].Segment
                ||r.Start!=expected[i].Start||r.End!=expected[i].End).Any())throw new ArgumentException("StaleBridgeFabricationRanges");
        }
        private BridgeSegmentRange[] BuildRanges()
        {
            BridgeDimensions.Positive(Length);if(GirderCount<2||GirderCount>32||Settings==null||Settings.Joints==null)throw new ArgumentException("InvalidBridgeFabricationPlan");
            BridgeDimensions.Nonnegative(Settings.MaximumLength);
            foreach(var pair in Settings.Joints)
            {
                if(pair.Key<0||pair.Key>GirderCount||pair.Value==null||pair.Value.Length>20000)throw new ArgumentException("InvalidBridgeJointBranch");
                foreach(var s in pair.Value){BridgeDimensions.Finite(s);if(s<0||s>Length+BridgeDimensions.Tolerance)throw new ArgumentException("BridgeJointOutsideAlignment");}
            }
            var result=new List<BridgeSegmentRange>();
            for(int girder=1;girder<=GirderCount;girder++)
            {
                var joints=(Settings.Joints.TryGetValue(0,out var common)?common:Array.Empty<double>())
                    .Concat(Settings.Joints.TryGetValue(girder,out var local)?local:Array.Empty<double>()).ToArray();
                double pieces=Settings.MaximumLength==0||joints.Length!=0?1:Math.Ceiling(Length/Settings.MaximumLength);
                if(pieces>20000)throw new ArgumentException("TooManyBridgeFabricationSegments");
                var values=joints.Length!=0?joints:Enumerable.Range(0,(int)pieces+1).Select(i=>Length*i/pieces).ToArray();
                var stations=new List<double>();
                foreach(var value in values.Concat(new[]{0d,Length}).OrderBy(v=>v))
                {
                    double s=Math.Min(Length,value);
                    if(stations.Count==0||s-stations[stations.Count-1]>BridgeDimensions.Tolerance)stations.Add(s);
                }
                if(stations.Count>20000)throw new ArgumentException("TooManyBridgeFabricationStations");
                for(int i=0;i+1<stations.Count;i++)result.Add(new BridgeSegmentRange {Girder=girder,Segment=i+1,Start=stations[i],End=stations[i+1]});
            }
            return result.ToArray();
        }
        [OnDeserialized]private void OnRead(StreamingContext context)=>Validate();
    }
    internal static class BridgeDimensions
    {
        internal const double Tolerance=1e-5; // mm; matches the source fabrication coincidence tolerance.
        internal static void Finite(double v){if(double.IsNaN(v)||double.IsInfinity(v))throw new ArgumentException("NonFiniteBridgeDimension");}
        internal static void Nonnegative(double v){Finite(v);if(v<0)throw new ArgumentException("NegativeBridgeDimension");}
        internal static void Positive(double v){Finite(v);if(v<=Tolerance)throw new ArgumentException("NonPositiveBridgeDimension");}
        internal static void Values(double[] values){if(values==null)throw new ArgumentException("MissingBridgeDimensions");foreach(var v in values)Finite(v);}
        internal static double[] Expand(double[] values,int count)
        {Values(values);if(values.Length!=1&&values.Length!=count)throw new ArgumentException("BridgeDimensionCountMismatch");return values.Length==1?Enumerable.Repeat(values[0],count).ToArray():(double[])values.Clone();}
        internal static void Stations(double[] values,double length,int minimum)
        {
            Values(values);if(values.Length<minimum||values.Length>20000||values.Any(v=>v<0||v>length+Tolerance)
                ||values.Zip(values.Skip(1),(a,b)=>b-a).Any(v=>v<=Tolerance))throw new ArgumentException("InvalidBridgeStations");
        }
    }
}
