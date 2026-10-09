using System;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Detailing
{
    /// <summary>Detailing and anchorage profiles implemented for ordinary reinforced concrete members.</summary>
    public enum DetailingProfile { Ntc2018, EN1992p11, UniEN1992p11, DsEN1992p11, CnrDT200 }

    /// <summary>
    /// Maps a typed standard to its detailing profile by exact type, without fallback. Implemented: NTC 2018 (transferred from ANTHEA) and
    /// CNR-DT 200 (NTC member), EN 1992-1-1 recommended values, UNI with the values of DM 31/07/2012, DS with DK NA (values of chapter 9
    /// "unchanged"; the national As,min and ρw,min of beams are not available here). DIN, NS and Model Code 2010 national rules are not
    /// implemented; CS-TR34 does not define member detailing.
    /// </summary>
    public static class DetailingProfiles
    {
        public static bool TryResolve(Standard standard, out DetailingProfile profile)
        {
            profile = default(DetailingProfile);
            if (standard == null) return false;
            var type = standard.GetType();
            if (type == typeof(StandardNTC2018Concrete)) profile = DetailingProfile.Ntc2018;
            else if (type == typeof(StandardEN1992p11)) profile = DetailingProfile.EN1992p11;
            else if (type == typeof(StandardUNIEN1992p11)) profile = DetailingProfile.UniEN1992p11;
            else if (type == typeof(StandardDSEN1992p11)) profile = DetailingProfile.DsEN1992p11;
            else if (type == typeof(StandardCNR200)) profile = DetailingProfile.CnrDT200;
            else return false;
            return true;
        }

        public static string NotApplicableReason(Standard standard)
            => standard != null && standard.GetType() == typeof(StandardCSTR34) ? "CS-TR34 covers ground-supported floor slabs: it does not define the detailing of beams and columns." : null;

        public static string NotSupportedReason(Standard standard)
        {
            if (standard == null) return null;
            var type = standard.GetType();
            if (type == typeof(StandardDINEN1992p11) || type == typeof(StandardNSEN1992p11))
                return standard.Name + ": the national rules of chapters 8 and 9 (anchorage, laps, minimum reinforcement) are not implemented.";
            if (type == typeof(StandardModelCode2010)) return "Model Code 2010: bond (6.1.3) and detailing rules are not implemented.";
            if (type == typeof(StandardCNR204)) return "CNR-DT 204: detailing of fibre-reinforced members is not implemented.";
            return null;
        }

        public static DetailingProfile Resolve(Standard standard)
        {
            if (standard == null) throw new ArgumentNullException(nameof(standard));
            if (!TryResolve(standard, out var profile))
                throw new NotSupportedException(NotApplicableReason(standard) ?? NotSupportedReason(standard) ?? "Detailing: no implemented profile for "
                    + standard.GetType().Name + (standard is StandardACI318 ? " (American standards: future implementation)." : "."));
            return profile;
        }

        internal static bool IsNtc(DetailingProfile p) => p == DetailingProfile.Ntc2018 || p == DetailingProfile.CnrDT200;

        public static string Reference(DetailingProfile profile)
        {
            switch (profile)
            {
                case DetailingProfile.Ntc2018: return "NTC 2018 §4.1.6.1 with EC2 §8";
                case DetailingProfile.CnrDT200: return "CNR-DT 200 R1/2013 with NTC 2018 §4.1.6.1 for the RC member";
                case DetailingProfile.EN1992p11: return "EN 1992-1-1 §§8, 9.2, 9.5 (recommended values)";
                case DetailingProfile.UniEN1992p11: return "UNI EN 1992-1-1 §§8, 9.2, 9.5 with DM 31/07/2012";
                case DetailingProfile.DsEN1992p11: return "DS/EN 1992-1-1 §§8, 9.2, 9.5 with DK NA";
                default: throw new ArgumentOutOfRangeException(nameof(profile));
            }
        }
    }
}
