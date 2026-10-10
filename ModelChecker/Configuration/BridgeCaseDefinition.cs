using System;
using System.Runtime.Serialization;
using GPC.Checkers.CompositeBridge;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Results.Queries;

namespace GPC.Model.Checker.Configuration
{
    [DataContract(Name = "BridgeCase", Namespace = ConfigurationArchive.Namespace)]
    public sealed class BridgeCaseDefinition
    {
        [DataMember(Order = 1)] public BeamCut Cut { get; set; }
        [DataMember(Order = 2)] public ResultSelection State { get; set; }
        [DataMember(Order = 3)] public CoordinateSystem SectionAxes { get; set; }
        [DataMember(Order = 4)] public double? ActionReferenceY { get; set; }
        [DataMember(Order = 5)] public bool HistoryAndReferenceConfirmed { get; set; }
        [DataMember(Order = 6)] public string Source { get; set; }
        [DataMember(Order = 7)] public BridgeInputDefinition Input { get; set; }
        public static BridgeCaseDefinition FromCase(CompositeBridgeCase value) => new BridgeCaseDefinition {
            Cut = new BeamCut { ElementId = value.BeamId, Parameter = value.Station, Side = value.Side,
                Domain = (BeamStationDomain)Enum.Parse(typeof(BeamStationDomain), value.StationDomain) },
            State = value.State.Copy(), SectionAxes = value.SectionAxes, ActionReferenceY = value.ActionReferenceY,
            HistoryAndReferenceConfirmed = value.HistoryAndReferenceConfirmed, Source = value.Source, Input = BridgeInputDefinition.FromInput(value.Input) };
        internal CompositeBridgeCase ToCase()
        {
            var location = (Cut ?? throw new ArgumentException("MissingBridgeCut")).ToLocation();
            return new CompositeBridgeCase { BeamId = location.BeamId.Value, Station = location.Station.Value, StationDomain = location.StationDomain,
                Side = location.Side, State = State, SectionAxes = SectionAxes, ActionReferenceY = ActionReferenceY,
                HistoryAndReferenceConfirmed = HistoryAndReferenceConfirmed, Source = Source,
                Input = (Input ?? throw new ArgumentException("MissingBridgeInput")).ToInput() };
        }
    }
    /// <summary>Versioned bridge input data, with explicit material fields. Quantities retain the native contract's mm/MPa/kN/kNm units.</summary>
    [DataContract(Name = "BridgeInput", Namespace = ConfigurationArchive.Namespace)]
    public sealed class BridgeInputDefinition
    {
        [DataMember(Order = 1)] public ConcreteMaterialEN1992 Concrete { get; set; }
        [DataMember(Order = 2)] public SteelMaterial Steel { get; set; }
        [DataMember(Order = 3)] public SteelMaterial Rebar { get; set; }
        [DataMember(Order = 4)] public BridgePhase[] Phases { get; set; }
        [DataMember(Order = 5)] public HSectionDimensions Geometry { get; set; }
        [DataMember(Order = 6)] public BridgeRebarRow TopRebars { get; set; }
        [DataMember(Order = 7)] public BridgeRebarRow BottomRebars { get; set; }
        [DataMember(Order = 8)] public BridgeAnalysisOptions Options { get; set; }
        [DataMember(Order = 9)] public BridgeIntermediateStiffener Intermediate { get; set; }
        [DataMember(Order = 10)] public BridgeSupportStiffener Support { get; set; }
        [DataMember(Order = 11)] public BridgeStudOptions Studs { get; set; }
        [DataMember(Order = 12)] public BridgeTransverseReinforcement Transverse { get; set; }
        [DataMember(Order = 13)] public BridgeFatigueOptions Fatigue { get; set; }
        [DataMember(Order = 14)] public BridgeBoxOptions Box { get; set; }
        public static BridgeInputDefinition FromInput(HBridgeInput value) => new BridgeInputDefinition {
            Concrete = value.Materials.Concrete, Steel = value.Materials.Steel, Rebar = value.Materials.Rebar, Phases = value.Phases,
            Geometry = value.Geometry, TopRebars = value.TopRebars, BottomRebars = value.BottomRebars, Options = value.Options,
            Intermediate = value.Intermediate, Support = value.Support, Studs = value.Studs, Transverse = value.Transverse, Fatigue = value.Fatigue, Box = value.Box };
        internal HBridgeInput ToInput() => new HBridgeInput { Materials = new BridgeMaterialSet(Concrete, Steel, Rebar), Phases = Phases,
            Geometry = Geometry, TopRebars = TopRebars, BottomRebars = BottomRebars, Options = Options,
            Intermediate = Intermediate, Support = Support, Studs = Studs, Transverse = Transverse, Fatigue = Fatigue, Box = Box };
    }
}
