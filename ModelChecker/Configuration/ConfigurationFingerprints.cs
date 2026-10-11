using System.Linq;
using GPC.Model.Core;

[assembly: FingerprintVocabulary(typeof(GPC.Model.Checker.Configuration.ConfigurationFingerprints))]

namespace GPC.Model.Checker.Configuration
{
    /// <summary>
    /// Fingerprint contracts of the configuration and of the engine options (architecture review R8). The fingerprints of the configurations
    /// and of the stored reports depend on these names: they are the names that the reflection of the fields used before the contracts, in
    /// the same order. When a property is renamed keep its wire name; a new property must be declared here (with omitWhenNull, if it is a
    /// reference added after reports were stored), otherwise the first fingerprint fails.
    /// </summary>
    internal sealed class ConfigurationFingerprints : IFingerprintVocabulary
    {
        public void Declare(FingerprintVocabulary v)
        {
            v.Record(typeof(VerificationConfiguration), "GPC.Model.Checker.Configuration.VerificationConfiguration", Properties("Contexts", "Engines",
                "Jobs", "PlateJobs", "Schema"));
            v.Record(typeof(DesignContextDefinition), "GPC.Model.Checker.Configuration.DesignContextDefinition", Properties("BridgeCode", "Code",
                "Edition", "Id", "NationalAnnex", "Revision", "Source", "Units"));
            v.Record(typeof(EngineDefinition), "GPC.Model.Checker.Configuration.EngineDefinition", Properties("BridgeCases", "BridgeHistory", "Concrete",
                "ContextId", "ContextRevision", "Id", "Kind", "Parameters", "RequiredImplementationVersion", "Schema"));
            v.Record(typeof(ConcreteVerificationOptions), "GPC.Model.Checker.ConcreteVerificationOptions", Properties("AngularDivisions",
                "CalculationEngineConfiguration", "CalculationEngineId", "CalculationEngineVersion", "ConcreteStressLimitFactor", "ConsiderTensileConcrete",
                "CrackDesignLimit", "CrackLoadDuration", "Criterion", "NationalAnnex", "PsiRebar", "PsiTendon", "ServiceabilityAnalysis", "ShearCotTheta",
                "Standard", "StandardEdition"));
            v.Record(typeof(BridgeHistoryDefinition), "GPC.Model.Checker.Configuration.BridgeHistoryDefinition", Properties("ConcreteLayers",
                "EffectiveRelaxation", "EffectiveTolerance", "FlangeLayers", "ForceTolerance", "InstantaneousConcrete", "MaximumEffectiveIterations",
                "MaximumLineSearchIterations", "MaximumNewtonIterations", "MomentTolerance", "RelativeTolerance", "SubstepsPerPhase", "WebLayers"));
            v.Record(typeof(BridgeCaseDefinition), "GPC.Model.Checker.Configuration.BridgeCaseDefinition", Properties("ActionReferenceY", "Cut",
                "HistoryAndReferenceConfirmed", "Input", "SectionAxes", "Source", "State"));
            v.Record(typeof(BridgeInputDefinition), "GPC.Model.Checker.Configuration.BridgeInputDefinition", Properties("BottomRebars", "Box", "Concrete",
                "Fatigue", "Geometry", "Intermediate", "Options", "Phases", "Rebar", "Steel", "Studs", "Support", "TopRebars", "Transverse"));
            v.Record(typeof(CompositeBridgeCase), "GPC.Model.Checker.CompositeBridgeCase", Properties("ActionReferenceY", "BeamId",
                "HistoryAndReferenceConfirmed", "Input", "SectionAxes", "Side", "Source", "State", "Station", "StationDomain"));
            v.Record(typeof(ConfiguredCheckJob), "GPC.Model.Checker.Configuration.ConfiguredCheckJob", Properties("Cuts", "Name", "Plan", "Routes"));
            v.Record(typeof(BeamCut), "GPC.Model.Checker.Configuration.BeamCut", Properties("Domain", "ElementId", "Parameter", "Side"));
            v.Record(typeof(CheckRouteDefinition), "GPC.Model.Checker.Configuration.CheckRouteDefinition", Properties("Checks", "Elements", "EngineId",
                "Mechanisms", "PlateCheckIds"));
            v.Record(typeof(ConfiguredPlateJob), "GPC.Model.Checker.Configuration.ConfiguredPlateJob", Properties("Axes", "AxesKind", "Checks", "Name",
                "Preparation", "Routes"));
            v.Record(typeof(PlateCheckSpecification), "GPC.Model.Checker.Configuration.PlateCheckSpecification", Properties("Category", "Direction", "Face",
                "Id", "Mechanism", "MethodId", "ReinforcementRequired"));
            v.Record(typeof(PlateFaceReference), "GPC.Model.Checker.Configuration.PlateFaceReference", Properties("NormalFace", "PhysicalName"));
        }

        private static FingerprintMember[] Properties(params string[] names) => names.Select(n => FingerprintMember.AutoProperty(n)).ToArray();
    }
}
