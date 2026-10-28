using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Connections
{
    public enum SpliceFlangeFaceModel { BalancedEqualFaces, EqualThicknessIndependentWidths, IndependentFaceDimensions, PackedIndependentFaceDimensions }

    /// <summary>Applicability of the conventional two-membrane double-cover model.
    /// Equal thickness/areas remain the default; independent widths or dimensions are explicitly selected.
    /// All models require identical materials and centred inner-strip bolt
    /// groups, no credited contact bearing or externally applied bolt-axis load. Packing requires its explicit model.
    /// Unequal TOP/BOTTOM parent flanges are permitted. No rotational stiffness calculation.</summary>
    public static class WeldedIBeamSpliceStandardModel
    {
        public static ConnectionVerification<WeldedIBeamSpliceDetailInput> Verify(WeldedIBeamSpliceDetailInput p)
            => Verify(p, SpliceFlangeFaceModel.BalancedEqualFaces);
        public static ConnectionVerification<WeldedIBeamSpliceDetailInput> Verify(WeldedIBeamSpliceDetailInput p, SpliceFlangeFaceModel faceModel)
        {
            if (!Enum.IsDefined(typeof(SpliceFlangeFaceModel), faceModel)) throw new ArgumentOutOfRangeException(nameof(faceModel));
            if (p == null) throw new ArgumentNullException(nameof(p));
            return Verify(p, faceModel, WeldedIBeamSpliceForces.Calculate(p.Plates.Bolts.Detail));
        }
        internal static ConnectionVerification<WeldedIBeamSpliceDetailInput> Verify(WeldedIBeamSpliceDetailInput p, SpliceFlangeFaceModel faceModel, WeldedIBeamSpliceForceResult f)
        {
            if (!Enum.IsDefined(typeof(SpliceFlangeFaceModel), faceModel)) throw new ArgumentOutOfRangeException(nameof(faceModel));
            if (!ReferenceEquals(p.Plates.Bolts.Detail, f.Input)) throw new ArgumentException("Prepared forces belong to another input.");
            var b = p.Plates.Bolts; var d = b.Detail;
            var issues = new List<string>(f.Diagnostics);
            var checks = new List<ConnectionCheck>();
            var trace = new List<SectionResistanceValue>(f.Trace); var reported = new List<SectionResistanceValue>();
            Flange(d.Top, "Top"); Flange(d.Bottom, "Bottom");
            if (d.Packing.HasPacking && faceModel != SpliceFlangeFaceModel.PackedIndependentFaceDimensions)
                issues.Add("EXPLICIT_PACKED_FACE_MODEL_REQUIRED");
            if (d.Packing.HasPacking && string.IsNullOrWhiteSpace(d.Packing.ExecutionSource))
                issues.Add("PACKING_EXECUTION_SOURCE_REQUIRED");
            if (d.WebBolts.LongitudinalRows > 20 || d.WebBolts.TransverseRows > 16)
                issues.Add("STANDARD_WEB_GRID_LIMIT_20_BY_16");
            foreach (var plate in new[] { d.Section.TopFlange, d.Section.Web, d.Section.BottomFlange,
                d.Top.Outer, d.Top.EachInner, d.Bottom.Outer, d.Bottom.EachInner, d.EachWebCover })
                if (plate.Fy > 460) issues.Add("STANDARD_CARBON_STEEL_FY_UP_TO_460_REQUIRED");
            if (f.IsBalanced)
            {
                FaceStiffness(d.Top, d.Section.TopFlange, true); FaceStiffness(d.Bottom, d.Section.BottomFlange, false);
                checks.Add(new ConnectionCheck("ForceModel.Equilibrium", 0, 1,
                    "EN1993-1-8:2005 2.5,3.12; SCI P357 section12; declared two-face membrane double-cover idealisation", "1"));
            }
            return new ConnectionVerification<WeldedIBeamSpliceDetailInput>(p, b.Code,
                "Steel.Connection.WeldedIBeamSplice.StandardModel",
                "Applicability of the declared aligned double-cover static force model: " + faceModel + "; not resistance or rotational stiffness.",
                checks, trace, issues.Distinct(), new[] {
                    "SCI P357 example1 section12.7 p60 uses half flange load on each face and verifies the actual inner strip areas separately. EqualThicknessIndependentWidths extends the conventional equal-thickness double-lap model to independent widths with symmetric inner strips; no force-proportional-to-area redistribution is made.",
                    "BalancedEqualFaces retains the2.90 equal-area/equal-thickness restriction; EqualThicknessIndependentWidths retains equal thickness. IndependentFaceDimensions explicitly permits unequal face thickness using actual centre-plane offsets. All modes require identical material, centred inner-strip bolt groups and every actual gross/net/block/stability/detail check.",
                    "The independent two-line membrane stiffness system E[[A,S],[S,I]]*(epsilon0,kappa)=(N,-M) reproduces both face forces and retains parent flange self-bending. I excludes each cover own bending inertia: the physical idealisation is axial membranes at their centroids. The nonzero face strain gradient is reported, not forced to zero or used as proof of contact compatibility.",
                    "No continuous equal-strain bond between parent and covers is assumed. Bolt shear and bearing, slip, actual plate areas, outstands, net sections and parent welds are verified separately. This remains a conventional static double-lap model; it does not solve local contact/plate lifting or supply rotational stiffness.",
                    "Contact-face transport couples alone are not an externally applied bolt tension. This conventional double-lap membrane model does not solve contact, prying or plate lifting. Those details must use another method.",
                    "All covers are continuous and flat. PackedIndependentFaceDimensions permits full-footprint parallel packing layers per face and per member side, identical on left/right, with no load or stiffness credit; actual total offsets, bolt reduction and every serial friction interface are assessed. No tapered/discontinuous packs, stiffener cut-outs, notches, section changes or out-of-plane actions.",
                    "The structural analysis must justify continuity, restraints and second-order actions separately. This assessment supplies no rotational stiffness or fatigue qualification." }, reported);

            void FaceStiffness(SpliceFlangeCoverDetail x, ConnectionPlate parent, bool top)
            {
                string name = top ? "Top" : "Bottom"; double direction = top ? 1 : -1;
                double yo = direction * ((parent.Thickness + x.Outer.Thickness) / 2 + (top ? d.Packing.TopOuter : d.Packing.BottomOuter));
                double yi = -direction * ((parent.Thickness + x.EachInner.Thickness) / 2 + (top ? d.Packing.TopInner : d.Packing.BottomInner));
                double ao = x.Outer.Width * x.Outer.Thickness, ai = 2 * x.EachInner.Width * x.EachInner.Thickness;
                double area = ao + ai, first = ao * yo + ai * yi, second = ao * yo * yo + ai * yi * yi;
                // Positive factored determinant avoids subtraction of large nearly equal terms.
                double determinant = ao * ai * (yo - yi) * (yo - yi);
                var typed = f.Flange(SpliceSide.Left, top);
                double n = typed.ParentAxial, m = typed.ParentLocalMoment, e = p.Plates.Policy.ElasticModulus;
                double strain = (second * n + first * m) / (e * determinant);
                double curvature = (-area * m - first * n) / (e * determinant);
                double outer = e * ao * (strain + curvature * yo), inner = e * ai * (strain + curvature * yi);
                Value("AreaCentroidOffset", first / area, "mm"); Value("Determinant", determinant, "mm6");
                Value("MembraneStrainAtParentCentroid", strain, "1"); Value("FaceStrainGradient", curvature, "1/mm");
                Value("OuterForce", outer, "N"); Value("BothInnerForce", inner, "N");
                double scale = Math.Abs(n) + Math.Abs(m / (yo - yi));
                if (!WeldedIBeamSplicePlates.Finite(outer) || !WeldedIBeamSplicePlates.Finite(inner)
                    || Math.Abs(outer - typed.OuterForce) > 128 * 2.2204460492503131e-16 * scale
                    || Math.Abs(inner - typed.BothInnerForce) > 128 * 2.2204460492503131e-16 * scale)
                    issues.Add(name + ".TWO_FACE_MEMBRANE_EQUILIBRIUM");
                void Value(string key, double value, string unit)
                {
                    if (WeldedIBeamSplicePlates.Finite(value))
                    {
                        var entry = new SectionResistanceValue(name + ".FaceModel." + key, value, unit);
                        trace.Add(entry); reported.Add(entry);
                    }
                    else issues.Add(name + ".FACE_MODEL_NUMERIC_RANGE");
                }
            }
            void Flange(SpliceFlangeCoverDetail x, string name)
            {
                if (faceModel != SpliceFlangeFaceModel.IndependentFaceDimensions && faceModel != SpliceFlangeFaceModel.PackedIndependentFaceDimensions && x.Outer.Thickness != x.EachInner.Thickness
                    || faceModel == SpliceFlangeFaceModel.BalancedEqualFaces && x.Outer.Width != 2 * x.EachInner.Width)
                    issues.Add(name + ".BALANCED_FACE_THICKNESS_AND_AREA_REQUIRED");
                if (x.Outer.Fy != x.EachInner.Fy || x.Outer.Fu != x.EachInner.Fu)
                    issues.Add(name + ".IDENTICAL_FACE_MATERIAL_REQUIRED");
                if (x.Bolts.LongitudinalRows > 20 || x.Bolts.TransverseRows > 16)
                    issues.Add(name + ".STANDARD_FLANGE_GRID_LIMIT_20_BY_16");
                var ys = x.Bolts.Points(SpliceSide.Left).Where(q => q.Y > 0).Select(q => q.Y).ToArray();
                if (ys.Length == 0 || !Equal(ys.Average(), (x.InnerGap + x.EachInner.Width) / 2))
                    issues.Add(name + ".CENTRED_INNER_STRIP_BOLT_GROUP_REQUIRED");
            }
        }
        internal static bool Equal(double a, double b) => Math.Abs(a - b) <= 1e-12 * Math.Max(Math.Abs(a), Math.Abs(b));
    }
}
