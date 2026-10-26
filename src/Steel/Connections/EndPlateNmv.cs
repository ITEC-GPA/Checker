using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Actual coincident actions. BeamFaceActions refer to the beam centroid at
    /// the column OUTER flange face. Column cuts are extrapolated to the beam mid-height
    /// on the column centroid line. N>0 tension, V2>0 upward, M1>0 upper-flange tension.
    /// With e=column.Height/2: lower.N-upper.N=-V2, lower.V2-upper.V2=-N,
    /// lower.M1-upper.M1=M1-e*V2. No independent maxima or omitted rigid arm.</summary>
    public sealed class EndPlateNmvInput
    {
        public EndPlateNmvGeometry Geometry { get; }
        public SteelActions BeamFaceActions { get; }
        public SteelActions ColumnUpper { get; }
        public SteelActions ColumnLower { get; }
        public string CombinationSource { get; }
        public string ColumnReferenceSource { get; }
        public bool ColumnPanelModelledSeparately { get; }
        public string ColumnPanelModelSource { get; }
        public EndPlateNmvTangent AppliedGlobalTangent { get; }
        public EndPlateNmvInput(EndPlateNmvGeometry geometry, SteelActions beamFaceActions,
            SteelActions columnUpper, SteelActions columnLower, string combinationSource, string columnReferenceSource,
            bool columnPanelModelledSeparately = false, string columnPanelModelSource = null, EndPlateNmvTangent appliedGlobalTangent = null)
        {
            Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            BeamFaceActions = beamFaceActions ?? throw new ArgumentNullException(nameof(beamFaceActions));
            ColumnUpper = columnUpper ?? throw new ArgumentNullException(nameof(columnUpper));
            ColumnLower = columnLower ?? throw new ArgumentNullException(nameof(columnLower));
            CombinationSource = combinationSource; ColumnReferenceSource = columnReferenceSource;
            ColumnPanelModelledSeparately = columnPanelModelledSeparately; ColumnPanelModelSource = columnPanelModelSource;
            if (appliedGlobalTangent != null && !appliedGlobalTangent.IsPositiveDefinite)
                throw new ArgumentException("The applied two-DOF matrix must be positive definite.", nameof(appliedGlobalTangent));
            AppliedGlobalTangent = appliedGlobalTangent;
        }
    }

    public sealed class EndPlateNmvResult
    {
        public EndPlateNmvInput Input { get; }
        public EndPlateNmvEquilibriumResult Equilibrium { get; }
        public bool IsComplete => Diagnostics.Count == 0;
        public ConnectionStatus Status => Checks.Any(c => !c.Passed) ? ConnectionStatus.Failed : IsComplete ? ConnectionStatus.Passed : ConnectionStatus.NotSupported;
        public bool CanQualifyWholeJoint => IsComplete;
        public bool CanQualifyWholeMember => false;
        /// <summary>Pure-load component bounds at retained V and column compression context;
        /// NOT full-frame resistances or permissions to scale the column cut actions.</summary>
        public double? AxialTensionResistance { get; }
        public double? AxialCompressionResistance { get; }
        public double? PositiveMomentResistance { get; }
        public double? NegativeMomentResistance { get; }
        public double? ComponentUtilization { get; }
        public IReadOnlyList<ConnectionCheck> Checks { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public IReadOnlyList<string> ExternalConditions { get; }
        internal EndPlateNmvResult(EndPlateNmvInput input, EndPlateNmvEquilibriumResult equilibrium, List<ConnectionCheck> checks,
            List<SectionResistanceValue> trace, List<string> issues, double[] capacities, double? componentUtilization)
        {
            Input = input; Equilibrium = equilibrium; Checks = Array.AsReadOnly(checks.ToArray()); Trace = Array.AsReadOnly(trace.ToArray());
            Diagnostics = Array.AsReadOnly(issues.Distinct().ToArray()); ComponentUtilization = componentUtilization;
            if (capacities != null && IsComplete)
            { AxialTensionResistance = capacities[0]; AxialCompressionResistance = capacities[1]; PositiveMomentResistance = capacities[2]; NegativeMomentResistance = capacities[3]; }
            ExternalConditions = Array.AsReadOnly(new[] {
                "Standard single-sided rolled I/H joint, all physical rows qualified in tension, initial closed flange contact and full-strength transfer at both flanges.",
                "Elastic compatible component distribution without plastic redistribution; pure-load linear interaction is an additional conservative bound, not a generic fracture law.",
                "Actual face actions and column cuts belong to one combination; the face-to-centroid shear arm and column net-centroid eccentricity are included.",
                "Member stability, fatigue, fire, seismic ductility, available rotation capacity and arbitrary three-dimensional joints remain external.",
                "The local tangent excludes column panel shear flexibility. Use it in a global model only with a separately represented panel and within its qualified elastic branch.",
                "Source strings retain provenance; actual hardware, contact preparation, root transitions, penetration and inspection still require the supplied execution details."
            });
        }
    }

    public static class EndPlateNmv
    {
        public static EndPlateNmvResult Verify(EndPlateNmvInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var g = p.Geometry; var m = g.Baseline.Input; var code = m.Policy.Code; var a = p.BeamFaceActions;
            var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>();
            var issues = new List<string>(g.Diagnostics); EndPlateNmvEquilibriumResult eq = null;
            double[] capacities = null; double? componentUse = null; double columnNormalBound = 0;
            // Preserve every known fixed-detail failure, even if another field is unsupported.
            AddChecks("Bolts.", g.Connection.Bolts.BoltAssessment.Checks);
            if (g.Connection.FilletWelds != null) AddChecks("Weld.", g.Connection.FilletWelds.WeldAssessment.Checks);
            if (g.Connection.TopFlushCjpWelds != null) AddChecks("Weld.", g.Connection.TopFlushCjpWelds.WeldAssessment.Checks);
            AddChecks("InPlane.", g.InPlane.Checks);
            Require(!string.IsNullOrWhiteSpace(p.CombinationSource) && !string.IsNullOrWhiteSpace(p.ColumnReferenceSource), "ActualCombinationAndColumnReferenceRequired");
            foreach (var v in new[] { a, p.ColumnUpper, p.ColumnLower })
                Require(v.V1 == 0 && v.M2 == 0 && v.T == 0, "OnlyConcomitantInPlaneNM1V2Supported");
            Require(Equal(Math.Abs(a.V2), m.Shear), "PreparedShearEnvelopeDoesNotMatchActualCase");
            double arm = m.Column.Height / 2, referenceMoment = a.M1 - arm * a.V2;
            Require(Equal(p.ColumnLower.N - p.ColumnUpper.N, -a.V2), "ColumnAxialJumpDoesNotBalanceBeamShear");
            Require(Equal(p.ColumnLower.V2 - p.ColumnUpper.V2, -a.N), "ColumnShearJumpDoesNotBalanceBeamAxialForce");
            Require(Equal(p.ColumnLower.M1 - p.ColumnUpper.M1, referenceMoment), "ColumnMomentJumpMustIncludeFaceShearArm");
            Value("BeamFace.N", a.N, "N"); Value("BeamFace.V2", a.V2, "N"); Value("BeamFace.M1", a.M1, "Nmm");
            Value("ColumnFaceOffset", arm, "mm"); Value("MomentAtColumnCentroid", referenceMoment, "Nmm");
            if (issues.Count != 0) return Result();
            eq = EndPlateNmvEquilibrium.Solve(g.Rows, g.Contacts, a.N, a.M1);
            Require(eq.IsConverged, "NmvEquilibriumDidNotConverge");
            Value("Equilibrium.AxialResidual", eq.AxialResidual, "N"); Value("Equilibrium.MomentResidual", eq.MomentResidual, "Nmm");
            Value("Equilibrium.Opening", eq.AxialOpening, "mm"); Value("Equilibrium.Rotation", eq.Rotation, "rad");
            Value("Equilibrium.ElasticEnergy", eq.ElasticEnergy, "Nmm");
            double panel = PanelEnvelope(eq, p.ColumnUpper.V2);
            Value("Column.PanelShearEnvelope", panel, "N");
            double compressionStress = ColumnChecks(panel);
            double transverseMargin = 1 - columnNormalBound / (Math.Min(m.Column.Fy, m.ColumnFlangeFy) / code.Plates.GammaM0);
            Require(transverseMargin > 0, "NoColumnElasticMarginForTransverseComponents");
            Value("Column.TransverseElasticMargin", Math.Max(0, transverseMargin), "1");
            var panelResult = ColumnWebComponents.VerifyPanelShear(new ColumnWebPanelInput(m.Policy, m.Column, panel,
                ColumnWebPanelConfiguration.SingleSidedJoint, p.ColumnReferenceSource));
            issues.AddRange(panelResult.Diagnostics.Select(d => "Panel:" + d));
            if (!panelResult.IsComplete) return Result();
            double panelResistance = panelResult.Checks.Min(c => c.Resistance);
            var compression = new double[2];
            for (int i = 0; i < 2; i++)
            {
                var r = ColumnWebComponents.VerifyTransverseCompression(new ColumnWebCompressionInput(m.Policy, m.Column,
                    g.CompressionWidths[i], 0, 0, compressionStress, "Panel interaction checked explicitly by conservative linear utilization",
                    "Actual net column stress envelope from this N-M-V case", m.ColumnSwayRestraintSource, p.CombinationSource));
                issues.AddRange(r.Diagnostics.Select(d => "Compression" + i + ":" + d));
                if (!r.IsComplete || r.Checks.Count == 0) continue;
                compression[i] = r.Checks.Min(c => c.Resistance);
                Value("Contact" + i + ".ColumnCompressionResistance", compression[i], "N");
            }
            Require(compression.All(x => x > 0), "PositiveActualColumnCompressionResistancesRequired");
            var section = Rectangles(m.Beam, 0);
            double tau = 1.5 * Math.Abs(a.V2) / (m.Beam.WebThickness * m.Beam.ClearWeb);
            double normalLimit = Math.Min(m.Beam.Fy / code.Plates.GammaM0 - Math.Sqrt(3) * tau, .9 * m.Beam.Fu / code.Plates.GammaM2);
            Check("Beam.ElasticShearBound", Math.Sqrt(3) * tau, m.Beam.Fy / code.Plates.GammaM0, "Conservative elastic web stress bound", "MPa");
            Require(normalLimit > 0, "NoPositiveElasticBeamNormalStressMarginAtRetainedShear");
            if (issues.Count != 0) return Result();
            var components = ComponentChecks(eq, p.ColumnUpper.V2).ToArray(); checks.AddRange(components);
            componentUse = components.Max(c => c.Utilization);
            capacities = new[] { PureCapacity(100000, 0), PureCapacity(-100000, 0), PureCapacity(0, 1e8), PureCapacity(0, -1e8) };
            if (capacities.Any(x => !(x > 0) || double.IsInfinity(x) || double.IsNaN(x)))
            { issues.Add("PureComponentResistanceOutsideNumericRange"); capacities = null; return Result(); }
            double axialCapacity = a.N >= 0 ? capacities[0] : capacities[1];
            double momentCapacity = a.M1 >= 0 ? capacities[2] : capacities[3];
            Check("Joint.ConservativeNMInteraction", Math.Abs(a.N) / axialCapacity + Math.Abs(a.M1) / momentCapacity, 1,
                "EN1993-1-8:2005 6.2.7.1(3), Eq6.24; sign-specific pure component bounds, additionally applied below5%Npl", "1");
            Value("PureComponent.NtRd", capacities[0], "N"); Value("PureComponent.NcRd", capacities[1], "N");
            Value("PureComponent.MpositiveRd", capacities[2], "Nmm"); Value("PureComponent.MnegativeRd", capacities[3], "Nmm");
            return Result();

            IEnumerable<ConnectionCheck> ComponentChecks(EndPlateNmvEquilibriumResult state, double upperShear)
            {
                var list = new List<ConnectionCheck>(); double pv = PanelEnvelope(state, upperShear);
                Add("Panel.Shear", pv, panelResistance, "EN1993-1-8 6.2.6.1; actual cumulative normal-force equilibrium");
                foreach (var constraint in g.Baseline.MomentComponents.Constraints.Where(c => c.Id.Contains(".TStub") || c.Id.Contains(".Web")))
                {
                    double force = state.Rows.Skip(constraint.FirstRow).Take(constraint.RowCount).Sum(r => r.Tension);
                    if (constraint.Id.StartsWith("Column.") && constraint.Id.EndsWith(".Web"))
                        Add("Components." + constraint.Id, force / constraint.Resistance + pv / panelResistance, transverseMargin,
                            constraint.Reference + "; supplementary conservative linear longitudinal/transverse/panel interaction", "1");
                    else Add("Components." + constraint.Id, force, constraint.Resistance, constraint.Reference);
                }
                for (int i = 0; i < 2; i++)
                {
                    var contact = state.Contacts[i]; double height = contact.Band.UpperZ - contact.Band.LowerZ;
                    double peakEquivalent = contact.MaximumCompressionPerLength * height;
                    Add("Contact" + i + ".ColumnWeb", peakEquivalent / compression[i] + pv / panelResistance, transverseMargin,
                        "EN6.2.6.2 compression with actual longitudinal stress; supplementary peak-load and linear panel bounds", "1");
                    double pressure = contact.MaximumCompressionPerLength / g.ContactWidths[i];
                    Add("Contact" + i + ".PeakPressure", pressure, Math.Min(m.Beam.Fy, Math.Min(m.EndPlate.Fy, m.ColumnFlangeFy)) / code.Plates.GammaM0,
                        "Actual unilateral contact band; conservative elastic peak pressure", "MPa");
                    Add("Contact" + i + ".BeamFlange", peakEquivalent, m.Beam.Width * m.Beam.FlangeThickness * m.Beam.Fy / code.Plates.GammaM0,
                        "Conservative isolated beam flange normal-force bound");
                }
                Add("Beam.ElasticNMAtRetainedV", Math.Abs(state.AxialForce) / section.Area + Math.Abs(state.Moment) / section.Modulus,
                    normalLimit, "EN1993-1-1 6.2.1 elastic bound: |N|/A+|M|/Wel+sqrt(3)|tau|<=fy/gammaM0; omitted root fillets", "MPa");
                return list;
                void Add(string id, double d, double r, string reference, string unit = "N") => list.Add(new ConnectionCheck(id, d, r, reference, unit));
            }

            double PureCapacity(double n, double moment)
            {
                // Constitutive basis calculation, never a fabricated FEM combination.
                // Initially closed contact is homogeneous: all variable demands scale.
                var state = EndPlateNmvEquilibrium.Solve(g.Rows, g.Contacts, n, moment);
                if (!state.IsConverged) return double.NaN;
                double utilization = ComponentChecks(state, 0).Max(c => c.Utilization);
                return (n == 0 ? Math.Abs(moment) : Math.Abs(n)) / utilization;
            }

            double ColumnChecks(double pv)
            {
                var col = m.Column; var net = Rectangles(col, 2 * m.Bolt.HoleDiameter);
                double span = g.Rows.Select(r => r.Z).Concat(g.Contacts.SelectMany(c => new[] { c.LowerZ, c.UpperZ })).Max()
                    - g.Rows.Select(r => r.Z).Concat(g.Contacts.SelectMany(c => new[] { c.LowerZ, c.UpperZ })).Min();
                double maximumCompression = 0, fy = Math.Min(col.Fy, m.ColumnFlangeFy);
                Value("Column.NetArea", net.Area, "mm2"); Value("Column.NetCentroidShift", net.Centroid, "mm");
                Value("Column.NetInertia", net.Inertia, "mm4"); Value("Column.PanelSpan", span, "mm");
                int i = 0;
                foreach (var cut in new[] { p.ColumnUpper, p.ColumnLower })
                {
                    // Both cut actions were extrapolated to mid-height. Bound the actual
                    // moment anywhere in the panel without inferring a frame diagram.
                    double momentBound = Math.Abs(cut.M1) + pv * span + Math.Abs(cut.N * net.Centroid);
                    double normal = Math.Abs(cut.N) / net.Area + momentBound / net.Modulus;
                    columnNormalBound = Math.Max(columnNormalBound, normal);
                    maximumCompression = Math.Max(maximumCompression, Math.Max(0, -cut.N / net.Area + momentBound / net.Modulus));
                    double shear = 1.5 * pv / (col.WebThickness * col.ClearWeb);
                    Check("Column.Cut" + i + ".ElasticNet", normal + Math.Sqrt(3) * shear, fy / code.Plates.GammaM0,
                        "Conservative elastic net-column envelope throughout the panel, including gross-to-net axial eccentricity", "MPa");
                    Check("Column.Cut" + i + ".NetNormalBound", normal, .9 * Math.Min(col.Fu, g.Connection.Bolts.BoltAssessment.Input.ColumnFlangeFu) / code.Plates.GammaM2,
                        "Supplementary elastic normal-stress bound on net rectangles", "MPa"); i++;
                }
                Value("Column.LongitudinalCompressionBound", maximumCompression, "MPa");
                return maximumCompression;
            }
            void Require(bool ok, string diagnostic) { if (!ok) issues.Add(diagnostic); }
            void Value(string id, double value, string unit)
            { if (double.IsNaN(value) || double.IsInfinity(value)) issues.Add("NumericRange:" + id); else trace.Add(new SectionResistanceValue(id, value, unit)); }
            void Check(string id, double d, double r, string reference, string unit = "N")
            { if (!(r > 0) || double.IsInfinity(d / r) || double.IsNaN(d / r)) issues.Add("NumericRange:" + id); else checks.Add(new ConnectionCheck(id, d, r, reference, unit)); }
            void AddChecks(string prefix, IEnumerable<ConnectionCheck> source)
            { checks.AddRange(source.Select(c => new ConnectionCheck(prefix + c.Id, c.Demand, c.Resistance, c.Reference, c.Unit))); }
            EndPlateNmvResult Result() => new EndPlateNmvResult(p, eq, checks, trace, issues, capacities, componentUse);
        }

        internal static double PanelEnvelope(EndPlateNmvEquilibriumResult state, double upperShear)
        {
            double maximum = Math.Abs(upperShear);
            var cuts = state.Rows.Select(r => r.Row.Z).Concat(state.Contacts.SelectMany(c => new[] { c.Band.LowerZ, c.Band.UpperZ })).Distinct();
            foreach (double z in cuts)
            {
                double forceAbove = state.Rows.Where(r => r.Row.Z > z).Sum(r => r.Tension);
                foreach (var c in state.Contacts)
                {
                    if (!c.ClosedLowerZ.HasValue) continue;
                    double lo = Math.Max(z, c.ClosedLowerZ.Value), hi = c.ClosedUpperZ.Value;
                    if (hi > lo) forceAbove += c.Band.Stiffness / (c.Band.UpperZ - c.Band.LowerZ) * (hi - lo)
                        * (state.AxialOpening + state.Rotation * (lo + hi) / 2);
                }
                maximum = Math.Max(maximum, Math.Abs(upperShear - forceAbove));
                maximum = Math.Max(maximum, Math.Abs(upperShear - forceAbove - state.Rows.Where(r => r.Row.Z == z).Sum(r => r.Tension)));
            }
            return Math.Max(maximum, Math.Abs(upperShear - state.AxialForce));
        }

        internal static (double Area, double Centroid, double Inertia, double Modulus) Rectangles(ConnectionRolledISection s, double removedFlangeWidth)
        {
            double h = s.Height, b = s.Width, t = s.FlangeThickness, tw = s.WebThickness, hw = h - 2 * t, z = (h - t) / 2;
            double area = 2 * b * t + hw * tw - removedFlangeWidth * t;
            double centroid = -removedFlangeWidth * t * z / area;
            double inertia = 2 * (b * t * t * t / 12 + b * t * z * z) + tw * hw * hw * hw / 12
                - removedFlangeWidth * t * (z * z + t * t / 12) - area * centroid * centroid;
            return (area, centroid, inertia, inertia / (h / 2 + Math.Abs(centroid)));
        }
        private static bool Equal(double x, double y) => Math.Abs(x - y) <= 1e-8 * Math.Max(1, Math.Max(Math.Abs(x), Math.Abs(y)));
    }
}
