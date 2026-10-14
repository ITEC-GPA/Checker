namespace GPC.Design.Bridges;

/// <summary>Immutable, explicit SI inputs. Zero dimensions request the documented automatic rule; no archive defaults are inferred.</summary>
public sealed record BridgeInput
{
    /// <summary>Defaults only for source compatibility; archive adapters must explicitly identify old documents.</summary>
    public TrafficModel TrafficModel { get; init; } = TrafficModel.LegacyUniform;
    public required double Length { get; init; }
    public required double Height { get; init; }
    public required double ObstacleWidth { get; init; }
    public required double Lanes { get; init; }
    public required double LaneWidth { get; init; }
    public required double Shoulder { get; init; }
    public required double Median { get; init; }
    public required double BarrierWidth { get; init; }
    public required double Spans { get; init; }
    public required double Depth { get; init; }
    public required double Slab { get; init; }
    public required double Spacing { get; init; }
    public required double Web { get; init; }
    public required double Bottom { get; init; }
    public required double BottomRatio { get; init; }
    public required double Cells { get; init; }
    public required double Boxes { get; init; }
    public required double FlangeWidth { get; init; }
    public required double FlangeMm { get; init; }
    public required double WebMm { get; init; }
    public required double WebSlope { get; init; }
    public required double Haunch { get; init; }
    public required double UTop { get; init; }
    public required double UBottom { get; init; }
    public required double Fc { get; init; }
    public required double ArchRise { get; init; }
    public required double TowerHeight { get; init; }
    public required double CableSag { get; init; }
    public required double SuspenderSpacing { get; init; }
    public required double TrussRatio { get; init; }
    public required double EmbeddedCover { get; init; }
    public required double DeckPlateMm { get; init; }
    public required double RibHeight { get; init; }
    public required double RibTop { get; init; }
    public required double RibBottom { get; init; }
    public required double RibMm { get; init; }
    public required double RibSpacing { get; init; }
    public required double PileLength { get; init; }
    public required double PierSize { get; init; }
    public required double PileCount { get; init; }
    public required double FootingSize { get; init; }
    public required double FcSub { get; init; }
    public required string DeckType { get; init; }
    public required string Family { get; init; }
    public required string Obstacle { get; init; }
    public required string Soil { get; init; }
    public required string Pier { get; init; }
    public required string Foundation { get; init; }
    public required bool Continuous { get; init; }
    public required bool StartPier { get; init; }
    public required bool EndPier { get; init; }
    public required bool LowCarbon { get; init; }
    public required bool Recycled { get; init; }
    internal double Value(string key) => key switch
    {
        "length" => Length,
        "height" => Height,
        "obstacle_width" => ObstacleWidth,
        "lanes" => Lanes,
        "lane_width" => LaneWidth,
        "shoulder" => Shoulder,
        "median" => Median,
        "barrier_width" => BarrierWidth,
        "spans" => Spans,
        "depth" => Depth,
        "slab" => Slab,
        "spacing" => Spacing,
        "web" => Web,
        "bottom" => Bottom,
        "bottom_ratio" => BottomRatio,
        "cells" => Cells,
        "boxes" => Boxes,
        "flange_width" => FlangeWidth,
        "flange_mm" => FlangeMm,
        "web_mm" => WebMm,
        "web_slope" => WebSlope,
        "haunch" => Haunch,
        "u_top" => UTop,
        "u_bottom" => UBottom,
        "fc" => Fc,
        "arch_rise" => ArchRise,
        "tower_height" => TowerHeight,
        "cable_sag" => CableSag,
        "suspender_spacing" => SuspenderSpacing,
        "truss_ratio" => TrussRatio,
        "embedded_cover" => EmbeddedCover,
        "deck_plate_mm" => DeckPlateMm,
        "rib_height" => RibHeight,
        "rib_top" => RibTop,
        "rib_bottom" => RibBottom,
        "rib_mm" => RibMm,
        "rib_spacing" => RibSpacing,
        "pile_length" => PileLength,
        "pier_size" => PierSize,
        "pile_count" => PileCount,
        "footing_size" => FootingSize,
        "fc_sub" => FcSub,
        _ => throw new ArgumentException("Unknown numeric field: " + key)
    };
}

public sealed record BridgeAssumptions
{
    public required double Prelims { get; init; }
    public required double Contingency { get; init; }
    public required double Uncertainty { get; init; }
    public required double Co2Concrete { get; init; }
    public required double Co2Rebar { get; init; }
    public required double Co2Steel { get; init; }
    public required double Co2Pt { get; init; }
    public required double Co2Site { get; init; }
    public required double LowCarbonFactor { get; init; }
    public required double RecycledFactor { get; init; }
    public required double RebarRc { get; init; }
    public required double RebarPsc { get; init; }
    public required double RebarSub { get; init; }
    public required double RebarFound { get; init; }
    public required double PtDensity { get; init; }
    public required double WearLoad { get; init; }
    public required double TrafficLoad { get; init; }
    public required double GammaG { get; init; }
    public required double GammaQ { get; init; }
    public required double SoilPressure { get; init; }
    public required double PileSkin { get; init; }
    public required double PileBase { get; init; }
    public required double MonthsBase { get; init; }
    public required double MonthsSpan { get; init; }
    public required double MonthsPier { get; init; }
    public required double ConceptTension { get; init; }
    public required double ConceptCompression { get; init; }
    public required double ConceptCable { get; init; }
    public required double ConceptTower { get; init; }
    public required double AnchorFriction { get; init; }
    public required double SpecialConnections { get; init; }
    public required double Co2Cable { get; init; }
    internal double Value(string key) => key switch
    {
        "prelims" => Prelims,
        "contingency" => Contingency,
        "uncertainty" => Uncertainty,
        "co2_concrete" => Co2Concrete,
        "co2_rebar" => Co2Rebar,
        "co2_steel" => Co2Steel,
        "co2_pt" => Co2Pt,
        "co2_site" => Co2Site,
        "low_carbon_factor" => LowCarbonFactor,
        "recycled_factor" => RecycledFactor,
        "rebar_rc" => RebarRc,
        "rebar_psc" => RebarPsc,
        "rebar_sub" => RebarSub,
        "rebar_found" => RebarFound,
        "pt_density" => PtDensity,
        "wear_load" => WearLoad,
        "traffic_load" => TrafficLoad,
        "gamma_g" => GammaG,
        "gamma_q" => GammaQ,
        "soil_pressure" => SoilPressure,
        "pile_skin" => PileSkin,
        "pile_base" => PileBase,
        "months_base" => MonthsBase,
        "months_span" => MonthsSpan,
        "months_pier" => MonthsPier,
        "concept_tension" => ConceptTension,
        "concept_compression" => ConceptCompression,
        "concept_cable" => ConceptCable,
        "concept_tower" => ConceptTower,
        "anchor_friction" => AnchorFriction,
        "special_connections" => SpecialConnections,
        "co2_cable" => Co2Cable,
        _ => throw new ArgumentException("Unknown numeric field: " + key)
    };
}

public sealed record BridgeRates
{
    public required double ConcreteDeck { get; init; }
    public required double ConcreteSub { get; init; }
    public required double Rebar { get; init; }
    public required double Prestress { get; init; }
    public required double Steel { get; init; }
    public required double SteelBox { get; init; }
    public required double Formwork { get; init; }
    public required double Pile1 { get; init; }
    public required double Pile15 { get; init; }
    public required double Bearing { get; init; }
    public required double Joint { get; init; }
    public required double Barrier { get; init; }
    public required double Surfacing { get; init; }
    public required double SteelOrtho { get; init; }
    public required double Cables { get; init; }
    public required double ErectionSpecial { get; init; }
    internal double Value(string key) => key switch
    {
        "concrete_deck" => ConcreteDeck,
        "concrete_sub" => ConcreteSub,
        "rebar" => Rebar,
        "prestress" => Prestress,
        "steel" => Steel,
        "steel_box" => SteelBox,
        "formwork" => Formwork,
        "pile_1" => Pile1,
        "pile_15" => Pile15,
        "bearing" => Bearing,
        "joint" => Joint,
        "barrier" => Barrier,
        "surfacing" => Surfacing,
        "steel_ortho" => SteelOrtho,
        "cables" => Cables,
        "erection_special" => ErectionSpecial,
        _ => throw new ArgumentException("Unknown numeric field: " + key)
    };
}

public sealed record BridgeModel(BridgeInput Input, BridgeRates Rates, BridgeAssumptions Assumptions)
{
    /// <summary>Null preserves the rates and arithmetic of a legacy calculation. Otherwise this owned snapshot governs every candidate.</summary>
    public GPC.Design.Costs.ResolvedPriceSnapshot? Prices { get; init; }
}
