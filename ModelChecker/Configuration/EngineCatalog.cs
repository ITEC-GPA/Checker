using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Model.Core;
using GPC.Model.Standards;
using GPC.Model.Checking.Contracts;

namespace GPC.Model.Checker.Configuration
{
    public sealed class EngineCapability
    {
        public string Kind { get; }
        public int Schema { get; }
        public string Description { get; }
        public string Limitations { get; }
        internal Func<EngineDefinition, DesignContextDefinition, IMaterialChecker> Factory { get; }
        internal Func<IMaterialChecker, CheckMechanism, SectionCheckSpecification, bool> Supports { get; }
        public EngineCapability(string kind, int schema, string description, string limitations,
            Func<EngineDefinition, DesignContextDefinition, IMaterialChecker> factory,
            Func<IMaterialChecker, CheckMechanism, SectionCheckSpecification, bool> supports)
        {
            if (string.IsNullOrWhiteSpace(kind) || schema < 1) throw new ArgumentException("InvalidEngineCapability");
            Kind = kind; Schema = schema; Description = description; Limitations = limitations;
            Factory = factory ?? throw new ArgumentNullException(nameof(factory)); Supports = supports ?? throw new ArgumentNullException(nameof(supports));
        }
    }
    /// <summary>Only explicitly registered factories can be instantiated. Files contain keys/data, never CLR class names.</summary>
    public sealed partial class EngineCatalog
    {
        private readonly Dictionary<string, EngineCapability> _engines = new Dictionary<string, EngineCapability>(StringComparer.Ordinal);
        public IReadOnlyCollection<EngineCapability> Capabilities => Array.AsReadOnly(_engines.Values.OrderBy(e => e.Kind, StringComparer.Ordinal).ToArray());
        public void Register(EngineCapability capability)
        {
            if (capability == null) throw new ArgumentNullException(nameof(capability));
            _engines.Add(capability.Kind, capability);
        }
        public bool Supports(EngineDefinition definition, DesignContextDefinition context, CheckMechanism mechanism, SectionCheckSpecification check = null)
        {
            var engine = Create(ConfigurationArchive.CopyData(definition), ConfigurationArchive.CopyData(context));
            return _engines[definition.Kind].Supports(engine, mechanism, check);
        }
        private IMaterialChecker Create(EngineDefinition definition, DesignContextDefinition context)
        {
            ValidateBinding(definition, context);
            if (!_engines.TryGetValue(definition.Kind, out var descriptor) || descriptor.Schema != definition.Schema)
                throw new NotSupportedException("UnknownEngineOrSchema: " + definition.Kind);
            var engine = descriptor.Factory(definition, context) ?? throw new InvalidOperationException("NullEngineFactoryResult");
            ValidateRuntime(engine.Id, engine.Version, engine.Configuration);
            if (definition.RequiredImplementationVersion != null && definition.RequiredImplementationVersion != engine.Version)
                throw new InvalidOperationException("EngineVersionMismatch: " + definition.Id);
            return engine;
        }
        private static void ValidateBinding(EngineDefinition definition, DesignContextDefinition context)
        {
            if (definition == null || context == null || string.IsNullOrWhiteSpace(definition.Id) || string.IsNullOrWhiteSpace(definition.Kind)
                || definition.Schema < 1 || definition.Parameters == null || string.IsNullOrWhiteSpace(context.Id) || context.Revision < 1
                || string.IsNullOrWhiteSpace(context.Edition) || context.Units != CheckUnitConvention.N_Mm_Rad
                || definition.ContextId != context.Id || definition.ContextRevision != context.Revision)
                throw new ArgumentException("InvalidEngineDesignContextBinding");
        }
        private static void ValidateRuntime(string id, string version, string configuration)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(version) || configuration == null)
                throw new InvalidOperationException("IncompleteRuntimeEngineIdentity");
        }
        /// <summary>Independent runtime adapters; no native session is created until execution.</summary>
        public MultiMaterialCheckRequest Compile(VerificationConfiguration configuration)
        {
            var copy = ConfigurationArchive.Copy(configuration);
            string fingerprint = ConfigurationArchive.Fingerprint(copy);
            var used = new HashSet<string>(copy.Jobs.SelectMany(j => j.Routes).Select(r => r.EngineId), StringComparer.Ordinal);
            var engines = copy.Engines.Where(e => used.Contains(e.Id)).ToDictionary(e => e.Id,
                e => Create(e, copy.Contexts.Single(c => c.Id == e.ContextId)), StringComparer.Ordinal);
            var request = new MultiMaterialCheckRequest();
            foreach (var job in copy.Jobs)
            {
                var plan = job.Plan.Copy();
                if (job.Cuts != null)
                {
                    if (plan.Locations.Length != 0) throw new ArgumentException("SpecifyCutsOrLegacyLocations");
                    plan.Locations = job.Cuts.Select(c => c.ToLocation()).ToArray();
                }
                foreach (var location in plan.Locations.Where(l => l.BeamId.HasValue))
                    if (!Enum.TryParse<BeamStationDomain>(location.StationDomain, out var domain) || !Enum.IsDefined(typeof(BeamStationDomain), domain)
                        || domain.ToString() != location.StationDomain) throw new ArgumentException("UnsupportedStationDomain");
                plan.Settings = (plan.Settings ?? "") + "\nConfiguration:" + fingerprint;
                request.Jobs.Add(new MultiMaterialCheckJob { Name = job.Name, Plan = plan, Assignments = job.Routes.Select(r => new MaterialCheckerAssignment {
                    Checker = engines[r.EngineId], Selection = r.Elements, Mechanisms = r.Mechanisms, Checks = r.Checks }).ToArray() });
            }
            return request;
        }
        public static EngineCatalog BuiltIn() => BuiltIn(new ConcreteCalculationCatalog());
        public static EngineCatalog BuiltIn(ConcreteCalculationCatalog calculations)
        {
            if (calculations == null) throw new ArgumentNullException(nameof(calculations));
            var catalog = new EngineCatalog();
            catalog.Register(new EngineCapability("Concrete.Section", 1, "Concrete section checks", "Explicit standard/edition; detailed checks retain the native profile's applicability.",
                (definition, context) => {
                    NoExtraParameters(definition);
                    if (!(context.Code is StandardModelCode2010 code) || context.BridgeCode.HasValue) throw new ArgumentException("ConcreteCodeRequired");
                    var options = definition.Concrete ?? new ConcreteVerificationOptions();
                    if (options.Standard != null && ModelValues.Fingerprint(new object[] { options.Standard }) != ModelValues.Fingerprint(new object[] { code })
                        || options.StandardEdition != null && options.StandardEdition != context.Edition
                        || options.NationalAnnex != null && options.NationalAnnex != context.NationalAnnex) throw new ArgumentException("ConflictingConcreteContext");
                    options.Standard = code; options.StandardEdition = context.Edition; options.NationalAnnex = context.NationalAnnex;
                    return new ConcreteMaterialChecker(options, calculations.Resolve(options));
                }, (engine, mechanism, check) => {
                    var verifier = ((ConcreteMaterialChecker)engine).CreateVerifier();
                    return check == null ? verifier.Capabilities.Contains(mechanism) : check.Mechanism == mechanism && verifier.Supports(check);
                }));
            catalog.Register(new EngineCapability("Steel.EN1993.PlasticShear", 1, "Steel section shear", "Qualified stocky symmetric H sections, EN1993-1-1:2005, no torsion/global stability.",
                (definition, context) => {
                    NoExtraParameters(definition);
                    if (!(context.Code is StandardEN1993p11 code) || context.BridgeCode.HasValue) throw new ArgumentException("SteelCodeRequired");
                    return new SteelMaterialChecker(code, context.Edition, context.NationalAnnex);
                }, (engine, mechanism, check) => check == null && mechanism == CheckMechanism.Shear
                    && ((SteelMaterialChecker)engine).Edition == "2005" && ((SteelMaterialChecker)engine).Code.GetType() == typeof(StandardEN1993p11)));
            catalog.Register(new EngineCapability("Steel.EN1993.Section", 1, "Steel section shear and uniaxial bending",
                "EN1993-1-1:2005; symmetric H stocky in compression; bending requires N=T=0, one moment axis and low shear. No global stability.",
                (definition, context) => {
                    NoExtraParameters(definition);
                    if (!(context.Code is StandardEN1993p11 code) || context.BridgeCode.HasValue) throw new ArgumentException("SteelCodeRequired");
                    return new SteelMaterialChecker(code, context.Edition, context.NationalAnnex, true);
                }, (engine, mechanism, check) => check == null && (mechanism == CheckMechanism.Shear || mechanism == CheckMechanism.UlsBiaxialSection)
                    && ((SteelMaterialChecker)engine).Edition == "2005" && ((SteelMaterialChecker)engine).Code.GetType() == typeof(StandardEN1993p11)));
            catalog.Register(new EngineCapability("CompositeBridge.Section", 1, "Composite bridge section", "Explicit phase history and moment reference required; SLE stresses and web shear only.",
                (definition, context) => {
                    NoExtraParameters(definition);
                    if (!context.BridgeCode.HasValue || context.Code != null || definition.BridgeCases == null) throw new ArgumentException("BridgeContextAndCasesRequired");
                    return new CompositeBridgeMaterialChecker(context.BridgeCode.Value, context.Edition,
                        definition.BridgeCases.Select(c => c.ToCase()), context.NationalAnnex);
                }, (engine, mechanism, check) => check == null && (mechanism == CheckMechanism.Serviceability || mechanism == CheckMechanism.Shear)));
            catalog.Register(new EngineCapability("CompositeBridge.HistoryLinear", 1, "Composite bridge linear construction history",
                "Explicit incremental phase history, reference and numerical settings. Only SLE stresses; no shear, nonlinear resistance or member stability.",
                (definition, context) => {
                    NoExtraParameters(definition);
                    if (!context.BridgeCode.HasValue || context.Code != null || definition.BridgeCases == null || definition.BridgeHistory == null)
                        throw new ArgumentException("BridgeHistoryContextAndOptionsRequired");
                    return new CompositeBridgeMaterialChecker(context.BridgeCode.Value, context.Edition,
                        definition.BridgeCases.Select(c => c.ToCase()), context.NationalAnnex, definition.BridgeHistory);
                }, (engine, mechanism, check) => check == null && mechanism == CheckMechanism.Serviceability
                    && ((CompositeBridgeMaterialChecker)engine).Edition == (((CompositeBridgeMaterialChecker)engine).Code == GPC.Checkers.CompositeBridge.BridgeStandard.Ntc2018 ? "2018" : "2005")));
            catalog.RegisterPlate(new PlateEngineCapability("Concrete.Plate", 1, "Concrete plate checks", "No qualified resistant implementation is installed; explicit method, faces and reinforcement are required."));
            catalog.RegisterPlate(new PlateEngineCapability("Steel.Plate", 1, "Steel plate checks", "No qualified plate resistance/stability implementation is installed."));
            return catalog;
        }
        private static void NoExtraParameters(EngineDefinition definition)
        {
            if (definition.BridgeHistory != null && definition.Kind != "CompositeBridge.HistoryLinear")
                throw new ArgumentException("BridgeHistoryOptionsOnDifferentEngine");
            if (definition.Parameters.Count != 0) throw new ArgumentException("UnknownBuiltInEngineParameters");
        }
    }
}
