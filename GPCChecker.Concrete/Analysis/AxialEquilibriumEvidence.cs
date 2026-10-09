using System;

namespace GPC.Checkers.Concrete.Analysis
{
    /// <summary>Axial equilibrium at assigned N, in N. This does not certify moment equilibrium or an engineering verdict.</summary>
    public sealed class AxialEquilibriumEvidence
    {
        public double Requested { get; }
        public double Actual { get; }
        public double Tolerance { get; }
        public double Residual => Actual - Requested;
        public bool Accepted => Math.Abs(Residual) <= Tolerance;
        public AxialEquilibriumEvidence(double requested, double actual, double tolerance)
        {
            if (!SectionAnalysisInput.Finite(requested) || !SectionAnalysisInput.Finite(actual)
                || !SectionAnalysisInput.Finite(actual - requested) || !SectionAnalysisInput.Finite(tolerance) || tolerance < 0)
                throw new ArgumentException("Finite forces and nonnegative tolerance required.");
            Requested = requested; Actual = actual; Tolerance = tolerance;
        }
    }
}
