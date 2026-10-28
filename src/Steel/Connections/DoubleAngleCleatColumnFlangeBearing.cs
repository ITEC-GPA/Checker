using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Real supporting column flange for the standard double-angle
    /// shear component. End distances run from the extreme bolt centres to the
    /// ACTUAL column ends, not to the ends of the cleats. Flange strengths belong
    /// to its actual thickness; Column.Fy/Fu belong to the column web. N,mm,MPa.</summary>
    internal sealed class DoubleAngleCleatColumnFlangeBearingInput
    {
        public DoubleAngleCleatComponentInput Cleats { get; }
        public ConnectionRolledISection Column { get; }
        public ConnectionPlate ColumnFlange { get; }
        public double TopBoltEnd { get; }
        public double BottomBoltEnd { get; }
        public string SupportDetailSource { get; }
        public DoubleAngleCleatColumnFlangeBearingInput(DoubleAngleCleatComponentInput cleats,
            ConnectionRolledISection column, double flangeFy, double flangeFu,
            double topBoltEnd, double bottomBoltEnd, string supportDetailSource)
        {
            Cleats = cleats ?? throw new ArgumentNullException(nameof(cleats));
            Column = column ?? throw new ArgumentNullException(nameof(column));
            ColumnFlange = new ConnectionPlate(column.Width, column.FlangeThickness, flangeFy, flangeFu);
            ConnectionValidation.Positive(topBoltEnd, nameof(topBoltEnd));
            ConnectionValidation.Positive(bottomBoltEnd, nameof(bottomBoltEnd));
            TopBoltEnd = topBoltEnd; BottomBoltEnd = bottomBoltEnd; SupportDetailSource = supportDetailSource;
        }
    }

    /// <summary>Supporting-column FLANGE BEARING ONLY for the existing standard
    /// double-angle shear detail. Does not check the support's block/net/yield,
    /// bending, concurrent member actions or parasitic bolt tension. A successful
    /// bearing component does not close phase2c.3 or qualify the whole joint.</summary>
    internal static class DoubleAngleCleatColumnFlangeBearing
    {
        public static ConnectionVerification<DoubleAngleCleatColumnFlangeBearingInput> Verify(DoubleAngleCleatColumnFlangeBearingInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var c = p.Cleats; var issues = new List<string>();
            var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>();
            var cleats = DoubleAngleCleatComponents.Verify(c);
            if (!cleats.ComponentAssessment.IsComplete)
                issues.AddRange(cleats.ComponentAssessment.Diagnostics.Select(d => "Cleats:" + d));
            if (string.IsNullOrWhiteSpace(p.SupportDetailSource)) issues.Add("SUPPORT_DETAIL_SOURCE_REQUIRED");
            double gauge = c.SupportGauge, edge = (p.Column.Width - gauge) / 2;
            double end = Math.Min(p.TopBoltEnd, p.BottomBoltEnd);
            double backClear = (gauge - p.Column.WebThickness) / 2 - p.Column.RootRadius;
            // Both complete support-side legs land on the actual flange. The
            // existing standard cleat model uses a37mm M20 hardware envelope.
            if (p.Column.Width < c.Beam.WebThickness + 2 * c.CleatLeg.Width)
                issues.Add("CLEAT_SUPPORT_LEGS_EXTEND_BEYOND_COLUMN_FLANGE");
            if (backClear < 37d / 2) issues.Add("SUPPORT_HARDWARE_INTERSECTS_COLUMN_WEB_OR_ROOT");
            if (edge < Math.Max(1.2 * c.Bolt.HoleDiameter, 37d / 2)
                || end < Math.Max(1.2 * c.Bolt.HoleDiameter, 37d / 2))
                issues.Add("SUPPORT_MINIMUM_END_OR_EDGE_DISTANCE");
            if (p.ColumnFlange.Fy > 460) issues.Add("SUPPORT_FLANGE_CARBON_STEEL_UP_TO_S460_ONLY");
            double maximumPitch = Math.Min(14 * Math.Min(c.CleatLeg.Thickness, p.Column.FlangeThickness), 200);
            // Within each lapped leg the rows share the existing70mm pitch.
            // The bare gap between separate L legs is not a lapped-plate gauge.
            if (c.Pitch > maximumPitch) issues.Add("SUPPORT_MAXIMUM_LONGITUDINAL_BOLT_PITCH");
            void Value(string name, double value, string unit)
                => trace.Add(new SectionResistanceValue(name, value, unit));
            Value("Support.FlangeThickness", p.ColumnFlange.Thickness, "mm");
            Value("Support.FlangeFy", p.ColumnFlange.Fy, "MPa"); Value("Support.FlangeFu", p.ColumnFlange.Fu, "MPa");
            Value("Support.Gauge", gauge, "mm"); Value("Support.TransverseEdge", edge, "mm");
            Value("Support.TopBoltEnd", p.TopBoltEnd, "mm"); Value("Support.BottomBoltEnd", p.BottomBoltEnd, "mm");
            Value("Support.ConservativeEnd", end, "mm"); Value("Support.HardwareBackClearance", backClear, "mm");
            if (issues.Count == 0)
            {
                var endBolt = BoltBearing.Verify(new BoltBearingInput(c.Code, c.Bolt, p.ColumnFlange, 0,
                    end, edge, c.Pitch, gauge, true, true, c.HoleClearanceServiceabilitySource));
                var innerBolt = BoltBearing.Verify(new BoltBearingInput(c.Code, c.Bolt, p.ColumnFlange, 0,
                    end, edge, c.Pitch, gauge, false, true, c.HoleClearanceServiceabilitySource));
                if (!endBolt.IsComplete || !innerBolt.IsComplete)
                    issues.AddRange(endBolt.Diagnostics.Concat(innerBolt.Diagnostics).Select(d => "SupportBearing:" + d));
                else
                {
                    double resistance = Math.Min(endBolt.Checks.Single().Resistance, innerBolt.Checks.Single().Resistance);
                    Value("Support.BearingResistancePerBolt", resistance, "N");
                    Value("Support.EndBoltResistance", endBolt.Checks.Single().Resistance, "N");
                    Value("Support.InteriorBoltResistance", innerBolt.Checks.Single().Resistance, "N");
                    string source = (c.Code.IsNtc ? "NTC2018 4.2.8.1.1 (4.2.67)" : "EN1993-1-8:2005 Table3.4")
                        + "; actual supporting-column flange; SBE-MS5 4.2.1.2 support-side force model";
                    foreach (bool left in new[] { true, false })
                    {
                        string side = left ? "Left" : "Right";
                        var group = left ? cleats.LeftSupportBolts : cleats.RightSupportBolts;
                        foreach (var force in group.Forces)
                        {
                            string id = side + ".SupportBolt" + (force.Index + 1);
                            checks.Add(new ConnectionCheck(id + ".ColumnFlangeBearing", force.Shear, resistance, source));
                            Value(id + ".TransversePosition", (left ? -1 : 1) * gauge / 2, "mm");
                            Value(id + ".VerticalPosition", force.Position.Y, "mm");
                            Value(id + ".VerticalForce", force.Fy, "N");
                        }
                    }
                }
            }
            if (trace.Any(t => !Finite(t.Value)))
            { issues.Add("SUPPORT_BEARING_TRACE_OUTSIDE_NUMERIC_RANGE"); trace.RemoveAll(t => !Finite(t.Value)); }
            return new ConnectionVerification<DoubleAngleCleatColumnFlangeBearingInput>(p, c.Code,
                "Steel.Connection.DoubleAngleCleat.ColumnFlangeBearing",
                "SUPPORTING COLUMN FLANGE BEARING ONLY: two mirrored standard angle cleats under downward shear. "
                + "No support net/block/yield, local bending, concurrent support actions, prying, rotation or whole-joint qualification.",
                checks, trace, issues, new[] {
                    "Uses the actual support-side bolt forces from the same immutable standard cleat input. Each support bolt has one shear plane; its total bearing force acts on the one physical column flange, without another division by two.",
                    "Support-face coordinates are transverse u=beam Z and vertical v=beam Y; the right-handed normal is -beam X. Left/right rows lie at u=-/+gauge/2.",
                    "Each bolt uses the lesser end/interior capacity and the lesser actual top/bottom end distance. These conservative capacities do not imply a redistribution of bolt forces.",
                    "Support flange strengths are explicit at its actual thickness. The column web strengths are not reused as flange strengths; its root geometry is used only to check hardware clearance.",
                    "The existing SBE0.8 support-bolt shear factor is not an invented extra bearing factor or a solved parasitic tension. Support local resistance and ordinary N+V remain separate unresolved mechanisms.",
                    "This supports the same standard L90x90x10/M20 ordinary-shear family as DoubleAngleCleatComponents. It cannot be substituted for a supporting-web or arbitrary-support model."
                });
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
