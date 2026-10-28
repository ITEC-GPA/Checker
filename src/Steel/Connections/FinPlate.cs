using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>SCI P358 short standard fin plate, documented in PHASE_2B_SPEC.txt.
    /// EC3/NTC material and bearing rules remain separate; SCI is an explicit complementary joint model.</summary>
    public static class FinPlate
    {
        public static FinPlateResult Verify(FinPlateInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>();
            var issues = new List<string>(); var g = p.Layout; var plate = p.Plate; var beam = p.Beam; var support = p.Support;
            ElasticBoltGroupResult bolts = null; FilletWeldGroupResult welds = null;
            bool detailConforms = false;
            void Gate(bool condition, string diagnostic) { if (!condition) issues.Add(diagnostic); }
            void Value(string name, double value, string unit)
            { ConnectionNumbers.Finite(value, name); trace.Add(new SectionResistanceValue(name, value, unit)); }
            void Check(string id, double demand, double resistance, string reference, string unit = "N")
                => checks.Add(new ConnectionCheck(id, demand, resistance, reference, unit));
            FinPlateResult Result() => new FinPlateResult(new ConnectionVerification<FinPlateInput>(p, p.Code,
                FinPlateResult.MethodId,
                "Ordinary static ULS of a standard short S275 fin plate, 2-5 M20 8.8 bolts in one vertical column, "
                + "two full-size fillet welds and an unnotched laterally restrained rolled I/H beam connected to one side of an I/H column web. "
                + "Includes connected beam web and local support shear/punching under the declared zero pre-existing support action state. "
                + "No frame member/global stability verification, stiffness calculation, tying, fatigue, seismic or fire assessment.",
                checks, trace, issues, new[] {
                    "SCI P358:2011 revised reprint2014, chapter5, is the explicitly selected complementary joint model; it is not a dedicated NTC fin-plate rule.",
                    "Nominally pinned behaviour is conditional on the checked SCI detail and the caller's separate frame-model qualification. No rotational stiffness is calculated or certified.",
                    "Both support-flexibility load-transfer envelopes are evaluated: eccentric bolt group and full-strength welds with V and Vz; their moments are not added together.",
                    "Round-hole non-preloaded product-compliant M20 bolts, no packing/countersunk heads; steel strengths supplied for actual web/plate thicknesses.",
                    "SingleLapWithSingleBoltRow is false for this vertical-shear SCI model: its 2-5 bolt positions lie along the primary force, not transversely to it. No horizontal tying force is accepted.",
                    "Two vertical weld roots on the two plate faces; local secondary effects follow the qualified symmetric SCI detail and full-strength requirement.",
                    "Only one loaded side, unexposed detail and explicitly zero pre-existing support actions; other member/joint actions need a different interaction model.",
                    "Weld consumable and hole-clearance references are caller evidence, not fabrication or SLS checks executed by this method."
                }), bolts, welds, detailConforms);

            // Every physical action is inspected exactly, without a numerical force cutoff.
            Gate(p.Actions.Fx == 0 && p.Actions.Fy == 0 && p.Actions.Fz <= 0
                && p.Actions.Mx == 0 && p.Actions.My == 0 && p.Actions.Mz == 0, "ORDINARY_DOWNWARD_VERTICAL_SHEAR_ONLY");
            Gate(p.ExistingSupportActions.IsZero, "PREEXISTING_SUPPORT_ACTIONS_REQUIRE_INTERACTION_MODEL");
            Gate(p.SupportFace == FinPlateSupportFace.ColumnWeb, "ONLY_IH_COLUMN_WEB_SUPPORT_QUALIFIED");
            Gate(p.SupportLoadedSides == 1, "ONLY_ONE_LOADED_SUPPORT_SIDE_QUALIFIED");
            Gate(!p.BeamNotched, "NOTCHED_BEAM_REQUIRES_CHECKS_4_TO_7");
            Gate(p.BeamLaterallyRestrained, "LATERAL_RESTRAINT_REQUIRED_BY_INITIAL_SCOPE");
            Gate(!p.ExposedToWeather, "EXPOSED_DETAIL_NOT_QUALIFIED");
            Gate(g.Rows >= 2 && g.Rows <= 5 && g.Columns == 1 && g.Gauge == 0, "STANDARD_SINGLE_COLUMN_2_TO_5_ROWS_ONLY");
            Gate(plate.Width == 100 && plate.Thickness == 10 && g.Pitch == 70 && g.TopEdge == 40 && g.BottomEdge == 40
                && g.FreeEdge == 50 && g.BeamTopOffset == 50 && g.Gap == 10 && g.BeamEndEdge == 40,
                "NONSTANDARD_DETAIL_REQUIRES_SEPARATE_QUALIFICATION");
            Gate(plate.Fy == 275 && plate.Fu == 410 && beam.Fy == 275 && beam.Fu == 410 && support.Fy == 275 && support.Fu == 410
                && beam.WebThickness <= 16 && support.WebThickness <= 16, "INITIAL_S275_MATERIAL_THICKNESS_SCOPE");
            Gate(p.Bolt.Diameter == 20 && p.Bolt.HoleDiameter == 22 && p.Bolt.TensileArea == 245
                && p.Bolt.Grade == ConnectionBoltGrade.Class8_8 && p.Bolt.ThreadedShearPlanes,
                "STANDARD_M20_8_8_THREADED_22_MM_HOLES_ONLY");
            Gate(beam.Height <= 610, "DEEP_BEAM_REQUIRES_SEPARATE_DETAIL_QUALIFICATION");
            Gate(g.FirstBoltProjection <= plate.Thickness / .15, "LONG_FIN_PLATE_NOT_QUALIFIED");
            Gate(beam.ClearWeb / beam.WebThickness <= 60 * Math.Sqrt(235 / beam.Fy), "BEAM_SHEAR_BUCKLING_REQUIRES_SEPARATE_MODEL");
            Gate(support.ClearWeb / support.WebThickness <= 40 * Math.Sqrt(235 / support.Fy), "SUPPORT_WEB_SLENDERNESS_OUTSIDE_SCI_SCOPE");
            Gate(beam.Width < support.Height - 2 * support.FlangeThickness - 2 * support.RootRadius,
                "BEAM_WIDTH_DOES_NOT_CLEAR_COLUMN_FLANGES");
            Gate(g.BeamTopOffset >= beam.FlangeThickness + beam.RootRadius
                && g.BeamTopOffset + g.Height <= beam.Height - beam.FlangeThickness - beam.RootRadius,
                "PLATE_INTERSECTS_BEAM_FLANGE_OR_ROOT");
            Gate(p.Weld.EndCondition == FilletWeldEndCondition.FullSize, "FULL_SIZE_WELD_TERMINATIONS_REQUIRED");
            Gate(p.Weld.Throat <= plate.Thickness / Math.Sqrt(2),
                "WELD_THROAT_EXCEEDS_STANDARD_FILLET_GEOMETRY");
            Gate(!string.IsNullOrWhiteSpace(p.Weld.ConsumableCompatibilitySource), "WELD_CONSUMABLE_EVIDENCE_REQUIRED");
            if (p.Code.IsNtc)
                Gate(!string.IsNullOrWhiteSpace(p.HoleClearanceServiceabilitySource), "NTC_22_MM_HOLE_REQUIRES_SLE_EVIDENCE");
            if (issues.Count != 0) return Result();

            double v = -p.Actions.Fz, z = g.CentroidProjection, gm0 = p.Code.Plates.GammaM0, gm2p = p.Code.Plates.GammaM2;
            Gate(!double.IsInfinity(v * z), "ACTION_TRANSPORT_OUTSIDE_NUMERIC_RANGE");
            if (issues.Count != 0) return Result();
            double beamTopEdge = g.BeamTopOffset + g.TopEdge;
            double beamBottomEdge = beam.Height - (beamTopEdge + (g.Rows - 1) * g.Pitch);
            double beamVerticalEdge = Math.Min(beamTopEdge, beamBottomEdge);
            Value("VerticalShear", v, "N"); Value("FirstBoltProjection", g.FirstBoltProjection, "mm");
            Value("BoltCentroidProjection", z, "mm"); Value("TransferredMoment", v * z, "Nmm");
            Value("BeamTopBoltEdge", beamTopEdge, "mm"); Value("BeamBottomBoltEdge", beamBottomEdge, "mm");
            Value("GammaM0", gm0, "1"); Value("GammaM2Plate", gm2p, "1"); Value("GammaM2Connection", p.Code.GammaM2Connection, "1");
            Check("Detail.MinimumPlateHeight", .6 * beam.Height, g.Height, "SCI P358 pp109-114", "mm");
            Check("Detail.MinimumBeamBoltEdge", 2 * p.Bolt.Diameter, Math.Min(beamVerticalEdge, g.BeamEndEdge), "SCI P358 5.3", "mm");
            Check("Detail.Pitch", g.Pitch, Math.Min(14 * Math.Min(plate.Thickness, beam.WebThickness), 200),
                p.Code.IsNtc ? "NTC2018 Table4.2.XVIII" : "EN1993-1-8:2005 Table3.3", "mm");
            // The detail qualification includes EVERY geometric check, not the action-dependent capacities.
            // Evaluate it before checking caller evidence so missing evidence cannot hide a failed detail.
            double requiredThroat = plate.Thickness * plate.Fy * .85 * p.Code.GammaM2Connection / (Math.Sqrt(2) * Math.Min(plate.Fu, support.Fu));
            detailConforms = checks.All(c => c.Passed) && p.Weld.Throat >= requiredThroat;
            Gate(!string.IsNullOrWhiteSpace(p.PinnedModelQualificationSource), "PINNED_FRAME_MODEL_QUALIFICATION_REQUIRED");
            if (issues.Count != 0) return Result();

            var positions = Enumerable.Range(0, g.Rows).Select(i => new ConnectionPoint2D(0,
                (g.Rows - 1) * g.Pitch / 2 - i * g.Pitch)).ToArray();
            // Component basis (X,Z,-Y), force at the support face: eccentricity creates +Mz in this basis.
            bolts = ElasticBoltGroup.Calculate(positions, new PlanarConnectionActions(0, -v, 0, 0, 0, 0, -z, 0));
            if (!bolts.IsSupported) { issues.AddRange(bolts.Diagnostics.Select(s => "BOLT_DISTRIBUTION: " + s)); return Result(); }
            Value("BoltGroup.MzAtCentroid", bolts.ActionsAtCentroid.Mz, "Nmm");

            // Conservative minimum over end/interior positions, exactly the SCI group-bearing envelope.
            // n2=1: no pitch in the horizontal force direction; the vertical pitch becomes transverse there.
            double Bearing(ConnectionPlate ply, double eVertical, double eHorizontal, bool vertical, string label)
            {
                var end = BoltBearing.Verify(new BoltBearingInput(p.Code, p.Bolt, ply, 0,
                    vertical ? eVertical : eHorizontal, vertical ? eHorizontal : eVertical,
                    vertical ? (double?)g.Pitch : null, vertical ? null : (double?)g.Pitch,
                    true, true, p.HoleClearanceServiceabilitySource));
                if (!end.IsComplete) { issues.AddRange(end.Diagnostics.Select(s => label + ": " + s)); return 0; }
                double resistance = end.Checks.Single().Resistance;
                if (vertical)
                {
                    var inner = BoltBearing.Verify(new BoltBearingInput(p.Code, p.Bolt, ply, 0,
                        eVertical, eHorizontal, g.Pitch, null, false, true, p.HoleClearanceServiceabilitySource));
                    if (!inner.IsComplete) { issues.AddRange(inner.Diagnostics.Select(s => label + ": " + s)); return 0; }
                    resistance = Math.Min(resistance, inner.Checks.Single().Resistance);
                }
                Value(label + ".Resistance", resistance, "N"); return resistance;
            }
            var beamPly = new ConnectionPlate(beam.Height, beam.WebThickness, beam.Fy, beam.Fu);
            double plateVertical = Bearing(plate, Math.Min(g.TopEdge, g.BottomEdge), g.FreeEdge, true, "PlateBearing.Vertical");
            double plateHorizontal = Bearing(plate, Math.Min(g.TopEdge, g.BottomEdge), g.FreeEdge, false, "PlateBearing.Horizontal");
            double beamVertical = Bearing(beamPly, beamVerticalEdge, g.BeamEndEdge, true, "BeamBearing.Vertical");
            double beamHorizontal = Bearing(beamPly, beamVerticalEdge, g.BeamEndEdge, false, "BeamBearing.Horizontal");
            if (issues.Count != 0) return Result();
            foreach (var force in bolts.Forces)
            {
                string id = "Bolt" + (force.Index + 1);
                var strength = BoltStrength.Verify(new BoltStrengthInput(p.Code, p.Bolt, force.Shear, 0,
                    jointLength: (g.Rows - 1) * g.Pitch, holeClearanceServiceabilitySource: p.HoleClearanceServiceabilitySource), out var strengthValues);
                if (!strength.IsComplete) { issues.AddRange(strength.Diagnostics.Select(s => id + ": " + s)); continue; }
                Check(id + ".Shear", force.Shear, strengthValues.ShearResistance, strengthValues.Reference);
                Check(id + ".PlateBearingInteraction", ElasticBoltGroup.Hypot(force.Fx / plateHorizontal, force.Fy / plateVertical),
                    1, "SCI P358 Check2; " + (p.Code.IsNtc ? "NTC2018 (4.2.67)" : "EN1993-1-8 Table3.4"), "1");
                Check(id + ".BeamBearingInteraction", ElasticBoltGroup.Hypot(force.Fx / beamHorizontal, force.Fy / beamVertical),
                    1, "SCI P358 Check2; " + (p.Code.IsNtc ? "NTC2018 (4.2.67)" : "EN1993-1-8 Table3.4"), "1");
                Value(id + ".HorizontalForce", force.Fx, "N"); Value(id + ".VerticalForce", force.Fy, "N");
            }
            if (issues.Count != 0) return Result();

            double avNet = plate.Thickness * (g.Height - g.Rows * p.Bolt.HoleDiameter);
            double ant = plate.Thickness * (g.FreeEdge - p.Bolt.HoleDiameter / 2);
            double anv = plate.Thickness * (g.Height - g.TopEdge - (g.Rows - .5) * p.Bolt.HoleDiameter);
            double wel = plate.Thickness * g.Height * g.Height / 6;
            Value("Plate.AvNet", avNet, "mm2"); Value("Plate.Ant", ant, "mm2"); Value("Plate.Anv", anv, "mm2"); Value("Plate.Wel", wel, "mm3");
            Check("Plate.GrossShear", v, PlasticShear.Resistance(g.Height * plate.Thickness, plate.Fy, 1.27, gm0), "SCI P358 Check3 p118, nominal-moment factor1.27");
            Check("Plate.NetShear", v, PlasticShear.Resistance(avNet, plate.Fu, gm2p), "SCI P358 Check3 p118");
            Check("Plate.BlockTearing", v, .5 * plate.Fu * ant / gm2p + PlasticShear.Resistance(plate.Fy, anv, gm0),
                "SCI P358 Check3 p118; EN1993-1-8 3.10.2, eccentric block");
            Check("Plate.ShortPlateFlexure", v * z, wel * plate.Fy / gm0, "SCI P358 Check3 p119, short plate; finite elastic bound", "Nmm");
            Value("Plate.ShortPlateLimit", plate.Thickness / .15, "mm");

            double avBeam = Math.Max(beam.Area - 2 * beam.Width * beam.FlangeThickness
                + (beam.WebThickness + 2 * beam.RootRadius) * beam.FlangeThickness,
                (beam.Height - 2 * beam.FlangeThickness) * beam.WebThickness);
            double avNetBeam = avBeam - g.Rows * p.Bolt.HoleDiameter * beam.WebThickness;
            Value("Beam.Av", avBeam, "mm2"); Value("Beam.AvNet", avNetBeam, "mm2");
            Check("Beam.GrossShear", v, PlasticShear.Resistance(avBeam, beam.Fy, gm0), "SCI P358 Check4 p121, unnotched rolled I/H");
            Check("Beam.NetShear", v, PlasticShear.Resistance(avNetBeam, beam.Fu, gm2p), "SCI P358 Check4 p121, unnotched rolled I/H");

            // Component basis (Y,Z,X). The moment is bending Mx, not planar torsion Mz.
            double halfHeight = g.Height / 2, halfThickness = plate.Thickness / 2;
            var segments = new[] {
                new FilletWeldSegment(new ConnectionPoint2D(-halfThickness, -halfHeight), new ConnectionPoint2D(-halfThickness, halfHeight),
                    p.Weld.Throat, p.Weld.EndCondition, Math.PI / 4),
                new FilletWeldSegment(new ConnectionPoint2D(halfThickness, -halfHeight), new ConnectionPoint2D(halfThickness, halfHeight),
                    p.Weld.Throat, p.Weld.EndCondition, 3 * Math.PI / 4)
            };
            welds = FilletWeldGroup.Verify(new FilletWeldGroupInput(p.Code, segments,
                new FilletWeldMaterial(FilletWeldSteelGrade.S275, plate.Fu, plate.Thickness),
                new FilletWeldMaterial(FilletWeldSteelGrade.S275, support.Fu, support.WebThickness),
                new PlanarConnectionActions(0, -v, 0, v * z, 0, 0), FilletWeldMethod.Directional,
                p.Weld.ConsumableCompatibilitySource, true, "SCI P358 standard symmetric two-sided fin plate, chapter5 and AppendixC"));
            if (!welds.IsComplete) { issues.AddRange(welds.Diagnostics.Select(s => "WELD: " + s)); return Result(); }
            foreach (var point in welds.Points)
                foreach (var check in point.Verification.Checks)
                    Check("Weld" + (point.SegmentIndex + 1) + (point.AtStart ? ".Start." : ".End.") + check.Id,
                        check.Demand, check.Resistance, check.Reference, check.Unit);
            foreach (var item in welds.Trace) Value("WeldGroup." + item.Name, item.Value, item.Unit);
            Check("Weld.FullStrengthThroat", requiredThroat, p.Weld.Throat, "SCI P358 AppendixC.1/C.3, unrounded full-strength rule", "mm");

            double supportArea = g.Height * support.WebThickness;
            Value("Support.LocalShearArea", supportArea, "mm2");
            Value("Support.WebSlenderness", support.ClearWeb / support.WebThickness, "1");
            Value("Support.ConservativePlateThicknessLimit", support.WebThickness * support.Fu / (plate.Fy * gm2p), "mm");
            Check("Support.LocalShear", v / 2, PlasticShear.Resistance(supportArea, support.Fy, gm0), "SCI P358 Check10 p128");
            Check("Support.Punching", v, support.WebThickness * support.Fu * g.Height * g.Height / (6 * z * gm2p),
                "SCI P358 Check10 p128, actual-load form; SCI AD422 (gammaM2_plate)");
            return Result();
        }
    }
}
