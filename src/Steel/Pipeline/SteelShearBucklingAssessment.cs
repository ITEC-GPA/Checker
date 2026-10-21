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
    public sealed class SteelShearBucklingAssessment
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
            var shape = input.Section.CreateSectionCopy().SectionShape;
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
                "EN 1993-1-1:2005 6.2.6(6); Ec3WebShearBuckling.RequiresCheck", new[] {
                    new SectionResistanceValue("ClearWebHeight", h.HeightWeb, "mm"),
                    new SectionResistanceValue("WebThickness", h.ThicknessWeb, "mm"),
                    new SectionResistanceValue("Fy", input.Section.YieldStrength, "MPa"),
                    new SectionResistanceValue("Eta", eta, "1"),
                    new SectionResistanceValue("WebSlenderness", slenderness, "1"),
                    new SectionResistanceValue("WebSlendernessLimit", limit, "1") });
        }
    }
}
