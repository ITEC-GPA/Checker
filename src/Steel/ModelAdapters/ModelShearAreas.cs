using System;
using GPC.Checkers.Steel.Sections;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;

namespace GPC.Checkers.Steel.ModelAdapters
{
    /// <summary>Model dimensional reconstruction for the existing facade's shear-area
    /// conventions (mm2). Axis1/2 preserve its direction mapping; they do not newly
    /// qualify arbitrary section rotations, derived shapes or principal-axis mappings.
    /// These properties alone do not verify plastic shear, shear buckling or torsion.
    /// The caller validates geometry and numerical output; code selection is explicit.</summary>
    public static class ModelShearAreas
    {
        public static double GetArea2(ISteelSection section, SectionResistanceStandard standard)
        {
            if (!Enum.IsDefined(typeof(SectionResistanceStandard), standard)) throw new ArgumentOutOfRangeException(nameof(standard));
            if (section == null) throw new ArgumentNullException(nameof(section));
            if (section.SectionShape is SectionH sech && section.SectionType == Section.SectionTypes.Rolled)
                return SectionPropertyArithmetic.RolledWebArea(sech.Area, sech.LenghtTopFlange, sech.ThicknessTopFlange,
                    sech.LenghtBottomFlange, sech.ThicknessBottomFlange, sech.ThicknessWeb, 2 * sech.R);

            else if (section.SectionShape is SectionH secH && section.SectionType == Section.SectionTypes.Welded)
                return SectionPropertyArithmetic.WebArea(secH.ThicknessWeb, secH.HeightWeb);

            else if (section.SectionShape is SectionC secC && section.SectionType == Section.SectionTypes.Rolled)
                return SectionPropertyArithmetic.RolledWebArea(secC.Area, secC.LengthTop, secC.ThicknessTop,
                    secC.LengthBottom, secC.ThicknessBottom, secC.ThicknessWeb, secC.R1);

            else if (section.SectionShape is SectionC sectC && section.SectionType == Section.SectionTypes.Welded)
                return SectionPropertyArithmetic.WebArea(sectC.ThicknessWeb, sectC.Height - sectC.ThicknessBottom - sectC.ThicknessTop);

            else if (section.SectionShape is SectionRHS sectionRHS && section.SectionType == Section.SectionTypes.Welded)
                return SectionPropertyArithmetic.WebArea(sectionRHS.ThicknessWebLeft + sectionRHS.ThicknessWebRight, sectionRHS.Heightinternal);

            else if (section.SectionShape is SectionRHS sectionRhs && section.SectionType == Section.SectionTypes.Rolled)
                return SectionPropertyArithmetic.HollowAreaShare(sectionRhs.Area, sectionRhs.Height, sectionRhs.Base + sectionRhs.Height);

            else if (section.SectionShape is SectionCHS sectionCHS)
                return SectionPropertyArithmetic.CircularShearArea(sectionCHS.Area);

            else if (section.SectionShape is SectionT sectionT)
            {
                // NTC 2018 [4.2.21] differs from EN 1993-1-1:2005/AC:2009 6.2.6(3).
                if (standard == SectionResistanceStandard.Ntc2018)
                    return SectionPropertyArithmetic.NtcTeeWebArea(sectionT.Area, sectionT.LenghtFlange, sectionT.ThicknessFlange);
                if (section.SectionType == Section.SectionTypes.Welded)
                    return SectionPropertyArithmetic.WeldedTeeWebArea(sectionT.Height, sectionT.ThicknessFlange, sectionT.ThicknessWeb);
                return SectionPropertyArithmetic.RolledTeeWebArea(sectionT.Area, sectionT.LenghtFlange,
                    sectionT.ThicknessFlange, sectionT.ThicknessWeb, sectionT.R);
            }

            else if (section.SectionShape is SectionL sectionL)
                return SectionPropertyArithmetic.WebArea(sectionL.HorizontalLegLength, sectionL.HorizontalLegThickness);

            else
                throw new NotImplementedException("GetShearArea: not implemented section");
        }

        /// <summary>
        /// EN1993-1-1: 2005 Chapter 6.2.6(3)
        /// </summary>
        public static double GetArea1(ISteelSection section)
        {
            if (section == null) throw new ArgumentNullException(nameof(section));
            if (section.SectionShape is SectionH sech)
                return SectionPropertyArithmetic.FlangeArea(sech.LenghtTopFlange, sech.ThicknessTopFlange, sech.LenghtBottomFlange, sech.ThicknessBottomFlange);

            else if (section.SectionShape is SectionC secC)
                return SectionPropertyArithmetic.FlangeArea(secC.LengthTop, secC.ThicknessTop, secC.LengthBottom, secC.ThicknessBottom);

            else if (section.SectionShape is SectionRHS sectionRHS && section.SectionType == Section.SectionTypes.Welded)
                return sectionRHS.Area - SectionPropertyArithmetic.WebArea(sectionRHS.ThicknessWebLeft + sectionRHS.ThicknessWebRight, sectionRHS.Heightinternal);

            else if (section.SectionShape is SectionRHS sectionRhs && section.SectionType == Section.SectionTypes.Rolled)
                return SectionPropertyArithmetic.HollowAreaShare(sectionRhs.Area, sectionRhs.Base, sectionRhs.Base + sectionRhs.Height);

            else if (section.SectionShape is SectionCHS sectionCHS)
                return SectionPropertyArithmetic.CircularShearArea(sectionCHS.Area);

            else if (section.SectionShape is SectionT sectionT)
                return SectionPropertyArithmetic.WebArea(sectionT.LenghtFlange, sectionT.ThicknessFlange);

            else if (section.SectionShape is SectionL sectionL)
                return SectionPropertyArithmetic.WebArea(sectionL.VerticalLegLength, sectionL.VerticalLegThickness);

            else
                throw new NotImplementedException("GetShearArea: not implemented section");
        }

    }
}
