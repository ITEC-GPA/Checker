using System;
using System.Collections.Generic;
using System.Linq;

namespace GPC.Checkers.Concrete.Durability
{
    /// <summary>
    /// Exposure class with the values of EN 206 Table F.1 (informative: maximum w/c, minimum strength class as fck, minimum cement content kg/m³,
    /// minimum air content %), the column of EN 1992-1-1 Table 4.4N (−1 = no corrosion exposure), the NTC environment group (0 ordinary,
    /// 1 aggressive, 2 very aggressive, NTC Tab. 4.1.III) and the UNI 11104 requirements (minimum fck, maximum w/c, minimum cement; UNI 11104:2016, prospetto 5).
    /// Transferred from ANTHEA (Materiali.Durability, MinimumConcrete, AtecapMix and NtcCover.Severity, commit fe4652c). EN 206 F.1 and NTC
    /// Tab. 4.1.III verified on the texts; UNI 11104:2016 prospetto 5 from ANTHEA, as reproduced in ATECAP 2020 p. 19 (secondary source). UNI 11104:2025 (in force since 24/07/2025), prospetto 6, according to an extract published on 28/07/2025 (secondary source, to be checked on the text of the standard):
    /// same minimum classes except XF1 (C30/37), w/c 0.55 for XF1 and lower minimum cement contents; XC3, XD1, XF4 and XA1 are C30/37 in both editions (C28/35 attributed to UNI 11104:2004). Indicative classes of Annex E: EN 1992-1-1:2004 Table E.1N, DM 31/07/2012 Prospetto E.1N
    /// (XC1 C25/30, XF2 C30/37; merged cells read as in the recommended table), DK NA:2024 Tabel E.1(2).
    /// </summary>
    public sealed class ExposureClass
    {
        public string Code { get; }
        public string Description { get; }
        public double? En206MaxWaterCement { get; }
        public int En206MinStrength { get; }
        public int En206MinCement { get; }
        public double En206MinAir { get; }
        public int CoverColumn { get; }
        public int NtcEnvironment { get; }
        public int Uni11104MinStrength { get; }
        public double? Uni11104MaxWaterCement { get; }
        public int? Uni11104MinCement { get; }
        /// <summary>EN 1992-1-1 Table E.1N (recommended), fck of the indicative minimum class; null for XF4 (not in the table).</summary>
        public int? En1992IndicativeStrength { get; }
        /// <summary>UNI EN 1992-1-1, DM 31/07/2012 Prospetto E.1N; null for XF4.</summary>
        public int? UniIndicativeStrength { get; }
        /// <summary>DS/EN 1992-1-1 DK NA Tabel E.1(2): minimum specified fck for reinforced concrete.</summary>
        public int DkMinimumStrength { get; }
        internal ExposureClass(string code, string description, double? ratio, int strength, int cement, double air, int column, int environment, int uniStrength,
            double? uniRatio, int? uniCement, int? enIndicative, int? uniIndicative, int dkMinimum)
        {
            Code = code; Description = description; En206MaxWaterCement = ratio; En206MinStrength = strength; En206MinCement = cement; En206MinAir = air;
            CoverColumn = column; NtcEnvironment = environment; Uni11104MinStrength = uniStrength; Uni11104MaxWaterCement = uniRatio; Uni11104MinCement = uniCement;
            En1992IndicativeStrength = enIndicative; UniIndicativeStrength = uniIndicative; DkMinimumStrength = dkMinimum;
        }
    }

    /// <summary>Minimum fck of an exposure combination, MPa (null when no class of the combination is in the table), with the classes not covered.</summary>
    public sealed class StrengthRequirement
    {
        public int? Fck { get; }
        public string Reference { get; }
        public IReadOnlyList<string> Undefined { get; }
        internal StrengthRequirement(int? fck, string reference, string[] undefined) { Fck = fck; Reference = reference; Undefined = undefined; }
    }

    /// <summary>Exposure classes of EN 206 and their requirements; combinations of classes act together (the most severe value governs).</summary>
    public static class ExposureClasses
    {
        private static readonly ExposureClass[] Catalog = new[]
        {
            new ExposureClass("X0", "No risk; for reinforcement very dry", null, 12, 0, 0, 0, 0, 12, null, null, 12, 12, 12),
            new ExposureClass("XC1", "Carbonation, dry or permanently wet", .65, 20, 260, 0, 1, 0, 25, .60, 300, 20, 25, 12),
            new ExposureClass("XC2", "Carbonation, wet, rarely dry", .60, 25, 280, 0, 2, 0, 25, .60, 300, 25, 25, 30),
            new ExposureClass("XC3", "Carbonation, moderate humidity", .55, 30, 280, 0, 2, 0, 30, .55, 320, 25, 25, 30),
            new ExposureClass("XC4", "Carbonation, cyclic wet and dry", .50, 30, 300, 0, 3, 1, 32, .50, 340, 30, 30, 30),
            new ExposureClass("XD1", "Chlorides not from sea water, moderate humidity", .55, 30, 300, 0, 4, 1, 30, .55, 320, 30, 30, 35),
            new ExposureClass("XD2", "Chlorides not from sea water, wet, rarely dry", .55, 30, 300, 0, 5, 2, 32, .50, 340, 30, 30, 40),
            new ExposureClass("XD3", "Chlorides not from sea water, cyclic wet and dry", .45, 35, 320, 0, 6, 2, 35, .45, 360, 35, 35, 40),
            new ExposureClass("XS1", "Sea water, airborne salt", .50, 30, 300, 0, 4, 1, 32, .50, 340, 30, 30, 35),
            new ExposureClass("XS2", "Sea water, permanently submerged", .45, 35, 320, 0, 5, 2, 35, .45, 360, 35, 35, 35),
            new ExposureClass("XS3", "Sea water, tidal, splash and spray zones", .45, 35, 340, 0, 6, 2, 35, .45, 360, 35, 35, 40),
            new ExposureClass("XF1", "Freeze/thaw, moderate saturation, no de-icing agent", .55, 30, 300, 0, -1, 0, 32, .50, 320, 30, 30, 30),
            new ExposureClass("XF2", "Freeze/thaw, moderate saturation, de-icing agent", .55, 25, 300, 4, -1, 1, 25, .50, 340, 25, 30, 35),
            new ExposureClass("XF3", "Freeze/thaw, high saturation, no de-icing agent", .50, 30, 320, 4, -1, 1, 25, .50, 340, 30, 30, 35),
            new ExposureClass("XF4", "Freeze/thaw, high saturation, de-icing agent or sea water", .45, 30, 340, 4, -1, 2, 30, .45, 360, null, null, 40),
            new ExposureClass("XA1", "Slightly aggressive chemical environment", .55, 30, 300, 0, -1, 1, 30, .55, 320, 30, 30, 30),
            new ExposureClass("XA2", "Moderately aggressive chemical environment", .50, 30, 320, 0, -1, 1, 32, .50, 340, 30, 30, 35),
            new ExposureClass("XA3", "Highly aggressive chemical environment", .45, 35, 360, 0, -1, 2, 35, .45, 360, 35, 35, 40)
        };

        /// <summary>The 18 classes in this order: read-only view of the private catalog that Get and Resolve read (a cast to an array cannot change it).</summary>
        public static readonly IReadOnlyList<ExposureClass> All = new System.Collections.ObjectModel.ReadOnlyCollection<ExposureClass>(Catalog);

        public static ExposureClass Get(string code) => Catalog.FirstOrDefault(e => e.Code == code) ?? throw new ArgumentException("Unknown exposure class: " + code);

        /// <summary>At least one class; X0 cannot be combined with other classes.</summary>
        public static ExposureClass[] Resolve(IEnumerable<string> codes)
        {
            var values = (codes ?? throw new ArgumentNullException(nameof(codes))).Select(Get).ToArray();
            if (values.Length == 0) throw new ArgumentException("Select at least one exposure class.");
            if (values.Length > 1 && values.Any(x => x.Code == "X0")) throw new ArgumentException("X0 cannot be combined with other exposure classes.");
            return values;
        }

        /// <summary>UNI 11104:2016 (prospetto 5) minimum characteristic cylinder strength of the combination (most severe class), MPa.</summary>
        public static int Uni11104MinimumStrength(IEnumerable<string> codes) => Resolve(codes).Max(e => e.Uni11104MinStrength);

        /// <summary>EN 206 Table F.1 minimum strength of the combination, MPa.</summary>
        public static int En206MinimumStrength(IEnumerable<string> codes) => Resolve(codes).Max(e => e.En206MinStrength);

        /// <summary>
        /// Minimum strength of the combination for the durability of the profile: NTC 2018 and CNR-DT 200, UNI 11104 (NTC §11.2.11); EN 1992-1-1
        /// and UNI, indicative classes of Annex E (informative; XF4 is not in the table and is listed in <see cref="StrengthRequirement.Undefined"/>);
        /// DS, DK NA Tabel E.1(2) (requirement for reinforced concrete).
        /// </summary>
        public static StrengthRequirement MinimumStrength(DurabilityProfile profile, IEnumerable<string> codes)
        {
            var values = Resolve(codes);
            switch (profile)
            {
                case DurabilityProfile.Ntc2018:
                case DurabilityProfile.CnrDT200:
                    return new StrengthRequirement(values.Max(e => e.Uni11104MinStrength), "UNI 11104 prospetto 5 (NTC 2018 §11.2.11)", new string[0]);
                case DurabilityProfile.EN1992p11:
                case DurabilityProfile.UniEN1992p11:
                    bool uni = profile == DurabilityProfile.UniEN1992p11;
                    var defined = values.Select(e => uni ? e.UniIndicativeStrength : e.En1992IndicativeStrength).Where(v => v.HasValue).Select(v => v.Value).ToArray();
                    return new StrengthRequirement(defined.Length == 0 ? (int?)null : defined.Max(),
                        uni ? "UNI EN 1992-1-1 Annex E, DM 31/07/2012 Prospetto E.1N (informative)" : "EN 1992-1-1 Annex E, Table E.1N (informative)",
                        values.Where(e => (uni ? e.UniIndicativeStrength : e.En1992IndicativeStrength) == null).Select(e => e.Code).ToArray());
                case DurabilityProfile.DsEN1992p11:
                    return new StrengthRequirement(values.Max(e => e.DkMinimumStrength), "DS/EN 1992-1-1 DK NA Tabel E.1(2)", new string[0]);
                default: throw new ArgumentOutOfRangeException(nameof(profile));
            }
        }

        /// <summary>UNI 11104:2016 prospetto 5: largest w/c and smallest cement content allowed by the combination (null when no limit).</summary>
        public static Tuple<double?, int?> Uni11104Mix(IEnumerable<string> codes)
        {
            var values = Resolve(codes);
            return Tuple.Create(values.Min(e => e.Uni11104MaxWaterCement), values.Max(e => e.Uni11104MinCement));
        }

        /// <summary>Minimum entrained air content for XF2-XF4, %: 4 for dmax &gt; 20 mm, 5 for 12 ≤ dmax ≤ 16 mm; null otherwise (UNI 11104:2016 prospetto 5, note a; same note in UNI 11104:2025 prospetto 6).</summary>
        public static double? Uni11104Air(IEnumerable<string> codes, double maximumAggregate)
        {
            var values = Resolve(codes);
            if (double.IsNaN(maximumAggregate) || double.IsInfinity(maximumAggregate) || maximumAggregate <= 0) throw new ArgumentException("Invalid maximum aggregate size.");
            if (!values.Any(e => e.Code == "XF2" || e.Code == "XF3" || e.Code == "XF4")) return null;
            return maximumAggregate > 20 ? 4 : maximumAggregate >= 12 && maximumAggregate <= 16 ? 5 : (double?)null;
        }
    }
}
