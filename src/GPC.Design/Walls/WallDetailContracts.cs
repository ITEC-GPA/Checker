using GPC.Checkers.Concrete.Durability;
using GPC.Model.Checking.Contracts;
using GPC.Model.Core.Diagnostics;

namespace GPC.Design.Walls;

public enum WallRebarZone { Stem, StemUpper, Toe, Heel }
public enum WallRebarFace { Upstream, Downstream, Bottom, Top }
public enum WallDetailRule { ClearSpacing, MainSpacing, Cover, CommonLap, LapPairClearSpacing, LapClearDistance,
    LapPercentAtBoundary, LapExtent, BarCountContinuity, SecondaryArea, SecondarySpacing, Mandrel, BendHeight,
    TailExtent, FootAnchorage, RootAnchorage, AnchorageExtent, JunctionTieSpacing, JunctionTieArea }
/// <summary>Direction of the foot (horizontal tail) of a stem bar in the foundation: towards the toe (valley) or the heel (mountain).</summary>
public enum WallFootDirection { None, Valley, Mountain }
/// <summary>Wall dimensions in metres. Reinforcement/detail lengths below are in mm unless stated otherwise.</summary>
public sealed record WallDetailGeometry(double Height, double Slab, double Toe, double StemBase, double StemTop, double Heel);
public sealed record WallFaceBars(double Diameter, int Count);
public sealed record WallZoneBars(WallFaceBars FirstFace, WallFaceBars OppositeFace, double SecondaryDiameter,
    double SecondarySpacing, double AnchorLength = 0, double LapLength = 0, double BendDiameter = 0);
public sealed class WallRebarLayout
{
    public bool TwoZones { get; }
    /// <summary>Metres from the top of the slab.</summary>
    public double LowerHeight { get; }
    public IReadOnlyDictionary<WallRebarZone, WallZoneBars> Zones { get; }
    public WallRebarLayout(bool twoZones, double lowerHeight, IReadOnlyDictionary<WallRebarZone, WallZoneBars> zones)
    {
        Guard.NotNull(zones);
        TwoZones = twoZones; LowerHeight = lowerHeight;
        Zones = new System.Collections.ObjectModel.ReadOnlyDictionary<WallRebarZone, WallZoneBars>(zones.ToDictionary(pair => pair.Key, pair => pair.Value));
    }
}
/// <summary>Positive design strengths in MPa, obtained from the selected material/standard services.</summary>
public sealed record WallDetailMaterial(double Fcd, double Fyd, double Fctk05, double GammaC);
public sealed record WallDetailOptions(double Aggregate, double LapClearDistance, double LapPercent, bool GoodBond,
    double TieDiameter, double TieSpacing, DurabilityProfile CoverProfile, CoverInput CoverSpecification)
{
    /// <summary>Stock bar (m): the toe and heel bars of a layer of the foundation are one continuous bar only if it fits.</summary>
    public double StockLength { get; init; } = 12;
}
public sealed record WallDetailInput(WallDetailGeometry Geometry, WallRebarLayout Reinforcement, WallDetailMaterial Material,
    double Cover, WallDetailOptions Options);
/// <summary>Coordinates in metres, at the centre line of the bar.</summary>
public sealed record WallBarPoint(double X, double Y);
public enum WallBarLegKind { Straight, Arc }
/// <summary>A leg of the centre line of a bar, in m. Straight legs carry their end points (section coordinates) and the angle in degrees
/// of a sharp bend at their start (0 if none); arcs carry the bend angle in degrees.</summary>
public sealed record WallBarLeg(string Name, double Length)
{
    public WallBarLegKind Kind { get; init; }
    public double Angle { get; init; }
    public WallBarPoint? From { get; init; }
    public WallBarPoint? To { get; init; }
}
/// <summary>Main bar of a zone and face (stem verticals, foundation transverse bars). Mark is the position number of the drawing.
/// A continuous foundation bar serves the toe and the heel (Zone is the toe).</summary>
public sealed record WallBarDetail(string Mark, WallRebarZone Zone, WallRebarFace Face, double Diameter, int Count,
    double Spacing, double Area, double Length, double RequiredAnchor, double Anchor,
    double RequiredLap, double Lap, double Mandrel, double Fbd, IReadOnlyList<WallBarPoint> Path)
{
    public IReadOnlyList<WallBarLeg> Legs { get; init; } = [];
    public bool Continuous { get; init; }
    public WallFootDirection Foot { get; init; }
    /// <summary>The foot does not fit on the preferred side (stem bars cross in the foundation) and turns the other way.</summary>
    public bool FootReversed { get; init; }
}
/// <summary>U-bar closing the free head of the stem (EN 1992-1-1 9.3.1.4, fig. 9.8): Count bars per metre, legs from the top in m.</summary>
public sealed record WallHeadDetail(string Mark, WallRebarZone Zone, double Diameter, int Count, double Spacing, double LegLength,
    double Mandrel, double Length, IReadOnlyList<WallBarPoint> Path, IReadOnlyList<WallBarLeg> Legs);
/// <summary>Cross ties between the faces in the lap of the stem bars: straight through the thickness at the junction, 135° hooks
/// extended by <see cref="Hook"/> mm. Levels are the heights of the rows (m).</summary>
public sealed record WallTieDetail(string Mark, double Diameter, double Spacing, int Rows, double Mandrel, double Hook, double Length,
    IReadOnlyList<WallBarPoint> Path, IReadOnlyList<WallBarLeg> Legs, IReadOnlyList<double> Levels);
/// <summary>Longitudinal (distribution) bars of a layer, inside the main bars: their positions in the section (m), the nominal
/// spacing and the lap l0 (mm) when they are spliced along the wall.</summary>
public sealed record WallLongitudinalDetail(string Mark, WallRebarZone Zone, WallRebarFace Face, double Diameter, double Spacing,
    double RequiredLap, double Lap, IReadOnlyList<WallBarPoint> Points);
/// <summary>Absolute demand and historical signed capacity (e.g. impossible free space). A nonpositive
/// capacity with positive demand is never satisfied; negative capacity retains unavailable legacy ratio.</summary>
public sealed record WallDetailCheck(WallDetailRule Rule, WallRebarZone? Zone, WallRebarFace? Face, double Demand, double Capacity, string Unit)
{
    public double? Ratio => Capacity > 0 ? Demand / Capacity : Capacity == 0 && Demand == 0 ? 0 : null;
    public EngineeringOutcome Outcome => Capacity < 0 ? EngineeringOutcome.NotEvaluated : Demand <= Capacity ? EngineeringOutcome.Satisfied : EngineeringOutcome.NotSatisfied;
    public ExecutionStatus Execution => ExecutionStatus.Completed;
    public DataStatus Data => DataStatus.Ready;
}
/// <summary>Bars of the section in drawing order (marks 1, 2, …): main bars, head U-bars, ties, longitudinal bars. SteelKg per metre of wall.</summary>
public sealed record WallReinforcementDetails(IReadOnlyList<WallBarDetail> Bars, IReadOnlyList<WallDetailCheck> Checks, double SteelKg)
{
    public WallHeadDetail? Head { get; init; }
    public WallTieDetail? Tie { get; init; }
    public IReadOnlyList<WallLongitudinalDetail> Longitudinals { get; init; } = [];
}
