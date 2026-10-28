using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
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

        private static SteelShearBucklingAssessment EvaluateHWeb(SteelVerificationInput input, double eta, string source, bool ntc)
        {
            var shape = input.Section.ReadOnlySection.SectionShape;
            if (shape.GetType() != typeof(SectionH) || Math.Abs(shape.AngleX1) > 1e-10)
                throw new NotSupportedException("Exact H geometry with horizontal principal axis 1 is required.");
            var h = (SectionH)shape;
            bool required = Ec3WebShearBuckling.RequiresCheck(h.HeightWeb, h.ThicknessWeb, input.Section.YieldStrength, eta);
            // Guard the comparison's numerical range; do not accept infinity <= infinity.
            double slenderness = h.HeightWeb / h.ThicknessWeb;
            double limit = 72 * Math.Sqrt(235 / input.Section.YieldStrength) / eta;
            if (!(slenderness > 0) || double.IsInfinity(slenderness) || !(limit > 0) || double.IsInfinity(limit))
                throw new ArithmeticException("Shear-buckling assessment is outside the finite positive numerical range.");
            return new SteelShearBucklingAssessment(input, 2, !required, source,
                ntc ? "NTC 2018 4.2.4.1.2.4 equation 4.2.27; eta=1" : "EN 1993-1-1:2005 6.2.6(6); Ec3WebShearBuckling.RequiresCheck", new[] {
                    new SectionResistanceValue("ClearWebHeight", h.HeightWeb, "mm"),
                    new SectionResistanceValue("WebThickness", h.ThicknessWeb, "mm"),
                    new SectionResistanceValue("Fy", input.Section.YieldStrength, "MPa"),
                    new SectionResistanceValue("Eta", eta, "1"),
                    new SectionResistanceValue("WebSlenderness", slenderness, "1"),
                    new SectionResistanceValue("WebSlendernessLimit", limit, "1") });
        }
    }
}
