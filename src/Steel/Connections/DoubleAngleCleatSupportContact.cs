using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Supplementary short-leg contact model. ContactHeight/Width describe
    /// the actual rectangular upper cleat-to-BEAM-WEB contact patch on EACH side.
    /// They are not an effective column-flange width. N, mm, MPa.</summary>
    public sealed class DoubleAngleCleatSupportContactInput
    {
        public DoubleAngleCleatColumnFlangeBearingInput Support { get; }
        public double ContactHeight { get; }
        public double ContactWidth { get; }
        public double MeanHeadNutDiameter { get; }
        public string ContactGeometrySource { get; }
        public DoubleAngleCleatSupportContactInput(DoubleAngleCleatColumnFlangeBearingInput support,
            double contactHeight, double contactWidth, double meanHeadNutDiameter, string contactGeometrySource)
        {
            Support = support ?? throw new ArgumentNullException(nameof(support));
            ConnectionValidation.Positive(contactHeight, nameof(contactHeight));
            ConnectionValidation.Positive(contactWidth, nameof(contactWidth));
            ConnectionValidation.Positive(meanHeadNutDiameter, nameof(meanHeadNutDiameter));
            ContactHeight = contactHeight; ContactWidth = contactWidth;
            MeanHeadNutDiameter = meanHeadNutDiameter; ContactGeometrySource = contactGeometrySource;
        }
    }

    public sealed class DoubleAngleCleatSupportContactResult
    {
        public ConnectionVerification<DoubleAngleCleatSupportContactInput> ContactAndShear { get; }
        public DoubleAngleCleatComponentResult Cleats { get; }
        public ConnectionVerification<DoubleAngleCleatColumnFlangeBearingInput> ColumnBearing { get; }
        public IReadOnlyList<ConnectionCheck> PunchingHierarchy { get; }
        public bool PunchingExcludedBeforeBoltTensionResistance => PunchingHierarchy.Count == 2 && PunchingHierarchy.All(x => x.Passed);
        public bool CanQualifyWholeJoint => false;
        public ConnectionStatus Status => ContactAndShear.Status == ConnectionStatus.Failed
            || Cleats.ComponentAssessment.Status == ConnectionStatus.Failed || ColumnBearing.Status == ConnectionStatus.Failed
            ? ConnectionStatus.Failed : ConnectionStatus.NotSupported;
        public IReadOnlyList<string> MissingWholeJointChecks { get; } = Array.AsReadOnly(new[] {
            "BOLT_NORMAL_FORCE_AND_PRYING_MODEL", "COLUMN_LOCAL_RESISTANCE_AND_CONCURRENT_ACTIONS", "FRAME_ROTATION_QUALIFICATION" });
        internal DoubleAngleCleatSupportContactResult(ConnectionVerification<DoubleAngleCleatSupportContactInput> r,
            DoubleAngleCleatComponentResult c, ConnectionVerification<DoubleAngleCleatColumnFlangeBearingInput> b, IEnumerable<ConnectionCheck> hierarchy)
        { ContactAndShear = r; Cleats = c; ColumnBearing = b; PunchingHierarchy = Array.AsReadOnly(hierarchy.ToArray()); }
    }

    /// <summary>Weynand/Oerder/Demonceau, Resistance tables for standardized joints,
    /// section3.1, Figures7-9. Solves the transverse IN-PLANE bolt forces for a
    /// supplied upper contact patch and an elastic force distribution. It does not
    /// infer bolt-axis tension from these forces or from the SBE0.8 factor.</summary>
    public static class DoubleAngleCleatSupportContact
    {
        public static DoubleAngleCleatSupportContactResult Verify(DoubleAngleCleatSupportContactInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var s = p.Support; var c = s.Cleats;
            var cleats = DoubleAngleCleatComponents.Verify(c);
            var bearing = DoubleAngleCleatColumnFlangeBearing.Verify(s);
            var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>(); var issues = new List<string>();
            var hierarchy = new List<ConnectionCheck>();
            if (!cleats.ComponentAssessment.IsComplete) issues.AddRange(cleats.ComponentAssessment.Diagnostics);
            if (!bearing.IsComplete) issues.AddRange(bearing.Diagnostics);
            if (string.IsNullOrWhiteSpace(p.ContactGeometrySource)) issues.Add("ACTUAL_SHORT_LEG_CONTACT_PATCH_REQUIRED");
            // The upper bolt is40mm below the cleat end. The contact zone remains
            // entirely above it; its X footprint clears the10mm beam-end gap and root.
            if (p.ContactHeight > 40 - c.Bolt.HoleDiameter / 2) issues.Add("CONTACT_ZONE_REACHES_TOP_BOLT");
            if (p.ContactWidth > c.Eccentricity - c.Gap - c.CleatLeg.Thickness - c.CleatRootRadius)
                issues.Add("CONTACT_WIDTH_OUTSIDE_STRAIGHT_SHORT_LEG_PATCH");
            if (p.MeanHeadNutDiameter <= c.Bolt.HoleDiameter || p.MeanHeadNutDiameter > 37)
                issues.Add("ACTUAL_HEAD_NUT_DIAMETER_OUTSIDE_STANDARD_HARDWARE");
            if (issues.Count == 0)
            {
                double v = -c.Actions.Fy, moment = v / 2 * c.Eccentricity;
                var z = Enumerable.Range(0, c.Rows).Select(i => 40 + i * c.Pitch - p.ContactHeight / 2).ToArray();
                double sum2 = z.Sum(x => x * x), transverse = moment * z.Sum() / sum2;
                Value("EachCleat.Moment", moment, "Nmm"); Value("EachCleat.ContactForce", transverse, "N");
                Value("EachCleat.BoltLeverSquares", sum2, "mm2");
                double stress = transverse / (p.ContactHeight * p.ContactWidth);
                if (double.IsNaN(stress) || double.IsInfinity(stress))
                { issues.Add("CONTACT_STRESS_OUTSIDE_NUMERIC_RANGE"); return Finish(); }
                checks.Add(new ConnectionCheck("BeamWebContact.ElasticCompression", stress,
                    Math.Min(c.CleatLeg.Fy, c.Beam.Fy) / c.Code.Plates.GammaM0,
                    "Weynand/Oerder/Demonceau section3.1 Fig8-9; rectangular contact patch, elastic compression only", "MPa"));
                var bolt = BoltStrength.Verify(new BoltStrengthInput(c.Code, c.Bolt, 0, 0,
                    jointLength: (c.Rows - 1) * c.Pitch, holeClearanceServiceabilitySource: c.HoleClearanceServiceabilitySource));
                double fv = bolt.Checks.Single(x => x.Id == "Bolt.Shear").Resistance;
                double ft = bolt.Checks.Single(x => x.Id == "Bolt.Tension").Resistance;
                // Sufficient punching hierarchy. It is independent of a presumed
                // prying force and is NOT a verification that the bolt can carry Ft+V.
                foreach (var ply in new[] { c.CleatLeg, s.ColumnFlange })
                {
                    string label = ReferenceEquals(ply, c.CleatLeg) ? "Cleat" : "Column";
                    var punch = BoltPunching.Verify(new BoltPunchingInput(c.Code, ply, ft, c.Bolt.HoleDiameter, p.MeanHeadNutDiameter));
                    hierarchy.Add(new ConnectionCheck(label + ".PunchingCapacityHierarchy", ft, punch.Checks.Single().Resistance,
                        "EN1993-1-8 Table3.4 / NTC2018 4.2.70; sufficient Bp,Rd>=Ft,Rd, not an applied bolt tension"));
                }
                double edge = (s.Column.Width - c.SupportGauge) / 2;
                double columnY = Bearing(s.ColumnFlange, Math.Min(s.TopBoltEnd, s.BottomBoltEnd), edge, c.Pitch, null, true);
                double columnX = Bearing(s.ColumnFlange, edge, Math.Min(s.TopBoltEnd, s.BottomBoltEnd), null, c.Pitch, false);
                double cleatY = Bearing(c.CleatLeg, 40, c.FreeEdge, c.Pitch, null, true);
                double cleatX = Bearing(c.CleatLeg, c.FreeEdge, 40, null, c.Pitch, false);
                for (int i = 0; i < z.Length; i++)
                {
                    double horizontal = moment * z[i] / sum2, vertical = v / (2 * c.Rows);
                    foreach (int sign in new[] { -1, 1 })
                    {
                        string id = (sign < 0 ? "Left" : "Right") + ".Bolt" + (i + 1);
                        Value(id + ".TransverseShear", sign * horizontal, "N"); Value(id + ".VerticalShear", -vertical, "N");
                        Value(id + ".ContactLever", z[i], "mm");
                        checks.Add(new ConnectionCheck(id + ".ResultantShear", ElasticBoltGroup.Hypot(horizontal, vertical), fv,
                            "Weynand/Oerder/Demonceau Fig9; EN1993-1-8 Table3.4,3.8"));
                        checks.Add(new ConnectionCheck(id + ".ColumnBearing", horizontal / columnX + vertical / columnY, 1,
                            "Conservative directional linear bearing interaction; EN1993-1-8 Table3.4 / NTC4.2.67", "1"));
                        checks.Add(new ConnectionCheck(id + ".CleatBearing", horizontal / cleatX + vertical / cleatY, 1,
                            "Conservative directional linear bearing interaction; EN1993-1-8 Table3.4 / NTC4.2.67", "1"));
                    }
                }
            }
            return Finish();
            DoubleAngleCleatSupportContactResult Finish() => new DoubleAngleCleatSupportContactResult(new ConnectionVerification<DoubleAngleCleatSupportContactInput>(p, c.Code,
                "Steel.Connection.DoubleAngleCleat.ShortLegContact", "Supplementary contact and support IN-PLANE shear components; no normal-bolt-force, prying or whole-joint qualification.",
                checks, trace, issues, new[] { "Contact dimensions are actual model inputs; this method does not derive them from a stiffness or plasticity solution.",
                    "The original SBE component checks remain separately available. The transverse forces here are NOT bolt-axis tension.",
                    "Punching hierarchy is sufficient only: failure of the hierarchy does not prove punching failure under the actual unknown tension." }), cleats, bearing, hierarchy);

            double Bearing(ConnectionPlate ply, double e1, double e2, double? p1, double? p2, bool vertical)
            {
                double cap = double.MaxValue;
                foreach (bool end in vertical ? new[] { true, false } : new[] { true })
                foreach (bool edgeBolt in !vertical && c.Rows > 2 ? new[] { true, false } : new[] { true })
                {
                    var r = BoltBearing.Verify(new BoltBearingInput(c.Code, c.Bolt, ply, 0, e1, e2, p1, p2,
                        end, edgeBolt, c.HoleClearanceServiceabilitySource));
                    if (!r.IsComplete) { issues.AddRange(r.Diagnostics); return 1; }
                    cap = Math.Min(cap, r.Checks.Single().Resistance);
                }
                return cap;
            }
            void Value(string name, double value, string unit) => trace.Add(new SectionResistanceValue(name, value, unit));
        }
    }
}
