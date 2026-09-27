using GPC.Model.Materials;
namespace GPC.Checkers.CompositeBridge;

public enum BridgePhaseKind { SteelOnly, Composite, ConcreteExcluded, Shrinkage }
public enum BridgeLoadReference { GrossCentroid, EffectiveCentroid, CommonElevation }
public enum BridgeHomogenizationSource { Phi, ModularRatio }
public enum BridgeStandard { Ntc2018, Eurocode4 }
public enum BridgeLimitState { Ultimate, Rare, QuasiPermanent }
public enum BridgeStiffenerLayout { Symmetric, LeftOnly, RightOnly, Unequal }
public enum BridgeSupportLocation { Internal, LeftEnd, RightEnd }
/// <summary>The steel section of the girder: H with vertical web, H with inclined web, box girder open at the top (two webs, two top flanges)</summary>
public enum BridgeSteelSectionType { H, InclinedWebH, Box }
public enum BridgeStiffenerRole { Intermediate, Support }

/// <summary>Materials owned by Model. No catalog lookup, JSON, WPF or ANTHEA dependency.</summary>
public sealed record BridgeMaterialSet(ConcreteMaterialEN1992 Concrete, SteelMaterial Steel, SteelMaterial Rebar);
/// <summary>mm, MPa; mechanical increments in kN/kNm, shrinkage in microstrain. Model materials are shared and must not be mutated during analysis.</summary>
public sealed record HBridgeInput
{
    public BridgeMaterialSet Materials { get; init; } = null!;
    public BridgePhase[] Phases { get; init; } = Array.Empty<BridgePhase>();
    public HSectionDimensions Geometry { get; init; } = new();
    public BridgeRebarRow TopRebars { get; init; } = new();
    public BridgeRebarRow BottomRebars { get; init; } = new();
    public BridgeAnalysisOptions Options { get; init; } = new();
    public BridgeIntermediateStiffener Intermediate { get; init; } = new();
    public BridgeSupportStiffener Support { get; init; } = new();
    public BridgeStudOptions Studs { get; init; } = new();
    public BridgeTransverseReinforcement Transverse { get; init; } = new();
    public BridgeFatigueOptions Fatigue { get; init; } = new();
    public BridgeStiffener Stiffener(BridgeStiffenerRole role) => role == BridgeStiffenerRole.Intermediate ? Intermediate : Support;
    public HBridgeInput Snapshot() => this with { Phases = Phases.Select(p => p with { }).ToArray() };
}

public sealed record BridgePhase
{
    public string Name { get; init; } = "Phase";
    public bool Active { get; init; } = true;
    public BridgePhaseKind Kind { get; init; } = BridgePhaseKind.Composite;
    public BridgeLoadReference Reference { get; init; } = BridgeLoadReference.GrossCentroid;
    public BridgeHomogenizationSource HomogenizationSource { get; init; }
    public double ForceKN { get; init; }
    public double MomentKNm { get; init; }
    public double ShearKN { get; init; }
    public double AdditionalConnectionFlow { get; init; }
    public double ShrinkageMicrostrain { get; init; }
    public double Phi { get; init; }
    public double PsiL { get; init; } = 1;
    public double N { get; init; } = double.NaN;
    public string KindName => (int)Kind >= 0 && (int)Kind < HBridgeSection.PhaseKinds.Length ? HBridgeSection.PhaseKinds[(int)Kind] : "Invalid";
    public string ReferenceName => (int)Reference >= 0 && (int)Reference < HBridgeSection.LoadReferences.Length ? HBridgeSection.LoadReferences[(int)Reference] : "Invalid";
    public string HomogenizationName => HomogenizationSource == BridgeHomogenizationSource.Phi ? "Da φ" : HomogenizationSource == BridgeHomogenizationSource.ModularRatio ? "Da n" : "Invalid";
}

public record BridgeStiffener
{
    public BridgeStiffenerLayout Layout { get; init; }
    public string LayoutName => (int)Layout >= 0 && (int)Layout < HBridgeSection.StiffenerSides.Length ? HBridgeSection.StiffenerSides[(int)Layout] : "Invalid";
    public double Width { get; init; }
    public double Thickness { get; init; }
    public double RightWidth { get; init; }
    public double RightThickness { get; init; }
    public double LengthFactor { get; init; } = 1;
    public bool CheckWelds { get; init; }
    public double WeldThroat { get; init; }
}
public sealed record HSectionDimensions
{
    public double SlabWidth { get; init; }
    public double SlabHeight { get; init; }
    public double WebHeight { get; init; }
    public double WebThickness { get; init; }
    public double TopWidth { get; init; }
    public double TopThickness { get; init; }
    public double BottomWidth { get; init; }
    public double BottomThickness { get; init; }
    public double SecondBottomWidth { get; init; }
    public double SecondBottomThickness { get; init; }
    public bool SecondBottomEnabled { get; init; }
    /// <summary>The type of the steel section. With <see cref="BridgeSteelSectionType.Box"/> TopWidth is the width of each of the two top flanges
    /// and BottomWidth the width of the whole bottom flange; the second bottom plate is only for the H</summary>
    public BridgeSteelSectionType SectionType { get; init; }
    /// <summary>Inclined web H: horizontal offset of the end of the web at the bottom flange respect to the end at the top flange (positive to the
    /// right). Box: inward offset of each web at the bottom flange (the spacing of the webs at the bottom is WebSpacing - 2 WebOffset; negative:
    /// the box is wider at the bottom). The webs are plates of thickness WebThickness normal to their plane; WebHeight is the vertical height</summary>
    public double WebOffset { get; init; }
    /// <summary>Box: distance between the axes of the two webs at the top flanges</summary>
    public double WebSpacing { get; init; }
}
public sealed record BridgeRebarRow
{
    public bool Enabled { get; init; }
    public double Diameter { get; init; }
    public double Pitch { get; init; }
    public double AxisDistance { get; init; }
}
public sealed record BridgeAnalysisOptions
{
    public double GammaM0 { get; init; } = 1.05;
    public double GammaC { get; init; } = 1.5;
    public double GammaS { get; init; } = 1.15;
    public double AlphaCC { get; init; } = .85;
    public bool Class4 { get; init; } = true;
    /// <summary>Local buckling (EN 1993-1-5 4.4) of the top flange outstands; false keeps them fully effective. Used only with Class4.</summary>
    public bool TopFlangeBuckling { get; init; } = true;
    /// <summary>Local buckling of the bottom flange outstands (both plates); false keeps them fully effective. Used only with Class4.</summary>
    public bool BottomFlangeBuckling { get; init; } = true;
    /// <summary>Local buckling of the web as an internal plate; false keeps it fully effective. Used only with Class4.</summary>
    public bool WebBuckling { get; init; } = true;
    /// <summary>Effective-width iteration: each situation starts from the converged geometry of the previous one and the relaxation factor
    /// follows the Aitken formula (true, default); false: every situation from the gross geometry with the fixed relaxation 0.55.
    /// The two converge to the same geometry within the tolerance.</summary>
    public bool AcceleratedIteration { get; init; } = true;
    public double CommonLoadY { get; init; }
    public double GammaM1 { get; init; } = 1.1;
    public double GammaM2 { get; init; } = 1.25;
    public double ShearEta { get; init; } = 1.2;
    public BridgeStandard Standard { get; init; }
    public BridgeLimitState LimitState { get; init; }
    public string StandardName => Standard == BridgeStandard.Ntc2018 ? HBridgeSection.Standards[0] : Standard == BridgeStandard.Eurocode4 ? HBridgeSection.Standards[1] : "Invalid";
    public string LimitStateName => LimitState == BridgeLimitState.Ultimate ? "SLU" : LimitState == BridgeLimitState.Rare ? "SLE rara" : LimitState == BridgeLimitState.QuasiPermanent ? "SLE quasi permanente" : "Invalid";
}
public sealed record BridgeIntermediateStiffener : BridgeStiffener
{
    public bool Enabled { get; init; }
    public double LeftPanel { get; init; }
    public double RightPanel { get; init; }
    public bool EqualPanels { get; init; }
    public double ExternalCompressionKN { get; init; }
    public double LoadX { get; init; }
}
public sealed record BridgeSupportStiffener : BridgeStiffener
{
    public bool Enabled { get; init; }
    public double LeftPanel { get; init; }
    public double RightPanel { get; init; }
    public double EndDistance { get; init; }
    public double ReactionKN { get; init; }
    public double LoadX { get; init; }
    public double LoadZ { get; init; }
    public double FootprintLength { get; init; }
    public double FootprintWidth { get; init; }
    public bool RigidEndPost { get; init; }
    public double EndPostSpacing { get; init; }
    public BridgeSupportLocation Location { get; init; }
    public string LocationName => (int)Location >= 0 && (int)Location < HBridgeSection.SupportLocations.Length ? HBridgeSection.SupportLocations[(int)Location] : "Invalid";
}
public sealed record BridgeStudOptions
{
    public bool Enabled { get; init; }
    public double CountPerRow { get; init; }
    public double Diameter { get; init; }
    public double Height { get; init; }
    public double LongitudinalPitch { get; init; }
    public double TransversePitch { get; init; }
    public double Fu { get; init; }
    public double GammaV { get; init; }
    public double HeadDiameter { get; init; }
    public double HeadThickness { get; init; }
    public bool RepeatedActionDetail { get; init; }
    public double RequiredCover { get; init; }
}
public sealed record BridgeTransverseReinforcement
{
    public bool Enabled { get; init; }
    public double TopDiameter { get; init; }
    public double TopPitch { get; init; }
    public double BottomDiameter { get; init; }
    public double BottomPitch { get; init; }
    public double CotTheta { get; init; }
    public double LeftFlowFraction { get; init; }
    public double BendingSteelPerMetre { get; init; }
    public double AnchorageLength { get; init; }
    public bool GoodBond { get; init; }
    public double LeftConcreteEdge { get; init; }
    public double RightConcreteEdge { get; init; }
    public bool EdgeUBar { get; init; }
    public double UBarDiameter { get; init; }
}
public sealed record BridgeFatigueOptions
{
    public bool Enabled { get; init; }
    public double MinimumFlow { get; init; }
    public double MaximumFlow { get; init; }
    public double EquivalenceFactor { get; init; }
    public double DynamicFactor { get; init; }
    public bool TensileFlange { get; init; }
    public double FlangeStressRange { get; init; }
    public double GammaFf { get; init; }
    public double GammaMfStud { get; init; }
    public double GammaMfFlange { get; init; }
}
