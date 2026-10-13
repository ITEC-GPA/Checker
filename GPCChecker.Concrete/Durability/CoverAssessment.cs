using System;
using GPC.Model.Checking.Contracts;
using GPC.Model.Core.Diagnostics;

namespace GPC.Checkers.Concrete.Durability
{
    /// <summary>Combines the material-sheet requirement and the requirement for actual bars (mm).
    /// No cover formula is repeated here: both minima come from CoverRequirements.
    /// An already insufficient adopted cover fails even if the actual reinforcement is still unavailable.</summary>
    public sealed class CoverAssessment
    {
        public double Adopted { get; }
        public double MaterialMinimum { get; }
        public double? ActualBarsMinimum { get; }
        public double Required { get; }
        public EngineeringOutcome Outcome { get; }
        public ExecutionStatus Execution { get { return ExecutionStatus.Completed; } }
        public DataStatus Data { get; }
        public bool? Passed { get { return Outcome == EngineeringOutcome.NotEvaluated ? (bool?)null : Outcome == EngineeringOutcome.Satisfied; } }
        private CoverAssessment(double adopted,double materialMinimum,double? actualBarsMinimum)
        {
            Adopted=adopted;MaterialMinimum=materialMinimum;ActualBarsMinimum=actualBarsMinimum;
            Required=Math.Max(materialMinimum,actualBarsMinimum ?? materialMinimum);
            Data=actualBarsMinimum.HasValue?DataStatus.Ready:DataStatus.Insufficient;
            Outcome=adopted<Required?EngineeringOutcome.NotSatisfied:actualBarsMinimum.HasValue?EngineeringOutcome.Satisfied:EngineeringOutcome.NotEvaluated;
        }
        public static CoverAssessment Evaluate(double adopted,double materialMinimum,double? actualBarsMinimum)
        {
            if(!Valid(adopted)||!Valid(materialMinimum)||actualBarsMinimum.HasValue&&!Valid(actualBarsMinimum.Value))
                throw new ArgumentException("Finite nonnegative cover values required.");
            return new CoverAssessment(adopted,materialMinimum,actualBarsMinimum);
        }
        private static bool Valid(double value) { return !double.IsNaN(value)&&!double.IsInfinity(value)&&value>=0; }
    }
}
