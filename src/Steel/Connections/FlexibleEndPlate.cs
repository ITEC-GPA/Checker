using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>SCI P358 partial-depth end plate, ordinary static shear. Full-strength
    /// weld hierarchy without the optional UK factor0.8; long-joint reduction retained.
    /// Both are explicit conservative choices, not errors calibrated to the examples.</summary>
    public static class FlexibleEndPlate
    {
        public static FlexibleEndPlateResult Verify(FlexibleEndPlateInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>();
            var issues = new List<string>(); var plate = p.Plate; var beam = p.Beam; var support = p.Support;
            ElasticBoltGroupResult bolts = null; FilletWeldGroupResult welds = null;
            bool conforms = false; double? supportBearingPerBolt = null;
            void Gate(bool condition, string reason) { if (!condition) issues.Add(reason); }
            void Value(string name, double value, string unit)
            { ConnectionNumbers.Finite(value, name); trace.Add(new SectionResistanceValue(name, value, unit)); }
            void Check(string id, double demand, double resistance, string source, string unit = "N")
                => checks.Add(new ConnectionCheck(id, demand, resistance, source, unit));
            void Detail(string id, double demand, double resistance, string source, string unit = "N")
                => checks.Add(new ConnectionCheck(id, demand, resistance, source, unit, ConnectionCheckRole.Detailing));
            FlexibleEndPlateResult Result() => new FlexibleEndPlateResult(new ConnectionVerification<FlexibleEndPlateInput>(
                p, p.Code, "Steel.Connection.FlexibleEndPlate.PartialDepth.ColumnWeb",
                "Ordinary static downward shear, partial-depth S275 plate, two symmetric columns of M20 8.8 bolts, "
                + "unnotched restrained I/H beam, one loaded side of a column web and explicitly zero pre-existing support actions. "
                + "Includes local beam web, bolts, plate, full-strength fillet welds, support bearing and local shear. "
                + "No global member verification, tying, ordinary tension, rotation curve, fatigue, seismic or fire verification.",
                checks, trace, issues, new[] {
                    "SCI P358 chapter4 is a complementary analytical/detail model, not a dedicated NTC equation.",
                    "The factor0.8 on bolt shear and1.27 on gross plate shear account for nominal connection bending in this SCI model.",
                    "The full-strength weld requirement omits the additional UK-only0.8 reduction; EN3.8 long-joint reduction is retained.",
                    "Support top/bottom edges refer to the actual column ends, not the plate ends. The initial model has no other support actions.",
                    "Conformity to a detail is separate from the caller's qualification of nominally pinned frame behaviour; no stiffness is calculated.",
                    "Only normal round holes, no packing or preloading, unexposed detail and full-size weld ends. Execution and clearance sources are caller evidence."
                }), bolts, welds, conforms, supportBearingPerBolt);

            var a = p.Actions;
            Gate(a.Fx == 0 && a.Fy <= 0 && a.Fz == 0 && a.Mx == 0 && a.My == 0 && a.Mz == 0
                && a.ApplicationX == 0 && a.ApplicationY == 0 && a.ApplicationZ == 0, "CENTRED_DOWNWARD_SHEAR_ONLY");
            Gate(Zero(p.ExistingSupportActions), "PREEXISTING_SUPPORT_ACTIONS_REQUIRE_INTERACTION_MODEL");
            Gate(!p.BeamNotched, "NOTCHED_BEAM_NOT_QUALIFIED");
            Gate(p.BeamLaterallyRestrained, "LATERAL_RESTRAINT_REQUIRED");
            Gate(p.Rows >= 2 && p.Rows <= 6 && (p.Gauge == 90 || p.Gauge == 140)
                && plate.Width == p.Gauge + 60 && (plate.Thickness == 10 || plate.Thickness == 12), "NONSTANDARD_DETAIL_NOT_QUALIFIED");
            Gate(plate.Fy == 275 && plate.Fu == 410 && beam.Fy == 275 && beam.Fu == 410
                && (support.Fy == 265 || support.Fy == 275) && support.Fu == 410
                && beam.WebThickness >= 4 && beam.WebThickness <= 16 && support.WebThickness >= 4 && support.WebThickness <= 16,
                "INITIAL_S275_MATERIAL_THICKNESS_SCOPE");
            Gate(p.Bolt.Diameter == 20 && p.Bolt.HoleDiameter == 22 && p.Bolt.TensileArea == 245
                && p.Bolt.Grade == ConnectionBoltGrade.Class8_8 && p.Bolt.ThreadedShearPlanes, "STANDARD_M20_8_8_ONLY");
            Gate(beam.Height <= 610 && p.PlateTopOffset >= beam.FlangeThickness + beam.RootRadius
                && p.PlateTopOffset + p.Height <= beam.Height - beam.FlangeThickness - beam.RootRadius,
                "PLATE_INTERSECTS_BEAM_ROOT_OR_BEAM_TOO_DEEP");
            Gate(beam.Width < support.ClearWeb && plate.Width < support.ClearWeb, "ASSEMBLY_DOES_NOT_CLEAR_COLUMN_FLANGES");
            Gate(beam.ClearWeb / beam.WebThickness <= 60 * Math.Sqrt(235 / beam.Fy), "BEAM_SHEAR_BUCKLING_REQUIRES_SEPARATE_MODEL");
            Gate(support.ClearWeb / support.WebThickness <= 40 * Math.Sqrt(235 / support.Fy), "SUPPORT_WEB_SLENDERNESS_OUTSIDE_SCOPE");
            Gate(p.SupportTopBoltEdge >= 1.2 * p.Bolt.HoleDiameter
                && p.SupportBottomBoltEdge >= Math.Min(p.Gauge / 2, 5 * p.Bolt.Diameter), "COLUMN_END_TOO_CLOSE_FOR_SUPPORT_MODEL");
            Gate(p.WeldEnds == FilletWeldEndCondition.FullSize, "FULL_SIZE_WELD_ENDS_REQUIRED");
            Gate(p.WeldThroat >= 3 && p.WeldThroat <= Math.Min(plate.Thickness, beam.WebThickness) / Math.Sqrt(2), "WELD_THROAT_OUTSIDE_FILLET_SCOPE");
            Gate(!string.IsNullOrWhiteSpace(p.ConsumableCompatibilitySource), "CONSUMABLE_EVIDENCE_REQUIRED");
            if (p.Code.IsNtc) Gate(!string.IsNullOrWhiteSpace(p.HoleClearanceServiceabilitySource), "NTC_HOLE_CLEARANCE_EVIDENCE_REQUIRED");
            if (issues.Count != 0) return Result();

            double v = -a.Fy, gm0 = p.Code.Plates.GammaM0, gm2 = p.Code.Plates.GammaM2;
            double minimumThroat = beam.WebThickness * beam.Fy * .85 * p.Code.GammaM2Connection
                / (Math.Sqrt(2) * Math.Min(beam.Fu, plate.Fu));
            Detail("Detail.MinimumHeight", .6 * beam.Height, p.Height, "SCI P358 Check1", "mm");
            double maximumSpacing = Math.Min(14 * Math.Min(plate.Thickness, support.WebThickness), 200);
            Detail("Detail.Pitch", p.Pitch, maximumSpacing, "EN1993-1-8 Table3.3 / NTC Table4.2.XVIII", "mm");
            Detail("Detail.Gauge", p.Gauge, maximumSpacing, "EN1993-1-8 Table3.3 / NTC Table4.2.XVIII", "mm");
            Detail("Weld.FullStrengthThroat", minimumThroat, p.WeldThroat, "SCI P358 AppendixC.1 without optional UK end-plate factor0.8", "mm");
            conforms = checks.All(c => c.Passed);
            Gate(!string.IsNullOrWhiteSpace(p.PinnedModelQualificationSource), "PINNED_FRAME_MODEL_QUALIFICATION_REQUIRED");
            if (issues.Count != 0) return Result();

            var positions = Enumerable.Range(0, p.Rows).SelectMany(i => new[] {
                new ConnectionPoint2D(-p.Gauge / 2, (p.Rows - 1) * p.Pitch / 2 - i * p.Pitch),
                new ConnectionPoint2D(p.Gauge / 2, (p.Rows - 1) * p.Pitch / 2 - i * p.Pitch) }).ToArray();
            bolts = ElasticBoltGroup.Calculate(positions, a);
            if (!bolts.IsSupported) { issues.AddRange(bolts.Diagnostics); return Result(); }
            var bolt = BoltStrength.Verify(new BoltStrengthInput(p.Code, p.Bolt, v / (2 * p.Rows), 0,
                jointLength: (p.Rows - 1) * p.Pitch, holeClearanceServiceabilitySource: p.HoleClearanceServiceabilitySource), out var boltValues);
            if (!bolt.IsComplete) { issues.AddRange(bolt.Diagnostics); return Result(); }
            double singleShear = boltValues.ShearResistance;
            Value("Bolt.BetaLongJoint", boltValues.BetaLongJoint, "1");
            Value("Bolt.SciParasiticTensionFactor", .8, "1");
            Check("BoltGroup.ShearWithParasiticTension", v, 2 * p.Rows * .8 * singleShear, "SCI P358 Check8; EN1993-1-8 Table3.4 and3.8");
            double Bearing(ConnectionPlate part, double e1, double e2, string prefix)
            {
                var end = BoltBearing.Verify(new BoltBearingInput(p.Code, p.Bolt, part, 0, e1, e2,
                    p.Pitch, p.Gauge, true, true, p.HoleClearanceServiceabilitySource), out var endValues);
                var inner = BoltBearing.Verify(new BoltBearingInput(p.Code, p.Bolt, part, 0, e1, e2,
                    p.Pitch, p.Gauge, false, true, p.HoleClearanceServiceabilitySource), out var innerValues);
                if (!end.IsComplete || !inner.IsComplete)
                { issues.AddRange(end.Diagnostics.Concat(inner.Diagnostics).Select(s => prefix + ":" + s)); return 0; }
                Value(prefix + ".AlphaB", Math.Min(endValues.AlphaB, innerValues.AlphaB), "1");
                Value(prefix + ".K1", endValues.K1, "1");
                return Math.Min(endValues.Resistance, innerValues.Resistance);
            }
            double plateBearing = Bearing(plate, 40, 30, "PlateBearing");
            double supportBearing = Bearing(new ConnectionPlate(support.ClearWeb, support.WebThickness, support.Fy, support.Fu),
                Math.Min(p.SupportTopBoltEdge, p.SupportBottomBoltEdge), (support.ClearWeb - p.Gauge) / 2, "SupportBearing");
            if (issues.Count != 0) return Result();
            Value("PlateBearing.PerBolt", plateBearing, "N"); Value("SupportBearing.PerBolt", supportBearing, "N"); supportBearingPerBolt = supportBearing;
            foreach (var force in bolts.Forces)
            {
                Check("Bolt" + (force.Index + 1) + ".PlateBearing", force.Shear, plateBearing, "SCI P358 Check8; " + (p.Code.IsNtc ? "NTC4.2.67" : "EN Table3.4"));
                Check("Bolt" + (force.Index + 1) + ".SupportBearing", force.Shear, supportBearing, "SCI P358 Check8; " + (p.Code.IsNtc ? "NTC4.2.67" : "EN Table3.4"));
            }

            double av = .9 * p.Height * beam.WebThickness;
            Value("Beam.EffectiveShearArea", av, "mm2");
            Check("Beam.WebShear", v, PlasticShear.Resistance(av, beam.Fy, gm0), "SCI P358 Check4/SN014, effective factor0.9");
            double avNet = plate.Thickness * (p.Height - p.Rows * p.Bolt.HoleDiameter);
            double ant = plate.Thickness * (30 - p.Bolt.HoleDiameter / 2);
            double anv = plate.Thickness * (p.Height - 40 - (p.Rows - .5) * p.Bolt.HoleDiameter);
            bool shortPlate = p.Height < 1.36 * p.Gauge;
            double blockFactor = shortPlate && p.Rows > 1 ? .5 : 1;
            Value("Plate.NetAreaPerSide", avNet, "mm2"); Value("Plate.AntPerSide", ant, "mm2");
            Value("Plate.AnvPerSide", anv, "mm2"); Value("Plate.BlockTensionFactor", blockFactor, "1");
            Check("Plate.GrossShear", v, PlasticShear.Resistance(2 * p.Height * plate.Thickness, plate.Fy, 1.27, gm0), "SCI P358 Check9, nominal bending factor1.27");
            Check("Plate.NetShear", v, PlasticShear.Resistance(2 * avNet, plate.Fu, gm2), "SCI P358 Check9");
            Check("Plate.BlockTearing", v, 2 * (blockFactor * ant * plate.Fu / gm2 + PlasticShear.Resistance(anv, plate.Fy, gm0)), "SCI P358 Check9; EN1993-1-8 3.10.2");
            if (shortPlate) Check("Plate.InPlaneBending", v,
                2 * plate.Thickness * p.Height * p.Height * plate.Fy / (3 * (p.Gauge - beam.WebThickness) * gm0), "SCI P358 Check9, hp<1.36g");

            double halfHeight = p.Height / 2, halfWeb = beam.WebThickness / 2;
            welds = FilletWeldGroup.Verify(new FilletWeldGroupInput(p.Code, new[] {
                new FilletWeldSegment(new ConnectionPoint2D(-halfWeb,-halfHeight),new ConnectionPoint2D(-halfWeb,halfHeight),p.WeldThroat,p.WeldEnds,Math.PI/4),
                new FilletWeldSegment(new ConnectionPoint2D(halfWeb,-halfHeight),new ConnectionPoint2D(halfWeb,halfHeight),p.WeldThroat,p.WeldEnds,3*Math.PI/4) },
                new FilletWeldMaterial(FilletWeldSteelGrade.S275, beam.Fu, beam.WebThickness),
                new FilletWeldMaterial(FilletWeldSteelGrade.S275, plate.Fu, plate.Thickness), a, FilletWeldMethod.Directional,
                p.ConsumableCompatibilitySource, true, "SCI partial-depth symmetric double web fillets; full-strength hierarchy accounts for nominal end bending"));
            if (!welds.IsComplete) { issues.AddRange(welds.Diagnostics); return Result(); }
            foreach (var point in welds.Points)
                foreach (var item in point.Verification.Checks)
                    Check("Weld" + (point.SegmentIndex + 1) + (point.AtStart ? ".Start." : ".End.") + item.Id,
                        item.Demand, item.Resistance, item.Reference, item.Unit);
            foreach (var item in welds.Trace) Value("Weld." + item.Name, item.Value, item.Unit);
            AddSupportShear(p, p.Rows, v, "Support", checks, trace);
            return Result();
        }

        /// <summary>Legacy summary of the pair. Unsupported children suppress its
        /// checks/utilization. Use VerifyPairAssessment to retain each child result
        /// and any already-known failure when the aggregate is unsupported.</summary>
        public static ConnectionVerification<FlexibleEndPlatePairInput> VerifyPair(FlexibleEndPlatePairInput p)
            => VerifyPairAssessment(p).Assessment;

        /// <summary>Detailed ordinary pair assessment. The aggregate has the same
        /// checks and applicability rules as VerifyPair; First and Second remain
        /// available even when no common applicable envelope can be returned.</summary>
        public static FlexibleEndPlatePairAssessmentResult VerifyPairAssessment(FlexibleEndPlatePairInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var first = Verify(p.First); var second = Verify(p.Second);
            var checks = new List<ConnectionCheck>(); var trace = new List<SectionResistanceValue>(); var issues = new List<string>();
            void Include(string prefix, FlexibleEndPlateResult part)
            {
                issues.AddRange(part.Diagnostics.Select(d => prefix + ":" + d));
                foreach (var c in part.Checks) checks.Add(c.WithId(prefix + "." + c.Id));
                foreach (var t in part.Trace) trace.Add(new SectionResistanceValue(prefix + "." + t.Name, t.Value, t.Unit));
            }
            Include("First", first); Include("Second", second);
            if (issues.Count == 0)
            {
                double v1 = -p.First.Actions.Fy, v2 = -p.Second.Actions.Fy;
                double bearing = Math.Min(first.SupportBearingPerBolt.Value, second.SupportBearingPerBolt.Value);
                int rows = Math.Max(p.First.Rows, p.Second.Rows);
                // SCI Check10 shared support: each beam contributes its own uniform reaction per row.
                // Every upper prefix is checked; no shared region is verified with only one beam's demand.
                for (int k = 1; k <= rows; k++)
                {
                    double demand = v1 * Math.Min(k, p.First.Rows) / p.First.Rows + v2 * Math.Min(k, p.Second.Rows) / p.Second.Rows;
                    double perBolt = (k <= p.First.Rows ? v1 / (2 * p.First.Rows) : 0)
                        + (k <= p.Second.Rows ? v2 / (2 * p.Second.Rows) : 0);
                    if (double.IsInfinity(demand) || double.IsInfinity(perBolt)) { issues.Add("SHARED_ACTION_SUM_OUTSIDE_NUMERIC_RANGE"); break; }
                    AddSupportShear(p.First, k, demand, "Shared.Prefix" + k, checks, trace);
                    checks.Add(new ConnectionCheck("Shared.Row" + k + ".SupportBearing", perBolt, bearing, "SCI P358 shared-support Check10, concurrent reactions"));
                }
            }
            var assessment = new ConnectionVerification<FlexibleEndPlatePairInput>(p, p.First.Code,
                "Steel.Connection.FlexibleEndPlate.OppositeColumnWebPair",
                "Two top-aligned partial-depth end plates on opposite faces of one column web; two bolt columns with shared through-bolts. "
                + "Includes both beam connections and the summed local support demands. Same ordinary-static restrictions as the individual inputs; no other support actions or global column check.",
                checks, trace, issues, new[] { "Each end plate loads its respective bolt shear plane; these plane checks are not replaced by an equal-share double-shear capacity.",
                    "Both reactions are downward in the common vertical frame. Support bearing and all critical upper row prefixes receive the sum of the two contributions.",
                    "Zero ExistingSupportActions excludes the two beam reactions explicitly assembled here." });
            return new FlexibleEndPlatePairAssessmentResult(assessment, first, second);
        }

        private static void AddSupportShear(FlexibleEndPlateInput p, int rows, double fullShear, string prefix,
            IList<ConnectionCheck> checks, IList<SectionResistanceValue> trace)
        {
            double top = Math.Min(p.SupportTopBoltEdge, 5 * p.Bolt.Diameter);
            double bottom = Math.Min(p.Gauge / 2, 5 * p.Bolt.Diameter);
            double area = p.Support.WebThickness * (top + (rows - 1) * p.Pitch + bottom);
            double net = area - rows * p.Bolt.HoleDiameter * p.Support.WebThickness;
            trace.Add(new SectionResistanceValue(prefix + ".GrossAreaPerSide", area, "mm2"));
            trace.Add(new SectionResistanceValue(prefix + ".NetAreaPerSide", net, "mm2"));
            checks.Add(new ConnectionCheck(prefix + ".GrossShear", fullShear / 2,
                PlasticShear.Resistance(area, p.Support.Fy, p.Code.Plates.GammaM0), "SCI P358 Check10 column web"));
            checks.Add(new ConnectionCheck(prefix + ".NetShear", fullShear / 2,
                PlasticShear.Resistance(net, p.Support.Fu, p.Code.Plates.GammaM2), "SCI P358 Check10 column web"));
        }
        private static bool Zero(PlanarConnectionActions p) => p.Fx == 0 && p.Fy == 0 && p.Fz == 0 && p.Mx == 0 && p.My == 0 && p.Mz == 0;
    }
}
