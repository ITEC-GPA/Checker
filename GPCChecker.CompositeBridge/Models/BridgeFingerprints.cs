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
        v.Record(typeof(BridgeWorkflowDefinition), "GPC.Checkers.CompositeBridge.BridgeWorkflowDefinition", Properties("ModelGuid", "AnalysisFingerprint", "Method", "Phases").Concat(new FingerprintMember[]{new FingerprintMember("schema")}).ToArray());
        v.Record(typeof(BridgeWorkflowPhaseDefinition), "GPC.Checkers.CompositeBridge.BridgeWorkflowPhaseDefinition", Properties("Id", "Order", "Name", "State", "Day", "Actions"));
        v.Record(typeof(BridgeWorkflowActionDefinition), "GPC.Checkers.CompositeBridge.BridgeWorkflowActionDefinition", Properties("Key", "SourceModelGuid", "SourceAnalysisFingerprint", "SourceCaseGuid", "SourceName", "Behavior", "ForceCaseGuid", "StrainCaseGuid", "Strain", "BeamGuids", "PrimaryOnly", "ConcreteExcludedBeamGuids", "SectionStates")
            .Concat(new[]{FingerprintMember.AutoProperty("PsiL", omitWhenNull: true)}).ToArray());
        v.Record(typeof(BridgeBeamSectionState), "GPC.Checkers.CompositeBridge.BridgeBeamSectionState", Properties("BeamGuid", "Behavior", "ConcreteExcluded", "ModularRatio", "ClassificationCaseGuid", "PhysicalPropertyFingerprint"));
        v.Record(typeof(BridgeCompositeSectionDefinition), "GPC.Checkers.CompositeBridge.BridgeCompositeSectionDefinition", Properties("Section", "NLongTerm", "NShortTerm",
            "NShrinkage", "VerticalOffsetMm", "ConsiderConcrete", "ConsiderRebar", "Name").Concat(new FingerprintMember[]{new FingerprintMember("schema"),
            // P2H (5/11): the rows of bars as given, omitted when absent (the fingerprint of a definition without them does not change).
            FingerprintMember.AutoProperty("RebarRows", omitWhenNull: true),
            // P2H (5/11): the steel of the analysis properties, omitted when absent.
            FingerprintMember.AutoProperty("AnalysisSteel", omitWhenNull: true)}).ToArray());
        v.Record(typeof(BridgeCompositeRebarRows), "GPC.Checkers.CompositeBridge.BridgeCompositeRebarRows",
            Properties("TopAreaMm2", "TopCoverMm", "BottomAreaMm2", "BottomCoverMm", "BarsPerRow"));
        // Lane L12: the bridge data of a model (BridgeModel) and the bridge vocabulary that left GPCModel. New contracts: their names are
        // those of the CLR types in CompositeBridge (nothing was released or stored with the names they had in GPCModel).
        v.Record(typeof(BridgeModelData), "GPC.Checkers.CompositeBridge.BridgeModelData",
            new FingerprintMember("schema"), new FingerprintMember("beams"), FingerprintMember.AutoProperty("Workflow", omitWhenNull: true));
        v.Record(typeof(BridgeBeamData), "GPC.Checkers.CompositeBridge.BridgeBeamData", Properties("BeamGuid", "Member", "SuperElement", "SectionState", "CompositeSection"));
        v.Record(typeof(BridgeLocalSectionState), "GPC.Checkers.CompositeBridge.BridgeLocalSectionState",
            Properties("Behavior", "ConcreteExcluded", "ModularRatio", "ClassificationCase", "PropertyFingerprint"));
        v.Record(typeof(BridgeBeamCompositeSection), "GPC.Checkers.CompositeBridge.BridgeBeamCompositeSection", Properties("Start", "End"));
        v.Record(typeof(CompositeSectionDefinition), "GPC.Checkers.CompositeBridge.CompositeSectionDefinition", new FingerprintMember[]{new FingerprintMember("schema")}
            .Concat(Properties("Id", "PhysicalSection", "LongTermRatio", "ShortTermRatio", "ShrinkageRatio", "IncludeConcrete", "IncludeReinforcement")).ToArray());
        v.Record(typeof(Layout.BridgeMemberMetadata), "GPC.Checkers.CompositeBridge.Layout.BridgeMemberMetadata",
            Properties("PhysicalId", "Family", "Group", "Segment", "Span", "StationFromMm", "StationToMm").Concat(new FingerprintMember[]{new FingerprintMember("sectionUp")})
            .Concat(Properties("GeometryId")).ToArray());
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
