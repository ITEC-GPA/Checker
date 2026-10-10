using System;
using System.Linq;
using System.Runtime.Serialization;
using GPC.Checkers.CompositeBridge.History;

namespace GPC.Model.Checker.Configuration
{
    /// <summary>Explicit linear construction-history settings. Native constitutive laws and verification methods are unchanged.</summary>
    [DataContract(Name = "BridgeHistory", Namespace = ConfigurationArchive.Namespace)]
    public sealed class BridgeHistoryDefinition
    {
        [DataMember(Order = 1, IsRequired = true)] public int WebLayers { get; set; } = 160;
        [DataMember(Order = 2, IsRequired = true)] public int FlangeLayers { get; set; } = 4;
        [DataMember(Order = 3, IsRequired = true)] public int ConcreteLayers { get; set; } = 32;
        [DataMember(Order = 4, IsRequired = true)] public int SubstepsPerPhase { get; set; } = 1;
        [DataMember(Order = 5, IsRequired = true)] public bool InstantaneousConcrete { get; set; }
        [DataMember(Order = 6, IsRequired = true)] public int MaximumNewtonIterations { get; set; } = 80;
        [DataMember(Order = 7, IsRequired = true)] public int MaximumEffectiveIterations { get; set; } = 160;
        [DataMember(Order = 8, IsRequired = true)] public int MaximumLineSearchIterations { get; set; } = 24;
        [DataMember(Order = 9, IsRequired = true)] public double RelativeTolerance { get; set; } = 1e-9;
        [DataMember(Order = 10, IsRequired = true)] public double ForceTolerance { get; set; } = 1e-4;
        [DataMember(Order = 11, IsRequired = true)] public double MomentTolerance { get; set; } = .01;
        [DataMember(Order = 12, IsRequired = true)] public double EffectiveTolerance { get; set; } = 1e-7;
        [DataMember(Order = 13, IsRequired = true)] public double EffectiveRelaxation { get; set; } = .55;
        public HBridgeHistoryOptions CreateOptions()
        {
            if (new[] { WebLayers, FlangeLayers, ConcreteLayers, SubstepsPerPhase, MaximumNewtonIterations,
                    MaximumEffectiveIterations, MaximumLineSearchIterations }.Any(n => n < 1)
                || new[] { ForceTolerance, MomentTolerance, EffectiveTolerance, EffectiveRelaxation }.Any(v => double.IsNaN(v) || double.IsInfinity(v) || v <= 0)
                || double.IsNaN(RelativeTolerance) || double.IsInfinity(RelativeTolerance) || RelativeTolerance < 0 || EffectiveRelaxation > 1)
                throw new ArgumentException("InvalidBridgeHistorySettings");
            return new HBridgeHistoryOptions { MaterialMode = HistoryMaterialMode.Linear,
                WebLayers = WebLayers, FlangeLayers = FlangeLayers, ConcreteLayers = ConcreteLayers,
                SubstepsPerPhase = SubstepsPerPhase, InstantaneousConcrete = InstantaneousConcrete,
                Solver = new HistorySolverOptions { MaximumNewtonIterations = MaximumNewtonIterations,
                    MaximumEffectiveIterations = MaximumEffectiveIterations, MaximumLineSearchIterations = MaximumLineSearchIterations,
                    RelativeTolerance = RelativeTolerance, ForceTolerance = ForceTolerance, MomentTolerance = MomentTolerance,
                    EffectiveTolerance = EffectiveTolerance, EffectiveRelaxation = EffectiveRelaxation } };
        }
    }
}
