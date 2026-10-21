using System.Linq;

namespace GPC.Checkers.Steel.Sections
{
    /// <summary>Italy, DM 17/01/2018, 4.2.3.1 Tables 4.2.III-V, printed pp.94-95.
    /// Nominal uniform compression only. Shared plate arithmetic has identical limits
    /// to EC3 Table 5.2; references and CHS epsilon-squared policy remain NTC specific.
    /// Class 4 is a classification result, not a verified effective-section resistance.</summary>
    public static class Ntc2018CompressionClassification
    {
        public static CompressionPartClassification EvaluatePart(CompressionPartKind kind,
            double? widthOrDiameter, double? thickness, double? yieldStrength)
            => CompressionPartEvaluator.Evaluate(kind, widthOrDiameter, thickness, yieldStrength, true);

        public static UniformCompressionClassification Evaluate(CompressionSectionGeometry geometry, double? yieldStrength)
        {
            if (geometry == null) return new UniformCompressionClassification(SectionClassificationStatus.MissingInput,
                null, "Geometry.Required", new NamedCompressionPartClassification[0]);
            var parts = geometry.Parts.Select(p => new NamedCompressionPartClassification(p.Id,
                EvaluatePart(p.Kind, p.WidthOrDiameter, p.Thickness, yieldStrength))).ToArray();
            var failed = parts.FirstOrDefault(p => !p.Classification.IsEvaluated);
            return failed == null
                ? new UniformCompressionClassification(SectionClassificationStatus.Completed, parts.Max(p => p.Classification.NominalClass.Value), null, parts)
                : new UniformCompressionClassification(failed.Classification.Status, null, failed.Id + "." + failed.Classification.DiagnosticCode, parts);
        }

        /// <summary>Table 4.2.V plus the outstand check of Table 4.2.IV.
        /// Isolated equal-thickness angles: conservative class 3/4; no plastic claim.</summary>
        public static int ClassifyAngle(double leg1, double leg2, double thickness, double yieldStrength, double rootRadius = 0)
            => AngleCompressionArithmetic.Classify(leg1, leg2, thickness, yieldStrength, rootRadius);
    }
}
