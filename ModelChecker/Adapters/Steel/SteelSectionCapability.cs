using System;
using GPC.Model.Checking.Contracts;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;

namespace GPC.Model.Checker
{
    /// <summary>One qualification policy shared by engine discovery and execution.
    /// Catalog support is necessary, not sufficient: section/action gates still run for each cut.</summary>
    internal sealed class SteelSectionCapability
    {
        internal static readonly SteelSectionCapability Shear = new SteelSectionCapability(false);
        internal static readonly SteelSectionCapability Section = new SteelSectionCapability(true);
        internal bool IncludesBending { get; }
        internal string Id => IncludesBending ? "Steel.EN1993.Section" : "Steel.EN1993.PlasticShear";
        internal string Description => IncludesBending ? "Steel section shear and uniaxial bending" : "Steel section shear";
        internal string Limitations => "EN1993-1-1:2005; constant symmetric H sections, classes 1/2 in compression; T=0; no shear buckling. "
            + (IncludesBending ? "Bending requires N=0, one moment axis and low shear. " : "")
            + "Individual section checks; no N-M-V interaction or global stability.";
        internal const string ShearScope = "Two individual section shear checks. Does not certify N-M-V interaction or member stability.";
        internal const string BendingScope = "Uniaxial plastic cross-section bending, N=T=0, low shear, stocky symmetric H; member stability is a separate check.";

        private SteelSectionCapability(bool includesBending) { IncludesBending = includesBending; }

        internal CheckResult ContextFailure(StandardEN1993p11 code, string edition, CheckMechanism mechanism)
        {
            if (edition != "2005") return NativeResults.Unavailable("SteelEditionNotQualified");
            if (mechanism != CheckMechanism.Shear && !(IncludesBending && mechanism == CheckMechanism.UlsBiaxialSection))
                return NativeResults.Unavailable("SteelMethodNotQualified", "This adapter enables only explicitly qualified section primitives; member stability and section interactions remain unavailable.");
            if (code.GetType() != typeof(StandardEN1993p11)) return NativeResults.Unavailable("SteelStandardVariantNotQualified");
            return null;
        }

        internal static CheckResult GeometryFailure(SteelSection section)
        {
            if (!(section?.SectionShape is SectionH h) || h.GetType() != typeof(SectionH))
                return NativeResults.Unavailable("SteelRequiresConstantHSection");
            if (h.LenghtTopFlange != h.LenghtBottomFlange || h.ThicknessTopFlange != h.ThicknessBottomFlange)
                return NativeResults.Unavailable("SteelAsymmetricSectionNotQualified");
            return null;
        }

        internal static CheckResult TorsionFailure(ResultBeamForces forces)
            => forces.T != 0 ? NativeResults.Unavailable("SteelTorsionInteractionNotQualified") : null;

        internal static CheckResult ClassificationFailure(int sectionClass)
            => sectionClass > 2 ? NativeResults.Unavailable("SteelSlenderSectionNotQualified") : null;

        internal static CheckResult BendingActionsFailure(ResultBeamForces forces, double shearResistance1, double shearResistance2)
        {
            if (forces.N != 0 || forces.M1 != 0 && forces.M2 != 0)
                return NativeResults.Unavailable("SteelCombinedNormalActionsNotQualified");
            if (Math.Abs(forces.V1) > .5 * shearResistance1 || Math.Abs(forces.V2) > .5 * shearResistance2)
                return NativeResults.Unavailable("SteelHighShearBendingNotQualified");
            return null;
        }
    }
}
