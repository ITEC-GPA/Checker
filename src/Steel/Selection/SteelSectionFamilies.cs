using System;
using System.Collections.Generic;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;

namespace GPC.Checkers.Steel.Selection
{
    internal enum SteelSectionKind { Solid, IOrH, Tee, Channel, Angle, CircularTube, RectangularTube, BuiltUpPair }

    /// <summary>One profile per qualified family: what the routes may assume about it.
    /// The exact Model type selects the profile; a derived shape never inherits one.</summary>
    internal sealed class SteelSectionFamily
    {
        internal string Name { get; }
        internal SteelSectionKind Kind { get; }
        /// <summary>Open thin-walled profile: cold-formed members need their own design route.</summary>
        internal bool OpenProfile { get; }
        /// <summary>Bending resistance uses the elastic moduli even for class 1/2.</summary>
        internal bool ElasticBendingOnly { get; }
        private readonly Func<ISectionShape, bool> qualifies;
        private readonly Func<ISteelSection, bool> shearAreas;

        internal SteelSectionFamily(string name, SteelSectionKind kind, bool openProfile, bool elasticBendingOnly,
            Func<ISteelSection, bool> shearAreas, Func<ISectionShape, bool> qualifies = null)
        {
            Name = name; Kind = kind; OpenProfile = openProfile; ElasticBendingOnly = elasticBendingOnly;
            this.shearAreas = shearAreas; this.qualifies = qualifies ?? (s => true);
        }

        internal bool Qualifies(ISectionShape shape) => qualifies(shape);
        /// <summary>Code shear areas are mapped for this family (principal-axis orientation checked separately).</summary>
        internal bool HasShearAreas(ISteelSection section) => shearAreas(section);
    }

    internal static class SteelSectionFamilies
    {
        private static readonly Dictionary<Type, SteelSectionFamily> Table = new Dictionary<Type, SteelSectionFamily>
        {
            [typeof(SectionH)] = new SteelSectionFamily("H", SteelSectionKind.IOrH, true, false, s => true),
            [typeof(SectionT)] = new SteelSectionFamily("T", SteelSectionKind.Tee, true, false, s => true),
            [typeof(SectionC)] = new SteelSectionFamily("C", SteelSectionKind.Channel, true, false, s => true),
            [typeof(SectionL)] = new SteelSectionFamily("L", SteelSectionKind.Angle, true, true, s => false),
            [typeof(SectionBuiltUp)] = new SteelSectionFamily("BuiltUp", SteelSectionKind.BuiltUpPair, true, false, s => false),
            [typeof(SectionCHS)] = new SteelSectionFamily("CHS", SteelSectionKind.CircularTube, false, false, s => true),
            // Model's RHS radius is not part of its section calculations: sharp corners only.
            [typeof(SectionRHS)] = new SteelSectionFamily("RHS", SteelSectionKind.RectangularTube, false, false, s => true,
                shape => ((SectionRHS)shape).R == 0),
            [typeof(SectionRHSRoundedCorners)] = new SteelSectionFamily("RHSRounded", SteelSectionKind.RectangularTube, false, true,
                s => s.SectionType == Section.SectionTypes.Rolled && s.FormedType == Section.FormedTypes.HotFinished),
            [typeof(SectionRectangular)] = new SteelSectionFamily("Rectangular", SteelSectionKind.Solid, false, false, s => false),
            [typeof(SectionCircular)] = new SteelSectionFamily("Circular", SteelSectionKind.Solid, false, false, s => false),
        };

        /// <summary>The family profile, or null for an unqualified type or geometry.</summary>
        internal static SteelSectionFamily Of(ISectionShape shape)
            => shape != null && Table.TryGetValue(shape.GetType(), out var family) && family.Qualifies(shape) ? family : null;

        internal static bool Is(ISectionShape shape, SteelSectionKind kind) => Of(shape)?.Kind == kind;
    }
}
