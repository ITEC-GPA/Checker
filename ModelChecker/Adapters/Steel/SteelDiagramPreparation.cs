using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Sections;
using GPC.Model.Checking.Contracts;

namespace GPC.Model.Checker
{
    public sealed partial class SteelMemberPipelineChecker
    {
        private static void FullDeclaredSpan(PhysicalMemberCheckInput member)
        {
            var s = member.Snapshot;
            if (s.SpanStart != 0 || s.SpanEnd != s.Length || s.Length != s.DeformableLength || s.Context.Restraints.Count != 0
                || s.Context.LateralTorsionalLength.HasValue && s.Context.LateralTorsionalLength.Value != s.Length)
                throw new NotSupportedException("Diagram preparation requires a full prismatic deformable span without intermediate restraints; FEM nodes are not restraints.");
        }

        private IReadOnlyList<SteelStabilityValue> PrepareForkDiagram(PhysicalMemberCheckInput member,
            SteelVerificationInput input, SteelPhysicalMemberPreparation physical)
        {
            // A fork segment is qualified by its restraint plan; its cuts are measured from the segment start.
            if (physical.Segment == null) FullDeclaredSpan(member);
            var shape = EnumValue<SteelForkMomentShape>("ForkDiagram");
            if (physical.Segment?.Count > 1 && (shape == SteelForkMomentShape.UniformTransverseLoad || shape == SteelForkMomentShape.CentralPointLoad))
                throw new SteelPreparationRefusal("SteelMember.Segment.DiagramShapeNotQualified", "A full-span transverse-load shape does not describe a fork segment; use EndMoments or GenericEnvelope per segment.");
            double first = physical.Cuts.First().Actions.M1, last = physical.Cuts.Last().Actions.M1;
            double maximum = physical.Cuts.OrderByDescending(c => Math.Abs(c.Actions.M1)).First().Actions.M1;
            string source = Required("LoadPatternReference");
            SteelForkMomentDiagram diagram;
            if (shape == SteelForkMomentShape.GenericEnvelope)
            {
                diagram = SteelForkMomentDiagram.GenericEnvelope(input.Member.Length,
                    physical.Cuts.Select(c => new SteelMomentStation(c.Distance, c.Actions.M1)), Number("ForkLoadHeight"),
                    source + "; extrema/cut coverage: " + Required("MomentDiagramCoverageSource"));
                if (Required("McrBasis") != "UniformMoment")
                    throw new ArgumentException("Generic envelope uses an explicit conservative UniformMoment bound, not an actual-diagram eigenvalue.");
            }
            else if (shape == SteelForkMomentShape.EndMoments)
            {
                if (_parameters.ContainsKey("ForkLoadHeight")) throw new ArgumentException("End-moment loading has no transverse load height.");
                diagram = SteelForkMomentDiagram.EndMoments(first, last, source);
            }
            else
            {
                if (!physical.Cuts.Any(c => Math.Abs(c.Distance / input.Member.Length - .5) <= 1e-10))
                    throw new ArgumentException("Transverse-load preparation requires an actual midspan cut, not an inferred peak.");
                diagram = shape == SteelForkMomentShape.UniformTransverseLoad
                    ? SteelForkMomentDiagram.UniformTransverseLoad(maximum, Number("ForkLoadHeight"), source)
                    : SteelForkMomentDiagram.CentralPointLoad(maximum, Number("ForkLoadHeight"), source);
            }
            foreach (var cut in physical.Cuts)
            {
                if (shape == SteelForkMomentShape.GenericEnvelope) break; // No invented interpolation or derivative of generic FEM samples.
                double x = cut.Distance / input.Member.Length;
                if (!Close(cut.Actions.M1, diagram.MomentAt(x), Math.Abs(maximum)))
                    throw new NotSupportedException("Exported moments conflict with the declared full-span diagram.");
                // Positive-section-face equilibrium: V2=-dM1/dz. At the central
                // point load either real one-sided shear is admissible, never zero.
                double v = shape == SteelForkMomentShape.EndMoments ? (first - last) / input.Member.Length
                    : shape == SteelForkMomentShape.UniformTransverseLoad ? -4 * maximum * (1 - 2 * x) / input.Member.Length
                    : (x < .5 ? -2 : 2) * maximum / input.Member.Length;
                bool pointAtMidspan = shape == SteelForkMomentShape.CentralPointLoad && Math.Abs(x - .5) <= 1e-10;
                if (!Close(pointAtMidspan ? Math.Abs(cut.Actions.V2) : cut.Actions.V2, pointAtMidspan ? Math.Abs(v) : v,
                    4 * Math.Abs(maximum) / input.Member.Length))
                    throw new NotSupportedException("Exported shear conflicts with equilibrium of the declared moment diagram.");
            }
            var prepared = SteelMemberSchemePreparation.ForkBending(input.Section, physical.Segment == null ? input.Member : new SteelMemberInput(input.Member.Length),
                diagram, CriticalSource(physical));
            var basis = EnumValue<SteelCriticalMomentBasis>("McrBasis");
            bool ntc = input.Code.SectionCode.Standard == SectionResistanceStandard.Ntc2018;
            if (ntc && basis != SteelCriticalMomentBasis.UniformMoment)
                throw new ArgumentException("NTC preparation requires UniformMoment basis.");
            if (basis == SteelCriticalMomentBasis.UniformMoment && diagram.LoadHeight != 0)
                throw new NotSupportedException("Uniform-reference Mcr cannot discard a transverse load-height effect.");
            return Array.AsReadOnly(new[] { basis == SteelCriticalMomentBasis.UniformMoment ? prepared.UniformMoment : prepared.ForCode(input.Code.SectionCode.Standard) });
        }

        private double[] PrepareMomentFactors(PhysicalMemberCheckInput member, SteelVerificationInput input,
            SteelPhysicalMemberPreparation physical, out string source)
        {
            source = Required("MomentFactorSource");
            if (!_parameters.TryGetValue("MomentFactorPreparation", out var method)) return new[] { Number("Cm1"), Number("Cm2"), Number("CmLT") };
            if ((method != "LinearDiagrams" && method != "ConservativeEnvelope") || new[] { "Cm1", "Cm2", "CmLT" }.Any(_parameters.ContainsKey))
                throw new ArgumentException("Choose computed or explicit moment factors, not both.");
            // Cm1 belongs to the in-plane span: linear factors of a segment only when it is the whole span.
            if (physical.Segment == null || method == "LinearDiagrams" && physical.Segment.Count > 1) FullDeclaredSpan(member);
            if (method == "ConservativeEnvelope")
            {
                source += "; generic same-case FEM diagrams, conservative Cm1=Cm2=CmLT=1; exported extrema qualification: " + Required("MomentDiagramCoverageSource");
                return new[] { 1d, 1d, 1d };
            }
            // Caller explicitly qualifies nonsway behavior and the same relevant
            // braced span in MomentFactorSource/StabilityModelSource. No mesh inference.
            double Factor(Func<SteelActions, double> action)
            {
                double a = action(physical.Cuts.First().Actions), b = action(physical.Cuts.Last().Actions);
                double maximum = Math.Max(Math.Abs(a), Math.Abs(b));
                foreach (var cut in physical.Cuts)
                {
                    double x = cut.Distance / input.Member.Length;
                    if (!Close(action(cut.Actions), (1 - x) * a + x * b, maximum))
                        throw new NotSupportedException("Moment factors require actual linear diagrams over the relevant braced span.");
                }
                return maximum == 0 ? 1 : SteelForkMomentDiagram.EndMoments(a, b, "Validated physical span").EquivalentMomentFactor;
            }
            double first = Factor(a => a.M1), second = Factor(a => a.M2);
            source += input.Code.SectionCode.Standard == SectionResistanceStandard.Ntc2018
                ? "; Circolare2019 TableC4.2.VI, linear nonsway diagrams, same explicit full braced span"
                : "; EN1993-1-1:2005 TableB.3, linear nonsway diagrams, same explicit full braced span";
            return new[] { first, second, first };
        }
        private static bool Close(double value, double expected, double scale) => Math.Abs(value - expected) <= 1e-9 * Math.Max(1, scale);
    }
}
