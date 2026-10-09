using System;
using GPC.Checkers.Concrete.Shear;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Torsion
{
    /// <summary>Torsion profiles implemented for non-prestressed concrete sections with closed links.</summary>
    public enum TorsionProfile { Ntc2018, ModelCode2010, EN1992p11, UniEN1992p11, DinEN1992p11, DsEN1992p11, NsEN1992p11, CnrDT200 }

    /// <summary>
    /// Maps a typed standard to its torsion profile by exact type, without fallback (a derived class does not inherit the profile).
    /// CS-TR34 does not define the check (<see cref="NotApplicableReason"/>); CNR-DT 204 is known but not implemented
    /// (<see cref="NotSupportedReason"/>). American standards (ACI 318, AASHTO) are a future implementation.
    /// </summary>
    public static class TorsionProfiles
    {
        public static bool TryResolve(Standard standard, out TorsionProfile profile)
        {
            profile = default(TorsionProfile);
            if (standard == null) return false;
            var type = standard.GetType();
            if (type == typeof(StandardNTC2018Concrete)) profile = TorsionProfile.Ntc2018;
            else if (type == typeof(StandardModelCode2010)) profile = TorsionProfile.ModelCode2010;
            else if (type == typeof(StandardEN1992p11)) profile = TorsionProfile.EN1992p11;
            else if (type == typeof(StandardUNIEN1992p11)) profile = TorsionProfile.UniEN1992p11;
            else if (type == typeof(StandardDINEN1992p11)) profile = TorsionProfile.DinEN1992p11;
            else if (type == typeof(StandardDSEN1992p11)) profile = TorsionProfile.DsEN1992p11;
            else if (type == typeof(StandardNSEN1992p11)) profile = TorsionProfile.NsEN1992p11;
            else if (type == typeof(StandardCNR200)) profile = TorsionProfile.CnrDT200;
            else return false;
            return true;
        }

        /// <summary>Reason why the standard does not define the torsion check of a beam section; null otherwise.</summary>
        public static string NotApplicableReason(Standard standard)
        {
            if (standard != null && standard.GetType() == typeof(StandardCSTR34))
                return "CS-TR34 covers ground-supported floor slabs: it does not define the torsion check of a beam section.";
            return null;
        }

        /// <summary>Known standard whose torsion method is not implemented, with the reason; null otherwise.</summary>
        public static string NotSupportedReason(Standard standard)
        {
            if (standard != null && standard.GetType() == typeof(StandardCNR204))
                return "CNR-DT 204: torsion needs closed links, and fibres combined with shear reinforcement are not implemented (same limit as the shear check).";
            return null;
        }

        public static TorsionProfile Resolve(Standard standard)
        {
            if (standard == null) throw new ArgumentNullException(nameof(standard));
            if (!TryResolve(standard, out var profile))
                throw new NotSupportedException(NotApplicableReason(standard) ?? NotSupportedReason(standard) ?? "Torsion: no implemented profile for "
                    + standard.GetType().Name + (standard is StandardACI318 ? " (American standards: future implementation)." : "."));
            return profile;
        }

        /// <summary>Shear profile of the same standard, used for the concomitant shear with the common cot θ.</summary>
        internal static ShearProfile Shear(TorsionProfile profile)
        {
            switch (profile)
            {
                case TorsionProfile.Ntc2018: return ShearProfile.Ntc2018;
                case TorsionProfile.ModelCode2010: return ShearProfile.ModelCode2010;
                case TorsionProfile.EN1992p11: return ShearProfile.EN1992p11;
                case TorsionProfile.UniEN1992p11: return ShearProfile.UniEN1992p11;
                case TorsionProfile.DinEN1992p11: return ShearProfile.DinEN1992p11;
                case TorsionProfile.DsEN1992p11: return ShearProfile.DsEN1992p11;
                case TorsionProfile.NsEN1992p11: return ShearProfile.NsEN1992p11;
                case TorsionProfile.CnrDT200: return ShearProfile.CnrDT200;
                default: throw new ArgumentOutOfRangeException(nameof(profile));
            }
        }

        /// <summary>Upper limit of cot θ for torsion; the shear of each direction may restrict it further (DIN, NS).</summary>
        internal static double MaximumCotTheta(TorsionProfile profile)
        {
            switch (profile)
            {
                case TorsionProfile.ModelCode2010: return 1 / Math.Tan(20 * Math.PI / 180);
                case TorsionProfile.DinEN1992p11: return 3;
                case TorsionProfile.DsEN1992p11: return 2;
                default: return 2.5;
            }
        }

        /// <summary>Clauses of the implemented method; not a statement of full compliance.</summary>
        public static string Reference(TorsionProfile profile)
        {
            switch (profile)
            {
                case TorsionProfile.Ntc2018: return "NTC 2018 §4.1.2.3.6 (4.1.27-4.1.32)";
                case TorsionProfile.ModelCode2010: return "fib MC2010 §7.3.4";
                case TorsionProfile.EN1992p11: return "EN 1992-1-1 §6.3.2 (6.26-6.30)";
                case TorsionProfile.UniEN1992p11: return "UNI EN 1992-1-1 §6.3.2; ν from DM 31/07/2012 6.2.2(6)";
                case TorsionProfile.DinEN1992p11: return "DIN EN 1992-1-1 §6.3.2 with DIN EN 1992-1-1/NA";
                case TorsionProfile.DsEN1992p11: return "DS/EN 1992-1-1 §6.3.2 with DK NA (ν 5.6.1(3)P, θ 6.7a NA)";
                case TorsionProfile.NsEN1992p11: return "NS-EN 1992-1-1 §6.3.2";
                case TorsionProfile.CnrDT200: return "CNR-DT 200 R1/2013 §4.4 with NTC 2018 §4.1.2.3.6 for the RC member";
                default: throw new ArgumentOutOfRangeException(nameof(profile));
            }
        }

        public static string Model(TorsionProfile profile)
        {
            switch (profile)
            {
                case TorsionProfile.Ntc2018: return "NTC peripheral truss";
                case TorsionProfile.ModelCode2010: return "Model Code 2010 thin-walled truss";
                case TorsionProfile.CnrDT200: return "NTC member, FRP contribution";
                default: return "Eurocode 2 thin-walled truss";
            }
        }
    }
}
