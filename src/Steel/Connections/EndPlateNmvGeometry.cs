using System;
using System.Collections.Generic;
using System.Linq;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Prepared geometry and full-strength transfer envelopes at a retained
    /// |V|. The baseline has N=M=0 only to derive capacities and row stiffnesses;
    /// it is NOT the actual load case. Every physical bolt row must have a T-stub.
    /// The actual signed N-M-V and column cut actions enter EndPlateNmvInput.</summary>
    public sealed class EndPlateNmvGeometry
    {
        public EndPlateMomentWithBeamSectionResult Baseline { get; }
        public EndPlateConnectionAssessmentResult Connection { get; }
        public EndPlateInitialStiffnessFromMomentResult InitialComponents { get; }
        public EndPlateInPlaneResult InPlane { get; }
        public IReadOnlyList<EndPlateNmvSpringRow> Rows { get; }
        public IReadOnlyList<EndPlateNmvContactBand> Contacts { get; }
        public IReadOnlyList<ColumnWebEffectiveWidth> CompressionWidths { get; }
        public IReadOnlyList<double> ContactWidths { get; }
        public string ContactSource { get; }
        public double InitialContactGap { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public bool IsSupported => Diagnostics.Count == 0;
        public bool CanQualifyWholeJoint => false;

        private EndPlateNmvGeometry(EndPlateMomentWithBeamSectionResult baseline, EndPlateConnectionAssessmentResult connection,
            IEnumerable<EndPlateBoltElongationDetail> hardware, double upperWidth, double upperHeight,
            double lowerWidth, double lowerHeight, double gap, string contactSource)
        {
            Baseline = baseline; Connection = connection; ContactSource = contactSource; InitialContactGap = gap;
            var m = baseline.Input; var b = m.Beam; var c = m.Column;
            var issues = new List<string>(); var rows = new List<EndPlateNmvSpringRow>();
            issues.AddRange(baseline.Diagnostics.Select(x => "Baseline:" + x));
            issues.AddRange(connection.Diagnostics.Select(x => "Envelope:" + x));
            var contacts = new List<EndPlateNmvContactBand>(); var widths = new List<ColumnWebEffectiveWidth>();
            ContactWidths = Array.AsReadOnly(new[] { upperWidth, lowerWidth });
            Require(baseline.IsComplete && baseline.Status == ConnectionStatus.Passed, "CompleteModelBeamAndGeometryBaselineRequired");
            Require(m.Axial == 0 && m.Moment == 0, "CapacityPreparationRequiresZeroNAndMNotAnActualCase");
            Require(connection.IsSameMomentAssessment && ReferenceEquals(connection.MomentComponents, baseline.MomentComponents),
                "SameImmutableGeometryBaselineRequired");
            Require(connection.CoveredComponentsStatus != ConnectionStatus.NotSupported, "CompletePhysicalBoltAndWeldEnvelopesRequired");
            Require(m.PanelConfiguration == ColumnWebPanelConfiguration.SingleSidedJoint && m.Beta == 1, "SingleSidedStandardGeometryRequired");
            Require(gap == 0 && !string.IsNullOrWhiteSpace(contactSource), "InitiallyClosedActualContactOnBothFlangesRequired");
            Require(upperWidth <= Math.Min(b.Width, Math.Min(m.EndPlate.Width, c.Width)) && lowerWidth <= Math.Min(b.Width, Math.Min(m.EndPlate.Width, c.Width))
                && upperHeight <= b.FlangeThickness && lowerHeight <= b.FlangeThickness, "ContactBandsMustFitBothPhysicalFlanges");
            Require(connection.Bolts.BoltAssessment.Input.Rows.Count >= 2, "AtLeastTwoPhysicalTensionRowsRequiredForNmv");
            Require(connection.Bolts.BoltAssessment.Input.Rows.All(r => r.MomentRowIndex.HasValue),
                "EveryPhysicalRowMustBeQualifiedInTensionNoImplicitShearOnlyRows");
            if (connection.FilletWelds != null)
                Require(connection.FilletWelds.WeldAssessment.Input.Detail.CompressionTransfer == EndPlateCompressionTransfer.FullStrengthWelds,
                    "BothFlangesNeedFullStrengthWeldsForAxialTensionAndMomentReversal");
            if (connection.TopFlushCjpWelds != null)
                Require(connection.TopFlushCjpWelds.WeldAssessment.Input.CompressionTransfer == EndPlateCompressionTransfer.FullStrengthWelds,
                    "LowerFlangeNeedsFullStrengthWeldsForAxialTensionAndMomentReversal");
            // Uniform-compression class <=3 bounds every admitted N-M stress pattern.
            // The N=0 adapter's bending-only web classification must not be reused.
            Compact(b, b.Fy, "Beam"); Compact(c, Math.Min(c.Fy, m.ColumnFlangeFy), "Column");
            InPlane = EndPlateInPlaneChecks.Verify(connection.Bolts);
            if (!InPlane.IsComplete) issues.AddRange(InPlane.Diagnostics.Select(x => "InPlane:" + x));
            if (issues.Count == 0)
            {
                try
                {
                    InitialComponents = EndPlateInitialStiffnessFromMoment.Calculate(
                        EndPlateInitialStiffnessFromMoment.CreateInputFromGeometry(baseline, hardware));
                    if (!InitialComponents.IsSupported) issues.AddRange(InitialComponents.Diagnostics);
                }
                catch (NotSupportedException ex) { issues.Add("InitialGeometry:" + ex.Message); }
                if (InitialComponents != null && InitialComponents.IsSupported)
                {
                    var physical = connection.Bolts.BoltAssessment.Input.Rows;
                    var stiffness = InitialComponents.InitialModel.Input.Rows;
                    Require(physical.Count == stiffness.Count, "AllPhysicalRowsMustHaveTheirOwnStiffness");
                    for (int i = 0; i < stiffness.Count && physical.Count == stiffness.Count; i++)
                    {
                        EndPlateStiffnessArithmetic.Row(c, stiffness[i], out _, out _, out _, out _, out _, out double k);
                        rows.Add(new EndPlateNmvSpringRow("Row" + (i + 1), b.Height / 2 - physical[i].Y, 210000 * k));
                    }
                    double[] heights = { upperHeight, lowerHeight };
                    double[] centres = { (b.Height - b.FlangeThickness) / 2, -(b.Height - b.FlangeThickness) / 2 };
                    for (int i = 0; i < 2; i++)
                    {
                        // Conservative Eq6.11 dispersion: no favourable fillet-leg or
                        // plate-projection addition. The actual contact height replaces tf.
                        var width = new ColumnWebEffectiveWidth(heights[i] + 5 * (c.FlangeThickness + c.RootRadius) + m.EndPlate.Thickness,
                            "EN1993-1-8 6.2.6.2(6.11), lower-bound spread; no weld-leg or projection credit; " + contactSource);
                        widths.Add(width);
                        double k = 210000 * .7 * width.Width * c.WebThickness / c.ClearWeb;
                        contacts.Add(new EndPlateNmvContactBand(i == 0 ? "UpperContact" : "LowerContact",
                            centres[i] - heights[i] / 2, centres[i] + heights[i] / 2, k));
                        double y = b.Height / 2 - centres[i];
                        double top = physical.First().Y - m.ColumnFirstEndContinuation;
                        double bottom = physical.Last().Y + m.ColumnLastEndContinuation;
                        Require(y - top >= width.Width / 2 && bottom - y >= width.Width / 2, "ColumnEndTruncatesCompressionSpread");
                    }
                    Require(centres[0] - centres[1] >= (widths[0].Width + widths[1].Width) / 2,
                        "OverlappingCompressionSpreadsRequireAnotherModel");
                }
            }
            Rows = Array.AsReadOnly(rows.ToArray()); Contacts = Array.AsReadOnly(contacts.ToArray());
            CompressionWidths = Array.AsReadOnly(widths.ToArray()); Diagnostics = Array.AsReadOnly(issues.Distinct().ToArray());
            void Require(bool ok, string diagnostic) { if (!ok) issues.Add(diagnostic); }
            void Compact(ConnectionRolledISection s, double fy, string name)
            {
                double eps = Math.Sqrt(235 / fy);
                Require((s.Width - s.WebThickness - 2 * s.RootRadius) / (2 * s.FlangeThickness) <= 14 * eps
                    && s.ClearWeb / s.WebThickness <= 42 * eps, name + "MustBeClassThreeOrBetterInUniformCompression");
            }
        }

        public static EndPlateNmvGeometry Prepare(EndPlateMomentWithBeamSectionResult baseline,
            EndPlateConnectionAssessmentResult connection, IEnumerable<EndPlateBoltElongationDetail> hardware,
            double upperContactWidth, double upperContactHeight, double lowerContactWidth, double lowerContactHeight,
            string contactExecutionSource, double initialContactGap = 0)
        {
            if (baseline == null) throw new ArgumentNullException(nameof(baseline));
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (hardware == null) throw new ArgumentNullException(nameof(hardware));
            foreach (double v in new[] { upperContactWidth, upperContactHeight, lowerContactWidth, lowerContactHeight }) ConnectionValidation.Positive(v, "contact dimensions");
            ConnectionValidation.Nonnegative(initialContactGap, nameof(initialContactGap));
            return new EndPlateNmvGeometry(baseline, connection, hardware, upperContactWidth, upperContactHeight,
                lowerContactWidth, lowerContactHeight, initialContactGap, contactExecutionSource);
        }
    }
}
