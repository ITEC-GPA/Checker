using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Qualified beam section moment resistance, Nmm, already reduced for the
    /// concomitant shear and section class. This component does not infer section class.</summary>
    public sealed class EndPlateBeamMomentResistance
    {
        public ConnectionDesignCode Code { get; }
        public ConnectionRolledISection Beam { get; }
        public double Value { get; }
        public double ConcomitantShear { get; }
        public string Source { get; }
        public EndPlateBeamMomentResistance(ConnectionDesignCode code, ConnectionRolledISection beam,
            double value, double concomitantShear, string source)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Beam = beam ?? throw new ArgumentNullException(nameof(beam));
            ConnectionValidation.Positive(value, nameof(value)); ConnectionValidation.Nonnegative(concomitantShear, nameof(concomitantShear));
            Value = value; ConcomitantShear = concomitantShear; Source = source;
        }
    }

    /// <summary>Unstiffened I/H beam/column, rectangular welded flush or one-row extended
    /// end plate, identical two-bolt tension rows. Forces N, lengths mm, moments Nmm.
    /// Positive Moment tensions the named upper rows. Axial is retained but must be zero
    /// in this first field. Shear is retained explicitly as an unqualified joint action.</summary>
    public sealed class EndPlateMomentComponentInput
    {
        public ColumnWebPolicy Policy { get; }
        public ConnectionRolledISection Column { get; }
        public ConnectionRolledISection Beam { get; }
        public double ColumnFlangeFy { get; }
        public ConnectionPlate EndPlate { get; }
        public ConnectionBolt Bolt { get; }
        public TStubInternalEndPlateGeometry InternalGeometry { get; }
        public TStubExtendedEndPlateGeometry ExtensionGeometry { get; }
        public double ColumnFirstEndContinuation { get; }
        public double ColumnLastEndContinuation { get; }
        public double CompressionEndPlateProjection { get; }
        public EndPlateBeamMomentResistance BeamMomentResistance { get; }
        public double Moment { get; }
        public double Shear { get; }
        public double Axial { get; }
        public double Beta { get; }
        public double ColumnLongitudinalCompressionStress { get; }
        public ColumnWebPanelConfiguration PanelConfiguration { get; }
        public string TransformationSource { get; }
        public string ColumnStressSource { get; }
        public string ColumnSwayRestraintSource { get; }
        public string ActionCombinationSource { get; }
        public string HoleClearanceServiceabilitySource { get; }
        public TStubMode1Method TStubMethod { get; }
        public double ContactWidth { get; }
        public EndPlateMomentComponentInput(ColumnWebPolicy policy, ConnectionRolledISection column,
            ConnectionRolledISection beam, double columnFlangeFy, ConnectionPlate endPlate, ConnectionBolt bolt,
            TStubInternalEndPlateGeometry internalGeometry, TStubExtendedEndPlateGeometry extensionGeometry,
            double columnFirstEndContinuation, double columnLastEndContinuation, double compressionEndPlateProjection,
            EndPlateBeamMomentResistance beamMomentResistance, double moment, double shear, double axial,
            double beta, double columnLongitudinalCompressionStress, ColumnWebPanelConfiguration panelConfiguration,
            string transformationSource, string columnStressSource, string columnSwayRestraintSource,
            string actionCombinationSource, TStubMode1Method tStubMethod = TStubMode1Method.PointForce,
            double contactWidth = 0, string holeClearanceServiceabilitySource = null)
        {
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
            Column = column ?? throw new ArgumentNullException(nameof(column));
            Beam = beam ?? throw new ArgumentNullException(nameof(beam));
            EndPlate = endPlate ?? throw new ArgumentNullException(nameof(endPlate));
            Bolt = bolt ?? throw new ArgumentNullException(nameof(bolt));
            InternalGeometry = internalGeometry ?? throw new ArgumentNullException(nameof(internalGeometry));
            BeamMomentResistance = beamMomentResistance ?? throw new ArgumentNullException(nameof(beamMomentResistance));
            foreach (double v in new[] { columnFlangeFy, columnFirstEndContinuation, columnLastEndContinuation })
                ConnectionValidation.Positive(v, nameof(v));
            foreach (double v in new[] { compressionEndPlateProjection, moment, shear, beta, columnLongitudinalCompressionStress, contactWidth })
                ConnectionValidation.Nonnegative(v, nameof(v));
            ConnectionNumbers.Finite(axial, nameof(axial));
            if (beta > 2) throw new ArgumentOutOfRangeException(nameof(beta));
            if (!Enum.IsDefined(typeof(ColumnWebPanelConfiguration), panelConfiguration)) throw new ArgumentOutOfRangeException(nameof(panelConfiguration));
            if (!Enum.IsDefined(typeof(TStubMode1Method), tStubMethod)) throw new ArgumentOutOfRangeException(nameof(tStubMethod));
            ColumnFlangeFy = columnFlangeFy; ExtensionGeometry = extensionGeometry;
            ColumnFirstEndContinuation = columnFirstEndContinuation; ColumnLastEndContinuation = columnLastEndContinuation;
            CompressionEndPlateProjection = compressionEndPlateProjection;
            Moment = moment; Shear = shear; Axial = axial; Beta = beta;
            ColumnLongitudinalCompressionStress = columnLongitudinalCompressionStress; PanelConfiguration = panelConfiguration;
            TransformationSource = transformationSource; ColumnStressSource = columnStressSource;
            ColumnSwayRestraintSource = columnSwayRestraintSource; ActionCombinationSource = actionCombinationSource;
            TStubMethod = tStubMethod; ContactWidth = contactWidth; HoleClearanceServiceabilitySource = holeClearanceServiceabilitySource;
        }
    }

    /// <summary>Typed kind of a row capacity constraint; callers use it instead of the identifier text.</summary>
    internal enum EndPlateRowComponentKind { Global, TStub, BeamWeb, ColumnWeb }

    public sealed class EndPlateRowCapacityConstraint
    {
        public string Id { get; }
        public int FirstRow { get; }
        public int RowCount { get; }
        public double Resistance { get; }
        public string Reference { get; }
        internal EndPlateRowComponentKind Kind { get; }
        internal EndPlateRowCapacityConstraint(string id, int first, int count, double resistance, string reference,
            EndPlateRowComponentKind kind = EndPlateRowComponentKind.Global)
        { Id = id; FirstRow = first; RowCount = count; Resistance = resistance; Reference = reference; Kind = kind; }
    }

    public sealed class EndPlateEffectiveRow
    {
        public int Row { get; }
        public double LeverArm { get; }
        public double Resistance { get; }
        public string LimitingConstraint { get; }
        internal EndPlateEffectiveRow(int row, double lever, double resistance, string limit)
        { Row = row; LeverArm = lever; Resistance = resistance; LimitingConstraint = limit; }
    }

    public sealed class EndPlateMomentComponentResult
    {
        public ConnectionVerification<EndPlateMomentComponentInput> ComponentAssessment { get; }
        public bool CanQualifyWholeJoint => false;
        public double? MomentResistance => ComponentAssessment.IsComplete
            ? (double?)EffectiveRows.Sum(r => r.LeverArm * r.Resistance) : null;
        public IReadOnlyList<EndPlateEffectiveRow> EffectiveRows { get; }
        public IReadOnlyList<EndPlateRowCapacityConstraint> Constraints { get; }
        public IReadOnlyList<string> PendingChecks { get; }
        internal EndPlateMomentComponentResult(ConnectionVerification<EndPlateMomentComponentInput> assessment,
            IEnumerable<EndPlateEffectiveRow> rows, IEnumerable<EndPlateRowCapacityConstraint> constraints)
        {
            ComponentAssessment = assessment;
            EffectiveRows = Array.AsReadOnly(assessment.IsComplete ? rows.ToArray() : Array.Empty<EndPlateEffectiveRow>());
            Constraints = Array.AsReadOnly(assessment.IsComplete ? constraints.ToArray() : Array.Empty<EndPlateRowCapacityConstraint>());
            PendingChecks = Array.AsReadOnly(new[] {
                "Vertical shear, bolt shear/tension interaction and bearing for the retained concomitant Shear action",
                "Bolt-head/nut/washer bearing and plate/column-flange punching",
                "Beam-to-end-plate weld resistance and force introduction, including external-row flange transfer",
                "Member resistance and stability away from the local components; supplied beam Mc,Rd qualification",
                "Joint rotational stiffness, rotation capacity and compatibility with the global model" });
        }
    }

    internal static class EndPlateMomentComponents
    {
        public static EndPlateMomentComponentResult Verify(EndPlateMomentComponentInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var issues = new List<string>(); var trace = new List<SectionResistanceValue>(); var checks = new List<ConnectionCheck>();
            var constraints = new List<EndPlateRowCapacityConstraint>(); var rows = new List<EndPlateEffectiveRow>();
            var g = p.InternalGeometry; var col = p.Column; var beam = p.Beam; var code = p.Policy.Code;
            int offset = p.ExtensionGeometry == null ? 0 : 1;
            string reference = "EN1993-1-8:2005 6.2.7.2(1-9), (6.25-6.26)"
                + (code.IsNtc ? "; supplementary EC component model with NTC2018 factors, not a literal national equation" : "");
            if (p.Axial != 0) issues.Add("NonzeroAxialRequiresDifferentJointEquilibriumModel");
            if (string.IsNullOrWhiteSpace(p.BeamMomentResistance.Source)) issues.Add("QualifiedBeamMomentResistanceSourceRequired");
            if (!ReferenceEquals(p.BeamMomentResistance.Code, code) || !ReferenceEquals(p.BeamMomentResistance.Beam, beam)
                || !Equal(p.BeamMomentResistance.ConcomitantShear, p.Shear))
                issues.Add("BeamMomentResistanceMustUseSameCodeBeamAndConcomitantShear");
            if (!Equal(g.PlateWidth, p.EndPlate.Width) || !Equal(g.OpposingFlangeWidth, col.Width)
                || !Equal(g.BeamWebThickness, beam.WebThickness) || !Equal(g.BeamClearDepth, beam.Height - 2 * beam.FlangeThickness))
                issues.Add("InternalGeometryDoesNotMatchBeamColumnAndPlateSnapshots");
            // SCI P398 STEP1, printed p11: the flange must be wide enough to
            // constrain the supplementary opposite-boundary yield-line patterns.
            if (g.LastRowAlpha != null && beam.Width < 1.33 * g.BoltGauge)
                issues.Add("BeamFlangeTooNarrowForSciBothFlangePatterns");
            if (beam.Fy > 460 || p.ColumnFlangeFy > 460 || p.EndPlate.Fy > 460) issues.Add("CarbonSteelUpToS460Only");
            if (offset != 0 && (!Equal(p.ExtensionGeometry.PlateWidth, p.EndPlate.Width)
                || !Equal(p.ExtensionGeometry.BoltGauge, g.BoltGauge)
                || !Equal(p.ExtensionGeometry.FlangeWeldLeg, g.TensionFlangeWeldLeg)))
                issues.Add("ExtensionDoesNotMatchInternalPlateAndWeldGeometry");
            if (p.PanelConfiguration == ColumnWebPanelConfiguration.SingleSidedJoint && p.Beta != 1)
                issues.Add("SingleSidedJointRequiresBetaOneInThisField");
            if (p.CompressionEndPlateProjection < g.CompressionFlangeWeldLeg)
                issues.Add("EndPlateMustContainCompressionFlangeWeld");
            var coordinates = new List<double>();
            if (offset != 0) coordinates.Add(-p.ExtensionGeometry.FlangeToRow);
            coordinates.AddRange(g.OrderedInternalRows.Select(y => y + beam.FlangeThickness));
            if (coordinates.Count > 20) issues.Add("MaximumTwentyTensionRows");
            double compressionCentre = beam.Height - beam.FlangeThickness / 2;
            var levers = coordinates.Select(y => compressionCentre - y).ToArray();
            if (levers.Any(h => !(h > 0) || !Finite(h))) issues.Add("TensionRowsMustRemainAboveCompressionCentre");
            Value("MomentDemand", p.Moment, "Nmm"); Value("RetainedShearDemand", p.Shear, "N"); Value("AxialDemand", Math.Abs(p.Axial), "N");
            if (issues.Count != 0) return Result();

            var columnGeometry = TStubEffectiveGeometry.UnstiffenedColumnFlange(new TStubUnstiffenedColumnGeometry(
                col.Width, col.WebThickness, col.RootRadius, p.EndPlate.Width, g.BoltGauge, coordinates,
                p.ColumnFirstEndContinuation, p.ColumnLastEndContinuation, g.DetailSource));
            var internalGeometry = TStubInternalEndPlateLengths.Calculate(g);
            var extensionGeometry = offset == 0 ? null : TStubEffectiveGeometry.ExtendedEndPlateOuterRow(p.ExtensionGeometry);
            Geometry("Column", columnGeometry); Geometry("InternalPlate", internalGeometry);
            if (extensionGeometry != null) Geometry("Extension", extensionGeometry);
            if (issues.Count != 0) return Result();
            TStubBoltRowsInput columnRows, internalRows, externalRows = null;
            try
            {
                columnRows = Build(columnGeometry, col.FlangeThickness, p.ColumnFlangeFy);
                internalRows = Build(internalGeometry, p.EndPlate.Thickness, p.EndPlate.Fy);
                if (extensionGeometry != null) externalRows = Build(extensionGeometry, p.EndPlate.Thickness, p.EndPlate.Fy);
            }
            catch (ArgumentException ex) { issues.Add("BoltGeometry:" + ex.Message); return Result(); }
            TStubFamily("Column", columnRows, 0, true);
            TStubFamily("InternalPlate", internalRows, offset, false);
            if (externalRows != null) TStubFamily("ExternalPlate", externalRows, 0, false, true);
            if (issues.Count != 0) return Result();

            var compressionWidth = ColumnWebEffectiveWidth.FromBoltedEndPlate(col, beam.FlangeThickness,
                g.CompressionFlangeWeldLeg, p.EndPlate.Thickness, p.CompressionEndPlateProjection, g.DetailSource);
            double compressionToTop = compressionCentre - coordinates.First() + p.ColumnFirstEndContinuation;
            double compressionToBottom = coordinates.Last() + p.ColumnLastEndContinuation - compressionCentre;
            // First conservative field requires the entire loaded zone within physical column ends.
            if (Math.Min(compressionToTop, compressionToBottom) < compressionWidth.Width / 2)
                issues.Add("CompressionSpreadTruncatedByColumnFreeEnd");
            var compression = ColumnWebComponents.VerifyTransverseCompression(new ColumnWebCompressionInput(p.Policy, col,
                compressionWidth, 0, p.Beta, p.ColumnLongitudinalCompressionStress, p.TransformationSource,
                p.ColumnStressSource, p.ColumnSwayRestraintSource, p.ActionCombinationSource));
            Include("ColumnCompression", compression.Trace, compression.Diagnostics);
            var panel = ColumnWebComponents.VerifyPanelShear(new ColumnWebPanelInput(p.Policy, col, 0,
                p.PanelConfiguration, p.TransformationSource));
            Include("ColumnPanel", panel.Trace, panel.Diagnostics);
            double beamCompression = p.BeamMomentResistance.Value / (beam.Height - beam.FlangeThickness);
            Value("BeamCompressionResistance", beamCompression, "N");
            if (issues.Count != 0) return Result();
            Add("ColumnCompression", 0, levers.Length, compression.Checks.Min(c => c.Resistance), "EN6.2.6.2");
            Add("BeamCompression", 0, levers.Length, beamCompression, "EN6.2.6.7(6.21); " + p.BeamMomentResistance.Source);
            if (p.Beta > 0) Add("ColumnPanelTransformation", 0, levers.Length,
                panel.Checks.Min(c => c.Resistance) / p.Beta, "EN6.2.7.2(7); " + p.TransformationSource);
            var bolt = BoltStrength.Verify(new BoltStrengthInput(code, p.Bolt, 0, 0,
                holeClearanceServiceabilitySource: p.HoleClearanceServiceabilitySource), out var boltValues);
            Include("Bolt", bolt.Trace, bolt.Diagnostics);
            if (issues.Count != 0) return Result();
            double threshold = 1.9 * boltValues.TensionResistance;
            Value("EN626ElasticRowThreshold", threshold, "N");
            int? elasticReference = null;
            for (int i = 0; i < levers.Length; i++)
            {
                double allowed = double.PositiveInfinity; string limit = null;
                // At each step all closer-row resistances are zero, EN6.2.7.2(4).
                // Every relevant contiguous group is enforced, including those containing later rows.
                foreach (var c in constraints.Where(c => c.FirstRow <= i && i < c.FirstRow + c.RowCount))
                {
                    double used = rows.Where(r => r.Row >= c.FirstRow && r.Row < c.FirstRow + c.RowCount).Sum(r => r.Resistance);
                    double available = Math.Max(0, c.Resistance - used);
                    if (available < allowed) { allowed = available; limit = c.Id; }
                }
                if (elasticReference.HasValue)
                {
                    var previous = rows[elasticReference.Value];
                    double elastic = previous.Resistance * levers[i] / previous.LeverArm;
                    if (elastic < allowed) { allowed = elastic; limit = "EN6.2.7.2(9).Row" + (elasticReference.Value + 1); }
                }
                if (!Finite(allowed)) { issues.Add("RowResistanceOutsideNumericRange"); return Result(); }
                rows.Add(new EndPlateEffectiveRow(i, levers[i], allowed, limit));
                if (!elasticReference.HasValue && allowed > threshold) elasticReference = i;
                Value("Row" + (i + 1) + ".LeverArm", levers[i], "mm"); Value("Row" + (i + 1) + ".EffectiveResistance", allowed, "N");
            }
            double moment = rows.Sum(r => r.Resistance * r.LeverArm), total = rows.Sum(r => r.Resistance);
            Value("MomentResistance", moment, "Nmm"); Value("CompressionResultantAtResistance", total, "N");
            Value("PanelShearAtResistance", p.Beta * total, "N");
            if (!(moment > 0)) issues.Add("PositiveMomentResistanceRequired");
            if (issues.Count != 0) return Result();
            checks.Add(new ConnectionCheck("EndPlate.MomentComponents", p.Moment, moment, reference, "Nmm"));
            double scale = p.Moment / moment;
            foreach (var c in constraints)
            {
                double force = rows.Where(r => r.Row >= c.FirstRow && r.Row < c.FirstRow + c.RowCount).Sum(r => r.Resistance) * scale;
                if (!Finite(force)) issues.Add("DesignRowForcesOutsideNumericRange");
                else
                {
                    // Remove only round-off at a known feasible mechanism scaled down to demand.
                    // The independent moment check retains the exact supplied demand.
                    if (scale <= 1 && force <= c.Resistance * (1 + 1e-12)) force = Math.Min(force, c.Resistance);
                    checks.Add(new ConnectionCheck(c.Id, force, c.Resistance, c.Reference));
                }
            }
            return Result();

            TStubBoltRowsInput Build(TStubDerivedGeometry geometry, double t, double fy) => geometry.CreateInput(code, p.Bolt,
                t, fy, Enumerable.Repeat(0.0, geometry.IndividualRows.Count), p.TStubMethod, p.ContactWidth,
                holeClearanceServiceabilitySource: p.HoleClearanceServiceabilitySource);
            void Geometry(string name, TStubDerivedGeometry geometry) => Include(name + ".Geometry", geometry.Trace, geometry.Diagnostics);
            void TStubFamily(string name, TStubBoltRowsInput family, int first, bool isColumn, bool outside = false)
            {
                for (int i = 0; i < family.OrderedRows.Count; i++) Component(name + ".Row" + (i + 1), first + i, 1, family.OrderedRows[i], isColumn, outside);
                foreach (var gr in family.AdjacentGroups) Component(name + ".Rows" + (gr.FirstRow + 1) + "To" + (gr.FirstRow + gr.RowCount),
                    first + gr.FirstRow, gr.RowCount, gr.Component, isColumn, outside);
            }
            void Component(string name, int first, int count, TStubTensionInput input, bool isColumn, bool outside)
            {
                var t = TStubTension.Verify(input); Include(name, t.Trace, t.Diagnostics);
                if (!t.IsComplete) return;
                Add(name + ".TStub", first, count, t.Checks.Min(c => c.Resistance), "EN6.2.4/Table6.2; " + input.EffectiveLengths.Source, EndPlateRowComponentKind.TStub);
                // Explicit conservative choice also for bolt-governed modes, never GoverningCheck at zero demand.
                double width = Math.Min(input.EffectiveLengths.Mode1, input.EffectiveLengths.Mode2);
                Value(name + ".ConservativeWebEffectiveWidth", width, "mm");
                if (isColumn)
                {
                    var w = ColumnWebComponents.VerifyTransverseTension(new ColumnWebTensionInput(p.Policy, col,
                        new ColumnWebEffectiveWidth(width, input.EffectiveLengths.Source + "; min(leff1,leff2) conservative"), 0,
                        p.Beta, p.TransformationSource, p.ActionCombinationSource));
                    Include(name + ".Web", w.Trace, w.Diagnostics);
                    if (w.IsComplete) Add(name + ".Web", first, count, w.Checks.Min(c => c.Resistance), "EN6.2.6.3", EndPlateRowComponentKind.ColumnWeb);
                }
                else if (!outside) Add(name + ".Web", first, count, width * beam.WebThickness * beam.Fy / code.Plates.GammaM0, "EN6.2.6.8(6.22), min(leff1,leff2) conservative",
                    EndPlateRowComponentKind.BeamWeb);
            }
            void Add(string name, int first, int count, double resistance, string source, EndPlateRowComponentKind kind = EndPlateRowComponentKind.Global)
            {
                if (!(resistance > 0) || !Finite(resistance)) issues.Add(name + "OutsideNumericRange");
                else constraints.Add(new EndPlateRowCapacityConstraint(name, first, count, resistance, source, kind));
            }
            void Include(string name, IEnumerable<SectionResistanceValue> values, IEnumerable<string> diagnostics)
            {
                trace.AddRange(values.Select(v => new SectionResistanceValue(name + "." + v.Name, v.Value, v.Unit)));
                issues.AddRange(diagnostics.Select(d => name + ":" + d));
            }
            void Value(string name, double value, string unit)
            { if (!Finite(value)) issues.Add(name + "OutsideNumericRange"); else trace.Add(new SectionResistanceValue(name, value, unit)); }
            EndPlateMomentComponentResult Result() => new EndPlateMomentComponentResult(new ConnectionVerification<EndPlateMomentComponentInput>(
                p, code, "Steel.Connection.EndPlate.MomentComponents",
                "Local pure-bending component resistance and EN sequential tension rows. Retained shear is not qualified; no whole-joint result or stiffness classification.",
                checks, trace, issues, new[] {
                    "Compression centre at the connected beam compression-flange mid-thickness; moment has the declared sign; N=0 only.",
                    "Unstiffened rolled I/H members, rectangular end plate, symmetric identical two-bolt rows; no haunch or backing plates.",
                    "Column and beam webs use min(leff1,leff2) conservatively, irrespective of the controlling T-stub mode.",
                    "Prying develops per Table6.2 note1; EN1.9Ft row restriction applies without implicit UK National Annex exceptions.",
                    "Beam Mc,Rd is supplied after section classification and concomitant shear reduction: " + p.BeamMomentResistance.Source,
                    "Beta, existing column stress, restraints and concomitant actions are qualified by their explicit input sources.",
                    "NTC2018 factors use the explicitly supplementary EN component method; capacity is not a national literal equation.",
                    "Effective-row forces are a resistance mechanism and proportional demand check, not elastic service bolt forces or prying Q."
                }), rows, constraints);
        }
        private static bool Equal(double a, double b) => Math.Abs(a - b) <= 1e-10 * Math.Max(Math.Abs(a), Math.Abs(b));
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
