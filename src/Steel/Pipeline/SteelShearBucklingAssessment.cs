using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Sections;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.Pipeline
{
    /// <summary>Exclusion of shear buckling for one principal shear direction and the
    /// exact immutable input. This is a prerequisite, not a shear resistance result.
    /// External provenance is caller supplied, not independent certification.</summary>
    public sealed partial class SteelShearBucklingAssessment
    {
        public SteelVerificationInput Input { get; }
        public int Axis { get; }
        public bool IsBucklingExcluded { get; }
        public string Source { get; }
        public string Reference { get; }
        public IReadOnlyList<SectionResistanceValue> Trace { get; }

        private SteelShearBucklingAssessment(SteelVerificationInput input, int axis, bool excluded,
            string source, string reference, IEnumerable<SectionResistanceValue> trace)
        {
            Input = input ?? throw new ArgumentNullException(nameof(input));
            if (axis != 1 && axis != 2) throw new ArgumentOutOfRangeException(nameof(axis));
            if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("An assessment source is required.", nameof(source));
            Axis = axis; IsBucklingExcluded = excluded; Source = source; Reference = reference;
            Trace = Array.AsReadOnly(trace.ToArray());
        }

        /// <summary>Explicit engineering assessment under this input's code. A positive
        /// assessment cannot enable a method rejected by the selector, or substitute a
        /// reduced buckling resistance for the selected plastic shear resistance.</summary>
        public static SteelShearBucklingAssessment FromExternal(SteelVerificationInput input,
            int axis, bool isBucklingExcluded, string source)
            => new SteelShearBucklingAssessment(input, axis, isBucklingExcluded, source, source,
                Enumerable.Empty<SectionResistanceValue>());

        /// <summary>Reuses the existing EC3 unstiffened-web criterion for exact H geometry,
        /// V2 and horizontal principal axis 1. Eta and its provenance are explicit. The
        /// clear height is between flanges, including fillet zones; this is not the width
        /// used in section classification. No inference for NTC, V1 or other shapes.</summary>
        public static SteelShearBucklingAssessment EvaluateEc3HWeb(SteelVerificationInput input,
            double eta, string source)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.Code.SectionCode.Standard != SectionResistanceStandard.Eurocode3_2005)
                throw new NotSupportedException("The EC3 H-web assessment is not an NTC assessment.");
            return EvaluateHWeb(input, eta, source, false);
        }

        /// <summary>NTC 2018 4.2.4.1.2.4 equation4.2.27, conservative eta=1.
        /// Exact H, web-direction shear, principal axis1 horizontal. This excludes
        /// shear buckling only; it is not a slender-web resistance calculation.</summary>
        public static SteelShearBucklingAssessment EvaluateNtcHWeb(SteelVerificationInput input, string source)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.Code.SectionCode.Standard != SectionResistanceStandard.Ntc2018)
                throw new NotSupportedException("The NTC H-web assessment requires an NTC input.");
            return EvaluateHWeb(input, 1, source, true);
        }

        /// <summary>V1 on an exact H with horizontal principal axis 1: the flanges carry the shear.
        /// Shear buckling is excluded when every flange outstand stays within the uniform-compression
        /// class 3 limit c/t &lt;= 14 epsilon (EN 1993-1-1 Table 5.2, NTC Table 4.2.IV): its elastic
        /// critical stress with k=0.43 is then at least 1.7 fy, above the level at which
        /// EN 1993-1-1 6.2.6(6) and NTC 4.2.27 exclude web shear buckling (about 1.2 fy).
        /// Otherwise the assessment is negative: no slender-flange shear resistance is provided.</summary>
        public static SteelShearBucklingAssessment EvaluateHFlanges(SteelVerificationInput input, string source)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var shape = input.Section.ReadOnlySection.SectionShape;
            if (shape.GetType() != typeof(SectionH) || Math.Abs(shape.AngleX1) > 1e-10)
                throw new NotSupportedException("Exact H geometry with horizontal principal axis 1 is required.");
            var h = (SectionH)shape;
            double epsilon = Math.Sqrt(235 / input.Section.YieldStrength), limit = 14 * epsilon;
            double top = ((h.LenghtTopFlange - h.ThicknessWeb) / 2 - h.R) / h.ThicknessTopFlange;
            double bottom = ((h.LenghtBottomFlange - h.ThicknessWeb) / 2 - h.R) / h.ThicknessBottomFlange;
            double slenderness = Math.Max(top, bottom);
            if (!(slenderness > 0) || double.IsInfinity(slenderness) || !(limit > 0) || double.IsInfinity(limit))
                throw new ArithmeticException("Shear-buckling assessment is outside the finite positive numerical range.");
            bool ntc = input.Code.SectionCode.Standard == SectionResistanceStandard.Ntc2018;
            return new SteelShearBucklingAssessment(input, 1, slenderness <= limit, source,
                (ntc ? "NTC 2018 Table 4.2.IV" : "EN 1993-1-1:2005 Table 5.2")
                + "; flange outstands within the class 3 uniform-compression limit exclude shear buckling under V1", new[] {
                    new SectionResistanceValue("Fy", input.Section.YieldStrength, "MPa"),
                    new SectionResistanceValue("FlangeOutstandSlenderness", slenderness, "1"),
                    new SectionResistanceValue("FlangeOutstandSlendernessLimit", limit, "1") });
        }

        /// <summary>EN 1993-1-1:2005 6.2.6(6) / EN 1993-1-5:2006 5.1(2) on a qualified rectangular hollow section
        /// (sharp SectionRHS, SectionRHSRoundedCorners) with principal axis 1 horizontal: the two walls parallel to the
        /// shear are unstiffened webs, those of depth h for V2 and of width b for V1. hw is the clear distance between
        /// the walls normal to the shear (h - tTop - tBottom, b - tLeft - tRight; corner radii ignored, so hw exceeds
        /// the flat width and c = h - 3t of Table 5.2: conservative); t is the thinner web. Eta explicit as for the H
        /// web. No inference for CHS or rotated principal axes.</summary>
        public static SteelShearBucklingAssessment EvaluateEc3RhsWebs(SteelVerificationInput input, int axis,
            double eta, string source)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.Code.SectionCode.Standard != SectionResistanceStandard.Eurocode3_2005)
                throw new NotSupportedException("The EC3 RHS-web assessment is not an NTC assessment.");
            return EvaluateRhsWebs(input, axis, eta, source, false);
        }

        /// <summary>NTC 2018 4.2.4.1.2.4 equation4.2.27, conservative eta=1, on the RHS walls parallel to the shear,
        /// with the geometry and hw of EvaluateEc3RhsWebs. This excludes shear buckling only.</summary>
        public static SteelShearBucklingAssessment EvaluateNtcRhsWebs(SteelVerificationInput input, int axis, string source)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.Code.SectionCode.Standard != SectionResistanceStandard.Ntc2018)
                throw new NotSupportedException("The NTC RHS-web assessment requires an NTC input.");
            return EvaluateRhsWebs(input, axis, 1, source, true);
        }

        private static SteelShearBucklingAssessment EvaluateRhsWebs(SteelVerificationInput input, int axis, double eta, string source, bool ntc)
        {
            if (axis != 1 && axis != 2) throw new ArgumentOutOfRangeException(nameof(axis));
            var shape = input.Section.ReadOnlySection.SectionShape;
            if (!SteelSectionFamilies.Is(shape, SteelSectionKind.RectangularTube) || Math.Abs(shape.AngleX1) > 1e-10)
                throw new NotSupportedException("A qualified rectangular hollow section with horizontal principal axis 1 is required.");
            var rhs = (SectionRHS)shape;
            double hw = axis == 2 ? rhs.Heightinternal : rhs.BaseInternal;
            double t = axis == 2 ? Math.Min(rhs.ThicknessWebLeft, rhs.ThicknessWebRight) : Math.Min(rhs.ThicknessTop, rhs.ThicknessBottom);
            return Web(input, axis, hw, t, eta, source, (ntc ? "NTC 2018 4.2.4.1.2.4 equation 4.2.27; eta=1"
                : "EN 1993-1-1:2005 6.2.6(6), EN 1993-1-5:2006 5.1(2); Ec3WebShearBuckling.RequiresCheck")
                + "; RHS walls parallel to V" + axis + " as unstiffened webs, hw = " + (axis == 2 ? "h - tTop - tBottom" : "b - tLeft - tRight")
                + " between the inner faces (corner radii ignored), thinner wall");
        }

        private static SteelShearBucklingAssessment EvaluateHWeb(SteelVerificationInput input, double eta, string source, bool ntc)
        {
            var shape = input.Section.ReadOnlySection.SectionShape;
            if (shape.GetType() != typeof(SectionH) || Math.Abs(shape.AngleX1) > 1e-10)
                throw new NotSupportedException("Exact H geometry with horizontal principal axis 1 is required.");
            var h = (SectionH)shape;
            return Web(input, 2, h.HeightWeb, h.ThicknessWeb, eta, source,
                ntc ? "NTC 2018 4.2.4.1.2.4 equation 4.2.27; eta=1" : "EN 1993-1-1:2005 6.2.6(6); Ec3WebShearBuckling.RequiresCheck");
        }

        // hw/t <= 72 eps/eta on one unstiffened web; the H web and the RHS walls share it.
        private static SteelShearBucklingAssessment Web(SteelVerificationInput input, int axis, double hw, double t,
            double eta, string source, string reference)
        {
            bool required = Ec3WebShearBuckling.RequiresCheck(hw, t, input.Section.YieldStrength, eta);
            // Guard the comparison's numerical range; do not accept infinity <= infinity.
            double slenderness = hw / t;
            double limit = 72 * Math.Sqrt(235 / input.Section.YieldStrength) / eta;
            if (!(slenderness > 0) || double.IsInfinity(slenderness) || !(limit > 0) || double.IsInfinity(limit))
                throw new ArithmeticException("Shear-buckling assessment is outside the finite positive numerical range.");
            return new SteelShearBucklingAssessment(input, axis, !required, source, reference, new[] {
                    new SectionResistanceValue("ClearWebHeight", hw, "mm"),
                    new SectionResistanceValue("WebThickness", t, "mm"),
                    new SectionResistanceValue("Fy", input.Section.YieldStrength, "MPa"),
                    new SectionResistanceValue("Eta", eta, "1"),
                    new SectionResistanceValue("WebSlenderness", slenderness, "1"),
                    new SectionResistanceValue("WebSlendernessLimit", limit, "1") });
        }
    }
}
