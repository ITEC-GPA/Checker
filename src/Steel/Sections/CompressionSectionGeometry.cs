using System;
using System.Collections.Generic;
using System.Linq;

namespace GPC.Checkers.Steel.Sections
{
    /// <summary>One immutable clear-width/thickness pair in mm. No Model geometry.
    /// A representative part may cover equivalent outstands under uniform compression.</summary>
    public sealed class CompressionPartGeometry
    {
        public string Id { get; }
        public CompressionPartKind Kind { get; }
        public double WidthOrDiameter { get; }
        public double Thickness { get; }
        public CompressionPartGeometry(string id, CompressionPartKind kind, double widthOrDiameter, double thickness)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A part identifier is required.", nameof(id));
            Id = id; Kind = kind; WidthOrDiameter = widthOrDiameter; Thickness = thickness;
        }
    }

    /// <summary>Snapshot of all parts for uniform-compression classification.
    /// The mapper owns completeness and geometry qualification; this container cannot
    /// infer omitted parts or additional family-specific requirements (e.g. angles).</summary>
    public sealed class CompressionSectionGeometry
    {
        public string Family { get; }
        public IReadOnlyList<CompressionPartGeometry> Parts { get; }
        public CompressionSectionGeometry(string family, IEnumerable<CompressionPartGeometry> parts)
        {
            if (string.IsNullOrWhiteSpace(family)) throw new ArgumentException("A geometry family is required.", nameof(family));
            if (parts == null) throw new ArgumentNullException(nameof(parts));
            var copy = parts.ToArray();
            if (copy.Length == 0 || copy.Any(p => p == null) || copy.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != copy.Length)
                throw new ArgumentException("Supply nonempty parts with unique identifiers.", nameof(parts));
            Family = family; Parts = Array.AsReadOnly(copy);
        }
    }

    public sealed class NamedCompressionPartClassification
    {
        public string Id { get; }
        public CompressionPartClassification Classification { get; }
        internal NamedCompressionPartClassification(string id, CompressionPartClassification classification)
        { Id = id; Classification = classification; }
    }

    /// <summary>Nominal class only, for uniform compression of all supplied parts.
    /// No stress-dependent class relaxation, resistance or member stability decision.</summary>
    public sealed class UniformCompressionClassification
    {
        public SectionClassificationStatus Status { get; }
        public int? NominalClass { get; }
        public string DiagnosticCode { get; }
        public IReadOnlyList<string> GoverningParts { get; }
        public IReadOnlyList<NamedCompressionPartClassification> Parts { get; }
        public bool IsEvaluated => Status == SectionClassificationStatus.Completed;
        internal UniformCompressionClassification(SectionClassificationStatus status, int? nominalClass,
            string diagnostic, NamedCompressionPartClassification[] parts)
        {
            Status = status; NominalClass = nominalClass; DiagnosticCode = diagnostic;
            Parts = Array.AsReadOnly(parts);
            GoverningParts = Array.AsReadOnly(nominalClass.HasValue
                ? parts.Where(p => p.Classification.NominalClass == nominalClass).Select(p => p.Id).ToArray()
                : new string[0]);
        }
    }

    public static class Ec3UniformCompressionClassification
    {
        public static UniformCompressionClassification Evaluate(CompressionSectionGeometry geometry, double? yieldStrength)
        {
            if (geometry == null) return new UniformCompressionClassification(SectionClassificationStatus.MissingInput,
                null, "Geometry.Required", new NamedCompressionPartClassification[0]);
            var parts = geometry.Parts.Select(p => new NamedCompressionPartClassification(p.Id,
                Ec3CompressionPartClassification.Evaluate(p.Kind, p.WidthOrDiameter, p.Thickness, yieldStrength))).ToArray();
            var failed = parts.FirstOrDefault(p => !p.Classification.IsEvaluated);
            if (failed != null) return new UniformCompressionClassification(failed.Classification.Status, null,
                failed.Id + "." + failed.Classification.DiagnosticCode, parts);
            return new UniformCompressionClassification(SectionClassificationStatus.Completed,
                parts.Max(p => p.Classification.NominalClass.Value), null, parts);
        }
    }
}
