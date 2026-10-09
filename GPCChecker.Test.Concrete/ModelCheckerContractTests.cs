using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Cracking;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Checkers.Concrete.Serviceability;
using GPC.Checkers.Concrete.Shear;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Persistence;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
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
    /// Contract of GPCChecker.Concrete 0.0.17.0 as ModelChecker uses it (ANTHEA F2.7, part K0): regression snapshot of
    /// <see cref="SectionCrackCheck.Evaluate"/> (existing <see cref="SectionCrackInput"/> constructor, default options, the arguments of
    /// Model ModelChecker ConcreteSectionVerifier.Cracking.cs) and of <see cref="StressLimitCheck.Evaluate"/> / <see cref="StressLimitCheck.NotApplicableReason"/>
    /// (ConcreteSectionVerifier.Checks.cs). Captured from the code of Checker develop 4f54139a before any change of F2.7.
    /// Fixtures/model-checker-contract.json holds, for every state, Details (symbol, value, unit, expression), Status, Outcome, Verdict, Reference and every other
    /// public member of the 0.0.17.0 results (listed explicitly: members added later are not part of the contract) and the number of calls of the
    /// uncracked-section function, or the exact type, message and parameter name of the exception with the call that threw it:
    /// - all the 936 states of crack-legacy.csv and the 2016 states of stress-legacy.csv, replayed as CrackMigrationTests / ServiceabilityMigrationTests do;
    /// - boundary inputs: invalid designLimit for NTC, UNI, EN and MC2010 in the required and in the other combinations; cover, spacing and nominal cover
    ///   overrides NaN, negative, zero or infinite; NaN / infinite bar stresses with a compressed, bent or decompression state; the three NoEffectiveArea
    ///   branches (zero hc,eff, no effective area with bars, entirely tensile face without steel); plain bars with MC2010 and DIN on every branch;
    ///   CS-TR34, CNR-DT 204 and ACI 318; the DIN condition (h − x)/3 ≥ c + 20 with nominal and assigned cover; the other outcomes ModelChecker maps;
    /// - the requirement grid through Evaluate (8 profiles, 3 combinations, 19 exposures, sensitivity, design limit) and the profile table;
    /// - stress limits of every standard, invalid factors and combinations.
    /// Doubles are written with the round-trip format (G17 where "R" does not round-trip, -0 kept), so the comparison is bit for bit. Generation runs with
    /// the invariant culture and UI culture (exception messages of mscorlib in English on every machine); x64 test platform as the rest of the suite.
    /// It is a regression snapshot, not an independent expectation: never regenerate it to make the test pass. On a difference the generated text is written
    /// to %TEMP%\model-checker-contract.actual.json and the first differing line is reported.
    /// </summary>
    [TestClass]
    public class ModelCheckerContractTests
    {
        private const string FileName = "model-checker-contract.json";

        [TestMethod]
        public void ModelCheckerContractOf0017IsUnchanged()
        {
            Dictionary<string, int> counts;
            string actual = Generate(out counts);
            Assert.AreEqual(936, counts["crackFixtureStates"], "crack fixture states");
            Assert.AreEqual(2016, counts["stressFixtureStates"], "stress fixture states");
            Assert.AreEqual(8 * 3 * 19 * 2 * 2, counts["crackRequirementGrid"], "requirement grid");
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", FileName);
            string actualPath = Path.Combine(Path.GetTempPath(), "model-checker-contract.actual.json");
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
            string state = "";
            for (int i = Math.Min(line, a.Length - 1); i >= 0; i--)
                if (a[i].TrimStart().StartsWith("\"id\": ", StringComparison.Ordinal)) { state = a[i].Trim() + (i + 1 < a.Length ? " " + a[i + 1].Trim() : ""); break; }
            Assert.Fail("Contract 0.0.17.0 changed at line " + (line + 1) + " (" + state + ")\nexpected: " + (line < e.Length ? e[line] : "<end>")
                + "\nactual:   " + (line < a.Length ? a[line] : "<end>") + "\nGenerated text: " + actualPath);
        }

        // ---------------------------------------------------------------- generation

        private static string Generate(out Dictionary<string, int> counts)
        {
            var thread = Thread.CurrentThread;
            var culture = thread.CurrentCulture; var uiCulture = thread.CurrentUICulture;
            try
            {
                thread.CurrentCulture = CultureInfo.InvariantCulture; thread.CurrentUICulture = CultureInfo.InvariantCulture;
                var sections = new List<KeyValuePair<string, List<string>>>
                {
                    Section("crackFixtureStates", CrackFixtureStates()),
                    Section("crackBoundaryStates", Numbered(CrackBoundaryStates())),
                    Section("crackRequirementGrid", CrackRequirementGrid()),
                    Section("crackProfiles", CrackProfileTable()),
                    Section("stressFixtureStates", StressFixtureStates()),
                    Section("stressBoundaryStates", Numbered(StressBoundaryStates())),
                };
                counts = sections.ToDictionary(s => s.Key, s => s.Value.Count);
                var text = new StringBuilder();
                text.Append("{\n");
                text.Append("  \"contract\": ").Append(S("ModelChecker contract of GPCChecker.Concrete 0.0.17.0 (ANTHEA F2.7 K0): SectionCrackCheck.Evaluate and StressLimitCheck.Evaluate with the arguments of ModelChecker, default options")).Append(",\n");
                text.Append("  \"source\": ").Append(S("Checker develop 4f54139a, before any change of F2.7; regression snapshot, not an independent expectation")).Append(",\n");
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

        /// <summary>Boundary states share their group name: the id gets the position in the section ("#7 group").</summary>
        private static IEnumerable<string> Numbered(IEnumerable<string> items)
        {
            const string marker = "\"id\": \"";
            int n = 0;
            foreach (var item in items)
            {
                int at = item.IndexOf(marker, StringComparison.Ordinal);
                yield return item.Substring(0, at + marker.Length) + "#" + (++n).ToString(CultureInfo.InvariantCulture) + " " + item.Substring(at + marker.Length);
            }
        }

        // ---------------------------------------------------------------- JSON text

        /// <summary>One state: fields one per line (6 spaces), arrays with one item per line (8 spaces).</summary>
        private sealed class Entry
        {
            private readonly List<string> fields = new List<string>();
            public Entry Raw(string key, string json) { fields.Add(S(key) + ": " + json); return this; }
            public Entry Text(string key, string value) => Raw(key, value == null ? "null" : S(value));
            public Entry Number(string key, double? value) => Raw(key, value.HasValue ? N(value.Value) : "null");
            public Entry Flag(string key, bool? value) => Raw(key, value.HasValue ? (value.Value ? "true" : "false") : "null");
            public Entry Lines(string key, IEnumerable<string> items)
            {
                var list = items.ToList();
                return Raw(key, list.Count == 0 ? "[]" : "[\n" + string.Join(",\n", list.Select(i => "        " + i)) + "\n      ]");
            }
            public string Render() => "    {\n" + string.Join(",\n", fields.Select(f => "      " + f)) + "\n    }";
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
        /// <summary>The same text without JSON quotes, inside descriptive strings.</summary>
        private static string P(double? v) => N(v).Trim('"');
        private static string JsonArray(IEnumerable<string> items) => "[" + string.Join(", ", items) + "]";
        private static string JsonPoint(Point2d p) => "[" + N(p.X) + ", " + N(p.Y) + "]";
        private static string JsonPolygon(IEnumerable<Point2d> points) => JsonArray(points.Select(p => JsonPoint(p)));

        private static void Failure(Entry entry, string call, Exception ex)
        {
            entry.Text("exception", ex.GetType().FullName).Text("message", ex.Message).Text("call", call);
            if (ex is ArgumentException argument) entry.Text("paramName", argument.ParamName);
        }

        // ---------------------------------------------------------------- crack check

        private static string JsonDetail(ShearCalculationDetail d) => JsonArray(new[] { S(d.Symbol), N(d.Value), S(d.Unit), S(d.Expression) });

        private static string JsonRegion(CrackRegion g) => JsonArray(new[]
        {
            S(g.Key), N(g.Qx), N(g.Qy), N(g.Level), N(g.Area), N(g.SteelArea), N(g.Width), JsonArray(g.BarIndices.Select(i => i.ToString(CultureInfo.InvariantCulture))),
            JsonPolygon(g.Outline), JsonArray(g.Holes.Select(h => JsonPolygon(h)))
        });

        /// <summary>Builds the input (as ModelChecker, inside its try) and evaluates it; the uncracked stress function is counted.</summary>
        private static string CrackState(string id, string input, Func<Func<double>, SectionCrackInput> build, Func<double> uncracked)
        {
            var entry = new Entry().Text("id", id).Text("input", input);
            int calls = 0;
            Func<double> counted = uncracked == null ? null : (Func<double>)(() => { calls++; return uncracked(); });
            SectionCrackInput p;
            try { p = build(counted); }
            catch (Exception ex) { Failure(entry, "SectionCrackInput", ex); return entry.Render(); }
            SectionCrackResult r;
            try { r = SectionCrackCheck.Evaluate(p); }
            catch (Exception ex) { Failure(entry, "SectionCrackCheck.Evaluate", ex); entry.Number("uncrackedCalls", calls); return entry.Render(); }
            entry.Text("profile", r.Profile.ToString())
                .Text("criterion", r.Requirement?.Criterion.ToString()).Number("requirementLimit", r.Requirement?.Limit)
                .Text("requiredCombination", r.Requirement?.RequiredCombination?.ToString())
                .Text("verdict", r.Verdict.ToString()).Text("outcome", r.Outcome.ToString()).Text("status", r.Status).Text("reference", r.Reference)
                .Number("width", r.Width).Number("limit", r.Limit).Number("ratio", r.Ratio).Flag("passed", r.Passed).Number("k2", r.K2)
                .Number("effectiveArea", r.EffectiveArea).Number("effectiveSteel", r.EffectiveSteel).Number("barSpacing", r.BarSpacing).Text("spacingSource", r.SpacingSource)
                .Number("uncrackedMaximumStress", r.UncrackedMaximumStress).Number("stressLimit", r.StressLimit).Text("governingRegion", r.GoverningRegion)
                .Number("uncrackedCalls", calls)
                .Lines("regions", r.Regions.Select(g => JsonRegion(g)))
                .Lines("details", r.Details.Select(d => JsonDetail(d)));
            return entry.Render();
        }

        private static double D(string s) => double.Parse(s, CultureInfo.InvariantCulture);
        private static double? Optional(string s) => s.Length == 0 ? (double?)null : D(s);
        private static string Or(string s) => s.Length == 0 ? "null" : s;
        private static string[] Rows(string name)
            => File.ReadAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", name), Encoding.UTF8).Where(l => !l.StartsWith("#")).Skip(1).ToArray();
        private static ServiceabilityCombination Combination(string set)
            => set == "SLE" ? ServiceabilityCombination.Characteristic : set == "SLE_QP" ? ServiceabilityCombination.QuasiPermanent : ServiceabilityCombination.Frequent;
        private static string Exposure(string legacy) => legacy == "Da scegliere" ? null : legacy;

        private static GPC.Model.Models.Model Archive(string name)
        {
            using (var stream = File.OpenRead(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", name))) return ModelArchive.Load(stream);
        }

        private static CoordinateSystem Axes(string origin, string v1, string v2)
        {
            double[] P(string s) => s.Split(',').Select(D).ToArray();
            var o = P(origin); var a = P(v1); var b = P(v2);
            return new CoordinateSystem(new Point3d(o[0], o[1], o[2]), new Vector3d(a[0], a[1], a[2]), new Vector3d(b[0], b[1], b[2]));
        }

        // Nominal cover to the longitudinal bars of the captured sections (CrackMigrationTests.Covers).
        private static readonly Dictionary<string, double> Covers = new Dictionary<string, double>
        { { "R300x500", 40 }, { "T1200x800", 80 }, { "C1000", 80 }, { "R600x800H", 80 }, { "C1000H", 80 }, { "R400x400", 45 } };

        /// <summary>The 936 states of crack-legacy.csv, built as CrackMigrationTests.LegacyCrackStatesAreReproduced; without strain plane ModelChecker stops before the check.</summary>
        private static IEnumerable<string> CrackFixtureStates()
        {
            var archive = Archive("crack-sections.xml");
            var checkers = new Dictionary<string, SectionCheckerModelCode2010>();
            SectionCheckerModelCode2010 Checker(string[] c, CoordinateSystem axes, bool linear, bool tension)
            {
                string key = string.Join("|", c[1], c[2], c[3], linear, c[5], tension, c[8], c[9], c[10]);
                if (checkers.TryGetValue(key, out var checker)) return checker;
                var standard = ServiceabilityMigrationTests.Standard(c[2]);
                foreach (var pair in c[3].Split(',')) { var kv = pair.Split('='); typeof(StandardModelCode2010).GetProperty(kv[0]).SetValue(standard, D(kv[1])); }
                var section = (ReinforcedConcreteSection)archive.BeamProperties[c[1]];
                var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(axes, SectionSolver.FailureAnalysisTypes.ConstantN, SectionSolver.FailureDomainTypes.Plastic,
                    linear ? SectionSolver.StressAnalysisTypes.Linear : SectionSolver.StressAnalysisTypes.NonLinear, D(c[5]), 0, tension, int.Parse(c[7]));
                checker = new SectionCheckerModelCode2010(new SectionCheckerAttribute(section, null, null), options, standard, tension);
                checkers[key] = checker; return checker;
            }
            foreach (var row in Rows("crack-legacy.csv"))
            {
                var c = row.Split(';');
                string id = "crack-legacy " + c[0] + " " + c[17];
                string input = string.Join(" | ", c[1], c[2], "linear " + c[4], "psi " + c[5], "tension " + c[6], c[17], c[18], c[19], c[20], c[21], "cover override " + Or(c[22]),
                    "spacing override " + Or(c[23]), "wlim " + Or(c[24]));
                var axes = Axes(c[8], c[9], c[10]);
                bool linear = bool.Parse(c[4]), tension = bool.Parse(c[6]);
                var section = (ReinforcedConcreteSection)archive.BeamProperties[c[1]];
                var force = new ResultBeamForces(D(c[11]), D(c[12]), D(c[13]), D(c[14]), D(c[15]), D(c[16]), axes);
                StressAnalysisResult stress = null; Exception analysisError = null;
                try { stress = Checker(c, axes, linear, tension).GetTensionAnalysisResult(force); }
                catch (Exception ex) { analysisError = ex; }
                if (analysisError != null)
                {
                    var failed = new Entry().Text("id", id).Text("input", input); Failure(failed, "GetTensionAnalysisResult", analysisError);
                    yield return failed.Render(); continue;
                }
                if (stress?.StrainPlane == null)
                {
                    yield return new Entry().Text("id", id).Text("input", input).Text("strainPlane", null).Text("modelChecker", "CheckerStressAnalysisNotConverged: the check is not called").Render();
                    continue;
                }
                var concrete = (ConcreteMaterialEuropeanCommon)section.ConcreteMaterial;
                yield return CrackState(id, input, uncracked => new SectionCrackInput(ServiceabilityMigrationTests.Standard(c[2]), Combination(c[17]), Exposure(c[18]), c[19] == "Sensibile",
                    Optional(c[24]), CrackSectionGeometry.From(section, c[1] == "C1000H"), stress.StrainPlane, SectionCrackInput.OrdinaryBarStresses(stress, section), linear, tension, false,
                    section.Rebars.First().RebarMaterial.E, concrete.Ecm, concrete.Fctm, c[20] == "Breve", c[21] == "Migliorata", Covers[c[1]], Optional(c[22]), Optional(c[23]), uncracked),
                    () => Checker(c, axes, true, true).GetTensionAnalysisResult(force) is var u ? u.GetConcreteVerticesTension(u.PsiRebar ?? 0).Max(v => v.tension) : 0);
            }
        }

        // ---- boundary inputs on hand-made sections (300×500, Ø20 = 314.16 mm², Es 200 GPa, Ecm 33 GPa, fctm 2.9 MPa, c 40 mm, σs = Es ε)

        private static readonly Point2d[] Rectangle = { new Point2d(-150, -250), new Point2d(150, -250), new Point2d(150, 250), new Point2d(-150, 250) };
        private static CrackBar[] Row(double y) => new[] { new CrackBar(-100, y, 20, 314.16), new CrackBar(0, y, 20, 314.16), new CrackBar(100, y, 20, 314.16) };
        private static readonly CrackBar[] Bottom = Row(-200), Top = Row(200), Doubly = Row(-200).Concat(Row(200)).ToArray(), OneBar = { new CrackBar(0, -200, 20, 314.16) };

        private sealed class PlaneCase
        {
            public string Name; public StrainPlane Value;
            public PlaneCase(string name, StrainPlane value) { Name = name; Value = value; }
        }
        // ε = χy (y − y0) + ε0 with the reference point (0, y0).
        private static readonly PlaneCase Bent = new PlaneCase("bent: neutral axis at y = 50, tension below", new StrainPlane(0, -4e-6, new Point2d(0, 50), 0));
        private static readonly PlaneCase Compressed = new PlaneCase("uniform compression −5e-4", new StrainPlane(0, 0, new Point2d(0, 0), -5e-4));
        private static readonly PlaneCase CompressedToZero = new PlaneCase("compressed, ε = 0 at the bottom edge", new StrainPlane(0, -1e-6, new Point2d(0, -250), 0));
        private static readonly PlaneCase Tensile = new PlaneCase("entirely tensile: 2e-4 top, 7e-4 bottom", new StrainPlane(0, -1e-6, new Point2d(0, 250), 2e-4));
        private static readonly PlaneCase Uniform = new PlaneCase("uniform tension 5e-4", new StrainPlane(0, 0, new Point2d(0, 0), 5e-4));
        private static readonly PlaneCase Unbonded = new PlaneCase("neutral axis at y = −150: tensile bars outside Ac,eff", new StrainPlane(0, -1e-5, new Point2d(0, -150), 0));
        private static readonly PlaneCase Cover = new PlaneCase("neutral axis at y = −230, in the bottom cover", new StrainPlane(0, -1e-5, new Point2d(0, -230), 0));
        private static readonly PlaneCase Middle = new PlaneCase("bent: neutral axis at y = 0", new StrainPlane(0, -1e-5, new Point2d(0, 0), 0));

        private static double[] Stresses(IEnumerable<CrackBar> bars, StrainPlane plane) => bars.Select(b => 200000 * plane.GetStrain(b.X, b.Y)).ToArray();

        /// <summary>Arguments of a hand-made state; defaults as ModelChecker passes them (linear cracked analysis, no tendon, no cover override).</summary>
        private sealed class Hand
        {
            public string Standard = "NTC 2018"; public ServiceabilityCombination Combination = ServiceabilityCombination.QuasiPermanent; public string Exposure = "XC1";
            public bool Sensitive; public double? DesignLimit; public Point2d[] Outline = Rectangle; public Point2d[][] Holes; public CrackBar[] Bars = Doubly; public PlaneCase Plane = Bent;
            public double[] BarStresses; public bool Linear = true, TensileConcrete, Prestressed; public double Es = 200000, Ecm = 33000, Fctm = 2.9; public bool ShortTerm, Ribbed = true;
            public double NominalCover = 40; public double? CoverOverride, SpacingOverride; public double? Uncracked = 1.2; public bool LegacyK2;
            public bool NullStandard, NullGeometry, NullPlane, NullStresses;

            public Hand With(Action<Hand> change) { var copy = (Hand)MemberwiseClone(); change(copy); return copy; }

            public string Describe() => string.Join(" | ", NullStandard ? "standard null" : Standard, Combination, Exposure ?? "exposure null", Sensitive ? "sensitive" : "little sensitive",
                "wlim " + P(DesignLimit), (Holes != null ? "holes " + Holes.Length + ", " : "") + Bars.Length + " bars" + (NullGeometry ? " (geometry null)" : ""),
                NullPlane ? "plane null" : Plane.Name, NullStresses ? "stresses null" : BarStresses == null ? "σs = Es ε" : "σs " + string.Join(", ", BarStresses.Select(v => P(v))),
                "linear " + Linear, "tensile concrete " + TensileConcrete, "prestressed " + Prestressed, "Es " + P(Es), "Ecm " + P(Ecm), "fctm " + P(Fctm),
                ShortTerm ? "short term" : "long term", Ribbed ? "ribbed" : "plain", "c " + P(NominalCover), "cover override " + P(CoverOverride),
                "spacing override " + P(SpacingOverride), "uncracked " + P(Uncracked), LegacyK2 ? "ntcK2FromCompressedBars" : "default k2 rule");

            public SectionCrackInput Build(Func<double> uncracked)
            {
                var geometry = NullGeometry ? null : new CrackSectionGeometry(Outline, Holes, Bars, CrackBarLayout.Rows);
                var stresses = NullStresses ? null : BarStresses ?? Stresses(Bars, Plane.Value);
                return new SectionCrackInput(NullStandard ? null : ServiceabilityMigrationTests.Standard(Standard), Combination, Exposure, Sensitive, DesignLimit, geometry,
                    NullPlane ? null : Plane.Value, stresses, Linear, TensileConcrete, Prestressed, Es, Ecm, Fctm, ShortTerm, Ribbed, NominalCover, CoverOverride, SpacingOverride,
                    Uncracked.HasValue ? uncracked : null, LegacyK2);
            }
        }

        private static string HandState(string group, Hand h)
        {
            double value = h.Uncracked ?? 0;
            return CrackState(group, h.Describe(), h.Build, h.Uncracked.HasValue ? () => value : (Func<double>)null);
        }

        private static readonly double[] InvalidLimits = { 0, -.2, double.NaN, double.PositiveInfinity };
        private static readonly ServiceabilityCombination[] Combinations = { ServiceabilityCombination.Characteristic, ServiceabilityCombination.Frequent, ServiceabilityCombination.QuasiPermanent };

        private static IEnumerable<string> CrackBoundaryStates()
        {
            var basic = new Hand();
            // Design limit: validated today for every profile and combination (ArgumentOutOfRangeException); 0.25 is valid (ignored by NTC / UNI).
            foreach (var standard in new[] { "NTC 2018", "UNI EN 1992-1-1", "EN 1992-1-1", "Model Code 2010" })
                foreach (var combination in Combinations)
                    foreach (double limit in InvalidLimits.Concat(new[] { .25 }))
                        yield return HandState("designLimit", basic.With(h => { h.Standard = standard; h.Combination = combination; h.DesignLimit = limit; h.Exposure = standard.StartsWith("NTC") || standard.StartsWith("UNI") ? "XC1" : "XC3"; }));

            // Overrides: where they enter (crack width, DIN hc,eff), where they do not (decompression, not required).
            var situations = new[]
            {
                basic,
                basic.With(h => { h.Exposure = "XD1"; h.Sensitive = true; }),
                basic.With(h => { h.Standard = "EN 1992-1-1"; h.Exposure = "XC3"; h.Combination = ServiceabilityCombination.Characteristic; }),
                basic.With(h => { h.Standard = "DIN EN 1992-1-1"; h.Exposure = "XC3"; h.Bars = Bottom; }),
            };
            foreach (var s in situations)
            {
                foreach (double v in new[] { double.NaN, -1, double.PositiveInfinity, 30 }) yield return HandState("coverOverride", s.With(h => h.CoverOverride = v));
                foreach (double v in new[] { double.NaN, -1, 0, double.PositiveInfinity, 150 }) yield return HandState("spacingOverride", s.With(h => h.SpacingOverride = v));
                foreach (double v in new[] { double.NaN, -1, double.PositiveInfinity }) yield return HandState("nominalCover", s.With(h => h.NominalCover = v));
            }

            // Bar stresses NaN or infinite: compressed, bent, decompression and not-required states, with and without the legacy k2 rule.
            foreach (var standard in new[] { "NTC 2018", "EN 1992-1-1" })
                foreach (var plane in new[] { Compressed, CompressedToZero, Bent })
                    foreach (double bad in new[] { double.NaN, double.PositiveInfinity })
                        foreach (bool legacy in new[] { false, true })
                        {
                            var stresses = Stresses(Doubly, plane.Value); stresses[0] = bad;
                            yield return HandState("barStresses", basic.With(h => { h.Standard = standard; h.Exposure = standard == "NTC 2018" ? "XC1" : "XC3"; h.Plane = plane; h.BarStresses = stresses; h.LegacyK2 = legacy; }));
                        }
            var allNaN = Doubly.Select(b => double.NaN).ToArray();
            yield return HandState("barStresses", basic.With(h => { h.Plane = Compressed; h.BarStresses = allNaN; }));
            foreach (bool legacy in new[] { false, true })
                yield return HandState("barStresses", basic.With(h => { h.Exposure = "XD1"; h.Sensitive = true; h.BarStresses = allNaN; h.LegacyK2 = legacy; }));
            yield return HandState("barStresses", basic.With(h => { h.Standard = "EN 1992-1-1"; h.Exposure = "XC3"; h.Combination = ServiceabilityCombination.Characteristic; h.BarStresses = allNaN; }));
            yield return HandState("barStresses", basic.With(h => { h.Plane = Compressed; h.Bars = new CrackBar[0]; }));

            // The three branches of NoEffectiveArea (NTC, EN, DIN: hc,eff = 0; Ac,eff = 0 with effective bars; entirely tensile face +y without steel); DS
            // finds its band by bisection and leaves the first two.
            var hole = new[] { new[] { new Point2d(-150, -250), new Point2d(150, -250), new Point2d(150, -200), new Point2d(-150, -200) } };
            foreach (var standard in new[] { "NTC 2018", "EN 1992-1-1", "DIN EN 1992-1-1", "DS EN 1992-1-1" })
            {
                string exposure = standard == "NTC 2018" ? "XC1" : "XC3";
                yield return HandState("effectiveArea: bars on the tensile edge", basic.With(h => { h.Standard = standard; h.Exposure = exposure; h.Bars = Row(-250); h.Plane = Middle; }));
                yield return HandState("effectiveArea: hole over the tensile band, bars in it", basic.With(h => { h.Standard = standard; h.Exposure = exposure; h.Holes = hole; h.Bars = Row(-240); h.Plane = Middle; }));
                yield return HandState("effectiveArea: entirely tensile, bars only at the bottom", basic.With(h => { h.Standard = standard; h.Exposure = exposure; h.Bars = Bottom; h.Plane = Uniform; h.SpacingOverride = 100; }));
            }

            // Plain bars: MC2010 and DIN implement only ribbed bars (every branch); EN and NTC use k1 = 1.6.
            foreach (var standard in new[] { "Model Code 2010", "DIN EN 1992-1-1" })
            {
                double? limit = standard == "Model Code 2010" ? .3 : (double?)null;
                foreach (var pair in new[] { Tuple.Create(Bent, Bottom), Tuple.Create(Unbonded, Bottom), Tuple.Create(Tensile, Doubly), Tuple.Create(Compressed, Doubly), Tuple.Create(Cover, Doubly) })
                    yield return HandState("plainBars", basic.With(h => { h.Standard = standard; h.Exposure = "XC3"; h.DesignLimit = limit; h.Plane = pair.Item1; h.Bars = pair.Item2; h.Ribbed = false; }));
                yield return HandState("plainBars", basic.With(h => { h.Standard = standard; h.Exposure = "XC3"; h.DesignLimit = limit; h.Bars = Bottom; }));
            }
            yield return HandState("plainBars", basic.With(h => { h.Standard = "EN 1992-1-1"; h.Exposure = "XC3"; h.Bars = Bottom; h.Ribbed = false; }));
            yield return HandState("plainBars", basic.With(h => { h.Bars = Bottom; h.Ribbed = false; }));

            // Standards without a crack profile.
            foreach (var standard in new[] { "CS-TR34", "CNR-DT 204/2006" })
                foreach (var combination in Combinations)
                    yield return HandState("noProfile", basic.With(h => { h.Standard = standard; h.Combination = combination; h.Exposure = "XC3"; }));
            yield return CrackState("noProfile", "ACI 318-19 | QuasiPermanent | XC3", u => new SectionCrackInput(new StandardACI318p19(), ServiceabilityCombination.QuasiPermanent, "XC3", false, null,
                new CrackSectionGeometry(Rectangle, null, Doubly, CrackBarLayout.Rows), Bent.Value, Stresses(Doubly, Bent.Value), true, false, false, 200000, 33000, 2.9, false, true, 40, null, null, u), () => 1.2);

            // DIN NCI 7.3.2(3): (h − x)/3 = 100 mm ≥ c + 20 with the nominal cover, also when the cover is assigned.
            foreach (var pair in new[] { Tuple.Create(40d, (double?)null), Tuple.Create(40d, (double?)90), Tuple.Create(90d, (double?)40), Tuple.Create(90d, (double?)null) })
                yield return HandState("dinEffectiveDepthCover", basic.With(h => { h.Standard = "DIN EN 1992-1-1"; h.Exposure = "XC3"; h.Bars = Bottom; h.NominalCover = pair.Item1; h.CoverOverride = pair.Item2; }));

            // Other outcomes and refusals mapped by ModelChecker.
            yield return HandState("outcome", basic.With(h => h.Exposure = null));
            yield return HandState("outcome", basic.With(h => { h.Standard = "EN 1992-1-1"; h.Exposure = null; }));
            yield return HandState("outcome", basic.With(h => { h.Standard = "Model Code 2010"; h.Exposure = "XC3"; }));
            yield return HandState("outcome", basic.With(h => { h.Standard = "DS EN 1992-1-1"; h.Exposure = "X0"; }));
            yield return HandState("outcome", basic.With(h => { h.Standard = "NS EN 1992-1-1"; h.Exposure = "XF1"; }));
            yield return HandState("outcome", basic.With(h => { h.Standard = "NS EN 1992-1-1"; h.Exposure = "XD3"; }));
            yield return HandState("outcome", basic.With(h => { h.Standard = "NS EN 1992-1-1"; h.Exposure = "XD3"; h.Combination = ServiceabilityCombination.Frequent; }));
            yield return HandState("outcome", basic.With(h => h.Exposure = "XY9"));
            yield return HandState("outcome", basic.With(h => { h.Exposure = "XD1"; h.Sensitive = true; h.Uncracked = null; }));
            yield return HandState("outcome", basic.With(h => { h.Exposure = "XD1"; h.Sensitive = true; h.Uncracked = -.5; }));
            yield return HandState("outcome", basic.With(h => { h.Exposure = "XS3"; h.Sensitive = true; h.Combination = ServiceabilityCombination.Frequent; }));
            yield return HandState("outcome", basic.With(h => { h.Exposure = "XS3"; h.Sensitive = true; h.Combination = ServiceabilityCombination.Frequent; h.Uncracked = 3; }));
            yield return HandState("outcome", basic.With(h => { h.Standard = "EN 1992-1-1"; h.Exposure = "XC3"; h.Prestressed = true; }));
            yield return HandState("outcome", basic.With(h => h.Linear = false));
            yield return HandState("outcome", basic.With(h => h.TensileConcrete = true));
            yield return HandState("outcome", basic.With(h => { h.Bars = Top; h.Plane = Cover; }));
            yield return HandState("outcome", basic.With(h => { h.Bars = OneBar; }));
            yield return HandState("outcome", basic.With(h => { h.Bars = Bottom; h.Plane = Unbonded; }));
            yield return HandState("outcome", basic.With(h => { h.Plane = Cover; }));
            yield return HandState("outcome", basic.With(h => { h.Plane = Tensile; }));
            yield return HandState("outcome", basic.With(h => { h.Plane = Tensile; h.Standard = "DS EN 1992-1-1"; h.Exposure = "XC3"; }));
            yield return HandState("outcome", basic.With(h => { h.Plane = Compressed; }));
            yield return HandState("outcome", basic.With(h => { h.Standard = "CNR-DT 200 R1/2013"; }));
            yield return HandState("outcome", basic.With(h => { h.ShortTerm = true; h.Bars = Bottom; }));
            yield return HandState("outcome", basic.With(h => { h.Bars = Bottom; h.LegacyK2 = true; }));

            // Constructor refusals.
            yield return HandState("constructor", basic.With(h => h.BarStresses = new[] { 1.0 }));
            yield return HandState("constructor", basic.With(h => h.Es = 0));
            yield return HandState("constructor", basic.With(h => h.Ecm = double.NaN));
            yield return HandState("constructor", basic.With(h => h.Fctm = -1));
            yield return HandState("constructor", basic.With(h => h.NullStandard = true));
            yield return HandState("constructor", basic.With(h => h.NullGeometry = true));
            yield return HandState("constructor", basic.With(h => h.NullPlane = true));
            yield return HandState("constructor", basic.With(h => h.NullStresses = true));
        }

        private static readonly string[] CrackStandards = { "NTC 2018", "Model Code 2010", "EN 1992-1-1", "UNI EN 1992-1-1", "DIN EN 1992-1-1", "DS EN 1992-1-1", "NS EN 1992-1-1", "CNR-DT 200 R1/2013" };
        private static readonly string[] ExposureList = { null, "X0", "XC1", "XC2", "XC3", "XF1", "XC4", "XD1", "XS1", "XA1", "XA2", "XF2", "XF3", "XD2", "XD3", "XS2", "XS3", "XA3", "XF4" };

        /// <summary>Requirement of every profile, combination, exposure, sensitivity and design limit through Evaluate (doubly reinforced 300×500 in bending), one line each.</summary>
        private static IEnumerable<string> CrackRequirementGrid()
        {
            var stresses = Stresses(Doubly, Bent.Value);
            var geometry = new CrackSectionGeometry(Rectangle, null, Doubly, CrackBarLayout.Rows);
            foreach (var name in CrackStandards)
                foreach (var combination in Combinations)
                    foreach (var exposure in ExposureList)
                        foreach (bool sensitive in new[] { false, true })
                            foreach (var limit in new double?[] { null, .25 })
                            {
                                string key = string.Join(" | ", name, combination, exposure ?? "null", sensitive ? "sensitive" : "little sensitive", "wlim " + P(limit));
                                string value;
                                try
                                {
                                    var r = SectionCrackCheck.Evaluate(new SectionCrackInput(ServiceabilityMigrationTests.Standard(name), combination, exposure, sensitive, limit, geometry, Bent.Value,
                                        stresses, true, false, false, 200000, 33000, 2.9, false, true, 40, null, null, () => 1.2));
                                    value = string.Join(" | ", r.Requirement.Criterion, "requirement limit " + P(r.Requirement.Limit), "required in " + (r.Requirement.RequiredCombination?.ToString() ?? "null"),
                                        r.Verdict, r.Outcome, "wk " + P(r.Width), "ratio " + P(r.Ratio), r.Status);
                                }
                                catch (Exception ex) { value = ex.GetType().FullName + ": " + ex.Message; }
                                yield return "    " + S(key + " => " + value);
                            }
        }

        private sealed class CustomAnnex : StandardEN1992p11 { }

        /// <summary>Profile resolution and the reasons ModelChecker reads, for every concrete standard of Model, an ACI standard and a derived annex.</summary>
        private static IEnumerable<string> CrackProfileTable()
        {
            var standards = new[] { "NTC 2018", "Model Code 2010", "EN 1992-1-1", "UNI EN 1992-1-1", "DIN EN 1992-1-1", "DS EN 1992-1-1", "NS EN 1992-1-1", "CNR-DT 204/2006", "CS-TR34", "CNR-DT 200 R1/2013" }
                .Select(n => Tuple.Create(n, (Standard)ServiceabilityMigrationTests.Standard(n))).Concat(new[] { Tuple.Create("ACI 318-19", (Standard)new StandardACI318p19()), Tuple.Create("EN 1992-1-1 derived annex", (Standard)new CustomAnnex()) });
            foreach (var s in standards)
            {
                bool resolved = CrackProfiles.TryResolve(s.Item2, out var profile);
                string resolve;
                try { resolve = CrackProfiles.Resolve(s.Item2).ToString(); } catch (Exception ex) { resolve = ex.GetType().FullName + ": " + ex.Message; }
                yield return "    " + S(string.Join(" | ", s.Item1, "TryResolve " + resolved + (resolved ? " " + profile : ""), "Resolve " + resolve,
                    "Reference " + (resolved ? CrackProfiles.Reference(profile) : "null"), "NotApplicableReason " + (CrackProfiles.NotApplicableReason(s.Item2) ?? "null"),
                    "NotSupportedReason " + (CrackProfiles.NotSupportedReason(s.Item2) ?? "null"), "StressLimitCheck.NotApplicableReason " + (StressLimitCheck.NotApplicableReason(s.Item2) ?? "null")));
            }
        }

        // ---------------------------------------------------------------- stress limits

        private static string StressPoint(StressLimitPoint p) => p == null ? "null" : JsonArray(new[] { S(p.Id), N(p.X), N(p.Y), N(p.Stress), N(p.Limit), N(p.Ratio) });

        private static string StressState(string id, string input, Standard standard, Func<StressAnalysisResult> analysis, ServiceabilityCombination combination, double factor)
        {
            var entry = new Entry().Text("id", id).Text("input", input).Text("notApplicableReason", StressLimitCheck.NotApplicableReason(standard));
            StressAnalysisResult state;
            try { state = analysis(); }
            catch (Exception ex) { Failure(entry, "GetTensionAnalysisResult", ex); return entry.Render(); }
            entry.Text("strainPlane", state?.StrainPlane == null ? "null (ModelChecker: CheckerStressAnalysisNotConverged, the check is not called)" : "present");
            StressLimitResult r;
            try { r = StressLimitCheck.Evaluate(state, combination, factor); }
            catch (Exception ex) { Failure(entry, "StressLimitCheck.Evaluate", ex); return entry.Render(); }
            entry.Text("combination", r.Combination.ToString()).Flag("linearAnalysis", r.LinearAnalysis).Number("psiRebar", r.PsiRebar).Number("psiTendon", r.PsiTendon)
                .Number("concreteLimitFactor", r.ConcreteLimitFactor).Number("concreteMinStress", r.ConcreteMinStress).Number("concreteLimit", r.ConcreteLimit)
                .Number("concreteRatio", r.ConcreteRatio).Number("steelMaxStress", r.SteelMaxStress).Number("steelRatio", r.SteelRatio).Number("ratio", r.Ratio)
                .Raw("concreteGoverning", StressPoint(r.ConcreteGoverning)).Raw("steelGoverning", StressPoint(r.SteelGoverning))
                .Lines("concretePoints", r.ConcretePoints.Select(p => StressPoint(p))).Lines("steelPoints", r.SteelPoints.Select(p => StressPoint(p)));
            return entry.Render();
        }

        /// <summary>The 2016 states of stress-legacy.csv, evaluated as ServiceabilityMigrationTests.LegacyStressStatesAreReproduced (ModelChecker: Evaluate(analysis, combination, factor)).</summary>
        private static IEnumerable<string> StressFixtureStates()
        {
            var archive = Archive("stress-sections.xml");
            var checkers = new Dictionary<string, SectionCheckerModelCode2010>();
            foreach (var row in Rows("stress-legacy.csv"))
            {
                var c = row.Split(';');
                var axes = Axes(c[9], c[10], c[11]);
                bool linear = bool.Parse(c[4]), tension = bool.Parse(c[6]); double psi = D(c[5]);
                string key = string.Join("|", c[1], c[2], c[3], c[4], c[5], c[6], c[9], c[10], c[11]);
                if (!checkers.TryGetValue(key, out var checker))
                {
                    var standard = ServiceabilityMigrationTests.Standard(c[2]);
                    foreach (var pair in c[3].Split(',')) { var kv = pair.Split('='); typeof(StandardModelCode2010).GetProperty(kv[0]).SetValue(standard, D(kv[1])); }
                    var section = (ReinforcedConcreteSection)archive.BeamProperties[c[1]];
                    var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(axes, SectionSolver.FailureAnalysisTypes.ConstantN, SectionSolver.FailureDomainTypes.Plastic,
                        linear ? SectionSolver.StressAnalysisTypes.Linear : SectionSolver.StressAnalysisTypes.NonLinear, psi, 0, tension, int.Parse(c[7]));
                    checker = new SectionCheckerModelCode2010(new SectionCheckerAttribute(section, null, null), options, standard, tension);
                    checkers[key] = checker;
                }
                var force = new ResultBeamForces(D(c[12]), D(c[13]), D(c[14]), D(c[15]), D(c[16]), D(c[17]), axes);
                string input = string.Join(" | ", c[1], c[2], "linear " + c[4], "psi " + c[5], "tension " + c[6], "factor " + c[8], c[18]);
                yield return StressState("stress-legacy " + c[0] + " " + c[18], input, ServiceabilityMigrationTests.Standard(c[2]), () => checker.GetTensionAnalysisResult(force), Combination(c[18]), D(c[8]));
            }
        }

        private static readonly string[] StressStandards = { "NTC 2018", "Model Code 2010", "EN 1992-1-1", "UNI EN 1992-1-1", "DIN EN 1992-1-1", "DS EN 1992-1-1", "NS EN 1992-1-1",
            "CNR-DT 204/2006", "CS-TR34", "CNR-DT 200 R1/2013" };

        /// <summary>Section R300x500 of stress-sections.xml (C30/37, 3Ø20 + 2Ø16 B450C), N = −300 kN, M = 120 kNm (ServiceabilityMigrationTests.LimitsFollowTheCoefficientsOfEveryStandard).</summary>
        private static IEnumerable<string> StressBoundaryStates()
        {
            var archive = Archive("stress-sections.xml");
            var section = (ReinforcedConcreteSection)archive.BeamProperties["R300x500"];
            var axes = new CoordinateSystem(section.Centroid, new Vector3d(1, 0, 0), new Vector3d(0, 1, 0));
            SectionCheckerModelCode2010 Checker(StandardModelCode2010 standard, bool linear) => new SectionCheckerModelCode2010(new SectionCheckerAttribute(section, null, null),
                new SectionCheckerModelCode2010.SectionOptionsModelCode2010(axes, SectionSolver.FailureAnalysisTypes.ConstantN, SectionSolver.FailureDomainTypes.Plastic,
                    linear ? SectionSolver.StressAnalysisTypes.Linear : SectionSolver.StressAnalysisTypes.NonLinear, 2, 0, false, 32), standard, false);
            var forces = new ResultBeamForces(-300000, 0, 0, 0, 120e6, 0, axes);
            foreach (var name in StressStandards)
            {
                var standard = ServiceabilityMigrationTests.Standard(name);
                var checker = Checker(standard, true);
                foreach (var combination in Combinations)
                    yield return StressState("standard", string.Join(" | ", "R300x500", name, "linear φ = 2", "N −300 kN, M 120 kNm", combination), standard, () => checker.GetTensionAnalysisResult(forces), combination, 1);
            }
            var ntc = ServiceabilityMigrationTests.Standard("NTC 2018");
            var nonLinear = Checker(ntc, false);
            foreach (var combination in Combinations)
                yield return StressState("nonLinear", string.Join(" | ", "R300x500", "NTC 2018", "non linear", "N −300 kN, M 120 kNm", combination), ntc, () => nonLinear.GetTensionAnalysisResult(forces), combination, 1);
            var linearChecker = Checker(ntc, true);
            foreach (double factor in new[] { 0, -.5, double.NaN, double.PositiveInfinity, 1.0000000001, .8, 1 })
                yield return StressState("concreteLimitFactor", string.Join(" | ", "R300x500", "NTC 2018", "linear φ = 2", "factor " + P(factor), "Characteristic"), ntc,
                    () => linearChecker.GetTensionAnalysisResult(forces), ServiceabilityCombination.Characteristic, factor);
            foreach (int combination in new[] { -1, 3 })
                yield return StressState("combination", string.Join(" | ", "R300x500", "NTC 2018", "linear φ = 2", "combination " + combination), ntc,
                    () => linearChecker.GetTensionAnalysisResult(forces), (ServiceabilityCombination)combination, 1);
            yield return StressState("nullResult", "result null", ntc, () => null, ServiceabilityCombination.Characteristic, 1);
            var crushing = new ResultBeamForces(-1e9, 0, 0, 0, 0, 0, axes);
            yield return StressState("notConverged", string.Join(" | ", "R300x500", "NTC 2018", "non linear", "N −1e6 kN"), ntc, () => nonLinear.GetTensionAnalysisResult(crushing),
                ServiceabilityCombination.Characteristic, 1);
        }
    }
}
