using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    public sealed class StandardEndPlateResult
    {
        public StandardEndPlateInput Input { get; }
        /// <summary>Internal capacity preparation, never the actual N-M load case.</summary>
        public EndPlateBeamSectionAssessment BeamPreparation { get; }
        public EndPlateNmvResult Strength { get; }
        public EndPlateNmvStiffnessResult Stiffness { get; }
        public IReadOnlyList<ConnectionCheck> Checks { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public bool IsComplete => Diagnostics.Count == 0 && Strength != null && Strength.IsComplete;
        public ConnectionStatus Status => Checks.Any(c => !c.Passed) ? ConnectionStatus.Failed : IsComplete ? ConnectionStatus.Passed : ConnectionStatus.NotSupported;
        public bool CanQualifyWholeJoint => IsComplete;
        public bool CanQualifyWholeMember => false;
        public StandardConnectionCaseReport Report { get; }
        internal StandardEndPlateResult(StandardEndPlateInput input, EndPlateBeamSectionAssessment beam,
            EndPlateNmvResult strength, IEnumerable<string> issues)
        {
            Input = input; BeamPreparation = beam; Strength = strength;
            Stiffness = strength == null ? null : EndPlateNmvStiffness.Evaluate(strength);
            Checks = strength != null ? strength.Checks : beam.Checks;
            Diagnostics = Array.AsReadOnly(issues.Concat(beam.Diagnostics).Concat(strength?.Diagnostics ?? Array.Empty<string>()).Distinct().ToArray());
            Report = StandardConnectionCaseReport.Create(this);
        }
    }

    /// <summary>Single entry point for the bounded standard N-M-V model. Uniform
    /// vertical shear in all bolts, two symmetric bolt columns, same hardware;
    /// upper-flush CJP or one upper external row with paired fillets. No panel
    /// flexibility, member stability or plastic rotation is inferred.</summary>
    public static class StandardEndPlate
    {
        public static StandardEndPlateResult Verify(StandardEndPlateInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var b = p.Beam; var l = p.Layout; var c = p.Column; var a = b.Actions;
            var issues = new List<string>(); EndPlateNmvResult strength = null;
            // The only preparation is internal, retains the actual signed V2 and
            // exact immutable Model/material/code instances, and exports no fake case.
            var beam = EndPlateBeamSectionAdapter.Evaluate(new EndPlateBeamSectionInput(b.Code, b.Beam, b.Section,
                new SteelActions(0, 0, a.V2, 0, 0, 0), b.FlangeYieldStrength, b.FlangeUltimateStrength,
                b.HasNotchesOrOpenings, b.MaterialAtActualThicknessSource, b.GeometrySource,
                "Internal capacity preparation at actual V2; " + b.ActionCombinationSource, b.Ec3ShearEta, b.Ec3ShearEtaSource));
            Require(ReferenceEquals(p.Policy.Code, b.Code), "SameCodeInstanceRequired");
            Require(!string.IsNullOrWhiteSpace(b.ActionCombinationSource), "ActualCombinationSourceRequired");
            foreach (var action in new[] { a, p.ColumnUpper, p.ColumnLower })
                Require(action.V1 == 0 && action.M2 == 0 && action.T == 0, "OnlyActualConcomitantInPlaneNM1V2Supported");
            Require(l.RowY.Zip(l.RowY.Skip(1), (x,y) => y > x).All(x => x), "PhysicalRowsMustBeStrictlyOrdered");
            int external = l.RowY.Count(y => y < 0);
            Require(external <= 1 && external < l.RowY.Count, "AtMostOneUpperExternalRowAndAtLeastOneInternalRowRequired");
            Require(l.RowY.All(y => y > -l.TopProjection && y < b.Beam.Height + l.BottomProjection), "RowsMustLieWithinActualPlateEdges");
            Require(p.Welds.TopCjp != null ? external == 0 && l.TopProjection == 0 : external == 1,
                "ChooseTopFlushCjpOrOneRowExtendedPairedFillets");
            if (!beam.IsSectionAssessmentComplete || issues.Count != 0) return Result();
            try
            {
                var rowY = l.RowY.Skip(external).Select(y => y - b.Beam.FlangeThickness).ToArray();
                double clear = b.Beam.Height - 2 * b.Beam.FlangeThickness;
                double m = (l.Gauge - b.Beam.WebThickness) / 2 - .8 * l.WebWeldLeg;
                double e = (l.Plate.Width - l.Gauge) / 2;
                var upper = l.UpperAlpha; var lower = l.LowerAlpha;
                if (l.AlphaMethod == EndPlateAlphaMethod.SciP398Approximation)
                {
                    Require(upper == null && lower == null, "SciMethodMustNotSilentlyOverwriteSuppliedAlpha");
                    if (issues.Count != 0) return Result();
                    upper = TStubSciAlpha.Calculate(m, e, rowY.First() - .8 * l.UpperWeldLeg, l.Source);
                    if (l.Boundaries == EndPlateFlangeBoundaries.BothFlanges)
                        lower = TStubSciAlpha.Calculate(m, e, clear - rowY.Last() - .8 * l.LowerWeldLeg, l.Source);
                }
                Require(upper != null && (l.Boundaries != EndPlateFlangeBoundaries.BothFlanges || lower != null), "AllSelectedFlangeBoundariesRequireAlpha");
                Require(l.Boundaries != EndPlateFlangeBoundaries.UpperOnly || lower == null, "LowerAlphaRequiresBothFlangesModel");
                if (issues.Count != 0) return Result();
                var internalGeometry = new TStubInternalEndPlateGeometry(l.Plate.Width, c.Section.Width, l.Gauge,
                    b.Beam.WebThickness, l.WebWeldLeg, l.UpperWeldLeg, l.LowerWeldLeg, clear, rowY, upper, l.Source);
                if (l.Boundaries == EndPlateFlangeBoundaries.BothFlanges) internalGeometry = internalGeometry.WithOppositeFlange(lower);
                var extension = external == 0 ? null : new TStubExtendedEndPlateGeometry(l.Plate.Width, l.Gauge,
                    -l.RowY[0], l.UpperWeldLeg, l.TopProjection + l.RowY[0], l.Source);
                var joint = new EndPlateMomentComponentInput(p.Policy, c.Section, b.Beam, c.FlangeFy, l.Plate, l.Bolt,
                    internalGeometry, extension, c.FirstEndContinuation, c.LastEndContinuation, l.BottomProjection,
                    beam.DerivedResistance, 0, Math.Abs(a.V2), 0, 1, 0, ColumnWebPanelConfiguration.SingleSidedJoint,
                    "Standard single-sided joint; actual cuts retained separately", "Internal capacity preparation only",
                    c.RestraintSource, b.ActionCombinationSource, TStubMode1Method.DistributedContact,
                    Math.Min(p.Fasteners.Hardware.HeadAcrossPoints, p.Fasteners.Hardware.NutAcrossPoints), p.Fasteners.HoleServiceabilitySource);
                var baseline = EndPlateMomentWithBeamSection.Verify(joint, beam);
                if (!baseline.IsComplete) { issues.AddRange(baseline.Diagnostics); return Result(); }
                var bolts = new EndPlateBoltComponentInput(baseline.MomentComponents, p.Fasteners.Hardware,
                    l.RowY.Select((y,i) => new EndPlateBoltShearRow(i, y, Math.Abs(a.V2) / (2 * l.RowY.Count), Math.Abs(a.V2) / (2 * l.RowY.Count))),
                    c.FlangeFu, c.MaterialSource, p.Fasteners.UniformShearTransferSource);
                var w = p.Welds; var d = w.Detail; EndPlateConnectionAssessmentResult connection;
                if (w.TopCjp == null)
                    connection = EndPlateConnectionAssessment.Verify(bolts, new EndPlateWeldComponentInput(baseline.MomentComponents,
                        w.BeamGrade, w.PlateGrade, b.FlangeYieldStrength, b.FlangeUltimateStrength, b.MaterialAtActualThicknessSource, d));
                else
                    connection = EndPlateConnectionAssessment.Verify(bolts, new TopFlushCjpEndPlateWeldInput(baseline.MomentComponents,
                        w.TopCjp, b.Beam.Height + l.TopProjection + l.BottomProjection, l.TopProjection, w.BeamGrade, w.PlateGrade,
                        b.FlangeYieldStrength, b.FlangeUltimateStrength, b.MaterialAtActualThicknessSource, d.WebEnds, d.CompressionFlangeEnds,
                        d.CompressionTransfer, d.DetailSource, d.NormalTransferSource, d.ShearTransferSource, d.ConsumableCompatibilitySource,
                        d.LocalEccentricitySource, d.CompressionBearingSource));
                var hardware = Enumerable.Range(0, l.RowY.Count).Select(i => new EndPlateBoltElongationDetail(i,
                    p.Fasteners.ElongationLength, p.Fasteners.ElongationSource));
                var ct = p.Contact;
                var geometry = l.RowY.Count == 1
                    ? EndPlateNmvGeometry.PrepareSingleRow(baseline, connection, hardware, ct.UpperWidth, ct.UpperHeight, ct.LowerWidth, ct.LowerHeight, ct.Source, ct.InitialGap)
                    : EndPlateNmvGeometry.Prepare(baseline, connection, hardware, ct.UpperWidth, ct.UpperHeight, ct.LowerWidth, ct.LowerHeight, ct.Source, ct.InitialGap);
                strength = EndPlateNmv.Verify(new EndPlateNmvInput(geometry, a, p.ColumnUpper, p.ColumnLower,
                    b.ActionCombinationSource, p.ColumnReferenceSource, p.ColumnPanelModelledSeparately, p.ColumnPanelModelSource, p.AppliedGlobalTangent));
            }
            catch (NotSupportedException ex) { issues.Add("PreparationNotSupported:" + ex.Message); }
            catch (ArgumentException ex) { issues.Add("InconsistentPhysicalDetail:" + ex.Message); }
            return Result();
            void Require(bool valid, string diagnostic) { if (!valid) issues.Add(diagnostic); }
            StandardEndPlateResult Result() => new StandardEndPlateResult(p, beam, strength, issues);
        }
    }

    public sealed partial class StandardConnectionCaseReport
    {
        public static StandardConnectionCaseReport Create(StandardEndPlateResult r)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            if (r.Strength != null) return Create(r.Strength);
            var p = r.Input; var input = Factors(p.Beam.Code);
            Actions("BeamFace", p.Beam.Actions); Actions("Column.Upper", p.ColumnUpper); Actions("Column.Lower", p.ColumnLower);
            input.AddRange(new[] { V("Plate.Width", p.Layout.Plate.Width, "mm"), V("Plate.Thickness", p.Layout.Plate.Thickness, "mm"),
                V("Plate.TopProjection", p.Layout.TopProjection, "mm"), V("Plate.BottomProjection", p.Layout.BottomProjection, "mm"), V("Bolt.Gauge", p.Layout.Gauge, "mm") });
            for (int i = 0; i < p.Layout.RowY.Count; i++) input.Add(V("Row" + (i + 1) + ".Y", p.Layout.RowY[i], "mm"));
            return new StandardConnectionCaseReport("Steel.Connection.EndPlate.Application", p.Beam.Code,
                "Actual standard end-plate case; preparation did not qualify a complete N-M-V joint.", r.Status, false,
                input, r.BeamPreparation.Trace.Select(x => V("CapacityPreparation." + x.Name, x.Value, x.Unit)),
                r.Checks, r.Diagnostics, new[] { "No joint resistance or global stiffness is exported from incomplete preparation." },
                new[] { p.Beam.ActionCombinationSource, p.Beam.GeometrySource, p.Layout.Source, p.ColumnReferenceSource });
            void Actions(string id, SteelActions a)
            {
                input.AddRange(new[] { V(id + ".N", a.N, "N"), V(id + ".V1", a.V1, "N"), V(id + ".V2", a.V2, "N"),
                    V(id + ".M1", a.M1, "Nmm"), V(id + ".M2", a.M2, "Nmm"), V(id + ".T", a.T, "Nmm") });
            }
        }
    }
}
