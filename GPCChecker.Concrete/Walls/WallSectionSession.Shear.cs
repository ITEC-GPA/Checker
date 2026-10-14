using System;
using System.Threading;
using GPC.Checkers.Concrete.Shear;
using GPC.Model.Checking.Contracts;

namespace GPC.Checkers.Concrete.Walls
{
    public sealed class WallShearResult
    {
        public WallSectionCheck Check { get; }
        public SectionShearResult Response { get; }
        internal WallShearResult(WallSectionCheck check, SectionShearResult response) { Check = check; Response = response; }
    }

    public sealed partial class WallSectionSession
    {
        /// <summary>Native no-link shear resistance, including axial compression and its normative cap.
        /// Tension is not evaluated automatically. Anchorage and applicability of a member without shear reinforcement
        /// must be established separately; this section resistance does not certify construction detailing.</summary>
        public WallShearResult CheckShear(WallSectionActions action, CancellationToken cancellation = default(CancellationToken))
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            lock (sync)
            {
                cancellation.ThrowIfCancellationRequested();
                var input = new SectionShearInput(standard, action.N, action.V, action.M, strip.ConcreteArea, strip.Width,
                    strip.EffectiveDepth(strip.TensionFace(action.M)), strip.TensionArea(action.M), Math.Abs(concrete.Fck),
                    Math.Abs(concrete.CalculateFcd(standard)), Math.Abs(steel.CalculateFyd(standard)), standard.GammaC, steel.E, 0, 1);
                var response = SectionShearCalculator.Calculate(input);
                cancellation.ThrowIfCancellationRequested();
                return new WallShearResult(new WallSectionCheck("Wall.ShearWithoutLinks", "N", Math.Abs(action.V),
                    response.Verdict == ShearVerdict.NotEvaluated ? (double?)null : response.VRd, ExecutionStatus.Completed, response.Status), response);
            }
        }
    }
}
