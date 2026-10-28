using System;
using GPC.Checkers.Steel.Inputs;

namespace GPC.Checkers.Steel.ModelAdapters
{
    /// <summary>The one plastic shear resistance Av fy/(sqrt3 gammaM0) of the section routes,
    /// on the code-specific qualified shear areas (EC3 6.2.6(3), NTC 4.2.4.1.2.4). The operation
    /// order of the expressions it replaced is kept, so results stay bit-identical; it differs from
    /// SectionResistanceArithmetic.Shear only by rounding.</summary>
    internal static class SteelPlasticShear
    {
        internal static double Area(SteelVerificationInput input, int axis)
        {
            var section = input.Section.ReadOnlySection;
            return axis == 1 ? ModelShearAreas.GetArea1(section) : ModelShearAreas.GetArea2(section, input.Code.SectionCode.Standard);
        }

        internal static double Resistance(SteelVerificationInput input, int axis)
            => Area(input, axis) * input.Section.YieldStrength / (Math.Sqrt(3) * input.Code.SectionCode.GammaM0);
    }
}
