using System;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Cracking
{
    /// <summary>Crack-control profiles implemented for ordinary reinforced concrete sections.</summary>
    public enum CrackProfile { Ntc2018, ModelCode2010, EN1992p11, UniEN1992p11, DinEN1992p11, DsEN1992p11, NsEN1992p11, CnrDT200 }

    /// <summary>
    /// Maps a typed standard to its crack-control profile by exact type, without fallback. CS-TR34 does not define the crack width of a
    /// beam section (<see cref="NotApplicableReason"/>); CNR-DT 204 is known but its fibre-reinforced crack model is not implemented
    /// (<see cref="NotSupportedReason"/>). American standards (ACI 318, AASHTO) are a future implementation.
    /// </summary>
    public static class CrackProfiles
    {
        public static bool TryResolve(Standard standard, out CrackProfile profile)
        {
            profile = default(CrackProfile);
            if (standard == null) return false;
            var type = standard.GetType();
            if (type == typeof(StandardNTC2018Concrete)) profile = CrackProfile.Ntc2018;
            else if (type == typeof(StandardModelCode2010)) profile = CrackProfile.ModelCode2010;
            else if (type == typeof(StandardEN1992p11)) profile = CrackProfile.EN1992p11;
            else if (type == typeof(StandardUNIEN1992p11)) profile = CrackProfile.UniEN1992p11;
            else if (type == typeof(StandardDINEN1992p11)) profile = CrackProfile.DinEN1992p11;
            else if (type == typeof(StandardDSEN1992p11)) profile = CrackProfile.DsEN1992p11;
            else if (type == typeof(StandardNSEN1992p11)) profile = CrackProfile.NsEN1992p11;
            else if (type == typeof(StandardCNR200)) profile = CrackProfile.CnrDT200;
            else return false;
            return true;
        }

        public static string NotApplicableReason(Standard standard)
        {
            if (standard != null && standard.GetType() == typeof(StandardCSTR34))
                return "CS-TR34 covers ground-supported floor slabs: it does not define the crack width check of a beam section.";
            return null;
        }

        public static string NotSupportedReason(Standard standard)
        {
            if (standard != null && standard.GetType() == typeof(StandardCNR204))
                return "CNR-DT 204: the crack width of fibre-reinforced concrete (residual strength in the tension stiffening) is not implemented.";
            return null;
        }

        public static CrackProfile Resolve(Standard standard)
        {
            if (standard == null) throw new ArgumentNullException(nameof(standard));
            if (!TryResolve(standard, out var profile))
                throw new NotSupportedException(NotApplicableReason(standard) ?? NotSupportedReason(standard) ?? "Cracking: no implemented profile for "
                    + standard.GetType().Name + (standard is StandardACI318 ? " (American standards: future implementation)." : "."));
            return profile;
        }

        /// <summary>The NTC 2018 crack-width formula and requirement table (also CNR-DT 200 without FRP data).</summary>
        internal static bool IsNtc(CrackProfile p) => p == CrackProfile.Ntc2018 || p == CrackProfile.CnrDT200;

        public static string Reference(CrackProfile profile)
        {
            switch (profile)
            {
                case CrackProfile.Ntc2018: return "NTC 2018 §4.1.2.2.4 and Circolare 2019 C4.1.2.2.4";
                case CrackProfile.ModelCode2010: return "fib MC2010 §7.6.4";
                case CrackProfile.EN1992p11: return "EN 1992-1-1 §7.3.2-7.3.4";
                case CrackProfile.UniEN1992p11: return "UNI EN 1992-1-1 §7.3.4 (requirements of NTC 2018)";
                case CrackProfile.DinEN1992p11: return "DIN EN 1992-1-1 §7.3 with NA";
                case CrackProfile.DsEN1992p11: return "DS/EN 1992-1-1 §7.3 with DK NA";
                case CrackProfile.NsEN1992p11: return "NS-EN 1992-1-1 §7.3 with NA";
                case CrackProfile.CnrDT200: return "CNR-DT 200 R1/2013 with NTC 2018 §4.1.2.2.4 for the RC member";
                default: throw new ArgumentOutOfRangeException(nameof(profile));
            }
        }
    }
}
