using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using GPC.Checkers.Steel.EffectiveSections;
using GPC.Checkers.Steel.Inputs;

namespace GPC.Checkers.Steel.Members
{
    // Exact-input memo of the class4 member resistance properties (EN1993-1-5 4.3(3)/(4)) on one owned
    // section snapshot: Aeff under uniform compression and Weff per axis under pure bending depend on the
    // snapshot alone, never on the actions or on the combination (the bending solve also reads GammaM0,
    // for its unused utilization: it is part of the key). Engines that share one snapshot across
    // combinations and cuts compute them once. A rejected section throws as before and is not memoized.
    // The section class is NOT memoized: it remains a property of each action state.
    internal static class MemoizedEffectiveProperties
    {
        private const int CompressionAxis = 0;
        private static readonly ConditionalWeakTable<SteelSectionInput, ConcurrentDictionary<(int Axis, double GammaM0), double>> Values
            = new ConditionalWeakTable<SteelSectionInput, ConcurrentDictionary<(int, double), double>>();

        /// <summary>SteelEffectiveMemberProperties.Compression(section).Area.</summary>
        internal static double CompressionArea(SteelSectionInput section)
        {
            var values = Values.GetValue(section, _ => new ConcurrentDictionary<(int, double), double>());
            var key = (CompressionAxis, 0d);
            if (values.TryGetValue(key, out double area)) return area;
            area = SteelEffectiveMemberProperties.Compression(section).Area;
            return values.GetOrAdd(key, area);
        }

        /// <summary>SteelEffectiveMemberProperties.BendingModulus(input, axis).</summary>
        internal static double BendingModulus(SteelVerificationInput input, int axis)
        {
            var values = Values.GetValue(input.Section, _ => new ConcurrentDictionary<(int, double), double>());
            var key = (axis, input.Code.SectionCode.GammaM0);
            if (values.TryGetValue(key, out double modulus)) return modulus;
            modulus = SteelEffectiveMemberProperties.BendingModulus(input, axis);
            return values.GetOrAdd(key, modulus);
        }
    }
}
