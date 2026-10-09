using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Cracking;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Checkers.Concrete.Serviceability;
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

namespace ConcreteTests
{
    /// <summary>
    /// Crack control moved from ANTHEA (Ntc2018Checks.Cracking, ConcreteCodeChecks, ConcreteTensionCracking, ConcreteInnerCracking, TensionBarSpacing,
    /// SectionRegions; commit fe4652c) to GPC.Checkers.Concrete.Cracking.
    /// Fixtures/crack-legacy.csv: 936 serviceability states on the 6 sections of Fixtures/crack-sections.xml (7 standards, rotating exposure,
    /// sensitivity, duration, bond and overrides); Fixtures/crack-scalar-legacy.csv: 1400 crack widths and the whole requirement table.
    /// Captured on 7/10/2026 from ANTHEA refactoring/integrazione-d7b-d2 d2d3225 (supporto/test/CheckerMigration.Capture, mode tutte), which
    /// already applies D7-b (k2 = 0.5 with the neutral axis inside the section), wk = 0 with the neutral axis in the cover, the upper bound of
    /// eq. (7.14) with the tensile bars outside Ac,eff and the bounded h − x of the inner bands of hollow sections: the current rule reproduces
    /// every state.
    /// </summary>
    [TestClass]
    public class CrackMigrationTests
    {
        private static double D(string s) => double.Parse(s, CultureInfo.InvariantCulture);
        private static double? N(string s) => s.Length == 0 ? (double?)null : D(s);
        private static void Close(double expected, double actual, string what, double tolerance = 1e-9)
            => Assert.AreEqual(expected, actual, tolerance * Math.Max(1, Math.Abs(expected)), what);
        private static string[] Rows(string name, bool header = true)
            => File.ReadAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", name), Encoding.UTF8).Where(l => !l.StartsWith("#")).Skip(header ? 1 : 0).ToArray();
        private static ServiceabilityCombination Combination(string set)
            => set == "SLE" ? ServiceabilityCombination.Characteristic : set == "SLE_QP" ? ServiceabilityCombination.QuasiPermanent : ServiceabilityCombination.Frequent;
        private static string Exposure(string legacy) => legacy == "Da scegliere" ? null : legacy;

        private static string RegionKey(string legacy)
        {
            if (legacy == "Zona tesa efficace") return "TensileZone";
            if (legacy == "Sistema grossolano DS") return "DsCoarseSystem";
            if (legacy == "Anello interno") return "InnerRing";
            if (legacy.StartsWith("Faccia ")) return "Face" + legacy.Substring(7).Replace("−", "-");
            if (legacy.StartsWith("Parete interna ")) return "InnerWall" + legacy.Substring(15).Replace("−", "-");
            if (legacy.StartsWith("Fascia radiale ")) return "Radial(" + legacy.Substring(15).TrimEnd('°') + ")";
            throw new ArgumentException(legacy);
        }

        /// <summary>Legacy status without width: the outcome of the new check.</summary>
        private static CrackOutcome? LegacyOutcome(string status, string exposure)
        {
            if (status.StartsWith("Non richiesta")) return CrackOutcome.NotRequired;
            if (status.StartsWith("Selezionare la classe")) return CrackOutcome.MissingExposure;
            if (status.StartsWith("Selezionare esposizione")) return exposure == null ? CrackOutcome.MissingExposure : CrackOutcome.MissingDesignLimit;
            if (status.StartsWith("Selezionare wlim")) return CrackOutcome.MissingDesignLimit;
            if (status.StartsWith("wk richiede")) return CrackOutcome.RequiresLinearCrackedAnalysis;
            if (status.StartsWith("Asse neutro")) return CrackOutcome.NeutralAxisUndetermined;
            if (status.StartsWith("Nessuna armatura tesa")) return CrackOutcome.NoTensileReinforcement;
            if (status.StartsWith("Armatura/area efficace assente") || status.StartsWith("Area efficace nulla") || status.Contains("area o armatura efficace assente"))
                return CrackOutcome.NoEffectiveArea;
            if (status.StartsWith("Interasse automatico") || status.Contains("specificare l") || status.Contains("inserire interasse")) return CrackOutcome.SpacingUndetermined;
            return null;
        }

        // Nominal cover to the longitudinal bars of the captured sections: cover_mm + link diameter of the ANTHEA inputs.
        private static readonly Dictionary<string, double> Covers = new Dictionary<string, double>
        { { "R300x500", 40 }, { "T1200x800", 80 }, { "C1000", 80 }, { "R600x800H", 80 }, { "C1000H", 80 }, { "R400x400", 45 } };

        [TestMethod]
        public void LegacyCrackStatesAreReproduced()
        {
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures");
            GPC.Model.Models.Model archive;
            using (var stream = File.OpenRead(Path.Combine(folder, "crack-sections.xml"))) archive = ModelArchive.Load(stream);
            var rows = Rows("crack-legacy.csv");
            Assert.AreEqual(936, rows.Length);
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
            int widths = 0, verdicts = 0, regions = 0, errors = 0, coverOnly = 0, unbonded = 0;
            foreach (var row in rows)
            {
                var c = row.Split(';'); string id = "state " + c[0] + " " + c[1] + " " + c[2] + " " + c[17] + " " + c[18];
                double[] P(string s) => s.Split(',').Select(D).ToArray();
                var o = P(c[8]); var v1 = P(c[9]); var v2 = P(c[10]);
                var axes = new CoordinateSystem(new Point3d(o[0], o[1], o[2]), new Vector3d(v1[0], v1[1], v1[2]), new Vector3d(v2[0], v2[1], v2[2]));
                bool linear = bool.Parse(c[4]), tension = bool.Parse(c[6]);
                var section = (ReinforcedConcreteSection)archive.BeamProperties[c[1]];
                var force = new ResultBeamForces(D(c[11]), D(c[12]), D(c[13]), D(c[14]), D(c[15]), D(c[16]), axes);
                var stress = Checker(c, axes, linear, tension).GetTensionAnalysisResult(force);
                if (c[25] != "ok" && c[30].Contains("non convergente"))
                {
                    Assert.IsTrue(stress?.StrainPlane == null || section.ConcreteShape.GetPoints2d().Any(p => double.IsNaN(stress.StrainPlane.GetStrain(p)) || double.IsInfinity(stress.StrainPlane.GetStrain(p))), id);
                    errors++; continue;
                }
                var concrete = (ConcreteMaterialEuropeanCommon)section.ConcreteMaterial;
                var input = new SectionCrackInput(ServiceabilityMigrationTests.Standard(c[2]), Combination(c[17]), Exposure(c[18]), c[19] == "Sensibile", N(c[24]),
                    CrackSectionGeometry.From(section, c[1] == "C1000H"), stress.StrainPlane, SectionCrackInput.OrdinaryBarStresses(stress, section), linear, tension, false,
                    section.Rebars.First().RebarMaterial.E, concrete.Ecm, concrete.Fctm, c[20] == "Breve", c[21] == "Migliorata", Covers[c[1]], N(c[22]), N(c[23]),
                    () => Checker(c, axes, true, true).GetTensionAnalysisResult(force) is var uncracked ? uncracked.GetConcreteVerticesTension(uncracked.PsiRebar ?? 0).Max(v => v.tension) : 0);
                if (c[25] != "ok")
                {
                    Assert.AreEqual("error:ArgumentException", c[25], id);
                    Assert.ThrowsException<ArgumentException>(() => SectionCrackCheck.Evaluate(input), id + " " + c[30]);
                    errors++; continue;
                }
                var r = SectionCrackCheck.Evaluate(input);
                string status = c[30];
                var width = N(c[26]);
                Assert.AreEqual(width.HasValue, r.Width.HasValue, id + " width defined: " + status + " / " + r.Status);
                if (width.HasValue) { Close(width.Value, r.Width.Value, id + " wk"); widths++; }
                var wlim = N(c[27]);
                Assert.AreEqual(wlim.HasValue, r.Limit.HasValue, id + " wlim defined"); if (wlim.HasValue) Close(wlim.Value, r.Limit.Value, id + " wlim");
                var ratio = N(c[28]);
                Assert.AreEqual(ratio.HasValue, r.Ratio.HasValue, id + " ratio defined: " + status); if (ratio.HasValue) Close(ratio.Value, r.Ratio.Value, id + " ratio");
                bool? passed = c[29].Length == 0 ? (bool?)null : bool.Parse(c[29]);
                Assert.AreEqual(passed, r.Passed, id + " passed: " + status + " / " + r.Status);
                if (passed.HasValue) verdicts++;
                if (!width.HasValue && !passed.HasValue)
                {
                    var expected = LegacyOutcome(status, Exposure(c[18]));
                    Assert.IsNotNull(expected, id + " unmapped legacy status " + status);
                    Assert.AreEqual(expected.Value, r.Outcome, id + " outcome: " + status + " / " + r.Status);
                }
                var aceff = N(c[31]); if (aceff.HasValue) Close(aceff.Value, r.EffectiveArea.Value, id + " Ac,eff");
                var aseff = N(c[32]); if (aseff.HasValue) Close(aseff.Value, r.EffectiveSteel.Value, id + " As,eff");
                var spacing = N(c[33]); if (spacing.HasValue) Close(spacing.Value, r.BarSpacing.Value, id + " s");
                var legacyRegions = c[35].Length == 0 ? new string[0] : c[35].Split('|');
                Assert.AreEqual(legacyRegions.Length, r.Regions.Count, id + " regions " + c[35] + " / " + string.Join(",", r.Regions.Select(x => x.Key)));
                for (int i = 0; i < legacyRegions.Length; i++)
                {
                    var parts = legacyRegions[i].Split(':'); var region = r.Regions[i];
                    Assert.AreEqual(RegionKey(parts[0]), region.Key, id + " region key");
                    Close(D(parts[1]), region.Area, id + " " + region.Key + " area"); Close(D(parts[2]), region.SteelArea, id + " " + region.Key + " steel");
                    var regionWidth = N(parts[3]);
                    Assert.AreEqual(regionWidth.HasValue, region.Width.HasValue, id + " " + region.Key + " width defined");
                    if (regionWidth.HasValue) Close(regionWidth.Value, region.Width.Value, id + " " + region.Key + " width");
                    CollectionAssert.AreEqual(parts[4].Length == 0 ? new int[0] : parts[4].Split('/').Select(int.Parse).ToArray(), region.BarIndices.ToArray(), id + " " + region.Key + " bars");
                    regions++;
                }
                // Branches where wk does not come from the bars in Ac,eff: neutral axis in the cover (wk = 0, no region) and tensile bars outside Ac,eff
                // (upper bound of EN 1992-1-1 7.3.4(3), eq. (7.14), one tensile zone without bars).
                if (status.StartsWith("Asse neutro nel copriferro"))
                {
                    Assert.IsTrue(r.Width == 0 && r.Passed == true && r.Regions.Count == 0, id + " " + r.Status); coverOnly++;
                }
                if (status.StartsWith("Nessuna barra in Ac,eff"))
                {
                    Assert.IsTrue(r.Regions.Count == 1 && r.Regions[0].Key == "TensileZone" && r.Regions[0].BarIndices.Count == 0, id + " " + r.Status); unbonded++;
                }
            }
            // 392 widths (fully compressed included), 377 verdicts, 1112 regions with area, steel, width and bars, 10 legacy errors, 22 neutral axes in the cover,
            // 8 tensile zones without bars in Ac,eff.
            Assert.AreEqual(392, widths); Assert.AreEqual(377, verdicts); Assert.AreEqual(1112, regions); Assert.AreEqual(10, errors); Assert.AreEqual(22, coverOnly);
            Assert.AreEqual(8, unbonded);
        }

        /// <summary>Partially compressed 300×500, bars Ø20 at 50 mm from the edges: neutral axis 20 mm from the bottom edge, inside the cover.</summary>
        [TestMethod]
        public void NeutralAxisWithinTheCoverGivesZeroWidth()
        {
            var outline = new[] { new Point2d(-150, -250), new Point2d(150, -250), new Point2d(150, 250), new Point2d(-150, 250) };
            CrackBar Bar(double x, double y) => new CrackBar(x, y, 20, 314.16);
            // ε = −1e-5 (y + 230): tension below y = −230, bars at y = ±200 compressed.
            var plane = new StrainPlane(0, -1e-5, new Point2d(0, -230), 0);
            SectionCrackResult Evaluate(CrackBar[] bars) => SectionCrackCheck.Evaluate(new SectionCrackInput(ServiceabilityMigrationTests.Standard("EN 1992-1-1"),
                ServiceabilityCombination.QuasiPermanent, "XC3", false, null, new CrackSectionGeometry(outline, null, bars, CrackBarLayout.Rows), plane,
                bars.Select(b => 200000 * plane.GetStrain(b.X, b.Y)), true, false, false, 200000, 33000, 2.9, false, true, 40));
            var r = Evaluate(new[] { Bar(-100, -200), Bar(100, -200), Bar(-100, 200), Bar(100, 200) });
            Assert.AreEqual(CrackOutcome.Evaluated, r.Outcome, r.Status);
            Assert.AreEqual(CrackVerdict.Satisfied, r.Verdict);
            Assert.AreEqual(0, r.Width.Value); Assert.AreEqual(0, r.Ratio.Value);
            // Bars only at the compressed top: the tensile bottom is unreinforced, no verdict.
            var top = Evaluate(new[] { Bar(-100, 200), Bar(100, 200) });
            Assert.AreEqual(CrackOutcome.NoTensileReinforcement, top.Outcome, top.Status);
            Assert.IsNull(top.Width);
        }

        /// <summary>
        /// Same section, neutral axis at y = −150: h − x = 100 mm, hc,eff = min[2.5·50; 100/3; 250] = 33.3 mm above the bottom bars (σs = 100 MPa).
        /// EC2 7.3.4(3), eq. (7.14): wk = 1.3 (h − x) · 0.6 σs/Es = 130 · 3e-4 = 0.039 mm; NTC: 1.7 · 0.75 · 100 · 3e-4 = 0.03825 mm.
        /// </summary>
        [TestMethod]
        public void TensileBarsOutsideEffectiveAreaGiveUpperBound()
        {
            var outline = new[] { new Point2d(-150, -250), new Point2d(150, -250), new Point2d(150, 250), new Point2d(-150, 250) };
            var bars = new[] { new CrackBar(-100, -200, 20, 314.16), new CrackBar(100, -200, 20, 314.16), new CrackBar(-100, 200, 20, 314.16), new CrackBar(100, 200, 20, 314.16) };
            var plane = new StrainPlane(0, -1e-5, new Point2d(0, -150), 0);
            SectionCrackResult Evaluate(string standard, string exposure) => SectionCrackCheck.Evaluate(new SectionCrackInput(ServiceabilityMigrationTests.Standard(standard),
                ServiceabilityCombination.QuasiPermanent, exposure, false, null, new CrackSectionGeometry(outline, null, bars, CrackBarLayout.Rows), plane,
                bars.Select(b => 200000 * plane.GetStrain(b.X, b.Y)), true, false, false, 200000, 33000, 2.9, false, true, 40));
            var r = Evaluate("EN 1992-1-1", "XC3");
            Assert.AreEqual(CrackOutcome.Evaluated, r.Outcome, r.Status);
            Assert.AreEqual(.039, r.Width.Value, 1e-12); Assert.AreEqual(CrackVerdict.Satisfied, r.Verdict);
            Assert.AreEqual(300 * 100 / 3.0, r.EffectiveArea.Value, 1e-6); Assert.AreEqual(0, r.EffectiveSteel.Value);
            Assert.AreEqual(1.7 * .75 * 100 * 3e-4, Evaluate("NTC 2018", "XC1").Width.Value, 1e-12);
        }

        // D7-b: with the neutral axis inside the section k2 = 0.5 for every profile (Circolare 2019 C4.1.2.2.4.5; EN 1992-1-1 7.3.4(3));
        // the legacy NTC / CNR-DT 200 rule (k2 from the bar stresses) only with SectionCrackInput.NtcK2FromCompressedBars. The trace has one "k2"
        // entry, the section-level one with its reason (the width formula does not repeat it). Inner surfaces of hollow sections keep the k2 of their band.
        private const string BendingK2 = "neutral axis inside the section: bending, k2 = 0.5 (Circolare 2019 C4.1.2.2.4.5; EN 1992-1-1 7.3.4(3))";
        private static readonly Point2d[] Rectangle300x500 = { new Point2d(-150, -250), new Point2d(150, -250), new Point2d(150, 250), new Point2d(-150, 250) };
        private static CrackBar[] Row(double y) => new[] { new CrackBar(-100, y, 20, 314.16), new CrackBar(0, y, 20, 314.16), new CrackBar(100, y, 20, 314.16) };
        private static readonly CrackBar[] Bottom = Row(-200), Doubly = Row(-200).Concat(Row(200)).ToArray();

        private static SectionCrackResult Crack(Standard standard, string exposure, CrackBar[] bars, StrainPlane plane, bool legacyK2)
            => SectionCrackCheck.Evaluate(new SectionCrackInput(standard, ServiceabilityCombination.QuasiPermanent, exposure, false, null,
                new CrackSectionGeometry(Rectangle300x500, null, bars, CrackBarLayout.Rows), plane, bars.Select(b => 200000 * plane.GetStrain(b.X, b.Y)), true, false, false,
                200000, 33000, 2.9, false, true, 40, ntcK2FromCompressedBars: legacyK2));

        private static Standard Ntc => ServiceabilityMigrationTests.Standard("NTC 2018");

        private static string Trace(SectionCrackResult r)
            => string.Join("\n", r.Details.Select(d => d.Symbol + "=" + d.Value.ToString("R", CultureInfo.InvariantCulture) + " [" + d.Expression + "]"));

        private static void AssertSame(SectionCrackResult expected, SectionCrackResult actual, string what)
        {
            Assert.AreEqual(expected.Outcome, actual.Outcome, what + " outcome"); Assert.AreEqual(expected.Status, actual.Status, what + " status");
            Assert.AreEqual(expected.Width, actual.Width, what + " wk"); Assert.AreEqual(expected.Ratio, actual.Ratio, what + " ratio");
            Assert.AreEqual(expected.Passed, actual.Passed, what + " passed"); Assert.AreEqual(expected.K2, actual.K2, what + " k2");
            Assert.AreEqual(Trace(expected), Trace(actual), what + " trace");
        }

        /// <summary>
        /// (a) 300×500, 3Ø20 at 50 mm from the bottom only, neutral axis at y = 50: h − x = 300, hc,eff = min[125; 100; 250] = 100, Ac,eff = 30 000 mm²,
        /// σs = 200 MPa, s = 100 mm ≤ 5 (c + Ø/2). Circolare C4.1.2.2.4.5: wk = 1.7 Δsm (εsm − εcm), Δsm = (3.4 c + k1 k2 0.425 Ø/ρ)/1.7.
        /// No bar is compressed, so the legacy rule gave k2 = 1 although the section is bent.
        /// </summary>
        [TestMethod]
        public void SinglyReinforcedBeamInBendingUsesK2Half()
        {
            var plane = new StrainPlane(0, -4e-6, new Point2d(0, 50), 0);
            double sigma = 200000 * plane.GetStrain(0, -200), rho = 3 * 314.16 / 30000.0;
            double strain = Math.Max((sigma - .4 * 2.9 / rho * (1 + 200000 / 33000.0 * rho)) / 200000, .6 * sigma / 200000);
            double Hand(double k2) => 1.7 * ((3.4 * 40 + .8 * k2 * .425 * 20 / rho) / 1.7) * strain;
            foreach (var standard in new Standard[] { Ntc, new StandardCNR200() })
            {
                string what = standard.GetType().Name;
                var r = Crack(standard, "XC1", Bottom, plane, false);
                Assert.AreEqual(CrackOutcome.Evaluated, r.Outcome, what + " " + r.Status);
                Assert.AreEqual(30000, r.EffectiveArea.Value, 1e-6, what); Assert.AreEqual(100, r.BarSpacing.Value, 1e-9, what);
                Assert.AreEqual(.5, r.K2.Value, what); Assert.AreEqual(Hand(.5), r.Width.Value, 1e-12, what + " wk with k2 = 0.5");
                Assert.AreEqual(BendingK2, r.Details.Single(d => d.Symbol == "k2").Expression, what);
                Assert.AreEqual(.1905, r.Width.Value, 5e-4, what);
                var legacy = Crack(standard, "XC1", Bottom, plane, true);
                Assert.AreEqual(1, legacy.K2.Value, what); Assert.AreEqual(Hand(1), legacy.Width.Value, 1e-12, what + " legacy wk with k2 = 1");
                StringAssert.StartsWith(legacy.Details.Single(d => d.Symbol == "k2").Expression, "legacy rule", what);
                Assert.AreEqual(1, legacy.Details.Single(d => d.Symbol == "k2").Value, what);
                // The width formula still lists its own terms around k2 (k1 before, εsm − εcm after).
                var symbols = r.Details.Select(d => d.Symbol).ToList();
                Assert.IsTrue(symbols.Contains("k1") && symbols.Contains("εsm − εcm") && symbols.IndexOf("k2") < symbols.IndexOf("k1"), what + "\n" + Trace(r));
                Assert.IsFalse(legacy.Details.Any(d => d.Expression == BendingK2), what);
            }
        }

        /// <summary>(b) Neutral axis inside the section with an axial force: bending, k2 = 0.5 whether the bars are all tensile or not.</summary>
        [TestMethod]
        public void BendingWithAxialForceUsesK2Half()
        {
            // Compression and bending, singly reinforced: neutral axis at y = −100 (350 mm compressed), σs = 80 MPa, no compressed bar.
            var compression = new StrainPlane(0, -4e-6, new Point2d(0, -100), 0);
            var r = Crack(Ntc, "XC1", Bottom, compression, false); var legacy = Crack(Ntc, "XC1", Bottom, compression, true);
            Assert.AreEqual(CrackOutcome.Evaluated, r.Outcome, r.Status); Assert.AreEqual(.5, r.K2.Value); Assert.AreEqual(1, legacy.K2.Value);
            Assert.IsTrue(r.Width.Value > 0 && r.Width.Value < legacy.Width.Value, r.Width + " / " + legacy.Width);
            // Doubly reinforced: the top bars are compressed, 0.5 also with the legacy rule and the same width.
            var doubly = Crack(Ntc, "XC1", Doubly, compression, false); var doublyLegacy = Crack(Ntc, "XC1", Doubly, compression, true);
            Assert.AreEqual(CrackOutcome.Evaluated, doubly.Outcome, doubly.Status); Assert.AreEqual(.5, doubly.K2.Value); Assert.AreEqual(.5, doublyLegacy.K2.Value);
            Assert.AreEqual(doublyLegacy.Width, doubly.Width); Assert.AreEqual(doublyLegacy.Passed, doubly.Passed);
            // Tension and bending, neutral axis at y = 230 in the top cover: all the bars are tensile but the section is still bent.
            var tension = new StrainPlane(0, -4e-6, new Point2d(0, 230), 0);
            var t = Crack(Ntc, "XC1", Doubly, tension, false); var tLegacy = Crack(Ntc, "XC1", Doubly, tension, true);
            Assert.AreEqual(CrackOutcome.Evaluated, t.Outcome, t.Status); Assert.AreEqual(.5, t.K2.Value); Assert.AreEqual(1, tLegacy.K2.Value);
            Assert.IsTrue(t.Width.Value < tLegacy.Width.Value);
            Assert.AreEqual(BendingK2, t.Details.Single(d => d.Symbol == "k2").Expression);
        }

        /// <summary>
        /// (c) Pure compression, also with εc,max = 0 or a positive εc,max ≤ 1e-12 and with no strain at all (σs = 0, legacy k2 of the bars = 1): wk = 0, check
        /// evaluated and satisfied, K2 null and no k2 in the trace (bending is never suggested), identical with and without the legacy option.
        /// </summary>
        [TestMethod]
        public void PureCompressionGivesZeroWidthWithoutK2()
        {
            var planes = new[]
            {
                new StrainPlane(0, 0, new Point2d(0, 0), -5e-4), new StrainPlane(0, -1e-6, new Point2d(0, -250), 0), new StrainPlane(0, -1e-6, new Point2d(0, -250), 5e-13),
                new StrainPlane(2e-7, -1e-6, new Point2d(150, -250), 1e-12), new StrainPlane(0, 0, new Point2d(0, 0), 0)
            };
            int checks = 0;
            foreach (var standard in new Standard[] { Ntc, new StandardCNR200() })
                foreach (var bars in new[] { Bottom, Doubly })
                    foreach (var plane in planes)
                    {
                        string what = standard.GetType().Name + " " + bars.Length + " bars, ε0 = " + plane.StrainReferencePoint + ", χ = " + plane.ChiX + "/" + plane.ChiY;
                        var r = Crack(standard, "XC1", bars, plane, false);
                        Assert.IsTrue(r.Details.Single(d => d.Symbol == "εc,max").Value <= 1e-12, what);
                        Assert.AreEqual(CrackOutcome.Evaluated, r.Outcome, what); Assert.AreEqual(CrackVerdict.Satisfied, r.Verdict, what);
                        Assert.AreEqual("Section entirely compressed", r.Status, what);
                        Assert.AreEqual(0, r.Width.Value, what); Assert.AreEqual(0, r.Ratio.Value, what); Assert.IsNull(r.K2, what);
                        Assert.IsFalse(r.Details.Any(d => d.Symbol == "k2" || d.Expression.Contains("bending") || d.Expression.Contains("k2")), what + "\n" + Trace(r));
                        AssertSame(r, Crack(standard, "XC1", bars, plane, true), what + " legacy");
                        checks++;
                    }
            Assert.AreEqual(20, checks);
        }

        /// <summary>(d) Entirely tensile sections keep their branch: k2 = (εmax + εmin)/(2 εmax), 1 in uniform tension; the option has no effect.</summary>
        [TestMethod]
        public void EntirelyTensileSectionIsUnaffectedByTheLegacyOption()
        {
            var eccentric = new StrainPlane(0, -1e-6, new Point2d(0, 250), 2e-4); // 2e-4 at the top, 7e-4 at the bottom
            var uniform = new StrainPlane(0, 0, new Point2d(0, 0), 5e-4);
            foreach (var pair in new[] { Tuple.Create(Ntc, "XC1"), Tuple.Create((Standard)new StandardCNR200(), "XC1"), Tuple.Create((Standard)ServiceabilityMigrationTests.Standard("EN 1992-1-1"), "XC3") })
            {
                var standard = pair.Item1; string exposure = pair.Item2, what = standard.GetType().Name;
                var r = Crack(standard, exposure, Doubly, eccentric, false);
                Assert.AreEqual(CrackOutcome.Evaluated, r.Outcome, what + " " + r.Status); StringAssert.StartsWith(r.Status, "Entirely tensile", what);
                Assert.AreEqual((7e-4 + 2e-4) / (2 * 7e-4), r.K2.Value, 1e-12, what);
                AssertSame(r, Crack(standard, exposure, Doubly, eccentric, true), what + " eccentric");
                var u = Crack(standard, exposure, Doubly, uniform, false);
                Assert.AreEqual(1, u.K2.Value, what); AssertSame(u, Crack(standard, exposure, Doubly, uniform, true), what + " uniform");
                Assert.IsFalse(r.Details.Concat(u.Details).Any(d => d.Expression.Contains("bending") || d.Expression.StartsWith("legacy")), what);
            }
        }

        /// <summary>(e) Eurocode family: k2 = 0.5 with the neutral axis inside the section already before D7-b; the option has no effect.</summary>
        [TestMethod]
        public void EurocodeProfilesAreUnaffectedByTheLegacyOption()
        {
            var plane = new StrainPlane(0, -4e-6, new Point2d(0, 50), 0);
            foreach (var name in new[] { "EN 1992-1-1", "UNI EN 1992-1-1", "DIN EN 1992-1-1", "DS EN 1992-1-1", "NS EN 1992-1-1" })
            {
                string exposure = name == "UNI EN 1992-1-1" ? "XC1" : "XC3";
                var r = Crack(ServiceabilityMigrationTests.Standard(name), exposure, Bottom, plane, false);
                AssertSame(r, Crack(ServiceabilityMigrationTests.Standard(name), exposure, Bottom, plane, true), name);
                if (r.Outcome != CrackOutcome.Evaluated) { Assert.AreEqual("NS EN 1992-1-1", name, r.Status); continue; }
                Assert.AreEqual(.5, r.K2.Value, name); Assert.AreEqual(BendingK2, r.Details.Single(d => d.Symbol == "k2").Expression, name);
            }
            // EN by hand (7.8-7.11), k2 = 0.5: sr,max = 3.4 c + 0.8 · 0.5 · 0.425 Ø/ρ.
            double sigma = 200000 * plane.GetStrain(0, -200), rho = 3 * 314.16 / 30000.0;
            double strain = Math.Max(.6 * sigma / 200000, (sigma - .4 * 2.9 / rho * (1 + 200000 / 33000.0 * rho)) / 200000);
            Assert.AreEqual((3.4 * 40 + .8 * .5 * .425 * 20 / rho) * strain, Crack(ServiceabilityMigrationTests.Standard("EN 1992-1-1"), "XC3", Bottom, plane, false).Width.Value, 1e-12);
        }

        /// <summary>
        /// (f) Branches where k2 does not enter wk: neutral axis within the cover without tensile bars (wk = 0, or no verdict without bars on that side) and
        /// tensile bars outside Ac,eff (upper bound of eq. (7.14)): same width and verdict with and without the option; K2 is reported (0.5 now).
        /// </summary>
        [TestMethod]
        public void CoverAndUnbondedBranchesKeepTheirWidth()
        {
            var cover = new StrainPlane(0, -1e-5, new Point2d(0, -230), 0);
            foreach (var bars in new[] { Bottom, Doubly })
            {
                var r = Crack(Ntc, "XC1", bars, cover, false); var legacy = Crack(Ntc, "XC1", bars, cover, true);
                Assert.AreEqual(CrackOutcome.Evaluated, r.Outcome, r.Status); Assert.AreEqual(0, r.Width.Value); Assert.AreEqual(CrackVerdict.Satisfied, r.Verdict);
                Assert.AreEqual(legacy.Width, r.Width); Assert.AreEqual(legacy.Passed, r.Passed); Assert.AreEqual(legacy.Status, r.Status);
                Assert.AreEqual(.5, r.K2.Value); Assert.AreEqual(.5, legacy.K2.Value, "the bars are compressed");
            }
            var top = Row(200);
            Assert.AreEqual(CrackOutcome.NoTensileReinforcement, Crack(Ntc, "XC1", top, cover, false).Outcome);
            Assert.AreEqual(CrackOutcome.NoTensileReinforcement, Crack(Ntc, "XC1", top, cover, true).Outcome);
            // Neutral axis at y = −150, singly reinforced: σs = 100 MPa, no compressed bar (legacy k2 = 1), hc,eff = 33.3 mm above the bars.
            var unbonded = new StrainPlane(0, -1e-5, new Point2d(0, -150), 0);
            var u = Crack(Ntc, "XC1", Bottom, unbonded, false); var uLegacy = Crack(Ntc, "XC1", Bottom, unbonded, true);
            Assert.AreEqual(CrackOutcome.Evaluated, u.Outcome, u.Status); StringAssert.StartsWith(u.Status, "No bar in Ac,eff");
            Assert.AreEqual(1.7 * .75 * 100 * 3e-4, u.Width.Value, 1e-12); Assert.AreEqual(uLegacy.Width, u.Width); Assert.AreEqual(uLegacy.Passed, u.Passed);
            Assert.AreEqual(.5, u.K2.Value); Assert.AreEqual(1, uLegacy.K2.Value);
        }

        /// <summary>
        /// (g) Hollow section in bending: the neutral axis cuts the section (k2 = 0.5 for the outer tensile zone), but each inner wall is checked as a local
        /// area with the k2 of its own tensile band, for every profile and with or without the legacy option. Box 400×600, hole 200×400, 4Ø20 at y = ±250,
        /// neutral axis at y = 0: band of the bottom wall 200 ≤ −y ≤ 250 (hc,eff = min[2.5 · 50; 100/2] = 50), ε = 8e-4 … 1e-3, k2 = (8e-4 + 1e-3)/(2e-3) = 0.9;
        /// 2Ø20 in the band (x = ±50), Ac,eff = 200 · 50, c = 40, s = 100, σs = 200 MPa. That wall governs; the side walls are tensile without bars.
        /// </summary>
        [TestMethod]
        public void HollowSectionInnerWallKeepsItsBandK2()
        {
            var box = new[] { new Point2d(-200, -300), new Point2d(200, -300), new Point2d(200, 300), new Point2d(-200, 300) };
            var hole = new[] { new Point2d(-100, -200), new Point2d(100, -200), new Point2d(100, 200), new Point2d(-100, 200) };
            var bars = new[] { -150d, -50, 50, 150 }.SelectMany(x => new[] { new CrackBar(x, -250, 20, Math.PI * 100), new CrackBar(x, 250, 20, Math.PI * 100) }).ToArray();
            var plane = new StrainPlane(0, -4e-6, new Point2d(0, 0), 0);
            SectionCrackResult Hollow(Standard standard, string exposure, bool legacyK2) => SectionCrackCheck.Evaluate(new SectionCrackInput(standard, ServiceabilityCombination.QuasiPermanent,
                exposure, false, null, new CrackSectionGeometry(box, new[] { hole }, bars, CrackBarLayout.Rows), plane, bars.Select(b => 200000 * plane.GetStrain(b.X, b.Y)), true, false, false,
                200000, 33000, 2.9, false, true, 40, ntcK2FromCompressedBars: legacyK2));
            double rho = 2 * Math.PI * 100 / (200 * 50.0), sigma = 200;
            double strain = Math.Max((sigma - .4 * 2.9 / rho * (1 + 200000 / 33000.0 * rho)) / 200000, .6 * sigma / 200000);
            double ntcHand = 1.7 * ((3.4 * 40 + .8 * .9 * .425 * 20 / rho) / 1.7) * strain;
            foreach (var pair in new[] { Tuple.Create(Ntc, "XC1"), Tuple.Create((Standard)new StandardCNR200(), "XC1"), Tuple.Create((Standard)ServiceabilityMigrationTests.Standard("EN 1992-1-1"), "XC3") })
            {
                string what = pair.Item1.GetType().Name;
                var r = Hollow(pair.Item1, pair.Item2, false);
                Assert.AreEqual(CrackOutcome.InnerSurfaceUnreinforced, r.Outcome, what + " " + r.Status); Assert.IsNull(r.Passed, what);
                Assert.AreEqual("InnerWall-y", r.GoverningRegion, what); Assert.AreEqual(.9, r.K2.Value, 1e-12, what);
                Assert.AreEqual(BendingK2, r.Details.Single(d => d.Symbol == "k2").Expression, what + ": the section-level k2 is 0.5");
                Assert.AreEqual(.5, r.Details.Single(d => d.Symbol == "k2").Value, what);
                Assert.AreEqual(.9, r.Details.Single(d => d.Symbol == "InnerWall-y · k2").Value, 1e-12, what);
                Assert.IsTrue(r.Regions.Single(g => g.Key == "TensileZone").Width < r.Width, what);
                Assert.AreEqual(ntcHand, r.Width.Value, 1e-12, what + " wk of the inner wall with k2 = 0.9");
                // Legacy rule: the top bars are compressed, so the bars also give 0.5; only the reason of the section-level k2 differs (NTC / CNR-DT 200).
                var legacy = Hollow(pair.Item1, pair.Item2, true);
                Assert.AreEqual(r.Outcome, legacy.Outcome, what); Assert.AreEqual(r.Status, legacy.Status, what); Assert.AreEqual(r.Width, legacy.Width, what);
                Assert.AreEqual(r.Ratio, legacy.Ratio, what); Assert.AreEqual(r.Passed, legacy.Passed, what); Assert.AreEqual(r.K2, legacy.K2, what);
                Assert.AreEqual(Trace(r), Trace(legacy).Replace("legacy rule (before D7-b): 0.5 with a compressed bar, 1.0 otherwise", BendingK2), what);
            }
        }

        /// <summary>
        /// (h) Tensile depth h − x of the inner bands (fix of 0.0.17.0). Box 400×600, hole 200×400, 4Ø20 at y = ±250 and 3Ø20 at x = ±150 (y = 0, ±100),
        /// spacing assigned 300 mm &gt; 5 (c + Ø/2) = 250 mm, so sr,max = 1.3 (h − x) (EN 1992-1-1 7.3.4(3), eq. (7.14)) and NTC Δsm,far = 0.75 (h − x) govern.
        /// Bands: ±y walls 200 × 50 (2Ø20, c = 40), ±x walls 50 × 400 (3Ø20, c = 40). Before the fix h − x was εmax/|∇ε| also for a noise gradient
        /// (1e-12 1/mm: h − x = 5e8 mm, wk ≈ 1e5 mm) and Max(B, H) for a zero gradient.
        /// Noise or zero gradient: uniform tension, k2 = 1, h − x = h of the section normal to the face (600 for ±y, 400 for ±x).
        /// Eccentric tension, ε = 2e-4 − 1e-6 (y − 250): neutral axis at y = 450, outside; h − x = min(εmax/|∇ε|; h of the section along the gradient = 600)
        /// (ANTHEA R15, decision of the user of 7/10/2026): −y band 700 → 600, ±x bands 650 → 600, +y band 250 (unchanged, no trace entry). Bending with
        /// the neutral axis inside: no h − x entry, nothing changes. Continuity where the neutral axis enters the section:
        /// <see cref="HollowSectionInnerBandDepthIsContinuousWhereTheNeutralAxisEnters"/>; residual jump at the uniform-tension threshold:
        /// <see cref="HollowSectionInnerBandDepthJumpsAtTheUniformThresholdWhereTheHeightsDiffer"/>.
        /// </summary>
        [TestMethod]
        public void HollowSectionInnerBandsHaveABoundedTensileDepth()
        {
            var walls = InnerWalls; var standards = InnerStandards;
            Func<Standard, string, StrainPlane, SectionCrackResult> Hollow = HollowBox; Func<SectionCrackResult, string, string, GPC.Checkers.Concrete.Shear.ShearCalculationDetail> Entry = BandEntry;
            Func<StrainPlane, string, double> BandStress = HollowBandStress; Func<bool, double, double, int, double, double, double> Hand = HollowBandHand;
            int checks = 0;
            foreach (var plane in new[] { new StrainPlane(3e-13, -9.5e-13, new Point2d(0, 0), 5e-4), new StrainPlane(0, 0, new Point2d(0, 0), 5e-4) })
                foreach (var s in standards)
                {
                    var r = Hollow(s.Item1, s.Item2, plane); string what = s.Item1.GetType().Name + " χ = " + plane.ChiX + "/" + plane.ChiY;
                    Assert.IsTrue(r.Regions.Any(g => g.Key == "Face+x"), what + ": entirely tensile branch\n" + Trace(r));
                    Assert.IsTrue(r.Width.Value < 1, what + ": wk = " + r.Width + " mm\n" + Trace(r));
                    foreach (var wall in walls)
                    {
                        double h = wall.Item1.EndsWith("x") ? 400 : 600;
                        var depth = Entry(r, wall.Item1, "h − x");
                        Assert.IsNotNull(depth, what + " " + wall.Item1 + "\n" + Trace(r));
                        Assert.AreEqual(h, depth.Value, what + " " + wall.Item1); StringAssert.StartsWith(depth.Expression, "uniform tension", what);
                        Assert.AreEqual(1, Entry(r, wall.Item1, "k2").Value, what + " " + wall.Item1 + ": k2 = 1");
                        Assert.AreEqual(Hand(s.Item3, BandStress(plane, wall.Item1), wall.Item2, wall.Item3, h, 1), Entry(r, wall.Item1, "wk").Value, 1e-12, what + " " + wall.Item1 + " wk by hand");
                        checks++;
                    }
                }
            var eccentric = new StrainPlane(0, -1e-6, new Point2d(0, 250), 2e-4);
            foreach (var s in standards)
            {
                var r = Hollow(s.Item1, s.Item2, eccentric); string what = s.Item1.GetType().Name + " eccentric";
                Assert.IsTrue(r.Regions.Any(g => g.Key == "Face+x"), what + ": entirely tensile branch\n" + Trace(r));
                foreach (var wall in walls)
                {
                    var depth = Entry(r, wall.Item1, "h − x"); double expected = wall.Item1 == "InnerWall+y" ? 250 : 600;
                    if (wall.Item1 == "InnerWall+y") Assert.IsNull(depth, what + ": +y band within the section, no entry");
                    else
                    {
                        Assert.AreEqual(expected, depth.Value, 1e-9, what + " " + wall.Item1);
                        Assert.AreEqual("min[εmax/|∇ε| = " + (wall.Item1 == "InnerWall-y" ? "700" : "650") + " mm; h along the gradient]: neutral axis outside the section, x = 0 (EN 1992-1-1 7.3.4(3), eq. (7.14))",
                            depth.Expression, what);
                    }
                    double k2 = Entry(r, wall.Item1, "k2").Value, sigma = BandStress(eccentric, wall.Item1);
                    double bandMin = wall.Item1 == "InnerWall-y" ? 6.5e-4 : wall.Item1 == "InnerWall+y" ? 2e-4 : 2.5e-4, bandMax = wall.Item1 == "InnerWall-y" ? 7e-4 : wall.Item1 == "InnerWall+y" ? 2.5e-4 : 6.5e-4;
                    Assert.AreEqual((bandMin + bandMax) / (2 * bandMax), k2, 1e-12, what + " " + wall.Item1 + " k2 of the band");
                    Assert.AreEqual(Hand(s.Item3, sigma, wall.Item2, wall.Item3, expected, k2), Entry(r, wall.Item1, "wk").Value, 1e-12, what + " " + wall.Item1 + " wk by hand");
                    checks++;
                }
            }
            // Bending, neutral axis at y = 0 inside the section: no inner band gets an h − x entry (the depth stays εmax/|∇ε|).
            var bending = Hollow(Ntc, "XC1", new StrainPlane(0, -4e-6, new Point2d(0, 0), 0));
            Assert.IsFalse(bending.Details.Any(d => d.Symbol.StartsWith("InnerWall") && d.Symbol.EndsWith("· h − x")), Trace(bending));
            Assert.AreEqual(Hand(true, 200, 10000, 2, 250, .9), Entry(bending, "InnerWall-y", "wk").Value, 1e-12, "bending: −y band with h − x = 250 mm from the neutral axis");
            Assert.AreEqual(4 * 4 + 2 * 4, checks);
        }

        // Box 400×600 with a hole 200×400 of (h) and (i): 4Ø20 at y = ±250 and 3Ø20 at x = ±150 (y = 0, ±100), spacing assigned 300 mm.
        private static readonly Point2d[] InnerBoxOutline = { new Point2d(-200, -300), new Point2d(200, -300), new Point2d(200, 300), new Point2d(-200, 300) };
        private static readonly Point2d[] InnerBoxHole = { new Point2d(-100, -200), new Point2d(100, -200), new Point2d(100, 200), new Point2d(-100, 200) };
        private static readonly CrackBar[] InnerBoxBars = new[] { -150d, -50, 50, 150 }.SelectMany(x => new[] { new CrackBar(x, -250, 20, Math.PI * 100), new CrackBar(x, 250, 20, Math.PI * 100) })
            .Concat(new[] { -100d, 0, 100 }.SelectMany(y => new[] { new CrackBar(-150, y, 20, Math.PI * 100), new CrackBar(150, y, 20, Math.PI * 100) })).ToArray();
        /// <summary>Wall, area of the band (mm2), bars in the band.</summary>
        private static readonly Tuple<string, double, int>[] InnerWalls =
            { Tuple.Create("InnerWall+x", 20000d, 3), Tuple.Create("InnerWall-x", 20000d, 3), Tuple.Create("InnerWall+y", 10000d, 2), Tuple.Create("InnerWall-y", 10000d, 2) };
        /// <summary>Standard, exposure, NTC (Δsm path) or EN (sr,max path).</summary>
        private static readonly Tuple<Standard, string, bool>[] InnerStandards =
            { Tuple.Create(Ntc, "XC1", true), Tuple.Create((Standard)ServiceabilityMigrationTests.Standard("EN 1992-1-1"), "XC3", false) };

        private static SectionCrackResult HollowBox(Standard standard, string exposure, StrainPlane plane) => SectionCrackCheck.Evaluate(new SectionCrackInput(standard,
            ServiceabilityCombination.QuasiPermanent, exposure, false, null, new CrackSectionGeometry(InnerBoxOutline, new[] { InnerBoxHole }, InnerBoxBars, CrackBarLayout.Rows), plane,
            InnerBoxBars.Select(b => 200000 * plane.GetStrain(b.X, b.Y)), true, false, false, 200000, 33000, 2.9, false, true, 40, spacingOverride: 300));

        private static GPC.Checkers.Concrete.Shear.ShearCalculationDetail BandEntry(SectionCrackResult r, string wall, string symbol) => r.Details.SingleOrDefault(d => d.Symbol == wall + " · " + symbol);

        private static double HollowBandStress(StrainPlane plane, string wall)
        {
            Func<CrackBar, bool> inBand = wall == "InnerWall+x" ? b => b.X == 150 && Math.Abs(b.Y) <= 100 : wall == "InnerWall-x" ? b => b.X == -150 && Math.Abs(b.Y) <= 100
                : wall == "InnerWall+y" ? b => b.Y == 250 && Math.Abs(b.X) == 50 : (Func<CrackBar, bool>)(b => b.Y == -250 && Math.Abs(b.X) == 50);
            return InnerBoxBars.Where(inBand).Max(b => 200000 * plane.GetStrain(b.X, b.Y));
        }

        /// <summary>Hand calculation of the band width with the given h − x and k2 (EN 7.3.4 with sr,max = 1.3 (h − x); NTC C4.1.2.2.4.5 with Δsm = max(near; 0.75 (h − x))).</summary>
        private static double HollowBandHand(bool ntc, double sigma, double area, int count, double depth, double k2)
        {
            double rho = count * Math.PI * 100 / area, strain = Math.Max(.6 * sigma / 200000, (sigma - .4 * 2.9 / rho * (1 + 200000 / 33000.0 * rho)) / 200000);
            return ntc ? 1.7 * Math.Max((3.4 * 40 + .8 * k2 * .425 * 20 / rho) / 1.7, .75 * depth) * strain : 1.3 * depth * strain;
        }

        // Geometries of (i) and (j): the box 400×600 of (h); a square box 400×400 with a hole 200×200 and 2Ø20 at 150 mm from the axes on each wall
        // (x = ±150, y = ±50 and y = ±150, x = ±50); a ring Ø1000 with a hole Ø600 (64-gons with a vertex at 0°), 16Ø20 at r = 440 and 8Ø20 at r = 340
        // (band 300 ≤ r ≤ 400). Box bands: hc,eff = min[2.5 · 50; 100/2] = 50 mm, c = 40 mm. Spacing assigned 300 mm everywhere.
        private static readonly CrackSectionGeometry RectangularBox = new CrackSectionGeometry(InnerBoxOutline, new[] { InnerBoxHole }, InnerBoxBars, CrackBarLayout.Rows);
        private static readonly CrackSectionGeometry SquareBox = new CrackSectionGeometry(
            new[] { new Point2d(-200, -200), new Point2d(200, -200), new Point2d(200, 200), new Point2d(-200, 200) },
            new[] { new[] { new Point2d(-100, -100), new Point2d(100, -100), new Point2d(100, 100), new Point2d(-100, 100) } },
            new[] { -50d, 50 }.SelectMany(t => new[] { new CrackBar(150, t, 20, Math.PI * 100), new CrackBar(-150, t, 20, Math.PI * 100), new CrackBar(t, 150, 20, Math.PI * 100),
                new CrackBar(t, -150, 20, Math.PI * 100) }).ToArray(), CrackBarLayout.Rows);
        private static Point2d[] Polygon64(double radius) => Enumerable.Range(0, 64).Select(k => new Point2d(radius * Math.Cos(2 * Math.PI * k / 64), radius * Math.Sin(2 * Math.PI * k / 64))).ToArray();
        private static readonly CrackSectionGeometry HollowRing = new CrackSectionGeometry(Polygon64(500), new[] { Polygon64(300) },
            Enumerable.Range(0, 16).Select(k => new CrackBar(440 * Math.Cos(2 * Math.PI * k / 16), 440 * Math.Sin(2 * Math.PI * k / 16), 20, Math.PI * 100))
                .Concat(Enumerable.Range(0, 8).Select(k => new CrackBar(340 * Math.Cos(2 * Math.PI * (k + .5) / 8), 340 * Math.Sin(2 * Math.PI * (k + .5) / 8), 20, Math.PI * 100))).ToArray(),
            CrackBarLayout.Ring);

        private static SectionCrackResult HollowSection(CrackSectionGeometry geometry, Standard standard, string exposure, StrainPlane plane) => SectionCrackCheck.Evaluate(
            new SectionCrackInput(standard, ServiceabilityCombination.QuasiPermanent, exposure, false, null, geometry, plane, geometry.Bars.Select(b => 200000 * plane.GetStrain(b.X, b.Y)),
                true, false, false, 200000, 33000, 2.9, false, true, 40, spacingOverride: 300));

        private static string[] Bands(CrackSectionGeometry geometry) => geometry.Circular ? new[] { "InnerRing" } : InnerWalls.Select(w => w.Item1).ToArray();

        /// <summary>h − x used in wk: NTC Δsm,far / 0.75, EN sr,max / 1.3 (spacing 300 mm &gt; 5 (c + Ø/2)).</summary>
        private static double UsedDepth(SectionCrackResult r, string band, bool ntc) => ntc ? BandEntry(r, band, "Δsm,far").Value / .75 : BandEntry(r, band, "sr,max").Value / 1.3;

        /// <summary>Vertices of the band of an inner wall of a box (hc,eff = 50 mm) or of the inner ring (r = 400 mm), the bars in it and its area.</summary>
        private static Tuple<Point2d[], CrackBar[], double> BandOf(CrackSectionGeometry geometry, string band)
        {
            if (geometry.Circular) return Tuple.Create(Polygon64(400), geometry.Bars.Where(b => CrackSectionGeometry.Hypot(b.X, b.Y) <= 400).ToArray(), double.NaN);
            var hole = geometry.Holes[0]; double hw = hole.Max(v => v.X), hh = hole.Max(v => v.Y);
            double s = band[band.Length - 2] == '+' ? 1 : -1; bool alongX = band.EndsWith("x");
            double a = alongX ? hw : hh, t = alongX ? hh : hw;
            var corners = new[] { a, a + 50 }.SelectMany(n => new[] { -t, t }.Select(m => alongX ? new Point2d(s * n, m) : new Point2d(m, s * n))).ToArray();
            var bars = geometry.Bars.Where(b => { double n = s * (alongX ? b.X : b.Y), m = alongX ? b.Y : b.X; return n > a && n <= a + 50 + 1e-8 && Math.Abs(m) <= t + 1e-8; }).ToArray();
            return Tuple.Create(corners, bars, 50 * 2 * t);
        }

        /// <summary>Height of the outline along the unit direction (qx, qy).</summary>
        private static double HeightAlong(CrackSectionGeometry geometry, double qx, double qy) => geometry.Outline.Max(v => qx * v.X + qy * v.Y) - geometry.Outline.Min(v => qx * v.X + qy * v.Y);

        private static readonly Tuple<string, CrackSectionGeometry, Tuple<double, double>[]>[] InnerSections =
        {
            Tuple.Create("box 400×600", RectangularBox, new[] { Tuple.Create(0d, 1d), Tuple.Create(1d, 0d), Tuple.Create(.6, .8) }),
            Tuple.Create("box 400×400", SquareBox, new[] { Tuple.Create(0d, 1d), Tuple.Create(.6, .8) }),
            Tuple.Create("ring Ø1000", HollowRing, new[] { Tuple.Create(1d, 0d), Tuple.Create(Math.Cos(Math.PI / 64), Math.Sin(Math.PI / 64)), Tuple.Create(.6, .8) }),
        };

        /// <summary>
        /// (i) Continuity of h − x of the inner bands where the neutral axis enters the section (ANTHEA R15, decision of the user of 7/10/2026: "Altezza lungo
        /// il gradiente", h − x = min[εmax/|∇ε|; h of the section along the gradient]). Box 400×600, square box 400×400 and ring Ø1000, gradient along an axis
        /// (straight bending) or oblique (biaxial bending), NTC and EN: ε = 1e-6 (q · p − c), with the neutral axis 1e-6 mm outside (c = min q · p − 1e-6,
        /// entirely tensile section) and 1e-6 mm inside (c = min q · p + 1e-6, bending). On both sides h − x of each band is εmax/|∇ε| of the band, within the
        /// height along the gradient, with no "h − x" entry, and h − x, k2 and wk of each band agree within 1e-6 relative. Inside, h − x = εmax/|∇ε| is the rule
        /// before 0.0.17.0 (unchanged with the neutral axis inside). The ring has the same behaviour. The intermediate rule of d2f81329 (height normal to the
        /// face in an entirely tensile section) jumped here: box 400×600 with the gradient along y and the neutral axis at y = 310 / 290, ±x bands 400 / 490 mm.
        /// </summary>
        [TestMethod]
        public void HollowSectionInnerBandDepthIsContinuousWhereTheNeutralAxisEnters()
        {
            int checks = 0;
            foreach (var section in InnerSections)
                foreach (var q in section.Item3)
                    foreach (var s in InnerStandards)
                    {
                        var geometry = section.Item2; double qx = q.Item1, qy = q.Item2, kappa = 1e-6;
                        double edge = geometry.Outline.Min(v => qx * v.X + qy * v.Y);
                        StrainPlane At(double offset) => new StrainPlane(kappa * qx, kappa * qy, new Point2d(qx * (edge + offset), qy * (edge + offset)), 0);
                        StrainPlane planeOut = At(-1e-6), planeIn = At(1e-6);
                        var outside = HollowSection(geometry, s.Item1, s.Item2, planeOut); var inside = HollowSection(geometry, s.Item1, s.Item2, planeIn);
                        string what = section.Item1 + " " + s.Item1.GetType().Name + " q = (" + qx.ToString("0.####", CultureInfo.InvariantCulture) + "; " + qy.ToString("0.####", CultureInfo.InvariantCulture) + ")";
                        Assert.IsFalse(outside.Regions.Any(g => g.Key == "TensileZone"), what + ": neutral axis outside, entirely tensile branch\n" + outside.Status);
                        Assert.IsTrue(inside.Regions.Any(g => g.Key == "TensileZone"), what + ": neutral axis inside, bending branch\n" + inside.Status);
                        double along = HeightAlong(geometry, qx, qy);
                        foreach (var band in Bands(geometry))
                        {
                            var info = BandOf(geometry, band);
                            double handOut = info.Item1.Max(v => planeOut.GetStrain(v.X, v.Y)) / kappa, handIn = info.Item1.Max(v => planeIn.GetStrain(v.X, v.Y)) / kappa;
                            double dOut = UsedDepth(outside, band, s.Item3), dIn = UsedDepth(inside, band, s.Item3);
                            Assert.IsNull(BandEntry(outside, band, "h − x"), what + " " + band + ": no bound just outside\n" + Trace(outside));
                            Assert.IsNull(BandEntry(inside, band, "h − x"), what + " " + band + ": no bound just inside\n" + Trace(inside));
                            Assert.IsTrue(handOut <= along && handIn <= along, what + " " + band + ": εmax/|∇ε| within the height along the gradient");
                            Assert.AreEqual(handOut, dOut, 1e-9 * handOut, what + " " + band + ": h − x = εmax/|∇ε| just outside");
                            Assert.AreEqual(handIn, dIn, 1e-9 * handIn, what + " " + band + ": h − x = εmax/|∇ε| just inside (rule before 0.0.17.0)");
                            Assert.AreEqual(dOut, dIn, 1e-6 * dOut, what + " " + band + ": h − x continuous");
                            double kOut = BandEntry(outside, band, "k2").Value, kIn = BandEntry(inside, band, "k2").Value;
                            Assert.AreEqual(kOut, kIn, 1e-6 * kOut, what + " " + band + ": k2 continuous");
                            double wOut = BandEntry(outside, band, "wk").Value, wIn = BandEntry(inside, band, "wk").Value;
                            Assert.AreEqual(wOut, wIn, 1e-6 * wOut, what + " " + band + ": wk continuous (" + wOut.ToString("R", CultureInfo.InvariantCulture) + " / " + wIn.ToString("R", CultureInfo.InvariantCulture) + ")");
                            if (!geometry.Circular)
                            {
                                double sigmaIn = info.Item2.Max(b => 200000 * planeIn.GetStrain(b.X, b.Y));
                                Assert.AreEqual(HollowBandHand(s.Item3, sigmaIn, info.Item3, info.Item2.Length, handIn, kIn), wIn, 1e-12, what + " " + band + ": wk by hand with h − x = εmax/|∇ε|");
                            }
                            checks++;
                        }
                    }
            Assert.AreEqual(2 * (3 * 4 + 2 * 4 + 3 * 1), checks);
        }

        /// <summary>
        /// (j) Residual jump of the rule of (i) at the uniform-tension threshold |∇ε| h = 1e-4 εmax, declared and pinned: below it uniform tension, h − x = h of
        /// the section normal to the face (box: 400 / 600 for ±x / ±y, square box 400, ring 1000 = diameter), k2 = 1; above it min[εmax/|∇ε|; h along the
        /// gradient] = h along the gradient (εmax/|∇ε| ≈ 1e4 h). ε = 5e-4 + χ (q · p) at the centroid, |∇ε| h / εmax = 0.99e-4 and 1.01e-4, NTC and EN.
        /// Jumps (far crack spacing and wk in the same ratio, k2 and σs move by 1e-4 at most): box 400×600 with the gradient along y, ±x bands 400 → 600 (+50 %);
        /// along x, ±y bands 600 → 400 (−33 %); oblique (0.6; 0.8), every band → 720; square box along (0.6; 0.8), 400 → 560 (+40 %); ring 1000 → 1000 along a
        /// vertex and 1000 cos(π/64) = 998.8 between two (polygon only). No jump where the two heights coincide (box bands parallel to the gradient, square box
        /// in straight bending).
        /// </summary>
        [TestMethod]
        public void HollowSectionInnerBandDepthJumpsAtTheUniformThresholdWhereTheHeightsDiffer()
        {
            const double eps0 = 5e-4;
            int jumps = 0, checks = 0;
            foreach (var section in InnerSections.Concat(new[] { Tuple.Create("box 400×400 straight", SquareBox, new[] { Tuple.Create(1d, 0d) }) }))
                foreach (var q in section.Item3)
                    foreach (var s in InnerStandards)
                    {
                        var geometry = section.Item2; double qx = q.Item1, qy = q.Item2, along = HeightAlong(geometry, qx, qy);
                        SectionCrackResult At(double ratio, out StrainPlane plane)
                        {
                            // ratio = χ h / εmax with εmax = eps0 + χ h / 2 (sections symmetric about the centroid).
                            double chi = ratio * eps0 / (along * (1 - ratio / 2));
                            plane = new StrainPlane(chi * qx, chi * qy, new Point2d(0, 0), eps0);
                            return HollowSection(geometry, s.Item1, s.Item2, plane);
                        }
                        var below = At(.99e-4, out var planeBelow); var above = At(1.01e-4, out var planeAbove);
                        string what = section.Item1 + " " + s.Item1.GetType().Name + " q = (" + qx.ToString("0.####", CultureInfo.InvariantCulture) + "; " + qy.ToString("0.####", CultureInfo.InvariantCulture) + ")";
                        Assert.IsFalse(below.Regions.Any(g => g.Key == "TensileZone") || above.Regions.Any(g => g.Key == "TensileZone"), what + ": entirely tensile branch");
                        foreach (var band in Bands(geometry))
                        {
                            double normal = geometry.Circular ? 1000 : band.EndsWith("x") ? geometry.Width : geometry.Height;
                            var d0 = BandEntry(below, band, "h − x"); var d1 = BandEntry(above, band, "h − x");
                            Assert.AreEqual(normal, d0.Value, 1e-9, what + " " + band + " below"); StringAssert.StartsWith(d0.Expression, "uniform tension", what);
                            Assert.AreEqual(along, d1.Value, 1e-9, what + " " + band + " above: height along the gradient\n" + Trace(above));
                            StringAssert.StartsWith(d1.Expression, "min[εmax/|∇ε| = ", what); StringAssert.EndsWith(d1.Expression, " mm; h along the gradient]: neutral axis outside the section, x = 0 (EN 1992-1-1 7.3.4(3), eq. (7.14))", what);
                            Assert.AreEqual(normal, UsedDepth(below, band, s.Item3), 1e-9, what + " " + band + ": h − x used below");
                            Assert.AreEqual(along, UsedDepth(above, band, s.Item3), 1e-9, what + " " + band + ": h − x used above");
                            double k0 = BandEntry(below, band, "k2").Value, k1 = BandEntry(above, band, "k2").Value;
                            Assert.AreEqual(1, k0, what); Assert.IsTrue(k1 < 1 && k1 >= 1 - 1.01e-4 / 2, what + ": k2 = " + k1);
                            double w0 = BandEntry(below, band, "wk").Value, w1 = BandEntry(above, band, "wk").Value;
                            if (!geometry.Circular)
                            {
                                var info = BandOf(geometry, band);
                                Assert.AreEqual(HollowBandHand(s.Item3, info.Item2.Max(b => 200000 * planeBelow.GetStrain(b.X, b.Y)), info.Item3, info.Item2.Length, normal, 1), w0, 1e-12, what + " " + band + " wk by hand below");
                                Assert.AreEqual(HollowBandHand(s.Item3, info.Item2.Max(b => 200000 * planeAbove.GetStrain(b.X, b.Y)), info.Item3, info.Item2.Length, along, k1), w1, 1e-12, what + " " + band + " wk by hand above");
                            }
                            // The far crack spacing governs (NTC Δsm,far = 0.75 (h − x), EN sr,max = 1.3 (h − x)): wk jumps by the ratio of the heights.
                            Assert.AreEqual(along / normal, w1 / w0, 2e-4, what + " " + band + ": wk " + w0.ToString("R", CultureInfo.InvariantCulture) + " → " + w1.ToString("R", CultureInfo.InvariantCulture));
                            if (Math.Abs(along - normal) > 1e-9) jumps++;
                            checks++;
                        }
                    }
            // Bands with a jump: box 400×600 along y (±x) and along x (±y), oblique (4), square box oblique (4), ring between two vertices and oblique, each for NTC and EN.
            Assert.AreEqual(2 * (3 * 4 + 2 * 4 + 3 * 1 + 4), checks);
            Assert.AreEqual(2 * (2 + 2 + 4 + 4 + 2), jumps);
        }

        private static CrackProfile Profile(string name) => CrackProfiles.Resolve(ServiceabilityMigrationTests.Standard(name));

        [TestMethod]
        public void LegacyCrackWidthsAndRequirementsAreReproduced()
        {
            var rows = Rows("crack-scalar-legacy.csv", header: false);
            int widths = 0, requirements = 0;
            foreach (var row in rows)
            {
                var c = row.Split(';');
                if (c[0].StartsWith("W"))
                {
                    var input = new CrackWidthInput(D(c[2]), D(c[3]), D(c[4]), D(c[5]), D(c[6]), D(c[7]), D(c[8]), D(c[9]), D(c[10]), bool.Parse(c[11]), bool.Parse(c[12]), D(c[13]));
                    if (c[14] != "ok") { Assert.AreEqual("error:ArgumentException", c[14], c[0]); Assert.ThrowsException<ArgumentException>(() => CrackWidthCalculator.Width(Profile(c[1]), input), c[0]); }
                    else Close(D(c[15]), CrackWidthCalculator.Width(Profile(c[1]), input), c[0] + " " + c[1]);
                    widths++; continue;
                }
                var exposure = Exposure(c[3]);
                var r = CrackRequirements.For(Profile(c[1]), Combination(c[2]), exposure, bool.Parse(c[4]), N(c[5]));
                string kind = c[6]; string what = string.Join(" ", c[1], c[2], c[3], c[4], c[5], kind);
                var expected = kind.StartsWith("Non richiesta") ? CrackCriterion.NotRequired : kind.StartsWith("Selezionare la classe") ? CrackCriterion.ExposureRequired
                    : kind.StartsWith("Selezionare esposizione") ? (exposure == null ? CrackCriterion.ExposureRequired : CrackCriterion.DesignLimitRequired)
                    : kind.StartsWith("Selezionare wlim") ? CrackCriterion.DesignLimitRequired : kind == "Decompressione" ? CrackCriterion.Decompression
                    : kind == "Formazione fessure" ? CrackCriterion.CrackFormation : kind == "Apertura fessure" ? CrackCriterion.CrackWidth : (CrackCriterion)(-1);
                Assert.AreEqual(expected, r.Criterion, what);
                var limit = N(c[7]);
                Assert.AreEqual(limit.HasValue, r.Limit.HasValue, what); if (limit.HasValue) Close(limit.Value, r.Limit.Value, what);
                requirements++;
            }
            Assert.AreEqual(1400, widths); Assert.AreEqual(7 * 3 * 19 * 2 * 2, requirements);
        }

        /// <summary>EN 1992-1-1 7.3.4 by hand: σs = 250 MPa, Ø20, c = 40 mm, Ac,eff = 300·110, As = 942.48 mm², Ecm = 33 GPa, fctm = 2.9 MPa, long term, ribbed, k2 = 0.5.</summary>
        [TestMethod]
        public void Eurocode2CrackWidthMatchesHandCalculation()
        {
            double rho = 942.48 / 33000, alphaE = 200000 / 33000.0;
            double strain = Math.Max(.6 * 250 / 200000, (250 - .4 * 2.9 / rho * (1 + alphaE * rho)) / 200000); // 0.0010117 > 0.00075
            double sr = 3.4 * 40 + .8 * .5 * .425 * 20 / rho;                                                     // 255.1 mm
            var input = new CrackWidthInput(250, 200000, 33000, 2.9, rho, 20, 40, 100, 330, false, true, .5);
            double wk = CrackWidthCalculator.Width(CrackProfile.EN1992p11, input);
            Assert.AreEqual(sr * strain, wk, 1e-12); Assert.AreEqual(.258, wk, 5e-4);
            // Sparse bars (s > 5 (c + Ø/2) = 250 mm): sr,max = 1.3 (h − x).
            Assert.AreEqual(1.3 * 330 * strain, CrackWidthCalculator.Width(CrackProfile.EN1992p11, new CrackWidthInput(250, 200000, 33000, 2.9, rho, 20, 40, 300, 330, false, true, .5)), 1e-12);
            // DS: k3 = 3.4 (25/c)^(2/3) reduces the cover term for c > 25 mm.
            Assert.IsTrue(CrackWidthCalculator.Width(CrackProfile.DsEN1992p11, input) < wk);
            // DIN: kt = 0.4 and sr,max ≤ σs Ø/(3.6 fct); MC2010 and DIN need ribbed bars.
            Assert.AreEqual(Math.Min(20 / (3.6 * rho), 250 * 20 / (3.6 * 2.9)) * strain, CrackWidthCalculator.Width(CrackProfile.DinEN1992p11, input), 1e-12);
            Assert.ThrowsException<ArgumentException>(() => CrackWidthCalculator.Width(CrackProfile.ModelCode2010, new CrackWidthInput(250, 200000, 33000, 2.9, rho, 20, 40, 100, 330, false, false, .5)));
            // CNR-DT 200 without FRP data uses the NTC member formula.
            Assert.AreEqual(CrackWidthCalculator.Width(CrackProfile.Ntc2018, input), CrackWidthCalculator.Width(CrackProfile.CnrDT200, input));
            // k2 from the bar stresses (legacy NTC / CNR-DT 200 rule before D7-b, SectionCrackInput.NtcK2FromCompressedBars): one compressed bar means bending.
            Assert.AreEqual(.5, CrackWidthCalculator.K2(new[] { 120.0, -10 })); Assert.AreEqual(1, CrackWidthCalculator.K2(new[] { 120.0, 0 }));
        }

        /// <summary>NTC 2018 Tab. 4.1.IV: limits by environment, combination and reinforcement sensitivity.</summary>
        [TestMethod]
        public void NtcRequirementsFollowTable41IV()
        {
            CrackRequirement R(string exposure, ServiceabilityCombination combination, bool sensitive) => CrackRequirements.For(CrackProfile.Ntc2018, combination, exposure, sensitive);
            var frequent = ServiceabilityCombination.Frequent; var qp = ServiceabilityCombination.QuasiPermanent;
            Assert.AreEqual(.4, R("XC1", frequent, false).Limit); Assert.AreEqual(.3, R("XC1", qp, false).Limit);   // ordinary
            Assert.AreEqual(.3, R("XD1", frequent, false).Limit); Assert.AreEqual(.2, R("XD1", qp, false).Limit);   // aggressive
            Assert.AreEqual(.2, R("XS3", frequent, false).Limit); Assert.AreEqual(.2, R("XS3", qp, false).Limit);   // very aggressive
            Assert.AreEqual(.3, R("XC1", frequent, true).Limit); Assert.AreEqual(.2, R("XC1", qp, true).Limit);
            Assert.AreEqual(.2, R("XD1", frequent, true).Limit); Assert.AreEqual(CrackCriterion.Decompression, R("XD1", qp, true).Criterion);
            Assert.AreEqual(CrackCriterion.CrackFormation, R("XS3", frequent, true).Criterion); Assert.AreEqual(CrackCriterion.Decompression, R("XS3", qp, true).Criterion);
            Assert.AreEqual(CrackCriterion.NotRequired, R("XC1", ServiceabilityCombination.Characteristic, false).Criterion);
            Assert.AreEqual(CrackCriterion.ExposureRequired, R(null, qp, false).Criterion);
            // Eurocode family: quasi-permanent only; MC2010 needs a design limit; a design limit overrides the table.
            Assert.AreEqual(ServiceabilityCombination.QuasiPermanent, CrackRequirements.For(CrackProfile.EN1992p11, frequent, "XC3", false).RequiredCombination);
            Assert.AreEqual(CrackCriterion.DesignLimitRequired, CrackRequirements.For(CrackProfile.ModelCode2010, qp, "XC3", false).Criterion);
            Assert.AreEqual(.25, CrackRequirements.For(CrackProfile.ModelCode2010, qp, "XC3", false, .25).Limit);
            Assert.AreEqual(ServiceabilityCombination.Frequent, CrackRequirements.For(CrackProfile.NsEN1992p11, qp, "XD3", false).RequiredCombination);
            Assert.ThrowsException<ArgumentException>(() => CrackRequirements.For(CrackProfile.EN1992p11, qp, "XY9", false));
        }

        private sealed class CustomAnnex : StandardEN1992p11 { }

        [TestMethod]
        public void ProfilesAreResolvedByExactTypeWithoutFallback()
        {
            Assert.IsFalse(CrackProfiles.TryResolve(new CustomAnnex(), out _));
            StringAssert.Contains(CrackProfiles.NotApplicableReason(new StandardCSTR34()), "CS-TR34");
            StringAssert.Contains(CrackProfiles.NotSupportedReason(new StandardCNR204()), "CNR-DT 204");
            StringAssert.Contains(Assert.ThrowsException<NotSupportedException>(() => CrackProfiles.Resolve(new StandardACI318p19())).Message, "future implementation");
            Assert.IsTrue(CrackProfiles.TryResolve(new StandardCNR200(), out var frp) && frp == CrackProfile.CnrDT200);
        }

        /// <summary>Geometry helpers: half-plane clipping, spacing along rows and rings, effective depth.</summary>
        [TestMethod]
        public void GeometryHelpersWork()
        {
            var square = new[] { new Point2d(-200, -200), new Point2d(200, -200), new Point2d(200, 200), new Point2d(-200, 200) };
            var bars = new[] { new CrackBar(-150, -150, 20, 314.16), new CrackBar(0, -150, 20, 314.16), new CrackBar(150, -150, 20, 314.16), new CrackBar(-150, 150, 16, 201.06) };
            var g = new CrackSectionGeometry(square, null, bars, CrackBarLayout.Rows);
            var region = g.Region("TensileZone", 0, -1, 100, new[] { 0, 1, 2 });
            Assert.AreEqual(400 * 100, region.Area, 1e-9); Assert.AreEqual(3 * 314.16, region.SteelArea, 1e-9);
            Assert.AreEqual(150, g.MaximumSpacing(new[] { 0, 1, 2 }).Value, 1e-12);
            Assert.AreEqual(300, g.MaximumSpacing(new[] { 0, 3 }).Value, 1e-12, "column x = −150");
            Assert.IsNull(g.MaximumSpacing(new[] { 1 }));
            // hc,eff = min[2.5 (h − d); (h − x)/3; h/2] = min[125; 100; 200].
            Assert.AreEqual(100, g.EffectiveDepth(CrackProfile.EN1992p11, 0, -1, 200, 400, 50, 300, false, 40), 1e-12);
            Assert.AreEqual(125, g.EffectiveDepth(CrackProfile.EN1992p11, 0, -1, 200, 400, 50, 300, true, 40), 1e-12);
            // DIN: (2 + 0.1·400/50) (h − d) = 2.8·50 = 140 ≤ h/2, then (h − x)/3 = 100 since 100 ≥ c + 20.
            Assert.AreEqual(100, g.EffectiveDepth(CrackProfile.DinEN1992p11, 0, -1, 200, 400, 50, 300, false, 40), 1e-12);
            Assert.AreEqual(140, g.EffectiveDepth(CrackProfile.DinEN1992p11, 0, -1, 200, 400, 50, 300, false, 90), 1e-12);
            // DS: band centred on the bars (rectangle: 2 (h − d)).
            Assert.AreEqual(100, g.EffectiveDepth(CrackProfile.DsEN1992p11, 0, -1, 200, 400, 50, 300, false, 40), 1e-9);
            // Ring: arc between adjacent bars.
            var circle = Enumerable.Range(0, 32).Select(i => new Point2d(500 * Math.Cos(2 * Math.PI * i / 32), 500 * Math.Sin(2 * Math.PI * i / 32))).ToArray();
            var ring = Enumerable.Range(0, 8).Select(i => new CrackBar(400 * Math.Cos(2 * Math.PI * i / 8), 400 * Math.Sin(2 * Math.PI * i / 8), 20, 314.16)).ToArray();
            var c = new CrackSectionGeometry(circle, null, ring, CrackBarLayout.Ring);
            Assert.AreEqual(400 * 2 * Math.PI / 8, c.MaximumSpacing(new[] { 0, 1, 2 }).Value, 1e-9);
        }

        // ---- 0.0.18.0 (ANTHEA F2.7 K2): SectionCrackOptions (ValidateAtUse, EffectiveDepthCover) and the codes of the refusals in Exception.Data.

        private static readonly SectionCrackOptions AtUse = SectionCrackOptions.Default.WithValidateAtUse(true);
        // ε = χy (y − y0) + ε0 on the 300×500 of (a): bent with the neutral axis at y = 50 (h − x = 300, σs = 200 MPa at y = −200), uniform compression,
        // compressed down to ε = 0 at the bottom edge, entirely tensile (2e-4 top, 7e-4 bottom), neutral axis in the bottom cover, tensile bars outside Ac,eff.
        private static readonly StrainPlane BentAt50 = new StrainPlane(0, -4e-6, new Point2d(0, 50), 0);
        private static readonly StrainPlane UniformCompression = new StrainPlane(0, 0, new Point2d(0, 0), -5e-4);
        private static readonly StrainPlane CompressedToZero = new StrainPlane(0, -1e-6, new Point2d(0, -250), 0);
        private static readonly StrainPlane EccentricTension = new StrainPlane(0, -1e-6, new Point2d(0, 250), 2e-4);
        private static readonly StrainPlane AxisInTheCover = new StrainPlane(0, -1e-5, new Point2d(0, -230), 0);
        private static readonly StrainPlane AxisAboveTheBars = new StrainPlane(0, -1e-5, new Point2d(0, -150), 0);
        private static readonly ServiceabilityCombination[] AllCombinations =
            { ServiceabilityCombination.Characteristic, ServiceabilityCombination.Frequent, ServiceabilityCombination.QuasiPermanent };
        private const string InvalidWidthMessage = "Cracking: invalid crack-width parameters.";
        private const string RibbedBarsMessage = "Cracking: the Model Code 2010 / DIN crack model is implemented for ribbed bars.";

        /// <summary>A state on the 300×500 of (a): Es 200 GPa, Ecm 33 GPa, fctm 2.9 MPa, long term, linear cracked analysis, σs = Es ε unless given.</summary>
        private sealed class OptionCase
        {
            public string Standard = "NTC 2018"; public ServiceabilityCombination Combination = ServiceabilityCombination.QuasiPermanent; public string Exposure = "XC1";
            public bool Sensitive; public double? DesignLimit; public CrackBar[] Bars = Bottom; public StrainPlane Plane = BentAt50; public double[] Stresses;
            public double NominalCover = 40; public double? CoverOverride, SpacingOverride; public Func<double> Uncracked; public bool LegacyK2, Ribbed = true;

            public OptionCase With(Action<OptionCase> change) { var copy = (OptionCase)MemberwiseClone(); change(copy); return copy; }
            private CrackSectionGeometry Geometry() => new CrackSectionGeometry(Rectangle300x500, null, Bars, CrackBarLayout.Rows);
            private double[] BarStresses() => Stresses ?? Bars.Select(b => 200000 * Plane.GetStrain(b.X, b.Y)).ToArray();

            /// <summary>Constructor with the options (0.0.18.0).</summary>
            public SectionCrackInput Input(SectionCrackOptions options) => new SectionCrackInput(ServiceabilityMigrationTests.Standard(Standard), Combination, Exposure, Sensitive,
                DesignLimit, Geometry(), Plane, BarStresses(), true, false, false, 200000, 33000, 2.9, false, Ribbed, NominalCover, CoverOverride, SpacingOverride, Uncracked, LegacyK2,
                options);

            /// <summary>Constructor of 0.0.17.0, the one of ModelChecker.</summary>
            public SectionCrackInput WithoutOptions() => new SectionCrackInput(ServiceabilityMigrationTests.Standard(Standard), Combination, Exposure, Sensitive,
                DesignLimit, Geometry(), Plane, BarStresses(), true, false, false, 200000, 33000, 2.9, false, Ribbed, NominalCover, CoverOverride, SpacingOverride, Uncracked, LegacyK2);

            public SectionCrackResult Evaluate(SectionCrackOptions options) => SectionCrackCheck.Evaluate(Input(options));
        }

        private static void AssertSameResult(SectionCrackResult expected, SectionCrackResult actual, string what)
        {
            AssertSame(expected, actual, what);
            Assert.AreEqual(expected.Verdict, actual.Verdict, what + " verdict"); Assert.AreEqual(expected.Limit, actual.Limit, what + " wlim");
            Assert.AreEqual(expected.EffectiveArea, actual.EffectiveArea, what + " Ac,eff"); Assert.AreEqual(expected.BarSpacing, actual.BarSpacing, what + " s");
            Assert.AreEqual(string.Join(",", expected.Regions.Select(g => g.Key + ":" + g.Width)), string.Join(",", actual.Regions.Select(g => g.Key + ":" + g.Width)), what + " regions");
        }

        private static string ParamOf(Func<object> call, string what) => Assert.ThrowsException<ArgumentOutOfRangeException>(call, what).ParamName;

        /// <summary>A refusal with a code: exact type ArgumentException, the message of 0.0.17.0, the code under CrackRejection.DataKey, no parameter name.</summary>
        private static ArgumentException Refusal(Func<object> call, string code, string message, string what)
        {
            var e = Assert.ThrowsException<ArgumentException>(call, what);
            Assert.AreEqual(message, e.Message, what + " message"); Assert.AreEqual(code, CrackRejection.CodeOf(e), what + " code");
            Assert.AreEqual(code, e.Data[CrackRejection.DataKey], what + " Data"); Assert.IsNull(e.ParamName, what + " ParamName");
            return e;
        }

        /// <summary>
        /// Default options are the behaviour of 0.0.17.0 (the contract of ModelCheckerContractTests is captured with the constructor without options): same
        /// results through both constructors on every branch, same refusals (type, message, parameter, place), same requirement table. The With methods copy.
        /// </summary>
        [TestMethod]
        public void DefaultOptionsKeepTheBehaviourOf0017()
        {
            Assert.IsFalse(SectionCrackOptions.Default.ValidateAtUse); Assert.IsNull(SectionCrackOptions.Default.EffectiveDepthCover);
            var changed = SectionCrackOptions.Default.WithValidateAtUse(true).WithEffectiveDepthCover(10);
            Assert.IsTrue(changed.ValidateAtUse); Assert.AreEqual(10, changed.EffectiveDepthCover.Value);
            Assert.IsFalse(changed.WithValidateAtUse(false).ValidateAtUse); Assert.IsTrue(changed.ValidateAtUse); Assert.IsNull(changed.WithEffectiveDepthCover(null).EffectiveDepthCover);
            Assert.IsFalse(SectionCrackOptions.Default.ValidateAtUse); Assert.IsNull(SectionCrackOptions.Default.EffectiveDepthCover);
            var basic = new OptionCase();
            Assert.AreSame(SectionCrackOptions.Default, basic.WithoutOptions().Options);
            Assert.AreSame(AtUse, basic.Input(AtUse).Options);
            Assert.AreEqual("options", Assert.ThrowsException<ArgumentNullException>(() => basic.Input(null)).ParamName);
            var states = new[]
            {
                basic, basic.With(x => x.Bars = Doubly), basic.With(x => x.Plane = UniformCompression), basic.With(x => { x.Bars = Doubly; x.Plane = EccentricTension; }),
                basic.With(x => { x.Bars = Doubly; x.Plane = AxisInTheCover; }), basic.With(x => x.Plane = AxisAboveTheBars), basic.With(x => { x.Bars = Doubly; x.CoverOverride = 30; x.SpacingOverride = 150; }),
                basic.With(x => { x.Exposure = "XD1"; x.Sensitive = true; x.Uncracked = () => -.5; }), basic.With(x => { x.Exposure = "XS3"; x.Sensitive = true; x.Combination = ServiceabilityCombination.Frequent; x.Uncracked = () => 3; }),
                basic.With(x => x.LegacyK2 = true), basic.With(x => x.Combination = ServiceabilityCombination.Characteristic),
                basic.With(x => { x.Standard = "DIN EN 1992-1-1"; x.Exposure = "XC3"; x.NominalCover = 90; }), basic.With(x => { x.Standard = "DIN EN 1992-1-1"; x.Exposure = "XC3"; x.CoverOverride = 90; }),
                basic.With(x => { x.Standard = "EN 1992-1-1"; x.Exposure = "XC3"; x.DesignLimit = .25; }), basic.With(x => { x.Standard = "DS EN 1992-1-1"; x.Exposure = "XC3"; x.Bars = Doubly; x.Plane = EccentricTension; }),
                basic.With(x => { x.Standard = "Model Code 2010"; x.Exposure = "XC3"; }),
            };
            for (int i = 0; i < states.Length; i++)
                AssertSameResult(SectionCrackCheck.Evaluate(states[i].WithoutOptions()), states[i].Evaluate(SectionCrackOptions.Default), "state " + i);
            // Refusals of the constructor stay in the constructor, those of Evaluate in Evaluate.
            foreach (var bad in new Action<OptionCase>[] { x => x.NominalCover = double.NaN, x => x.CoverOverride = -1, x => x.SpacingOverride = 0, x => x.Stresses = new[] { 1.0 } })
            {
                var c = basic.With(bad);
                Exception expected = null, actual = null;
                try { c.WithoutOptions(); } catch (Exception e) { expected = e; }
                try { c.Input(SectionCrackOptions.Default); } catch (Exception e) { actual = e; }
                Assert.IsNotNull(expected); Assert.IsNotNull(actual); Assert.AreEqual(expected.GetType(), actual.GetType()); Assert.AreEqual(expected.Message, actual.Message);
                Assert.AreEqual(((ArgumentException)expected).ParamName, ((ArgumentException)actual).ParamName);
            }
            foreach (var bad in new Action<OptionCase>[] { x => x.DesignLimit = double.NaN, x => { x.Plane = UniformCompression; x.Stresses = new[] { double.NaN, 0, 0 }; },
                x => { x.Exposure = "XD1"; x.Sensitive = true; }, x => { x.Standard = "DIN EN 1992-1-1"; x.Exposure = "XC3"; x.Ribbed = false; } })
            {
                var c = basic.With(bad);
                var before = c.WithoutOptions(); var after = c.Input(SectionCrackOptions.Default);
                Exception expected = null, actual = null;
                try { SectionCrackCheck.Evaluate(before); } catch (Exception e) { expected = e; }
                try { SectionCrackCheck.Evaluate(after); } catch (Exception e) { actual = e; }
                Assert.IsNotNull(expected); Assert.IsNotNull(actual); Assert.AreEqual(expected.GetType(), actual.GetType()); Assert.AreEqual(expected.Message, actual.Message);
                Assert.AreEqual(CrackRejection.CodeOf(expected), CrackRejection.CodeOf(actual));
            }
            // The overload of CrackRequirements.For with the default options gives the table of the overload without options.
            int requirements = 0;
            foreach (CrackProfile profile in Enum.GetValues(typeof(CrackProfile)))
                foreach (var combination in AllCombinations)
                    foreach (var exposure in new string[] { null }.Concat(CrackRequirements.Exposures))
                        foreach (bool sensitive in new[] { false, true })
                            foreach (var limit in new double?[] { null, .25 })
                            {
                                var a = CrackRequirements.For(profile, combination, exposure, sensitive, limit);
                                var b = CrackRequirements.For(profile, combination, exposure, sensitive, limit, SectionCrackOptions.Default);
                                var c = CrackRequirements.For(profile, combination, exposure, sensitive, limit, AtUse);
                                string what = string.Join(" ", profile, combination, exposure, sensitive, limit);
                                foreach (var r in new[] { b, c })
                                {
                                    Assert.AreEqual(a.Criterion, r.Criterion, what); Assert.AreEqual(a.Limit, r.Limit, what); Assert.AreEqual(a.RequiredCombination, r.RequiredCombination, what);
                                }
                                requirements++;
                            }
            Assert.AreEqual(8 * 3 * 19 * 2 * 2, requirements);
        }

        /// <summary>
        /// ValidateAtUse, design limit (ANTHEA ConcreteCodeChecks.CrackRequirement reads limite_fessure only for the Eurocode family in the required combination):
        /// NTC 2018 and UNI ignore it, also when it is 0, negative, NaN or infinite; a combination where the check is not required gives NotRequired; where it
        /// enters (EN, MC2010 and NS in the required combination) it is refused with the ArgumentOutOfRangeException of 0.0.17.0.
        /// </summary>
        [TestMethod]
        public void ValidateAtUseChecksTheDesignLimitOnlyWhereItEnters()
        {
            var invalid = new[] { 0, -.2, double.NaN, double.PositiveInfinity };
            int checks = 0;
            foreach (var standard in new[] { "NTC 2018", "UNI EN 1992-1-1" })
                foreach (var combination in AllCombinations)
                {
                    var clean = new OptionCase { Standard = standard, Combination = combination };
                    foreach (double bad in invalid)
                    {
                        var c = clean.With(x => x.DesignLimit = bad); string what = standard + " " + combination + " wlim " + bad;
                        AssertSameResult(clean.Evaluate(AtUse), c.Evaluate(AtUse), what);
                        Assert.AreEqual("designLimit", ParamOf(() => c.Evaluate(SectionCrackOptions.Default), what + ", default options"));
                        checks++;
                    }
                }
            Assert.AreEqual(2 * 3 * 4, checks);
            var en = new OptionCase { Standard = "EN 1992-1-1", Exposure = "XC3", DesignLimit = double.NaN };
            var frequent = en.With(x => x.Combination = ServiceabilityCombination.Frequent).Evaluate(AtUse);
            Assert.AreEqual(CrackOutcome.NotRequired, frequent.Outcome); Assert.AreEqual(CrackVerdict.NotRequired, frequent.Verdict);
            Assert.AreEqual(ServiceabilityCombination.QuasiPermanent, frequent.Requirement.RequiredCombination.Value);
            Assert.AreEqual("Not required: check the QuasiPermanent combination", frequent.Status); Assert.IsNull(frequent.Limit);
            Assert.AreEqual(CrackOutcome.NotRequired, en.With(x => x.Combination = ServiceabilityCombination.Characteristic).Evaluate(AtUse).Outcome);
            foreach (double bad in invalid)
            {
                Assert.AreEqual("designLimit", ParamOf(() => en.With(x => x.DesignLimit = bad).Evaluate(AtUse), "EN quasi-permanent " + bad));
                Assert.AreEqual("designLimit", ParamOf(() => en.With(x => { x.DesignLimit = bad; x.Combination = ServiceabilityCombination.Frequent; }).Evaluate(SectionCrackOptions.Default), "EN frequent, default"));
            }
            var mc = en.With(x => x.Standard = "Model Code 2010");
            Assert.AreEqual(CrackOutcome.NotRequired, mc.With(x => x.Combination = ServiceabilityCombination.Frequent).Evaluate(AtUse).Outcome);
            Assert.AreEqual("designLimit", ParamOf(() => mc.Evaluate(AtUse), "MC2010 quasi-permanent"));
            // NS NA: XD3 is checked in the frequent combination.
            var ns = en.With(x => { x.Standard = "NS EN 1992-1-1"; x.Exposure = "XD3"; });
            Assert.AreEqual(CrackOutcome.NotRequired, ns.Evaluate(AtUse).Outcome);
            Assert.AreEqual("designLimit", ParamOf(() => ns.With(x => x.Combination = ServiceabilityCombination.Frequent).Evaluate(AtUse), "NS XD3 frequent"));
            // A valid design limit is used as before; MC2010 without one still asks for it.
            var valid = en.With(x => x.DesignLimit = .25);
            AssertSameResult(valid.Evaluate(SectionCrackOptions.Default), valid.Evaluate(AtUse), "EN wlim 0.25");
            Assert.AreEqual(.25, valid.Evaluate(AtUse).Limit.Value);
            Assert.AreEqual(CrackOutcome.MissingDesignLimit, mc.With(x => x.DesignLimit = null).Evaluate(AtUse).Outcome);
            // CrackRequirements.For with the options; the exposure is still validated up front.
            var qp = ServiceabilityCombination.QuasiPermanent;
            Assert.AreEqual(.3, CrackRequirements.For(CrackProfile.Ntc2018, qp, "XC1", false, double.NaN, AtUse).Limit.Value);
            Assert.AreEqual(CrackCriterion.Decompression, CrackRequirements.For(CrackProfile.UniEN1992p11, qp, "XD1", true, -1, AtUse).Criterion);
            Assert.AreEqual(CrackCriterion.NotRequired, CrackRequirements.For(CrackProfile.EN1992p11, ServiceabilityCombination.Frequent, "XC3", false, double.NaN, AtUse).Criterion);
            Assert.AreEqual("designLimit", ParamOf(() => CrackRequirements.For(CrackProfile.EN1992p11, qp, "XC3", false, 0, AtUse), "For EN"));
            Assert.AreEqual("designLimit", ParamOf(() => CrackRequirements.For(CrackProfile.Ntc2018, qp, "XC1", false, 0, SectionCrackOptions.Default), "For NTC default"));
            Assert.AreEqual("Unknown exposure class: XY9", Assert.ThrowsException<ArgumentException>(() => CrackRequirements.For(CrackProfile.Ntc2018, qp, "XY9", false, null, AtUse)).Message);
            Assert.AreEqual("options", Assert.ThrowsException<ArgumentNullException>(() => CrackRequirements.For(CrackProfile.Ntc2018, qp, "XC1", false, null, null)).ParamName);
        }

        /// <summary>
        /// ValidateAtUse, nominal cover and overrides (ANTHEA Required("copriferro_fessure"), Required("spaziatura_fessure", strict) and Required("cover_mm") where
        /// the branch reads them): decompression, crack formation, a combination not required, the neutral axis in the cover, tensile bars outside Ac,eff and the
        /// entirely compressed section do not read them; the width formula refuses them where it reads them, with the parameter of 0.0.17.0, cover before spacing.
        /// The states are NTC 2018; the DIN condition of hc,eff, which reads the nominal cover before the branch of the tensile bars outside Ac,eff, is in
        /// EffectiveDepthCoverChangesOnlyTheDinCondition.
        /// </summary>
        [TestMethod]
        public void ValidateAtUseChecksCoverAndSpacingInTheBranchThatUsesThem()
        {
            var basic = new OptionCase();
            var invalid = new[]
            {
                Tuple.Create("nominalCover", (Action<OptionCase>)(x => x.NominalCover = double.NaN)), Tuple.Create("nominalCover", (Action<OptionCase>)(x => x.NominalCover = -1)),
                Tuple.Create("coverOverride", (Action<OptionCase>)(x => x.CoverOverride = double.NaN)), Tuple.Create("coverOverride", (Action<OptionCase>)(x => x.CoverOverride = -1)),
                Tuple.Create("spacingOverride", (Action<OptionCase>)(x => x.SpacingOverride = double.NaN)), Tuple.Create("spacingOverride", (Action<OptionCase>)(x => x.SpacingOverride = 0)),
            };
            var unread = new[]
            {
                Tuple.Create("decompression", basic.With(x => { x.Exposure = "XD1"; x.Sensitive = true; x.Uncracked = () => -.5; })),
                Tuple.Create("crack formation", basic.With(x => { x.Exposure = "XS3"; x.Sensitive = true; x.Combination = ServiceabilityCombination.Frequent; x.Uncracked = () => 3; })),
                Tuple.Create("not required", basic.With(x => x.Combination = ServiceabilityCombination.Characteristic)),
                Tuple.Create("EN not required", basic.With(x => { x.Standard = "EN 1992-1-1"; x.Exposure = "XC3"; x.Combination = ServiceabilityCombination.Frequent; })),
                Tuple.Create("neutral axis in the cover", basic.With(x => { x.Bars = Doubly; x.Plane = AxisInTheCover; })),
                Tuple.Create("no bar in Ac,eff", basic.With(x => x.Plane = AxisAboveTheBars)),
                Tuple.Create("entirely compressed", basic.With(x => { x.Bars = Doubly; x.Plane = UniformCompression; })),
            };
            int checks = 0;
            foreach (var state in unread)
            {
                var clean = state.Item2.Evaluate(AtUse);
                foreach (var bad in invalid)
                {
                    var c = state.Item2.With(bad.Item2); string what = state.Item1 + ", invalid " + bad.Item1;
                    AssertSameResult(clean, c.Evaluate(AtUse), what);
                    Assert.AreEqual(bad.Item1, ParamOf(() => c.Input(SectionCrackOptions.Default), what + ": refused by the constructor with the default options"));
                    checks++;
                }
                AssertSameResult(clean, state.Item2.With(x => { x.NominalCover = double.NaN; x.CoverOverride = -1; x.SpacingOverride = 0; }).Evaluate(AtUse), state.Item1 + ", all invalid");
            }
            Assert.AreEqual(7 * 6, checks);
            // Width formula of the partially compressed section: refused at use (the constructor accepts the values), cover before spacing.
            foreach (var bad in invalid)
            {
                var input = basic.With(bad.Item2).Input(AtUse);
                Assert.AreEqual(bad.Item1, ParamOf(() => SectionCrackCheck.Evaluate(input), "bending, invalid " + bad.Item1));
            }
            Assert.AreEqual("coverOverride", ParamOf(() => basic.With(x => { x.CoverOverride = double.NaN; x.SpacingOverride = double.NaN; }).Evaluate(AtUse), "cover first"));
            Assert.AreEqual("nominalCover", ParamOf(() => basic.With(x => { x.NominalCover = double.NaN; x.SpacingOverride = double.NaN; }).Evaluate(AtUse), "nominal cover first"));
            // An assigned cover replaces the nominal cover in the formula: an invalid nominal cover is not read (NTC hc,eff does not use it).
            var assigned = basic.With(x => x.CoverOverride = 30);
            var r = assigned.With(x => x.NominalCover = double.NaN).Evaluate(AtUse);
            AssertSameResult(assigned.Evaluate(AtUse), r, "assigned cover, nominal NaN");
            Assert.AreEqual(30, r.Details.Single(d => d.Symbol == "c").Value); Assert.AreEqual("assigned", r.Details.Single(d => d.Symbol == "c").Expression);
            // Entirely tensile section: the faces read the overrides (cover before spacing), never the nominal cover.
            var tensile = basic.With(x => { x.Bars = Doubly; x.Plane = EccentricTension; });
            Assert.AreEqual(CrackOutcome.Evaluated, tensile.Evaluate(AtUse).Outcome);
            Assert.AreEqual("coverOverride", ParamOf(() => tensile.With(x => x.CoverOverride = double.NaN).Evaluate(AtUse), "faces, cover"));
            Assert.AreEqual("spacingOverride", ParamOf(() => tensile.With(x => x.SpacingOverride = double.NaN).Evaluate(AtUse), "faces, spacing"));
            AssertSameResult(tensile.Evaluate(AtUse), tensile.With(x => x.NominalCover = double.NaN).Evaluate(AtUse), "faces, nominal cover not read");
            var ds = tensile.With(x => { x.Standard = "DS EN 1992-1-1"; x.Exposure = "XC3"; });
            AssertSameResult(ds.Evaluate(AtUse), ds.With(x => x.NominalCover = double.NaN).Evaluate(AtUse), "DS faces and coarse system, nominal cover not read");
            // Valid values: the same result in both modes.
            foreach (var c in new[] { basic, assigned, basic.With(x => x.SpacingOverride = 150), tensile.With(x => { x.CoverOverride = 35; x.SpacingOverride = 120; }), ds })
                AssertSameResult(c.Evaluate(SectionCrackOptions.Default), c.Evaluate(AtUse), "valid values");
        }

        /// <summary>
        /// ValidateAtUse, bar stresses (ANTHEA Ntc2018Checks.Cracking checks them after the compression return): an entirely compressed section gives wk = 0 also with
        /// missing or non-finite stresses; elsewhere Evaluate refuses them before the branches, with the code BarStresses and the message without "k2:". With
        /// ntcK2FromCompressedBars the bars choose k2 and are checked before any branch, as in 0.0.17.0.
        /// </summary>
        [TestMethod]
        public void ValidateAtUseChecksTheBarStressesAfterTheCompressionReturn()
        {
            var basic = new OptionCase { Bars = Doubly };
            const string k2Message = "k2: bar stresses missing or not finite.", atUseMessage = "Cracking: bar stresses missing or not finite.";
            int checks = 0;
            foreach (var standard in new[] { "NTC 2018", "EN 1992-1-1" })
                foreach (var plane in new[] { UniformCompression, CompressedToZero })
                    foreach (double bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                    {
                        var stresses = Doubly.Select(b => 200000 * plane.GetStrain(b.X, b.Y)).ToArray(); stresses[0] = bad;
                        var clean = basic.With(x => { x.Standard = standard; x.Exposure = standard == "NTC 2018" ? "XC1" : "XC3"; x.Plane = plane; });
                        var c = clean.With(x => x.Stresses = stresses); string what = standard + " " + plane.StrainReferencePoint + " σs[0] = " + bad;
                        var r = c.Evaluate(AtUse);
                        Assert.AreEqual(CrackOutcome.Evaluated, r.Outcome, what); Assert.AreEqual("Section entirely compressed", r.Status, what);
                        Assert.AreEqual(0, r.Width.Value, what); Assert.AreEqual(CrackVerdict.Satisfied, r.Verdict, what); Assert.IsNull(r.K2, what);
                        AssertSameResult(clean.Evaluate(AtUse), r, what);
                        Refusal(() => c.Evaluate(SectionCrackOptions.Default), CrackRejection.BarStresses, k2Message, what + ", default options");
                        Refusal(() => c.With(x => x.LegacyK2 = true).Evaluate(AtUse), CrackRejection.BarStresses, k2Message, what + ", ntcK2FromCompressedBars");
                        checks++;
                    }
            Assert.AreEqual(2 * 2 * 3, checks);
            var none = basic.With(x => { x.Plane = UniformCompression; x.Bars = new CrackBar[0]; });
            Assert.AreEqual(0, none.Evaluate(AtUse).Width.Value);
            Refusal(() => none.Evaluate(SectionCrackOptions.Default), CrackRejection.BarStresses, k2Message, "no bars, default options");
            Refusal(() => none.With(x => x.Plane = BentAt50).Evaluate(AtUse), CrackRejection.BarStresses, atUseMessage, "no bars, bent");
            // Bent and entirely tensile sections: the constructor accepts them, Evaluate refuses them after the compression return.
            foreach (var plane in new[] { BentAt50, EccentricTension, AxisInTheCover })
                foreach (double bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                {
                    var stresses = Doubly.Select(b => 200000 * plane.GetStrain(b.X, b.Y)).ToArray(); stresses[3] = bad;
                    var input = basic.With(x => { x.Plane = plane; x.Stresses = stresses; }).Input(AtUse);
                    Refusal(() => SectionCrackCheck.Evaluate(input), CrackRejection.BarStresses, atUseMessage, "at use, " + plane.StrainReferencePoint + " σs[3] = " + bad);
                }
            // Decompression does not read them in either mode.
            var decompression = basic.With(x => { x.Exposure = "XD1"; x.Sensitive = true; x.Uncracked = () => -.5; x.Stresses = Doubly.Select(b => double.NaN).ToArray(); });
            AssertSameResult(decompression.Evaluate(SectionCrackOptions.Default), decompression.Evaluate(AtUse), "decompression");
        }

        /// <summary>
        /// ValidateAtUse and the number of bar stresses (0.0.25.0; ANTHEA Ntc2018Checks.Cracking takes the stresses of the state and checks them only where σs is
        /// used): an entirely compressed section gives wk = 0 also with no stress, one less or one more than the bars, the same result as with the right stresses;
        /// elsewhere Evaluate refuses them before the branches, with the code BarStresses and the message without "k2:", and with ntcK2FromCompressedBars before
        /// any branch with the message of K2. Without ValidateAtUse the constructor refuses them as in 0.0.17.0, without a code (contract K0).
        /// </summary>
        [TestMethod]
        public void ValidateAtUseChecksTheNumberOfBarStressesAfterTheCompressionReturn()
        {
            var basic = new OptionCase { Bars = Doubly };
            const string countMessage = "Cracking: one stress per ordinary bar is required.", k2Message = "k2: bar stresses missing or not finite.",
                atUseMessage = "Cracking: bar stresses missing or not finite.";
            double[][] Wrong(double[] all) => new[] { new double[0], all.Take(all.Length - 1).ToArray(), all.Concat(new[] { 0.0 }).ToArray() };
            int checks = 0;
            foreach (var standard in new[] { "NTC 2018", "EN 1992-1-1" })
                foreach (var plane in new[] { UniformCompression, CompressedToZero })
                {
                    var clean = basic.With(x => { x.Standard = standard; x.Exposure = standard == "NTC 2018" ? "XC1" : "XC3"; x.Plane = plane; });
                    foreach (var stresses in Wrong(Doubly.Select(b => 200000 * plane.GetStrain(b.X, b.Y)).ToArray()))
                    {
                        var c = clean.With(x => x.Stresses = stresses);
                        string what = standard + " " + plane.StrainReferencePoint + ", " + stresses.Length + " stresses for " + Doubly.Length + " bars";
                        var r = c.Evaluate(AtUse);
                        Assert.AreEqual(CrackOutcome.Evaluated, r.Outcome, what); Assert.AreEqual(CrackReason.EntirelyCompressed, r.Reason, what);
                        Assert.AreEqual(0, r.Width.Value, what); Assert.AreEqual(CrackVerdict.Satisfied, r.Verdict, what); Assert.IsNull(r.K2, what);
                        AssertSameResult(clean.Evaluate(AtUse), r, what);
                        var constructor = Assert.ThrowsException<ArgumentException>(() => c.Input(SectionCrackOptions.Default), what + ", default options");
                        Assert.AreEqual(countMessage, constructor.Message, what); Assert.IsNull(CrackRejection.CodeOf(constructor), what);
                        Refusal(() => c.With(x => x.LegacyK2 = true).Evaluate(AtUse), CrackRejection.BarStresses, k2Message, what + ", ntcK2FromCompressedBars");
                        checks++;
                    }
                }
            Assert.AreEqual(2 * 2 * 3, checks);
            // Bent, entirely tensile and neutral axis in the cover: refused after the compression return, before the branches.
            foreach (var plane in new[] { BentAt50, EccentricTension, AxisInTheCover })
                foreach (var stresses in Wrong(Doubly.Select(b => 200000 * plane.GetStrain(b.X, b.Y)).ToArray()))
                {
                    var c = basic.With(x => { x.Plane = plane; x.Stresses = stresses; });
                    Refusal(() => SectionCrackCheck.Evaluate(c.Input(AtUse)), CrackRejection.BarStresses, atUseMessage, "at use, " + plane.StrainReferencePoint + ", " + stresses.Length + " stresses");
                }
        }

        /// <summary>
        /// EffectiveDepthCover (ANTHEA W8): c of the DIN condition (h − x)/3 ≥ c + 20 mm (NCI 7.3.2(3)), by default the nominal cover. 300×500 with 3Ø20 at y = −200,
        /// neutral axis at y = 50: h − x = 300, h − d = 50, h = 500, coefficient 2 + 0.1 · 500/50 = 3, hc,eff = min(150; 250) = 150 mm unless (h − x)/3 = 100 ≥ c + 20,
        /// then 100 mm. σs = 200 MPa, s = 100 mm ≤ 5 (c + Ø/2), DIN: kt = 0.4, sr,max = min[Ø/(3.6 ρ); σs Ø/(3.6 fct)], wk = sr,max max[0.6 σs/Es; (σs − kt fct/ρ (1 + αe ρ))/Es].
        /// </summary>
        [TestMethod]
        public void EffectiveDepthCoverChangesOnlyTheDinCondition()
        {
            double Hand(double hc)
            {
                double rho = 3 * 314.16 / (300 * hc), sigma = 200;
                double strain = Math.Max(.6 * sigma / 200000, (sigma - .4 * 2.9 / rho * (1 + 200000 / 33000.0 * rho)) / 200000);
                return Math.Min(20 / (3.6 * rho), sigma * 20 / (3.6 * 2.9)) * strain;
            }
            var din = new OptionCase { Standard = "DIN EN 1992-1-1", Exposure = "XC3" };
            foreach (var options in new[] { SectionCrackOptions.Default, AtUse })
            {
                string mode = options.ValidateAtUse ? "at use" : "default";
                // Nominal 90 mm: 100 < 110, hc,eff = 150; with the DIN cover 40 mm: 100 ≥ 60, hc,eff = 100 while the formula keeps c = 90.
                var nominal90 = din.With(x => x.NominalCover = 90);
                foreach (var pair in new[] { Tuple.Create(nominal90, (double?)null, 150d, 90d), Tuple.Create(nominal90, (double?)40, 100d, 90d),
                    Tuple.Create(din, (double?)null, 100d, 40d), Tuple.Create(din, (double?)90, 150d, 40d), Tuple.Create(din, (double?)-5, 100d, 40d), Tuple.Create(nominal90, (double?)79, 100d, 90d) })
                {
                    var r = pair.Item1.Evaluate(options.WithEffectiveDepthCover(pair.Item2));
                    string what = mode + ", nominal " + pair.Item1.NominalCover + ", DIN cover " + pair.Item2;
                    Assert.AreEqual(CrackOutcome.Evaluated, r.Outcome, what + " " + r.Status);
                    Assert.AreEqual(pair.Item3, r.Details.Single(d => d.Symbol == "hc,eff").Value, 1e-9, what + " hc,eff");
                    Assert.AreEqual(300 * pair.Item3, r.EffectiveArea.Value, 1e-6, what + " Ac,eff");
                    Assert.AreEqual(pair.Item4, r.Details.Single(d => d.Symbol == "c").Value, what + " c of the formula");
                    Assert.AreEqual(Hand(pair.Item3), r.Width.Value, 1e-12, what + " wk by hand");
                }
                // null = nominal cover, as 0.0.17.0.
                AssertSameResult(nominal90.Evaluate(SectionCrackOptions.Default), nominal90.Evaluate(options.WithEffectiveDepthCover(null)), mode + " null");
                AssertSameResult(nominal90.Evaluate(SectionCrackOptions.Default), nominal90.Evaluate(options.WithEffectiveDepthCover(90)), mode + " same as nominal");
                // The other profiles and the entirely tensile section do not read it.
                foreach (var c in new[] { din.With(x => { x.Standard = "EN 1992-1-1"; }), din.With(x => { x.Standard = "DS EN 1992-1-1"; }), din.With(x => { x.Standard = "NS EN 1992-1-1"; }),
                    din.With(x => { x.Standard = "Model Code 2010"; x.DesignLimit = .3; }), new OptionCase(), din.With(x => { x.Bars = Doubly; x.Plane = EccentricTension; }) })
                    AssertSameResult(c.Evaluate(options), c.Evaluate(options.WithEffectiveDepthCover(1000)), mode + " " + c.Standard + " " + c.Plane.StrainReferencePoint);
            }
            // Not finite: refused by the option, with its parameter.
            foreach (double bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                Assert.AreEqual("effectiveDepthCover", ParamOf(() => SectionCrackOptions.Default.WithEffectiveDepthCover(bad), "DIN cover " + bad));
            // ValidateAtUse: without the DIN cover the condition reads the nominal cover and refuses it there, also with an assigned cover of the formula.
            var nanNominal = din.With(x => { x.NominalCover = double.NaN; x.CoverOverride = 30; });
            Assert.AreEqual("nominalCover", ParamOf(() => nanNominal.Evaluate(AtUse), "DIN condition with the nominal cover"));
            var withCover = nanNominal.Evaluate(AtUse.WithEffectiveDepthCover(40));
            Assert.AreEqual(100, withCover.Details.Single(d => d.Symbol == "hc,eff").Value, 1e-9); Assert.AreEqual(30, withCover.Details.Single(d => d.Symbol == "c").Value);
            AssertSameResult(din.With(x => x.CoverOverride = 30).Evaluate(AtUse.WithEffectiveDepthCover(40)), withCover, "nominal cover never read");
            // Tensile bars outside Ac,eff, neutral axis at y = −150: h − x = 100, h − d = 50; with c = 10 mm (h − x)/3 = 33.3 ≥ 30, hc,eff = 33.3 mm < h − d and
            // no bar is in Ac,eff (upper bound). The DIN condition reads the nominal cover before that branch: ValidateAtUse refuses an invalid nominal cover
            // there, unlike NTC (ValidateAtUseChecksCoverAndSpacingInTheBranchThatUsesThem); with the DIN cover assigned the nominal cover is not read.
            var above = din.With(x => { x.Plane = AxisAboveTheBars; x.NominalCover = 10; });
            var bound = above.Evaluate(AtUse);
            Assert.AreEqual(CrackOutcome.Evaluated, bound.Outcome, bound.Status); StringAssert.StartsWith(bound.Status, "No bar in Ac,eff");
            Assert.AreEqual(100 / 3.0, bound.Details.Single(d => d.Symbol == "hc,eff").Value, 1e-9, "hc,eff = (h − x)/3");
            Assert.AreEqual("nominalCover", ParamOf(() => above.With(x => x.NominalCover = double.NaN).Evaluate(AtUse), "DIN, tensile bars outside Ac,eff, nominal NaN"));
            Assert.AreEqual("nominalCover", ParamOf(() => above.With(x => x.NominalCover = -1).Evaluate(AtUse), "DIN, tensile bars outside Ac,eff, nominal −1"));
            AssertSameResult(bound, above.With(x => x.NominalCover = double.NaN).Evaluate(AtUse.WithEffectiveDepthCover(10)), "DIN cover assigned, nominal cover not read");
        }

        /// <summary>
        /// Codes of the refusals (CrackRejection): same exact type and message as 0.0.17.0, the code in Exception.Data. WidthParameters and UpperBoundParameters
        /// share the message and are told apart only by the code; the parameter refusals (ArgumentOutOfRangeException) keep their ParamName and have no code.
        /// </summary>
        [TestMethod]
        public void RejectionCodesTellRefusalsWithTheSameMessageApart()
        {
            Assert.AreEqual("GPC.Checkers.Concrete.Cracking.CrackRejection", CrackRejection.DataKey);
            var codes = new[] { CrackRejection.WidthParameters, CrackRejection.UpperBoundParameters, CrackRejection.RibbedBarsRequired, CrackRejection.BarStresses, CrackRejection.UncrackedStressRequired };
            CollectionAssert.AreEqual(new[] { "WidthParameters", "UpperBoundParameters", "RibbedBarsRequired", "BarStresses", "UncrackedStressRequired" }, codes);
            double rho = 942.48 / 33000;
            CrackWidthInput W(double sigma = 250, double es = 200000, double ecm = 33000, double fct = 2.9, double r = double.NaN, double phi = 20, double cover = 40, double spacing = 100,
                double depth = 330, bool ribbed = true, double k2 = .5) => new CrackWidthInput(sigma, es, ecm, fct, double.IsNaN(r) ? rho : r, phi, cover, spacing, depth, false, ribbed, k2);
            var widthCases = new[] { W(sigma: -1), W(sigma: double.NaN), W(es: 0), W(ecm: double.NaN), W(fct: -1), W(r: 0), W(phi: double.PositiveInfinity), W(cover: -1), W(spacing: 0),
                W(depth: double.NaN), W(k2: .4), W(k2: 1.1), W(cover: double.PositiveInfinity) };
            int width = 0, bound = 0;
            foreach (var profile in new[] { CrackProfile.Ntc2018, CrackProfile.EN1992p11, CrackProfile.DinEN1992p11, CrackProfile.ModelCode2010 })
            {
                foreach (var input in widthCases)
                {
                    Refusal(() => CrackWidthCalculator.Width(profile, input), CrackRejection.WidthParameters, InvalidWidthMessage, profile + " width " + width);
                    // Invalid parameters come before the bond check, also with plain bars.
                    Refusal(() => CrackWidthCalculator.Width(profile, new CrackWidthInput(input.SteelStress, input.Es, input.Ecm, input.Fct, input.Rho, input.Diameter, input.Cover,
                        input.Spacing, input.TensileDepth, false, false, input.K2)), CrackRejection.WidthParameters, InvalidWidthMessage, profile + " plain width " + width);
                    width++;
                }
                foreach (var call in new Func<object>[]
                {
                    () => CrackWidthCalculator.UnbondedUpperBound(profile, -1, 200000, 2.9, 20, 300, false, true), () => CrackWidthCalculator.UnbondedUpperBound(profile, double.NaN, 200000, 2.9, 20, 300, false, true),
                    () => CrackWidthCalculator.UnbondedUpperBound(profile, 200, 0, 2.9, 20, 300, false, true), () => CrackWidthCalculator.UnbondedUpperBound(profile, 200, 200000, double.NaN, 20, 300, false, true),
                    () => CrackWidthCalculator.UnbondedUpperBound(profile, 200, 200000, 2.9, -20, 300, false, true), () => CrackWidthCalculator.UnbondedUpperBound(profile, 200, 200000, 2.9, 20, 0, false, false),
                })
                {
                    Refusal(call, CrackRejection.UpperBoundParameters, InvalidWidthMessage, profile + " upper bound " + bound);
                    bound++;
                }
                bool needsRibs = profile == CrackProfile.DinEN1992p11 || profile == CrackProfile.ModelCode2010;
                if (needsRibs)
                {
                    Refusal(() => CrackWidthCalculator.Width(profile, W(ribbed: false)), CrackRejection.RibbedBarsRequired, RibbedBarsMessage, profile + " width, plain bars");
                    Refusal(() => CrackWidthCalculator.UnbondedUpperBound(profile, 200, 200000, 2.9, 20, 300, false, false), CrackRejection.RibbedBarsRequired, RibbedBarsMessage, profile + " bound, plain bars");
                }
                else
                {
                    Assert.IsTrue(CrackWidthCalculator.Width(profile, W(ribbed: false)) > 0); Assert.IsTrue(CrackWidthCalculator.UnbondedUpperBound(profile, 200, 200000, 2.9, 20, 300, false, false) > 0);
                }
            }
            Assert.AreEqual(4 * 13, width); Assert.AreEqual(4 * 6, bound);
            // Same message, different codes.
            var widthRefusal = Assert.ThrowsException<ArgumentException>(() => CrackWidthCalculator.Width(CrackProfile.EN1992p11, W(es: 0)));
            var boundRefusal = Assert.ThrowsException<ArgumentException>(() => CrackWidthCalculator.UnbondedUpperBound(CrackProfile.EN1992p11, 200, 0, 2.9, 20, 300, false, true));
            Assert.AreEqual(widthRefusal.Message, boundRefusal.Message); Assert.AreNotEqual(CrackRejection.CodeOf(widthRefusal), CrackRejection.CodeOf(boundRefusal));
            // Bar stresses of the legacy k2 rule.
            Refusal(() => CrackWidthCalculator.K2(new double[0]), CrackRejection.BarStresses, "k2: bar stresses missing or not finite.", "K2 empty");
            Refusal(() => CrackWidthCalculator.K2(new[] { 1, double.NaN }), CrackRejection.BarStresses, "k2: bar stresses missing or not finite.", "K2 NaN");
            Refusal(() => CrackWidthCalculator.K2(null), CrackRejection.BarStresses, "k2: bar stresses missing or not finite.", "K2 null");
            // Through Evaluate, in both modes: decompression and crack formation without the uncracked section, plain bars with DIN and MC2010 (formula and upper bound).
            var basic = new OptionCase();
            foreach (var options in new[] { SectionCrackOptions.Default, AtUse })
            {
                Refusal(() => basic.With(x => { x.Exposure = "XD1"; x.Sensitive = true; }).Evaluate(options), CrackRejection.UncrackedStressRequired,
                    "Cracking: the stress of the uncracked section is required for Decompression.", "decompression");
                Refusal(() => basic.With(x => { x.Exposure = "XS3"; x.Sensitive = true; x.Combination = ServiceabilityCombination.Frequent; }).Evaluate(options), CrackRejection.UncrackedStressRequired,
                    "Cracking: the stress of the uncracked section is required for CrackFormation.", "crack formation");
                foreach (var plain in new[] { basic.With(x => { x.Standard = "DIN EN 1992-1-1"; x.Exposure = "XC3"; x.Ribbed = false; }),
                    basic.With(x => { x.Standard = "Model Code 2010"; x.Exposure = "XC3"; x.DesignLimit = .3; x.Ribbed = false; }) })
                {
                    Refusal(() => plain.Evaluate(options), CrackRejection.RibbedBarsRequired, RibbedBarsMessage, plain.Standard + " bent");
                    Refusal(() => plain.With(x => x.Plane = AxisAboveTheBars).Evaluate(options), CrackRejection.RibbedBarsRequired, RibbedBarsMessage, plain.Standard + " upper bound");
                }
            }
            // Parameter refusals: ParamName, no code. Exceptions without a code.
            var limitRefusal = Assert.ThrowsException<ArgumentOutOfRangeException>(() => basic.With(x => x.DesignLimit = 0).Evaluate(SectionCrackOptions.Default));
            Assert.AreEqual("designLimit", limitRefusal.ParamName); Assert.IsNull(CrackRejection.CodeOf(limitRefusal));
            var coverRefusal = Assert.ThrowsException<ArgumentOutOfRangeException>(() => basic.With(x => x.CoverOverride = -1).Evaluate(AtUse));
            Assert.AreEqual("coverOverride", coverRefusal.ParamName); Assert.IsNull(CrackRejection.CodeOf(coverRefusal));
            Assert.IsNull(CrackRejection.CodeOf(new ArgumentException("x"))); Assert.IsNull(CrackRejection.CodeOf(null));
        }

        /// <summary>
        /// The refusals of the legacy captures carry the code of their cause. crack-scalar-legacy.csv: the 57 widths in error are MC2010 and DIN with plain bars
        /// (ANTHEA ConcreteCodeChecks.CrackWidth), valid with ribbed bars: RibbedBarsRequired. crack-legacy.csv: the 6 states refused by ANTHEA with "Modello di
        /// fessurazione MC/DIN implementato per barre ad aderenza migliorata." give RibbedBarsRequired, with ValidateAtUse too (the 4 non-convergent analyses stop before).
        /// </summary>
        [TestMethod]
        public void LegacyRefusalsCarryTheirCode()
        {
            int plainWidths = 0;
            foreach (var row in Rows("crack-scalar-legacy.csv", header: false))
            {
                var c = row.Split(';');
                if (!c[0].StartsWith("W") || c[14] == "ok") continue;
                var profile = Profile(c[1]);
                Assert.IsTrue((profile == CrackProfile.ModelCode2010 || profile == CrackProfile.DinEN1992p11) && !bool.Parse(c[12]), c[0] + " " + c[1] + " ribbed " + c[12]);
                CrackWidthInput Input(bool ribbed) => new CrackWidthInput(D(c[2]), D(c[3]), D(c[4]), D(c[5]), D(c[6]), D(c[7]), D(c[8]), D(c[9]), D(c[10]), bool.Parse(c[11]), ribbed, D(c[13]));
                Refusal(() => CrackWidthCalculator.Width(profile, Input(false)), CrackRejection.RibbedBarsRequired, RibbedBarsMessage, c[0]);
                Assert.IsTrue(CrackWidthCalculator.Width(profile, Input(true)) >= 0, c[0] + " valid with ribbed bars");
                plainWidths++;
            }
            Assert.AreEqual(57, plainWidths);
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures");
            GPC.Model.Models.Model archive;
            using (var stream = File.OpenRead(Path.Combine(folder, "crack-sections.xml"))) archive = ModelArchive.Load(stream);
            int refused = 0, notConverged = 0;
            foreach (var row in Rows("crack-legacy.csv"))
            {
                var c = row.Split(';');
                if (c[25] == "ok") continue;
                if (c[30].Contains("non convergente")) { notConverged++; continue; }
                Assert.AreEqual("Modello di fessurazione MC/DIN implementato per barre ad aderenza migliorata.", c[30], c[0]);
                double[] P(string s) => s.Split(',').Select(D).ToArray();
                var o = P(c[8]); var v1 = P(c[9]); var v2 = P(c[10]);
                var axes = new CoordinateSystem(new Point3d(o[0], o[1], o[2]), new Vector3d(v1[0], v1[1], v1[2]), new Vector3d(v2[0], v2[1], v2[2]));
                bool linear = bool.Parse(c[4]), tension = bool.Parse(c[6]);
                var standard = ServiceabilityMigrationTests.Standard(c[2]);
                foreach (var pair in c[3].Split(',')) { var kv = pair.Split('='); typeof(StandardModelCode2010).GetProperty(kv[0]).SetValue(standard, D(kv[1])); }
                var section = (ReinforcedConcreteSection)archive.BeamProperties[c[1]];
                var analysis = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(axes, SectionSolver.FailureAnalysisTypes.ConstantN, SectionSolver.FailureDomainTypes.Plastic,
                    linear ? SectionSolver.StressAnalysisTypes.Linear : SectionSolver.StressAnalysisTypes.NonLinear, D(c[5]), 0, tension, int.Parse(c[7]));
                var stress = new SectionCheckerModelCode2010(new SectionCheckerAttribute(section, null, null), analysis, standard, tension)
                    .GetTensionAnalysisResult(new ResultBeamForces(D(c[11]), D(c[12]), D(c[13]), D(c[14]), D(c[15]), D(c[16]), axes));
                var concrete = (ConcreteMaterialEuropeanCommon)section.ConcreteMaterial;
                foreach (var options in new[] { SectionCrackOptions.Default, AtUse })
                {
                    var input = new SectionCrackInput(ServiceabilityMigrationTests.Standard(c[2]), Combination(c[17]), Exposure(c[18]), c[19] == "Sensibile", N(c[24]),
                        CrackSectionGeometry.From(section, c[1] == "C1000H"), stress.StrainPlane, SectionCrackInput.OrdinaryBarStresses(stress, section), linear, tension, false,
                        section.Rebars.First().RebarMaterial.E, concrete.Ecm, concrete.Fctm, c[20] == "Breve", c[21] == "Migliorata", Covers[c[1]], N(c[22]), N(c[23]), null, false, options);
                    Refusal(() => SectionCrackCheck.Evaluate(input), CrackRejection.RibbedBarsRequired, RibbedBarsMessage, "state " + c[0] + (options.ValidateAtUse ? " at use" : ""));
                }
                refused++;
            }
            Assert.AreEqual(6, refused); Assert.AreEqual(4, notConverged);
        }

        // ---- 0.0.18.0 (ANTHEA F2.7 K3): trace with stable codes (CrackTraceEntry), SectionCrackResult.Reason, RegionOutcomes and the analysis context.

        private static readonly SectionCrackOptions Traced = SectionCrackOptions.Default.WithTrace(true);

        /// <summary>
        /// Symbols of the details column of crack-legacy.csv (ANTHEA supporto/test/CheckerMigration.Capture, CrackCapture.Symbols): the entries with a value whose
        /// symbol is one of these or ends with " · " and one of these (faces, bands, bars).
        /// </summary>
        private static readonly string[] DetailSymbols = { "Criterio k₂", "hc,eff", "Ac,eff", "As,eff", "Øeq", "σs", "c", "s", "sr,max", "wk", "εsm − εcm", "Δsm adottata",
            "σct,max", "σct,lim", "h − x", "Qtaglio" };

        /// <summary>
        /// Symbol of ANTHEA of every trace code, read from the legacy (X.Calculations 2d40a95: Ntc2018Checks.cs, ConcreteTensionCracking.cs, ConcreteInnerCracking.cs,
        /// ConcreteCodeChecks.cs): the table the mapping of ANTHEA needs. The bar counts (suffix " per k₂") and RegionCheck (the name of the region) are in LegacySymbol.
        /// </summary>
        private static readonly Dictionary<string, string> LegacySymbols = new Dictionary<string, string>
        {
            { CrackTraceCodes.AuxiliaryAnalysis, "Analisi ausiliaria" }, { CrackTraceCodes.Fctm, "fctm" }, { CrackTraceCodes.UncrackedMaximumStress, "σct,max" },
            { CrackTraceCodes.UncrackedStressLimit, "σct,lim" }, { CrackTraceCodes.FormationDivisor, "Divisore formazione" }, { CrackTraceCodes.K2Criterion, "Criterio k₂" },
            { CrackTraceCodes.MinimumStrain, "εc,min" }, { CrackTraceCodes.MaximumStrain, "εc,max" }, { CrackTraceCodes.CompressionTolerance, "Tolleranza compressione" },
            { CrackTraceCodes.ChiX, "χx" }, { CrackTraceCodes.ChiY, "χy" }, { CrackTraceCodes.StrainGradient, "|∇ε|" }, { CrackTraceCodes.DirectionX, "qx" },
            { CrackTraceCodes.DirectionY, "qy" }, { CrackTraceCodes.Qmax, "Qmax" }, { CrackTraceCodes.Qmin, "Qmin" }, { CrackTraceCodes.Height, "h" },
            { CrackTraceCodes.TensileDepth, "h − x" }, { CrackTraceCodes.CompressedDepth, "x" }, { CrackTraceCodes.NearestBarDepth, "h − d,min" },
            { CrackTraceCodes.TensileBarCount, "Numero barre tese" }, { CrackTraceCodes.TensileCentroid, "QG,s" }, { CrackTraceCodes.CoverToCentroid, "h − d" },
            { CrackTraceCodes.EffectiveHeight, "d" }, { CrackTraceCodes.EffectiveDepthCandidate1, "Candidato 1 hc,eff" }, { CrackTraceCodes.EffectiveDepthCandidate2, "Candidato 2 hc,eff" },
            { CrackTraceCodes.EffectiveDepthCandidate3, "Candidato 3 hc,eff" }, { CrackTraceCodes.EffectiveDepth, "hc,eff" }, { CrackTraceCodes.CutLevel, "Qtaglio" },
            { CrackTraceCodes.EffectiveArea, "Ac,eff" }, { CrackTraceCodes.EffectiveBarCount, "Numero barre efficaci" }, { CrackTraceCodes.BarStrain, "ε" }, { CrackTraceCodes.BarX, "x" },
            { CrackTraceCodes.BarY, "y" }, { CrackTraceCodes.BarQ, "Q" }, { CrackTraceCodes.BarDiameter, "Ø" }, { CrackTraceCodes.BarArea, "As" }, { CrackTraceCodes.BarStress, "σs" },
            { CrackTraceCodes.EquivalentDiameter, "Øeq" }, { CrackTraceCodes.SteelStress, "σs" }, { CrackTraceCodes.EffectiveSteel, "As,eff" }, { CrackTraceCodes.DiameterSquareSum, "ΣØ²" },
            { CrackTraceCodes.DiameterSum, "ΣØ" }, { CrackTraceCodes.Cover, "c" }, { CrackTraceCodes.Spacing, "s" }, { CrackTraceCodes.AnalysisConcreteModulus, "Ecls analisi" },
            { CrackTraceCodes.AnalysisModularRatio, "n analisi" }, { CrackTraceCodes.Es, "Es" }, { CrackTraceCodes.Ecm, "Ecm" }, { CrackTraceCodes.EffectiveTensileStrength, "fct,eff = fctm" },
            { CrackTraceCodes.Rho, "ρp,eff" }, { CrackTraceCodes.AlphaE, "αe" }, { CrackTraceCodes.Kt, "kt" }, { CrackTraceCodes.K1, "k₁" }, { CrackTraceCodes.K2, "k₂" },
            { CrackTraceCodes.K3, "k₃" }, { CrackTraceCodes.K4, "k₄" }, { CrackTraceCodes.BetaMinimum, "β minimo deformazione" }, { CrackTraceCodes.BetaWidth, "β apertura" },
            { CrackTraceCodes.FarRegionCoefficient, "Coefficiente regione distante" }, { CrackTraceCodes.SpacingThresholdCoefficient, "Coefficiente soglia interasse" },
            { CrackTraceCodes.FormulaSteelStress, "σs (formula)" }, { CrackTraceCodes.FormulaDiameter, "Øeq (formula)" }, { CrackTraceCodes.FormulaCover, "c (formula)" },
            { CrackTraceCodes.FormulaSpacing, "s (formula)" }, { CrackTraceCodes.FormulaTensileDepth, "h − x (formula)" }, { CrackTraceCodes.InteractionFactor, "1 + αe·ρp,eff" },
            { CrackTraceCodes.TensionStiffening, "Δσ tension stiffening" }, { CrackTraceCodes.ComputedStrainDifference, "Δε calcolata" },
            { CrackTraceCodes.MinimumStrainDifference, "Δε minima" }, { CrackTraceCodes.MeanStrainDifference, "εsm − εcm" }, { CrackTraceCodes.CoverTerm, "Termine copriferro" },
            { CrackTraceCodes.ReinforcementTerm, "Termine armatura" }, { CrackTraceCodes.NearSpacing, "Δsm,vicino" }, { CrackTraceCodes.SpacingLimit, "s_lim" },
            { CrackTraceCodes.SpacingExcess, "s − s_lim" }, { CrackTraceCodes.FarSpacing, "Δsm,distante" }, { CrackTraceCodes.AdoptedSpacing, "Δsm adottata" },
            { CrackTraceCodes.MaximumCrackSpacing, "sr,max" }, { CrackTraceCodes.Width, "wk" }, { CrackTraceCodes.WidthRatio, "ηw" },
            { CrackTraceCodes.EntirelyTensileK2, "k₂ · interamente tesa" }, { CrackTraceCodes.GoverningFace, "Faccia governante" },
            { CrackTraceCodes.BandTensileDepth, "h − x della fascia" }, { CrackTraceCodes.BandK2, "k₂ della fascia" }, { CrackTraceCodes.GoverningSurface, "Superficie governante" },
            { CrackTraceCodes.InnerBoundaryNote, "Contorno interno" }
        };

        private static readonly string[] BarCodes = { CrackTraceCodes.BarStrain, CrackTraceCodes.BarX, CrackTraceCodes.BarY, CrackTraceCodes.BarQ, CrackTraceCodes.BarDiameter,
            CrackTraceCodes.BarArea, CrackTraceCodes.BarStress };

        /// <summary>Name of a region as ANTHEA writes it in the prefix of the trace (inverse of RegionKey; the DS coarse system is "Sistema grossolano" there).</summary>
        private static string LegacyRegionName(string key)
        {
            if (key == "TensileZone") return "Zona tesa efficace";
            if (key == "DsCoarseSystem") return "Sistema grossolano";
            if (key == "InnerRing") return "Anello interno";
            if (key.StartsWith("Face")) return "Faccia " + key.Substring(4).Replace("-", "−");
            if (key.StartsWith("InnerWall")) return "Parete interna " + key.Substring(9).Replace("-", "−");
            if (key.StartsWith("Radial(")) return "Fascia radiale " + key.Substring(7).TrimEnd(')') + "°";
            throw new ArgumentException(key);
        }

        /// <summary>Symbol of ANTHEA of a trace entry: the code, the region prefix unless the entry is a summary, the bar prefix "Bnn · ".</summary>
        private static string LegacySymbol(CrackTraceEntry e)
        {
            string per = e.HasFlag(CrackTraceFlags.K2FromBars) ? " per k₂" : "", symbol;
            switch (e.Code)
            {
                case CrackTraceCodes.CompressedBars: symbol = "Barre compresse" + per; break;
                case CrackTraceCodes.TensileBars: symbol = "Barre tese" + per; break;
                case CrackTraceCodes.ZeroStressBars: symbol = "Barre a tensione nulla" + per; break;
                case CrackTraceCodes.RegionCheck: return LegacyRegionName(e.Region);
                default: symbol = LegacySymbols[e.Code]; break;
            }
            if (BarCodes.Contains(e.Code)) symbol = "B" + ((int)e.Argument(CrackTraceArguments.Bar).Value + 1).ToString("D2", CultureInfo.InvariantCulture) + " · " + symbol;
            if (e.Region != null && !e.HasFlag(CrackTraceFlags.Summary)) symbol = LegacyRegionName(e.Region) + " · " + symbol;
            return symbol;
        }

        private static string Codes(SectionCrackResult r) => string.Join("\n", r.Trace.Select(e => e.ToString()));

        /// <summary>
        /// The codes are public constants equal to their name and all different; the table of the symbols of ANTHEA covers every code (the bar counts and
        /// RegionCheck are composed). Flags and arguments are constants equal to their name.
        /// </summary>
        [TestMethod]
        public void TraceCodesAreStableConstants()
        {
            foreach (var type in new[] { typeof(CrackTraceCodes), typeof(CrackTraceFlags), typeof(CrackTraceArguments) })
            {
                var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                Assert.IsTrue(fields.Length > 0 && fields.All(f => f.IsLiteral && f.FieldType == typeof(string)), type.Name);
                foreach (var f in fields) Assert.AreEqual(f.Name, (string)f.GetRawConstantValue(), type.Name + "." + f.Name);
            }
            var codes = typeof(CrackTraceCodes).GetFields().Select(f => (string)f.GetRawConstantValue()).ToArray();
            Assert.AreEqual(91, codes.Length);
            var composed = new[] { CrackTraceCodes.CompressedBars, CrackTraceCodes.TensileBars, CrackTraceCodes.ZeroStressBars, CrackTraceCodes.RegionCheck };
            CollectionAssert.AreEquivalent(codes, LegacySymbols.Keys.Concat(composed).ToArray());
        }

        /// <summary>
        /// (K3) The trace reproduces the details column of crack-legacy.csv: on every state with a result, the entries with a value whose symbol of ANTHEA (code,
        /// region prefix, bar prefix) is one of the 16 symbols of the capture, in the same order and within 1e-9: Criterio k₂, hc,eff, Ac,eff, As,eff, Øeq, σs, c, s,
        /// sr,max, wk, εsm − εcm, Δsm adottata, σct,max, σct,lim, h − x, Qtaglio, also with the prefix of a face, a radial band, the DS coarse system, an inner wall
        /// or ring and a bar (13 339 values on 400 states). The trace changes nothing else (result, Details, refusals) and is empty without the option; every
        /// region of the result is in RegionOutcomes with its width.
        /// </summary>
        [TestMethod]
        public void TraceReproducesTheLegacyDetails()
        {
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures");
            GPC.Model.Models.Model archive;
            using (var stream = File.OpenRead(Path.Combine(folder, "crack-sections.xml"))) archive = ModelArchive.Load(stream);
            var rows = Rows("crack-legacy.csv");
            Assert.AreEqual(936, rows.Length);
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
            int states = 0, withDetails = 0, legacyEntries = 0, compared = 0, refusals = 0, regions = 0, prefixed = 0, bars = 0, summaries = 0;
            foreach (var row in rows)
            {
                var c = row.Split(';'); string id = "state " + c[0] + " " + c[1] + " " + c[2] + " " + c[17] + " " + c[18];
                if (c[25] != "ok" && c[30].Contains("non convergente")) continue;
                double[] P(string s) => s.Split(',').Select(D).ToArray();
                var o = P(c[8]); var v1 = P(c[9]); var v2 = P(c[10]);
                var axes = new CoordinateSystem(new Point3d(o[0], o[1], o[2]), new Vector3d(v1[0], v1[1], v1[2]), new Vector3d(v2[0], v2[1], v2[2]));
                bool linear = bool.Parse(c[4]), tension = bool.Parse(c[6]);
                var section = (ReinforcedConcreteSection)archive.BeamProperties[c[1]];
                var force = new ResultBeamForces(D(c[11]), D(c[12]), D(c[13]), D(c[14]), D(c[15]), D(c[16]), axes);
                var stress = Checker(c, axes, linear, tension).GetTensionAnalysisResult(force);
                var concrete = (ConcreteMaterialEuropeanCommon)section.ConcreteMaterial;
                SectionCrackInput Input(SectionCrackOptions options) => new SectionCrackInput(ServiceabilityMigrationTests.Standard(c[2]), Combination(c[17]), Exposure(c[18]),
                    c[19] == "Sensibile", N(c[24]), CrackSectionGeometry.From(section, c[1] == "C1000H"), stress.StrainPlane, SectionCrackInput.OrdinaryBarStresses(stress, section), linear,
                    tension, false, section.Rebars.First().RebarMaterial.E, concrete.Ecm, concrete.Fctm, c[20] == "Breve", c[21] == "Migliorata", Covers[c[1]], N(c[22]), N(c[23]),
                    () => Checker(c, axes, true, true).GetTensionAnalysisResult(force) is var uncracked ? uncracked.GetConcreteVerticesTension(uncracked.PsiRebar ?? 0).Max(v => v.tension) : 0,
                    false, options);
                if (c[25] != "ok")
                {
                    var plain = Assert.ThrowsException<ArgumentException>(() => SectionCrackCheck.Evaluate(Input(SectionCrackOptions.Default)), id);
                    var traced = Assert.ThrowsException<ArgumentException>(() => SectionCrackCheck.Evaluate(Input(Traced)), id);
                    Assert.AreEqual(plain.Message, traced.Message, id); Assert.AreEqual(CrackRejection.CodeOf(plain), CrackRejection.CodeOf(traced), id);
                    refusals++; continue;
                }
                var r = SectionCrackCheck.Evaluate(Input(SectionCrackOptions.Default)); var t = SectionCrackCheck.Evaluate(Input(Traced));
                Assert.AreEqual(0, r.Trace.Count, id); Assert.AreEqual(0, r.RegionOutcomes.Count, id);
                AssertSameResult(r, t, id); Assert.AreEqual(r.Reason, t.Reason, id); Assert.AreEqual(r.GoverningRegion, t.GoverningRegion, id);
                Assert.AreEqual(r.EffectiveSteel, t.EffectiveSteel, id); Assert.AreEqual(r.UncrackedMaximumStress, t.UncrackedMaximumStress, id);
                var expected = c[36].Length == 0 ? new string[0] : c[36].Split('|');
                legacyEntries += expected.Length;
                var actual = t.Trace.Where(e => e.Value.HasValue).Select(e => Tuple.Create(LegacySymbol(e), e.Value.Value, e))
                    .Where(e => DetailSymbols.Any(s => e.Item1 == s || e.Item1.EndsWith(" · " + s))).ToArray();
                string both = "\nlegacy:  " + c[36] + "\nlibrary: " + string.Join("|", actual.Select(a => a.Item1 + "=" + a.Item2.ToString("R", CultureInfo.InvariantCulture)));
                Assert.AreEqual(expected.Length, actual.Length, id + " entries" + both);
                for (int i = 0; i < expected.Length; i++)
                {
                    int equal = expected[i].LastIndexOf('=');
                    string symbol = expected[i].Substring(0, equal);
                    Assert.AreEqual(symbol, actual[i].Item1, id + " entry " + i + both);
                    Close(D(expected[i].Substring(equal + 1)), actual[i].Item2, id + " " + symbol);
                    if (actual[i].Item3.Region != null && !actual[i].Item3.HasFlag(CrackTraceFlags.Summary)) prefixed++;
                    if (actual[i].Item3.HasFlag(CrackTraceFlags.Summary)) summaries++;
                    if (BarCodes.Contains(actual[i].Item3.Code)) bars++;
                    compared++;
                }
                if (expected.Length > 0) withDetails++;
                Assert.AreEqual(t.RegionOutcomes.Count, t.RegionOutcomes.Select(x => x.Key).Distinct().Count(), id);
                foreach (var region in t.Regions)
                {
                    var outcome = t.RegionOutcomes.SingleOrDefault(x => x.Key == region.Key);
                    Assert.IsNotNull(outcome, id + " " + region.Key); Assert.AreEqual(region.Width, outcome.Width, id + " " + region.Key);
                    Assert.AreEqual(region.Width.HasValue, outcome.Outcome == CrackOutcome.Evaluated, id + " " + region.Key);
                    regions++;
                }
                states++;
            }
            // 932 states with a result or a refusal (4 analyses do not converge): 926 results, 6 refusals; 400 states with details, 13 339 values.
            Assert.AreEqual(926, states); Assert.AreEqual(6, refusals); Assert.AreEqual(400, withDetails);
            Assert.AreEqual(13339, legacyEntries); Assert.AreEqual(13339, compared); Assert.AreEqual(1112, regions);
            Assert.IsTrue(prefixed > 0 && bars > 0 && summaries > 0, prefixed + " prefixed, " + bars + " bars, " + summaries + " summaries");
        }

        /// <summary>
        /// (K3) Order of the trace of ANTHEA (Ntc2018Checks.Cracking, :102-241, and CalculateCrackWidth, :269-300) on the bent 300×500 of (a), NTC, with every value by
        /// hand: neutral axis at y = 50, h − x = 300, 3Ø20 at y = −200 (σs = 200 MPa), hc,eff = min[125; 100; 250] = 100, Ac,eff = 30 000 mm², s = 100 mm ≤ s_lim = 250.
        /// </summary>
        [TestMethod]
        public void TraceFollowsTheOrderOfAnthea()
        {
            var r = new OptionCase().Evaluate(Traced);
            Assert.AreEqual(CrackOutcome.Evaluated, r.Outcome, r.Status);
            var bar = new[] { CrackTraceCodes.BarStrain, CrackTraceCodes.BarX, CrackTraceCodes.BarY, CrackTraceCodes.BarQ, CrackTraceCodes.BarDiameter, CrackTraceCodes.BarArea, CrackTraceCodes.BarStress };
            var order = new[] { CrackTraceCodes.CompressedBars, CrackTraceCodes.TensileBars, CrackTraceCodes.ZeroStressBars, CrackTraceCodes.MinimumStrain, CrackTraceCodes.MaximumStrain,
                CrackTraceCodes.CompressionTolerance, CrackTraceCodes.K2Criterion, CrackTraceCodes.ChiX, CrackTraceCodes.ChiY, CrackTraceCodes.StrainGradient, CrackTraceCodes.DirectionX,
                CrackTraceCodes.DirectionY, CrackTraceCodes.Qmax, CrackTraceCodes.Qmin, CrackTraceCodes.Height, CrackTraceCodes.TensileDepth, CrackTraceCodes.CompressedDepth,
                CrackTraceCodes.TensileBarCount, CrackTraceCodes.TensileCentroid, CrackTraceCodes.CoverToCentroid, CrackTraceCodes.EffectiveHeight, CrackTraceCodes.EffectiveDepthCandidate1,
                CrackTraceCodes.EffectiveDepthCandidate2, CrackTraceCodes.EffectiveDepthCandidate3, CrackTraceCodes.EffectiveDepth, CrackTraceCodes.CutLevel, CrackTraceCodes.EffectiveArea,
                CrackTraceCodes.EffectiveBarCount }
                .Concat(bar).Concat(bar).Concat(bar)
                .Concat(new[] { CrackTraceCodes.EffectiveSteel, CrackTraceCodes.DiameterSquareSum, CrackTraceCodes.DiameterSum, CrackTraceCodes.EquivalentDiameter, CrackTraceCodes.SteelStress,
                    CrackTraceCodes.Cover, CrackTraceCodes.Spacing, CrackTraceCodes.Es, CrackTraceCodes.Ecm, CrackTraceCodes.EffectiveTensileStrength, CrackTraceCodes.Rho, CrackTraceCodes.AlphaE,
                    CrackTraceCodes.Kt, CrackTraceCodes.K1, CrackTraceCodes.K2, CrackTraceCodes.K3, CrackTraceCodes.K4, CrackTraceCodes.BetaMinimum, CrackTraceCodes.BetaWidth,
                    CrackTraceCodes.FarRegionCoefficient, CrackTraceCodes.SpacingThresholdCoefficient, CrackTraceCodes.FormulaSteelStress, CrackTraceCodes.FormulaDiameter,
                    CrackTraceCodes.FormulaCover, CrackTraceCodes.FormulaSpacing, CrackTraceCodes.FormulaTensileDepth, CrackTraceCodes.InteractionFactor, CrackTraceCodes.TensionStiffening,
                    CrackTraceCodes.ComputedStrainDifference, CrackTraceCodes.MinimumStrainDifference, CrackTraceCodes.MeanStrainDifference, CrackTraceCodes.CoverTerm,
                    CrackTraceCodes.ReinforcementTerm, CrackTraceCodes.NearSpacing, CrackTraceCodes.SpacingLimit, CrackTraceCodes.SpacingExcess, CrackTraceCodes.FarSpacing,
                    CrackTraceCodes.AdoptedSpacing, CrackTraceCodes.Width, CrackTraceCodes.WidthRatio }).ToArray();
            CollectionAssert.AreEqual(order, r.Trace.Select(e => e.Code).ToArray(), Codes(r));
            Assert.IsTrue(r.Trace.All(e => e.Region == null && e.Value.HasValue), Codes(r));
            double rho = 3 * 314.16 / 30000, alpha = 200000 / 33000.0, stiffening = .4 * 2.9 / rho * (1 + alpha * rho), computed = (200 - stiffening) / 200000, minimum = .6 * 200 / 200000;
            double strain = Math.Max(computed, minimum), term = .8 * .5 * .425 * 20 / rho, near = (3.4 * 40 + term) / 1.7, wk = 1.7 * near * strain;
            var values = new double[] { 0, 3, 0, -8e-4, 1.2e-3, 1e-12, .5, 0, -4e-6, 4e-6, 0, -1, 250, -250, 500, 300, 200, 3, 200, 50, 450, 125, 100, 250, 100, 150, 30000, 3 }
                .Concat(new[] { -100d, 0, 100 }.SelectMany(x => new[] { 1e-3, x, -200, 200, 20, 314.16, 200 }))
                .Concat(new[] { 3 * 314.16, 1200, 60, 20, 200, 40, 100, 200000, 33000, 2.9, rho, alpha, .4, .8, .5, 3.4, .425, .6, 1.7, .75, 5, 200, 20, 40, 100, 300, 1 + alpha * rho,
                    stiffening, computed, minimum, strain, 3.4 * 40, term, near, 250, -150, 225, near, wk, wk / .3 }).ToArray();
            Assert.AreEqual(values.Length, r.Trace.Count);
            // Relative tolerance (exact for the zeros): strains (1e-3), curvatures (4e-6) and the small constants are checked at their own scale.
            for (int i = 0; i < values.Length; i++) Assert.AreEqual(values[i], r.Trace[i].Value.Value, 1e-12 * Math.Abs(values[i]), r.Trace[i].ToString());
            Assert.AreEqual(wk, r.Width.Value, 1e-12 * wk);
            CrackTraceEntry One(string code) => r.Trace.Single(e => e.Code == code);
            // Constants of the legacy and inputs, exact: tolerance of the compression return, Es, Ecm, fct,eff and the coefficients of the NTC formula.
            var exact = new Dictionary<string, double>
            {
                { CrackTraceCodes.CompressionTolerance, 1e-12 }, { CrackTraceCodes.Es, 200000 }, { CrackTraceCodes.Ecm, 33000 }, { CrackTraceCodes.EffectiveTensileStrength, 2.9 },
                { CrackTraceCodes.Kt, .4 }, { CrackTraceCodes.K1, .8 }, { CrackTraceCodes.K2, .5 }, { CrackTraceCodes.K3, 3.4 }, { CrackTraceCodes.K4, .425 },
                { CrackTraceCodes.BetaMinimum, .6 }, { CrackTraceCodes.BetaWidth, 1.7 }, { CrackTraceCodes.FarRegionCoefficient, .75 }, { CrackTraceCodes.SpacingThresholdCoefficient, 5 }
            };
            foreach (var pair in exact) Assert.AreEqual(pair.Value, One(pair.Key).Value.Value, pair.Key);
            Assert.IsTrue(One(CrackTraceCodes.K2Criterion).HasFlag(CrackTraceFlags.Bending) && One(CrackTraceCodes.K2Criterion).Flags.Count == 1);
            Assert.IsTrue(One(CrackTraceCodes.EffectiveDepth).HasFlag(CrackTraceFlags.MinimumOfThree));
            Assert.IsTrue(r.Trace.Where(e => e.Code == CrackTraceCodes.BarStrain).All(e => e.HasFlag(CrackTraceFlags.Included)));
            CollectionAssert.AreEqual(new double[] { 0, 1, 2 }, r.Trace.Where(e => e.Code == CrackTraceCodes.BarStress).Select(e => e.Argument(CrackTraceArguments.Bar).Value).ToArray());
            Assert.IsTrue(One(CrackTraceCodes.Cover).HasFlag(CrackTraceFlags.Nominal) && One(CrackTraceCodes.Spacing).HasFlag(CrackTraceFlags.Automatic));
            Assert.IsTrue(One(CrackTraceCodes.EquivalentDiameter).HasFlag(CrackTraceFlags.EffectiveBars) && One(CrackTraceCodes.SteelStress).HasFlag(CrackTraceFlags.EffectiveBars));
            Assert.IsTrue(One(CrackTraceCodes.Kt).HasFlag(CrackTraceFlags.LongTerm) && One(CrackTraceCodes.K1).HasFlag(CrackTraceFlags.Ribbed));
            Assert.IsTrue(One(CrackTraceCodes.MeanStrainDifference).HasFlag(computed >= minimum ? CrackTraceFlags.ComputedGoverns : CrackTraceFlags.MinimumGoverns));
            foreach (var code in new[] { CrackTraceCodes.SpacingExcess, CrackTraceCodes.FarSpacing, CrackTraceCodes.AdoptedSpacing }) Assert.IsTrue(One(code).HasFlag(CrackTraceFlags.CloseBars), code);
            Assert.IsTrue(One(CrackTraceCodes.AdoptedSpacing).HasFlag(CrackTraceFlags.NearGoverns));
            var computedEntry = One(CrackTraceCodes.ComputedStrainDifference);
            Assert.AreEqual(200, computedEntry.Argument(CrackTraceArguments.SteelStress).Value, 1e-9); Assert.AreEqual(stiffening, computedEntry.Argument(CrackTraceArguments.TensionStiffening).Value, 1e-12);
            Assert.AreEqual(200000, computedEntry.Argument(CrackTraceArguments.Es).Value);
            Assert.AreEqual(near, One(CrackTraceCodes.Width).Argument(CrackTraceArguments.AdoptedSpacing).Value, 1e-12);
            Assert.AreEqual(strain, One(CrackTraceCodes.Width).Argument(CrackTraceArguments.MeanStrainDifference).Value, 1e-15);
            CollectionAssert.AreEqual(new[] { "TensileZone: Evaluated" }, r.RegionOutcomes.Select(x => x.Key + ": " + x.Outcome).ToArray());
            Assert.AreEqual(r.Width, r.RegionOutcomes[0].Width);
            Assert.AreEqual(CrackReason.None, r.Reason);
        }

        /// <summary>
        /// (K3) The branches of the trace: decompression and crack formation (Ntc2018Checks.cs:84-88), tensile bars outside Ac,eff (upper bound, :209-214 and
        /// ConcreteCodeChecks.cs:230-231), Eurocode formula (ConcreteCodeChecks.cs:206-211), entirely tensile section with the governing face repeated in the summary
        /// (ConcreteTensionCracking.cs), DS coarse system, radial bands with their angle, neutral axis in the cover; the rule of k2 before D7-b.
        /// </summary>
        [TestMethod]
        public void TraceCoversTheBranchesOfAnthea()
        {
            var basic = new OptionCase();
            string[] CodesOf(SectionCrackResult x) => x.Trace.Select(e => e.Code).ToArray();
            // Decompression and crack formation.
            var decompression = basic.With(x => { x.Exposure = "XD1"; x.Sensitive = true; x.Uncracked = () => -.5; }).Evaluate(Traced);
            CollectionAssert.AreEqual(new[] { CrackTraceCodes.AuxiliaryAnalysis, CrackTraceCodes.Fctm, CrackTraceCodes.UncrackedMaximumStress, CrackTraceCodes.UncrackedStressLimit },
                CodesOf(decompression), Codes(decompression));
            CollectionAssert.AreEqual(new double?[] { null, 2.9, -.5, 0 }, decompression.Trace.Select(e => e.Value).ToArray());
            Assert.IsTrue(decompression.Trace[3].HasFlag(CrackTraceFlags.Decompression));
            var formation = basic.With(x => { x.Exposure = "XS3"; x.Sensitive = true; x.Combination = ServiceabilityCombination.Frequent; x.Uncracked = () => 3; }).Evaluate(Traced);
            CollectionAssert.AreEqual(new[] { CrackTraceCodes.AuxiliaryAnalysis, CrackTraceCodes.Fctm, CrackTraceCodes.UncrackedMaximumStress, CrackTraceCodes.UncrackedStressLimit,
                CrackTraceCodes.FormationDivisor }, CodesOf(formation), Codes(formation));
            CollectionAssert.AreEqual(new double?[] { null, 2.9, 3, 2.9 / 1.2, 1.2 }, formation.Trace.Select(e => e.Value).ToArray());
            Assert.IsTrue(formation.Trace[3].HasFlag(CrackTraceFlags.CrackFormation));
            // Not required, missing exposure, linear analysis required: no calculated entry.
            foreach (var state in new[] { basic.With(x => x.Combination = ServiceabilityCombination.Characteristic), basic.With(x => x.Exposure = null) })
                Assert.AreEqual(0, state.Evaluate(Traced).Trace.Count);
            // Upper bound without bonded bars: Øeq and σs of the tensile bars, the four entries of the bound, ηw. NTC 1.7 · 0.75 (h − x), EN 1.3 (h − x), h − x = 100.
            foreach (var pair in new[] { Tuple.Create("NTC 2018", "XC1", CrackTraceFlags.UpperBoundNtc, 1.7 * .75 * 100), Tuple.Create("EN 1992-1-1", "XC3", CrackTraceFlags.UpperBoundEurocode, 130.0) })
            {
                var u = basic.With(x => { x.Standard = pair.Item1; x.Exposure = pair.Item2; x.Plane = AxisAboveTheBars; }).Evaluate(Traced);
                var tail = u.Trace.Skip(u.Trace.Count - 7).ToArray();
                CollectionAssert.AreEqual(new[] { CrackTraceCodes.EquivalentDiameter, CrackTraceCodes.SteelStress, CrackTraceCodes.MeanStrainDifference, CrackTraceCodes.BetaMinimum,
                    CrackTraceCodes.MaximumCrackSpacing, CrackTraceCodes.Width, CrackTraceCodes.WidthRatio }, tail.Select(e => e.Code).ToArray(), Codes(u));
                Assert.IsTrue(tail[0].HasFlag(CrackTraceFlags.TensileBars) && tail[1].HasFlag(CrackTraceFlags.TensileBars), pair.Item1);
                Assert.IsTrue(tail.Skip(2).Take(4).All(e => e.HasFlag(CrackTraceFlags.UpperBound)) && tail[4].HasFlag(pair.Item3), pair.Item1);
                Assert.AreEqual(pair.Item4, tail[4].Value.Value, 1e-9, pair.Item1); Assert.AreEqual(u.Width, tail[5].Value, pair.Item1);
                Assert.AreEqual(3, u.Trace.Count(e => e.Code == CrackTraceCodes.BarStrain && e.HasFlag(CrackTraceFlags.Excluded)), pair.Item1);
                Assert.AreEqual(0, u.Trace.Single(e => e.Code == CrackTraceCodes.EffectiveBarCount).Value, pair.Item1);
            }
            // Eurocode formula: ρ, αe, kt, k2, σs, c, Øeq, s, sr,max, εsm − εcm, βmin, wk (no k3); DIN and MC2010: k2 does not enter sr,max (also in the Criterio k₂).
            foreach (var name in new[] { "EN 1992-1-1", "DIN EN 1992-1-1", "Model Code 2010" })
            {
                var e = basic.With(x => { x.Standard = name; x.Exposure = "XC3"; x.DesignLimit = name == "Model Code 2010" ? .3 : (double?)null; }).Evaluate(Traced);
                int k = Array.IndexOf(CodesOf(e), CrackTraceCodes.Rho);
                CollectionAssert.AreEqual(new[] { CrackTraceCodes.Rho, CrackTraceCodes.AlphaE, CrackTraceCodes.Kt, CrackTraceCodes.K2, CrackTraceCodes.SteelStress, CrackTraceCodes.Cover,
                    CrackTraceCodes.EquivalentDiameter, CrackTraceCodes.Spacing, CrackTraceCodes.MaximumCrackSpacing, CrackTraceCodes.MeanStrainDifference, CrackTraceCodes.BetaMinimum,
                    CrackTraceCodes.Width, CrackTraceCodes.WidthRatio }, CodesOf(e).Skip(k).ToArray(), Codes(e));
                bool notIn = name != "EN 1992-1-1";
                Assert.AreEqual(notIn, e.Trace[k + 3].HasFlag(CrackTraceFlags.NotInWidthFormula), name);
                Assert.AreEqual(notIn, e.Trace.Single(x => x.Code == CrackTraceCodes.K2Criterion).HasFlag(CrackTraceFlags.NotInWidthFormula), name);
                Assert.IsTrue(e.Trace.Skip(k + 4).Take(4).All(x => x.HasFlag(CrackTraceFlags.Formula)), name);
                Assert.IsTrue(e.Trace[k + 8].HasFlag(name == "EN 1992-1-1" ? CrackTraceFlags.FormulaStandard : name == "DIN EN 1992-1-1" ? CrackTraceFlags.FormulaDin : CrackTraceFlags.FormulaModelCode2010), name);
                Assert.AreEqual(e.Width, e.Trace[k + 11].Value, name);
                Assert.IsTrue(e.Trace.Single(x => x.Code == CrackTraceCodes.EffectiveDepth).HasFlag(name == "DIN EN 1992-1-1" ? CrackTraceFlags.DinCoefficient : CrackTraceFlags.MinimumOfThree), name);
                // Rule before D7-b: the k2 of the bars first (all profiles), then 0.5 for the partially compressed section; the k2 of DIN / MC2010 keeps its plain text.
                var legacy = basic.With(x => { x.Standard = name; x.Exposure = "XC3"; x.DesignLimit = name == "Model Code 2010" ? .3 : (double?)null; x.LegacyK2 = true; }).Evaluate(Traced);
                var criteria = legacy.Trace.Where(x => x.Code == CrackTraceCodes.K2Criterion).ToArray();
                Assert.AreEqual(2, criteria.Length, name); Assert.IsTrue(criteria[0].HasFlag(CrackTraceFlags.K2FromBars) && criteria[0].Value == 1, name);
                Assert.IsTrue(criteria[1].HasFlag(CrackTraceFlags.PartiallyCompressed) && criteria[1].Value == .5, name);
                Assert.IsFalse(legacy.Trace.Single(x => x.Code == CrackTraceCodes.K2).HasFlag(CrackTraceFlags.NotInWidthFormula), name);
                Assert.IsTrue(legacy.Trace.Take(3).All(x => x.HasFlag(CrackTraceFlags.K2FromBars)), name);
            }
            // NTC with the rule before D7-b: one Criterio k₂, from the bars (none compressed: 1), before the strains.
            var ntcLegacy = basic.With(x => x.LegacyK2 = true).Evaluate(Traced);
            Assert.AreEqual(CrackTraceCodes.K2Criterion, ntcLegacy.Trace[3].Code); Assert.AreEqual(1, ntcLegacy.Trace[3].Value);
            Assert.AreEqual(1, ntcLegacy.Trace.Count(x => x.Code == CrackTraceCodes.K2Criterion)); Assert.IsFalse(ntcLegacy.Trace[3].HasFlag(CrackTraceFlags.CompressedBar));
            Assert.IsTrue(basic.With(x => { x.LegacyK2 = true; x.Bars = Doubly; }).Evaluate(Traced).Trace[3].HasFlag(CrackTraceFlags.CompressedBar));
            // Neutral axis in the cover: h − d,min after x, then nothing.
            var cover = basic.With(x => { x.Bars = Doubly; x.Plane = AxisInTheCover; }).Evaluate(Traced);
            Assert.AreEqual(CrackTraceCodes.NearestBarDepth, cover.Trace.Last().Code, Codes(cover)); Assert.AreEqual(CrackTraceCodes.CompressedDepth, cover.Trace[cover.Trace.Count - 2].Code);
            Assert.AreEqual(0, cover.RegionOutcomes.Count);
            // Entirely tensile, EN: Criterio k₂ and k₂ · interamente tesa, then per face header, hc,eff, Ac,eff, As,eff (bars as arguments) and the formula with the region;
            // the governing face, its formula again as a summary, Ac,eff, As,eff, ηw. With the rule before D7-b the Criterio k₂ of the bars is replaced.
            foreach (bool legacyK2 in new[] { false, true })
            {
                var t = basic.With(x => { x.Standard = "EN 1992-1-1"; x.Exposure = "XC3"; x.Bars = Doubly; x.Plane = EccentricTension; x.LegacyK2 = legacyK2; }).Evaluate(Traced);
                Assert.AreEqual(CrackOutcome.Evaluated, t.Outcome, t.Status);
                var criteria = t.Trace.Where(x => x.Code == CrackTraceCodes.K2Criterion).ToArray();
                Assert.AreEqual(1, criteria.Length, Codes(t)); Assert.IsTrue(criteria[0].HasFlag(CrackTraceFlags.EntirelyTensile)); Assert.AreEqual(t.K2, criteria[0].Value);
                int at = Array.IndexOf(CodesOf(t), CrackTraceCodes.K2Criterion);
                Assert.AreEqual(CrackTraceCodes.EntirelyTensileK2, t.Trace[at + 1].Code); Assert.AreEqual(legacyK2, t.Trace[at + 1].HasFlag(CrackTraceFlags.K2FromBars));
                Assert.AreEqual(CrackTraceCodes.CompressionTolerance, t.Trace[at - 1].Code, "the Criterio k₂ follows the strains");
                var faces = t.Trace.Where(x => x.Code == CrackTraceCodes.RegionCheck).Select(x => x.Region).ToArray();
                CollectionAssert.AreEqual(new[] { "Face+x", "Face-x", "Face+y", "Face-y" }, faces);
                int face = Array.IndexOf(CodesOf(t), CrackTraceCodes.RegionCheck);
                CollectionAssert.AreEqual(new[] { CrackTraceCodes.RegionCheck, CrackTraceCodes.EffectiveDepth, CrackTraceCodes.EffectiveArea, CrackTraceCodes.EffectiveSteel, CrackTraceCodes.Rho },
                    CodesOf(t).Skip(face).Take(5).ToArray());
                Assert.IsTrue(t.Trace.Skip(face).Take(13).All(x => x.Region == "Face+x"), Codes(t));
                var steel = t.Trace[face + 3]; Assert.AreEqual(t.Regions[0].SteelArea, steel.Value);
                CollectionAssert.AreEqual(t.Regions[0].BarIndices.Select(i => (double)i).ToArray(), steel.ArgumentsNamed(CrackTraceArguments.Bar).ToArray());
                int governing = Array.IndexOf(CodesOf(t), CrackTraceCodes.GoverningFace);
                Assert.AreEqual(t.GoverningRegion, t.Trace[governing].Region); Assert.AreEqual(t.Width, t.Trace[governing].Value);
                var own = t.Trace.Where(x => x.Region == t.GoverningRegion && !x.HasFlag(CrackTraceFlags.Summary)).SkipWhile(x => x.Code != CrackTraceCodes.Rho).ToArray();
                var summary = t.Trace.Skip(governing + 1).Take(own.Length).ToArray();
                CollectionAssert.AreEqual(own.Select(x => x.Code + "=" + x.Value).ToArray(), summary.Select(x => x.Code + "=" + x.Value).ToArray());
                Assert.IsTrue(summary.All(x => x.HasFlag(CrackTraceFlags.Summary) && x.Region == t.GoverningRegion));
                CollectionAssert.AreEqual(new[] { CrackTraceCodes.EffectiveArea, CrackTraceCodes.EffectiveSteel, CrackTraceCodes.WidthRatio }, CodesOf(t).Skip(governing + 1 + own.Length).ToArray());
                CollectionAssert.AreEqual(new[] { "Face+x", "Face-x", "Face+y", "Face-y" }, t.RegionOutcomes.Select(x => x.Key).ToArray());
            }
            // DS: the coarse system closes with the halved wk.
            var ds = basic.With(x => { x.Standard = "DS EN 1992-1-1"; x.Exposure = "XC3"; x.Bars = Doubly; x.Plane = EccentricTension; }).Evaluate(Traced);
            var half = ds.Trace.Single(x => x.HasFlag(CrackTraceFlags.DsCoarseHalf) && !x.HasFlag(CrackTraceFlags.Summary));
            Assert.AreEqual("DsCoarseSystem", half.Region); Assert.AreEqual(ds.Regions.Single(x => x.Key == "DsCoarseSystem").Width, half.Value);
            Assert.AreEqual(.5 * ds.Trace.Last(x => x.Code == CrackTraceCodes.Width && x.Region == "DsCoarseSystem" && !x.HasFlag(CrackTraceFlags.DsCoarseHalf) && !x.HasFlag(CrackTraceFlags.Summary)).Value.Value,
                half.Value.Value, 1e-15);
            Assert.AreEqual("DsCoarseSystem", ds.RegionOutcomes.Last().Key);
            // Radial bands of a ring in uniform tension: the header carries the angle.
            var ringPlane = new StrainPlane(0, 0, new Point2d(0, 0), 5e-4);
            var ring = SectionCrackCheck.Evaluate(new SectionCrackInput(Ntc, ServiceabilityCombination.QuasiPermanent, "XC1", false, null, HollowRing, ringPlane,
                HollowRing.Bars.Select(b => 200000 * ringPlane.GetStrain(b.X, b.Y)), true, false, false, 200000, 33000, 2.9, false, true, 40, null, 300, null, false, Traced));
            var headers = ring.Trace.Where(x => x.Code == CrackTraceCodes.RegionCheck).ToArray();
            Assert.IsTrue(headers.Length > 0 && headers.All(x => x.Region.StartsWith("Radial(")), Codes(ring));
            foreach (var header in headers)
                Assert.AreEqual("Radial(" + header.Argument(CrackTraceArguments.Angle).Value.ToString("0.##", CultureInfo.InvariantCulture) + ")", header.Region);
            Assert.AreEqual("Fascia radiale 22.5°", LegacySymbol(headers.Single(x => x.Region == "Radial(22.5)")));
        }

        /// <summary>
        /// (K3) Reason separates the three branches that Outcome joins in NoEffectiveArea (ANTHEA Ntc2018Checks.cs:180, :218, ConcreteTensionCracking.cs:42), always,
        /// also without options; Outcome and Status do not change. Bars on the tensile edge: hc,eff = 0; hole over the tensile band with the bars in it: Ac,eff = 0;
        /// entirely tensile with bars only at the bottom: the face +y has no steel. DS finds its band by bisection and reaches only the third (ModelCheckerContractTests).
        /// </summary>
        [TestMethod]
        public void ReasonSeparatesTheBranchesOfNoEffectiveArea()
        {
            var hole = new[] { new[] { new Point2d(-150, -250), new Point2d(150, -250), new Point2d(150, -200), new Point2d(-150, -200) } };
            var uniform = new StrainPlane(0, 0, new Point2d(0, 0), 5e-4);
            SectionCrackResult Rect(string standard, IEnumerable<IEnumerable<Point2d>> holes, CrackBar[] bars, StrainPlane plane, SectionCrackOptions options, double? spacing = null)
                => SectionCrackCheck.Evaluate(new SectionCrackInput(ServiceabilityMigrationTests.Standard(standard), ServiceabilityCombination.QuasiPermanent,
                    standard == "NTC 2018" || standard == "UNI EN 1992-1-1" ? "XC1" : "XC3", false, null, new CrackSectionGeometry(Rectangle300x500, holes, bars, CrackBarLayout.Rows),
                    plane, bars.Select(b => 200000 * plane.GetStrain(b.X, b.Y)), true, false, false, 200000, 33000, 2.9, false, true, 40, null, spacing, null, false, options));
            int checks = 0;
            foreach (var standard in new[] { "NTC 2018", "UNI EN 1992-1-1", "EN 1992-1-1", "DIN EN 1992-1-1", "NS EN 1992-1-1", "DS EN 1992-1-1" })
                foreach (var options in new[] { SectionCrackOptions.Default, Traced })
                {
                    string what = standard + (options.Trace ? " traced" : "");
                    var normal = Rect(standard, null, Bottom, BentAt50, options);
                    Assert.AreEqual(CrackOutcome.Evaluated, normal.Outcome, what); Assert.AreEqual(CrackReason.None, normal.Reason, what);
                    var face = Rect(standard, null, Bottom, uniform, options, 100);
                    Assert.AreEqual(CrackOutcome.NoEffectiveArea, face.Outcome, what + " " + face.Status); Assert.AreEqual("Face+y: no effective area or reinforcement", face.Status, what);
                    Assert.AreEqual(CrackReason.FaceWithoutAreaOrSteel, face.Reason, what);
                    if (options.Trace)
                    {
                        CollectionAssert.AreEqual(new[] { "Face+x: Evaluated", "Face-x: Evaluated", "Face+y: NoEffectiveArea" }, face.RegionOutcomes.Select(x => x.Key + ": " + x.Outcome).ToArray(), what);
                        Assert.IsNull(face.RegionOutcomes[2].Width, what);
                        var last = face.Trace.Skip(face.Trace.Count - 4).ToArray();
                        CollectionAssert.AreEqual(new[] { CrackTraceCodes.RegionCheck, CrackTraceCodes.EffectiveDepth, CrackTraceCodes.EffectiveArea, CrackTraceCodes.EffectiveSteel },
                            last.Select(x => x.Code).ToArray(), Codes(face));
                        Assert.IsTrue(last.All(x => x.Region == "Face+y"), what); Assert.AreEqual(0, last[3].Value, what); Assert.AreEqual(0, last[3].Arguments.Count, what);
                    }
                    checks++;
                    if (standard == "DS EN 1992-1-1") continue;
                    var edge = Rect(standard, null, Row(-250), BentAt50, options);
                    Assert.AreEqual(CrackOutcome.NoEffectiveArea, edge.Outcome, what); Assert.AreEqual("Zero effective area", edge.Status, what);
                    Assert.AreEqual(CrackReason.ZeroEffectiveDepth, edge.Reason, what);
                    var covered = Rect(standard, hole, Row(-240), BentAt50, options);
                    Assert.AreEqual(CrackOutcome.NoEffectiveArea, covered.Outcome, what); Assert.AreEqual("No effective reinforcement or area", covered.Status, what);
                    Assert.AreEqual(CrackReason.NoEffectiveSteelOrArea, covered.Reason, what);
                    if (options.Trace)
                    {
                        Assert.AreEqual(CrackTraceCodes.EffectiveDepth, edge.Trace.Last().Code, Codes(edge)); Assert.AreEqual(0, edge.Trace.Last().Value, what);
                        Assert.AreEqual(0, edge.RegionOutcomes.Count, what);
                        Assert.AreEqual(0, covered.Trace.Single(x => x.Code == CrackTraceCodes.EffectiveArea).Value, what);
                        Assert.AreEqual(3, covered.Trace.Single(x => x.Code == CrackTraceCodes.EffectiveBarCount).Value, what);
                        Assert.AreEqual(CrackTraceCodes.BarStress, covered.Trace.Last().Code, Codes(covered));
                        CollectionAssert.AreEqual(new[] { "TensileZone: NoEffectiveArea" }, covered.RegionOutcomes.Select(x => x.Key + ": " + x.Outcome).ToArray(), what);
                    }
                    checks += 2;
                }
            Assert.AreEqual(2 * 6 + 2 * 2 * 5, checks);
            // Every other outcome keeps Reason None; the entirely compressed section has its own reason since the prototype cycle of F2.7
            // (ReasonMarksTheEntirelyCompressedSection).
            var basic = new OptionCase();
            foreach (var state in new[] { basic.With(x => x.Combination = ServiceabilityCombination.Characteristic), basic.With(x => x.Exposure = null),
                basic.With(x => { x.Bars = Row(200); x.Plane = AxisInTheCover; }), basic.With(x => x.Plane = AxisAboveTheBars), basic.With(x => x.Bars = new[] { new CrackBar(0, -200, 20, 314.16) }) })
            {
                var r = state.Evaluate(SectionCrackOptions.Default);
                Assert.AreEqual(CrackReason.None, r.Reason, r.Outcome + " " + r.Status);
            }
        }

        /// <summary>
        /// (K3) RegionOutcomes of the inner surfaces (ANTHEA ConcreteInnerCracking.cs:131-134 composes the status from them): box 400×600 with a hole 200×400 of (g), in
        /// bending. Tensile zone evaluated, side walls tensile without bars, bottom wall evaluated and governing; with one bar in the bottom wall its spacing is not
        /// determined and the outer zone governs; a hole that is neither an axis-aligned rectangle nor a ring gives "InnerSurfaces". The trace repeats the governing
        /// band as a summary and closes with the note of the inner boundary.
        /// </summary>
        [TestMethod]
        public void RegionOutcomesListTheInnerSurfaces()
        {
            var box = new[] { new Point2d(-200, -300), new Point2d(200, -300), new Point2d(200, 300), new Point2d(-200, 300) };
            var hole = new[] { new Point2d(-100, -200), new Point2d(100, -200), new Point2d(100, 200), new Point2d(-100, 200) };
            var plane = new StrainPlane(0, -4e-6, new Point2d(0, 0), 0);
            SectionCrackResult Hollow(IEnumerable<double> xs, SectionCrackOptions options, bool legacyK2 = false)
            {
                var bars = xs.SelectMany(x => new[] { new CrackBar(x, -250, 20, Math.PI * 100), new CrackBar(x, 250, 20, Math.PI * 100) }).ToArray();
                return SectionCrackCheck.Evaluate(new SectionCrackInput(Ntc, ServiceabilityCombination.QuasiPermanent, "XC1", false, null, new CrackSectionGeometry(box, new[] { hole }, bars,
                    CrackBarLayout.Rows), plane, bars.Select(b => 200000 * plane.GetStrain(b.X, b.Y)), true, false, false, 200000, 33000, 2.9, false, true, 40, null, null, null, legacyK2, options));
            }
            var four = new[] { -150d, -50, 50, 150 };
            var r = Hollow(four, Traced); var d = Hollow(four, SectionCrackOptions.Default);
            AssertSameResult(d, r, "box"); Assert.AreEqual(0, d.RegionOutcomes.Count); Assert.AreEqual(0, d.Trace.Count);
            Assert.AreEqual(CrackOutcome.InnerSurfaceUnreinforced, r.Outcome, r.Status); Assert.AreEqual("InnerWall-y", r.GoverningRegion);
            CollectionAssert.AreEqual(new[] { "TensileZone: Evaluated", "InnerWall+x: InnerSurfaceUnreinforced", "InnerWall-x: InnerSurfaceUnreinforced", "InnerWall-y: Evaluated" },
                r.RegionOutcomes.Select(x => x.Key + ": " + x.Outcome).ToArray());
            CollectionAssert.AreEqual(r.Regions.Select(x => x.Key + ":" + x.Width).ToArray(), r.RegionOutcomes.Select(x => x.Key + ":" + x.Width).ToArray());
            Assert.AreEqual(r.Width, r.RegionOutcomes[3].Width);
            var criterion = r.Trace.Single(x => x.Code == CrackTraceCodes.K2Criterion);
            Assert.IsTrue(criterion.HasFlag(CrackTraceFlags.Bending) && criterion.HasFlag(CrackTraceFlags.InnerBands));
            // The band: Ac,eff, As,eff (bars), hc,eff, k₂ della fascia (local, 0.9), formula; then the governing surface, the band again, Ac,eff, As,eff, wk and ηw of the envelope, the note.
            var band = r.Trace.Where(x => x.Region == "InnerWall-y" && !x.HasFlag(CrackTraceFlags.Summary)).ToArray();
            CollectionAssert.AreEqual(new[] { CrackTraceCodes.EffectiveArea, CrackTraceCodes.EffectiveSteel, CrackTraceCodes.EffectiveDepth, CrackTraceCodes.BandK2, CrackTraceCodes.Es },
                band.Take(5).Select(x => x.Code).ToArray(), Codes(r));
            Assert.AreEqual(10000, band[0].Value.Value, 1e-9); Assert.AreEqual(2, band[1].ArgumentsNamed(CrackTraceArguments.Bar).Count()); Assert.AreEqual(50, band[2].Value.Value, 1e-12);
            Assert.AreEqual(.9, band[3].Value.Value, 1e-12); Assert.IsTrue(band[3].HasFlag(CrackTraceFlags.Local) && band.Take(3).All(x => x.HasFlag(CrackTraceFlags.InnerBand)));
            int governing = Array.FindIndex(r.Trace.ToArray(), x => x.Code == CrackTraceCodes.GoverningSurface);
            Assert.AreEqual("InnerWall-y", r.Trace[governing].Region); Assert.IsNull(r.Trace[governing].Value); Assert.IsTrue(r.Trace[governing].HasFlag(CrackTraceFlags.Summary));
            var repeated = r.Trace.Skip(governing + 1).Take(band.Length - 2).ToArray();
            CollectionAssert.AreEqual(band.Skip(2).Select(x => x.Code + "=" + x.Value).ToArray(), repeated.Select(x => x.Code + "=" + x.Value).ToArray());
            Assert.IsTrue(repeated.All(x => x.HasFlag(CrackTraceFlags.Summary) && x.Region == "InnerWall-y"));
            var tail = r.Trace.Skip(governing + 1 + repeated.Length).ToArray();
            CollectionAssert.AreEqual(new[] { CrackTraceCodes.EffectiveArea, CrackTraceCodes.EffectiveSteel, CrackTraceCodes.Width, CrackTraceCodes.WidthRatio, CrackTraceCodes.InnerBoundaryNote },
                tail.Select(x => x.Code).ToArray(), Codes(r));
            Assert.AreEqual(r.EffectiveArea, tail[0].Value); Assert.AreEqual(r.Width, tail[2].Value); Assert.IsTrue(tail[2].HasFlag(CrackTraceFlags.Envelope) && tail[2].Region == null);
            Assert.AreEqual(r.Ratio, tail[3].Value, "suspended ηw: a surface is left without a check");
            // Rule before D7-b: no k₂ della fascia.
            Assert.IsFalse(Hollow(four, Traced, true).Trace.Any(x => x.Code == CrackTraceCodes.BandK2));
            // One bar in the bottom wall: spacing not determined there, every band without width, the outer zone governs.
            var sparse = Hollow(new[] { -150d, -50, 150 }, Traced);
            CollectionAssert.AreEqual(new[] { "TensileZone: Evaluated", "InnerWall+x: InnerSurfaceUnreinforced", "InnerWall-x: InnerSurfaceUnreinforced", "InnerWall-y: SpacingUndetermined" },
                sparse.RegionOutcomes.Select(x => x.Key + ": " + x.Outcome).ToArray());
            Assert.AreEqual("TensileZone", sparse.GoverningRegion); Assert.AreEqual(CrackOutcome.InnerSurfaceUnreinforced, sparse.Outcome);
            Assert.IsFalse(sparse.Trace.Any(x => x.Code == CrackTraceCodes.GoverningSurface || x.Region != null), Codes(sparse));
            Assert.AreEqual(CrackTraceCodes.InnerBoundaryNote, sparse.Trace.Last().Code);
            // A triangular hole: inner surfaces not supported.
            var triangle = new[] { new[] { new Point2d(-50, -100), new Point2d(50, -100), new Point2d(0, -20) } };
            var odd = SectionCrackCheck.Evaluate(new SectionCrackInput(Ntc, ServiceabilityCombination.QuasiPermanent, "XC1", false, null, new CrackSectionGeometry(Rectangle300x500, triangle,
                Bottom, CrackBarLayout.Rows), BentAt50, Bottom.Select(b => 200000 * BentAt50.GetStrain(b.X, b.Y)), true, false, false, 200000, 33000, 2.9, false, true, 40, null, null, null,
                false, Traced));
            Assert.AreEqual(CrackOutcome.InnerSurfaceNotSupported, odd.Outcome, odd.Status);
            CollectionAssert.AreEqual(new[] { "TensileZone: Evaluated", "InnerSurfaces: InnerSurfaceNotSupported" }, odd.RegionOutcomes.Select(x => x.Key + ": " + x.Outcome).ToArray());
            Assert.IsFalse(odd.Trace.Any(x => x.Code == CrackTraceCodes.InnerBoundaryNote));
        }

        /// <summary>
        /// (K3, K0) Without SectionCrackOptions.Trace the trace and the region outcomes are empty; with it nothing else changes, on every branch and with ValidateAtUse,
        /// refusals included. The analysis context adds "Ecls analisi" and "n analisi" = Es (1 + φ)/Ecls before the width formula, only with the trace.
        /// </summary>
        [TestMethod]
        public void TraceIsOffByDefaultAndChangesNothingElse()
        {
            Assert.IsFalse(SectionCrackOptions.Default.Trace); Assert.IsNull(SectionCrackOptions.Default.AnalysisConcreteModulus); Assert.IsNull(SectionCrackOptions.Default.AnalysisPsiRebar);
            Assert.IsTrue(Traced.Trace); Assert.IsFalse(Traced.WithTrace(false).Trace); Assert.IsFalse(SectionCrackOptions.Default.Trace);
            Assert.IsTrue(Traced.WithValidateAtUse(true).Trace && Traced.WithValidateAtUse(true).ValidateAtUse);
            var basic = new OptionCase();
            var states = new[]
            {
                basic, basic.With(x => x.Bars = Doubly), basic.With(x => x.Plane = UniformCompression), basic.With(x => { x.Bars = Doubly; x.Plane = EccentricTension; }),
                basic.With(x => { x.Bars = Doubly; x.Plane = AxisInTheCover; }), basic.With(x => x.Plane = AxisAboveTheBars), basic.With(x => { x.Bars = Doubly; x.CoverOverride = 30; x.SpacingOverride = 150; }),
                basic.With(x => { x.Exposure = "XD1"; x.Sensitive = true; x.Uncracked = () => -.5; }), basic.With(x => { x.Exposure = "XS3"; x.Sensitive = true; x.Combination = ServiceabilityCombination.Frequent; x.Uncracked = () => 3; }),
                basic.With(x => x.LegacyK2 = true), basic.With(x => x.Combination = ServiceabilityCombination.Characteristic),
                basic.With(x => { x.Standard = "DIN EN 1992-1-1"; x.Exposure = "XC3"; x.NominalCover = 90; }), basic.With(x => { x.Standard = "DS EN 1992-1-1"; x.Exposure = "XC3"; x.Bars = Doubly; x.Plane = EccentricTension; }),
                basic.With(x => { x.Standard = "Model Code 2010"; x.Exposure = "XC3"; x.DesignLimit = .3; }), basic.With(x => x.Bars = new[] { new CrackBar(0, -200, 20, 314.16) }),
            };
            for (int i = 0; i < states.Length; i++)
                foreach (var pair in new[] { Tuple.Create(SectionCrackOptions.Default, Traced), Tuple.Create(AtUse, AtUse.WithTrace(true)) })
                {
                    string what = "state " + i + (pair.Item1.ValidateAtUse ? " at use" : "");
                    var plain = states[i].Evaluate(pair.Item1); var traced = states[i].Evaluate(pair.Item2);
                    Assert.AreEqual(0, plain.Trace.Count, what); Assert.AreEqual(0, plain.RegionOutcomes.Count, what);
                    AssertSameResult(plain, traced, what); Assert.AreEqual(plain.Reason, traced.Reason, what);
                    if (!pair.Item1.ValidateAtUse) AssertSameResult(SectionCrackCheck.Evaluate(states[i].WithoutOptions()), traced, what + " without options");
                    Assert.AreEqual(plain.Outcome == CrackOutcome.NotRequired, traced.Trace.Count == 0, what);
                }
            foreach (var bad in new Action<OptionCase>[] { x => x.DesignLimit = double.NaN, x => { x.Plane = UniformCompression; x.Stresses = new[] { double.NaN, 0, 0 }; },
                x => { x.Exposure = "XD1"; x.Sensitive = true; }, x => { x.Standard = "DIN EN 1992-1-1"; x.Exposure = "XC3"; x.Ribbed = false; }, x => { x.Stresses = new[] { 200, double.NaN, 200 }; } })
            {
                Exception expected = null, actual = null;
                try { basic.With(bad).Evaluate(SectionCrackOptions.Default); } catch (Exception e) { expected = e; }
                try { basic.With(bad).Evaluate(Traced); } catch (Exception e) { actual = e; }
                Assert.IsNotNull(expected); Assert.IsNotNull(actual); Assert.AreEqual(expected.GetType(), actual.GetType()); Assert.AreEqual(expected.Message, actual.Message);
                Assert.AreEqual(CrackRejection.CodeOf(expected), CrackRejection.CodeOf(actual));
            }
            // Analysis context: modulus 27 GPa, φ = 2.55, after s and before the formula; nothing without the trace. n analisi is the expression of ANTHEA
            // es * (1 + psi) / e (Ntc2018Checks.cs:238, = Homogenization.ModularRatio), bit for bit: with these values the other orders of the operations,
            // Es/(Ec/(1 + φ)) of Model and Es/Ec·(1 + φ), give another last digit (26.296296296296294 instead of 26.296296296296298).
            double es = 200000, ecls = 27000, psi = 2.55, n = es * (1 + psi) / ecls;
            Assert.AreNotEqual(es / (ecls / (1 + psi)), n, "the order of Model differs"); Assert.AreNotEqual(es / ecls * (1 + psi), n, "Es/Ec first differs");
            var context = Traced.WithAnalysisContext(ecls, psi);
            Assert.AreEqual(ecls, context.AnalysisConcreteModulus); Assert.AreEqual(psi, context.AnalysisPsiRebar); Assert.IsTrue(context.Trace);
            var with = basic.Evaluate(context); var without = basic.Evaluate(Traced);
            int s = Array.FindIndex(with.Trace.ToArray(), x => x.Code == CrackTraceCodes.Spacing);
            Assert.AreEqual(CrackTraceCodes.AnalysisConcreteModulus, with.Trace[s + 1].Code); Assert.AreEqual(ecls, with.Trace[s + 1].Value);
            Assert.AreEqual(CrackTraceCodes.AnalysisModularRatio, with.Trace[s + 2].Code);
            Assert.AreEqual(BitConverter.DoubleToInt64Bits(n), BitConverter.DoubleToInt64Bits(with.Trace[s + 2].Value.Value), "n analisi bit for bit: " + with.Trace[s + 2]);
            Assert.AreEqual(CrackTraceCodes.Es, with.Trace[s + 3].Code);
            CollectionAssert.AreEqual(without.Trace.Select(x => x.ToString()).ToArray(), with.Trace.Where(x => x.Code != CrackTraceCodes.AnalysisConcreteModulus && x.Code != CrackTraceCodes.AnalysisModularRatio)
                .Select(x => x.ToString()).ToArray());
            AssertSameResult(without, with, "context");
            Assert.AreEqual(0, basic.Evaluate(SectionCrackOptions.Default.WithAnalysisContext(30000, 1.5)).Trace.Count);
            Assert.IsFalse(basic.With(x => x.Plane = AxisAboveTheBars).Evaluate(context).Trace.Any(x => x.Code == CrackTraceCodes.AnalysisModularRatio), "only before the width formula");
            var cleared = context.WithAnalysisContext(null, null); Assert.IsNull(cleared.AnalysisConcreteModulus); Assert.IsNull(cleared.AnalysisPsiRebar);
            Assert.IsNotNull(context.AnalysisConcreteModulus, "the With methods copy");
            Assert.ThrowsException<ArgumentException>(() => Traced.WithAnalysisContext(30000, null));
            Assert.ThrowsException<ArgumentException>(() => Traced.WithAnalysisContext(null, 1));
            Assert.AreEqual("concreteModulus", Assert.ThrowsException<ArgumentOutOfRangeException>(() => Traced.WithAnalysisContext(0, 1)).ParamName);
            Assert.AreEqual("concreteModulus", Assert.ThrowsException<ArgumentOutOfRangeException>(() => Traced.WithAnalysisContext(double.NaN, 1)).ParamName);
            Assert.AreEqual("psiRebar", Assert.ThrowsException<ArgumentOutOfRangeException>(() => Traced.WithAnalysisContext(30000, double.PositiveInfinity)).ParamName);
        }

        // ---- 0.0.18.0 (ANTHEA F2.7 K4a): CrackRequirements.ExposureClasses, read-only view of the private list that the requirements read.

        private static string[][] LegacyRequirementRows() => Rows("crack-scalar-legacy.csv", header: false).Select(row => row.Split(';')).Where(c => c[0] == "R").ToArray();

        /// <summary>
        /// The requirement rows of crack-scalar-legacy.csv (legacy ConcreteCodeChecks.CrackRequirement) through the given call: criterion and wlim, with the
        /// mapping of LegacyCrackWidthsAndRequirementsAreReproduced. Returns the number of rows.
        /// </summary>
        private static int AssertLegacyRequirements(Func<CrackProfile, ServiceabilityCombination, string, bool, double?, CrackRequirement> requirement, string label)
        {
            int requirements = 0;
            foreach (var c in LegacyRequirementRows())
            {
                var exposure = Exposure(c[3]);
                var r = requirement(Profile(c[1]), Combination(c[2]), exposure, bool.Parse(c[4]), N(c[5]));
                string kind = c[6]; string what = label + ": " + string.Join(" ", c[1], c[2], c[3], c[4], c[5], kind);
                var expected = kind.StartsWith("Non richiesta") ? CrackCriterion.NotRequired : kind.StartsWith("Selezionare la classe") ? CrackCriterion.ExposureRequired
                    : kind.StartsWith("Selezionare esposizione") ? (exposure == null ? CrackCriterion.ExposureRequired : CrackCriterion.DesignLimitRequired)
                    : kind.StartsWith("Selezionare wlim") ? CrackCriterion.DesignLimitRequired : kind == "Decompressione" ? CrackCriterion.Decompression
                    : kind == "Formazione fessure" ? CrackCriterion.CrackFormation : kind == "Apertura fessure" ? CrackCriterion.CrackWidth : (CrackCriterion)(-1);
                Assert.AreEqual(expected, r.Criterion, what);
                var limit = N(c[7]);
                Assert.AreEqual(limit.HasValue, r.Limit.HasValue, what); if (limit.HasValue) Close(limit.Value, r.Limit.Value, what);
                requirements++;
            }
            return requirements;
        }

        /// <summary>
        /// (K4a) ExposureClasses lists the 18 classes in the order of the legacy list: the requirement rows of crack-scalar-legacy.csv run through it after
        /// "Da scegliere". The NTC environment groups of the captures (NTC 2018 and UNI, frequent combination, little sensitive bars: wlim 0.4 ordinary,
        /// 0.3 aggressive, 0.2 very aggressive) are the index ranges that the requirements apply to this order: up to XF1, up to XF3, then the rest.
        /// The view is read-only and the same at every call; the public array stays a public static readonly string[] with the same classes (binary
        /// compatibility with 0.0.17.0), as a separate object.
        /// </summary>
        [TestMethod]
        public void ExposureClassesAreAReadOnlyViewInTheLegacyOrder()
        {
            var rows = LegacyRequirementRows();
            Assert.AreEqual(7 * 3 * 19 * 2 * 2, rows.Length);
            var legacyOrder = rows.Select(c => c[3]).Where(e => e != "Da scegliere").Distinct().ToArray();
            Assert.AreEqual(18, legacyOrder.Length);
            CollectionAssert.AreEqual(legacyOrder, CrackRequirements.ExposureClasses.ToArray());
            foreach (var standard in new[] { "NTC 2018", "UNI EN 1992-1-1" })
                for (int i = 0; i < CrackRequirements.ExposureClasses.Count; i++)
                {
                    string exposure = CrackRequirements.ExposureClasses[i];
                    var row = rows.Single(c => c[1] == standard && c[2] == "SLE_FREQ" && c[3] == exposure && c[4] == "False" && c[5].Length == 0);
                    Assert.AreEqual("Apertura fessure", row[6], standard + " " + exposure);
                    int legacyGroup = row[7] == "0.4" ? 0 : row[7] == "0.3" ? 1 : row[7] == "0.2" ? 2 : -1;
                    Assert.AreEqual(i <= 4 ? 0 : i <= 11 ? 1 : 2, legacyGroup, standard + " " + exposure);
                }
            // Read-only view, the same object at every call.
            var view = CrackRequirements.ExposureClasses;
            Assert.AreSame(view, CrackRequirements.ExposureClasses);
            Assert.IsInstanceOfType(view, typeof(System.Collections.ObjectModel.ReadOnlyCollection<string>));
            Assert.IsFalse(view is string[]);
            var list = (IList<string>)view;
            Assert.IsTrue(list.IsReadOnly);
            Assert.ThrowsException<NotSupportedException>(() => list[0] = "XY9");
            Assert.ThrowsException<NotSupportedException>(() => list.Add("XY9"));
            Assert.ThrowsException<NotSupportedException>(() => list.Clear());
            Assert.AreEqual("X0", view[0]); Assert.AreEqual(18, view.Count);
            var property = typeof(CrackRequirements).GetProperty(nameof(CrackRequirements.ExposureClasses));
            Assert.AreEqual(typeof(IReadOnlyList<string>), property.PropertyType); Assert.IsNull(property.SetMethod);
            // The public array of 0.0.17.0 stays, with the same classes, as a separate object.
            var field = typeof(CrackRequirements).GetField(nameof(CrackRequirements.Exposures));
            Assert.IsTrue(field.IsPublic && field.IsStatic && field.IsInitOnly); Assert.AreEqual(typeof(string[]), field.FieldType);
            CollectionAssert.AreEqual(view.ToArray(), CrackRequirements.Exposures);
            Assert.AreNotSame(view, CrackRequirements.Exposures);
        }

        /// <summary>
        /// (K4a) The requirements read only the private list. With the public array reversed (before 0.0.18.0 X0..XF1 would have become very aggressive)
        /// and an unknown class in place of XF4, the 1596 requirement rows of crack-scalar-legacy.csv are still reproduced by CrackRequirements.For without
        /// options, with the default options and with ValidateAtUse; the unknown class is still refused and XF4 accepted; SectionCrackCheck.Evaluate gives
        /// the results and the refusal of the unchanged array through both constructors. The array is restored in finally.
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void ChangingThePublicExposuresArrayLeavesTheRequirementsUnchanged()
        {
            var saved = (string[])CrackRequirements.Exposures.Clone();
            var basic = new OptionCase();
            var states = new[]
            {
                basic, basic.With(x => x.Exposure = "XF4"), basic.With(x => { x.Exposure = "X0"; x.Sensitive = true; }),
                basic.With(x => { x.Exposure = "XD1"; x.Sensitive = true; x.Uncracked = () => -.5; }),
                basic.With(x => { x.Exposure = "XS3"; x.Sensitive = true; x.Combination = ServiceabilityCombination.Frequent; x.Uncracked = () => 3; }),
                basic.With(x => { x.Standard = "UNI EN 1992-1-1"; x.Exposure = "XF3"; x.Combination = ServiceabilityCombination.Frequent; }),
                basic.With(x => { x.Standard = "EN 1992-1-1"; x.Exposure = "XC3"; }),
                basic.With(x => { x.Standard = "NS EN 1992-1-1"; x.Exposure = "XD3"; x.Combination = ServiceabilityCombination.Frequent; }),
            };
            var before = states.Select(s => s.Evaluate(SectionCrackOptions.Default)).ToArray();
            var unknown = basic.With(x => x.Exposure = "XY9");
            string refusal = Assert.ThrowsException<ArgumentException>(() => unknown.Evaluate(SectionCrackOptions.Default)).Message;
            Assert.AreEqual("Unknown exposure class: XY9", refusal);
            var qp = ServiceabilityCombination.QuasiPermanent;
            try
            {
                Array.Reverse(CrackRequirements.Exposures);
                Assert.AreEqual("XF4", CrackRequirements.Exposures[0]);
                CrackRequirements.Exposures[0] = "XY9";
                CollectionAssert.AreEqual(saved, CrackRequirements.ExposureClasses.ToArray(), "the view does not follow the array");
                Assert.AreEqual(1596, AssertLegacyRequirements((p, c, e, s, l) => CrackRequirements.For(p, c, e, s, l), "without options"));
                Assert.AreEqual(1596, AssertLegacyRequirements((p, c, e, s, l) => CrackRequirements.For(p, c, e, s, l, SectionCrackOptions.Default), "default options"));
                Assert.AreEqual(1596, AssertLegacyRequirements((p, c, e, s, l) => CrackRequirements.For(p, c, e, s, l, AtUse), "ValidateAtUse"));
                Assert.AreEqual(refusal, Assert.ThrowsException<ArgumentException>(() => CrackRequirements.For(CrackProfile.Ntc2018, qp, "XY9", false)).Message);
                Assert.AreEqual(refusal, Assert.ThrowsException<ArgumentException>(() => CrackRequirements.For(CrackProfile.EN1992p11, qp, "XY9", false, null, AtUse)).Message);
                Assert.AreEqual(.2, CrackRequirements.For(CrackProfile.Ntc2018, qp, "XF4", false).Limit.Value);
                Assert.AreEqual(CrackCriterion.CrackFormation, CrackRequirements.For(CrackProfile.Ntc2018, ServiceabilityCombination.Frequent, "XF4", true).Criterion);
                for (int i = 0; i < states.Length; i++)
                {
                    string what = "state " + i + " " + states[i].Standard + " " + states[i].Exposure;
                    foreach (var after in new[] { states[i].Evaluate(SectionCrackOptions.Default), SectionCrackCheck.Evaluate(states[i].WithoutOptions()), states[i].Evaluate(AtUse) })
                    {
                        AssertSameResult(before[i], after, what); Assert.AreEqual(before[i].Reason, after.Reason, what);
                    }
                }
                Assert.AreEqual(refusal, Assert.ThrowsException<ArgumentException>(() => unknown.Evaluate(SectionCrackOptions.Default)).Message);
                Assert.AreEqual(refusal, Assert.ThrowsException<ArgumentException>(() => SectionCrackCheck.Evaluate(unknown.WithoutOptions())).Message);
            }
            finally
            {
                Array.Copy(saved, CrackRequirements.Exposures, saved.Length);
            }
            CollectionAssert.AreEqual(saved, CrackRequirements.Exposures);
            CollectionAssert.AreEqual(saved, CrackRequirements.ExposureClasses.ToArray());
        }

        // ---- 0.0.18.0 (ANTHEA F2.7, prototype cycle, round 1): what the mapping of ANTHEA had to replicate (formula family, requirement table, cover of the DIN
        // condition of hc,eff), the requirement of the entirely tensile section with the trace, the reason of the entirely compressed section.

        /// <summary>The eight crack profiles through the standards of Model (names of ServiceabilityMigrationTests.Standard).</summary>
        private static readonly string[] ProfileStandards = { "NTC 2018", "Model Code 2010", "EN 1992-1-1", "UNI EN 1992-1-1", "DIN EN 1992-1-1", "DS EN 1992-1-1",
            "NS EN 1992-1-1", "CNR-DT 200 R1/2013" };

        /// <summary>A state of OptionCase with the standard, exposure XC3 and, for Model Code 2010, the design limit 0.3 it requires.</summary>
        private static OptionCase Case(string standard, Action<OptionCase> change = null)
            => new OptionCase().With(x =>
            {
                x.Standard = standard; x.Exposure = "XC3"; x.DesignLimit = standard == "Model Code 2010" ? .3 : (double?)null;
                change?.Invoke(x);
            });

        /// <summary>
        /// (K3, prototype cycle) Formula family, requirement table and cover of the DIN condition of every profile, against the table written by hand from the legacy
        /// of ANTHEA (X.Calculations 2d40a95): Ntc2018Checks.CalculateCrackWidth for NTC 2018 and CNR-DT 200, ConcreteCodeChecks.CrackWidth for the others;
        /// Ntc2018Checks.CrackRequirement, which does not read 'limite_fessure', for NTC 2018, UNI and CNR-DT 200; ConcreteCodeChecks.EffectiveCrackDepth with the
        /// cover only for DIN. The check agrees: the entries of the NTC formula appear only with the NTC family, the upper bound takes its variant from it, a design
        /// limit changes the requirement only where it is used, the cover of the DIN condition moves hc,eff only for DIN (100 → 150 mm by hand). A profile outside the
        /// enumeration is refused.
        /// </summary>
        [TestMethod]
        public void ProfilesTellTheirFormulaRequirementTableAndCover()
        {
            var formula = new Dictionary<CrackProfile, CrackWidthFormula>
            {
                { CrackProfile.Ntc2018, CrackWidthFormula.Ntc2018 }, { CrackProfile.CnrDT200, CrackWidthFormula.Ntc2018 }, { CrackProfile.ModelCode2010, CrackWidthFormula.Eurocode },
                { CrackProfile.EN1992p11, CrackWidthFormula.Eurocode }, { CrackProfile.UniEN1992p11, CrackWidthFormula.Eurocode }, { CrackProfile.DinEN1992p11, CrackWidthFormula.Eurocode },
                { CrackProfile.DsEN1992p11, CrackWidthFormula.Eurocode }, { CrackProfile.NsEN1992p11, CrackWidthFormula.Eurocode }
            };
            var designLimit = new HashSet<CrackProfile> { CrackProfile.ModelCode2010, CrackProfile.EN1992p11, CrackProfile.DinEN1992p11, CrackProfile.DsEN1992p11, CrackProfile.NsEN1992p11 };
            var profiles = (CrackProfile[])Enum.GetValues(typeof(CrackProfile));
            CollectionAssert.AreEquivalent(profiles, formula.Keys.ToArray());
            foreach (var profile in profiles)
            {
                Assert.AreEqual(formula[profile], CrackProfiles.WidthFormula(profile), profile + " formula");
                Assert.AreEqual(designLimit.Contains(profile), CrackProfiles.UsesDesignLimit(profile), profile + " design limit");
                Assert.AreEqual(profile == CrackProfile.DinEN1992p11, CrackProfiles.EffectiveDepthReadsCover(profile), profile + " DIN cover");
            }
            var unknown = (CrackProfile)99;
            Assert.AreEqual("profile", Assert.ThrowsException<ArgumentOutOfRangeException>(() => CrackProfiles.WidthFormula(unknown)).ParamName);
            Assert.AreEqual("profile", Assert.ThrowsException<ArgumentOutOfRangeException>(() => CrackProfiles.UsesDesignLimit(unknown)).ParamName);
            Assert.AreEqual("profile", Assert.ThrowsException<ArgumentOutOfRangeException>(() => CrackProfiles.EffectiveDepthReadsCover(unknown)).ParamName);

            var seen = new HashSet<CrackProfile>(); int bounds = 0;
            foreach (var name in ProfileStandards)
            {
                var profile = CrackProfiles.Resolve(ServiceabilityMigrationTests.Standard(name)); seen.Add(profile);
                bool ntc = CrackProfiles.WidthFormula(profile) == CrackWidthFormula.Ntc2018;
                // Formula: the NTC entries (β apertura, Δsm) or the Eurocode sr,max of the section.
                var bent = Case(name).Evaluate(Traced);
                Assert.AreEqual(CrackOutcome.Evaluated, bent.Outcome, name + " " + bent.Status);
                Assert.AreEqual(ntc, bent.Trace.Any(e => e.Code == CrackTraceCodes.BetaWidth), name + " NTC entries\n" + Codes(bent));
                Assert.AreEqual(ntc, bent.Trace.Any(e => e.Code == CrackTraceCodes.AdoptedSpacing), name + " Δsm");
                Assert.AreEqual(!ntc, bent.Trace.Any(e => e.Code == CrackTraceCodes.MaximumCrackSpacing), name + " sr,max");
                // Upper bound without bonded bars: the NTC variant only with the NTC family. DIN (hc,eff 150 mm, since (h − x)/3 < c + 20) and DS (band centred
                // on the bars) reach the bars in this state and use their formula.
                var bound = Case(name, x => x.Plane = AxisAboveTheBars).Evaluate(Traced);
                var sr = bound.Trace.Single(e => e.Code == CrackTraceCodes.MaximumCrackSpacing);
                Assert.AreEqual(profile != CrackProfile.DinEN1992p11 && profile != CrackProfile.DsEN1992p11, sr.HasFlag(CrackTraceFlags.UpperBound), name + " " + sr);
                if (sr.HasFlag(CrackTraceFlags.UpperBound)) { Assert.AreEqual(ntc, sr.HasFlag(CrackTraceFlags.UpperBoundNtc), name + " " + sr); bounds++; }
                // Cover of the DIN condition: (h − x)/3 = 100 ≥ c + 20 with c = 0, not with c = 1000; DIN coefficient 2 + 0.1·500/50 = 3, so hc,eff 100 or 150 mm.
                double Hc(double cover) => Case(name).Evaluate(Traced.WithEffectiveDepthCover(cover)).Trace.Single(e => e.Code == CrackTraceCodes.EffectiveDepth).Value.Value;
                double low = Hc(0), high = Hc(1000);
                Assert.AreEqual(CrackProfiles.EffectiveDepthReadsCover(profile), low != high, name + " hc,eff " + low + " / " + high);
                if (CrackProfiles.EffectiveDepthReadsCover(profile)) { Assert.AreEqual(100, low, 1e-9, name); Assert.AreEqual(150, high, 1e-9, name); }
            }
            CollectionAssert.AreEquivalent(profiles, seen.ToArray()); Assert.AreEqual(6, bounds);

            // Requirement table: a design limit changes the requirement only for the profiles that use it, and there in every required combination.
            int unchanged = 0, assigned = 0;
            foreach (var profile in profiles)
                foreach (var combination in AllCombinations)
                    foreach (var exposure in new string[] { null }.Concat(CrackRequirements.ExposureClasses))
                        foreach (var sensitive in new[] { false, true })
                        {
                            string what = profile + " " + combination + " " + (exposure ?? "null") + " " + sensitive;
                            var without = CrackRequirements.For(profile, combination, exposure, sensitive, null, AtUse);
                            var with = CrackRequirements.For(profile, combination, exposure, sensitive, .123, AtUse);
                            if (!CrackProfiles.UsesDesignLimit(profile) || without.Criterion == CrackCriterion.NotRequired)
                            {
                                Assert.AreEqual(without.Criterion, with.Criterion, what); Assert.AreEqual(without.Limit, with.Limit, what);
                                Assert.AreEqual(without.RequiredCombination, with.RequiredCombination, what);
                                unchanged++;
                            }
                            else
                            {
                                Assert.AreEqual(CrackCriterion.CrackWidth, with.Criterion, what); Assert.AreEqual(.123, with.Limit.Value, what);
                                assigned++;
                            }
                        }
            Assert.AreEqual(8 * 3 * 19 * 2, unchanged + assigned);
            Assert.AreEqual(5 * 19 * 2, assigned, "five profiles, each in its required combination (NS: the frequent one for XD3 and XS3)");
        }

        /// <summary>
        /// (K3, prototype cycle) With the trace the result of an entirely tensile section carries its requirement, equal to CrackRequirements.For with the same data,
        /// also through the inner surfaces of a hollow section; without the trace it stays null, as in 0.0.17.0 (the contract K0 records the criterion null there).
        /// Nothing else changes. The other branches carry it with and without the trace.
        /// </summary>
        [TestMethod]
        public void TheEntirelyTensileSectionReportsItsRequirementWithTheTrace()
        {
            var uniformTension = new StrainPlane(0, 0, new Point2d(0, 0), 5e-4);
            void AssertRequirement(CrackRequirement expected, CrackRequirement actual, string what)
            {
                Assert.IsNotNull(actual, what);
                Assert.AreEqual(expected.Criterion, actual.Criterion, what); Assert.AreEqual(expected.Limit, actual.Limit, what);
                Assert.AreEqual(expected.RequiredCombination, actual.RequiredCombination, what);
            }
            int tensile = 0, others = 0;
            foreach (var name in ProfileStandards)
            {
                var states = new[]
                {
                    Tuple.Create(true, Case(name, x => { x.Bars = Doubly; x.Plane = EccentricTension; })),
                    Tuple.Create(true, Case(name, x => { x.Bars = Doubly; x.Plane = uniformTension; })),
                    Tuple.Create(false, Case(name)), Tuple.Create(false, Case(name, x => x.Plane = UniformCompression)), Tuple.Create(false, Case(name, x => x.Plane = AxisAboveTheBars)),
                    Tuple.Create(false, Case(name, x => x.Plane = AxisInTheCover))
                };
                for (int i = 0; i < states.Length; i++)
                    foreach (var pair in new[] { Tuple.Create(SectionCrackOptions.Default, Traced), Tuple.Create(AtUse, AtUse.WithTrace(true)) })
                    {
                        string what = name + " state " + i + (pair.Item1.ValidateAtUse ? " at use" : "");
                        var state = states[i].Item2;
                        var plain = state.Evaluate(pair.Item1); var traced = state.Evaluate(pair.Item2);
                        AssertSameResult(plain, traced, what); Assert.AreEqual(plain.Reason, traced.Reason, what);
                        var expected = CrackRequirements.For(CrackProfiles.Resolve(ServiceabilityMigrationTests.Standard(name)), state.Combination, state.Exposure, state.Sensitive,
                            state.DesignLimit, pair.Item1);
                        AssertRequirement(expected, traced.Requirement, what + " traced");
                        if (states[i].Item1)
                        {
                            Assert.IsTrue(traced.Trace.Any(e => e.Code == CrackTraceCodes.GoverningFace) || traced.Outcome != CrackOutcome.Evaluated, what + " entirely tensile\n" + Codes(traced));
                            Assert.IsNull(plain.Requirement, what + " without the trace");
                            tensile++;
                        }
                        else { AssertRequirement(expected, plain.Requirement, what + " plain"); others++; }
                    }
            }
            Assert.AreEqual(8 * 2 * 2, tensile); Assert.AreEqual(8 * 4 * 2, others);
            // Hollow box in uniform tension: the requirement goes through the inner surfaces.
            var box = new[] { new Point2d(-200, -300), new Point2d(200, -300), new Point2d(200, 300), new Point2d(-200, 300) };
            var hole = new[] { new Point2d(-100, -200), new Point2d(100, -200), new Point2d(100, 200), new Point2d(-100, 200) };
            var bars = new[] { -150d, -50, 50, 150 }.SelectMany(x => new[] { new CrackBar(x, -250, 20, Math.PI * 100), new CrackBar(x, 250, 20, Math.PI * 100) }).ToArray();
            SectionCrackResult Hollow(SectionCrackOptions options) => SectionCrackCheck.Evaluate(new SectionCrackInput(Ntc, ServiceabilityCombination.QuasiPermanent, "XC1", false, null,
                new CrackSectionGeometry(box, new[] { hole }, bars, CrackBarLayout.Rows), uniformTension, bars.Select(b => 200000 * uniformTension.GetStrain(b.X, b.Y)), true, false, false,
                200000, 33000, 2.9, false, true, 40, null, null, null, false, options));
            var hollowPlain = Hollow(SectionCrackOptions.Default); var hollowTraced = Hollow(Traced);
            AssertSameResult(hollowPlain, hollowTraced, "hollow");
            Assert.IsTrue(hollowTraced.Trace.Any(e => e.Code == CrackTraceCodes.GoverningFace) && hollowTraced.RegionOutcomes.Any(o => o.Key.StartsWith("InnerWall")), Codes(hollowTraced));
            Assert.IsNull(hollowPlain.Requirement, "hollow without the trace");
            AssertRequirement(CrackRequirements.For(CrackProfile.Ntc2018, ServiceabilityCombination.QuasiPermanent, "XC1", false), hollowTraced.Requirement, "hollow traced");
        }

        /// <summary>
        /// (K3, prototype cycle) Reason marks the entirely compressed section (εc,max ≤ 1e-12, ANTHEA "Sezione interamente compressa", Ntc2018Checks.cs:124) with every
        /// profile, with and without options, also when the bar stresses are not finite (wk = 0 with ValidateAtUse) and in a hollow section; Outcome, Status, wk and K2
        /// are those of 0.0.17.0. The other Evaluated branches keep None: they have their own entry in the trace.
        /// </summary>
        [TestMethod]
        public void ReasonMarksTheEntirelyCompressedSection()
        {
            int compressed = 0;
            foreach (var name in ProfileStandards)
            {
                foreach (var plane in new[] { UniformCompression, CompressedToZero })
                    foreach (var options in new[] { SectionCrackOptions.Default, Traced, AtUse, AtUse.WithTrace(true) })
                    {
                        string what = name + " " + (plane == UniformCompression ? "uniform" : "to zero") + (options.Trace ? " traced" : "") + (options.ValidateAtUse ? " at use" : "");
                        var r = Case(name, x => x.Plane = plane).Evaluate(options);
                        Assert.AreEqual(CrackReason.EntirelyCompressed, r.Reason, what); Assert.AreEqual(CrackOutcome.Evaluated, r.Outcome, what);
                        Assert.AreEqual("Section entirely compressed", r.Status, what); Assert.AreEqual(0, r.Width, what); Assert.IsNull(r.K2, what);
                        Assert.AreEqual(0, r.Regions.Count, what); Assert.IsNull(r.GoverningRegion, what);
                        if (options.Trace) Assert.AreEqual(CrackTraceCodes.CompressionTolerance, r.Trace.Last().Code, what + "\n" + Codes(r));
                        compressed++;
                    }
                var constructor = SectionCrackCheck.Evaluate(Case(name, x => x.Plane = UniformCompression).WithoutOptions());
                Assert.AreEqual(CrackReason.EntirelyCompressed, constructor.Reason, name + " constructor of 0.0.17.0");
                var nonFinite = Case(name, x => { x.Plane = UniformCompression; x.Stresses = new[] { double.NaN, 0, 0 }; }).Evaluate(AtUse);
                Assert.AreEqual(CrackReason.EntirelyCompressed, nonFinite.Reason, name + " non-finite stresses at use"); Assert.AreEqual(0, nonFinite.Width, name);
                // The other Evaluated branches: bending, upper bound, neutral axis in the cover, entirely tensile.
                foreach (var other in new[] { Case(name), Case(name, x => x.Plane = AxisAboveTheBars), Case(name, x => x.Plane = AxisInTheCover),
                    Case(name, x => { x.Bars = Doubly; x.Plane = EccentricTension; }) })
                {
                    var r = other.Evaluate(Traced);
                    Assert.AreEqual(CrackOutcome.Evaluated, r.Outcome, name + " " + r.Status); Assert.AreEqual(CrackReason.None, r.Reason, name + " " + r.Status);
                }
                Assert.IsTrue(Case(name, x => x.Plane = AxisInTheCover).Evaluate(Traced).Trace.Any(e => e.Code == CrackTraceCodes.NearestBarDepth), name + " neutral axis in the cover");
            }
            Assert.AreEqual(8 * 2 * 4, compressed);
            // Hollow section in uniform compression: no inner surface, the same reason.
            var box = new[] { new Point2d(-200, -300), new Point2d(200, -300), new Point2d(200, 300), new Point2d(-200, 300) };
            var hole = new[] { new Point2d(-100, -200), new Point2d(100, -200), new Point2d(100, 200), new Point2d(-100, 200) };
            var bars = new[] { new CrackBar(0, -250, 20, Math.PI * 100), new CrackBar(0, 250, 20, Math.PI * 100) };
            var hollow = SectionCrackCheck.Evaluate(new SectionCrackInput(Ntc, ServiceabilityCombination.QuasiPermanent, "XC1", false, null, new CrackSectionGeometry(box, new[] { hole }, bars,
                CrackBarLayout.Rows), UniformCompression, bars.Select(b => 200000 * UniformCompression.GetStrain(b.X, b.Y)), true, false, false, 200000, 33000, 2.9, false, true, 40, null, null,
                null, false, Traced));
            Assert.AreEqual(CrackReason.EntirelyCompressed, hollow.Reason, hollow.Status); Assert.AreEqual(0, hollow.RegionOutcomes.Count);
        }
    }
}
