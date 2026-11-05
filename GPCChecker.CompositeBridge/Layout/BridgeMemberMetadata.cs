#nullable disable
using System.Runtime.Serialization;
using GPC.Geometry;
using GPC.Model.Core.Coordinates;

namespace GPC.Checkers.CompositeBridge.Layout;

/// <summary>The families of the members of a bridge (moved from GPCModel by lane L12: bridge vocabulary).</summary>
public enum BridgeMemberFamily { Girder, SolidDiaphragm, TopChord, BottomChord, Post, Diagonal, LongitudinalBracing, BracingPost, DeckTransverse }

/// <summary>Physical bridge labels of a beam (one FE part of a physical member: BridGHe splits one member into parts "Id/P1", "Id/P2"...,
/// each with its own stations, SectionUp and GeometryId), independent of organizational groups and FEM material states. Stations are
/// coordinates along the alignment in mm (they may decrease); SectionUp is a global geometric direction, not a force convention. Immutable.
/// Held by <see cref="BridgeModelData"/> per beam GUID (lane L12; in GPCModel until then).</summary>
[DataContract]
public sealed class BridgeMemberMetadata
{
    [DataMember(Order = 0)] public string PhysicalId { get; private set; }
    [DataMember(Order = 1)] public BridgeMemberFamily Family { get; private set; }
    [DataMember(Order = 2)] public int Group { get; private set; }
    [DataMember(Order = 3)] public int Segment { get; private set; }
    [DataMember(Order = 4)] public int Span { get; private set; }
    [DataMember(Order = 5)] public double StationFromMm { get; private set; }
    [DataMember(Order = 6)] public double StationToMm { get; private set; }
    [DataMember(Order = 7)] private Vector3d sectionUp;
    [DataMember(Order = 8)] public string GeometryId { get; private set; }
    /// <summary>The section up direction (a copy).</summary>
    public Vector3d SectionUp => new Vector3d(sectionUp.X, sectionUp.Y, sectionUp.Z);

    public BridgeMemberMetadata(string physicalId, BridgeMemberFamily family, int group, int segment, int span,
        double stationFromMm, double stationToMm, Vector3d sectionUp, string geometryId = "")
    {
        PhysicalId = physicalId; Family = family; Group = group; Segment = segment; Span = span;
        StationFromMm = stationFromMm; StationToMm = stationToMm; GeometryId = geometryId;
        this.sectionUp = sectionUp == null ? null : new Vector3d(sectionUp.X, sectionUp.Y, sectionUp.Z);
        string error = Error();
        if (error != null) throw new ArgumentException(error);
    }

    [OnDeserialized] private void OnRead(StreamingContext context) { string error = Error(); if (error != null) throw new SerializationException(error); }

    private string Error()
    {
        if (string.IsNullOrWhiteSpace(PhysicalId) || !Enum.IsDefined(typeof(BridgeMemberFamily), Family) || sectionUp == null || GeometryId == null)
            return "InvalidBridgeMemberMetadata";
        if (!Finite(StationFromMm) || !Finite(StationToMm)) return "NonFiniteBridgeMemberStation";
        double up = Axes.Length(sectionUp);
        if (!Finite(up)) return "NonFiniteBridgeSectionUp";
        if (up <= 0) return "ZeroBridgeSectionUp";
        return null;
    }

    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
