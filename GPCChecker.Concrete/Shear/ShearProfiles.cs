using System;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Shear
{
    /// <summary>Shear profiles implemented for ordinary (non prestressed, normal-weight) concrete.</summary>
    public enum ShearProfile { Ntc2018, ModelCode2010, EN1992p11, UniEN1992p11, DinEN1992p11, DsEN1992p11, NsEN1992p11 }

    /// <summary>
    /// Maps a typed standard to its shear profile by exact type. A derived standard (for example StandardCNR200, derived from
    /// NTC 2018) does not inherit the profile of its base class: it is reported as not supported, never silently replaced.
    /// </summary>
    public static class ShearProfiles
    {
        public static bool TryResolve(Standard standard, out ShearProfile profile)
        {
            profile = default(ShearProfile);
            if (standard == null) return false;
            var type = standard.GetType();
            if (type == typeof(StandardNTC2018Concrete)) profile = ShearProfile.Ntc2018;
            else if (type == typeof(StandardModelCode2010)) profile = ShearProfile.ModelCode2010;
            else if (type == typeof(StandardEN1992p11)) profile = ShearProfile.EN1992p11;
            else if (type == typeof(StandardUNIEN1992p11)) profile = ShearProfile.UniEN1992p11;
            else if (type == typeof(StandardDINEN1992p11)) profile = ShearProfile.DinEN1992p11;
            else if (type == typeof(StandardDSEN1992p11)) profile = ShearProfile.DsEN1992p11;
            else if (type == typeof(StandardNSEN1992p11)) profile = ShearProfile.NsEN1992p11;
            else return false;
            return true;
        }

        public static ShearProfile Resolve(Standard standard)
        {
            if (standard == null) throw new ArgumentNullException(nameof(standard));
            if (!TryResolve(standard, out var profile))
                throw new NotSupportedException("Shear: no implemented profile for " + standard.GetType().Name + " (FRC, pavements and other standards are excluded).");
            return profile;
        }

        /// <summary>Clauses of the implemented method, as written in the legacy implementation; not a statement of full compliance.</summary>
        public static string Reference(ShearProfile profile)
        {
            switch (profile)
            {
                case ShearProfile.Ntc2018: return "NTC 2018 §§4.1.2.3.5.1-2";
                case ShearProfile.ModelCode2010: return "fib MC2010 level II §§7.3.3-7.3.4";
                case ShearProfile.EN1992p11: return "EN 1992-1-1 §§6.2.2-6.2.3";
                case ShearProfile.UniEN1992p11: return "UNI EN 1992-1-1 §§6.2.2-6.2.3";
                case ShearProfile.DinEN1992p11: return "DIN EN 1992-1-1 §§6.2.2-6.2.3";
                case ShearProfile.DsEN1992p11: return "DS EN 1992-1-1 §§6.2.2-6.2.3";
                case ShearProfile.NsEN1992p11: return "NS EN 1992-1-1 §§6.2.2-6.2.3";
                default: throw new ArgumentOutOfRangeException(nameof(profile));
            }
        }

        public static string Model(ShearProfile profile)
            => profile == ShearProfile.Ntc2018 ? "NTC variable-inclination truss" : profile == ShearProfile.ModelCode2010 ? "Model Code 2010 level II" : "Eurocode 2 first generation";
    }
}
