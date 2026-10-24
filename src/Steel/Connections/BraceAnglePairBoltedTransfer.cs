using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;
using GPC.Geometry;
using GPC.Model.Results;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Category A, one longitudinal bolt line through two identical back-to-back
    /// Model angles and a central rectangular gusset. N, mm, Nmm, MPa. Model supplies
    /// material, physical connected leg, bolt, pitch and explicitly equilibrated chord
    /// tensions. This input adds only attachment geometry and evidence. Positive X is
    /// member +Z. The angles' free end is at -X; the gusset's free end is at +X.
    /// GussetNegativeYEdge is measured from the physical -Y gusset edge to the bolt line.
    /// HardwareEnvelopeDiameter includes actual washers/head/nut and required tool access.</summary>
    public sealed class BraceAnglePairBoltedTransferInput
    {
        public BraceAngleModelAdapterResult Angles { get; }
        public ConnectionPlate Gusset { get; }
        public ConnectionDesignCode Code => Angles.Components[0].Input.Code;
        public double AngleEndDistance { get; }
        public double GussetEndDistance { get; }
        public double GussetNegativeYEdge { get; }
        public double HardwareEnvelopeDiameter { get; }
        public bool Exposed { get; }
        public bool HasAdditionalHolesOrCuts { get; }
        public string GeometrySource { get; }
        public string InPlaneDistributionSource { get; }
        public string ActionCombinationSource { get; }
        public string HoleClearanceServiceabilitySource { get; }
        public BraceAnglePairBoltedTransferInput(BraceAngleModelAdapterResult angles,
            ConnectionPlate gusset, double angleEndDistance, double gussetEndDistance,
            double gussetNegativeYEdge, double hardwareEnvelopeDiameter, bool exposed,
            bool hasAdditionalHolesOrCuts, string geometrySource, string inPlaneDistributionSource,
            string actionCombinationSource, string holeClearanceServiceabilitySource = null)
        {
            Angles = angles ?? throw new ArgumentNullException(nameof(angles));
            Gusset = gusset ?? throw new ArgumentNullException(nameof(gusset));
            ConnectionValidation.Positive(angleEndDistance, nameof(angleEndDistance));
            ConnectionValidation.Positive(gussetEndDistance, nameof(gussetEndDistance));
            ConnectionValidation.Positive(gussetNegativeYEdge, nameof(gussetNegativeYEdge));
            ConnectionValidation.Positive(hardwareEnvelopeDiameter, nameof(hardwareEnvelopeDiameter));
            AngleEndDistance = angleEndDistance; GussetEndDistance = gussetEndDistance;
            GussetNegativeYEdge = gussetNegativeYEdge; HardwareEnvelopeDiameter = hardwareEnvelopeDiameter;
            Exposed = exposed; HasAdditionalHolesOrCuts = hasAdditionalHolesOrCuts;
            GeometrySource = geometrySource; InPlaneDistributionSource = inPlaneDistributionSource;
            ActionCombinationSource = actionCombinationSource;
            HoleClearanceServiceabilitySource = holeClearanceServiceabilitySource;
        }
    }

    /// <summary>All six actions applied BY one angle, expressed in the common connection
    /// axes. AtBoltLine is at the mid-thickness line defined by the existing adapter;
    /// AtGussetFace is at the physical contact face. Local couples are retained even
    /// when the two faces' resultants cancel. These are demands, not a contact solution.</summary>
    public sealed class BraceAnglePairFaceDemand
    {
        public int PartIndex { get; }
        public ConnectionPoint2D GussetFaceInModelSection { get; }
        public PlanarConnectionActions AtBoltLine { get; }
        public PlanarConnectionActions AtGussetFace { get; }
        internal BraceAnglePairFaceDemand(int index, ConnectionPoint2D face,
            PlanarConnectionActions boltLine, PlanarConnectionActions gussetFace)
        { PartIndex = index; GussetFaceInModelSection = face; AtBoltLine = boltLine; AtGussetFace = gussetFace; }
    }

    /// <summary>Conditional IN-PLANE component results only. Status and the local 3D
    /// status remain NotSupported: the unknown contact/prying response is not qualified
    /// by cancellation of opposite local moments or by a zero resultant normal force.</summary>
    public sealed class BraceAnglePairBoltedTransferResult
    {
        public BraceAnglePairBoltedTransferInput Input { get; }
        public ConnectionVerification<BraceAnglePairBoltedTransferInput> InPlaneComponents { get; }
        public IReadOnlyList<ConnectionVerification<BraceAngleTensionInput>> AngleSections { get; }
        public IReadOnlyList<BraceAnglePairFaceDemand> FaceDemands { get; }
        public ElasticBoltGroupResult InPlaneBoltGroup { get; }
        /// <summary>Chord-force resultant. For rejected source/symmetry input this
        /// remains the raw transported sum, not a replacement for SourceActions or
        /// a qualified transfer; the inconsistent source remains in Input/Trace.</summary>
        public PlanarConnectionActions ActionsOnGussetAtBoltGroup { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public IReadOnlyList<string> MissingWholeJointChecks { get; }
        public ConnectionStatus Status => ConnectionStatus.NotSupported;
        public ConnectionStatus LocalThreeDimensionalStatus => ConnectionStatus.NotSupported;
        public bool CanQualifyWholeJoint => false;
        public string Scope => "One-line category A identical2L/gusset in-plane bolt shear, bidirectional ply bearing and effective angle net sections. "
            + "All local eccentric couples retained; no 3D contact/prying, gusset net/block/yielding, angle block tearing or support qualification.";
        internal BraceAnglePairBoltedTransferResult(BraceAnglePairBoltedTransferInput input,
            ConnectionVerification<BraceAnglePairBoltedTransferInput> planar,
            IEnumerable<ConnectionVerification<BraceAngleTensionInput>> sections,
            IEnumerable<BraceAnglePairFaceDemand> faces, ElasticBoltGroupResult group,
            PlanarConnectionActions actions, IEnumerable<string> diagnostics)
        {
            Input = input; InPlaneComponents = planar; AngleSections = Array.AsReadOnly(sections.ToArray());
            FaceDemands = Array.AsReadOnly(faces.ToArray()); InPlaneBoltGroup = group;
            ActionsOnGussetAtBoltGroup = actions;
            Diagnostics = Array.AsReadOnly(diagnostics.Concat(new[] { "LOCAL_3D_CONTACT_PRYING_RESPONSE_NOT_IMPLEMENTED" }).Distinct().ToArray());
            MissingWholeJointChecks = Array.AsReadOnly(new[] {
                "ANGLE_BLOCK_TEARING_ONE_LINE", "GUSSET_BLOCK_TEARING_ONE_LINE", "GUSSET_GROSS_NET_AND_LOAD_SPREAD",
                "ANGLE_LOCAL_3D_CONTACT_PRYING_AND_BOLT_NORMAL_FORCES", "GUSSET_TO_SUPPORT_ATTACHMENT",
                "SUPPORT_LOCAL_RESISTANCE_AND_GLOBAL_COMPATIBILITY", "MEMBER_AND_PACKING_SYSTEM", "SERVICEABILITY_SLIP" });
        }
    }

    public static class BraceAnglePairBoltedTransfer
    {
        public const string MethodId = "Steel.Connection.BraceAnglePair.OneLineBoltedTransfer";
        private const string EllipseReference = "SBE_SS5:2010 AppendixA p5-68, ECCS P126 additional bearing interaction; complementary to EN1993-1-8 Table3.4 / NTC2018 4.2.67";

        public static BraceAnglePairBoltedTransferResult Verify(BraceAnglePairBoltedTransferInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var issues = new List<string>(); var localIssues = new List<string>();
            var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>();
            var sections = new List<ConnectionVerification<BraceAngleTensionInput>>();
            var faces = new List<BraceAnglePairFaceDemand>();
            ElasticBoltGroupResult group = null; PlanarConnectionActions resultant = null;
            void Require(bool condition, string diagnostic) { if (!condition) issues.Add(diagnostic); }
            Require(p.Angles.IsPair, "TWO_IDENTICAL_MODEL_ANGLES_REQUIRED");
            Require(!p.HasAdditionalHolesOrCuts, "ADDITIONAL_HOLES_OR_CUTS_OUTSIDE_SCOPE");
            Require(!string.IsNullOrWhiteSpace(p.GeometrySource), "ACTUAL_GEOMETRY_AND_HARDWARE_SOURCE_REQUIRED");
            Require(!string.IsNullOrWhiteSpace(p.InPlaneDistributionSource), "IN_PLANE_RIGID_DISTRIBUTION_SOURCE_REQUIRED");
            Require(!string.IsNullOrWhiteSpace(p.ActionCombinationSource), "CONCOMITANT_ACTION_COMBINATION_SOURCE_REQUIRED");
            Require(p.Gusset.Fy <= 460, "GUSSET_CARBON_STEEL_UP_TO_S460_ONLY");
            if (!p.Angles.IsPair) return Finish();
            var first = p.Angles.Components[0].Input; var second = p.Angles.Components[1].Input;
            Require(first.Tension == second.Tension, "EQUAL_EXPLICIT_CHORD_TENSIONS_REQUIRED_FOR_DOUBLE_SHEAR");
            var source = p.Angles.SourceActions;
            bool sourceIsExactlyAxial = source.V1 == 0 && source.V2 == 0 && source.T == 0 && source.M1 == 0 && source.M2 == 0;
            bool sourceMatchesChordSum = source.N == first.Tension + second.Tension;
            Require(sourceIsExactlyAxial, "EXACT_AXIAL_SOURCE_ACTIONS_REQUIRED_FOR_SYMMETRIC_TRANSFER");
            Require(sourceMatchesChordSum, "SOURCE_AXIAL_FORCE_MUST_EQUAL_EXPLICIT_CHORD_SUM");
            // FromPair checks numerical equilibrium with a relative tolerance. That
            // tolerance must not erase a small but explicitly supplied physical action.
            var sourceNames = new[] { "N", "V1", "V2", "T", "M1", "M2" };
            var sourceValues = new[] { source.N, source.V1, source.V2, source.T, source.M1, source.M2 };
            for (int i = 0; i < sourceNames.Length; i++)
                AddValue(trace, issues, "SourceActions." + sourceNames[i], sourceValues[i], i < 3 ? "N" : "Nmm");
            Require(p.Angles.ClearBackGap.HasValue && p.Angles.ClearBackGap.Value == p.Gusset.Thickness,
                "GUSSET_THICKNESS_MUST_EQUAL_ACTUAL_CLEAR_BACK_GAP_NO_PACKING");
            Require(first.BoltCount >= 2, "AT_LEAST_TWO_LONGITUDINAL_BOLTS_REQUIRED");
            Require(first.BoltCount <= 1000, "BOLT_COUNT_OUTSIDE_SCOPE");
            Require(p.GussetNegativeYEdge < p.Gusset.Width, "BOLT_LINE_OUTSIDE_GUSSET");
            Require(p.HardwareEnvelopeDiameter > first.Bolt.HoleDiameter, "HARDWARE_ENVELOPE_MUST_COVER_HOLE");
            var property = p.Angles.SourceSection.CreateSectionCopy();
            var pair = (SectionBuiltUp)property.SectionShape;
            var angle = (SectionL)pair.Parts[0].Section;
            bool vertical = first.ConnectedLeg == BraceAngleConnectedLeg.Second;
            double low, high;
            bool stripFound = StraightInnerStrip(angle, vertical, out low, out high);
            Require(stripFound, "EXACT_MODEL_STRAIGHT_CONNECTED_STRIP_REQUIRED");
            double connected = vertical ? angle.VerticalLegLength : angle.HorizontalLegLength;
            double boltPosition = connected - first.ToeEdgeDistance;
            double heelEdge = boltPosition - low, toeEdge = high - boltPosition;
            double pitch = first.Pitch, d0 = first.Bolt.HoleDiameter, halfHardware = p.HardwareEnvelopeDiameter / 2;
            double t = first.Section.Thickness, length = (first.BoltCount - 1.0) * pitch;
            double minEdge = Math.Max(1.2 * d0, halfHardware);
            Require(Finite(length) && Finite(length + p.AngleEndDistance) && Finite(length + p.GussetEndDistance), "JOINT_LENGTH_OUTSIDE_NUMERIC_RANGE");
            Require(heelEdge >= minEdge && toeEdge >= minEdge, "BOLT_OR_HARDWARE_OUTSIDE_FLAT_ANGLE_STRIP");
            Require(p.AngleEndDistance >= minEdge && p.GussetEndDistance >= minEdge,
                "INSUFFICIENT_LONGITUDINAL_END_OR_HARDWARE_DISTANCE");
            Require(p.GussetNegativeYEdge >= minEdge && p.Gusset.Width - p.GussetNegativeYEdge >= minEdge,
                "INSUFFICIENT_GUSSET_EDGE_OR_HARDWARE_DISTANCE");
            // p is longitudinal for Fx and transverse for Fy; use the more restrictive
            // applicable minimum for the bidirectional component model, including zero Fy.
            Require(pitch >= Math.Max(2.4 * d0, p.HardwareEnvelopeDiameter), "INSUFFICIENT_BIDIRECTIONAL_PITCH_OR_HARDWARE_CLEARANCE");
            Require(pitch <= Math.Min(14 * Math.Min(t, p.Gusset.Thickness), 200), "MAXIMUM_BOLT_PITCH");
            if (p.Exposed)
            {
                Require(p.AngleEndDistance <= 4 * t + 40 && Math.Max(heelEdge, toeEdge) <= 4 * t + 40,
                    "EXPOSED_ANGLE_MAXIMUM_EDGE_DISTANCE");
                Require(p.GussetEndDistance <= 4 * p.Gusset.Thickness + 40
                    && Math.Max(p.GussetNegativeYEdge, p.Gusset.Width - p.GussetNegativeYEdge) <= 4 * p.Gusset.Thickness + 40,
                    "EXPOSED_GUSSET_MAXIMUM_EDGE_DISTANCE");
            }
            AddValue(trace, issues, "AngleFlatStripLow", low, "mm"); AddValue(trace, issues, "AngleFlatStripHigh", high, "mm");
            AddValue(trace, issues, "AngleHeelTangentEdge", heelEdge, "mm"); AddValue(trace, issues, "AngleToeTangentEdge", toeEdge, "mm");
            AddValue(trace, issues, "JointLength", length, "mm");
            try
            {
                var a0 = p.Angles.Components[0];
                var center = new Point3d(vertical ? pair.Parts[0].X / 2 + pair.Parts[1].X / 2 : a0.BoltLine.X,
                    vertical ? a0.BoltLine.Y : pair.Parts[0].Y / 2 + pair.Parts[1].Y / 2, 0);
                var atCenter = new List<PlanarConnectionActions>();
                foreach (var a in p.Angles.Components)
                {
                    var part = pair.Parts[a.PartIndex];
                    var force = new ResultBeamForces(a.Input.Tension, 0, 0, 0, 0, 0,
                        new CoordinateSystem(new Point3d(a.Centroid.X, a.Centroid.Y, 0), Vector3d.XAxis, Vector3d.YAxis, Vector3d.ZAxis));
                    var facePoint = new ConnectionPoint2D(vertical ? part.X : a.BoltLine.X, vertical ? a.BoltLine.Y : part.Y);
                    var atBolt = Transform(force, new Point3d(a.BoltLine.X, a.BoltLine.Y, 0), vertical);
                    var atFace = Transform(force, new Point3d(facePoint.X, facePoint.Y, 0), vertical);
                    faces.Add(new BraceAnglePairFaceDemand(a.PartIndex, facePoint, atBolt, atFace));
                    atCenter.Add(Transform(force, center, vertical));
                    ActionsTrace(trace, "Angle" + a.PartIndex + ".AtBoltLine", atBolt);
                    ActionsTrace(trace, "Angle" + a.PartIndex + ".AtGussetFace", atFace);
                    if (atFace.Fz != 0 || atFace.Mx != 0 || atFace.My != 0)
                        localIssues.Add("ANGLE_" + a.PartIndex + "_OUT_OF_PLANE_LOCAL_ACTIONS_REQUIRE_CONTACT_PRYING_MODEL");
                }
                var centerFirst = atCenter[0]; var centerSecond = atCenter[1];
                var raw = new PlanarConnectionActions(centerFirst.Fx + centerSecond.Fx, centerFirst.Fy + centerSecond.Fy, centerFirst.Fz + centerSecond.Fz,
                    centerFirst.Mx + centerSecond.Mx, centerFirst.My + centerSecond.My, centerFirst.Mz + centerSecond.Mz);
                ActionsTrace(trace, "GussetAtGroup.RawResult", raw);
                if (!sourceIsExactlyAxial || !sourceMatchesChordSum || first.Tension != second.Tension)
                {
                    // Preserve the raw chord demands for unsupported unequal splits
                    // or inconsistent sources without qualifying either load model.
                    resultant = raw; ActionsTrace(trace, "GussetAtGroup", raw); return Finish();
                }
                // Identical reflected angles, aligned facing backs and equal explicit
                // tensions prove cancellation of the normal eccentricities. Evaluate
                // the remaining in-plane lever in LOCAL geometry: subtracting global
                // centroids twice can create a fictitious My after a rigid translation.
                // This is exact symmetry, not a numerical cutoff on supplied actions.
                bool reversedInPlane = vertical ? pair.Parts[0].MirrorY : pair.Parts[0].MirrorX;
                double localCentroid = vertical ? angle.Centroid.Y : angle.Centroid.X;
                double lever = (boltPosition - localCentroid) * (reversedInPlane ? -1 : 1);
                AddValue(trace, issues, "GussetAtGroup.InPlaneLever", lever, "mm");
                resultant = new PlanarConnectionActions(source.N, 0, 0, 0, 0, source.N * lever);
                ActionsTrace(trace, "GussetAtGroup", resultant);
                ActionsTrace(trace, "GussetAtGroup.ArithmeticResidual", new PlanarConnectionActions(
                    raw.Fx - resultant.Fx, raw.Fy - resultant.Fy, raw.Fz - resultant.Fz,
                    raw.Mx - resultant.Mx, raw.My - resultant.My, raw.Mz - resultant.Mz));
                // Both original face wrenches and the full raw sum/residual remain
                // visible. Opposing local couples still require the separate 3D model.
                Require(resultant.Fy == 0 && resultant.Fx >= 0, "AXIAL_TENSION_RESULTANT_REQUIRED");
            }
            catch (ArgumentOutOfRangeException) { issues.Add("ACTION_TRANSFORM_OUTSIDE_NUMERIC_RANGE"); }
            if (issues.Count != 0) return Finish();
            // Additional holes/cuts or unqualified geometry invalidate the section
            // assumptions too: do not expose a favourable angle child before the gates.
            foreach (var a in p.Angles.Components)
            {
                var section = BraceAngleTension.Verify(a.Input); sections.Add(section);
                Append("Angle" + a.PartIndex + ".", section, checks, trace, issues);
            }
            if (issues.Count != 0) return Finish();
            try
            {
                var points = Enumerable.Range(0, first.BoltCount).Select(i => new ConnectionPoint2D((i - (first.BoltCount - 1) / 2.0) * pitch, 0));
                group = ElasticBoltGroup.Calculate(points, resultant);
                Require(group.IsSupported, "IN_PLANE_ELASTIC_GROUP_NOT_SUPPORTED");
                if (group.IsSupported)
                    foreach (var f in group.Forces)
                    {
                        string prefix = "Bolt" + (f.Index + 1) + ".";
                        var bolt = BoltStrength.Verify(new BoltStrengthInput(p.Code, first.Bolt, f.Shear, 0,
                            2, length, 0, p.HoleClearanceServiceabilitySource));
                        Append(prefix, bolt, checks, trace, issues);
                        // +F is applied by the angles ON the gusset. Bolt forces ON each
                        // angle are -F/2, in the same common axes. Only equal explicit,
                        // equilibrated chord forces permit this double-shear plane model.
                        Ply(p.Gusset, f.Fx, f.Fy, p.GussetEndDistance + (first.BoltCount - 1 - f.Index) * pitch,
                            f.Index == first.BoltCount - 1, p.GussetNegativeYEdge, p.Gusset.Width - p.GussetNegativeYEdge,
                            f.Index, prefix + "Gusset.");
                        for (int j = 0; j < 2; j++)
                        {
                            var part = pair.Parts[j]; bool reversedToe = vertical ? part.MirrorY : part.MirrorX;
                            var flat = new ConnectionPlate(high - low, t, first.Section.Fy, first.Section.Fu);
                            Ply(flat, -f.Fx / 2, -f.Fy / 2, p.AngleEndDistance + f.Index * pitch,
                                f.Index == 0, reversedToe ? toeEdge : heelEdge, reversedToe ? heelEdge : toeEdge,
                                f.Index, prefix + "Angle" + j + ".");
                        }
                    }
            }
            catch (ArgumentOutOfRangeException) { issues.Add("IN_PLANE_COMPONENT_OUTSIDE_NUMERIC_RANGE"); }
            return Finish();

            void Ply(ConnectionPlate plate, double fx, double fy, double longitudinalEnd,
                bool endBoltX, double negativeYEdge, double positiveYEdge, int index, string prefix)
            {
                bool edgeBoltY = index == 0 || index == first.BoltCount - 1;
                var x = BoltBearing.Verify(new BoltBearingInput(p.Code, first.Bolt, plate, Math.Abs(fx),
                    longitudinalEnd, Math.Min(negativeYEdge, positiveYEdge), pitch, null, endBoltX, true,
                    p.HoleClearanceServiceabilitySource));
                // When Fy=0 evaluate the smaller of both edge capacities: this avoids a
                // favourable arbitrary direction at the exact zero of the bolt-group field.
                double yEnd = fy > 0 ? positiveYEdge : fy < 0 ? negativeYEdge : Math.Min(negativeYEdge, positiveYEdge);
                var y = BoltBearing.Verify(new BoltBearingInput(p.Code, first.Bolt, plate, Math.Abs(fy),
                    yEnd, longitudinalEnd, null, pitch, true, edgeBoltY, p.HoleClearanceServiceabilitySource));
                Append(prefix + "X.", x, checks, trace, issues); Append(prefix + "Y.", y, checks, trace, issues);
                if (x.Status != ConnectionStatus.NotSupported && y.Status != ConnectionStatus.NotSupported)
                {
                    double ux = Math.Abs(fx) / x.Checks[0].Resistance, uy = Math.Abs(fy) / y.Checks[0].Resistance;
                    double interaction = ux * ux + uy * uy;
                    if (Finite(interaction)) checks.Add(new ConnectionCheck(prefix + "BearingEllipse", interaction, 1, EllipseReference, "1"));
                    else issues.Add(prefix + "BEARING_INTERACTION_OUTSIDE_NUMERIC_RANGE");
                }
            }
            BraceAnglePairBoltedTransferResult Finish()
            {
                var planar = new ConnectionVerification<BraceAnglePairBoltedTransferInput>(p, p.Code, MethodId + ".InPlane",
                    "Conditional in-plane shear/bearing of one-line category A2L/gusset and angle section tension only; no contact-induced normal bolt force or full transfer qualification.",
                    checks, trace, issues, new[] {
                        "Explicit equal concomitant tensions from the real Model pair; no automatic N/2 or omitted global moment.",
                        "The straight connected-leg strip has conservative free edges at its exact inner root/toe tangencies; no bent-corner bearing credit.",
                        "Equal stiffness bolts and rigid in-plane distribution. The real eccentric Mz is retained.",
                        "Zero normal bolt force is a conditional plane subproblem, not a solution or exemption for the retained local out-of-plane couples.",
                        "Bearing ellipse is a complementary ECCS recommendation as reproduced in SBE_SS5, not an EN/NTC formula.",
                        "CatA only; direct contact, no packing. All remaining net/block, 3D contact/prying, support and serviceability checks remain open." });
                return new BraceAnglePairBoltedTransferResult(p, planar, sections, faces, group, resultant, issues.Concat(localIssues));
            }
        }

        private static PlanarConnectionActions Transform(ResultBeamForces forces, Point3d origin, bool vertical)
        {
            var axes = new CoordinateSystem(origin, Vector3d.ZAxis, vertical ? Vector3d.YAxis : Vector3d.XAxis,
                vertical ? new Vector3d(-1, 0, 0) : Vector3d.YAxis);
            var a = forces.ToCoordinateSystemWithEccentricity(axes);
            return new PlanarConnectionActions(a.V1, a.V2, a.N, a.M1, a.M2, a.T);
        }
        private static bool StraightInnerStrip(SectionL angle, bool vertical, out double low, out double high)
        {
            low = 0; high = 0; var outlines = angle.GetCurveOutlines();
            if (outlines.Count != 1 || outlines[0].IsApproximation || outlines[0].Holes.Count != 0 || outlines[0].Children.Count != 0) return false;
            double thickness = vertical ? angle.VerticalLegThickness : angle.HorizontalLegThickness;
            // Arc tangent points may reach the exact inner plane with a few ulps
            // of trigonometric roundoff. This tolerance recognizes that plane in
            // LOCAL geometry only; it never enlarges the retained segment endpoints,
            // edge distances, hole or hardware clearance used by the checks.
            double planeRoundoff = 8 * 2.2204460492503131e-16
                * Math.Max(angle.HorizontalLegLength, angle.VerticalLegLength);
            bool OnPlane(double value, double expected) => Finite(value) && Math.Abs(value - expected) <= planeRoundoff;
            foreach (var line in StraightLines(outlines[0].Boundary))
            {
                var a = line.StartPoint; var b = line.EndPoint;
                if (!OnPlane(a.Z, 0) || !OnPlane(b.Z, 0)
                    || (vertical ? !OnPlane(a.X, thickness) || !OnPlane(b.X, thickness)
                        : !OnPlane(a.Y, thickness) || !OnPlane(b.Y, thickness))) continue;
                double x = vertical ? a.Y : a.X, y = vertical ? b.Y : b.X;
                double from = Math.Min(x, y), to = Math.Max(x, y);
                if (to - from > high - low) { low = from; high = to; }
            }
            return high > low;
        }
        private static IEnumerable<LineCurve3d> StraightLines(Curve3d curve)
        {
            if (curve is LineCurve3d line) { yield return line; yield break; }
            if (curve is PolylineCurve3d polyline)
            {
                var points = polyline.Vertices;
                for (int i = 1; i < points.Length; i++) yield return new LineCurve3d(points[i - 1], points[i]);
            }
            else if (curve is PolyCurve3d polycurve)
                for (int i = 0; i < polycurve.SegmentCount; i++)
                    foreach (var segment in StraightLines(polycurve.GetSegment(i))) yield return segment;
        }
        private static void Append<T>(string prefix, ConnectionVerification<T> child, ICollection<ConnectionCheck> checks,
            ICollection<SectionResistanceValue> trace, ICollection<string> issues) where T : class
        {
            foreach (var issue in child.Diagnostics) issues.Add(prefix + issue);
            foreach (var c in child.Checks) checks.Add(new ConnectionCheck(prefix + c.Id, c.Demand, c.Resistance, c.Reference, c.Unit));
            foreach (var v in child.Trace) trace.Add(new SectionResistanceValue(prefix + v.Name, v.Value, v.Unit));
        }
        private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
        private static void AddValue(ICollection<SectionResistanceValue> trace, ICollection<string> issues, string name, double value, string unit)
        { if (Finite(value)) trace.Add(new SectionResistanceValue(name, value, unit)); else issues.Add("NONFINITE_" + name); }
        private static void ActionsTrace(ICollection<SectionResistanceValue> trace, string prefix, PlanarConnectionActions a)
        {
            var values = new[] { a.Fx, a.Fy, a.Fz, a.Mx, a.My, a.Mz };
            var names = new[] { "Fx", "Fy", "Fz", "Mx", "My", "Mz" };
            for (int i = 0; i < 6; i++) trace.Add(new SectionResistanceValue(prefix + "." + names[i], values[i], i < 3 ? "N" : "Nmm"));
        }
    }
}
