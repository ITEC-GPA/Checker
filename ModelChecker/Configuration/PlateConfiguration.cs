using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using GPC.Geometry;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Core.Coordinates;

namespace GPC.Model.Checker.Configuration
{
    [DataContract(Name = "PlateCheck", Namespace = ConfigurationArchive.Namespace)]
    public sealed class PlateCheckSpecification
    {
        [DataMember(Order = 1, IsRequired = true)] public string Id { get; set; }
        [DataMember(Order = 2, IsRequired = true)] public string MethodId { get; set; }
        [DataMember(Order = 3)] public CheckMechanism Mechanism { get; set; }
        [DataMember(Order = 4)] public CombinationCategory Category { get; set; }
        [DataMember(Order = 5)] public SectionCheckDirection Direction { get; set; }
        /// <summary>Face relative to the normal of the prepared coordinate system.</summary>
        [DataMember(Order = 6)] public PlateFaceReference Face { get; set; }
        [DataMember(Order = 7)] public bool ReinforcementRequired { get; set; }
        public PlateCheckSpecification Copy() => new PlateCheckSpecification { Id = Id, MethodId = MethodId, Mechanism = Mechanism,
            Category = Category, Direction = Direction, ReinforcementRequired = ReinforcementRequired,
            Face = Face == null ? null : new PlateFaceReference { PhysicalName = Face.PhysicalName, NormalFace = Face.NormalFace } };
        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(MethodId) || !Enum.IsDefined(typeof(CheckMechanism), Mechanism)
                || !Enum.IsDefined(typeof(CombinationCategory), Category) || !Enum.IsDefined(typeof(SectionCheckDirection), Direction)
                || Face != null && (string.IsNullOrWhiteSpace(Face.PhysicalName) || !Enum.IsDefined(typeof(ShellNormalFace), Face.NormalFace)))
                throw new ArgumentException("ExplicitPlateMethodAndFaceRequired");
        }
    }
    [DataContract(Name = "PlateJob", Namespace = ConfigurationArchive.Namespace)]
    public sealed class ConfiguredPlateJob
    {
        [DataMember(Order = 1, IsRequired = true)] public string Name { get; set; }
        [DataMember(Order = 2, IsRequired = true)] public PreparationRequest Preparation { get; set; }
        [DataMember(Order = 3, IsRequired = true)] public PlateCheckSpecification[] Checks { get; set; } = new PlateCheckSpecification[0];
        [DataMember(Order = 4, IsRequired = true)] public CheckRouteDefinition[] Routes { get; set; } = new CheckRouteDefinition[0];
        [DataMember(Order = 5, IsRequired = true)] public ShellInputAxes AxesKind { get; set; } = ShellInputAxes.Reinforcement;
        [DataMember(Order = 6)] public CoordinateSystem Axes { get; set; }
    }
    public sealed class PlateEngineCapability
    {
        public string Kind { get; }
        public int Schema { get; }
        public string Description { get; }
        public string Limitations { get; }
        public bool IsAvailable => Factory != null;
        internal Func<EngineDefinition, DesignContextDefinition, IPlateChecker> Factory { get; }
        public PlateEngineCapability(string kind, int schema, string description, string limitations,
            Func<EngineDefinition, DesignContextDefinition, IPlateChecker> factory = null)
        {
            if (string.IsNullOrWhiteSpace(kind) || schema < 1) throw new ArgumentException("InvalidPlateCapability");
            Kind = kind; Schema = schema; Description = description; Limitations = limitations; Factory = factory;
        }
    }
    public sealed partial class EngineCatalog
    {
        private readonly Dictionary<string, PlateEngineCapability> _plates = new Dictionary<string, PlateEngineCapability>(StringComparer.Ordinal);
        public IReadOnlyCollection<PlateEngineCapability> PlateCapabilities => Array.AsReadOnly(_plates.Values.OrderBy(p => p.Kind, StringComparer.Ordinal).ToArray());
        public void RegisterPlate(PlateEngineCapability capability)
        {
            if (capability == null) throw new ArgumentNullException(nameof(capability));
            if (_plates.TryGetValue(capability.Kind, out var previous) && previous.IsAvailable) throw new ArgumentException("DuplicatePlateEngine");
            _plates[capability.Kind] = capability;
        }
        public PlateCheckRequest CompilePlates(VerificationConfiguration configuration)
        {
            var copy = ConfigurationArchive.Copy(configuration); var fingerprint = ConfigurationArchive.Fingerprint(copy);
            var used = new HashSet<string>(copy.PlateJobs.SelectMany(j => j.Routes).Select(r => r.EngineId), StringComparer.Ordinal);
            var engines = copy.Engines.Where(e => used.Contains(e.Id)).ToDictionary(e => e.Id, e => {
                var context = copy.Contexts.Single(c => c.Id == e.ContextId);
                ValidateBinding(e, context);
                if (!_plates.TryGetValue(e.Kind, out var descriptor)) throw new NotSupportedException("UnknownPlateEngine: " + e.Kind);
                if (descriptor.Schema != e.Schema) throw new NotSupportedException("UnsupportedPlateEngineSchema");
                if (!descriptor.IsAvailable) return null;
                var engine = descriptor.Factory(e, context) ?? throw new InvalidOperationException("NullPlateEngine");
                ValidateRuntime(engine.Id, engine.Version, engine.Configuration);
                if (engine.Standard == null || !engine.Standard.HasDeclaredEdition) throw new InvalidOperationException("UndeclaredPlateStandard");
                if (e.RequiredImplementationVersion != null && e.RequiredImplementationVersion != engine.Version) throw new InvalidOperationException("PlateEngineVersionMismatch");
                return engine;
            }, StringComparer.Ordinal);
            var request = new PlateCheckRequest();
            foreach (var job in copy.PlateJobs)
            {
                var preparation = job.Preparation.Copy(); preparation.Settings = (preparation.Settings ?? "") + "\nConfiguration:" + fingerprint;
                request.Jobs.Add(new PlateCheckJob { Name = job.Name, Preparation = preparation, Checks = job.Checks, Axes = job.Axes, AxesKind = job.AxesKind,
                    Assignments = job.Routes.Select(r => new PlateCheckerAssignment { Checker = engines[r.EngineId], EngineId = r.EngineId,
                        Selection = r.Elements, CheckIds = r.PlateCheckIds, Mechanisms = r.Mechanisms }).ToArray() });
            }
            return request;
        }
    }
}
