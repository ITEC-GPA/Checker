using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;
using GPC.Geometry;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>One physical equal-leg fillet loop, directly between the solid packing
    /// and the chord identified by Model PartIndex. Throat in mm. FullSize evidence
    /// includes starts, corners and terminations; no penetration allowance is inferred.</summary>
    public sealed class PackingFaceWeldDetail
    {
        public int PartIndex { get; }
        public double Throat { get; }
        public FilletWeldEndCondition EndCondition { get; }
        public string EndConditionSource { get; }
        public PackingFaceWeldDetail(int partIndex, double throat, FilletWeldEndCondition endCondition,
            string endConditionSource)
        {
            if (partIndex < 0 || partIndex > 1) throw new ArgumentOutOfRangeException(nameof(partIndex));
            ConnectionValidation.Positive(throat, nameof(throat));
            if (!Enum.IsDefined(typeof(FilletWeldEndCondition), endCondition)) throw new ArgumentOutOfRangeException(nameof(endCondition));
            PartIndex = partIndex; Throat = throat; EndCondition = endCondition; EndConditionSource = endConditionSource;
        }
    }

    /// <summary>Component-only assessment of one solid rectangular packing in identical
    /// Model2L/2C, with two actual external fillet loops on its opposite faces. All
    /// geometry and six concomitant face actions come from Demand. The chord material
    /// comes from that immutable Model input; block Fy/Fu are actual-product values in MPa.
    /// Sources must qualify compatible consumables, sound unperforated flat weld footprints,
    /// no root gap, corner details, and the local rigid-distributor/eccentricity model.
    /// These sources record caller evidence and do not certify execution or local chord strength.</summary>
    public sealed class PackingWeldedStationInput
    {
        public ConnectionDesignCode Code { get; }
        public PackingFaceActionAdapterResult Demand { get; }
        public double BlockFy { get; }
        public double BlockFu { get; }
        public FilletWeldSteelGrade BlockGrade { get; }
        public FilletWeldSteelGrade ChordGrade { get; }
        public IReadOnlyList<PackingFaceWeldDetail> FaceDetails { get; }
        public string MaterialSource { get; }
        public string ConsumableCompatibilitySource { get; }
        public string FootprintExecutionSource { get; }
        public string LocalDistributionSource { get; }
        public PackingWeldedStationInput(ConnectionDesignCode code, PackingFaceActionAdapterResult demand,
            double blockFy, double blockFu, FilletWeldSteelGrade blockGrade, FilletWeldSteelGrade chordGrade,
            IEnumerable<PackingFaceWeldDetail> faceDetails, string materialSource,
            string consumableCompatibilitySource, string footprintExecutionSource, string localDistributionSource)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Demand = demand ?? throw new ArgumentNullException(nameof(demand));
            ConnectionValidation.Positive(blockFy, nameof(blockFy)); ConnectionValidation.Positive(blockFu, nameof(blockFu));
            if (blockFu < blockFy) throw new ArgumentException("Block fu must not be below fy.");
            if (!Enum.IsDefined(typeof(FilletWeldSteelGrade), blockGrade)) throw new ArgumentOutOfRangeException(nameof(blockGrade));
            if (!Enum.IsDefined(typeof(FilletWeldSteelGrade), chordGrade)) throw new ArgumentOutOfRangeException(nameof(chordGrade));
            if (faceDetails == null) throw new ArgumentNullException(nameof(faceDetails));
            var details = faceDetails.ToArray();
            if (details.Length != 2 || details.Any(d => d == null) || details[0].PartIndex == details[1].PartIndex)
                throw new ArgumentException("Exactly one detail for each of the two physical faces is required.", nameof(faceDetails));
            BlockFy = blockFy; BlockFu = blockFu; BlockGrade = blockGrade; ChordGrade = chordGrade;
            FaceDetails = Array.AsReadOnly(details.OrderBy(d => d.PartIndex).ToArray());
            MaterialSource = materialSource; ConsumableCompatibilitySource = consumableCompatibilitySource;
            FootprintExecutionSource = footprintExecutionSource; LocalDistributionSource = localDistributionSource;
        }
    }

    public sealed class PackingFaceWeldComponent
    {
        public PackingFaceDemand Face { get; }
        public FilletWeldGroupResult Verification { get; }
        internal PackingFaceWeldComponent(PackingFaceDemand face, FilletWeldGroupResult verification)
        { Face = face; Verification = verification; }
    }

    /// <summary>Separate actual-face weld results and nominal prismatic-block yielding.
    /// CoveredComponentsStatus qualifies only these explicitly named components. No
    /// scalar complete-connection resistance is produced for SteelPackedPairAssessment.</summary>
    public sealed class PackingWeldedStationResult
    {
        public PackingWeldedStationInput Input { get; }
        public IReadOnlyList<PackingFaceWeldComponent> FaceWelds { get; }
        public ConnectionVerification<PackingWeldedStationInput> NominalBlock { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public bool CanQualifyWholeStation => false;
        public bool CanQualifyWholeJoint => false;
        public ConnectionStatus CoveredComponentsStatus { get; }
        public string Scope => "Two actual direct fillet loops under six supplied face actions and nominal planar Fx/Fz/My yielding of a solid rectangular block. "
            + "No local 3D block/chord-wall stress, contact, punching/tearing, torsional block resistance, member/spacing/stability or complete-station/joint qualification.";
        internal PackingWeldedStationResult(PackingWeldedStationInput input, IEnumerable<PackingFaceWeldComponent> welds,
            ConnectionVerification<PackingWeldedStationInput> block, IEnumerable<SectionResistanceValue> trace,
            IEnumerable<string> diagnostics)
        {
            Input = input; FaceWelds = Array.AsReadOnly(welds.ToArray()); NominalBlock = block;
            Trace = Array.AsReadOnly(trace.ToArray()); Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
            CoveredComponentsStatus = Diagnostics.Count != 0 || FaceWelds.Count != 2
                || block.Status == ConnectionStatus.NotSupported || FaceWelds.Any(w => w.Verification.Status == ConnectionStatus.NotSupported)
                ? ConnectionStatus.NotSupported : block.Status == ConnectionStatus.Failed || FaceWelds.Any(w => w.Verification.Status == ConnectionStatus.Failed)
                ? ConnectionStatus.Failed : ConnectionStatus.Passed;
        }
    }

    public static class PackingWeldedStationComponents
    {
        public const string MethodId = "Steel.Connection.PackingWeldedStation.Components";
        public static PackingWeldedStationResult Verify(PackingWeldedStationInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var issues = new List<string>(); var trace = new List<SectionResistanceValue>();
            var welds = new List<PackingFaceWeldComponent>(); var d = p.Demand;
            void Require(bool condition, string reason) { if (!condition) issues.Add(reason); }
            void Value(string name, double value, string unit)
            {
                if (Finite(value)) trace.Add(new SectionResistanceValue(name, value, unit));
                else issues.Add("NONFINITE_" + name);
            }
            Require(d.IsInEquilibrium, "TWO_FACE_STATE_NOT_IN_EQUILIBRIUM");
            Require(!string.IsNullOrWhiteSpace(p.MaterialSource), "ACTUAL_PRODUCT_MATERIAL_SOURCE_REQUIRED");
            Require(!string.IsNullOrWhiteSpace(p.ConsumableCompatibilitySource), "CONSUMABLE_COMPATIBILITY_SOURCE_REQUIRED");
            Require(!string.IsNullOrWhiteSpace(p.FootprintExecutionSource), "FOOTPRINT_EXECUTION_SOURCE_REQUIRED");
            Require(!string.IsNullOrWhiteSpace(p.LocalDistributionSource), "LOCAL_DISTRIBUTION_SOURCE_REQUIRED");
            var sectionCode = d.SourceInput.Code.SectionCode;
            Require(p.Code.Plates.Standard == sectionCode.Standard && p.Code.Plates.GammaM0 == sectionCode.GammaM0
                && p.Code.Plates.GammaM2 == sectionCode.GammaM2
                && p.Code.Plates.ParameterSet == sectionCode.ParameterSet, "SOURCE_DESIGN_CONTEXT_MISMATCH");
            Require(p.BlockFy <= (double)p.BlockGrade, "BLOCK_FY_EXCEEDS_DECLARED_GRADE");
            Require(d.SourceInput.Section.YieldStrength <= (double)p.ChordGrade
                && d.SourceInput.Section.UltimateStrength >= d.SourceInput.Section.YieldStrength, "CHORD_MATERIAL_GRADE_INCONSISTENT");
            var property = d.SourceInput.Section.CreateSectionCopy();
            Require(property.FormedType == Section.FormedTypes.HotFinished
                && (property.SectionType == Section.SectionTypes.Rolled || property.SectionType == Section.SectionTypes.Welded),
                "COLD_FORMED_OR_UNQUALIFIED_MANUFACTURING_OUTSIDE_SCOPE");
            Require(d.SourceInput.Member != null, "MEMBER_LENGTH_REQUIRED_FOR_WELD_FOOTPRINT");
            var pair = (SectionBuiltUp)property.SectionShape; // Exact Model2L/2C already qualified by the demand adapter.
            bool vertical = pair.Parts[0].MirrorX != pair.Parts[1].MirrorX;
            double chordThickness = pair.Parts[0].Section is SectionC c ? c.ThicknessWeb
                : vertical ? ((SectionL)pair.Parts[0].Section).VerticalLegThickness : ((SectionL)pair.Parts[0].Section).HorizontalLegThickness;
            Value("PackingThickness", d.PackingThickness, "mm"); Value("FaceLength", d.FaceLength, "mm"); Value("FaceWidth", d.FaceWidth, "mm");
            Value("ChordConnectedThickness", chordThickness, "mm");
            var legs = p.FaceDetails.Select(w => Math.Sqrt(2) * w.Throat).ToArray();
            Require(Finite(legs.Sum()) && !ConnectionValidation.BelowMinimumWithRoundoff(d.PackingThickness, legs.Sum()),
                "OPPOSING_EQUAL_LEG_WELDS_OVERLAP_ACROSS_GAP");
            for (int i = 0; i < 2; i++)
            {
                var face = d.Faces[i]; var part = pair.Parts[face.PartIndex]; var detail = p.FaceDetails[i];
                Require(!string.IsNullOrWhiteSpace(detail.EndConditionSource), "FACE_" + i + "_END_CONDITION_SOURCE_REQUIRED");
                Value("Face" + i + ".Leg", legs[i], "mm");
                var origin = face.LocalAxes.Origin;
                double transverse = vertical ? (origin.Y - part.Y) * (part.MirrorY ? -1 : 1)
                    : (origin.X - part.X) * (part.MirrorX ? -1 : 1);
                double low = transverse - d.FaceWidth / 2 - legs[i], high = transverse + d.FaceWidth / 2 + legs[i];
                Value("Face" + i + ".FootprintLow", low, "mm"); Value("Face" + i + ".FootprintHigh", high, "mm");
                Require(Finite(low) && Finite(high) && OnOneStraightBack(part.Section, vertical, transverse, d.FaceWidth / 2 + legs[i]),
                    "FACE_" + i + "_FOOTPRINT_OUTSIDE_EXACT_MODEL_STRAIGHT_BACK");
                double start = origin.Z - d.FaceLength / 2 - legs[i], end = origin.Z + d.FaceLength / 2 + legs[i];
                Value("Face" + i + ".FootprintStartStation", start, "mm"); Value("Face" + i + ".FootprintEndStation", end, "mm");
                Require(Finite(start) && Finite(end) && !ConnectionValidation.BelowMinimumWithRoundoff(origin.Z, d.FaceLength / 2 + legs[i])
                    && d.SourceInput.Member != null && !ConnectionValidation.BelowMinimumWithRoundoff(d.SourceInput.Member.Length, end),
                    "FACE_" + i + "_WELD_FOOTPRINT_OUTSIDE_MEMBER_LENGTH");
                ActionsTrace(trace, "Face" + i, face.ActionsOnBlockAtFaceCentre);
            }
            ActionsTrace(trace, "ResidualGlobal", d.ResidualAtBlockCentre);
            if (issues.Count == 0)
                for (int i = 0; i < 2; i++)
                {
                    var face = d.Faces[i]; var detail = p.FaceDetails[i];
                    try
                    {
                        var input = new FilletWeldGroupInput(p.Code, Loop(d.FaceLength, d.FaceWidth, detail),
                            new FilletWeldMaterial(p.BlockGrade, p.BlockFu, d.PackingThickness),
                            new FilletWeldMaterial(p.ChordGrade, d.SourceInput.Section.UltimateStrength, chordThickness),
                            face.ActionsOnBlockAtFaceCentre, FilletWeldMethod.Directional, p.ConsumableCompatibilitySource,
                            true, p.LocalDistributionSource, 0);
                        welds.Add(new PackingFaceWeldComponent(face, FilletWeldGroup.Verify(input)));
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        // Public input numbers are finite/positive. Intermediate line-group products
                        // may still overflow; such a group cannot become a favourable partial result.
                        issues.Add("FACE_" + i + "_NUMERIC_RANGE_OUTSIDE_SCOPE");
                    }
                }
            var block = Block(p, issues);
            return new PackingWeldedStationResult(p, welds, block, trace, issues);
        }

        private static ConnectionVerification<PackingWeldedStationInput> Block(PackingWeldedStationInput p, IEnumerable<string> commonIssues)
        {
            var issues = new List<string>(commonIssues); var trace = new List<SectionResistanceValue>(); var checks = new List<ConnectionCheck>();
            var d = p.Demand; var a = d.Faces[0].ActionsOnBlockAtFaceCentre;
            ActionsTrace(trace, "Face0", a); ActionsTrace(trace, "Face1", d.Faces[1].ActionsOnBlockAtFaceCentre);
            if (d.Faces.Any(f => f.ActionsOnBlockAtFaceCentre.Fy != 0 || f.ActionsOnBlockAtFaceCentre.Mx != 0 || f.ActionsOnBlockAtFaceCentre.Mz != 0))
                issues.Add("OUT_OF_PLANE_FY_MX_MZ_BLOCK_RESISTANCE_NOT_IMPLEMENTED");
            if (issues.Count == 0)
            {
                double area = d.FaceLength * d.FaceWidth;
                double iy = d.FaceWidth * d.FaceLength * d.FaceLength * d.FaceLength / 12;
                double wy = d.FaceWidth * d.FaceLength * d.FaceLength / 6;
                // Axes of face0 are also the block axes; face0 is at z=+g/2.
                // M_y(z)=M_y(face0)+(g/2-z)*F_x. Check both ends, not just the centre.
                double my0 = a.My, my1 = a.My + d.PackingThickness * a.Fx;
                double sigma = Math.Abs(a.Fz) / area + Math.Max(Math.Abs(my0), Math.Abs(my1)) / wy;
                double tau = 1.5 * Math.Abs(a.Fx) / area;
                double scale = Math.Max(sigma, tau);
                double eq = scale == 0 ? 0 : scale * Math.Sqrt((sigma / scale) * (sigma / scale) + 3 * (tau / scale) * (tau / scale));
                double resistance = p.BlockFy / p.Code.Plates.GammaM0;
                var values = new[] { area, iy, wy, my0, my1, sigma, tau, eq, resistance };
                var names = new[] { "Area", "Iy", "Wy", "MyAtFace0Cut", "MyAtFace1Cut", "SigmaBound", "TauBound", "EquivalentStressBound", "YieldStressResistance" };
                var units = new[] { "mm2", "mm4", "mm3", "Nmm", "Nmm", "MPa", "MPa", "MPa", "MPa" };
                for (int i = 0; i < values.Length; i++)
                    if (Finite(values[i])) trace.Add(new SectionResistanceValue(names[i], values[i], units[i]));
                    else issues.Add("BLOCK_NONFINITE_" + names[i]);
                if (!(area > 0) || !(iy > 0) || !(wy > 0) || !(resistance > 0)) issues.Add("BLOCK_NUMERIC_SCALE_OUTSIDE_SCOPE");
                if (issues.Count == 0) checks.Add(new ConnectionCheck("Block.NominalPlanarYielding", eq, resistance,
                    p.Code.IsNtc ? "NTC2018 gammaM0; supplemental EN1993-1-1:2005 6.2.1(5) nominal elastic yield criterion"
                        : "EN1993-1-1:2005 6.2.1(5), nominal rectangular-prism elastic stress bound", "MPa"));
            }
            return new ConnectionVerification<PackingWeldedStationInput>(p, p.Code, MethodId + ".NominalPlanarBlock",
                "Nominal solid rectangular-prism stress bound under Fx/Fz/My; no local 3D block/contact or chord-wall qualification.",
                checks, trace, issues, new[] { "No axial load split inferred; exact supplied face forces and couples.",
                    "sigmaBound=|Fz|/A+max(|My0|,|My0+g*Fx|)/Wy; tauBound=1.5|Fx|/A. Separate maxima form a conservative nominal stress bound.",
                    "No contact credit, plastic redistribution, local 3D end stresses, torsion, stability or complete station resistance." });
        }

        internal static IEnumerable<FilletWeldSegment> Loop(double length, double width, PackingFaceWeldDetail detail)
        {
            var points = new[] { new ConnectionPoint2D(-length / 2, -width / 2), new ConnectionPoint2D(length / 2, -width / 2),
                new ConnectionPoint2D(length / 2, width / 2), new ConnectionPoint2D(-length / 2, width / 2) };
            for (int i = 0; i < 4; i++)
                // CCW roots: p points into the rectangle; block is at z<0. The physical
                // external fillet occupies -p,-z, with positive throat normal -p+z.
                yield return new FilletWeldSegment(points[i], points[(i + 1) % 4], detail.Throat,
                    detail.EndCondition, 3 * Math.PI / 4, 90);
        }

        internal static bool OnOneStraightBack(Section chord, bool vertical, double centre, double halfExtent)
        {
            var outlines = chord.GetCurveOutlines();
            if (outlines.Count != 1 || outlines[0].IsApproximation || outlines[0].Holes.Count != 0 || outlines[0].Children.Count != 0) return false;
            foreach (var line in StraightPieces(outlines[0].Boundary))
            {
                var a = line.StartPoint; var b = line.EndPoint;
                if (a.Z != 0 || b.Z != 0 || (vertical ? a.X != 0 || b.X != 0 : a.Y != 0 || b.Y != 0)) continue;
                double x = vertical ? a.Y : a.X, y = vertical ? b.Y : b.X;
                if (!ConnectionValidation.BelowMinimumWithRoundoff(centre, Math.Min(x, y) + halfExtent)
                    && !ConnectionValidation.BelowMinimumWithRoundoff(Math.Max(x, y), centre + halfExtent)) return true;
            }
            return false;
        }
        private static IEnumerable<LineCurve3d> StraightPieces(Curve3d curve)
        {
            if (curve is LineCurve3d line) { yield return line; yield break; }
            if (curve is PolylineCurve3d polyline)
            {
                var vertices = polyline.Vertices;
                for (int i = 1; i < vertices.Length; i++) yield return new LineCurve3d(vertices[i - 1], vertices[i]);
            }
            else if (curve is PolyCurve3d polycurve)
                for (int i = 0; i < polycurve.SegmentCount; i++)
                    foreach (var segment in StraightPieces(polycurve.GetSegment(i))) yield return segment;
            // Arcs, elliptic pieces and approximations are never chorded into a flat weld footprint.
        }
        private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
        private static void ActionsTrace(ICollection<SectionResistanceValue> trace, string prefix, PlanarConnectionActions a)
        {
            var values = new[] { a.Fx, a.Fy, a.Fz, a.Mx, a.My, a.Mz };
            var names = new[] { "Fx", "Fy", "Fz", "Mx", "My", "Mz" };
            for (int i = 0; i < 6; i++) trace.Add(new SectionResistanceValue(prefix + "." + names[i], values[i], i < 3 ? "N" : "Nmm"));
        }
    }
}
