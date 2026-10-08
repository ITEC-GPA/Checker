using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Cracking;
using GPC.Checkers.Concrete.Detailing;
using GPC.Checkers.Concrete.Response;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Persistence;
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
    /// Contract of GPCChecker.Concrete 0.0.17.0 for Detailing/ and Response/ (ANTHEA F2.8, part L0): regression snapshot captured from the code of
    /// Checker develop 4f54139a before any change of F2.8. It is not an independent expectation: never regenerate it to make the test pass.
    /// - detailing-contract-0.0.17.csv: <see cref="MemberDetailingCalculator.Calculate"/> for every <see cref="DetailingProfile"/> × Beam and Column (and
    ///   an undefined kind value, handled as a beam) on the six sections of detailing-sections.xml with varied links, covers, laps, confirmations,
    ///   compression and the arguments of ModelChecker (coverAddition, groundCover) and of the piles (22 arguments); boundary inputs (LinkLegs −1,
    ///   NominalCover NaN, Fctm 0, Compression NaN, cmin,dur +∞/NaN/−5, section without bars). For every case Key, Actual, Limit, Unit, Passed,
    ///   Reference, Explanation and NotImplemented of every check in order, or the exact type and message of the exception.
    ///   <see cref="AnchorageCalculator.Calculate"/> for every profile on the rows of anchorage-legacy.csv, plain bars and αct ≠ 1 (every member of
    ///   the result, or the exception); <see cref="AnchorageCalculator.BondStrength"/> on the bond rows and on its rejections.
    /// - curvature-contract-0.0.17.csv: <see cref="MomentCurvatureAnalysis"/> on the 5 curves of curvature-legacy.csv (Status, InterruptedAtStep, the
    ///   scalar results and every point) and, on analytic functions, complete, partial, interrupted and refined curves and the four rejections
    ///   (invalid request, limit point not available, residual N, non-positive limit moment), with the invariant and the Italian culture
    ///   (numbers in the texts are formatted with the current culture).
    /// Doubles are written with the round-trip format (G17 where "R" does not round-trip, −0 kept), so the comparison is bit for bit. Generation runs with the
    /// invariant culture and UI culture (mscorlib messages in English on every machine). On a difference the generated text is written to
    /// %TEMP%\&lt;fixture&gt;.actual and the first differing line is reported.
    /// </summary>
    [TestClass]
    public class DetailingContractTests
    {
        private const string DetailingFile = "detailing-contract-0.0.17.csv", CurvatureFile = "curvature-contract-0.0.17.csv";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        [TestMethod]
        public void DetailingContractOf0017IsUnchanged()
        {
            var counts = new Dictionary<string, int>();
            string actual = WithCulture(Inv, () => GenerateDetailing(counts));
            Assert.AreEqual(6 * 5 * 3 * 6, counts["memberCases"], "member cases");
            Assert.AreEqual(450 * 5, counts["anchorageRows"], "anchorage rows × profiles");
            Assert.AreEqual(18, counts["bondRows"], "bond rows");
            Compare(DetailingFile, actual);
        }

        [TestMethod]
        public void CurvatureContractOf0017IsUnchanged()
        {
            var counts = new Dictionary<string, int>();
            string actual = WithCulture(Inv, () => GenerateCurvature(counts));
            Assert.AreEqual(5, counts["fixtureCurves"], "fixture curves");
            Assert.IsTrue(counts["rejections"] >= 2 * 4, "rejections");
            Compare(CurvatureFile, actual);
        }

        // ---------------------------------------------------------------- comparison

        private static void Compare(string fileName, string actual)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", fileName);
            string actualPath = Path.Combine(Path.GetTempPath(), fileName + ".actual");
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
            Assert.Fail("Contract 0.0.17.0 (" + fileName + ") changed at line " + (line + 1) + "\nexpected: " + (line < e.Length ? e[line] : "<end>")
                + "\nactual:   " + (line < a.Length ? a[line] : "<end>") + "\nGenerated text: " + actualPath);
        }

        private static T WithCulture<T>(CultureInfo culture, Func<T> action)
        {
            var thread = Thread.CurrentThread; var culture0 = thread.CurrentCulture; var ui0 = thread.CurrentUICulture;
            try { thread.CurrentCulture = culture; thread.CurrentUICulture = Inv; return action(); }
            finally { thread.CurrentCulture = culture0; thread.CurrentUICulture = ui0; }
        }

        /// <summary>Round-trip text of a double: "R", G17 where "R" does not give the same bits back, "-0" for negative zero.</summary>
        internal static string F(double v)
        {
            if (v == 0) return BitConverter.DoubleToInt64Bits(v) < 0 ? "-0" : "0";
            string r = v.ToString("R", Inv);
            if (!double.IsNaN(v) && BitConverter.DoubleToInt64Bits(double.Parse(r, Inv)) != BitConverter.DoubleToInt64Bits(v)) r = v.ToString("G17", Inv);
            return r;
        }
        internal static string F(double? v) => v.HasValue ? F(v.Value) : "null";
        internal static string B(bool? v) => v.HasValue ? (v.Value ? "true" : "false") : "null";
        internal static string T(string s) => s == null ? "null" : s.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", "\\r").Replace("\n", "\\n");
        private static string Error(Exception ex)
        {
            var arg = ex as ArgumentException;
            return "error\t" + ex.GetType().FullName + "\t" + T(ex.Message) + (arg != null ? "\tparam=" + T(arg.ParamName) : "");
        }
        private static double D(string s) => double.Parse(s, Inv);
        private static string Folder => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures");

        // ---------------------------------------------------------------- detailing and anchorage

        private sealed class SectionData
        {
            public string Name; public double Area, Fck, Top, Bottom, Web, Cover;
        }
        private static readonly SectionData[] Sections =
        {
            new SectionData { Name = "R300x500", Area = 150000, Fck = 30, Top = 300, Bottom = 300, Web = 300, Cover = 30 },
            new SectionData { Name = "T1200x800", Area = 520000, Fck = 35, Top = 1200, Bottom = 400, Web = 400, Cover = 70 },
            new SectionData { Name = "R400x400", Area = 160000, Fck = 25, Top = 400, Bottom = 400, Web = 400, Cover = 35 },
            new SectionData { Name = "C400", Area = 125663.70614359173, Fck = 32, Top = 400, Bottom = 400, Web = 400, Cover = 40 },
            new SectionData { Name = "R250x250", Area = 62500, Fck = 20, Top = 250, Bottom = 250, Web = 250, Cover = 25 },
            new SectionData { Name = "R600x800H", Area = 360000, Fck = 28, Top = 600, Bottom = 600, Web = 600, Cover = 70 }
        };
        private const double Fyk = 450, Fyd = 391.304347826087;
        private static double Fctm(double fck) => fck <= 50 ? .3 * Math.Pow(fck, 2d / 3) : 2.12 * Math.Log(1 + (fck + 8) / 10);
        private static readonly DetailingProfile[] Profiles = (DetailingProfile[])Enum.GetValues(typeof(DetailingProfile));

        private static string GenerateDetailing(Dictionary<string, int> counts)
        {
            var sb = new StringBuilder();
            sb.Append("# GPCChecker.Concrete 0.0.17.0 contract of Detailing/ (ANTHEA F2.8 L0), captured from Checker 4f54139a. Regression snapshot, not an independent expectation.\n");
            sb.Append("# M: member case (inputs; outcome; result Profile, Kind, Passed, Reference) followed by its checks C (index, Key, Actual, Limit, Unit, Passed, Reference, Explanation, NotImplemented).\n");
            sb.Append("# A: anchorage (inputs; outcome; every member of the result). B: bond strength. Doubles round-trip (R / G17).\n");
            ReinforcedConcreteSection[] sections;
            using (var stream = File.OpenRead(Path.Combine(Folder, "detailing-sections.xml")))
            {
                var archive = ModelArchive.Load(stream);
                sections = Sections.Select(s => (ReinforcedConcreteSection)archive.BeamProperties[s.Name]).ToArray();
            }
            int id = 0, memberCases = 0;
            var kinds = new[] { MemberDetailingKind.Beam, MemberDetailingKind.Column, (MemberDetailingKind)7 };
            for (int k = 0; k < Sections.Length; k++)
            {
                var data = Sections[k]; var geometry = CrackSectionGeometry.From(sections[k]);
                foreach (var profile in Profiles)
                    foreach (var kind in kinds)
                        for (int v = 0; v < 6; v++)
                        {
                            int w = v + 6 * (int)profile + k;
                            bool links = w % 5 != 3; double linkDiameter = new[] { 6.0, 8, 10 }[w % 3], linkSpacing = new[] { 100.0, 200, 300 }[(w / 3) % 3];
                            int legs = new[] { 2, 4, 0, 1 }[w % 4];
                            double aggregate = new[] { 16.0, 20, 40 }[(w / 2) % 3];
                            double? cdur = new double?[] { null, 25, 45 }[(w + 1) % 3];
                            double deviation = w % 2 == 0 ? 10 : 5, cover = w % 6 == 5 ? data.Cover - 10 : data.Cover;
                            bool lap = w % 5 == 1, restrained = w % 4 != 2, ends = w % 3 != 1;
                            double compression = kind == MemberDetailingKind.Column ? new[] { 0.0, 1.5e6, 4e6 }[w % 3] : w % 2 == 0 ? 0 : 2e5;
                            double addition = new[] { 0.0, 5, 15, 0, 20, 0, 0 }[w % 7], ground = new[] { 0.0, 40, 0, 75, 0, 0, 40 }[w % 7];
                            bool piles = w % 7 == 0 || w % 7 == 5;
                            Func<MemberDetailingInput> input;
                            if (piles)
                                input = () => new MemberDetailingInput(kind, geometry, data.Area, data.Fck, Fctm(data.Fck), Fyk, Fyd, data.Top, data.Bottom, data.Web, compression, links,
                                    linkDiameter, linkSpacing, legs, aggregate, cover, cdur, deviation, lap, restrained, ends);
                            else
                                input = () => new MemberDetailingInput(kind, geometry, data.Area, data.Fck, Fctm(data.Fck), Fyk, Fyd, data.Top, data.Bottom, data.Web, compression, links,
                                    linkDiameter, linkSpacing, legs, aggregate, cover, cdur, deviation, lap, restrained, ends, addition, ground);
                            string head = string.Join("\t", "M", id++, data.Name, profile, (int)kind, F(data.Area), F(data.Fck), F(Fctm(data.Fck)), F(Fyk), F(Fyd), F(data.Top),
                                F(data.Bottom), F(data.Web), F(compression), B(links), F(linkDiameter), F(linkSpacing), legs, F(aggregate), F(cover), F(cdur), F(deviation), B(lap),
                                B(restrained), B(ends), piles ? "22" : F(addition) + "/" + F(ground));
                            Member(sb, head, profile, input);
                            memberCases++;
                        }
            }
            counts["memberCases"] = memberCases;

            // Boundary inputs on a rectangular and a circular section, every profile, beam and column.
            foreach (int k in new[] { 0, 3 })
            {
                var data = Sections[k]; var geometry = CrackSectionGeometry.From(sections[k]);
                foreach (var profile in Profiles)
                    foreach (var kind in new[] { MemberDetailingKind.Beam, MemberDetailingKind.Column })
                    {
                        var cases = new List<KeyValuePair<string, Func<MemberDetailingInput>>>();
                        Func<int, double, double, double, double?, double, double, double, Func<MemberDetailingInput>> make = (legs, cover, fctm, compression, cdur, spacing, addition, ground)
                            => () => new MemberDetailingInput(kind, geometry, data.Area, data.Fck, fctm, Fyk, Fyd, data.Top, data.Bottom, data.Web, compression, true, 8, spacing, legs, 20,
                                cover, cdur, 10, false, true, true, addition, ground);
                        double fctm0 = Fctm(data.Fck);
                        cases.Add(new KeyValuePair<string, Func<MemberDetailingInput>>("LinkLegs -1", make(-1, data.Cover, fctm0, 1e6, 25, 200, 0, 0)));
                        cases.Add(new KeyValuePair<string, Func<MemberDetailingInput>>("LinkLegs -2", make(-2, data.Cover, fctm0, 1e6, 25, 200, 0, 0)));
                        cases.Add(new KeyValuePair<string, Func<MemberDetailingInput>>("NominalCover NaN", make(2, double.NaN, fctm0, 1e6, 25, 200, 0, 0)));
                        cases.Add(new KeyValuePair<string, Func<MemberDetailingInput>>("NominalCover -1", make(2, -1, fctm0, 1e6, 25, 200, 0, 0)));
                        cases.Add(new KeyValuePair<string, Func<MemberDetailingInput>>("NominalCover +inf", make(2, double.PositiveInfinity, fctm0, 1e6, 25, 200, 0, 0)));
                        cases.Add(new KeyValuePair<string, Func<MemberDetailingInput>>("Fctm 0", make(2, data.Cover, 0, 1e6, 25, 200, 0, 0)));
                        cases.Add(new KeyValuePair<string, Func<MemberDetailingInput>>("Compression NaN", make(2, data.Cover, fctm0, double.NaN, 25, 200, 0, 0)));
                        cases.Add(new KeyValuePair<string, Func<MemberDetailingInput>>("Compression -1", make(2, data.Cover, fctm0, -1, 25, 200, 0, 0)));
                        cases.Add(new KeyValuePair<string, Func<MemberDetailingInput>>("cmin,dur +inf", make(2, data.Cover, fctm0, 1e6, double.PositiveInfinity, 200, 0, 0)));
                        cases.Add(new KeyValuePair<string, Func<MemberDetailingInput>>("cmin,dur NaN", make(2, data.Cover, fctm0, 1e6, double.NaN, 200, 0, 0)));
                        cases.Add(new KeyValuePair<string, Func<MemberDetailingInput>>("cmin,dur -5", make(2, data.Cover, fctm0, 1e6, -5, 200, 0, 0)));
                        cases.Add(new KeyValuePair<string, Func<MemberDetailingInput>>("LinkSpacing 0", make(2, data.Cover, fctm0, 1e6, 25, 0, 0, 0)));
                        cases.Add(new KeyValuePair<string, Func<MemberDetailingInput>>("CoverAddition +inf", make(2, data.Cover, fctm0, 1e6, 25, 200, double.PositiveInfinity, 0)));
                        cases.Add(new KeyValuePair<string, Func<MemberDetailingInput>>("GroundCover -1", make(2, data.Cover, fctm0, 1e6, 25, 200, 0, -1)));
                        cases.Add(new KeyValuePair<string, Func<MemberDetailingInput>>("Width 0", () => new MemberDetailingInput(kind, geometry, data.Area, data.Fck, fctm0, Fyk, Fyd, 0,
                            data.Bottom, data.Web, 0, true, 8, 200, 2, 20, data.Cover, 25, 10, false, true, true)));
                        cases.Add(new KeyValuePair<string, Func<MemberDetailingInput>>("no bars", () => new MemberDetailingInput(kind,
                            new CrackSectionGeometry(geometry.Outline, geometry.Holes.Select(h => (IEnumerable<Point2d>)h), new CrackBar[0], geometry.Layout),
                            data.Area, data.Fck, fctm0, Fyk, Fyd, data.Top, data.Bottom, data.Web, 0, true, 8, 200, 2, 20, data.Cover, 25, 10, false, true, true)));
                        cases.Add(new KeyValuePair<string, Func<MemberDetailingInput>>("null geometry", () => new MemberDetailingInput(kind, null, data.Area, data.Fck, fctm0, Fyk, Fyd,
                            data.Top, data.Bottom, data.Web, 0, true, 8, 200, 2, 20, data.Cover, 25, 10, false, true, true)));
                        foreach (var c in cases) Member(sb, string.Join("\t", "M", id++, data.Name, profile, (int)kind, "boundary", T(c.Key)), profile, c.Value);
                    }
            }
            try { MemberDetailingCalculator.Calculate(DetailingProfile.Ntc2018, null); sb.Append("M\tnull input\tok\n"); }
            catch (Exception ex) { sb.Append("M\tnull input\t" + Error(ex) + "\n"); }

            // Anchorage: rows of anchorage-legacy.csv for every profile, plain bars and αct ≠ 1.
            int anchorageRows = 0, bondRows = 0;
            foreach (var row in File.ReadAllLines(Path.Combine(Folder, "anchorage-legacy.csv"), Encoding.UTF8).Where(l => !l.StartsWith("#")))
            {
                var c = row.Split(';');
                if (c[10] == "bond")
                {
                    Bond(sb, "B\t" + c[0], D(c[3]), D(c[1]), bool.Parse(c[5]) ? 1 : .7, 1, D(c[4])); bondRows++;
                    continue;
                }
                foreach (var profile in Profiles)
                {
                    var input = new AnchorageInput(D(c[1]), D(c[2]), D(c[3]), D(c[4]), bool.Parse(c[5]), D(c[6]), bool.Parse(c[7]), D(c[8]), D(c[9]));
                    Anchorage(sb, string.Join("\t", "A", c[0], profile), profile, input); anchorageRows++;
                }
            }
            counts["anchorageRows"] = anchorageRows; counts["bondRows"] = bondRows;
            int extra = 0;
            foreach (var profile in Profiles)
            {
                foreach (bool lap in new[] { false, true })
                {
                    Anchorage(sb, string.Join("\t", "A", "plain" + extra++, profile), profile, new AnchorageInput(20, 391.3, 2.0, 1.5, true, 900, lap, 50, 30, false));
                    foreach (double alpha in new[] { 1.0, .85, .7, 1.2, 0, -.5, double.NaN, double.PositiveInfinity })
                        Anchorage(sb, string.Join("\t", "A", "alpha" + extra++, profile, F(alpha)), profile, new AnchorageInput(26, 391.3, 2.2, 1.5, false, 1200, lap, 100, 20, true, alpha));
                    foreach (double percent in new[] { 0.0, 10, 33.3, 100, 100.1, double.NaN })
                        Anchorage(sb, string.Join("\t", "A", "percent" + extra++, profile, F(percent)), profile, new AnchorageInput(16, 300, 1.9, 1.5, true, 700, lap, percent, 60));
                    foreach (double clear in new[] { -1.0, 0, 49.9, 50, 64, 64.1, 200, double.PositiveInfinity })
                        Anchorage(sb, string.Join("\t", "A", "clear" + extra++, profile, F(clear)), profile, new AnchorageInput(16, 300, 1.9, 1.5, true, 700, lap, 50, clear));
                    foreach (double d in new[] { 0.0, 32, 32.5, 131.9, 132, 140, double.NaN })
                        Anchorage(sb, string.Join("\t", "A", "diameter" + extra++, profile, F(d)), profile, new AnchorageInput(d, 300, 1.9, 1.5, true, 700, lap, 50, 30));
                    foreach (double gamma in new[] { 1.0, 1.5, 0, -1, double.NaN, double.PositiveInfinity })
                        Anchorage(sb, string.Join("\t", "A", "gamma" + extra++, profile, F(gamma)), profile, new AnchorageInput(20, 300, 1.9, gamma, true, 700, lap, 50, 30));
                    foreach (double stress in new[] { 0.0, -1, double.NaN, double.PositiveInfinity })
                        Anchorage(sb, string.Join("\t", "A", "stress" + extra++, profile, F(stress)), profile, new AnchorageInput(20, stress, 1.9, 1.5, true, 700, lap, 50, 30));
                }
            }
            try { AnchorageCalculator.Calculate(DetailingProfile.Ntc2018, null); sb.Append("A\tnull input\tok\n"); }
            catch (Exception ex) { sb.Append("A\tnull input\t" + Error(ex) + "\n"); }
            int b = 0;
            foreach (double fct in new[] { 0.0, -1, 1.8, double.NaN, double.PositiveInfinity })
                foreach (double d in new[] { 0.0, 12, 32, 40, 131.9, 132 })
                    foreach (double alpha in new[] { 1.0, .85, 0 })
                        foreach (double gamma in new[] { 1.5, 1, double.NaN, double.PositiveInfinity })
                            Bond(sb, "B\tgrid" + b++, fct, d, .7, alpha, gamma);
            return sb.ToString();
        }

        private static void Member(StringBuilder sb, string head, DetailingProfile profile, Func<MemberDetailingInput> input)
        {
            MemberDetailingResult r;
            try { r = MemberDetailingCalculator.Calculate(profile, input()); }
            catch (Exception ex) { sb.Append(head + "\t" + Error(ex) + "\n"); return; }
            sb.Append(string.Join("\t", head, "ok", r.Profile, (int)r.Kind, B(r.Passed), T(r.Reference), r.Checks.Count) + "\n");
            for (int i = 0; i < r.Checks.Count; i++)
            {
                var c = r.Checks[i];
                sb.Append(string.Join("\t", "C", i, T(c.Key), F(c.Actual), F(c.Limit), T(c.Unit), B(c.Passed), T(c.Reference), T(c.Explanation), B(c.NotImplemented)) + "\n");
            }
        }

        private static void Anchorage(StringBuilder sb, string head, DetailingProfile profile, AnchorageInput p)
        {
            string inputs = string.Join("\t", F(p.Diameter), F(p.Stress), F(p.Fctk05), F(p.GammaC), B(p.GoodBond), F(p.AvailableLength), B(p.Lap), F(p.LapPercent),
                F(p.LapClearDistance), B(p.RibbedBars), F(p.AlphaCt));
            AnchorageResult r;
            try { r = AnchorageCalculator.Calculate(profile, p); }
            catch (Exception ex) { sb.Append(head + "\t" + inputs + "\t" + Error(ex) + "\n"); return; }
            sb.Append(string.Join("\t", head, inputs, "ok", r.Profile, F(r.Fbd), F(r.Eta1), F(r.Eta2), F(r.BasicLength), F(r.Alpha6), F(r.MinimumLength), F(r.RequiredLength),
                F(r.AvailableLength), B(r.LengthPassed), F(r.MaximumLapClearDistance), B(r.LapClearDistancePassed), B(r.Passed), T(r.Expression), T(r.Reference)) + "\n");
        }

        private static void Bond(StringBuilder sb, string head, double fct, double diameter, double eta1, double alpha, double gamma)
        {
            string inputs = string.Join("\t", F(fct), F(diameter), F(eta1), F(alpha), F(gamma));
            try { sb.Append(head + "\t" + inputs + "\tok\t" + F(AnchorageCalculator.BondStrength(fct, diameter, eta1, alpha, gamma)) + "\n"); }
            catch (Exception ex) { sb.Append(head + "\t" + inputs + "\t" + Error(ex) + "\n"); }
        }

        // ---------------------------------------------------------------- moment-curvature

        private static string GenerateCurvature(Dictionary<string, int> counts)
        {
            var sb = new StringBuilder();
            sb.Append("# GPCChecker.Concrete 0.0.17.0 contract of Response/ (ANTHEA F2.8 L0), captured from Checker 4f54139a. Regression snapshot, not an independent expectation.\n");
            sb.Append("# R: result (id; culture; outcome; LimitMoment, YieldCurvature, UltimateCurvature, LimitAxialForce, AxialResidual, InterruptedAtStep, points; Status) followed by\n");
            sb.Append("# its points P (Moment, Mx, My, Curvature, GradientX, GradientY, ReferenceStrain, ConcreteCompressionStrain, SteelStrain, Yielded, Limit). Doubles round-trip.\n");
            // A. The 5 curves of curvature-legacy.csv on the native checker (as LegacyMomentCurvatureIsReproduced).
            ReinforcedConcreteSection Section(GPC.Model.Models.Model archive, string name) => (ReinforcedConcreteSection)archive.BeamProperties[name];
            GPC.Model.Models.Model model;
            using (var stream = File.OpenRead(Path.Combine(Folder, "detailing-sections.xml"))) model = ModelArchive.Load(stream);
            int fixtureCurves = 0;
            foreach (var row in File.ReadAllLines(Path.Combine(Folder, "curvature-legacy.csv"), Encoding.UTF8).Where(l => !l.StartsWith("#")).Skip(1))
            {
                var c = row.Split(';');
                var standard = ServiceabilityMigrationTests.Standard(c[2]);
                foreach (var pair in c[3].Split(',')) { var kv = pair.Split('='); typeof(StandardModelCode2010).GetProperty(kv[0]).SetValue(standard, D(kv[1])); }
                Func<string, double[]> P = s => s.Split(',').Select(D).ToArray();
                var o = P(c[4]); var v1 = P(c[5]); var v2 = P(c[6]);
                var axes = new CoordinateSystem(new Point3d(o[0], o[1], o[2]), new Vector3d(v1[0], v1[1], v1[2]), new Vector3d(v2[0], v2[1], v2[2]));
                var section = Section(model, c[1]);
                var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(axes, SectionSolver.FailureAnalysisTypes.ConstantN, SectionSolver.FailureDomainTypes.Plastic,
                    SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, false, 32);
                var checker = new SectionCheckerModelCode2010(new SectionCheckerAttribute(section, null, null), options, standard, false);
                var request = new MomentCurvatureRequest(D(c[8]) * 1000, D(c[9]), int.Parse(c[10]), D(c[11]), bool.Parse(c[12]), D(c[13]) * 1000, int.Parse(c[14]));
                Curve(sb, "fixture " + c[0] + " " + c[1] + " " + c[2], "invariant", () => MomentCurvatureAnalysis.Calculate(request, checker, section, axes, D(c[7])));
                fixtureCurves++;
            }
            counts["fixtureCurves"] = fixtureCurves;

            // B. Analytic functions: limit point at Mlim = 3e8 Nmm with the given residual, softening response, bar strain at 230 mm, concrete at 250 mm.
            int rejections = 0;
            foreach (var culture in new[] { Inv, CultureInfo.GetCultureInfo("it-IT") })
            {
                string name = culture.Name.Length == 0 ? "invariant" : culture.Name;
                WithCulture(culture, () =>
                {
                    foreach (var a in AnalyticCases())
                    {
                        if (a.Rejection) rejections++;
                        Curve(sb, a.Name, name, () => MomentCurvatureAnalysis.Calculate(a.Request, a.Limit, a.Response, a.YieldStrain));
                    }
                    return 0;
                });
            }
            counts["rejections"] = rejections;
            return sb.ToString();
        }

        private static void Curve(StringBuilder sb, string id, string culture, Func<MomentCurvatureResult> calculate)
        {
            MomentCurvatureResult r;
            try { r = calculate(); }
            catch (Exception ex) { sb.Append(string.Join("\t", "R", T(id), culture, Error(ex)) + "\n"); return; }
            sb.Append(string.Join("\t", "R", T(id), culture, "ok", F(r.LimitMoment), F(r.YieldCurvature), F(r.UltimateCurvature), F(r.LimitAxialForce), F(r.AxialResidual),
                r.InterruptedAtStep.HasValue ? r.InterruptedAtStep.Value.ToString(Inv) : "null", r.Points.Count, T(r.Status)) + "\n");
            foreach (var p in r.Points)
                sb.Append(string.Join("\t", "P", F(p.Moment), F(p.Mx), F(p.My), F(p.Curvature), F(p.GradientX), F(p.GradientY), F(p.ReferenceStrain), F(p.ConcreteCompressionStrain),
                    F(p.SteelStrain), B(p.Yielded), B(p.Limit)) + "\n");
        }

        internal sealed class AnalyticCase
        {
            public string Name; public MomentCurvatureRequest Request; public double YieldStrain = .002; public bool Rejection;
            public Func<double, double, double, MomentCurvatureLimit> Limit; public Func<double, double, double, MomentCurvatureStrains> Response;
        }

        internal const double EI = 2e13, EA = 5e6, Mlim = 3e8;
        internal static MomentCurvatureStrains Strains(double n, double mx, double my)
        {
            double m = Math.Sqrt(mx * mx + my * my), soften = 1 + Math.Pow(m / Mlim, 4);
            double chiX = mx / EI * soften, chiY = my / EI * soften, chi = m / EI * soften;
            return new MomentCurvatureStrains(chiX, chiY, n / EA, 230 * chi, -250 * chi);
        }
        internal static Func<double, double, double, MomentCurvatureLimit> AnalyticLimit(double residual, double moment)
            => (n, c, s) => new MomentCurvatureLimit(n + residual, moment * c, moment * s, Strains(n + residual, 1.05 * moment * c, 1.05 * moment * s));

        /// <summary>Analytic curves of the contract; the same list is used by the L3 tests of the scale invariance.</summary>
        internal static IEnumerable<AnalyticCase> AnalyticCases()
        {
            Func<double, double, double, MomentCurvatureStrains> response = Strains;
            foreach (double theta in new[] { 0.0, 30, 135, 217 })
            {
                yield return new AnalyticCase { Name = "complete " + theta, Request = new MomentCurvatureRequest(-2.5e5, theta, 20), Limit = AnalyticLimit(.4, Mlim), Response = response };
                yield return new AnalyticCase { Name = "linear 0.8 " + theta, Request = new MomentCurvatureRequest(1e5, theta, 10, .8, false, 10, 0), Limit = AnalyticLimit(-3, Mlim), Response = response };
            }
            yield return new AnalyticCase { Name = "bisections 30", Request = new MomentCurvatureRequest(0, 60, 37, 1, true, 1, 30), Limit = AnalyticLimit(0, Mlim), Response = response };
            yield return new AnalyticCase { Name = "yield at the first point", Request = new MomentCurvatureRequest(0, 0, 10), Limit = AnalyticLimit(0, Mlim),
                Response = (n, mx, my) => { var s = Strains(n, mx, my); return new MomentCurvatureStrains(s.ChiX, s.ChiY, s.ReferenceStrain, s.MaximumBarStrain + .01, s.MinimumConcreteStrain); } };
            yield return new AnalyticCase { Name = "small yield strain", Request = new MomentCurvatureRequest(0, 0, 10), Limit = AnalyticLimit(0, Mlim), Response = response, YieldStrain = 1e-12 };
            yield return new AnalyticCase { Name = "never yielded", Request = new MomentCurvatureRequest(0, 0, 10), Limit = AnalyticLimit(0, Mlim), Response = response, YieldStrain = 1 };
            int calls = 0;
            yield return new AnalyticCase { Name = "interrupted at the 7th response", Request = new MomentCurvatureRequest(-1e3, 30, 12), Limit = AnalyticLimit(0, Mlim),
                Response = (n, mx, my) => { if (++calls == 7) throw new InvalidOperationException("analytic response failed (7)"); return Strains(n, mx, my); } };
            yield return new AnalyticCase { Name = "response not finite", Request = new MomentCurvatureRequest(0, 0, 10), Limit = AnalyticLimit(0, Mlim),
                Response = (n, mx, my) => Math.Sqrt(mx * mx + my * my) > .02 * Mlim ? new MomentCurvatureStrains(double.NaN, 0, 0, 0, 0) : Strains(n, mx, my) };
            int refine = 0;
            yield return new AnalyticCase { Name = "refinement interrupted", Request = new MomentCurvatureRequest(0, 45, 10, 1, false), Limit = AnalyticLimit(0, Mlim),
                Response = (n, mx, my) => { if (++refine > 10) throw new ArgumentException("analytic bisection failed"); return Strains(n, mx, my); } };
            yield return new AnalyticCase { Name = "limit strains not finite", Request = new MomentCurvatureRequest(0, 0, 10), Response = response,
                Limit = (n, c, s) => new MomentCurvatureLimit(n, Mlim * c, Mlim * s, new MomentCurvatureStrains(double.PositiveInfinity, 0, 0, 0, 0)) };
            // Rejections.
            foreach (var bad in new[] { new MomentCurvatureRequest(0, 0, 9), new MomentCurvatureRequest(0, 0, 501), new MomentCurvatureRequest(0, 0, 10, 0),
                new MomentCurvatureRequest(0, 0, 10, 1.01), new MomentCurvatureRequest(double.NaN, 0, 10), new MomentCurvatureRequest(0, double.PositiveInfinity, 10),
                new MomentCurvatureRequest(0, 0, 10, 1, true, 0), new MomentCurvatureRequest(0, 0, 10, 1, true, 1000, -1), new MomentCurvatureRequest(0, 0, 10, 1, true, 1000, 31) })
                yield return new AnalyticCase { Name = "invalid request", Request = bad, Limit = AnalyticLimit(0, Mlim), Response = response, Rejection = true };
            yield return new AnalyticCase { Name = "invalid yield strain", Request = new MomentCurvatureRequest(0, 0, 10), Limit = AnalyticLimit(0, Mlim), Response = response, YieldStrain = 0, Rejection = true };
            yield return new AnalyticCase { Name = "limit point not available", Request = new MomentCurvatureRequest(-1e5, 0, 10), Limit = (n, c, s) => null, Response = response, Rejection = true };
            yield return new AnalyticCase { Name = "residual", Request = new MomentCurvatureRequest(-123456.789, 30, 10, 1, true, 1000.5), Limit = AnalyticLimit(1234.5678, Mlim),
                Response = response, Rejection = true };
            yield return new AnalyticCase { Name = "residual negative", Request = new MomentCurvatureRequest(2.5e4, 30, 10, 1, true, 10), Limit = AnalyticLimit(-10.25, Mlim),
                Response = response, Rejection = true };
            yield return new AnalyticCase { Name = "non-positive limit moment", Request = new MomentCurvatureRequest(0, 0, 10), Limit = AnalyticLimit(0, -Mlim), Response = response, Rejection = true };
            // cos 90° is 6.1e-17, not 0: the limit moment is tiny but positive and the curve is calculated (not a rejection).
            yield return new AnalyticCase { Name = "zero limit moment", Request = new MomentCurvatureRequest(0, 90, 10), Limit = (n, c, s) => new MomentCurvatureLimit(n, Mlim, 0, Strains(n, Mlim, 0)),
                Response = response };
            yield return new AnalyticCase { Name = "NaN limit moment", Request = new MomentCurvatureRequest(0, 0, 10), Limit = AnalyticLimit(0, double.NaN), Response = response, Rejection = true };
        }
    }
}
