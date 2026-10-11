using GPC.Model.Core;

[assembly: FingerprintVocabulary(typeof(GPC.Checkers.CompositeBridge.BridgeFingerprints))]

namespace GPC.Checkers.CompositeBridge;

/// <summary>
/// Fingerprint contracts of the bridge inputs (architecture review R8). The fingerprints of the configurations and of the stored reports
/// depend on these names: they are the names that the reflection of the fields used before the contracts, in the same order (the fields
/// of the record in ordinal order, then those of its base). When a property is renamed keep its wire name; a new property must be declared
/// here (with omitWhenNull, if it is a reference added after reports were stored), otherwise the first fingerprint fails.
/// </summary>
internal sealed class BridgeFingerprints : IFingerprintVocabulary
{
    public void Declare(FingerprintVocabulary v)
    {
        v.Record(typeof(HBridgeInput), "GPC.Checkers.CompositeBridge.HBridgeInput", Properties("BottomRebars", "Box", "Fatigue", "Geometry",
            "Intermediate", "Materials", "Options", "Phases", "Studs", "Support", "TopRebars", "Transverse"));
        v.Record(typeof(BridgeMaterialSet), "GPC.Checkers.CompositeBridge.BridgeMaterialSet", Properties("Concrete", "Rebar", "Steel"));
        v.Record(typeof(BridgePhase), "GPC.Checkers.CompositeBridge.BridgePhase", Properties("Active", "AdditionalConnectionFlow", "ForceKN",
            "HomogenizationSource", "Kind", "MomentKNm", "N", "Name", "Phi", "PsiL", "Reference", "ShearKN", "ShrinkageMicrostrain", "TorsionKNm"));
        v.Record(typeof(HSectionDimensions), "GPC.Checkers.CompositeBridge.HSectionDimensions", Properties("BottomThickness", "BottomWidth",
            "SecondBottomEnabled", "SecondBottomThickness", "SecondBottomWidth", "SectionType", "SlabHeight", "SlabWidth", "TopThickness", "TopWidth",
            "WebHeight", "WebOffset", "WebSpacing", "WebThickness"));
        v.Record(typeof(BridgeRebarRow), "GPC.Checkers.CompositeBridge.BridgeRebarRow", Properties("AxisDistance", "Diameter", "Enabled", "Pitch"));
        v.Record(typeof(BridgeAnalysisOptions), "GPC.Checkers.CompositeBridge.BridgeAnalysisOptions", Properties("AcceleratedIteration", "AlphaCC",
            "BottomFlangeBuckling", "Class4", "CommonLoadY", "GammaC", "GammaM0", "GammaM1", "GammaM2", "GammaS", "LimitState", "ShearEta", "Standard",
            "TopFlangeBuckling", "WebBuckling"));
        v.Record(typeof(BridgeStiffener), "GPC.Checkers.CompositeBridge.BridgeStiffener", Stiffener());
        v.Record(typeof(BridgeIntermediateStiffener), "GPC.Checkers.CompositeBridge.BridgeIntermediateStiffener", Properties("Enabled", "EqualPanels",
            "ExternalCompressionKN", "LeftPanel", "LoadX", "RightPanel").Concat(Stiffener()).ToArray());
        v.Record(typeof(BridgeSupportStiffener), "GPC.Checkers.CompositeBridge.BridgeSupportStiffener", Properties("Enabled", "EndDistance",
            "EndPostSpacing", "FootprintLength", "FootprintWidth", "LeftPanel", "LoadX", "LoadZ", "Location", "ReactionKN", "RightPanel",
            "RigidEndPost").Concat(Stiffener()).ToArray());
        v.Record(typeof(BridgeStudOptions), "GPC.Checkers.CompositeBridge.BridgeStudOptions", Properties("CountPerRow", "Diameter", "Enabled", "Fu",
            "GammaV", "HeadDiameter", "HeadThickness", "Height", "LongitudinalPitch", "RepeatedActionDetail", "RequiredCover", "TransversePitch"));
        v.Record(typeof(BridgeTransverseReinforcement), "GPC.Checkers.CompositeBridge.BridgeTransverseReinforcement", Properties("AnchorageLength",
            "BendingSteelPerMetre", "BottomDiameter", "BottomPitch", "CotTheta", "EdgeUBar", "Enabled", "GoodBond", "LeftConcreteEdge",
            "LeftFlowFraction", "RightConcreteEdge", "TopDiameter", "TopPitch", "UBarDiameter"));
        v.Record(typeof(BridgeBoxOptions), "GPC.Checkers.CompositeBridge.BridgeBoxOptions", Properties("BearingSpacing", "BracingArea",
            "BracingBucklingFactor", "BracingRadius", "BracingThickness", "ConcentratedTorque", "DiaphragmKind", "DiaphragmSpacing", "DiaphragmThickness",
            "DistributedTorque", "Enabled", "SpanLength", "SupportDiaphragmThickness", "SupportTorqueKNm"));
        v.Record(typeof(BridgeFatigueOptions), "GPC.Checkers.CompositeBridge.BridgeFatigueOptions", Properties("DynamicFactor", "Enabled",
            "EquivalenceFactor", "FlangeStressRange", "GammaFf", "GammaMfFlange", "GammaMfStud", "MaximumFlow", "MinimumFlow", "TensileFlange"));
    }

    /// <summary>The members of the base stiffener, after those of the derived record.</summary>
    private static FingerprintMember[] Stiffener() => Properties("CheckWelds", "Layout", "LengthFactor", "RightThickness", "RightWidth", "Thickness",
        "WeldThroat", "Width");
    private static FingerprintMember[] Properties(params string[] names) => names.Select(n => FingerprintMember.AutoProperty(n)).ToArray();
}
