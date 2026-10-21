using System;
using System.Linq;
using GPC.Checkers.Steel.Members;

namespace GPC.Checkers.Steel.Sections
{
    /// <summary>Joins a successfully classified compressed flat part and its design
    /// maximum compressive stress in MPa. All parts must describe the same load state.</summary>
    public sealed class ClassifiedCompressionPart
    {
        public string Id { get; }
        public CompressionPartClassification Classification { get; }
        public double SigmaComEd { get; }
        public ClassifiedCompressionPart(string id, CompressionPartClassification classification, double sigmaComEd)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A part identifier is required.", nameof(id));
            if (classification == null) throw new ArgumentNullException(nameof(classification));
            if (!classification.IsEvaluated || !classification.Class3Limit.HasValue)
                throw new ArgumentException("Supply a completed classification of a compressed part.", nameof(classification));
            if (classification.Kind == CompressionPartKind.CircularHollow)
                throw new NotSupportedException("The flat-part stress treatment does not qualify the CHS epsilon-squared limits.");
            Ec3StressBasedClassification.Positive(sigmaComEd, nameof(sigmaComEd));
            Id = id; Classification = classification; SigmaComEd = sigmaComEd;
        }
    }

    /// <summary>Explicit connection between nominal part classification and 5.5.2(9)-(10).
    /// Supply every compressed flat part; no missing parts/stresses are inferred.
    /// The returned section resistance class never replaces MemberBucklingClass.</summary>
    public static class Ec3ClassifiedSectionTreatment
    {
        public static Ec3SectionClassificationTreatment Evaluate(double gammaM0, params ClassifiedCompressionPart[] parts)
        {
            if (parts == null) throw new ArgumentNullException(nameof(parts));
            if (parts.Length == 0 || parts.Any(p => p == null)) throw new ArgumentException("Supply all compressed parts.", nameof(parts));
            if (parts.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != parts.Length)
                throw new ArgumentException("Part identifiers must be unique.", nameof(parts));
            double fy = parts[0].Classification.YieldStrength.Value;
            if (parts.Any(p => p.Classification.YieldStrength != fy))
                throw new NotSupportedException("The section treatment requires one common yield strength.");
            return Ec3StressBasedClassification.Evaluate(fy, gammaM0, parts.Select(p => new Ec3CompressionPart(
                p.Classification.NominalClass.Value, p.Classification.WidthThicknessRatio.Value,
                p.Classification.Class3Limit.Value / p.Classification.Epsilon.Value, p.SigmaComEd)).ToArray());
        }
    }
}
