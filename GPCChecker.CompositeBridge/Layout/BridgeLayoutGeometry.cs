#nullable disable
using System;
using System.Linq;
using System.Runtime.Serialization;
using GPC.Geometry;
using GPC.Model.Core;
using GPC.Model.Core.Coordinates;

namespace GPC.Checkers.CompositeBridge.Layout
{
    /// <summary>Geometric bridge definition after sampling, before assigning FEM properties, loads or restraints.
    /// Coordinates/stations are mm. Node references are one-based within this snapshot; they are never solver IDs.
    /// Bridge vocabulary: GPC.Model.Structure.Bridges.BridgeGeometry until lane L12, renamed because CompositeBridge has the section
    /// record <see cref="CompositeBridge.BridgeGeometry"/>; persisted with <see cref="Save"/>.</summary>
    [Serializable,DataContract]
    public sealed class BridgeLayoutGeometry
    {
        /// <summary>The XML of the geometry (data contracts; points and frames with the vocabulary of GPCModel, shared points kept).</summary>
        public byte[] Save(){Validate();return LayoutContracts.Save(this);}
        /// <summary>Reads <see cref="Save"/> (at most 256 MiB) and validates the geometry.</summary>
        public static BridgeLayoutGeometry Read(byte[] bytes)=>LayoutContracts.Read<BridgeLayoutGeometry>(bytes,"MissingBridgeLayoutGeometry");
        /// <summary>A detached copy (shared points stay shared within the copy).</summary>
        public BridgeLayoutGeometry Copy()=>LayoutContracts.Copy(this);
        [DataMember(Order=0,IsRequired=true)] private int schema=1;
        [field:DataMember(Order=1)] public Guid Id {get;set;}
        [field:DataMember(Order=2)] public Point3d[] Nodes {get;set;}=Array.Empty<Point3d>();
        [field:DataMember(Order=3)] public BridgeAxisMember[] Members {get;set;}=Array.Empty<BridgeAxisMember>();
        [field:DataMember(Order=4)] public BridgeSupportPoint[] Supports {get;set;}=Array.Empty<BridgeSupportPoint>();
        [field:DataMember(Order=5)] public BridgeJointPoint[] Joints {get;set;}=Array.Empty<BridgeJointPoint>();
        [field:DataMember(Order=6)] public BridgeSegmentRange[] Segments {get;set;}=Array.Empty<BridgeSegmentRange>();
        [field:DataMember(Order=7)] public CoordinateSystem[] Frames {get;set;}=Array.Empty<CoordinateSystem>();
        [field:DataMember(Order=8)] public double[] Stations {get;set;}=Array.Empty<double>();
        [field:DataMember(Order=9)] public BridgeDeckMesh Deck {get;set;}
        [field:DataMember(Order=10)] public double DeckThickness {get;set;}
        [field:DataMember(Order=11)] public Point3d[] LeftEdge {get;set;}
        [field:DataMember(Order=12)] public Point3d[] RightEdge {get;set;}

        public void Validate()
        {
            if(schema!=1)throw new SerializationException("UnsupportedBridgeGeometrySchema");
            if(Id==Guid.Empty||Nodes==null||Nodes.Length<2||Members==null||Supports==null||Joints==null||Segments==null||Frames==null||Stations==null||Stations.Length<2||Frames.Length!=Stations.Length)
                throw new ArgumentException("InvalidBridgeGeometryGraph");
            double length=Stations[Stations.Length-1];BridgeDimensions.Stations(Stations,length,2);
            foreach(var p in Nodes)Point(p);foreach(var f in Frames)Axes.Validate(f);
            if(Members.Any(m=>m==null)||Members.Select(m=>m.Guid).Distinct().Count()!=Members.Length||Members.Select(m=>m.Id).Distinct(StringComparer.Ordinal).Count()!=Members.Length)
                throw new ArgumentException("DuplicateBridgeAxisIdentity");
            foreach(var m in Members)
            {
                m.Validate();Station(m.StationFrom,length);Station(m.StationTo,length);
                if(m.NodeFrom<1||m.NodeTo<1||m.NodeFrom>Nodes.Length||m.NodeTo>Nodes.Length||m.NodeFrom==m.NodeTo||Axes.Length(Nodes[m.NodeTo-1]-Nodes[m.NodeFrom-1])<=BridgeDimensions.Tolerance)
                    throw new ArgumentException("InvalidBridgeAxisConnectivity");
            }
            foreach(var p in Supports)
            {if(p==null||p.Support<1||p.Girder<1)throw new ArgumentException("InvalidBridgeSupportPoint");Station(p.Station,length);Point(p.Point);}
            foreach(var p in Joints)
            {if(p==null||p.Joint<1||p.Girder<1)throw new ArgumentException("InvalidBridgeJointPoint");Station(p.Station,length);Point(p.Point);}
            foreach(var s in Segments)
            {if(s==null||s.Girder<1||s.Segment<1||s.End<=s.Start)throw new ArgumentException("InvalidBridgeSegmentRange");Station(s.Start,length);Station(s.End,length);}
            BridgeDimensions.Nonnegative(DeckThickness);Deck?.Validate();
            foreach(var edge in new[]{LeftEdge,RightEdge})if(edge!=null){if(edge.Length<2)throw new ArgumentException("InvalidBridgeDeckEdge");foreach(var p in edge)Point(p);}
        }
        public Line3d Axis(Guid memberGuid)
        {
            Validate();var member=Members.SingleOrDefault(m=>m.Guid==memberGuid)??throw new ArgumentException("UnknownBridgeAxisMember");
            return new Line3d(new Point3d(Nodes[member.NodeFrom-1]),new Point3d(Nodes[member.NodeTo-1]));
        }
        public string Fingerprint(){Validate();return ModelValues.Fingerprint(new object[]{this});}
        internal static void Point(Point3d p){if(p==null||!Axes.IsFinite(p))throw new ArgumentException("InvalidBridgeGeometryPoint");}
        private static void Station(double station,double length)
        {BridgeDimensions.Finite(station);if(station<0||station>length+BridgeDimensions.Tolerance)throw new ArgumentException("BridgePointOutsideStationDomain");}
        [OnDeserialized]private void OnRead(StreamingContext context)=>Validate();
    }

    [Serializable,DataContract]
    public sealed class BridgeAxisMember
    {
        [field:DataMember(Order=0)]public Guid Guid {get;set;}
        [field:DataMember(Order=1)]public string Id {get;set;}
        [field:DataMember(Order=2)]public BridgeMemberFamily Family {get;set;}
        [field:DataMember(Order=3)]public int Group {get;set;}
        [field:DataMember(Order=4)]public int Segment {get;set;}
        [field:DataMember(Order=5)]public int Span {get;set;}
        [field:DataMember(Order=6)]public int NodeFrom {get;set;}
        [field:DataMember(Order=7)]public int NodeTo {get;set;}
        [field:DataMember(Order=8)]public double StationFrom {get;set;}
        [field:DataMember(Order=9)]public double StationTo {get;set;}
        public string PhysicalId=>Family+":"+Group+":"+Segment;
        public void Validate()
        {
            if(Guid==System.Guid.Empty||string.IsNullOrWhiteSpace(Id)||!Enum.IsDefined(typeof(BridgeMemberFamily),Family)||Group<1||Segment<1||Span<0)
                throw new ArgumentException("InvalidBridgeAxisMember");
            BridgeDimensions.Finite(StationFrom);BridgeDimensions.Finite(StationTo);
        }
        [OnDeserialized]private void OnRead(StreamingContext context)=>Validate();
    }
    [Serializable,DataContract]public sealed class BridgeSupportPoint
    {
        [field:DataMember(Order=0)]public int Support {get;set;}
        [field:DataMember(Order=1)]public int Girder {get;set;}
        [field:DataMember(Order=2)]public double Station {get;set;}
        [field:DataMember(Order=3)]public Point3d Point {get;set;}
    }
    [Serializable,DataContract]public sealed class BridgeJointPoint
    {
        [field:DataMember(Order=0)]public int Girder {get;set;}
        [field:DataMember(Order=1)]public int Joint {get;set;}
        [field:DataMember(Order=2)]public double Station {get;set;}
        [field:DataMember(Order=3)]public Point3d Point {get;set;}
    }
    /// <summary>Persistent triangle/quad connectivity of the sampled deck, with zero-based vertex indices.
    /// Only structural geometry is stored: Rhino mesh colours, cached normals and display state stay in the plugin.</summary>
    [Serializable,DataContract]public sealed class BridgeDeckMesh
    {
        [field:DataMember(Order=0)]public Point3d[] Vertices {get;set;}=Array.Empty<Point3d>();
        [field:DataMember(Order=1)]public int[][] Faces {get;set;}=Array.Empty<int[]>();
        public void Validate()
        {
            if(Vertices==null||Faces==null)throw new ArgumentException("InvalidBridgeDeckMesh");foreach(var p in Vertices)BridgeLayoutGeometry.Point(p);
            foreach(var f in Faces)
            {
                if(f==null||(f.Length!=3&&f.Length!=4)||f.Distinct().Count()!=f.Length||f.Any(i=>i<0||i>=Vertices.Length))throw new ArgumentException("InvalidBridgeDeckFace");
                Vector3d a=Vertices[f[1]]-Vertices[f[0]],b=Vertices[f[2]]-Vertices[f[0]];
                if(Axes.Length(a.CrossProduct(b))<=1e-10)throw new ArgumentException("DegenerateBridgeDeckFace");
                if(f.Length==4){Vector3d c=Vertices[f[3]]-Vertices[f[0]];if(Axes.Length(b.CrossProduct(c))<=1e-10||Axes.Dot(a.CrossProduct(b),b.CrossProduct(c))<=0)throw new ArgumentException("DegenerateOrFoldedBridgeDeckFace");}
            }
        }
        [OnDeserialized]private void OnRead(StreamingContext context)=>Validate();
    }
}
