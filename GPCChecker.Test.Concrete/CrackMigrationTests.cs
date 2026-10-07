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
        /// Eccentric tension, ε = 2e-4 − 1e-6 (y − 250): neutral axis at y = 450, outside; h − x = min(εmax/|∇ε|; h of the section normal to the face),
        /// the h of the outer faces: −y band 700 → 600, ±x bands 650 → 400, +y band 250 (unchanged, no trace entry). Bending with the neutral axis
        /// inside: no h − x entry, nothing changes. Continuity at the uniform-tension threshold: <see cref="HollowSectionInnerBandDepthIsContinuousAtTheUniformThreshold"/>.
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
                    var depth = Entry(r, wall.Item1, "h − x"); double expected = wall.Item1 == "InnerWall+y" ? 250 : wall.Item1 == "InnerWall-y" ? 600 : 400;
                    if (wall.Item1 == "InnerWall+y") Assert.IsNull(depth, what + ": +y band within the section, no entry");
                    else
                    {
                        Assert.AreEqual(expected, depth.Value, what + " " + wall.Item1);
                        StringAssert.StartsWith(depth.Expression, "min[εmax/|∇ε| = " + (wall.Item1 == "InnerWall-y" ? "700" : "650") + " mm; h of the section normal to the face]", what);
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

        /// <summary>
        /// (i) Continuity of h − x of the inner bands at the uniform-tension threshold |∇ε| h = 1e-4 εmax (review of the integration D7-b/d2), box 400×600 of (h),
        /// ε = 5e-4 + χ (q · p) at the centroid with the gradient along y (h along it 600), along x (400) and oblique (0.6; 0.8) (0.6 · 400 + 0.8 · 600 = 720), and
        /// |∇ε| h / εmax = 0.99e-4 (uniform) and 1.01e-4 (general rule). In both, h − x of each band is the height of the section normal to its face (400 for ±x,
        /// 600 for ±y), the h of the outer faces of the entirely tensile section; before, above the threshold, it was the height along the gradient (±x bands:
        /// 600 instead of 400 with the gradient along y, ±y bands: 400 instead of 600 along x), a jump of 50 % in the far crack spacing. wk changes only with σs
        /// and k2 (1 − k2 ≤ 1.01e-4 / 2). Neutral axis inside the section, close to the edge (y = 290): h − x of the ±x bands stays εmax/|∇ε| = 490 mm (beyond
        /// 400, without the bound and without the entry); just outside (y = 310) it is 400, bounded by the height normal to the face, where da7cf02a gave
        /// εmax/|∇ε| = 510. The continuous rule moves the band discontinuity from the threshold to the entry of the neutral axis (pending user decision, R15).
        /// </summary>
        [TestMethod]
        public void HollowSectionInnerBandDepthIsContinuousAtTheUniformThreshold()
        {
            const double eps0 = 5e-4;
            foreach (var direction in new[] { Tuple.Create(0d, 1d, 600d), Tuple.Create(1d, 0d, 400d), Tuple.Create(.6, .8, 720d) })
                foreach (var s in InnerStandards)
                {
                    SectionCrackResult At(double ratio, out StrainPlane plane)
                    {
                        // ratio = χ h / εmax with εmax = eps0 + χ h / 2 (centroid at mid-height along the gradient).
                        double chi = ratio * eps0 / (direction.Item3 * (1 - ratio / 2));
                        plane = new StrainPlane(chi * direction.Item1, chi * direction.Item2, new Point2d(0, 0), eps0);
                        return HollowBox(s.Item1, s.Item2, plane);
                    }
                    var below = At(.99e-4, out var planeBelow); var above = At(1.01e-4, out var planeAbove);
                    string what = s.Item1.GetType().Name + " q = (" + direction.Item1 + "; " + direction.Item2 + ")";
                    Assert.IsTrue(below.Regions.Any(g => g.Key == "Face+x") && above.Regions.Any(g => g.Key == "Face+x"), what + ": entirely tensile branch");
                    foreach (var wall in InnerWalls)
                    {
                        double h = wall.Item1.EndsWith("x") ? 400 : 600;
                        var d0 = BandEntry(below, wall.Item1, "h − x"); var d1 = BandEntry(above, wall.Item1, "h − x");
                        Assert.AreEqual(h, d0.Value, what + " " + wall.Item1 + " below"); StringAssert.StartsWith(d0.Expression, "uniform tension", what);
                        Assert.AreEqual(h, d1.Value, what + " " + wall.Item1 + " above: no jump at the threshold\n" + Trace(above));
                        StringAssert.StartsWith(d1.Expression, "min[εmax/|∇ε| = ", what); StringAssert.Contains(d1.Expression, "mm; h of the section normal to the face]", what);
                        double k0 = BandEntry(below, wall.Item1, "k2").Value, k1 = BandEntry(above, wall.Item1, "k2").Value;
                        Assert.AreEqual(1, k0, what); Assert.IsTrue(k1 < 1 && k1 >= 1 - 1.01e-4 / 2, what + ": k2 = " + k1);
                        double w0 = BandEntry(below, wall.Item1, "wk").Value, w1 = BandEntry(above, wall.Item1, "wk").Value;
                        Assert.AreEqual(HollowBandHand(s.Item3, HollowBandStress(planeBelow, wall.Item1), wall.Item2, wall.Item3, h, 1), w0, 1e-12, what + " " + wall.Item1 + " wk by hand below");
                        Assert.AreEqual(HollowBandHand(s.Item3, HollowBandStress(planeAbove, wall.Item1), wall.Item2, wall.Item3, h, k1), w1, 1e-12, what + " " + wall.Item1 + " wk by hand above");
                        Assert.AreEqual(w0, w1, 1e-4 * w0, what + " " + wall.Item1 + ": wk continuous at the threshold");
                    }
                    Assert.AreEqual(below.Width.Value, above.Width.Value, 1e-4 * below.Width.Value, what + ": governing wk continuous at the threshold");
                }
            // Neutral axis inside (y = 290) and just outside (y = 310) the top edge, tension below: ±x bands (y = −200 … 200).
            foreach (var s in InnerStandards)
            {
                var inside = HollowBox(s.Item1, s.Item2, new StrainPlane(0, -1e-6, new Point2d(0, 290), 0));
                var outside = HollowBox(s.Item1, s.Item2, new StrainPlane(0, -1e-6, new Point2d(0, 310), 0));
                string what = s.Item1.GetType().Name;
                Assert.IsFalse(inside.Regions.Any(g => g.Key == "Face+x"), what + ": neutral axis inside, bending branch");
                Assert.IsTrue(outside.Regions.Any(g => g.Key == "Face+x"), what + ": neutral axis outside, entirely tensile branch");
                foreach (var wall in new[] { InnerWalls[0], InnerWalls[1] })
                {
                    Assert.IsNull(BandEntry(inside, wall.Item1, "h − x"), what + " " + wall.Item1 + ": no bound with the neutral axis inside\n" + Trace(inside));
                    double k2 = BandEntry(inside, wall.Item1, "k2").Value;
                    Assert.AreEqual((9e-5 + 4.9e-4) / (2 * 4.9e-4), k2, 1e-12, what + " " + wall.Item1 + " k2 of the band");
                    var plane = new StrainPlane(0, -1e-6, new Point2d(0, 290), 0);
                    Assert.AreEqual(HollowBandHand(s.Item3, HollowBandStress(plane, wall.Item1), wall.Item2, wall.Item3, 490, k2), BandEntry(inside, wall.Item1, "wk").Value, 1e-12,
                        what + " " + wall.Item1 + ": h − x = εmax/|∇ε| = 490 mm");
                    var depth = BandEntry(outside, wall.Item1, "h − x");
                    Assert.AreEqual(400, depth.Value, what + " " + wall.Item1); StringAssert.StartsWith(depth.Expression, "min[εmax/|∇ε| = 510 mm; h of the section normal to the face]", what);
                }
            }
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
    }
}
