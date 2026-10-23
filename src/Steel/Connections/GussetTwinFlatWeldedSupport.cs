using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;
using GPC.Geometry;
using GPC.Model.Results;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Bolted twin-flat transfer followed by a symmetric two-sided fillet-weld
    /// T attachment of its constant-width gusset to a flat support face at X=SupportDistance.
    /// X/Y/Z are the original gusset axes and the bolt-group centroid remains their origin.
    /// SupportFaceWidth is along Y, SupportFaceDepth along Z; support material thickness
    /// is along X. The support patch is centred on Y=Z=0. No support resistance is inferred.
    /// Two equal 45-degree fillets extend along the full gusset width; Throat is a, not leg size.</summary>
    public sealed class GussetTwinFlatWeldedSupportInput
    {
        public GussetTwinFlatTensionInput Transfer { get; }
        public double SupportDistance { get; }
        public double SupportFaceWidth { get; }
        public double SupportFaceDepth { get; }
        public double Throat { get; }
        public FilletWeldSteelGrade GussetGrade { get; }
        public FilletWeldMaterial SupportMaterial { get; }
        public FilletWeldMethod Method { get; }
        public FilletWeldEndCondition EndCondition { get; }
        public string ConsumableCompatibilitySource { get; }
        public string SupportDistributionSource { get; }
        public string ExecutionSource { get; }

        public GussetTwinFlatWeldedSupportInput(GussetTwinFlatTensionInput transfer, double supportDistance,
            double supportFaceWidth, double supportFaceDepth, double throat, FilletWeldSteelGrade gussetGrade,
            FilletWeldMaterial supportMaterial, FilletWeldMethod method, FilletWeldEndCondition endCondition,
            string consumableCompatibilitySource, string supportDistributionSource, string executionSource)
        {
            Transfer = transfer ?? throw new ArgumentNullException(nameof(transfer));
            SupportMaterial = supportMaterial ?? throw new ArgumentNullException(nameof(supportMaterial));
            ConnectionValidation.Positive(supportDistance, nameof(supportDistance));
            ConnectionValidation.Positive(supportFaceWidth, nameof(supportFaceWidth));
            ConnectionValidation.Positive(supportFaceDepth, nameof(supportFaceDepth));
            ConnectionValidation.Positive(throat, nameof(throat));
            if (!Enum.IsDefined(typeof(FilletWeldSteelGrade), gussetGrade)) throw new ArgumentOutOfRangeException(nameof(gussetGrade));
            if (!Enum.IsDefined(typeof(FilletWeldMethod), method)) throw new ArgumentOutOfRangeException(nameof(method));
            if (!Enum.IsDefined(typeof(FilletWeldEndCondition), endCondition)) throw new ArgumentOutOfRangeException(nameof(endCondition));
            SupportDistance = supportDistance; SupportFaceWidth = supportFaceWidth; SupportFaceDepth = supportFaceDepth;
            Throat = throat; GussetGrade = gussetGrade; Method = method; EndCondition = endCondition;
            ConsumableCompatibilitySource = consumableCompatibilitySource;
            SupportDistributionSource = supportDistributionSource; ExecutionSource = executionSource;
        }
    }

    public sealed class GussetTwinFlatWeldedSupportResult
    {
        public ConnectionVerification<GussetTwinFlatWeldedSupportInput> Verification { get; }
        public ConnectionVerification<GussetTwinFlatTensionInput> BoltedTransfer { get; }
        public FilletWeldGroupResult SupportWeld { get; }
        /// <summary>Demand exerted ON the support, in original gusset X/Y/Z, applied at
        /// (SupportDistance,0,0). Sign is opposite to the weld action on the gusset.
        /// This remains a demand, not a verification of the support or its other actions.</summary>
        public PlanarConnectionActions ActionsOnSupport { get; }
        public CoordinateSystem WeldAxes => new CoordinateSystem(
            new Point3d(Verification.Input.SupportDistance, 0, 0), Vector3d.YAxis, Vector3d.ZAxis, Vector3d.XAxis);
        public ConnectionStatus Status => Verification.Status;
        public bool IsComplete => Verification.IsComplete;
        public bool CanQualifyWholeJoint => false;
        public double? Utilization => Verification.Utilization;
        internal GussetTwinFlatWeldedSupportResult(ConnectionVerification<GussetTwinFlatWeldedSupportInput> verification,
            ConnectionVerification<GussetTwinFlatTensionInput> transfer, FilletWeldGroupResult weld, PlanarConnectionActions support)
        { Verification = verification; BoltedTransfer = transfer; SupportWeld = weld; ActionsOnSupport = support; }
    }

    /// <summary>Aggregates the existing bolted load-transfer and weld kernels for one
    /// actual symmetric T detail. The assessment stops at the support face. It does not
    /// establish support-plate bending/punching, member adequacy or joint stiffness.</summary>
    public static class GussetTwinFlatWeldedSupport
    {
        public static GussetTwinFlatWeldedSupportResult Verify(GussetTwinFlatWeldedSupportInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var issues = new List<string>(); var checks = new List<ConnectionCheck>();
            var trace = new List<SectionResistanceValue>(); var t = p.Transfer; var g = t.Gusset;
            var bolts = GussetTwinFlatTension.Verify(t); FilletWeldGroupResult weld = null;
            foreach (var value in bolts.Trace) trace.Add(new SectionResistanceValue("Transfer." + value.Name, value.Value, value.Unit));
            if (!bolts.IsComplete) issues.AddRange(bolts.Diagnostics.Select(d => "Transfer:" + d));
            double leg = Math.Sqrt(2) * p.Throat;
            double braceEndX = (t.Rows - 1.0) * t.Pitch / 2 + t.BraceEnd;
            Gate(p.SupportDistance >= braceEndX + leg, "SupportFilletIntersectsBraceEnds");
            Gate(p.SupportFaceWidth >= g.Width, "SupportFaceDoesNotContainWeldLength");
            Gate(p.SupportFaceDepth >= g.Thickness + 2 * leg, "SupportFaceDoesNotContainFilletLegs");
            Gate(g.Fy <= (int)p.GussetGrade, "GussetNominalGradeBelowSuppliedYieldStrength");
            Gate(!string.IsNullOrWhiteSpace(p.SupportDistributionSource), "SupportDistributionAssessmentRequired");
            Gate(!string.IsNullOrWhiteSpace(p.ExecutionSource), "WeldExecutionAndAccessEvidenceRequired");

            // Transfer.Actions are the bolt forces ON the two braces. The gusset
            // receives their opposite, so equilibrium requires the weld to exert
            // +Actions ON the gusset. The gusset in turn exerts -Actions ON the
            // support. Transport that same six-component wrench before mapping axes.
            var a = t.Actions;
            var sourceAxes = new CoordinateSystem(new Point3d(a.ApplicationX, a.ApplicationY, a.ApplicationZ),
                Vector3d.XAxis, Vector3d.YAxis, Vector3d.ZAxis);
            var resultant = new ResultBeamForces(a.Fz, a.Fx, a.Fy, a.Mz, a.Mx, a.My, sourceAxes);
            var weldAxes = new CoordinateSystem(new Point3d(p.SupportDistance, 0, 0), Vector3d.YAxis, Vector3d.ZAxis, Vector3d.XAxis);
            var rw = resultant.ToCoordinateSystemWithEccentricity(weldAxes);
            var weldActions = new PlanarConnectionActions(rw.V1, rw.V2, rw.N, rw.M1, rw.M2, rw.T);
            // Weld axes (u,v,w)=(Y,Z,X), hence original-axis components are (w,u,v).
            var supportActions = new PlanarConnectionActions(-weldActions.Fz, -weldActions.Fx, -weldActions.Fy,
                -weldActions.Mz, -weldActions.Mx, -weldActions.My, p.SupportDistance, 0, 0);
            Value("SupportDistance", p.SupportDistance, "mm"); Value("BraceFreeEndX", braceEndX, "mm");
            Value("FilletLeg", leg, "mm"); Value("SupportDemand.Fx", supportActions.Fx, "N");
            Value("SupportDemand.Fy", supportActions.Fy, "N"); Value("SupportDemand.Fz", supportActions.Fz, "N");
            Value("SupportDemand.Mx", supportActions.Mx, "Nmm"); Value("SupportDemand.My", supportActions.My, "Nmm");
            Value("SupportDemand.Mz", supportActions.Mz, "Nmm");
            if (issues.Count == 0)
            {
                var roots = new[]
                {
                    new FilletWeldSegment(new ConnectionPoint2D(-g.Width / 2, g.Thickness / 2),
                        new ConnectionPoint2D(g.Width / 2, g.Thickness / 2), p.Throat, p.EndCondition, 3 * Math.PI / 4),
                    new FilletWeldSegment(new ConnectionPoint2D(-g.Width / 2, -g.Thickness / 2),
                        new ConnectionPoint2D(g.Width / 2, -g.Thickness / 2), p.Throat, p.EndCondition, Math.PI / 4)
                };
                var material = new FilletWeldMaterial(p.GussetGrade, g.Fu, g.Thickness);
                weld = FilletWeldGroup.Verify(new FilletWeldGroupInput(t.Code, roots, material, p.SupportMaterial,
                    weldActions, p.Method, p.ConsumableCompatibilitySource, true,
                    "Actual symmetric two-sided90-degree T joint, mirrored throat normals, full concomitant action transport; "
                    + p.SupportDistributionSource + "; " + p.ExecutionSource, lapLength: 0));
                foreach (var value in weld.Trace) trace.Add(new SectionResistanceValue("SupportWeld." + value.Name, value.Value, value.Unit));
                if (!weld.IsComplete) issues.AddRange(weld.Diagnostics.Select(d => "SupportWeld:" + d));
                if (issues.Count == 0)
                {
                    foreach (var k in bolts.Checks) checks.Add(new ConnectionCheck("Transfer." + k.Id, k.Demand, k.Resistance, k.Reference, k.Unit));
                    foreach (var point in weld.Points)
                        foreach (var k in point.Verification.Checks)
                            checks.Add(new ConnectionCheck("SupportWeld.Segment" + point.SegmentIndex + (point.AtStart ? ".Start." : ".End.")
                                + k.Id, k.Demand, k.Resistance, k.Reference, k.Unit));
                }
            }
            var assessment = new ConnectionVerification<GussetTwinFlatWeldedSupportInput>(p, t.Code,
                "Steel.Connection.Gusset.TwinFlatTension.WeldedSupportAttachment",
                "Bolted twin-flat/gusset ULS load transfer plus two symmetric straight fillet welds attaching the constant-width gusset to a flat support face. "
                + "Assessment boundary is the support face; support plate/member strength, support interactions, stiffness and complete-joint qualification are excluded.",
                checks, trace, issues, new[]
                {
                    "The gusset continues in +X beyond the brace free ends; its free end lies in-X, as in GussetTwinFlatTension. Support is on the continuation side.",
                    "The two equal continuous fillets form a90-degree T joint with roots on opposite gusset faces, not a single-sided fillet or a lap weld.",
                    "Support material thickness is used by the weld kernel only. Rectangular face dimensions check geometric containment, not support bending/punching or effective stiffness.",
                    "The supplied support-distribution and execution sources record external assumptions/evidence; they do not turn a weld-group check into support qualification.",
                    "All six actions and the original application point are transported before axis mapping. Unsupported eccentric loading remains blocked by the bolted-transfer component.",
                    "Weld resistance uses EN1993-1-8:2005 4.5.3 or NTC2018 4.2.8.2.4 through the existing common kernel; full-size/nominal-end conventions remain code-specific."
                });
            return new GussetTwinFlatWeldedSupportResult(assessment, bolts, weld, supportActions);
            void Gate(bool condition, string diagnostic) { if (!condition) issues.Add(diagnostic); }
            void Value(string name, double value, string unit)
            { ConnectionNumbers.Finite(value, name); trace.Add(new SectionResistanceValue(name, value, unit)); }
        }
    }
}
