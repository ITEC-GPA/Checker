using GPC.Checkers.Concrete.Durability;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace ConcreteTests
{
    /// <summary>
    /// Contract of the durability rules of GPCChecker.Concrete 0.0.17.0 (ANTHEA F2.9, part CD0): regression snapshot of Durability/, captured from the
    /// code of Checker develop 4f54139a before any change of F2.9. Fixtures/durability-contract.json holds, for every state, every public member of the
    /// 0.0.17.0 results (listed explicitly: members added later are not part of the contract), or the exact type, message and parameter name of the
    /// exception:
    /// - catalog: every field of the 18 classes of ExposureClasses.All, in order; get: ExposureClasses.Get on the 18 codes and on invalid codes;
    /// - standards: DurabilityProfiles.TryResolve, NotSupportedReason and Resolve on every concrete Standard class of GPC.Model.Standards (Model
    ///   5ad56681), on a class derived from StandardEN1992p11 and on null; references: DurabilityProfiles.Reference on the 5 profiles and out of the enum;
    /// - on 37 exposure sets (the 24 sets of durability-legacy.csv in the order of the capture, XF4 twice as there; 6 sets for the Danish groups; empty
    ///   set, unknown and lower-case code, X0 twice, a repeated class, a null code, no list): Resolve, Uni11104MinimumStrength, En206MinimumStrength
    ///   and Uni11104Mix (combinations), MinimumStrength with Fck, Reference and Undefined on the 5 profiles and out of the enum (minimumStrength),
    ///   Uni11104Air on 15 maximum aggregate sizes (air);
    /// - structuralClass: CoverRequirements.StructuralClass for the 18 classes, design life 50 and 100, the 8 combinations of the three EN flags and
    ///   14 values of fck (12 … 90, out of the range, NaN);
    /// - CoverRequirements.Calculate on the 5 profiles (every member of CoverResult and of its lines):
    ///   coverCombinations, the 37 sets × 14 values of fck with the base options (50 years, no flag, Ø 16, Dmax 20, Δcdev 10, no Cmin);
    ///   coverOptions, the 18 classes × fck 30 and 50 × 50 and 100 years × the EN flags (strength, slab, quality) and the NTC flags (plate, quality);
    ///   coverGeometry, one factor at a time on X0, XC3 and XD3 (Ø, Dmax 0 … 40 and NaN, Δcdev 0, 3, 5, 10 and invalid, rough surface, abrasion,
    ///   ground, design life); coverAdditions, rough surface × abrasion × Δcdev × ground on XC3 and XD3; coverCmin, the pertinent Cmin absent, valid,
    ///   out of the range and NaN with fck 25 … 45 on the three NTC groups; coverErrorOrder, invalid exposures, fck, geometry, Cmin, design life and
    ///   Δcdev combined (order of the checks); coverSpecial, null input and profiles out of the enum; cminMessageItIT, the message of the pertinent
    ///   Cmin in it-IT.
    /// Doubles are written with the round-trip format (G17 where "R" does not give the same bits back, -0 kept), so the comparison is bit for bit.
    /// Generation runs with the invariant culture and UI culture (exception messages of mscorlib in English on every machine), except the it-IT section;
    /// the default text of ArgumentOutOfRangeException, which .NET Framework keeps in the culture of its first use, is written as a token (see Message).
    /// x64 test platform as the rest of the suite. Nothing about the runtime type of ExposureClasses.All is recorded.
    /// It is a regression snapshot, not an independent expectation: never regenerate it to make the test pass. On a difference the generated text is
    /// written next to the test assembly (durability-contract.actual.json) and the first differing line is reported.
    /// </summary>
    [TestClass]
    public class DurabilityContractTests
    {
        private const string FileName = "durability-contract.json";

        [TestMethod]
        public void DurabilityContractOf0017IsUnchanged()
        {
            Dictionary<string, int> counts;
            string actual = Generate(out counts);
            var expectedCounts = new Dictionary<string, int>
            {
                { "catalog", 18 }, { "get", 23 }, { "standards", 30 }, { "references", 7 }, { "combinations", 37 }, { "minimumStrength", 37 * 6 }, { "air", 37 },
                { "structuralClass", 18 * 2 * 8 + 2 }, { "coverCombinations", 5 * 37 * 14 }, { "coverOptions", 5 * 18 * 2 * 2 * (8 + 3) }, { "coverGeometry", 5 * 3 * 44 },
                { "coverAdditions", 5 * 2 * 2 * 4 * 4 * 3 }, { "coverCmin", 5 * 5 * 5 * 11 }, { "coverErrorOrder", 5 * 5 * 2 * 2 * 2 * 2 }, { "coverSpecial", 5 + 6 },
                { "cminMessageItIT", 3 },
            };
            CollectionAssert.AreEqual(expectedCounts.Keys.ToArray(), counts.Keys.ToArray(), "sections");
            foreach (var count in expectedCounts) Assert.AreEqual(count.Value, counts[count.Key], count.Key);
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", FileName);
            string actualPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "durability-contract.actual.json");
            if (!File.Exists(path))
            {
                File.WriteAllText(actualPath, actual, new UTF8Encoding(false));
                Assert.Fail("Missing " + path + "; generated text written to " + actualPath);
            }
            // The working tree may hold CRLF (core.autocrlf): the contract is compared with LF line ends.
            string expected = File.ReadAllText(path, new UTF8Encoding(false)).Replace("\r\n", "\n");
            if (string.Equals(expected, actual, StringComparison.Ordinal)) return;
            File.WriteAllText(actualPath, actual, new UTF8Encoding(false));
            var e = expected.Split('\n'); var a = actual.Split('\n');
            int line = 0;
            while (line < e.Length && line < a.Length && string.Equals(e[line], a[line], StringComparison.Ordinal)) line++;
            string section = "";
            for (int i = Math.Min(line, a.Length - 1); i >= 0; i--)
                if (a[i].StartsWith("  \"", StringComparison.Ordinal)) { section = a[i].Trim(); break; }
            Assert.Fail("Durability contract 0.0.17.0 changed at line " + (line + 1) + " (section " + section + ")\nexpected: " + (line < e.Length ? e[line] : "<end>")
                + "\nactual:   " + (line < a.Length ? a[line] : "<end>") + "\nGenerated text: " + actualPath);
        }

        // ---------------------------------------------------------------- generation

        // The profiles of 0.0.17.0, in the order of the enum; values out of the enum checked separately.
        private static readonly DurabilityProfile[] Profiles =
            { DurabilityProfile.Ntc2018, DurabilityProfile.EN1992p11, DurabilityProfile.UniEN1992p11, DurabilityProfile.DsEN1992p11, DurabilityProfile.CnrDT200 };
        private static readonly DurabilityProfile[] OutOfEnum = { (DurabilityProfile)99, (DurabilityProfile)(-1) };

        // The 18 codes of 0.0.17.0 in the order of the catalog, written here so that only the catalog section reads ExposureClasses.All.
        private static readonly string[] Codes = { "X0", "XC1", "XC2", "XC3", "XC4", "XD1", "XD2", "XD3", "XS1", "XS2", "XS3", "XF1", "XF2", "XF3", "XF4", "XA1", "XA2", "XA3" };

        private static readonly double[] FckValues = { 12, 20, 25, 28, 30, 32, 35, 40, 45, 50, 90, 11.9, 90.1, double.NaN };

        private static string Generate(out Dictionary<string, int> counts)
        {
            var thread = Thread.CurrentThread;
            var culture = thread.CurrentCulture; var uiCulture = thread.CurrentUICulture;
            try
            {
                thread.CurrentCulture = CultureInfo.InvariantCulture; thread.CurrentUICulture = CultureInfo.InvariantCulture;
                var sets = ExposureSets();
                var sections = new List<KeyValuePair<string, List<string>>>
                {
                    Section("catalog", Catalog()),
                    Section("get", Get()),
                    Section("standards", Standards()),
                    Section("references", References()),
                    Section("combinations", Combinations(sets)),
                    Section("minimumStrength", MinimumStrength(sets)),
                    Section("air", Air(sets)),
                    Section("structuralClass", StructuralClasses()),
                    Section("coverCombinations", CoverCombinations(sets)),
                    Section("coverOptions", CoverOptions()),
                    Section("coverGeometry", CoverGeometry()),
                    Section("coverAdditions", CoverAdditions()),
                    Section("coverCmin", CoverCmin()),
                    Section("coverErrorOrder", CoverErrorOrder()),
                    Section("coverSpecial", CoverSpecial()),
                    Section("cminMessageItIT", CminMessageItIT()),
                };
                counts = sections.ToDictionary(s => s.Key, s => s.Value.Count);
                var text = new StringBuilder();
                text.Append("{\n");
                text.Append("  \"contract\": ").Append(S("Durability contract of GPCChecker.Concrete 0.0.17.0 (ANTHEA F2.9 CD0): ExposureClasses, DurabilityProfiles and CoverRequirements")).Append(",\n");
                text.Append("  \"source\": ").Append(S("Checker develop 4f54139a, before any change of F2.9; Model 5ad56681; regression snapshot, not an independent expectation")).Append(",\n");
                text.Append("  \"input\": ").Append(S("[exposures, fck, designLife, strengthReduction, slabGeometry, specialQualityControl, barDiameter, aggregate, deviation, roughSurface, abrasion, ground, plateElement, ntcQualityReduction, pertinentCmin]")).Append(",\n");
                text.Append("  \"lines\": ").Append(S("[exposure, structuralClass, durability]")).Append(",\n");
                for (int i = 0; i < sections.Count; i++)
                {
                    var items = sections[i].Value;
                    text.Append("  ").Append(S(sections[i].Key)).Append(": ");
                    if (items.Count == 0) text.Append("[]");
                    else text.Append("[\n").Append(string.Join(",\n", items)).Append("\n  ]");
                    text.Append(i + 1 < sections.Count ? ",\n" : "\n");
                }
                text.Append("}\n");
                return text.ToString();
            }
            finally { thread.CurrentCulture = culture; thread.CurrentUICulture = uiCulture; }
        }

        private static KeyValuePair<string, List<string>> Section(string name, IEnumerable<string> items) => new KeyValuePair<string, List<string>>(name, items.ToList());

        // ---------------------------------------------------------------- JSON text

        /// <summary>One state on one line (4 spaces), fields in a fixed order.</summary>
        private sealed class Item
        {
            private readonly List<string> fields = new List<string>();
            public Item Raw(string key, string json) { fields.Add(S(key) + ": " + json); return this; }
            public Item Text(string key, string value) => Raw(key, S(value));
            public Item Number(string key, double? value) => Raw(key, N(value));
            public Item Integer(string key, int? value) => Raw(key, I(value));
            public Item Flag(string key, bool value) => Raw(key, B(value));
            public Item Failure(Exception ex, string call = null)
            {
                Text("exception", ex.GetType().FullName).Text("message", Message(ex));
                if (ex is ArgumentException argument) Text("paramName", argument.ParamName);
                if (call != null) Text("call", call);
                return this;
            }
            public string Render() => "    {" + string.Join(", ", fields) + "}";
        }

        private static string S(string value)
        {
            if (value == null) return "null";
            var b = new StringBuilder("\"");
            foreach (char ch in value)
            {
                switch (ch)
                {
                    case '"': b.Append("\\\""); break;
                    case '\\': b.Append("\\\\"); break;
                    case '\n': b.Append("\\n"); break;
                    case '\r': b.Append("\\r"); break;
                    case '\t': b.Append("\\t"); break;
                    default:
                        if (ch < 0x20) b.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture)); else b.Append(ch);
                        break;
                }
            }
            return b.Append('"').ToString();
        }

        /// <summary>Round-trip text of a double: "R", G17 where "R" does not give the same bits back, -0 kept, non-finite values as strings.</summary>
        private static string N(double v)
        {
            if (double.IsNaN(v)) return "\"NaN\"";
            if (double.IsPositiveInfinity(v)) return "\"Infinity\"";
            if (double.IsNegativeInfinity(v)) return "\"-Infinity\"";
            if (v == 0) return BitConverter.DoubleToInt64Bits(v) == 0 ? "0" : "-0";
            string r = v.ToString("R", CultureInfo.InvariantCulture);
            if (BitConverter.DoubleToInt64Bits(double.Parse(r, CultureInfo.InvariantCulture)) != BitConverter.DoubleToInt64Bits(v)) r = v.ToString("G17", CultureInfo.InvariantCulture);
            return r;
        }

        private static string N(double? v) => v.HasValue ? N(v.Value) : "null";
        private static string I(int? v) => v.HasValue ? v.Value.ToString(CultureInfo.InvariantCulture) : "null";
        private static string B(bool v) => v ? "true" : "false";
        private static string JsonArray(IEnumerable<string> items) => "[" + string.Join(", ", items) + "]";
        private static string JsonCodes(string[] codes) => codes == null ? "null" : JsonArray(codes.Select(S));

        private static string ExceptionJson(Exception ex)
            => "{\"exception\": " + S(ex.GetType().FullName) + ", \"message\": " + S(Message(ex)) + (ex is ArgumentException a ? ", \"paramName\": " + S(a.ParamName) : "") + "}";

        /// <summary>
        /// The message of the exception. .NET Framework keeps the default text of ArgumentOutOfRangeException in the UI culture of its first use in the
        /// process, so that text depends on the tests run before (Italian in the whole suite on an Italian machine): it is written as
        /// "&lt;default range message&gt;", the rest of the message ("Parameter name: …") is kept.
        /// </summary>
        private static string Message(Exception ex)
        {
            string message = ex.Message;
            if (ex is ArgumentOutOfRangeException)
            {
                string range = new ArgumentOutOfRangeException().Message;
                if (message.StartsWith(range, StringComparison.Ordinal)) message = "<default range message>" + message.Substring(range.Length);
            }
            return message;
        }

        /// <summary>The JSON value, or the exception object.</summary>
        private static string Try(Func<string> json)
        {
            try { return json(); }
            catch (Exception ex) { return ExceptionJson(ex); }
        }

        // ---------------------------------------------------------------- exposure sets

        private static string[][] FixtureRows()
            => File.ReadAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "durability-legacy.csv"), Encoding.UTF8)
                .Where(l => !l.StartsWith("#")).Select(l => l.Split(';')).ToArray();

        private static string[] Set(string codes) => codes.Split('+');

        /// <summary>
        /// The 24 sets of durability-legacy.csv (MIX rows, in the order of the capture: XF4 is repeated at the end as there), 6 sets for the Danish groups
        /// (10/20/30/40 mm, XF/XA without a corrosion class) and the invalid or limit sets.
        /// </summary>
        private static List<string[]> ExposureSets()
        {
            var sets = FixtureRows().Where(c => c[0] == "MIX").Select(c => Set(c[1])).ToList();
            if (sets.Count != 24) throw new InvalidOperationException("durability-legacy.csv: 24 MIX rows expected, found " + sets.Count);
            foreach (var codes in new[] { "XC1+XD2", "XC2+XS1", "XC3+XS2+XA1", "XS3+XF3", "XD1+XC4+XF4", "XF1+XA3" }) sets.Add(Set(codes));
            sets.Add(new string[0]);
            sets.Add(new[] { "XZ9" });
            sets.Add(new[] { "xc3" });
            sets.Add(new[] { "X0", "X0" });
            sets.Add(new[] { "XC3", "XC3" });
            sets.Add(new[] { "XC3", null });
            sets.Add(null);
            return sets;
        }

        // ---------------------------------------------------------------- catalog and profiles

        private static IEnumerable<string> Catalog()
        {
            int index = 0;
            foreach (var e in ExposureClasses.All)
                yield return new Item().Integer("index", index++).Text("code", e.Code).Text("description", e.Description)
                    .Number("en206MaxWaterCement", e.En206MaxWaterCement).Integer("en206MinStrength", e.En206MinStrength).Integer("en206MinCement", e.En206MinCement)
                    .Number("en206MinAir", e.En206MinAir).Integer("coverColumn", e.CoverColumn).Integer("ntcEnvironment", e.NtcEnvironment)
                    .Integer("uni11104MinStrength", e.Uni11104MinStrength).Number("uni11104MaxWaterCement", e.Uni11104MaxWaterCement)
                    .Integer("uni11104MinCement", e.Uni11104MinCement).Integer("en1992IndicativeStrength", e.En1992IndicativeStrength)
                    .Integer("uniIndicativeStrength", e.UniIndicativeStrength).Integer("dkMinimumStrength", e.DkMinimumStrength).Render();
        }

        /// <summary>Get returns the class of the catalog (the same object as the element of All) or refuses the code.</summary>
        private static IEnumerable<string> Get()
        {
            foreach (var code in Codes.Concat(new[] { "XZ9", "xc3", "", " XC3", null }))
            {
                var item = new Item().Text("code", code);
                try
                {
                    var e = ExposureClasses.Get(code);
                    item.Text("result", e.Code).Flag("sameAsAll", ExposureClasses.All.Any(x => ReferenceEquals(x, e)));
                }
                catch (Exception ex) { item.Failure(ex); }
                yield return item.Render();
            }
        }

        private sealed class CustomAnnex : StandardEN1992p11 { }

        /// <summary>Every concrete class of GPC.Model.Standards at Model 5ad56681, a derived class (resolution by exact type) and null.</summary>
        private static IEnumerable<string> Standards()
        {
            var standards = new Standard[]
            {
                new StandardAASHTO(), new StandardACI318p08(), new StandardACI318p14(), new StandardACI318p19(), new StandardAISC360p05(), new StandardAISC360p10(),
                new StandardAISC360p16(), new StandardASCE16(), new StandardCNR200(), new StandardCNR204(), new StandardCopSuos2011(), new StandardCSTR34(),
                new StandardDINEN1992p11(), new StandardDSEN1992p11(), new StandardEN16612(), new StandardEN1990(), new StandardEN1992p11(), new StandardEN1993p11(),
                new StandardEN1997p1(), new StandardEN1999p11(), new StandardModelCode2010(), new StandardNSEN1992p11(), new StandardNTC2018Concrete(),
                new StandardNTC2018Geotechnics(), new StandardNTC2018Steel(), new StandardUNIEN1990(), new StandardUNIEN1992p11(), new StandardUNIEN1993p11(),
                new CustomAnnex(), null
            };
            foreach (var standard in standards)
            {
                var item = new Item().Text("type", standard == null ? null : standard.GetType().Name).Text("name", standard == null ? null : standard.Name);
                try
                {
                    bool resolved = DurabilityProfiles.TryResolve(standard, out var profile);
                    item.Flag("tryResolve", resolved).Text("profileOut", profile.ToString());
                }
                catch (Exception ex) { item.Raw("tryResolve", ExceptionJson(ex)); }
                item.Raw("notSupportedReason", Try(() => S(DurabilityProfiles.NotSupportedReason(standard))));
                item.Raw("resolve", Try(() => S(DurabilityProfiles.Resolve(standard).ToString())));
                yield return item.Render();
            }
        }

        private static IEnumerable<string> References()
        {
            foreach (var profile in Profiles.Concat(OutOfEnum))
                yield return new Item().Text("profile", profile.ToString()).Raw("reference", Try(() => S(DurabilityProfiles.Reference(profile)))).Render();
        }

        // ---------------------------------------------------------------- exposure combinations

        private static IEnumerable<string> Combinations(List<string[]> sets)
        {
            foreach (var codes in sets)
                yield return new Item().Raw("exposures", JsonCodes(codes))
                    .Raw("resolve", Try(() => JsonArray(ExposureClasses.Resolve(codes).Select(e => S(e.Code)))))
                    .Raw("uni11104MinimumStrength", Try(() => I(ExposureClasses.Uni11104MinimumStrength(codes))))
                    .Raw("en206MinimumStrength", Try(() => I(ExposureClasses.En206MinimumStrength(codes))))
                    .Raw("uni11104Mix", Try(() => { var mix = ExposureClasses.Uni11104Mix(codes); return JsonArray(new[] { N(mix.Item1), I(mix.Item2) }); }))
                    .Render();
        }

        private static IEnumerable<string> MinimumStrength(List<string[]> sets)
        {
            foreach (var codes in sets)
                foreach (var profile in Profiles.Concat(OutOfEnum.Take(1)))
                {
                    var item = new Item().Raw("exposures", JsonCodes(codes)).Text("profile", profile.ToString());
                    try
                    {
                        var r = ExposureClasses.MinimumStrength(profile, codes);
                        item.Integer("fck", r.Fck).Text("reference", r.Reference).Raw("undefined", JsonArray(r.Undefined.Select(S)));
                    }
                    catch (Exception ex) { item.Failure(ex); }
                    yield return item.Render();
                }
        }

        private static readonly double[] AggregateSizes = { 0, 8, 11.9, 12, 16, 16.5, 18, 20, 20.5, 21, 32, 40, -8, double.NaN, double.PositiveInfinity };

        private static IEnumerable<string> Air(List<string[]> sets)
        {
            foreach (var codes in sets)
                yield return new Item().Raw("exposures", JsonCodes(codes))
                    .Raw("air", JsonArray(AggregateSizes.Select(d => JsonArray(new[] { N(d), Try(() => N(ExposureClasses.Uni11104Air(codes, d))) })))).Render();
        }

        // ---------------------------------------------------------------- structural class

        private static CoverInput Input(string[] codes, double fck, int life, bool reduction, bool slab, bool quality)
            => new CoverInput(codes, fck, life, reduction, slab, quality, 16, 20, 10);

        private static IEnumerable<string> StructuralClasses()
        {
            foreach (var code in Codes)
            {
                var e = ExposureClasses.Get(code);
                foreach (var life in new[] { 50, 100 })
                    for (int flags = 0; flags < 8; flags++)
                    {
                        bool reduction = (flags & 1) != 0, slab = (flags & 2) != 0, quality = (flags & 4) != 0;
                        yield return new Item().Text("exposure", code).Integer("designLife", life).Flag("strengthReduction", reduction).Flag("slabGeometry", slab)
                            .Flag("specialQualityControl", quality)
                            .Raw("byFck", JsonArray(FckValues.Select(fck => JsonArray(new[]
                                { N(fck), Try(() => I(CoverRequirements.StructuralClass(e, Input(new[] { code }, fck, life, reduction, slab, quality)))) })))).Render();
                    }
            }
            yield return new Item().Raw("exposure", "null").Raw("result", Try(() => I(CoverRequirements.StructuralClass(null, Input(new[] { "XC3" }, 30, 50, false, false, false))))).Render();
            yield return new Item().Text("exposure", "XC3").Raw("input", "null").Raw("result", Try(() => I(CoverRequirements.StructuralClass(ExposureClasses.Get("XC3"), null)))).Render();
        }

        // ---------------------------------------------------------------- cover requirement

        /// <summary>CoverInput arguments; the defaults are the base options.</summary>
        private sealed class Options
        {
            public double Fck = 30; public int Life = 50; public bool Reduction, Slab, Quality; public double Diameter = 16, Aggregate = 20, Deviation = 10;
            public bool Rough; public int Abrasion, Ground; public bool Plate, NtcQuality; public double? Cmin;
            public Options With(Action<Options> change) { var o = (Options)MemberwiseClone(); change(o); return o; }
        }

        private static readonly Options Base = new Options();

        private static string Cover(DurabilityProfile profile, string[] codes, Options o)
        {
            var item = new Item().Text("profile", profile.ToString()).Raw("input", JsonArray(new[]
            {
                JsonCodes(codes), N(o.Fck), I(o.Life), B(o.Reduction), B(o.Slab), B(o.Quality), N(o.Diameter), N(o.Aggregate), N(o.Deviation), B(o.Rough), I(o.Abrasion), I(o.Ground),
                B(o.Plate), B(o.NtcQuality), N(o.Cmin)
            }));
            CoverInput input;
            try { input = new CoverInput(codes, o.Fck, o.Life, o.Reduction, o.Slab, o.Quality, o.Diameter, o.Aggregate, o.Deviation, o.Rough, o.Abrasion, o.Ground, o.Plate, o.NtcQuality, o.Cmin); }
            catch (Exception ex) { return item.Failure(ex, "CoverInput").Render(); }
            return Calculate(item, profile, input);
        }

        private static string Calculate(Item item, DurabilityProfile profile, CoverInput input)
        {
            CoverResult r;
            try { r = CoverRequirements.Calculate(profile, input); }
            catch (Exception ex) { return item.Failure(ex, "Calculate").Render(); }
            return item.Text("resultProfile", r.Profile.ToString()).Number("bond", r.Bond).Number("durability", r.Durability).Number("minimum", r.Minimum).Number("nominal", r.Nominal)
                .Raw("lines", JsonArray(r.Lines.Select(l => JsonArray(new[] { S(l.Exposure), I(l.StructuralClass), N(l.Durability) }))))
                .Integer("ntcEnvironment", r.NtcEnvironment).Number("ntcCmin", r.NtcCmin).Number("ntcC0", r.NtcC0).Number("ntcTable", r.NtcTable)
                .Number("ntcLifeExtra", r.NtcLifeExtra).Number("ntcLowStrengthExtra", r.NtcLowStrengthExtra).Number("ntcQualityReduction", r.NtcQualityReduction)
                .Text("reference", r.Reference).Render();
        }

        private static IEnumerable<string> CoverCombinations(List<string[]> sets)
        {
            foreach (var profile in Profiles)
                foreach (var codes in sets)
                    foreach (var fck in FckValues)
                        yield return Cover(profile, codes, Base.With(x => x.Fck = fck));
        }

        /// <summary>Single classes, fck 30 and 50, 50 and 100 years: the 8 combinations of the EN flags, then the NTC flags (plate, quality) without them.</summary>
        private static IEnumerable<string> CoverOptions()
        {
            foreach (var profile in Profiles)
                foreach (var code in Codes)
                    foreach (var fck in new double[] { 30, 50 })
                        foreach (var life in new[] { 50, 100 })
                        {
                            for (int flags = 0; flags < 8; flags++)
                                yield return Cover(profile, new[] { code }, Base.With(x =>
                                {
                                    x.Fck = fck; x.Life = life; x.Reduction = (flags & 1) != 0; x.Slab = (flags & 2) != 0; x.Quality = (flags & 4) != 0;
                                }));
                            for (int flags = 1; flags < 4; flags++)
                                yield return Cover(profile, new[] { code }, Base.With(x => { x.Fck = fck; x.Life = life; x.Plate = (flags & 1) != 0; x.NtcQuality = (flags & 2) != 0; }));
                        }
        }

        /// <summary>One factor at a time from the base options (fck 30): 44 variations, the first is the base.</summary>
        private static IEnumerable<string> CoverGeometry()
        {
            var variations = new List<Action<Options>> { x => { } };
            foreach (var v in new double[] { 8, 12, 20, 25, 32, 40, 0, -8, double.NaN, double.PositiveInfinity }) variations.Add(x => x.Diameter = v);
            foreach (var v in new double[] { 0, 8, 12, 16, 18, 21, 32, 32.5, 40, -1, double.NaN, double.PositiveInfinity }) variations.Add(x => x.Aggregate = v);
            foreach (var v in new double[] { 0, 3, 4.99, 5, 15, -1, double.NaN, double.PositiveInfinity }) variations.Add(x => x.Deviation = v);
            variations.Add(x => x.Rough = true);
            foreach (var v in new[] { 5, 10, 15, 7, -5 }) variations.Add(x => x.Abrasion = v);
            foreach (var v in new[] { 40, 75, 50, -40 }) variations.Add(x => x.Ground = v);
            foreach (var v in new[] { 0, 75, 100 }) variations.Add(x => x.Life = v);
            foreach (var profile in Profiles)
                foreach (var code in new[] { "X0", "XC3", "XD3" })
                    foreach (var variation in variations)
                        yield return Cover(profile, new[] { code }, Base.With(variation));
        }

        /// <summary>cmin and cnom: rough surface × abrasion × Δcdev × ground.</summary>
        private static IEnumerable<string> CoverAdditions()
        {
            foreach (var profile in Profiles)
                foreach (var code in new[] { "XC3", "XD3" })
                    foreach (var rough in new[] { false, true })
                        foreach (var abrasion in new[] { 0, 5, 10, 15 })
                            foreach (var deviation in new double[] { 0, 3, 5, 10 })
                                foreach (var ground in new[] { 0, 40, 75 })
                                    yield return Cover(profile, new[] { code }, Base.With(x => { x.Rough = rough; x.Abrasion = abrasion; x.Deviation = deviation; x.Ground = ground; }));
        }

        /// <summary>Pertinent Cmin absent, valid, out of the range (below 12 MPa, above C0 = 35, 40 or 45 MPa) and NaN on the three NTC groups.</summary>
        private static IEnumerable<string> CoverCmin()
        {
            foreach (var profile in Profiles)
                foreach (var codes in new[] { "XC3", "XD1", "XD3", "XF1", "XC4+XS3+XF4" })
                    foreach (var fck in new double[] { 25, 30, 35, 40, 45 })
                        foreach (var cmin in new double?[] { null, 12, 25, 30, 32, 35, 40, 45, 11.9, 45.1, double.NaN })
                            yield return Cover(profile, Set(codes), Base.With(x => { x.Fck = fck; x.Cmin = cmin; }));
        }

        /// <summary>Errors combined: invalid exposures, fck, bar diameter, Cmin, design life and Δcdev (DS) in every combination; the first check wins.</summary>
        private static IEnumerable<string> CoverErrorOrder()
        {
            foreach (var profile in Profiles)
                foreach (var codes in new[] { new string[0], new[] { "XZ9" }, new[] { "X0", "XC1" }, new[] { "XF2" }, new[] { "XC3" } })
                    foreach (var fck in new[] { 30, double.NaN })
                        foreach (var diameter in new double[] { 16, 0 })
                            foreach (var cmin in new double?[] { null, 50 })
                                foreach (var lifeAndDeviation in new[] { Tuple.Create(50, 10.0), Tuple.Create(100, 3.0) })
                                    yield return Cover(profile, codes, Base.With(x =>
                                    {
                                        x.Fck = fck; x.Diameter = diameter; x.Cmin = cmin; x.Life = lifeAndDeviation.Item1; x.Deviation = lifeAndDeviation.Item2;
                                    }));
        }

        private static IEnumerable<string> CoverSpecial()
        {
            foreach (var profile in Profiles) yield return Calculate(new Item().Text("profile", profile.ToString()).Raw("input", "null"), profile, null);
            var outOfEnum = OutOfEnum[0];
            yield return Cover(outOfEnum, new[] { "XC3" }, Base);
            yield return Cover(outOfEnum, new string[0], Base);
            yield return Cover(outOfEnum, new[] { "XC3" }, Base.With(x => x.Fck = double.NaN));
            yield return Cover(outOfEnum, new[] { "XC3" }, Base.With(x => x.Diameter = 0));
            yield return Cover(outOfEnum, new[] { "XF2" }, Base);
            yield return Cover(OutOfEnum[1], new[] { "XC3" }, Base);
        }

        /// <summary>The message of the pertinent Cmin joins C0 in the current culture: it-IT, on the three NTC groups.</summary>
        private static IEnumerable<string> CminMessageItIT()
        {
            var thread = Thread.CurrentThread;
            var culture = thread.CurrentCulture;
            var items = new List<string>();
            try
            {
                thread.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
                foreach (var code in new[] { "XC3", "XD1", "XD3" }) items.Add(Cover(DurabilityProfile.Ntc2018, new[] { code }, Base.With(x => x.Cmin = 50)));
            }
            finally { thread.CurrentCulture = culture; }
            return items;
        }
    }
}
