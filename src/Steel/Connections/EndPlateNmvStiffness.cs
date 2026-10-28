using System;
using System.Collections.Generic;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Qualification of the LOCAL elastic N-M component tangent at the beam
    /// face. The column panel shear flexibility is excluded and must be represented
    /// separately in a global model. No EN6.3 secant extrapolation at significant N,
    /// no scalar M/theta, available rotation or complete-frame qualification.</summary>
    public sealed class EndPlateNmvStiffnessResult
    {
        public EndPlateNmvResult Resistance { get; }
        public bool HasQualifiedLocalTangent { get; }
        public bool CanUseAsGlobalConnectionSpring { get; }
        public bool CanQualifyWholeFrame => false;
        public EndPlateNmvTangent LocalTangent { get; }
        public double? AxialOpening { get; }
        public double? ModelRotation { get; }
        public bool? GlobalMatrixMatches { get; }
        public bool RequiresGlobalModelReview => Resistance.Input.AppliedGlobalTangent != null
            && (!CanUseAsGlobalConnectionSpring || GlobalMatrixMatches == false);
        public IReadOnlyList<string> LocalDiagnostics { get; }
        public IReadOnlyList<string> GlobalDiagnostics { get; }
        public string Scope => "Supplementary unilateral elastic component tangent at the beam face, fixed active branch. "
            + "Column panel flexibility must be modelled separately; no axial-moment nonlinear secant or rotation-capacity law.";

        internal EndPlateNmvStiffnessResult(EndPlateNmvResult result)
        {
            Resistance = result;
            var local = new List<string>(); var global = new List<string>();
            if (!result.IsComplete || result.Status != ConnectionStatus.Passed) local.Add("PassedCompleteResistanceCaseRequired");
            var eq = result.Equilibrium;
            if (eq == null || !eq.IsConverged) local.Add("ConvergedSameCaseEquilibriumRequired");
            else
            {
                AxialOpening = eq.AxialOpening; ModelRotation = eq.Rotation;
                if (eq.IsAtContactSwitch) local.Add("ContactOrRowSwitchHasDirectionalTangentOnly");
                if (!eq.Tangent.IsPositiveDefinite) local.Add("NoStablePositiveDefiniteLocalTangent");
            }
            // Deliberate, documented elastic-use gate. It is not an extension of
            // the EN pure-moment secant exponent to a general axial load case.
            if (!result.ComponentUtilization.HasValue || result.ComponentUtilization > 2d / 3)
                local.Add("ComponentDemandOutsideConservativeInitialElasticUseRange");
            HasQualifiedLocalTangent = local.Count == 0;
            if (HasQualifiedLocalTangent) LocalTangent = eq.Tangent;
            if (!result.Input.ColumnPanelModelledSeparately || string.IsNullOrWhiteSpace(result.Input.ColumnPanelModelSource))
                global.Add("SeparateColumnPanelModelAndMatchingBeamFacePortsRequired");
            CanUseAsGlobalConnectionSpring = HasQualifiedLocalTangent && global.Count == 0;
            var applied = result.Input.AppliedGlobalTangent;
            if (CanUseAsGlobalConnectionSpring && applied != null)
            {
                double couplingScale = Math.Sqrt(LocalTangent.Axial) * Math.Sqrt(LocalTangent.Rotation);
                GlobalMatrixMatches = Close(applied.Axial, LocalTangent.Axial, LocalTangent.Axial)
                    && Close(applied.Rotation, LocalTangent.Rotation, LocalTangent.Rotation)
                    && Close(applied.Coupling, LocalTangent.Coupling, couplingScale);
                if (GlobalMatrixMatches == false) global.Add("AppliedNMMatrixDiffersReanalyseTheGlobalModel");
            }
            LocalDiagnostics = Array.AsReadOnly(local.ToArray()); GlobalDiagnostics = Array.AsReadOnly(global.ToArray());
        }
        private static bool Close(double a, double b, double scale) => Math.Abs(a - b) <= 1e-8 * scale;
    }

    internal static class EndPlateNmvStiffness
    {
        public static EndPlateNmvStiffnessResult Evaluate(EndPlateNmvResult resistance)
        {
            if (resistance == null) throw new ArgumentNullException(nameof(resistance));
            return new EndPlateNmvStiffnessResult(resistance);
        }
    }
}
