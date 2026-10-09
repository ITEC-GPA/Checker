using System;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Shear
{
    /// <summary>Shear profiles implemented for non-prestressed concrete sections.</summary>
    public enum ShearProfile { Ntc2018, ModelCode2010, EN1992p11, UniEN1992p11, DinEN1992p11, DsEN1992p11, NsEN1992p11, CnrDT204, CnrDT200 }

    /// <summary>
    /// Maps a typed standard to its shear profile by exact type. A derived standard does not inherit the profile of its base class:
    /// an unknown type is reported as not supported, never silently replaced. Standards that do not define beam shear are reported
    /// as not applicable (<see cref="NotApplicableReason"/>). American standards (ACI 318, AASHTO) are a future implementation.
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
            else if (type == typeof(StandardCNR204)) profile = ShearProfile.CnrDT204;
            else if (type == typeof(StandardCNR200)) profile = ShearProfile.CnrDT200;
            else return false;
            return true;
        }

        /// <summary>Reason why the standard does not define the shear check of a beam section; null otherwise.</summary>
        public static string NotApplicableReason(Standard standard)
        {
            if (standard == null) return null;
            if (standard.GetType() == typeof(StandardCSTR34))
                return "CS-TR34 covers ground-supported floor slabs: it does not define the shear check of a beam section (punching is a separate check).";
            return null;
        }

        public static ShearProfile Resolve(Standard standard)
        {
            if (standard == null) throw new ArgumentNullException(nameof(standard));
            if (!TryResolve(standard, out var profile))
                throw new NotSupportedException(NotApplicableReason(standard) ?? "Shear: no implemented profile for " + standard.GetType().Name
                    + (standard is StandardACI318 ? " (American standards: future implementation)." : "."));
            return profile;
        }

        /// <summary>Clauses of the implemented method; not a statement of full compliance.</summary>
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
                case ShearProfile.CnrDT204: return "CNR-DT 204/2006 §4.2.3 (members without shear reinforcement); fib MC2010 §7.7.3.2.2";
                case ShearProfile.CnrDT200: return "CNR-DT 200 R1/2013 §4.3.3 with NTC 2018 §4.1.2.3.5 for the RC member";
                default: throw new ArgumentOutOfRangeException(nameof(profile));
            }
        }

        public static string Model(ShearProfile profile)
        {
            switch (profile)
            {
                case ShearProfile.Ntc2018: return "NTC variable-inclination truss";
                case ShearProfile.ModelCode2010: return "Model Code 2010 level II";
                case ShearProfile.CnrDT204: return "FRC without shear reinforcement";
                case ShearProfile.CnrDT200: return "NTC member, FRP contribution";
                default: return "Eurocode 2 first generation";
            }
        }
    }
}
