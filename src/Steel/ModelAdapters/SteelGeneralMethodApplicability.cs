using System;
using GPC.Checkers.Steel.EffectiveSections;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.ModelAdapters
{
    /// <summary>Geometric subset of EN1993-1-1:2005 6.3.4(1). This check does not
    /// establish global-analysis validity, national applicability or restraints.</summary>
    public static class SteelGeneralMethodApplicability
    {
        public static void QualifySection(SteelSectionInput input,int memberClass)
        {
            if(input==null)throw new ArgumentNullException(nameof(input));
            if(memberClass<1||memberClass>4)throw new ArgumentOutOfRangeException(nameof(memberClass));
            var section=input.ReadOnlySection;var shape=section.SectionShape;var type=shape.GetType();
            bool known=type==typeof(SectionH)||type==typeof(SectionT)||type==typeof(SectionC)
                ||type==typeof(SectionL)||type==typeof(SectionRHS)||type==typeof(SectionRHSRoundedCorners)||type==typeof(SectionCHS);
            // L's symmetry is diagonal, so it cannot be read from Model's local X/Y flags.
            bool symmetric=shape.IsSymmetricAlongXLocalAxis||shape.IsSymmetricAlongYLocalAxis
                ||shape is SectionL l&&l.HorizontalLegLength==l.VerticalLegLength&&l.HorizontalLegThickness==l.VerticalLegThickness;
            if(!known||!symmetric||SteelMethodSelector.ColdFormedOpen(section))
                throw new NotSupportedException("General method requires a qualified mono/doubly symmetric section; unequal L, asymmetric C, cold-formed open and built-up sections are not qualified.");
            if(memberClass==4)SteelEffectiveSectionSolver.Qualify(input);
        }
    }
}
