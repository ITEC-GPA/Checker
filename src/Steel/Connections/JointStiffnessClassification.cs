using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    public enum JointStiffnessClass { NominallyPinned, SemiRigid, Rigid }

    internal sealed class JointStiffnessClassificationInput
    {
        public EndPlateRotationalStiffnessResult Stiffness { get; }
        public JointFrameMemberStiffness ConnectedBeam { get; }
        public JointFrameClassificationContext Frame { get; }
        public string SameDetailAndFrameDirectionSource { get; }
        public JointStiffnessClassificationInput(EndPlateRotationalStiffnessResult stiffness,
            JointFrameMemberStiffness connectedBeam, JointFrameClassificationContext frame,
            string sameDetailAndFrameDirectionSource)
        {
            Stiffness = stiffness ?? throw new ArgumentNullException(nameof(stiffness));
            ConnectedBeam = connectedBeam ?? throw new ArgumentNullException(nameof(connectedBeam));
            Frame = frame ?? throw new ArgumentNullException(nameof(frame));
            SameDetailAndFrameDirectionSource = sameDetailAndFrameDirectionSource;
        }
    }

    internal sealed class JointStiffnessClassificationResult
    {
        public JointStiffnessClassificationInput Input { get; }
        public JointStiffnessClass? NumericalZone { get; }
        public JointStiffnessClass? AppliedClass { get; }
        public bool IsClassificationComplete => AppliedClass.HasValue && Diagnostics.Count == 0;
        public bool CanQualifyWholeJoint => false;
        public bool StoreyRatioOverridesNumericalZone { get; }
        public double? BeamStiffness { get; }
        public double? PinnedLimit { get; }
        public double? RigidLimit { get; }
        public double? MinimumStoreyRatio { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public string Reference => "EN1993-1-8:2005 5.2.2, Figure5.4 including the kb=25 footnote";
        public string Scope => "Initial rotational-stiffness class for beam-column joints. A pinned zone alone does not establish force transfer or acceptance of load-induced rotations (5.2.2.2). No column bases or automatic global analysis model.";
        internal JointStiffnessClassificationResult(JointStiffnessClassificationInput input,
            JointStiffnessClass? zone, JointStiffnessClass? applied, bool overridden,
            double? beam, double? pinned, double? rigid, double? ratio,
            List<SectionResistanceValue> trace, List<string> diagnostics)
        {
            Input = input; NumericalZone = zone; AppliedClass = diagnostics.Count == 0 ? applied : null;
            StoreyRatioOverridesNumericalZone = overridden; BeamStiffness = beam;
            PinnedLimit = pinned; RigidLimit = rigid; MinimumStoreyRatio = ratio;
            Trace = Array.AsReadOnly(trace.ToArray()); Diagnostics = Array.AsReadOnly(diagnostics.Distinct().ToArray());
        }
    }

    internal static class JointStiffnessClassification
    {
        public static JointStiffnessClassificationResult Classify(JointStiffnessClassificationInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var issues = new List<string>(); var trace = new List<SectionResistanceValue>();
            JointStiffnessClass? zone = null, applied = null;
            double? beam = null, pinned = null, rigid = null, minimum = null; bool overridden = false;
            if (!p.Stiffness.IsSupported || !p.Stiffness.InitialStiffness.HasValue) issues.Add("SupportedInitialStiffnessResultRequired");
            if (string.IsNullOrWhiteSpace(p.SameDetailAndFrameDirectionSource)) issues.Add("SameDetailAndFrameDirectionEvidenceRequired");
            if (string.IsNullOrWhiteSpace(p.Frame.GlobalAnalysisSource)) issues.Add("FrameBracingAndGlobalAnalysisEvidenceRequired");
            bool braced = p.Frame.BracingDisplacementReduction >= .8;
            double kb = braced ? 8 : 25;
            // E is the same 210000MPa used by the typed component stiffness result.
            double rb = (210000 / p.ConnectedBeam.Length) * p.ConnectedBeam.SecondMoment;
            if (!PositiveFinite(rb) || !PositiveFinite(kb * rb) || !PositiveFinite(.5 * rb)) issues.Add("FrameLimitsOutsideNumericRange");
            else
            {
                beam = rb; pinned = .5 * rb; rigid = kb * rb;
                trace.Add(new SectionResistanceValue("BeamStiffness", rb, "Nmm/rad"));
                trace.Add(new SectionResistanceValue("PinnedLimit", pinned.Value, "Nmm/rad"));
                trace.Add(new SectionResistanceValue("RigidLimit", rigid.Value, "Nmm/rad"));
                trace.Add(new SectionResistanceValue("KbClassificationFactor", kb, "1"));
                if (p.Stiffness.InitialStiffness.HasValue)
                {
                    double s = p.Stiffness.InitialStiffness.Value;
                    trace.Add(new SectionResistanceValue("InitialJointStiffness", s, "Nmm/rad"));
                    zone = s <= pinned.Value ? JointStiffnessClass.NominallyPinned
                        : s >= rigid.Value ? JointStiffnessClass.Rigid : JointStiffnessClass.SemiRigid;
                }
            }
            if (!braced)
            {
                if (!p.Frame.IncludesEveryStorey || p.Frame.Storeys.Count == 0) issues.Add("EveryStoreyRequiredForOtherFrames");
                for (int i = 0; i < p.Frame.Storeys.Count; i++)
                {
                    var storey = p.Frame.Storeys[i];
                    if (string.IsNullOrWhiteSpace(storey.StoreySource)) issues.Add("Storey" + (i + 1) + ".CompleteMemberSetEvidenceRequired");
                    double kbMean = Mean(storey.BeamsAtTop), kcMean = Mean(storey.Columns);
                    double ratio = kbMean / kcMean;
                    if (!PositiveFinite(kbMean) || !PositiveFinite(kcMean) || !PositiveFinite(ratio))
                    { issues.Add("Storey" + (i + 1) + ".StiffnessOutsideNumericRange"); continue; }
                    trace.Add(new SectionResistanceValue("Storey" + (i + 1) + ".Kb", kbMean, "mm3"));
                    trace.Add(new SectionResistanceValue("Storey" + (i + 1) + ".Kc", kcMean, "mm3"));
                    trace.Add(new SectionResistanceValue("Storey" + (i + 1) + ".KbOverKc", ratio, "1"));
                    minimum = minimum.HasValue ? Math.Min(minimum.Value, ratio) : ratio;
                }
            }
            if (issues.Count == 0 && zone.HasValue)
            {
                // The Figure5.4 footnote belongs to the other-frame branch and precedes
                // BOTH zone-1 and zone-3 conclusions. Do not average ratios between storeys.
                overridden = !braced && minimum.HasValue && minimum.Value < .1;
                applied = overridden ? JointStiffnessClass.SemiRigid : zone;
            }
            return new JointStiffnessClassificationResult(p, zone, applied, overridden, beam, pinned, rigid, minimum, trace, issues);
        }
        private static double Mean(IReadOnlyList<JointFrameMemberStiffness> members)
        {
            // Divide before summing to avoid unnecessary overflow in a finite mean.
            return members.Sum(m => (m.SecondMoment / m.Length) / members.Count);
        }
        private static bool PositiveFinite(double x) => x > 0 && !double.IsInfinity(x);
    }
}
