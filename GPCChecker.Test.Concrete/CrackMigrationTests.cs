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
    /// SectionRegions; commit fe4652c, captured at 4bb8815 with unchanged sources) to GPC.Checkers.Concrete.Cracking.
    /// Fixtures/crack-legacy.csv: 936 serviceability states on the 6 sections of Fixtures/crack-sections.xml (7 standards, rotating exposure,
    /// sensitivity, duration, bond and overrides); Fixtures/crack-scalar-legacy.csv: 1400 crack widths and the whole requirement table.
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
                if (status.StartsWith("Nessuna armatura tesa"))
                {
                    // Neutral axis within the cover: no verdict in ANTHEA, wk = 0 now (the captured sections have bars on every face).
                    Assert.AreEqual(CrackOutcome.Evaluated, r.Outcome, id + " " + r.Status);
                    Assert.IsTrue(r.Width == 0 && r.Ratio == 0 && r.Passed == true && r.Regions.Count == 0, id + " " + r.Status);
                    coverOnly++; continue;
                }
                if (status.StartsWith("Armatura/area efficace assente"))
                {
                    // Tensile bars outside Ac,eff: no verdict in ANTHEA, upper bound of EC2 7.3.4(3), eq. (7.14) now, from h − x and σs of the legacy trace.
                    var trace = c[36].Split('|').Select(e => e.Split('=')).ToLookup(e => e[0], e => D(e[1]));
                    double depth = trace["h − x"].Single(), sigma = trace.Where(t => t.Key.EndsWith(" · σs")).SelectMany(t => t).Max();
                    var profile = Profile(c[2]);
                    Assert.AreNotEqual(CrackProfile.DinEN1992p11, profile, id);
                    double beta = profile == CrackProfile.ModelCode2010 ? (c[20] == "Breve" ? .4 : .6) : .6;
                    double bound = (CrackProfiles.IsNtc(profile) ? 1.7 * .75 : 1.3) * depth * beta * sigma / section.Rebars.First().RebarMaterial.E;
                    Assert.AreEqual(CrackOutcome.Evaluated, r.Outcome, id + " " + r.Status);
                    Close(bound, r.Width.Value, id + " wk bound"); Close(trace["Ac,eff"].Single(), r.EffectiveArea.Value, id + " Ac,eff");
                    Assert.AreEqual(bound <= r.Limit.Value, r.Passed, id);
                    Assert.IsTrue(r.Regions.Count == 1 && r.Regions[0].Key == "TensileZone" && r.Regions[0].BarIndices.Count == 0, id);
                    unbonded++; continue;
                }
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
            }
            // 362 widths (fully compressed included), 347 verdicts, 1104 regions with area, steel, width and bars, 10 legacy errors, 22 neutral axes in the cover,
            // 8 tensile zones without bars in Ac,eff.
            Assert.AreEqual(362, widths); Assert.AreEqual(347, verdicts); Assert.AreEqual(1104, regions); Assert.AreEqual(10, errors); Assert.AreEqual(22, coverOnly);
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
            // k2 from the bar stresses: one compressed bar means bending.
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
