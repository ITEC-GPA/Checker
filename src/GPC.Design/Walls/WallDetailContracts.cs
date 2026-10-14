using GPC.Checkers.Concrete.Durability;
using GPC.Model.Checking.Contracts;
using GPC.Model.Core.Diagnostics;

namespace GPC.Design.Walls;

public enum WallRebarZone { Stem, StemUpper, Toe, Heel }
public enum WallRebarFace { Upstream, Downstream, Bottom, Top }
public enum WallDetailRule { ClearSpacing, MainSpacing, Cover, CommonLap, LapPairClearSpacing, LapClearDistance,
    LapPercentAtBoundary, LapExtent, BarCountContinuity, SecondaryArea, SecondarySpacing, Mandrel, BendHeight,
    TailExtent, FootAnchorage, RootAnchorage, AnchorageExtent, JunctionTieSpacing, JunctionTieArea }
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
        ArgumentNullException.ThrowIfNull(zones);
        TwoZones = twoZones; LowerHeight = lowerHeight;
        Zones = new System.Collections.ObjectModel.ReadOnlyDictionary<WallRebarZone, WallZoneBars>(new Dictionary<WallRebarZone, WallZoneBars>(zones));
    }
}
/// <summary>Positive design strengths in MPa, obtained from the selected material/standard services.</summary>
public sealed record WallDetailMaterial(double Fcd, double Fyd, double Fctk05, double GammaC);
public sealed record WallDetailOptions(double Aggregate, double LapClearDistance, double LapPercent, bool GoodBond,
    double TieDiameter, double TieSpacing, DurabilityProfile CoverProfile, CoverInput CoverSpecification);
public sealed record WallDetailInput(WallDetailGeometry Geometry, WallRebarLayout Reinforcement, WallDetailMaterial Material,
    double Cover, WallDetailOptions Options);
/// <summary>Coordinates in metres, at the centre line of the bar.</summary>
public sealed record WallBarPoint(double X, double Y);
public sealed record WallBarDetail(string Mark, WallRebarZone Zone, WallRebarFace Face, double Diameter, int Count,
    double Spacing, double Area, double Length, double RequiredAnchor, double Anchor,
    double RequiredLap, double Lap, double Mandrel, double Fbd, IReadOnlyList<WallBarPoint> Path);
/// <summary>Absolute demand and historical signed capacity (e.g. impossible free space). A nonpositive
/// capacity with positive demand is never satisfied; negative capacity retains unavailable legacy ratio.</summary>
public sealed record WallDetailCheck(WallDetailRule Rule, WallRebarZone? Zone, WallRebarFace? Face, double Demand, double Capacity, string Unit)
{
    public double? Ratio => Capacity > 0 ? Demand / Capacity : Capacity == 0 && Demand == 0 ? 0 : null;
    public EngineeringOutcome Outcome => Capacity < 0 ? EngineeringOutcome.NotEvaluated : Demand <= Capacity ? EngineeringOutcome.Satisfied : EngineeringOutcome.NotSatisfied;
    public ExecutionStatus Execution => ExecutionStatus.Completed;
    public DataStatus Data => DataStatus.Ready;
}
public sealed record WallReinforcementDetails(IReadOnlyList<WallBarDetail> Bars, IReadOnlyList<WallDetailCheck> Checks, double SteelKg);
