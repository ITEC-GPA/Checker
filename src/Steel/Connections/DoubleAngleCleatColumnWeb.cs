using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Actual supporting column WEB for the standard twin-cleat component.
    /// X is normal to the web, away from the column, Y is upward along the column,
    /// Z completes the right-handed frame. Column depth is along Z, flange width
    /// along X. Both cuts use the SAME +Y section-face convention, not outward
    /// normals. Cut coordinates are actual column-centroid points (0,Y,0).
    /// Lower minus upper, after transport, balances the beam-on-column action.
    /// End distances run from extreme bolt centres to actual column ends. N,mm,MPa.</summary>
    internal sealed class DoubleAngleCleatColumnWebInput
    {
        public DoubleAngleCleatComponentInput Cleats { get; }
        public ConnectionRolledISection Column { get; }
        public ConnectionPlate ColumnFlange { get; }
        public PlanarConnectionActions UpperCut { get; }
        public PlanarConnectionActions LowerCut { get; }
        public double TopBoltEnd { get; }
        public double BottomBoltEnd { get; }
        public string DetailSource { get; }
        public string SameCaseCutSource { get; }
        public DoubleAngleCleatColumnWebInput(DoubleAngleCleatComponentInput cleats,
            ConnectionRolledISection column, double flangeFy, double flangeFu,
            double topBoltEnd, double bottomBoltEnd, PlanarConnectionActions upperCut,
            PlanarConnectionActions lowerCut, string detailSource, string sameCaseCutSource)
        {
            Cleats = cleats ?? throw new ArgumentNullException(nameof(cleats));
            Column = column ?? throw new ArgumentNullException(nameof(column));
            ColumnFlange = new ConnectionPlate(column.Width, column.FlangeThickness, flangeFy, flangeFu);
            UpperCut = upperCut ?? throw new ArgumentNullException(nameof(upperCut));
            LowerCut = lowerCut ?? throw new ArgumentNullException(nameof(lowerCut));
            ConnectionValidation.Positive(topBoltEnd, nameof(topBoltEnd));
            ConnectionValidation.Positive(bottomBoltEnd, nameof(bottomBoltEnd));
            TopBoltEnd = topBoltEnd; BottomBoltEnd = bottomBoltEnd;
            DetailSource = detailSource; SameCaseCutSource = sameCaseCutSource;
        }
    }

    /// <summary>Named support components, not a whole-joint verdict. Previously
    /// evaluated failures remain visible when another component is unsupported.</summary>
    internal sealed class DoubleAngleCleatColumnWebResult
    {
        public DoubleAngleCleatColumnWebInput Input { get; }
        public DoubleAngleCleatComponentResult Cleats { get; }
        public ConnectionVerification<DoubleAngleCleatColumnWebInput> Support { get; }
        public bool IsComponentAssessmentComplete => Cleats.ComponentAssessment.IsComplete && Support.IsComplete;
        public bool HasFailedComponent => Cleats.ComponentAssessment.Status == ConnectionStatus.Failed
            || Support.Status == ConnectionStatus.Failed;
        public ConnectionStatus ComponentStatus => HasFailedComponent ? ConnectionStatus.Failed
            : IsComponentAssessmentComplete ? ConnectionStatus.Passed : ConnectionStatus.NotSupported;
        public bool CanQualifyWholeJoint => false;
        public IReadOnlyList<string> MissingWholeJointChecks { get; } = Array.AsReadOnly(new[] {
            "SUPPORT_OUT_OF_PLANE_BENDING_AND_PRYING", "ACTUAL_BOLT_TENSION_AND_PUNCHING", "FRAME_ROTATION_QUALIFICATION" });
        internal DoubleAngleCleatColumnWebResult(DoubleAngleCleatColumnWebInput input,
            DoubleAngleCleatComponentResult cleats, ConnectionVerification<DoubleAngleCleatColumnWebInput> support)
        { Input = input; Cleats = cleats; Support = support; }
    }

    /// <summary>SBE-MS5 shear components plus a supplementary column-web assessment:
    /// bearing, SCI P358 Check10 strip shear, net-section elastic stress and equilibrium
    /// of actual concomitant cuts. No inference of bolt-axis force from in-plane shear.</summary>
    internal static class DoubleAngleCleatColumnWeb
    {
        public static DoubleAngleCleatColumnWebResult Verify(DoubleAngleCleatColumnWebInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var c = p.Cleats; var col = p.Column; var u = p.UpperCut; var l = p.LowerCut;
            var cleats = DoubleAngleCleatComponents.Verify(c);
            var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>();
            var issues = new List<string>();
            void Gate(bool ok, string id) { if (!ok) issues.Add(id); }
            void Value(string id, double value, string unit)
            {
                if (!Finite(value)) issues.Add("NUMERIC_RANGE:" + id);
                else trace.Add(new SectionResistanceValue(id, value, unit));
            }
            void Check(string id, double demand, double resistance, string source, string unit = "N")
            {
                if (!Finite(demand) || !Finite(resistance) || demand < 0 || resistance <= 0)
                    issues.Add("NUMERIC_RANGE:" + id);
                else checks.Add(new ConnectionCheck(id, demand, resistance, source, unit));
            }
            Gate(cleats.ComponentAssessment.IsComplete, "STANDARD_CLEAT_COMPONENT_SCOPE_REQUIRED");
            Gate(!string.IsNullOrWhiteSpace(p.DetailSource), "ACTUAL_COLUMN_WEB_DETAIL_REQUIRED");
            Gate(!string.IsNullOrWhiteSpace(p.SameCaseCutSource), "ACTUAL_CONCOMITANT_COLUMN_CUTS_REQUIRED");
            double halfBolts = (c.Rows - 1) * c.Pitch / 2, g = c.SupportGauge, d0 = c.Bolt.HoleDiameter;
            double transverseEdge = (col.ClearWeb - g) / 2;
            double extension = Math.Min(5 * c.Bolt.Diameter, g / 2);
            double topStrip = Math.Min(p.TopBoltEnd, 5 * c.Bolt.Diameter);
            double bottomStrip = Math.Min(p.BottomBoltEnd, 5 * c.Bolt.Diameter);
            double influenceTop = halfBolts + Math.Max(40, topStrip);
            double influenceBottom = halfBolts + Math.Max(40, bottomStrip);
            Gate(Math.Max(c.Beam.Width, c.Beam.WebThickness + 2 * c.CleatLeg.Width) < col.ClearWeb,
                "BEAM_AND_COMPLETE_CLEATS_MUST_FIT_BETWEEN_COLUMN_ROOTS");
            Gate(transverseEdge >= Math.Max(1.2 * d0, 18.5), "COLUMN_WEB_TRANSVERSE_EDGE_OR_HARDWARE_CLEARANCE");
            Gate(p.TopBoltEnd >= Math.Max(40, 1.2 * d0) && p.BottomBoltEnd >= Math.Max(40, 1.2 * d0),
                "COLUMN_END_MUST_EXTEND_BEYOND_COMPLETE_CLEAT");
            Gate(p.TopBoltEnd >= extension && p.BottomBoltEnd >= extension, "COLUMN_END_OUTSIDE_SCI_STRIP_EXTENSION");
            Gate(c.Pitch <= Math.Min(14 * Math.Min(c.CleatLeg.Thickness, col.WebThickness), 200), "COLUMN_WEB_MAXIMUM_BOLT_PITCH");
            Gate(col.Fy <= 460 && p.ColumnFlange.Fy <= 460, "COLUMN_CARBON_STEEL_UP_TO_S460_ONLY");
            Gate(col.ClearWeb / col.WebThickness <= 42 * Math.Sqrt(235 / col.Fy)
                && (col.Width - col.WebThickness - 2 * col.RootRadius) / (2 * col.FlangeThickness)
                    <= 14 * Math.Sqrt(235 / p.ColumnFlange.Fy), "COLUMN_CLASS_THREE_OR_BETTER_IN_UNIFORM_COMPRESSION_REQUIRED");
            foreach (var cut in new[] { u, l })
            {
                Gate(cut.ApplicationX == 0 && cut.ApplicationZ == 0, "CUTS_MUST_REFERENCE_ACTUAL_COLUMN_CENTROID");
                // Fz requires a separate perforated-web shear-flow model. Do not silently
                // replace it by a gross shear area or cancel opposing cut values.
                Gate(cut.Fz == 0, "CONCURRENT_STRONG_AXIS_SHEAR_THROUGH_PERFORATED_WEB_NOT_SUPPORTED");
                Gate(cut.My == 0, "CONCURRENT_COLUMN_TORSION_NOT_SUPPORTED");
            }
            Gate(u.ApplicationY >= influenceTop && -l.ApplicationY >= influenceBottom,
                "CUTS_MUST_BRACKET_COMPLETE_COLUMN_CONNECTION_ZONE");
            Gate(u.ApplicationY <= halfBolts + p.TopBoltEnd && -l.ApplicationY <= halfBolts + p.BottomBoltEnd,
                "CUTS_MUST_LIE_WITHIN_ACTUAL_COLUMN_ENDS");
            var upper = AtOrigin(u); var lower = AtOrigin(l);
            double appliedMoment = c.Actions.Fy * col.WebThickness / 2;
            double[] applied = { 0, c.Actions.Fy, 0, 0, 0, appliedMoment };
            string[] axes = { "Fx", "Fy", "Fz", "Mx", "My", "Mz" };
            for (int i = 0; i < axes.Length; ++i)
            {
                Value("Equilibrium." + axes[i] + ".Residual", lower[i] - upper[i] - applied[i], i < 3 ? "N" : "Nmm");
                // Scale by the actual terms, without a one-newton floor that would
                // turn a tiny but real unbalanced action into zero.
                Gate(Balances(lower[i], upper[i], applied[i]), "COLUMN_CUT_EQUILIBRIUM:" + axes[i]);
            }
            Value("Column.WebFaceX", col.WebThickness / 2, "mm");
            Value("Column.BeamReactionMoment", appliedMoment, "Nmm");
            Value("Column.SupportGauge", g, "mm");
            Value("Column.TransverseEdge", transverseEdge, "mm");
            if (issues.Count == 0)
            {
                var web = new ConnectionPlate(col.ClearWeb, col.WebThickness, col.Fy, col.Fu);
                var bearing = new List<double>();
                foreach (bool end in new[] { true, false })
                {
                    var r = BoltBearing.Verify(new BoltBearingInput(c.Code, c.Bolt, web, 0,
                        Math.Min(p.TopBoltEnd, p.BottomBoltEnd), transverseEdge, c.Pitch, g,
                        end, true, c.HoleClearanceServiceabilitySource));
                    if (!r.IsComplete) issues.AddRange(r.Diagnostics.Select(x => "ColumnBearing:" + x));
                    else bearing.Add(r.Checks.Single().Resistance);
                }
                if (issues.Count == 0)
                {
                    foreach (string side in new[] { "Left", "Right" })
                    foreach (var bolt in cleats.LeftSupportBolts.Forces)
                        Check(side + ".Bolt" + (bolt.Index + 1) + ".ColumnWebBearing", bolt.Shear, bearing.Min(),
                            "EN1993-1-8:2005 Table3.4 / NTC2018 4.2.8.1.1; one actual web, V/(2n) per bolt");
                    double t = col.WebThickness, tf = col.FlangeThickness, h = col.Height, b = col.Width;
                    double hw = h - 2 * tf, zf = (h - tf) / 2;
                    double ag = 2 * b * tf + hw * t, an = ag - 2 * d0 * t;
                    double ix = 2 * (b * tf * tf * tf / 12 + b * tf * zf * zf)
                        + t * hw * hw * hw / 12 - 2 * t * (d0 * d0 * d0 / 12 + d0 * g * g / 4);
                    double iz = 2 * tf * b * b * b / 12 + (hw - 2 * d0) * t * t * t / 12;
                    Value("Column.GrossRectangularArea", ag, "mm2"); Value("Column.NetArea", an, "mm2");
                    Value("Column.NetIx", ix, "mm4"); Value("Column.NetIz", iz, "mm4");
                    Gate(an > 0 && ix > 0 && iz > 0 && Finite(an + ix + iz), "POSITIVE_FINITE_COLUMN_NET_GEOMETRY_REQUIRED");
                    if (issues.Count == 0)
                    {
                        // N varies monotonically under the downward reactions. Mx is
                        // constant (Fz=0). The Mz envelope includes the entire eccentric
                        // reaction jump in addition to the endpoint maximum; this also
                        // bounds intermediate extrema between individual bolt rows.
                        double n = Math.Max(Math.Abs(u.Fy), Math.Abs(l.Fy));
                        double mx = Math.Max(Math.Abs(u.Mx), Math.Abs(l.Mx));
                        double mz = Math.Max(Math.Abs(u.Mz), Math.Abs(l.Mz)) + Math.Abs(appliedMoment);
                        double sigma = n / an + mx * h / (2 * ix) + mz * b / (2 * iz);
                        double tauFlange = 1.5 * Math.Max(Math.Abs(u.Fx), Math.Abs(l.Fx)) / (2 * b * tf);
                        double fy = Math.Min(col.Fy, p.ColumnFlange.Fy), fu = Math.Min(col.Fu, p.ColumnFlange.Fu);
                        Value("Column.AxialEnvelope", n, "N"); Value("Column.StrongMomentEnvelope", mx, "Nmm");
                        Value("Column.WeakMomentEnvelope", mz, "Nmm"); Value("Column.NormalStressBound", sigma, "MPa");
                        Value("Column.WeakShearStressBound", tauFlange, "MPa");
                        Check("Column.ElasticNetYield", sigma + Math.Sqrt(3) * tauFlange, fy / c.Code.Plates.GammaM0,
                            "Supplementary elastic rectangular net-section bound; EN1993-1-1:2005 6.2.1", "MPa");
                        Check("Column.NetNormalFractureBound", sigma, .9 * fu / c.Code.Plates.GammaM2,
                            "Supplementary conservative net normal-stress bound, including compression; no gross modulus substitution", "MPa");
                        for (int rows = 1; rows <= c.Rows; ++rows)
                        foreach (bool fromTop in new[] { true, false })
                        {
                            string id = "Column." + (fromTop ? "Top" : "Bottom") + rows;
                            double area = t * ((fromTop ? topStrip : bottomStrip) + (rows - 1) * c.Pitch + extension);
                            double net = area - rows * d0 * t;
                            double v = -c.Actions.Fy * rows / (2 * c.Rows);
                            Value(id + ".GrossStripArea", area, "mm2"); Value(id + ".NetStripArea", net, "mm2");
                            Check(id + ".GrossShear", v, PlasticShear.Resistance(area, col.Fy, c.Code.Plates.GammaM0),
                                "SCI P358 Check10 support-web strip; supplementary application to SBE-MS5 double cleats");
                            Check(id + ".NetShear", v, PlasticShear.Resistance(net, col.Fu, c.Code.Plates.GammaM2),
                                "SCI P358 Check10 net strip; both directions and every loaded prefix");
                            Check(id + ".ConcomitantNetYield", sigma + Math.Sqrt(3) * (tauFlange + v / net),
                                fy / c.Code.Plates.GammaM0, "Sufficient additive elastic stress bound; concurrent column stresses plus local net strip shear", "MPa");
                        }
                    }
                }
            }
            return new DoubleAngleCleatColumnWebResult(p, cleats, new ConnectionVerification<DoubleAngleCleatColumnWebInput>(p, c.Code,
                "Steel.Connection.DoubleAngleCleat.ColumnWebComponents",
                "Column-web bearing, local strip shear and concurrent elastic net-section components for standard double cleats. "
                + "Does not qualify support out-of-plane response, prying, bolt tension/punching, rotation or whole-joint/member resistance.",
                checks, trace, issues, new[] {
                    "Both actual column cuts use the same +Y-face convention. Lower minus upper at the origin balances beam-on-column action at X=tw/2.",
                    "No distributed column loads inside the two cuts. Concurrent Fx, Fy, Mx and Mz are retained; Fz and torsion are outside this perforated-web model.",
                    "Net rectangles omit root fillets and deduct both holes at every cut, including compression. Actual flange strengths are separate from web strengths.",
                    "The SCI strip extension is a supplementary in-plane model, not evidence for out-of-plane web bending or a complete double-angle joint.",
                    "Local connection forces follow the declared SBE nominal-shear model. Its factors0.8/1.27 are not inverted into fictitious bolt-axis forces."
                }));
        }

        private static double[] AtOrigin(PlanarConnectionActions a) => new[] { a.Fx, a.Fy, a.Fz,
            a.Mx + a.ApplicationY * a.Fz - a.ApplicationZ * a.Fy,
            a.My + a.ApplicationZ * a.Fx - a.ApplicationX * a.Fz,
            a.Mz + a.ApplicationX * a.Fy - a.ApplicationY * a.Fx };
        private static bool Balances(double lower, double upper, double applied)
        {
            double scale = Math.Max(Math.Abs(lower), Math.Max(Math.Abs(upper), Math.Abs(applied)));
            return Finite(lower) && Finite(upper) && Finite(applied)
                && (scale == 0 || Math.Abs(lower / scale - upper / scale - applied / scale) <= 64 * 2.2204460492503131e-16);
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
