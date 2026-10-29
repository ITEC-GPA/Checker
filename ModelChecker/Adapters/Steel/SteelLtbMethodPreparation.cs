using System;
using System.Globalization;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;

namespace GPC.Model.Checker
{
    public sealed partial class SteelMemberPipelineChecker
    {
        private static readonly string[] LtbMomentKeys = { "LtbMomentDistribution", "LtbPsi", "LtbMomentSource" };
        private static readonly string[] LtbCurveKeys = { "LtbCurve", "LtbCurveSource" };
        private static readonly string[] LtbExemptionKeys = { "LtbLowMomentExemption", "LtbLowMomentExemptionSource" };

        // LtbMethod=RolledOrEquivalentWelded: EN 1993-1-1:2005 6.3.2.3 / NTC 2018 4.2.4.1.3.2 with the code object's
        // BetaForLateralTorsionalBucklingMod/LambdaLT0ForLateralTorsionalBucklingMod; alphaLT remain the Table 6.3
        // values validated by ValidatePrismaticCoefficients. Absent or General without the EC3 options below: the
        // analysis is returned unchanged.
        /// <param name="segments">Fork segments of the member: with more than one, physical is the segment view.</param>
        private SteelMemberAnalysis WithLtbMethod(SteelMemberAnalysis analysis, string mode, SteelPhysicalMemberPreparation physical, int segments = 1)
        {
            bool options = LtbCurveKeys.Concat(LtbExemptionKeys).Any(_parameters.ContainsKey);
            if (!_parameters.TryGetValue("LtbMethod", out var method) && !options)
            {
                if (LtbMomentKeys.Any(_parameters.ContainsKey)) throw new ArgumentException("LtbMomentWithoutRolledMethod");
                return analysis;
            }
            if (mode != "Bending" && mode != "BeamColumn") throw new ArgumentException("LtbMethodOnAnotherRoute");
            SteelLateralTorsionalMethod selected;
            if (method == null || method == "General")
            {
                if (LtbMomentKeys.Any(_parameters.ContainsKey)) throw new ArgumentException("LtbMomentWithoutRolledMethod");
                if (!options) return analysis;
                selected = SteelLateralTorsionalMethod.General;
            }
            else
            {
                if (method != "RolledOrEquivalentWelded") throw new ArgumentException("InvalidSteelMemberParameter: LtbMethod");
                double beta = Code.BetaForLateralTorsionalBucklingMod, lambda0 = Code.LambdaLT0ForLateralTorsionalBucklingMod;
                if (!(beta >= .75 && beta <= 1 && lambda0 >= .2 && lambda0 <= .4))
                    throw new NotSupportedException("LtbRolledParametersNotQualified: the code object needs 0.75 <= BetaForLateralTorsionalBucklingMod <= 1 and 0.2 <= LambdaLT0ForLateralTorsionalBucklingMod <= 0.4.");
                string source = Code.Name + " (" + Edition + (NationalAnnex == null ? "" : ", " + NationalAnnex) + "): BetaForLateralTorsionalBucklingMod="
                    + Invariant(beta) + ", LambdaLT0ForLateralTorsionalBucklingMod=" + Invariant(lambda0);
                selected = SteelLateralTorsionalMethod.RolledOrEquivalentWelded(beta, lambda0, LtbMomentFactor(mode, physical, segments), source);
            }
            return analysis.WithLateralTorsionalMethod(WithLtbOptions(selected, mode));
        }

        // EN 1993-1-1 options of either method, both explicit: LtbCurve replaces the Table 6.4/6.5 assignment (an NDP of
        // 6.3.2.2(2)/6.3.2.3(1)) with the National Annex's curve, also for the beam-column chiLT; LtbLowMomentExemption=true
        // adopts 6.3.2.2(4), MEd/Mcr <= lambdaLT0^2, on the Bending route only. The library refuses both under NTC 2018.
        private SteelLateralTorsionalMethod WithLtbOptions(SteelLateralTorsionalMethod method, string mode)
        {
            if (LtbCurveKeys.Any(_parameters.ContainsKey))
            {
                var curve = EnumValue<SteelBucklingCurve>("LtbCurve");
                if (curve == SteelBucklingCurve.A0) throw new ArgumentException("InvalidSteelMemberParameter: LtbCurve");
                method = method.WithDeclaredCurve(curve, Required("LtbCurveSource") + " (" + Code.Name + ", " + Edition
                    + (NationalAnnex == null ? "" : ", " + NationalAnnex) + ")");
            }
            if (LtbExemptionKeys.Any(_parameters.ContainsKey))
            {
                if (mode != "Bending") throw new ArgumentException("LtbLowMomentExemptionOnAnotherRoute");
                if (!bool.TryParse(Required("LtbLowMomentExemption"), out bool adopted)) throw new ArgumentException("InvalidSteelMemberParameter: LtbLowMomentExemption");
                if (adopted) method = method.WithLowMomentExemption(Required("LtbLowMomentExemptionSource"));
                else if (_parameters.ContainsKey("LtbLowMomentExemptionSource")) throw new ArgumentException("LtbLowMomentSourceWithoutExemption");
            }
            return method;
        }

        // kc only from a diagram that the preparation has validated on every exported cut of the full declared span
        // (FullDeclaredSpan: no intermediate restraints), or from an explicit sourced declaration; otherwise kc=1 (f=1).
        // Fork segments: the prepared diagram is the segment's own (its exported cuts checked by the segment preparation);
        // one declared distribution cannot describe several segments.
        private SteelLtbMomentFactor LtbMomentFactor(string mode, SteelPhysicalMemberPreparation physical, int segments)
        {
            string prepared = _parameters.TryGetValue(mode == "Bending" ? "CriticalValues" : "MomentFactorPreparation", out var p) ? p : null;
            if (_parameters.ContainsKey("LtbMomentDistribution"))
            {
                if (segments > 1) throw new ArgumentException("LtbMomentDistributionWithSegments");
                if (prepared != null) throw new ArgumentException("LtbMomentDistributionConflictsWithPreparedDiagram");
                var row = EnumValue<SteelLtbMomentDistribution>("LtbMomentDistribution"); string declared = Required("LtbMomentSource");
                if (row == SteelLtbMomentDistribution.Linear) return SteelLtbMomentFactor.Linear(Number("LtbPsi"), declared);
                if (_parameters.ContainsKey("LtbPsi")) throw new ArgumentException("LtbPsiRequiresLinearDistribution");
                return row == SteelLtbMomentDistribution.NotEstablished ? SteelLtbMomentFactor.NotEstablished(declared) : SteelLtbMomentFactor.Tabulated(row, declared);
            }
            if (_parameters.ContainsKey("LtbPsi") || _parameters.ContainsKey("LtbMomentSource")) throw new ArgumentException("LtbMomentDistributionRequired");
            double first = physical.Cuts.First().Actions.M1, last = physical.Cuts.Last().Actions.M1;
            string full = (segments > 1 ? "; fork segment between qualifying restraints, its exported cuts checked; "
                : "; full declared span without intermediate restraints, every exported cut checked; ") + Required("LoadPatternReference");
            switch (prepared)
            {
                case "UniformFork": return SteelLtbMomentFactor.Tabulated(SteelLtbMomentDistribution.Uniform, "Uniform M1 (CriticalValues=UniformFork)" + full);
                case "ForkDiagram":
                    var shape = EnumValue<SteelForkMomentShape>("ForkDiagram");
                    if (shape == SteelForkMomentShape.EndMoments)
                        return SteelLtbMomentFactor.Linear(SteelForkMomentDiagram.EndMoments(first, last, "end moments").EndMomentRatio, "Linear end-moment diagram (ForkDiagram=EndMoments)" + full);
                    if (shape == SteelForkMomentShape.UniformTransverseLoad)
                        return SteelLtbMomentFactor.Tabulated(SteelLtbMomentDistribution.SimplySupportedUniformLoad, "Parabolic diagram, zero end moments (ForkDiagram=UniformTransverseLoad)" + full);
                    if (shape == SteelForkMomentShape.CentralPointLoad)
                        return SteelLtbMomentFactor.Tabulated(SteelLtbMomentDistribution.SimplySupportedCentralLoad, "Triangular diagram, zero end moments (ForkDiagram=CentralPointLoad)" + full);
                    return SteelLtbMomentFactor.NotEstablished("ForkDiagram=GenericEnvelope: diagram shape not established, conservative kc=1");
                case "LinearDiagrams":
                    return first == 0 && last == 0 ? SteelLtbMomentFactor.NotEstablished("No major-axis end moments: conservative kc=1")
                        : SteelLtbMomentFactor.Linear(SteelForkMomentDiagram.EndMoments(first, last, "end moments").EndMomentRatio, "Linear M1 (MomentFactorPreparation=LinearDiagrams)" + full);
                case "ConservativeEnvelope": return SteelLtbMomentFactor.NotEstablished("MomentFactorPreparation=ConservativeEnvelope: generic diagram, conservative kc=1");
                default: return SteelLtbMomentFactor.NotEstablished("No prepared or declared moment diagram between lateral restraints: conservative kc=1, f=1");
            }
        }
        private static string Invariant(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    }
}
