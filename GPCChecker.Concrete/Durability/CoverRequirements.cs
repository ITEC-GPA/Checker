using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Durability
{
    /// <summary>Durability and cover profiles.</summary>
    public enum DurabilityProfile { Ntc2018, EN1992p11, UniEN1992p11, DsEN1992p11, CnrDT200 }

    /// <summary>
    /// Maps a typed standard to its cover rules by exact type: NTC 2018 (Circolare 2019 table, transferred from ANTHEA NtcCover) and CNR-DT 200,
    /// EN 1992-1-1 Table 4.4N with the structural classes of Table 4.3N (ANTHEA Durability.Cover), UNI with DM 31/07/2012 (recommended values,
    /// verified), DS with DK NA:2024 Tabel 4.4N NA (no structural classes, verified). DIN, NS, MC2010 and CNR-DT 204: not supported.
    /// </summary>
    public static class DurabilityProfiles
    {
        public static bool TryResolve(Standard standard, out DurabilityProfile profile)
        {
            profile = default(DurabilityProfile);
            if (standard == null) return false;
            var type = standard.GetType();
            if (type == typeof(StandardNTC2018Concrete)) profile = DurabilityProfile.Ntc2018;
            else if (type == typeof(StandardEN1992p11)) profile = DurabilityProfile.EN1992p11;
            else if (type == typeof(StandardUNIEN1992p11)) profile = DurabilityProfile.UniEN1992p11;
            else if (type == typeof(StandardDSEN1992p11)) profile = DurabilityProfile.DsEN1992p11;
            else if (type == typeof(StandardCNR200)) profile = DurabilityProfile.CnrDT200;
            else return false;
            return true;
        }

        public static string NotSupportedReason(Standard standard)
        {
            if (standard == null || TryResolve(standard, out _)) return null;
            if (standard is StandardACI318) return standard.Name + ": American standards, future implementation.";
            return standard.Name + ": cover and durability rules (national tables) are not implemented.";
        }

        public static DurabilityProfile Resolve(Standard standard)
        {
            if (standard == null) throw new ArgumentNullException(nameof(standard));
            if (!TryResolve(standard, out var profile)) throw new NotSupportedException(NotSupportedReason(standard));
            return profile;
        }

        public static string Reference(DurabilityProfile profile)
        {
            switch (profile)
            {
                case DurabilityProfile.Ntc2018: return "NTC 2018 §4.1.6.1.3 and Circolare 2019 Tab. C4.1.IV";
                case DurabilityProfile.CnrDT200: return "CNR-DT 200 R1/2013 with NTC 2018 §4.1.6.1.3 for the RC member";
                case DurabilityProfile.EN1992p11: return "EN 1992-1-1 §4.4.1, Tables 4.3N and 4.4N";
                case DurabilityProfile.UniEN1992p11: return "UNI EN 1992-1-1 §4.4.1 with DM 31/07/2012 (recommended values)";
                case DurabilityProfile.DsEN1992p11: return "DS/EN 1992-1-1 §4.4.1 with DK NA:2024 Tabel 4.4N NA";
                default: throw new ArgumentOutOfRangeException(nameof(profile));
            }
        }
    }

    /// <summary>
    /// Data of the cover requirement. Units mm, MPa. Structural-class modifiers of EN Table 4.3N: design life 100 years (+2), strength class at least
    /// the threshold of the exposure (−1), slab geometry (−1), special quality control (−1). Abrasion k1/k2/k3 = 5/10/15 mm, concrete cast against
    /// prepared ground (40) or directly against soil (75). NTC: plate element, quality-control reduction and the pertinent Cmin (UNI 11104).
    /// </summary>
    public sealed class CoverInput
    {
        public IReadOnlyList<string> Exposures { get; }
        public double Fck { get; }
        public int DesignLife { get; }
        public bool StrengthReduction { get; }
        public bool SlabGeometry { get; }
        public bool SpecialQualityControl { get; }
        public double BarDiameter { get; }
        public double Aggregate { get; }
        public double Deviation { get; }
        public bool RoughSurface { get; }
        public int Abrasion { get; }
        public int Ground { get; }
        public bool PlateElement { get; }
        public bool NtcQualityReduction { get; }
        public double? PertinentCmin { get; }
        public CoverInput(IEnumerable<string> exposures, double fck, int designLife, bool strengthReduction, bool slabGeometry, bool specialQualityControl, double barDiameter,
            double aggregate, double deviation, bool roughSurface = false, int abrasion = 0, int ground = 0, bool plateElement = false, bool ntcQualityReduction = false,
            double? pertinentCmin = null)
        {
            Exposures = (exposures ?? throw new ArgumentNullException(nameof(exposures))).ToArray(); Fck = fck; DesignLife = designLife; StrengthReduction = strengthReduction;
            SlabGeometry = slabGeometry; SpecialQualityControl = specialQualityControl; BarDiameter = barDiameter; Aggregate = aggregate; Deviation = deviation;
            RoughSurface = roughSurface; Abrasion = abrasion; Ground = ground; PlateElement = plateElement; NtcQualityReduction = ntcQualityReduction; PertinentCmin = pertinentCmin;
        }
    }

    /// <summary>cmin,dur of one exposure class (EN: with its structural class).</summary>
    public sealed class CoverLine
    {
        public string Exposure { get; }
        public int? StructuralClass { get; }
        public double Durability { get; }
        internal CoverLine(string exposure, int? structuralClass, double durability) { Exposure = exposure; StructuralClass = structuralClass; Durability = durability; }
    }

    /// <summary>Cover requirement, mm: cmin,b (bond), cmin,dur (durability), cmin = max(10; cmin,b; cmin,dur) + surface and abrasion, cnom = max(cmin + Δcdev; ground).</summary>
    public sealed class CoverResult
    {
        public DurabilityProfile Profile { get; internal set; }
        public double Bond { get; internal set; }
        public double Durability { get; internal set; }
        public double Minimum { get; internal set; }
        public double Nominal { get; internal set; }
        public IReadOnlyList<CoverLine> Lines { get; internal set; } = new CoverLine[0];
        /// <summary>NTC: environment group 0-2, Cmin and C0 (MPa), table value and the additions (life, low strength) and quality reduction, mm.</summary>
        public int? NtcEnvironment { get; internal set; }
        public double? NtcCmin { get; internal set; }
        public double? NtcC0 { get; internal set; }
        public double? NtcTable { get; internal set; }
        public double? NtcLifeExtra { get; internal set; }
        public double? NtcLowStrengthExtra { get; internal set; }
        public double? NtcQualityReduction { get; internal set; }
        public string Reference { get; internal set; }
    }

    /// <summary>
    /// Cover requirement per standard, transferred from ANTHEA (Materiali.Durability.Cover and NtcCover.Calculate, commit fe4652c; fixtures
    /// durability-legacy.csv). EN 1992-1-1 / UNI: cmin,dur from Table 4.4N with structural class S4 modified by Table 4.3N (S1-S6). DS (DK NA):
    /// Tabel 4.4N NA by exposure group without structural classes, Δcdev ≥ 5 mm (4.4.1.3(1)P), 50 years. NTC: Circolare table by environment,
    /// element (plates 15/25/35 mm, others 20/30/40 mm for C ≥ C0, +5 mm for Cmin ≤ C &lt; C0), +10 mm for 100 years, +5 mm below Cmin, −5 mm with
    /// quality control.
    /// </summary>
    public static class CoverRequirements
    {
        // EN 1992-1-1 Table 4.4N, ordinary reinforcement: rows S1..S6, columns X0, XC1, XC2/XC3, XC4, XD1/XS1, XD2/XS2, XD3/XS3.
        private static readonly int[,] Table44N = {
            {10,10,10,15,20,25,30}, {10,10,15,20,25,30,35}, {10,10,20,25,30,35,40}, {10,15,25,30,35,40,45}, {15,20,30,35,40,45,50}, {20,25,35,40,45,50,55} };

        /// <summary>EN Table 4.3N: structural class of an exposure (S4 base; the strength threshold is C30/37 X0-XC1, C35/45 XC2-XC3, C40/50 XC4-XD2-XS1, else C45/55).</summary>
        public static int StructuralClass(ExposureClass e, CoverInput p)
        {
            int threshold = e.Code == "X0" || e.Code == "XC1" ? 30 : e.Code == "XC2" || e.Code == "XC3" ? 35 : e.Code == "XC4" || e.Code == "XD1" || e.Code == "XD2" || e.Code == "XS1" ? 40 : 45;
            return Math.Max(1, 4 + (p.DesignLife == 100 ? 2 : 0) - (p.StrengthReduction && p.Fck >= threshold ? 1 : 0) - (p.SlabGeometry ? 1 : 0) - (p.SpecialQualityControl ? 1 : 0));
        }

        public static CoverResult Calculate(DurabilityProfile profile, CoverInput p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var exposures = ExposureClasses.Resolve(p.Exposures);
            if (double.IsNaN(p.Fck) || p.Fck < 12 || p.Fck > 90) throw new ArgumentException("Invalid concrete strength.");
            if ((p.DesignLife != 50 && p.DesignLife != 100) || !Positive(p.BarDiameter) || !Positive(p.Aggregate) || double.IsNaN(p.Deviation) || double.IsInfinity(p.Deviation) || p.Deviation < 0
                || (p.Abrasion != 0 && p.Abrasion != 5 && p.Abrasion != 10 && p.Abrasion != 15) || (p.Ground != 0 && p.Ground != 40 && p.Ground != 75))
                throw new ArgumentException("Check bar diameter, maximum aggregate, deviation, design life, abrasion and casting conditions.");
            double bond = p.BarDiameter + (p.Aggregate > 32 ? 5 : 0);
            var result = new CoverResult { Profile = profile, Bond = bond, Reference = DurabilityProfiles.Reference(profile) };
            double durability;
            if (profile == DurabilityProfile.Ntc2018 || profile == DurabilityProfile.CnrDT200)
            {
                int severity = exposures.Max(e => e.NtcEnvironment);
                double c0 = 35 + 5 * severity, cmin = p.PertinentCmin ?? 25 + 5 * severity;
                if (double.IsNaN(cmin) || cmin < 12 || cmin > c0) throw new ArgumentException("Pertinent Cmin: give fck between 12 MPa and C0 = " + c0 + " MPa.");
                double table = (p.PlateElement ? 15 : 20) + 10 * severity + (p.Fck < c0 ? 5 : 0);
                double life = p.DesignLife == 100 ? 10 : 0, low = p.Fck < cmin ? 5 : 0, quality = p.NtcQualityReduction ? 5 : 0;
                durability = table + life + low - quality;
                result.NtcEnvironment = severity; result.NtcCmin = cmin; result.NtcC0 = c0; result.NtcTable = table; result.NtcLifeExtra = life;
                result.NtcLowStrengthExtra = low; result.NtcQualityReduction = quality;
            }
            else if (profile == DurabilityProfile.DsEN1992p11)
            {
                if (p.DesignLife != 50) throw new NotSupportedException("DK NA: the minimum covers of Tabel 4.4N NA are implemented for 50 years only.");
                if (p.Deviation < 5) throw new ArgumentException("DK NA 4.4.1.3(1)P: Δcdev not smaller than 5 mm.");
                var lines = exposures.Where(e => e.CoverColumn >= 0).Select(e => new CoverLine(e.Code, null,
                    e.Code == "X0" || e.Code == "XC1" ? 10 : e.Code == "XD2" || e.Code == "XD3" || e.Code == "XS3" ? 40 : e.Code == "XD1" || e.Code == "XS1" || e.Code == "XS2" ? 30 : 20)).ToArray();
                if (lines.Length == 0) throw new ArgumentException("Add the corrosion exposure (XC, XD or XS): XF/XA alone do not define cmin,dur.");
                durability = lines.Max(l => l.Durability); result.Lines = lines;
            }
            else
            {
                var lines = exposures.Where(e => e.CoverColumn >= 0).Select(e => { int s = StructuralClass(e, p); return new CoverLine(e.Code, s, Table44N[s - 1, e.CoverColumn]); }).ToArray();
                if (lines.Length == 0) throw new ArgumentException("Add the corrosion exposure (XC, XD or XS): XF/XA alone do not define cmin,dur.");
                durability = lines.Max(l => l.Durability); result.Lines = lines;
            }
            double minimum = Math.Max(10, Math.Max(bond, durability)) + (p.RoughSurface ? 5 : 0) + p.Abrasion;
            result.Durability = durability; result.Minimum = minimum; result.Nominal = Math.Max(minimum + p.Deviation, p.Ground);
            return result;
        }

        private static bool Positive(double v) => !double.IsNaN(v) && !double.IsInfinity(v) && v > 0;
    }
}
