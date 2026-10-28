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
        /// <summary>Prepares the physical joint of <paramref name="template"/> once; its actions and
        /// combination source are not part of the preparation.</summary>
        public static PreparedStandardEndPlate Prepare(StandardEndPlateInput template) => new PreparedStandardEndPlate(template);

        /// <summary>One actual concomitant case on a prepared joint; identical to Verify of the same input.</summary>
        public static StandardEndPlateResult Evaluate(PreparedStandardEndPlate prepared, SteelActions beamFaceActions,
            SteelActions columnUpper, SteelActions columnLower, string actionCombinationSource)
        {
            if (prepared == null) throw new ArgumentNullException(nameof(prepared));
            return Verify(prepared.CreateCase(beamFaceActions, columnUpper, columnLower, actionCombinationSource));
        }

        public static StandardEndPlateResult Verify(StandardEndPlateInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var prepared = p.Prepared;
            var b = p.Beam; var l = p.Layout; var c = p.Column; var a = b.Actions;
            var issues = new List<string>(); EndPlateNmvResult strength = null;
            // The only preparation is internal, retains the actual signed V2 and
            // exact immutable Model/material/code instances, and exports no fake case.
            var beam = EndPlateBeamSectionAdapter.Evaluate(new EndPlateBeamSectionInput(b.Code, b.Beam, b.Section,
                new SteelActions(0, 0, a.V2, 0, 0, 0), b.FlangeYieldStrength, b.FlangeUltimateStrength,
                b.HasNotchesOrOpenings, b.MaterialAtActualThicknessSource, b.GeometrySource,
                "Internal capacity preparation at actual V2; " + b.ActionCombinationSource, b.Ec3ShearEta, b.Ec3ShearEtaSource));
            Require(prepared.SameCodeInstance, "SameCodeInstanceRequired");
            Require(!string.IsNullOrWhiteSpace(b.ActionCombinationSource), "ActualCombinationSourceRequired");
            foreach (var action in new[] { a, p.ColumnUpper, p.ColumnLower })
                Require(action.V1 == 0 && action.M2 == 0 && action.T == 0, "OnlyActualConcomitantInPlaneNM1V2Supported");
            foreach (var issue in prepared.LayoutIssues) issues.Add(issue);
            int external = prepared.External;
            if (!beam.IsSectionAssessmentComplete || issues.Count != 0) return Result();
            try
            {
                // Plate, alpha and T-stub geometry are action independent: prepared once per joint.
                var stage = prepared.Geometry;
                stage.Rethrow();
                issues.AddRange(stage.Issues);
                if (issues.Count != 0) return Result();
                var internalGeometry = stage.Internal; var extension = stage.Extension;
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
                    b.ActionCombinationSource, p.ColumnReferenceSource, p.ColumnPanelModelledSeparately, p.ColumnPanelModelSource, p.AppliedGlobalTangent)
                    { PureStates = prepared.PureStates });
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

    /// <summary>One physical standard end-plate joint prepared once: plate, alpha and T-stub geometry
    /// and its validation are reused by every case, and pure-load equilibrium states are shared when
    /// the spring rows and bands coincide. Shear- and source-dependent stages (beam preparation at the
    /// actual V2, components, bolts, welds) stay per case, so results equal Verify bit for bit.</summary>
    public sealed class PreparedStandardEndPlate
    {
        private readonly StandardEndPlateInput template;
        internal PreparedStandardEndPlate(StandardEndPlateInput template)
        {
            this.template = template ?? throw new ArgumentNullException(nameof(template));
            _ = template.Prepared;
        }
        /// <summary>The prepared physical joint (its own actions are only a placeholder).</summary>
        public StandardEndPlateInput Detail => template;
        /// <summary>One actual concomitant case: beam-face N-M1-V2, the two column cuts and their combination source.</summary>
        public StandardEndPlateInput CreateCase(SteelActions beamFaceActions, SteelActions columnUpper, SteelActions columnLower, string actionCombinationSource)
        {
            var b = template.Beam;
            var beam = new EndPlateBeamSectionInput(b.Code, b.Beam, b.Section, beamFaceActions ?? throw new ArgumentNullException(nameof(beamFaceActions)),
                b.FlangeYieldStrength, b.FlangeUltimateStrength, b.HasNotchesOrOpenings, b.MaterialAtActualThicknessSource, b.GeometrySource,
                actionCombinationSource, b.Ec3ShearEta, b.Ec3ShearEtaSource);
            return new StandardEndPlateInput(template.Policy, beam, template.Column, template.Layout, template.Fasteners, template.Welds,
                template.Contact, columnUpper, columnLower, template.ColumnReferenceSource, template.ColumnPanelModelledSeparately,
                template.ColumnPanelModelSource, template.AppliedGlobalTangent) { Prepared = template.Prepared };
        }
        /// <summary>True when <paramref name="input"/> is a case of this prepared joint (reference identity).</summary>
        public bool Owns(StandardEndPlateInput input) => input != null && ReferenceEquals(input.Prepared, template.Prepared);
    }

    /// <summary>Action-independent part of StandardEndPlate.Verify for one physical joint.</summary>
    internal sealed class StandardEndPlatePreparation
    {
        internal sealed class GeometryStage
        {
            internal string[] Issues = Array.Empty<string>();
            internal TStubInternalEndPlateGeometry Internal;
            internal TStubExtendedEndPlateGeometry Extension;
            internal System.Runtime.ExceptionServices.ExceptionDispatchInfo Error;
            /// <summary>Raises the preparation exception again inside the caller's handlers, as the direct calculation did.</summary>
            internal void Rethrow() => Error?.Throw();
        }
        internal readonly bool SameCodeInstance;
        internal readonly string[] LayoutIssues;
        internal readonly int External;
        internal readonly EndPlateNmvPureStates PureStates = new EndPlateNmvPureStates();
        private readonly Lazy<GeometryStage> geometry;
        /// <summary>Built on first use, when the beam preparation and the case requirements passed.</summary>
        internal GeometryStage Geometry => geometry.Value;

        internal StandardEndPlatePreparation(StandardEndPlateInput p)
        {
            var b = p.Beam; var l = p.Layout; var issues = new List<string>();
            SameCodeInstance = ReferenceEquals(p.Policy.Code, b.Code);
            if (!l.RowY.Zip(l.RowY.Skip(1), (x, y) => y > x).All(x => x)) issues.Add("PhysicalRowsMustBeStrictlyOrdered");
            int external = l.RowY.Count(y => y < 0);
            if (!(external <= 1 && external < l.RowY.Count)) issues.Add("AtMostOneUpperExternalRowAndAtLeastOneInternalRowRequired");
            if (!l.RowY.All(y => y > -l.TopProjection && y < b.Beam.Height + l.BottomProjection)) issues.Add("RowsMustLieWithinActualPlateEdges");
            if (!(p.Welds.TopCjp != null ? external == 0 && l.TopProjection == 0 : external == 1)) issues.Add("ChooseTopFlushCjpOrOneRowExtendedPairedFillets");
            LayoutIssues = issues.ToArray(); External = external;
            geometry = new Lazy<GeometryStage>(() => Build(p, external), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
        }

        private static GeometryStage Build(StandardEndPlateInput p, int external)
        {
            var b = p.Beam; var l = p.Layout; var c = p.Column; var stage = new GeometryStage(); var issues = new List<string>();
            try
            {
                var rowY = l.RowY.Skip(external).Select(y => y - b.Beam.FlangeThickness).ToArray();
                double clear = b.Beam.Height - 2 * b.Beam.FlangeThickness;
                double m = (l.Gauge - b.Beam.WebThickness) / 2 - .8 * l.WebWeldLeg;
                double e = (l.Plate.Width - l.Gauge) / 2;
                var upper = l.UpperAlpha; var lower = l.LowerAlpha;
                if (l.AlphaMethod == EndPlateAlphaMethod.SciP398Approximation)
                {
                    if (!(upper == null && lower == null)) { stage.Issues = new[] { "SciMethodMustNotSilentlyOverwriteSuppliedAlpha" }; return stage; }
                    upper = TStubSciAlpha.Calculate(m, e, rowY.First() - .8 * l.UpperWeldLeg, l.Source);
                    if (l.Boundaries == EndPlateFlangeBoundaries.BothFlanges)
                        lower = TStubSciAlpha.Calculate(m, e, clear - rowY.Last() - .8 * l.LowerWeldLeg, l.Source);
                }
                if (!(upper != null && (l.Boundaries != EndPlateFlangeBoundaries.BothFlanges || lower != null))) issues.Add("AllSelectedFlangeBoundariesRequireAlpha");
                if (!(l.Boundaries != EndPlateFlangeBoundaries.UpperOnly || lower == null)) issues.Add("LowerAlphaRequiresBothFlangesModel");
                stage.Issues = issues.ToArray();
                if (issues.Count != 0) return stage;
                var internalGeometry = new TStubInternalEndPlateGeometry(l.Plate.Width, c.Section.Width, l.Gauge,
                    b.Beam.WebThickness, l.WebWeldLeg, l.UpperWeldLeg, l.LowerWeldLeg, clear, rowY, upper, l.Source);
                if (l.Boundaries == EndPlateFlangeBoundaries.BothFlanges) internalGeometry = internalGeometry.WithOppositeFlange(lower);
                stage.Internal = internalGeometry;
                stage.Extension = external == 0 ? null : new TStubExtendedEndPlateGeometry(l.Plate.Width, l.Gauge,
                    -l.RowY[0], l.UpperWeldLeg, l.TopProjection + l.RowY[0], l.Source);
            }
            catch (Exception ex) { stage.Error = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex); }
            return stage;
        }
    }}
