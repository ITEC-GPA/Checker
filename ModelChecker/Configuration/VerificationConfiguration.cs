using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using GPC.Model.Standards;
using GPC.Checkers.CompositeBridge;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Results.Locations;

namespace GPC.Model.Checker.Configuration
{
    public enum CheckUnitConvention { N_Mm_Rad }
    public enum BeamStationDomain { NodeToNode, OffsetToOffset, Deformable }
    public enum ShellNormalFace { Positive, Negative }

    [DataContract(Name = "BeamCut", Namespace = ConfigurationArchive.Namespace)]
    public sealed class BeamCut
    {
        [DataMember(Order = 1)] public int ElementId { get; set; }
        [DataMember(Order = 2)] public double Parameter { get; set; }
        [DataMember(Order = 3)] public BeamStationDomain Domain { get; set; }
        [DataMember(Order = 4)] public SectionSide Side { get; set; }
        public RequiredBeamLocation ToLocation()
        {
            if (double.IsNaN(Parameter) || Parameter < 0 || Parameter > 1 || !Enum.IsDefined(typeof(BeamStationDomain), Domain)
                || !Enum.IsDefined(typeof(SectionSide), Side)) throw new ArgumentException("InvalidBeamCut");
            return new RequiredBeamLocation { BeamId = ElementId, Station = Parameter, StationDomain = Domain.ToString(), Side = Side };
        }
    }
    /// <summary>Explicit correspondence with the plate normal, never inferred from a physical-face label.</summary>
    [DataContract(Name = "PlateFace", Namespace = ConfigurationArchive.Namespace)]
    public sealed class PlateFaceReference
    {
        [DataMember(Order = 1)] public string PhysicalName { get; set; }
        [DataMember(Order = 2)] public ShellNormalFace NormalFace { get; set; }
    }
    [DataContract(Name = "DesignContext", Namespace = ConfigurationArchive.Namespace)]
    public sealed class DesignContextDefinition
    {
        [DataMember(Order = 1, IsRequired = true)] public string Id { get; set; }
        [DataMember(Order = 2, IsRequired = true)] public int Revision { get; set; } = 1;
        [DataMember(Order = 3, IsRequired = true)] public string Edition { get; set; }
        [DataMember(Order = 4)] public string NationalAnnex { get; set; }
        public Standard Code { get; set; }
        // The legacy abstract Standard is not a data contract; its concrete, closed-vocabulary types are.
        [DataMember(Name = "Code", Order = 5)] private object CodeContract {
            get => Code;
            set => Code = value == null ? null : value as Standard ?? throw new SerializationException("InvalidStandardContract");
        }
        [DataMember(Order = 6)] public BridgeStandard? BridgeCode { get; set; }
        [DataMember(Order = 7, IsRequired = true)] public CheckUnitConvention Units { get; set; }
        [DataMember(Order = 8)] public string Source { get; set; }
    }
    /// <summary>Data only. Runtime factories and native checker instances never enter this contract.</summary>
    [DataContract(Name = "Engine", Namespace = ConfigurationArchive.Namespace)]
    public sealed class EngineDefinition
    {
        [DataMember(Order = 1, IsRequired = true)] public string Id { get; set; }
        [DataMember(Order = 2, IsRequired = true)] public string Kind { get; set; }
        [DataMember(Order = 3, IsRequired = true)] public int Schema { get; set; } = 1;
        [DataMember(Order = 4, IsRequired = true)] public string ContextId { get; set; }
        [DataMember(Order = 5, IsRequired = true)] public int ContextRevision { get; set; } = 1;
        [DataMember(Order = 6)] public string RequiredImplementationVersion { get; set; }
        [DataMember(Order = 7)] public ConcreteVerificationOptions Concrete { get; set; }
        [DataMember(Order = 8)] public BridgeCaseDefinition[] BridgeCases { get; set; }
        [DataMember(Order = 10, EmitDefaultValue = false)] public BridgeHistoryDefinition BridgeHistory { get; set; }
        [DataMember(Order = 9)] public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
    }
    [DataContract(Name = "Route", Namespace = ConfigurationArchive.Namespace)]
    public sealed class CheckRouteDefinition
    {
        [DataMember(Order = 1, IsRequired = true)] public string EngineId { get; set; }
        [DataMember(Order = 2)] public ElementSelection Elements { get; set; }
        [DataMember(Order = 3)] public CheckMechanism[] Mechanisms { get; set; }
        [DataMember(Order = 4)] public SectionCheckSpecification[] Checks { get; set; }
        [DataMember(Order = 5)] public string[] PlateCheckIds { get; set; }
    }
    [DataContract(Name = "Job", Namespace = ConfigurationArchive.Namespace)]
    public sealed class ConfiguredCheckJob
    {
        [DataMember(Order = 1, IsRequired = true)] public string Name { get; set; }
        [DataMember(Order = 2, IsRequired = true)] public BeamCheckPlanRequest Plan { get; set; }
        [DataMember(Order = 3, IsRequired = true)] public CheckRouteDefinition[] Routes { get; set; } = new CheckRouteDefinition[0];
        /// <summary>Optional typed replacement for Plan.Locations; cannot be supplied together with it.</summary>
        [DataMember(Order = 4)] public BeamCut[] Cuts { get; set; }
    }
    [DataContract(Name = "VerificationConfiguration", Namespace = ConfigurationArchive.Namespace)]
    public sealed class VerificationConfiguration
    {
        [DataMember(Order = 1, IsRequired = true)] public int Schema { get; set; } = 1;
        [DataMember(Order = 2, IsRequired = true)] public DesignContextDefinition[] Contexts { get; set; } = new DesignContextDefinition[0];
        [DataMember(Order = 3, IsRequired = true)] public EngineDefinition[] Engines { get; set; } = new EngineDefinition[0];
        [DataMember(Order = 4, IsRequired = true)] public ConfiguredCheckJob[] Jobs { get; set; } = new ConfiguredCheckJob[0];
        [DataMember(Order = 5)] public ConfiguredPlateJob[] PlateJobs { get; set; } = new ConfiguredPlateJob[0];
    }
}
