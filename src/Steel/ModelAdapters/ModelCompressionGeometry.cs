using System;
using GPC.Checkers.Steel.Sections;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;

namespace GPC.Checkers.Steel.ModelAdapters
{
    /// <summary>Copies Model dimensions into scalar data for nominal uniform compression.
    /// It does not infer a load state, material, plastic stress block or national code.
    /// Other families/derived shapes require an explicitly qualified mapper.</summary>
    public static class ModelCompressionGeometry
    {
        public static CompressionSectionGeometry Create(ISteelSection section)
        {
            if (section == null) throw new ArgumentNullException(nameof(section));
            var shape = section.SectionShape;
            if (shape == null) throw new ArgumentException("A section shape is required.", nameof(section));
            if (shape.GetType() == typeof(SectionH))
            {
                var h = (SectionH)shape;
                return new CompressionSectionGeometry("H", new[] {
                    Outstand("TopFlange", (h.LenghtTopFlange - h.ThicknessWeb) / 2 - h.R, h.ThicknessTopFlange),
                    Outstand("BottomFlange", (h.LenghtBottomFlange - h.ThicknessWeb) / 2 - h.R, h.ThicknessBottomFlange),
                    Internal("Web", h.D, h.ThicknessWeb)
                });
            }
            if (shape.GetType() == typeof(SectionT))
            {
                var t = (SectionT)shape;
                return new CompressionSectionGeometry("T", new[] {
                    Outstand("Flange", (t.LenghtFlange - t.ThicknessWeb) / 2 - t.R, t.ThicknessFlange),
                    Outstand("Stem", t.HeightWeb, t.ThicknessWeb)
                });
            }
            if (shape.GetType() == typeof(SectionC))
            {
                var c = (SectionC)shape;
                return new CompressionSectionGeometry("C", new[] {
                    Outstand("TopFlange", c.LengthTop - c.ThicknessWeb - c.R1, c.ThicknessTop),
                    Outstand("BottomFlange", c.LengthBottom - c.ThicknessWeb - c.R1, c.ThicknessBottom),
                    Internal("Web", c.HeightWeb - 2 * c.R1, c.ThicknessWeb)
                });
            }
            if (shape.GetType() == typeof(SectionRHS))
            {
                var rhs = (SectionRHS)shape;
                // Model's RHS radius is currently not included in its section calculations.
                if (rhs.R != 0) throw new NotSupportedException("RoundedRhsClassificationGeometryNotQualified");
                return new CompressionSectionGeometry("RHS", new[] {
                    Internal("TopWall", rhs.BaseInternal, rhs.ThicknessTop),
                    Internal("BottomWall", rhs.BaseInternal, rhs.ThicknessBottom),
                    Internal("LeftWall", rhs.Heightinternal, rhs.ThicknessWebLeft),
                    Internal("RightWall", rhs.Heightinternal, rhs.ThicknessWebRight)
                });
            }
            if (shape.GetType() == typeof(SectionCHS))
            {
                var chs = (SectionCHS)shape;
                return new CompressionSectionGeometry("CHS", new[] {
                    new CompressionPartGeometry("Tube", CompressionPartKind.CircularHollow, chs.Diameter, chs.Thickness)
                });
            }
            throw new NotSupportedException("UniformCompressionGeometryNotQualified: " + shape.GetType().Name);
        }
        private static CompressionPartGeometry Internal(string id, double c, double t)
            => new CompressionPartGeometry(id, CompressionPartKind.InternalFlat, c, t);
        private static CompressionPartGeometry Outstand(string id, double c, double t)
            => new CompressionPartGeometry(id, CompressionPartKind.OutstandFlat, c, t);
    }
}
