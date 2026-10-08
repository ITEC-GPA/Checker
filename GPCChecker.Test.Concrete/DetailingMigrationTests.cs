using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Cracking;
using GPC.Checkers.Concrete.Detailing;
using GPC.Checkers.Concrete.Response;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Materials;
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

namespace ConcreteTests
{
    /// <summary>
    /// Bond, anchorage and laps, 1D detailing of beams and columns and the moment-curvature response moved from ANTHEA (ConcreteBond,
    /// ConcreteAnchorageCalculator, ConcreteDetailingCalculator, MomentCurvatureCalculator; commit fe4652c).
    /// Fixtures: anchorage-legacy.csv (468 cases), detailing-legacy.csv (144 beam/column cases on detailing-sections.xml), curvature-legacy.csv (5 curves),
    /// captured on 7/10/2026 from ANTHEA refactoring/integrazione-d7b-d2 d2d3225 (supporto/test/CheckerMigration.Capture, mode tutte).
    /// </summary>
    [TestClass]
    public class DetailingMigrationTests
    {
        private static double D(string s) => double.Parse(s, CultureInfo.InvariantCulture);
        private static double? N(string s) => s.Length == 0 || s == "NaN" ? (double?)null : D(s);
        private static void Close(double expected, double actual, string what, double tolerance = 1e-9)
            => Assert.AreEqual(expected, actual, tolerance * Math.Max(1, Math.Abs(expected)), what);
        private static string Folder => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures");
        private static string[] Rows(string name) => File.ReadAllLines(Path.Combine(Folder, name), Encoding.UTF8).Where(l => !l.StartsWith("#")).ToArray();
        private static GPC.Model.Models.Model Archive()
        {
            using (var stream = File.OpenRead(Path.Combine(Folder, "detailing-sections.xml"))) return ModelArchive.Load(stream);
        }

        [TestMethod]
        public void LegacyAnchorageAndBondAreReproduced()
        {
            int anchors = 0, errors = 0, bonds = 0;
            foreach (var row in Rows("anchorage-legacy.csv"))
            {
                var c = row.Split(';');
                if (c[10] == "bond")
                {
                    Close(D(c[11]), AnchorageCalculator.BondStrength(D(c[3]), D(c[1]), bool.Parse(c[5]) ? 1 : .7, 1, D(c[4])), "bond " + c[0]); bonds++; continue;
                }
                var input = new AnchorageInput(D(c[1]), D(c[2]), D(c[3]), D(c[4]), bool.Parse(c[5]), D(c[6]), bool.Parse(c[7]), D(c[8]), D(c[9]));
                if (c[10] != "ok")
                {
                    Assert.AreEqual("error:ArgumentException", c[10], c[0]);
                    Assert.ThrowsException<ArgumentException>(() => AnchorageCalculator.Calculate(DetailingProfile.Ntc2018, input), c[0]); errors++; continue;
                }
                var r = AnchorageCalculator.Calculate(DetailingProfile.Ntc2018, input);
                Close(D(c[11]), r.Fbd, c[0] + " fbd"); Close(D(c[12]), r.BasicLength, c[0] + " lb,rqd"); Close(D(c[13]), r.RequiredLength, c[0] + " required");
                Assert.AreEqual(bool.Parse(c[14]), r.Passed, c[0] + " passed");
                Close(D(c[15]), r.Eta1, c[0] + " η1"); Close(D(c[16]), r.Eta2, c[0] + " η2"); Close(D(c[17]), r.Alpha6, c[0] + " α6"); Close(D(c[18]), r.MaximumLapClearDistance, c[0] + " 4Ø");
                Assert.AreEqual(bool.Parse(c[19]), r.LengthPassed, c[0]); Assert.AreEqual(bool.Parse(c[20]), r.LapClearDistancePassed, c[0]);
                anchors++;
            }
            Assert.AreEqual(445, anchors); Assert.AreEqual(5, errors); Assert.AreEqual(18, bonds);
        }

        /// <summary>EN 1992-1-1 8.4 and 8.7 by hand: Ø20, σsd = 391.3 MPa, fctk,0.05 = 2.0 MPa (C30/37), γc = 1.5, good bond.</summary>
        [TestMethod]
        public void EurocodeAnchorageAndLapMatchHandCalculation()
        {
            double fbd = 2.25 * 1 * 1 * 2.0 / 1.5;   // 3.0 MPa
            double lb = 20 * 391.3 / (4 * fbd);       // 652.2 mm
            var anchorage = AnchorageCalculator.Calculate(DetailingProfile.EN1992p11, new AnchorageInput(20, 391.3, 2.0, 1.5, true, 700));
            Assert.AreEqual(3.0, anchorage.Fbd, 1e-12); Assert.AreEqual(lb, anchorage.RequiredLength, 1e-9); Assert.IsTrue(anchorage.Passed);
            Assert.AreEqual(Math.Max(.3 * lb, 200), anchorage.MinimumLength, 1e-9, "lb,min = max(0.3 lb,rqd; 10Ø; 100 mm)");
            // Lap with 100% of the bars lapped: α6 = 1.5, l0 = 978.3 mm; a clear distance of 70 mm exceeds min(4Ø; 50) = 50 mm by 20 mm.
            var lap = AnchorageCalculator.Calculate(DetailingProfile.EN1992p11, new AnchorageInput(20, 391.3, 2.0, 1.5, true, 990, true, 100, 70));
            Assert.AreEqual(1.5, lap.Alpha6); Assert.AreEqual(1.5 * lb + 20, lap.RequiredLength, 1e-9); Assert.IsFalse(lap.Passed);
            // NTC keeps its floors (20Ø, 150 mm) and fails the lap with a clear distance beyond 4Ø.
            var ntc = AnchorageCalculator.Calculate(DetailingProfile.Ntc2018, new AnchorageInput(8, 100, 2.0, 1.5, true, 150));
            Assert.AreEqual(160, ntc.RequiredLength, 1e-12, "20Ø governs");
            Assert.IsFalse(AnchorageCalculator.Calculate(DetailingProfile.Ntc2018, new AnchorageInput(20, 391.3, 2.0, 1.5, true, 2000, true, 50, 90)).LapClearDistancePassed);
            // Poor bond: η1 = 0.7; large bars: η2 = (132 − Ø)/100; plain bars are not covered.
            Assert.AreEqual(.7 * fbd, AnchorageCalculator.Calculate(DetailingProfile.DsEN1992p11, new AnchorageInput(20, 391.3, 2.0, 1.5, false, 700)).Fbd, 1e-12);
            Assert.AreEqual(.92, AnchorageCalculator.Calculate(DetailingProfile.UniEN1992p11, new AnchorageInput(40, 391.3, 2.0, 1.5, true, 700)).Eta2, 1e-12);
            Assert.ThrowsException<NotSupportedException>(() => AnchorageCalculator.Calculate(DetailingProfile.EN1992p11, new AnchorageInput(20, 391.3, 2.0, 1.5, true, 700, ribbedBars: false)));
        }

        private static readonly Dictionary<string, string> Keys = new Dictionary<string, string>
        {
            { "Interferro minimo", "ClearSpacing" }, { "Copriferro nominale", "NominalCover" }, { "Margine copriferro barre longitudinali", "BarCoverMargin" },
            { "Diametro longitudinale", "LongitudinalDiameter" }, { "Interasse longitudinale", "LongitudinalSpacing" }, { "Armatura longitudinale minima", "MinimumLongitudinal" },
            { "Armatura longitudinale massima", "MaximumLongitudinal" }, { "Diametro staffe", "LinkDiameter" }, { "Passo staffe", "LinkSpacing" },
            { "As,min inferiore", "MinimumTension:Bottom" }, { "As,min superiore", "MinimumTension:Top" }, { "As,max inferiore", "MaximumTension:Bottom" },
            { "As,max superiore", "MaximumTension:Top" }, { "Staffe minime · inferiore", "MinimumLinks:Bottom" }, { "Staffe minime · superiore", "MinimumLinks:Top" },
            { "Passo staffe · inferiore", "LinkSpacing:Bottom" }, { "Passo staffe · superiore", "LinkSpacing:Top" }, { "Trattenimento barre compresse", "CompressionBarRestraint" },
            { "Ancoraggio negli appoggi di estremità", "EndSupportAnchorage" }, { "Barre trattenute dalle staffe", "BarsHeldByLinks" },
            { "Armatura massima nella giunzione", "MaximumAtLap" }
        };
        private static double Fctm(double fck) => fck <= 50 ? .3 * Math.Pow(fck, 2d / 3) : 2.12 * Math.Log(1 + (fck + 8) / 10);

        private static MemberDetailingInput Input(string[] c, ReinforcedConcreteSection section) => new MemberDetailingInput(
            c[2] == "Column" ? MemberDetailingKind.Column : MemberDetailingKind.Beam, CrackSectionGeometry.From(section), D(c[6]), D(c[3]), Fctm(D(c[3])), D(c[4]), D(c[5]),
            D(c[9]), D(c[10]), D(c[11]), D(c[13]), bool.Parse(c[14]), D(c[15]), D(c[16]), int.Parse(c[17]), D(c[18]), D(c[12]), N(c[19]), D(c[20]), bool.Parse(c[21]),
            bool.Parse(c[22]), bool.Parse(c[23]));

        [TestMethod]
        public void LegacyDetailingIsReproduced()
        {
            var archive = Archive(); var rows = Rows("detailing-legacy.csv").Skip(1).ToArray();
            Assert.AreEqual(144, rows.Length);
            int checks = 0;
            foreach (var row in rows)
            {
                var c = row.Split(';'); string id = "case " + c[0] + " " + c[1] + " " + c[2];
                Assert.AreEqual("ok", c[24], id);
                var r = MemberDetailingCalculator.Calculate(DetailingProfile.Ntc2018, Input(c, (ReinforcedConcreteSection)archive.BeamProperties[c[1]]));
                var legacy = c[25].Split('|');
                Assert.AreEqual(legacy.Length, r.Checks.Count, id + ": " + string.Join(",", r.Checks.Select(x => x.Key)));
                for (int i = 0; i < legacy.Length; i++)
                {
                    var parts = legacy[i].Split(':'); var check = r.Checks[i]; string what = id + " " + parts[0];
                    Assert.AreEqual(Keys[parts[0]], check.Key, what);
                    var actual = N(parts[1]); Assert.AreEqual(actual.HasValue, check.Actual.HasValue, what + " actual defined");
                    if (actual.HasValue) Close(actual.Value, check.Actual.Value, what + " actual");
                    var limit = N(parts[2]); Assert.AreEqual(limit.HasValue, check.Limit.HasValue, what + " limit defined");
                    if (limit.HasValue) Close(limit.Value, check.Limit.Value, what + " limit");
                    Assert.AreEqual(parts[4].Length == 0 ? (bool?)null : bool.Parse(parts[4]), check.Passed, what + " passed");
                    checks++;
                }
            }
            Assert.IsTrue(checks > 1000, checks + " checks");
        }

        /// <summary>
        /// EN 1992-1-1 and UNI (DM 31/07/2012) on the column R400x400 (8Ø16, links Ø8/250) and the beam R300x500: values of the standard by hand.
        /// DS beams keep As,min and ρw,min pending (national values not available).
        /// </summary>
        [TestMethod]
        public void EurocodeAndAnnexValuesOfBeamsAndColumns()
        {
            var archive = Archive();
            var column = (ReinforcedConcreteSection)archive.BeamProperties["R400x400"]; var beam = (ReinforcedConcreteSection)archive.BeamProperties["R300x500"];
            MemberDetailingInput Column(double compression, bool lap = false) => new MemberDetailingInput(MemberDetailingKind.Column, CrackSectionGeometry.From(column), 160000, 25,
                Fctm(25), 450, 391.3, 400, 400, 400, compression, true, 8, 250, 2, 20, 35, 25, 10, lap, true, true);
            Func<MemberDetailingResult, string, DetailingCheck> Check = (res, key) => res.Checks.Single(x => x.Key == key);
            var en = MemberDetailingCalculator.Calculate(DetailingProfile.EN1992p11, Column(2e6));
            Assert.AreEqual(8, Check(en, "LongitudinalDiameter").Limit); Assert.AreEqual(Math.Max(.1 * 2e6 / 391.3, .002 * 160000), Check(en, "MinimumLongitudinal").Limit.Value, 1e-9);
            Assert.AreEqual(Math.Min(20 * 16, 400), Check(en, "LinkSpacing").Limit.Value, 1e-12); Assert.AreEqual(true, Check(en, "LinkSpacing").Passed);
            Assert.AreEqual(.6 * 320, Check(MemberDetailingCalculator.Calculate(DetailingProfile.EN1992p11, Column(2e6, true)), "LinkSpacing").Limit.Value, 1e-12, "laps: × 0.6");
            var uni = MemberDetailingCalculator.Calculate(DetailingProfile.UniEN1992p11, Column(2e6));
            Assert.AreEqual(12, Check(uni, "LongitudinalDiameter").Limit); Assert.AreEqual(Math.Max(.1 * 2e6 / 391.3, .003 * 160000), Check(uni, "MinimumLongitudinal").Limit.Value, 1e-9);
            Assert.AreEqual(Math.Min(12 * 16, 250), Check(uni, "LinkSpacing").Limit.Value, 1e-12);
            Assert.AreEqual(false, Check(uni, "LinkSpacing").Passed, "250 mm > 12 Ømin = 192 mm (DM 31/07/2012)"); Assert.AreEqual(false, uni.Passed);
            Assert.AreEqual(true, en.Passed, "EN: every rule satisfied, end zones confirmed: " + string.Join("; ", en.Checks.Select(x => x.Key + " " + x.Actual + "/" + x.Limit + " " + x.Passed)));
            var unconfirmed = new MemberDetailingInput(MemberDetailingKind.Column, CrackSectionGeometry.From(column), 160000, 25, Fctm(25), 450, 391.3, 400, 400, 400, 2e6, true, 8, 250, 2, 20,
                35, 25, 10, false, true, false);
            Assert.AreEqual(null, MemberDetailingCalculator.Calculate(DetailingProfile.EN1992p11, unconfirmed).Passed, "end zones near beams pending without confirmation");
            var ds = MemberDetailingCalculator.Calculate(DetailingProfile.DsEN1992p11, Column(2e6));
            Assert.AreEqual(Check(en, "MinimumLongitudinal").Limit, Check(ds, "MinimumLongitudinal").Limit, "DK NA 9.5.2: unchanged");

            var geometry = CrackSectionGeometry.From(beam);
            // Nominal cover 30 mm to the links: cmin,dur 15 + Δcdev 10 = 25 ≤ 30 (with cmin,dur 25 the nominal cover would fail).
            MemberDetailingInput Beam() => new MemberDetailingInput(MemberDetailingKind.Beam, geometry, 150000, 30, Fctm(30), 450, 391.3, 300, 300, 300, 0, true, 8, 200, 2, 20, 30,
                15, 10, false, true, true);
            var b = MemberDetailingCalculator.Calculate(DetailingProfile.EN1992p11, Beam());
            var bottom = Check(b, "MinimumTension:Bottom");
            double ys = geometry.Bars.Where(x => x.Y < 0).Sum(x => x.Area * x.Y) / geometry.Bars.Where(x => x.Y < 0).Sum(x => x.Area), d = 250 - ys;
            Assert.AreEqual(Math.Max(.26 * Fctm(30) / 450, .0013) * 300 * d, bottom.Limit.Value, 1e-9);
            Assert.AreEqual(.08 * Math.Sqrt(30) / 450 * 300 * 1000, Check(b, "MinimumLinks:Bottom").Limit.Value, 1e-9, "ρw,min bw in mm²/m");
            Assert.AreEqual(.75 * d, Check(b, "LinkSpacing:Bottom").Limit.Value, 1e-9);
            var dsBeam = MemberDetailingCalculator.Calculate(DetailingProfile.DsEN1992p11, Beam());
            Assert.IsNull(Check(dsBeam, "MinimumTension:Bottom").Passed); Assert.IsNull(Check(dsBeam, "MinimumLinks:Bottom").Passed); Assert.IsNull(dsBeam.Passed);
            Assert.IsTrue(Check(dsBeam, "MinimumTension:Bottom").NotImplemented && Check(dsBeam, "MinimumLinks:Top").NotImplemented);
            Assert.IsNotNull(Check(dsBeam, "LinkSpacing:Bottom").Passed, "9.2.2(6) unchanged");
        }

        /// <summary>Durability additions to cmin (uneven surface, abrasion) and the nominal cover against ground (EN 4.4.1.2(11)-(13), 4.4.1.3(4)).</summary>
        [TestMethod]
        public void CoverAdditionsAndGroundCoverAreApplied()
        {
            var beam = (ReinforcedConcreteSection)Archive().BeamProperties["R300x500"]; var geometry = CrackSectionGeometry.From(beam);
            MemberDetailingInput Beam(double cover, double addition, double ground) => new MemberDetailingInput(MemberDetailingKind.Beam, geometry, 150000, 30, Fctm(30), 450, 391.3,
                300, 300, 300, 0, true, 8, 200, 2, 20, cover, 15, 10, false, true, true, addition, ground);
            DetailingCheck Check(MemberDetailingResult res, string key) => res.Checks.Single(x => x.Key == key);
            var plain = MemberDetailingCalculator.Calculate(DetailingProfile.EN1992p11, Beam(30, 0, 0));
            Assert.AreEqual(25, Check(plain, "NominalCover").Limit.Value, 1e-12, "max(10; 15; 8) + 10");
            Assert.AreEqual("max(10; cmin,dur; cmin,b) + Δcdev", Check(plain, "NominalCover").Explanation);
            // Rough surface + XM2: +15 mm on cmin → 40 mm > 30 mm; the bar margins drop by the same 15 mm.
            var added = MemberDetailingCalculator.Calculate(DetailingProfile.EN1992p11, Beam(30, 15, 0));
            Assert.AreEqual(40, Check(added, "NominalCover").Limit.Value, 1e-12); Assert.AreEqual(false, Check(added, "NominalCover").Passed);
            Assert.AreEqual(Check(plain, "BarCoverMargin").Actual.Value - 15, Check(added, "BarCoverMargin").Actual.Value, 1e-9);
            StringAssert.Contains(Check(added, "NominalCover").Explanation, "surface and abrasion");
            // Against prepared ground: cnom ≥ 40 mm governs over 25 mm; directly against soil 75 mm also governs the bar covers.
            var prepared = MemberDetailingCalculator.Calculate(DetailingProfile.EN1992p11, Beam(40, 0, 40));
            Assert.AreEqual(40, Check(prepared, "NominalCover").Limit.Value, 1e-12); Assert.AreEqual(true, Check(prepared, "NominalCover").Passed);
            var soil = MemberDetailingCalculator.Calculate(DetailingProfile.EN1992p11, Beam(40, 0, 75));
            Assert.AreEqual(75, Check(soil, "NominalCover").Limit.Value, 1e-12); Assert.AreEqual(false, soil.Passed);
            Assert.AreEqual(geometry.Bars.Min(x => geometry.BarCover(x)) - 75, Check(soil, "BarCoverMargin").Actual.Value, 1e-9);
            // cmin + additions + Δcdev larger than the ground value: the ground does not govern.
            Assert.AreEqual(50, Check(MemberDetailingCalculator.Calculate(DetailingProfile.EN1992p11, Beam(50, 25, 40)), "NominalCover").Limit.Value, 1e-12);
            Assert.ThrowsException<ArgumentException>(() => MemberDetailingCalculator.Calculate(DetailingProfile.EN1992p11, Beam(30, -5, 0)));
            Assert.ThrowsException<ArgumentException>(() => MemberDetailingCalculator.Calculate(DetailingProfile.EN1992p11, Beam(30, 0, double.NaN)));
            // Without cmin,dur the additions are not used: the cover checks stay pending.
            var pending = MemberDetailingCalculator.Calculate(DetailingProfile.EN1992p11, new MemberDetailingInput(MemberDetailingKind.Beam, geometry, 150000, 30, Fctm(30), 450, 391.3,
                300, 300, 300, 0, true, 8, 200, 2, 20, 30, null, 10, false, true, true, 15, 75));
            Assert.IsNull(Check(pending, "NominalCover").Passed); Assert.IsFalse(pending.Checks.Any(x => x.Key == "BarCoverMargin"));
        }

        private sealed class CustomAnnex : StandardEN1992p11 { }

        [TestMethod]
        public void ProfilesAreResolvedByExactTypeWithoutFallback()
        {
            Assert.IsFalse(DetailingProfiles.TryResolve(new CustomAnnex(), out _));
            StringAssert.Contains(DetailingProfiles.NotSupportedReason(new StandardDINEN1992p11()), "national rules");
            StringAssert.Contains(DetailingProfiles.NotSupportedReason(new StandardModelCode2010()), "Model Code 2010");
            StringAssert.Contains(DetailingProfiles.NotApplicableReason(new StandardCSTR34()), "CS-TR34");
            Assert.IsTrue(DetailingProfiles.TryResolve(new StandardCNR200(), out var frp) && frp == DetailingProfile.CnrDT200);
            StringAssert.Contains(Assert.ThrowsException<NotSupportedException>(() => DetailingProfiles.Resolve(new StandardACI318p19())).Message, "future implementation");
        }

        // ---------------------------------------------------------------- solid slabs and walls (0.0.18.0, ANTHEA F2.8 L1)

        private static readonly Dictionary<string, string> PlateKeys = new Dictionary<string, string>
        {
            { "Interasse armatura principale", "MainSpacing" }, { "Armatura secondaria", "SecondaryReinforcement" },
            { "Ripartizione armatura secondaria sulle facce", "SecondaryFaceDistribution" }, { "Interasse armatura secondaria", "SecondarySpacing" },
            { "Bordi, appoggi e punzonamento", "EdgesSupportsPunching" }, { "Armatura verticale parete", "WallVertical" }, { "Armatura verticale massima", "WallVerticalMaximum" },
            { "Interasse verticale sulla sezione", "WallVerticalSpacing" }, { "Armatura orizzontale per metro", "WallHorizontal" }, { "Interasse orizzontale", "WallHorizontalSpacing" },
            { "Distribuzione e collegamenti fra facce", "WallFacesAndTies" }
        };
        private static string LibraryKey(string legacy) => Keys.TryGetValue(legacy, out var key) ? key : PlateKeys[legacy];
        private static string LibraryUnit(string legacy) => legacy.Replace("mm²", "mm2");
        private static readonly Dictionary<string, string> PlateMessages = new Dictionary<string, string>
        {
            { "Soletta piena: usare una striscia rettangolare senza foro, b = larghezza e h = spessore.", "Detailing: solid slab: use a rectangular strip without holes, b = width and h = thickness." },
            { "Dettagli: i parametri numerici devono essere finiti e non negativi.", "Detailing: numerical data must be finite and non negative." }
        };
        private static MemberDetailingKind Kind(string legacy)
            => legacy == "Column" ? MemberDetailingKind.Column : legacy == "Slab" ? MemberDetailingKind.Slab : legacy == "Wall" ? MemberDetailingKind.Wall : MemberDetailingKind.Beam;

        /// <summary>
        /// Row of detailing-plate-legacy.csv as the ANTHEA mapping passes it (F2.8 §5 B2, M12): cmin,dur null when not finite, Fctm of the legacy formula,
        /// plate data for every kind, no cover additions.
        /// </summary>
        private static MemberDetailingInput PlateInput(string[] c, ReinforcedConcreteSection section, bool legacyNegativeLinkLegs)
        {
            double durability = D(c[19]);
            return new MemberDetailingInput(Kind(c[2]), CrackSectionGeometry.From(section), D(c[6]), D(c[3]), Fctm(D(c[3])), D(c[4]), D(c[5]), D(c[9]), D(c[10]), D(c[11]),
                D(c[13]), bool.Parse(c[14]), D(c[15]), D(c[16]), int.Parse(c[17]), D(c[18]), D(c[12]),
                double.IsNaN(durability) || double.IsInfinity(durability) ? (double?)null : durability, D(c[20]), bool.Parse(c[21]), bool.Parse(c[22]), bool.Parse(c[23]), 0, 0,
                new MemberDetailingOptions(new PlateDetailingData(D(c[24]), D(c[25]), bool.Parse(c[26])), legacyNegativeLinkLegs));
        }

        /// <summary>
        /// ANTHEA ConcreteDetailingCalculator on solid slab strips, walls, slab rejections and rows with −2 link legs (detailing-plate-legacy.csv,
        /// sections detailing-plate-sections.xml; F2.8 A0 capture F2-pre-f28 a/tutte, ANTHEA 1baeb60): same keys in the same order,
        /// same verdicts and units, Actual and Limit within 1e-9, rejections on the same rows with the corresponding message. The rows with −2 legs
        /// are reproduced with <see cref="MemberDetailingOptions.LegacyNegativeLinkLegs"/> and rejected without it.
        /// </summary>
        [TestMethod]
        public void LegacyPlateDetailingIsReproduced()
        {
            GPC.Model.Models.Model archive;
            using (var stream = File.OpenRead(Path.Combine(Folder, "detailing-plate-sections.xml"))) archive = ModelArchive.Load(stream);
            var lines = Rows("detailing-plate-legacy.csv");
            Assert.IsTrue(lines[0].StartsWith("id;section;kind;"), "header");
            var rows = lines.Skip(1).ToArray();
            Assert.AreEqual(282, rows.Length);
            int checks = 0, errors = 0, negativeLegs = 0, pending = 0, calculated = 0;
            var perKey = new Dictionary<string, int>();
            foreach (var row in rows)
            {
                var c = row.Split(';'); string id = "row " + c[0] + " " + c[1] + " " + c[2];
                var section = (ReinforcedConcreteSection)archive.BeamProperties[c[1]];
                if (c[27] != "ok")
                {
                    Assert.AreEqual("error:ArgumentException", c[27], id);
                    var ex = Assert.ThrowsException<ArgumentException>(() => MemberDetailingCalculator.Calculate(DetailingProfile.Ntc2018, PlateInput(c, section, true)), id);
                    Assert.AreEqual(PlateMessages[Uri.UnescapeDataString(c[28])], ex.Message, id);
                    errors++; continue;
                }
                if (int.Parse(c[17]) < 0)
                {
                    var ex = Assert.ThrowsException<ArgumentException>(() => MemberDetailingCalculator.Calculate(DetailingProfile.Ntc2018, PlateInput(c, section, false)), id + " without the legacy option");
                    Assert.AreEqual("Detailing: numerical data must be finite and non negative.", ex.Message);
                    negativeLegs++;
                }
                var r = MemberDetailingCalculator.Calculate(DetailingProfile.Ntc2018, PlateInput(c, section, true));
                Assert.AreEqual(Kind(c[2]), r.Kind, id); calculated++;
                var legacy = c[28].Split('|');
                Assert.AreEqual(legacy.Length, r.Checks.Count, id + ": " + string.Join(",", r.Checks.Select(x => x.Key)));
                for (int i = 0; i < legacy.Length; i++)
                {
                    var parts = legacy[i].Split(':').Select(Uri.UnescapeDataString).ToArray(); var check = r.Checks[i]; string what = id + " " + parts[0];
                    Assert.AreEqual(LibraryKey(parts[0]), check.Key, what);
                    var actual = N(parts[1]); Assert.AreEqual(actual.HasValue, check.Actual.HasValue, what + " actual defined");
                    if (actual.HasValue) Close(actual.Value, check.Actual.Value, what + " actual");
                    var limit = N(parts[2]); Assert.AreEqual(limit.HasValue, check.Limit.HasValue, what + " limit defined");
                    if (limit.HasValue) Close(limit.Value, check.Limit.Value, what + " limit");
                    Assert.AreEqual(LibraryUnit(parts[3]), check.Unit, what + " unit");
                    Assert.AreEqual(parts[4].Length == 0 ? (bool?)null : bool.Parse(parts[4]), check.Passed, what + " passed");
                    Assert.IsFalse(check.NotImplemented, what);
                    if (check.Passed == null) pending++;
                    perKey[check.Key] = perKey.TryGetValue(check.Key, out var n) ? n + 1 : 1;
                    checks++;
                }
            }
            Assert.AreEqual(10, errors, "rejected rows: 7 slab outlines, 3 numerical");
            Assert.AreEqual(12, negativeLegs, "rows with −2 legs");
            // Every rule of slabs and walls is met, pending branches included.
            foreach (var key in PlateKeys.Values) Assert.IsTrue(perKey.ContainsKey(key), key + " not covered");
            foreach (var key in new[] { "MaximumAtLap", "BarsHeldByLinks", "MinimumLinks:Bottom", "LongitudinalSpacing" }) Assert.IsTrue(perKey.ContainsKey(key), key + " not covered");
            Assert.AreEqual(272, calculated, "143 slabs, 123 walls, 3 beams, 3 columns");
            Assert.AreEqual(2610, checks); Assert.AreEqual(701, pending);
        }

        private static CrackSectionGeometry Strip(double width, double thickness, double y, int count, double diameter, bool topToo)
        {
            var bars = new List<CrackBar>();
            double area = Math.PI * diameter * diameter / 4, step = (width - 200) / (count - 1);
            foreach (double yy in topToo ? new[] { -y, y } : new[] { -y })
                for (int i = 0; i < count; i++) bars.Add(new CrackBar(-width / 2 + 100 + i * step, yy, diameter, area));
            return new CrackSectionGeometry(new[] { new Point2d(-width / 2, -thickness / 2), new Point2d(width / 2, -thickness / 2), new Point2d(width / 2, thickness / 2),
                new Point2d(-width / 2, thickness / 2) }, null, bars, CrackBarLayout.Rows);
        }

        /// <summary>
        /// Solid slab and wall by hand (EC2 §§9.3.1.1, 9.6.2, 9.6.3 with NTC 2018), independent of ANTHEA: strip 1000 × 200 mm, C30/37 (fctm = 0.3·30^(2/3)
        /// = 2.8965 MPa), B450C, 5Ø12 per face at y = ±69 mm (x = −400…400, step 200), cnom = 25 mm, no links, dg = 20 mm, cmin,dur = 15 mm, Δcdev = 10 mm.
        /// </summary>
        [TestMethod]
        public void SlabAndWallMatchHandCalculation()
        {
            var g = Strip(1000, 200, 69, 5, 12, true);
            double bar = Math.PI * 144 / 4;                       // 113.097 mm²
            double fctm = .3 * Math.Pow(30, 2d / 3);               // 2.8965 MPa
            MemberDetailingInput Input(MemberDetailingKind kind, double secondary, double spacing, bool critical, bool lap, bool links, bool restrained, bool legacyLegs = false, int legs = 2)
                => new MemberDetailingInput(kind, g, 200000, 30, fctm, 450, 391.3, 1000, 1000, 1000, 0, links, 8, 200, legs, 20, 25, 15, 10, lap, restrained, true, 0, 0,
                    new MemberDetailingOptions(new PlateDetailingData(secondary, spacing, critical), legacyLegs));
            DetailingCheck Check(MemberDetailingResult res, string key) => res.Checks.Single(x => x.Key == key);

            var slab = MemberDetailingCalculator.Calculate(DetailingProfile.Ntc2018, Input(MemberDetailingKind.Slab, 300, 250, false, false, false, true));
            CollectionAssert.AreEqual(new[] { "ClearSpacing", "NominalCover", "BarCoverMargin", "MinimumTension:Bottom", "MaximumTension:Bottom", "MinimumTension:Top",
                "MaximumTension:Top", "MainSpacing", "SecondaryReinforcement", "SecondaryFaceDistribution", "SecondarySpacing", "EdgesSupportsPunching" },
                slab.Checks.Select(x => x.Key).ToArray());
            // Clear spacing: 2·69 − 12 = 126 mm ≥ max(20; 12; 25) = 25 mm. Cover: max(10; 15; 12) + 10 = 25 mm = cnom; bar margin 31 − 6 − 25 = 0.
            Assert.AreEqual(126, Check(slab, "ClearSpacing").Actual.Value, 1e-9); Assert.AreEqual(25, Check(slab, "ClearSpacing").Limit.Value, 1e-12);
            Assert.AreEqual(25, Check(slab, "NominalCover").Limit.Value, 1e-12); Assert.AreEqual(true, Check(slab, "NominalCover").Passed);
            Assert.AreEqual(0, Check(slab, "BarCoverMargin").Actual.Value, 1e-9);
            // As,min = max(0.26·2.8965/450; 0.0013)·1000·169 = 0.0016735·169000 = 282.82 mm² ≤ 5·113.10 = 565.49 mm²; As,max = 0.04·200000 = 8000 mm².
            Assert.AreEqual(.26 * fctm / 450 * 1000 * 169, Check(slab, "MinimumTension:Bottom").Limit.Value, 1e-9);
            Assert.AreEqual(282.82, Check(slab, "MinimumTension:Top").Limit.Value, .01);
            Assert.AreEqual(5 * bar, Check(slab, "MinimumTension:Top").Actual.Value, 1e-9);
            Assert.AreEqual(8000, Check(slab, "MaximumTension:Bottom").Limit.Value, 1e-9);
            // Main spacing 200 mm ≤ min(3·200; 400) = 400 mm; secondary 300 ≥ 0.20·(10·113.10·1000/1000) = 226.19 mm²/m; spacing 250 ≤ min(3.5·200; 450) = 450.
            Assert.AreEqual(200, Check(slab, "MainSpacing").Actual.Value, 1e-9); Assert.AreEqual(400, Check(slab, "MainSpacing").Limit.Value, 1e-12);
            Assert.AreEqual(226.19, Check(slab, "SecondaryReinforcement").Limit.Value, .01); Assert.AreEqual(true, Check(slab, "SecondaryReinforcement").Passed);
            Assert.AreEqual("mm2/m", Check(slab, "SecondaryReinforcement").Unit);
            Assert.AreEqual(450, Check(slab, "SecondarySpacing").Limit.Value, 1e-12);
            Assert.IsNull(Check(slab, "SecondaryFaceDistribution").Passed); Assert.IsNull(Check(slab, "SecondaryFaceDistribution").Limit);
            Assert.IsNull(slab.Passed, "pending checks, none failed");
            // Critical region: main ≤ min(2·200; 250) = 250, secondary spacing ≤ min(3·200; 400) = 400; secondary 200 < 226.19 fails; spacing 0 = pending.
            var criticalSlab = MemberDetailingCalculator.Calculate(DetailingProfile.CnrDT200, Input(MemberDetailingKind.Slab, 200, 0, true, true, true, false));
            Assert.AreEqual(250, Check(criticalSlab, "MainSpacing").Limit.Value, 1e-12); Assert.AreEqual(false, Check(criticalSlab, "SecondaryReinforcement").Passed);
            Assert.IsNull(Check(criticalSlab, "SecondarySpacing").Limit); Assert.IsNull(Check(criticalSlab, "SecondarySpacing").Passed);
            Assert.IsFalse(criticalSlab.Checks.Any(x => x.Key.StartsWith("MaximumTension") || x.Key.StartsWith("MinimumLinks") || x.Key == "CompressionBarRestraint" || x.Key == "MaximumAtLap"),
                "lap zone: no As,max; no link checks, no restraint, no lap maximum for slabs");
            Assert.AreEqual("BarsHeldByLinks", criticalSlab.Checks.Last().Key, "links with unconfirmed restraint");
            Assert.AreEqual(false, criticalSlab.Passed);

            var wall = MemberDetailingCalculator.Calculate(DetailingProfile.Ntc2018, Input(MemberDetailingKind.Wall, 300, 250, false, false, false, true));
            CollectionAssert.AreEqual(new[] { "ClearSpacing", "NominalCover", "BarCoverMargin", "WallVertical", "WallVerticalMaximum", "WallVerticalSpacing", "WallHorizontal",
                "WallHorizontalSpacing", "WallFacesAndTies" }, wall.Checks.Select(x => x.Key).ToArray());
            // As,v = 10·113.10 = 1130.97 mm² ≥ 0.002·200000 = 400 mm², ≤ 0.04·200000 = 8000 mm²; t = 200, l = 1000: spacing 200 ≤ min(600; 400);
            // As,h ≥ max(0.25·1130.97·1000/1000; 0.001·200·1000) = max(282.74; 200) = 282.74 mm²/m; spacing 250 ≤ 400.
            Assert.AreEqual(10 * bar, Check(wall, "WallVertical").Actual.Value, 1e-9); Assert.AreEqual(400, Check(wall, "WallVertical").Limit.Value, 1e-12);
            Assert.AreEqual(8000, Check(wall, "WallVerticalMaximum").Limit.Value, 1e-12); Assert.AreEqual(400, Check(wall, "WallVerticalSpacing").Limit.Value, 1e-12);
            Assert.AreEqual(282.74, Check(wall, "WallHorizontal").Limit.Value, .01); Assert.AreEqual(true, Check(wall, "WallHorizontal").Passed);
            Assert.AreEqual(400, Check(wall, "WallHorizontalSpacing").Limit.Value, 1e-12);
            // Lap zone: no maximum outside laps, As ≤ 0.08 Ac = 16000 mm² at laps; horizontal steel 250 < 282.74 fails; spacing 0 = pending.
            var lapped = MemberDetailingCalculator.Calculate(DetailingProfile.Ntc2018, Input(MemberDetailingKind.Wall, 250, 0, false, true, false, true));
            Assert.AreEqual(16000, Check(lapped, "MaximumAtLap").Limit.Value, 1e-9); Assert.IsFalse(lapped.Checks.Any(x => x.Key == "WallVerticalMaximum"));
            Assert.AreEqual(false, Check(lapped, "WallHorizontal").Passed); Assert.IsNull(Check(lapped, "WallHorizontalSpacing").Passed);
            // A wall 200 × 1000 (thickness along x) gives the same limits.
            var g2 = new CrackSectionGeometry(g.Outline.Select(v => new Point2d(v.Y, v.X)), null, g.Bars.Select(b => new CrackBar(b.Y, b.X, b.Diameter, b.Area)), CrackBarLayout.Rows);
            var turned = MemberDetailingCalculator.Calculate(DetailingProfile.Ntc2018, new MemberDetailingInput(MemberDetailingKind.Wall, g2, 200000, 30, fctm, 450, 391.3, 200, 200, 200,
                0, false, 8, 200, 2, 20, 25, 15, 10, false, true, true, 0, 0, new MemberDetailingOptions(new PlateDetailingData(300, 250, false), false)));
            Assert.AreEqual(Check(wall, "WallHorizontal").Limit.Value, Check(turned, "WallHorizontal").Limit.Value, 1e-9);
            Assert.AreEqual(400, Check(turned, "WallVerticalSpacing").Limit.Value, 1e-12);

            // Thin members, where the thickness governs the four spacing limits: strip 1000 × 120 mm, 5Ø12 per face at y = ±29 mm (cover 60 − 29 − 6 = 25 mm).
            // Slab, h = 120: main spacing min(3·120; 400) = 360 mm, critical region min(2·120; 250) = 240 mm; secondary spacing min(3.5·120; 450) = 420 mm,
            // critical region min(3·120; 400) = 360 mm. Wall 120 × 1000 (t = 120, l = 1000): vertical spacing min(3·120; 400) = 360 mm, also with the
            // thickness along x. Actual main and vertical spacing 200 mm, secondary 250 mm.
            var thin = Strip(1000, 120, 29, 5, 12, true);
            var thinTurned = new CrackSectionGeometry(thin.Outline.Select(v => new Point2d(v.Y, v.X)), null, thin.Bars.Select(b => new CrackBar(b.Y, b.X, b.Diameter, b.Area)),
                CrackBarLayout.Rows);
            MemberDetailingResult Thin(MemberDetailingKind kind, CrackSectionGeometry geometry, double width, bool critical)
                => MemberDetailingCalculator.Calculate(DetailingProfile.Ntc2018, new MemberDetailingInput(kind, geometry, 120000, 30, fctm, 450, 391.3, width, width, width, 0, false, 8,
                    200, 2, 20, 25, 15, 10, false, true, true, 0, 0, new MemberDetailingOptions(new PlateDetailingData(300, 250, critical), false)));
            var thinSlab = Thin(MemberDetailingKind.Slab, thin, 1000, false);
            Assert.AreEqual(360, Check(thinSlab, "MainSpacing").Limit.Value, 1e-12); Assert.AreEqual(420, Check(thinSlab, "SecondarySpacing").Limit.Value, 1e-12);
            Assert.AreEqual(200, Check(thinSlab, "MainSpacing").Actual.Value, 1e-9); Assert.AreEqual(true, Check(thinSlab, "MainSpacing").Passed);
            var thinCritical = Thin(MemberDetailingKind.Slab, thin, 1000, true);
            Assert.AreEqual(240, Check(thinCritical, "MainSpacing").Limit.Value, 1e-12); Assert.AreEqual(360, Check(thinCritical, "SecondarySpacing").Limit.Value, 1e-12);
            Assert.AreEqual(true, Check(thinCritical, "SecondarySpacing").Passed);
            var thinWall = Thin(MemberDetailingKind.Wall, thin, 1000, false);
            Assert.AreEqual(360, Check(thinWall, "WallVerticalSpacing").Limit.Value, 1e-12); Assert.AreEqual(200, Check(thinWall, "WallVerticalSpacing").Actual.Value, 1e-9);
            Assert.AreEqual(360, Check(Thin(MemberDetailingKind.Wall, thinTurned, 120, false), "WallVerticalSpacing").Limit.Value, 1e-12);
        }

        /// <summary>Options, profiles, rejections and the named legacy option of the link legs.</summary>
        [TestMethod]
        public void PlateOptionsAndRejections()
        {
            var g = Strip(1000, 200, 69, 5, 12, true); double fctm = .3 * Math.Pow(30, 2d / 3);
            MemberDetailingInput Input(MemberDetailingKind kind, MemberDetailingOptions options, int legs = 2, CrackSectionGeometry geometry = null)
                => new MemberDetailingInput(kind, geometry ?? g, 200000, 30, fctm, 450, 391.3, 1000, 1000, 1000, 0, true, 8, 200, legs, 20, 25, 15, 10, false, true, true, 0, 0, options);
            var plate = new MemberDetailingOptions(new PlateDetailingData(300, 250, false), false);
            // Slab and wall: NTC 2018 and CNR-DT 200 only; plate data required; null options rejected.
            foreach (var profile in new[] { DetailingProfile.EN1992p11, DetailingProfile.UniEN1992p11, DetailingProfile.DsEN1992p11 })
                foreach (var kind in new[] { MemberDetailingKind.Slab, MemberDetailingKind.Wall })
                    Assert.AreEqual("Detailing: slab and wall rules are implemented for NTC 2018 only.",
                        Assert.ThrowsException<NotSupportedException>(() => MemberDetailingCalculator.Calculate(profile, Input(kind, plate))).Message);
            Assert.ThrowsException<ArgumentException>(() => Input(MemberDetailingKind.Slab, MemberDetailingOptions.Default));
            Assert.ThrowsException<ArgumentException>(() => new MemberDetailingInput(MemberDetailingKind.Wall, g, 200000, 30, fctm, 450, 391.3, 1000, 1000, 1000, 0, true, 8, 200, 2, 20,
                25, 15, 10, false, true, true));
            Assert.AreEqual("options", Assert.ThrowsException<ArgumentNullException>(() => Input(MemberDetailingKind.Beam, null)).ParamName);
            // Defaults of the constructor of 0.0.17.0.
            var old = new MemberDetailingInput(MemberDetailingKind.Beam, g, 200000, 30, fctm, 450, 391.3, 1000, 1000, 1000, 0, true, 8, 200, 2, 20, 25, 15, 10, false, true, true);
            Assert.AreSame(MemberDetailingOptions.Default, old.Options); Assert.IsNull(old.Plate);
            Assert.IsNull(MemberDetailingOptions.Default.Plate); Assert.IsFalse(MemberDetailingOptions.Default.LegacyNegativeLinkLegs);
            // Slab outline: holes, T, circles and rotated rectangles are rejected before the numerical validation.
            var holed = new CrackSectionGeometry(g.Outline, new[] { new[] { new Point2d(-100, -20), new Point2d(100, -20), new Point2d(100, 20), new Point2d(-100, 20) } }, g.Bars, CrackBarLayout.Rows);
            var rotated = new CrackSectionGeometry(new[] { new Point2d(0, -510), new Point2d(510, 0), new Point2d(0, 510), new Point2d(-510, 0) }, null, g.Bars, CrackBarLayout.Rows);
            var closed = new CrackSectionGeometry(new[] { new Point2d(-500, -100), new Point2d(500, -100), new Point2d(500, 100), new Point2d(500, 100), new Point2d(-500, 100),
                new Point2d(-500, -100) }, null, g.Bars, CrackBarLayout.Rows);
            var tee = new CrackSectionGeometry(new[] { new Point2d(-500, 0), new Point2d(500, 0), new Point2d(500, 100), new Point2d(100, 100), new Point2d(100, 300),
                new Point2d(-100, 300), new Point2d(-100, 100), new Point2d(-500, 100) }, null, g.Bars, CrackBarLayout.Rows);
            foreach (var shape in new[] { holed, rotated, tee })
            {
                var invalid = new MemberDetailingOptions(new PlateDetailingData(-1, double.NaN, false), false);
                Assert.AreEqual("Detailing: solid slab: use a rectangular strip without holes, b = width and h = thickness.",
                    Assert.ThrowsException<ArgumentException>(() => MemberDetailingCalculator.Calculate(DetailingProfile.Ntc2018, Input(MemberDetailingKind.Slab, invalid, -3, shape))).Message);
                Assert.IsNotNull(MemberDetailingCalculator.Calculate(DetailingProfile.Ntc2018, Input(MemberDetailingKind.Wall, plate, 2, shape)), "walls: no outline check");
            }
            Assert.IsNotNull(MemberDetailingCalculator.Calculate(DetailingProfile.Ntc2018, Input(MemberDetailingKind.Slab, plate, 2, closed)), "repeated and closing vertices: still a rectangle");
            // Plate data validated for every kind when given (ANTHEA validates the secondary data of beams and columns too); beams unchanged otherwise.
            foreach (var kind in new[] { MemberDetailingKind.Beam, MemberDetailingKind.Column, MemberDetailingKind.Slab, MemberDetailingKind.Wall })
                foreach (var data in new[] { new PlateDetailingData(-1, 0, false), new PlateDetailingData(0, -1, false), new PlateDetailingData(double.NaN, 0, false),
                    new PlateDetailingData(0, double.PositiveInfinity, true) })
                    Assert.ThrowsException<ArgumentException>(() => MemberDetailingCalculator.Calculate(DetailingProfile.Ntc2018, Input(kind, new MemberDetailingOptions(data, false))), kind.ToString());
            string Describe(MemberDetailingResult res) => string.Join("|", res.Checks.Select(x => x.Key + ":" + x.Actual + ":" + x.Limit + ":" + x.Passed + ":" + x.Reference + ":" + x.Explanation));
            foreach (var profile in (DetailingProfile[])Enum.GetValues(typeof(DetailingProfile)))
                foreach (var kind in new[] { MemberDetailingKind.Beam, MemberDetailingKind.Column })
                    Assert.AreEqual(Describe(MemberDetailingCalculator.Calculate(profile, Input(kind, MemberDetailingOptions.Default))),
                        Describe(MemberDetailingCalculator.Calculate(profile, Input(kind, plate))), profile + " " + kind + ": plate data do not change beams and columns");

            // Negative legs: rejected by default, used as they are with the named legacy option (Ast/s < 0: minimum links not satisfied).
            var legacy = new MemberDetailingOptions(null, true);
            Assert.ThrowsException<ArgumentException>(() => MemberDetailingCalculator.Calculate(DetailingProfile.Ntc2018, Input(MemberDetailingKind.Beam, MemberDetailingOptions.Default, -2)));
            var beam = MemberDetailingCalculator.Calculate(DetailingProfile.Ntc2018, Input(MemberDetailingKind.Beam, legacy, -2));
            Assert.AreEqual(-2 * Math.PI * 64 / 4 * 1000 / 200, beam.Checks.Single(x => x.Key == "MinimumLinks:Bottom").Actual.Value, 1e-9);
            Assert.AreEqual(false, beam.Checks.Single(x => x.Key == "MinimumLinks:Top").Passed);
            var en = MemberDetailingCalculator.Calculate(DetailingProfile.EN1992p11, Input(MemberDetailingKind.Beam, legacy, -2));
            Assert.IsNull(en.Checks.Single(x => x.Key == "LinkLegSpacing:Bottom").Passed, "Eurocode: fewer than two legs, transverse spacing pending");
            // With non-negative legs the option changes nothing.
            foreach (var profile in (DetailingProfile[])Enum.GetValues(typeof(DetailingProfile)))
                Assert.AreEqual(Describe(MemberDetailingCalculator.Calculate(profile, Input(MemberDetailingKind.Beam, MemberDetailingOptions.Default, 4))),
                    Describe(MemberDetailingCalculator.Calculate(profile, Input(MemberDetailingKind.Beam, legacy, 4))), profile.ToString());
            // A kind value that is not defined (outside 0-3) is a beam, as in 0.0.17.0.
            foreach (var undefined in new[] { (MemberDetailingKind)9, (MemberDetailingKind)(-1) })
                Assert.AreEqual(Describe(MemberDetailingCalculator.Calculate(DetailingProfile.Ntc2018, Input(MemberDetailingKind.Beam, MemberDetailingOptions.Default))),
                    Describe(MemberDetailingCalculator.Calculate(DetailingProfile.Ntc2018, Input(undefined, MemberDetailingOptions.Default))), undefined.ToString());
            // 2 and 3, not defined in 0.0.17.0 (beams there), are now Slab and Wall: with the constructor of 0.0.17.0 they need the plate data.
            foreach (int value in new[] { 2, 3 })
                Assert.AreEqual("Detailing: slabs and walls need the plate data (secondary reinforcement, spacing, critical region).",
                    Assert.ThrowsException<ArgumentException>(() => new MemberDetailingInput((MemberDetailingKind)value, g, 200000, 30, fctm, 450, 391.3, 1000, 1000, 1000, 0, true, 8,
                        200, 2, 20, 25, 15, 10, false, true, true)).Message, value.ToString());
        }

        // ---------------------------------------------------------------- complete bond strength (0.0.18.0, ANTHEA F2.8 L2)

        private static readonly Dictionary<string, string> BondMessages = new Dictionary<string, string>
        {
            { "Controllare fck (> 0), αct (0 < αct ≤ 1) e γc (≥ 1).", "Bond: check fck (> 0), αct (0 < αct ≤ 1) and γc (≥ 1)." },
            { "Aderenza: controllare diametro, resistenza e coefficienti.", "Bond: check diameter, strength and coefficients." }
        };
        private static void Bits(string expected, double actual, string what)
            => Assert.AreEqual(BitConverter.DoubleToInt64Bits(D(expected)), BitConverter.DoubleToInt64Bits(actual), what + ": " + expected + " / " + actual.ToString("R", CultureInfo.InvariantCulture));

        /// <summary>
        /// ANTHEA ConcreteBond.Calculate (bond-legacy.csv; F2.8 A0 capture F2-pre-f28 a/tutte, ANTHEA 1baeb60): fctk,0.05 with the C60/75
        /// limit, fctd, η2 and fbd identical bit for bit; the two rejections (data of the first stage, then those of the bond strength) on the same rows with
        /// the corresponding message, both <see cref="ArgumentException"/>.
        /// </summary>
        [TestMethod]
        public void LegacyBondIsReproduced()
        {
            var lines = Rows("bond-legacy.csv");
            Assert.IsTrue(lines[0].StartsWith("id;block;class;fck;"), "header");
            int ok = 0, first = 0, second = 0, capped = 0;
            foreach (var row in lines.Skip(1))
            {
                var c = row.Split(';'); string id = "bond " + c[0] + " block " + c[1] + " fck " + c[3] + " Ø " + c[4] + " αct " + c[6] + " γc " + c[7];
                double fck = D(c[3]), diameter = D(c[4]), eta1 = D(c[5]), alpha = D(c[6]), gamma = D(c[7]);
                if (c[8] != "ok")
                {
                    Assert.AreEqual("error:ArgumentException", c[8], id);
                    var ex = Assert.ThrowsException<ArgumentException>(() => AnchorageCalculator.Bond(fck, diameter, eta1, alpha, gamma), id);
                    Assert.AreEqual(BondMessages[Uri.UnescapeDataString(c[13])], ex.Message, id);
                    if (c[13].StartsWith("Controllare")) first++; else second++;
                    continue;
                }
                var r = AnchorageCalculator.Bond(fck, diameter, eta1, alpha, gamma);
                Bits(c[9], r.Fctk05, id + " fctk,0.05"); Bits(c[10], r.Fctd, id + " fctd"); Bits(c[11], r.Eta2, id + " η2"); Bits(c[12], r.Fbd, id + " fbd");
                Assert.AreEqual(fck > 60, r.Capped, id + " capped"); Assert.AreEqual(eta1, r.Eta1, id);
                Assert.AreEqual(BitConverter.DoubleToInt64Bits(r.Fbd), BitConverter.DoubleToInt64Bits(AnchorageCalculator.BondStrength(r.Fctk05, diameter, eta1, alpha, gamma)), id);
                if (r.Capped) capped++;
                ok++;
            }
            Assert.AreEqual(712, ok); Assert.AreEqual(746, first); Assert.AreEqual(460, second); Assert.IsTrue(capped > 50, capped + " capped");
        }

        /// <summary>
        /// fctk,0.05 of the bond: equal to Model's |fctk,0.05| of ConcreteMaterialEN1992(min(fck; 60)) for the four diagrams (it does not depend on the
        /// diagram) on fck 12…90 step 0.5, and to the uncapped value without the limit. By hand (EC2 Table 3.1): fctm = 0.30 fck^(2/3) up to C50/60,
        /// 2.12 ln(1 + fcm/10) beyond; C30/37: 0.7·2.8965 = 2.0276 MPa; C60/75 (and every class above with the limit): 0.7·4.3547 = 3.0483 MPa;
        /// C70/85 without the limit: 0.7·4.6105 = 3.2273 MPa; C90/105: 0.7·5.0446 = 3.5312 MPa.
        /// </summary>
        [TestMethod]
        public void BondFctk05MatchesModelAndHandCalculation()
        {
            Assert.AreEqual(60, AnchorageCalculator.BondStrengthClassLimit);
            var diagrams = new[] { ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle, ConcreteMaterial.CompressionStressStrainDiagrams.Bilinear,
                ConcreteMaterial.CompressionStressStrainDiagrams.StressBlock, ConcreteMaterial.CompressionStressStrainDiagrams.NonLinear };
            int count = 0;
            for (double fck = 12; fck <= 90; fck += .5)
            {
                double capped = AnchorageCalculator.BondFctk05(fck), free = AnchorageCalculator.BondFctk05(fck, false);
                Assert.AreEqual(capped, AnchorageCalculator.BondFctk05(fck, true));
                foreach (var d in diagrams)
                {
                    Assert.AreEqual(BitConverter.DoubleToInt64Bits(Math.Abs(new ConcreteMaterialEN1992("C", Math.Min(fck, 60), d).Fctk05)), BitConverter.DoubleToInt64Bits(capped), fck + " " + d);
                    Assert.AreEqual(BitConverter.DoubleToInt64Bits(Math.Abs(new ConcreteMaterialEN1992("C", fck, d).Fctk05)), BitConverter.DoubleToInt64Bits(free), fck + " " + d + " without the limit");
                    count++;
                }
                if (fck <= 60) Assert.AreEqual(capped, free, fck.ToString());
            }
            Assert.AreEqual(157 * 4, count);
            Assert.AreEqual(.7 * .3 * Math.Pow(30, 2d / 3), AnchorageCalculator.BondFctk05(30), 1e-9);
            Assert.AreEqual(2.0276, AnchorageCalculator.BondFctk05(30), 1e-4);
            Assert.AreEqual(3.0483, AnchorageCalculator.BondFctk05(70), 1e-4); Assert.AreEqual(3.0483, AnchorageCalculator.BondFctk05(90), 1e-4);
            Assert.AreEqual(3.2273, AnchorageCalculator.BondFctk05(70, false), 1e-4); Assert.AreEqual(3.5312, AnchorageCalculator.BondFctk05(90, false), 1e-4);
            // Bond by hand: C70/85, Ø 40, poor bond, αct 0.85, γc 1.5: fctk,0.05 = 3.0483 (limited), fctd = 0.85·3.0483/1.5 = 1.7274 MPa,
            // η2 = (132 − 40)/100 = 0.92, fbd = 2.25·0.7·0.92·0.85·3.0483/1.5 = 2.5030 MPa; without the limit fbd = 2.25·0.7·0.92·0.85·3.2273/1.5 = 2.6500 MPa.
            var b = AnchorageCalculator.Bond(70, 40, .7, .85, 1.5);
            Assert.IsTrue(b.Capped); Assert.AreEqual(1.7274, b.Fctd, 1e-4); Assert.AreEqual(.92, b.Eta2, 1e-12); Assert.AreEqual(2.5030, b.Fbd, 1e-4); Assert.AreEqual(.7, b.Eta1);
            var free70 = AnchorageCalculator.Bond(70, 40, .7, .85, 1.5, false);
            Assert.IsFalse(free70.Capped); Assert.AreEqual(2.6500, free70.Fbd, 1e-4);
            Assert.IsFalse(AnchorageCalculator.Bond(60, 16, 1, 1, 1.5).Capped, "C60/75 is not above the limit");
            // Stage order: αct ≤ 0 and γc NaN pass the first stage and are rejected by the bond strength; αct > 1 and γc < 1 by the first stage.
            Assert.AreEqual("Bond: check diameter, strength and coefficients.", Assert.ThrowsException<ArgumentException>(() => AnchorageCalculator.Bond(30, 16, 1, 0, 1.5)).Message);
            Assert.AreEqual("Bond: check diameter, strength and coefficients.", Assert.ThrowsException<ArgumentException>(() => AnchorageCalculator.Bond(30, 16, 1, 1, double.NaN)).Message);
            Assert.AreEqual("Bond: check diameter, strength and coefficients.", Assert.ThrowsException<ArgumentException>(() => AnchorageCalculator.Bond(30, 132, 1, 1, 1.5)).Message);
            Assert.AreEqual("Bond: check fck (> 0), αct (0 < αct ≤ 1) and γc (≥ 1).", Assert.ThrowsException<ArgumentException>(() => AnchorageCalculator.Bond(30, 16, 1, 1.01, 1.5)).Message);
            Assert.AreEqual("Bond: check fck (> 0), αct (0 < αct ≤ 1) and γc (≥ 1).", Assert.ThrowsException<ArgumentException>(() => AnchorageCalculator.Bond(30, 16, 1, 1, .99)).Message);
            Assert.AreEqual("Bond: check fck (> 0), αct (0 < αct ≤ 1) and γc (≥ 1).", Assert.ThrowsException<ArgumentException>(() => AnchorageCalculator.Bond(double.PositiveInfinity, 16, 1, 1, 1.5)).Message);
        }

        // ---------------------------------------------------------------- moment-curvature: units, structured outcome, lazy limit, typed rejections (0.0.18.0, F2.8 L3)

        private static T InCulture<T>(string culture, Func<T> action)
        {
            var thread = System.Threading.Thread.CurrentThread; var culture0 = thread.CurrentCulture;
            try { thread.CurrentCulture = CultureInfo.GetCultureInfo(culture); return action(); }
            finally { thread.CurrentCulture = culture0; }
        }

        /// <summary>Status rebuilt from the structured fields only, as ANTHEA rebuilds its Italian text (MomentCurvature.cs:40, :54, :65, :67, :69).</summary>
        private static string StatusFromFields(MomentCurvatureResult r, MomentCurvatureUnits units)
        {
            var s = new StringBuilder("Increasing-moment branch at constant N; material laws and factors of the section and standard.");
            if (r.InterruptedAtStep.HasValue) s.Append(" Interrupted at step " + r.InterruptedAtStep.Value + ": " + r.InterruptionMessage);
            else Assert.IsNull(r.InterruptionMessage);
            var y = r.YieldRefinement;
            if (y != null)
                s.Append(y.Applied ? $" First yield refined with {y.Bisections} bisections, My = {y.Moment.Value:G7} {units.Moment}."
                    : " Yield taken at the first sample: refinement interrupted, " + y.InterruptionMessage);
            s.Append($" N at the limit point = {r.LimitAxialForce:G9} {units.Force}; residual = {r.AxialResidual:G6} {units.Force}. No post-peak branch.");
            return s.ToString();
        }

        /// <summary>
        /// Analytic case of the contract that the request rejects. The contract (CL0, kept as captured) also flags "zero limit moment", which only adds
        /// to its count of rejections: cos 90° = 6.1e-17, so the limit moment is tiny but positive and the curve is calculated.
        /// </summary>
        private static bool Rejects(DetailingContractTests.AnalyticCase a) => a.Rejection && a.Name != "zero limit moment";

        /// <summary>
        /// InterruptedAtStep, InterruptionMessage and YieldRefinement give back the whole Status (invariant and Italian culture) of the analytic curves of the
        /// contract and of the 5 curves of curvature-legacy.csv, so a caller rebuilds its own text without reading the English one.
        /// </summary>
        [TestMethod]
        public void MomentCurvatureStructuredFieldsGiveTheStatus()
        {
            int curves = 0, interrupted = 0, refined = 0, refinementInterrupted = 0, notAttempted = 0;
            foreach (var culture in new[] { "", "it-IT" })
                InCulture(culture, () =>
                {
                    foreach (var a in DetailingContractTests.AnalyticCases().Where(x => !Rejects(x)))
                    {
                        var r = MomentCurvatureAnalysis.Calculate(a.Request, a.Limit, a.Response, a.YieldStrain);
                        Assert.AreEqual(r.Status, StatusFromFields(r, MomentCurvatureUnits.NewtonMillimetre), a.Name);
                        if (r.InterruptedAtStep.HasValue) interrupted++;
                        if (r.YieldRefinement == null) notAttempted++;
                        else if (r.YieldRefinement.Applied)
                        {
                            refined++; Assert.AreEqual(a.Request.YieldRefinementSteps, r.YieldRefinement.Bisections); Assert.IsNull(r.YieldRefinement.InterruptionMessage);
                            Assert.IsTrue(r.Points.Any(v => v.Moment == r.YieldRefinement.Moment.Value || v.Yielded), a.Name);
                        }
                        else { refinementInterrupted++; Assert.IsNull(r.YieldRefinement.Moment); }
                        curves++;
                    }
                    return 0;
                });
            Assert.AreEqual(2 * 17, curves); Assert.AreEqual(2 * 3, interrupted); Assert.AreEqual(2 * 1, refinementInterrupted);
            Assert.AreEqual(2 * 8, refined); Assert.AreEqual(2 * 8, notAttempted);

            var archive = Archive();
            foreach (var row in Rows("curvature-legacy.csv").Skip(1))
            {
                var c = row.Split(';');
                var standard = ServiceabilityMigrationTests.Standard(c[2]);
                foreach (var pair in c[3].Split(',')) { var kv = pair.Split('='); typeof(StandardModelCode2010).GetProperty(kv[0]).SetValue(standard, D(kv[1])); }
                double[] P(string s) => s.Split(',').Select(D).ToArray();
                var o = P(c[4]); var v1 = P(c[5]); var v2 = P(c[6]);
                var axes = new CoordinateSystem(new Point3d(o[0], o[1], o[2]), new Vector3d(v1[0], v1[1], v1[2]), new Vector3d(v2[0], v2[1], v2[2]));
                var section = (ReinforcedConcreteSection)archive.BeamProperties[c[1]];
                var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(axes, SectionSolver.FailureAnalysisTypes.ConstantN, SectionSolver.FailureDomainTypes.Plastic,
                    SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, false, 32);
                var checker = new SectionCheckerModelCode2010(new SectionCheckerAttribute(section, null, null), options, standard, false);
                var r = MomentCurvatureAnalysis.Calculate(new MomentCurvatureRequest(D(c[8]) * 1000, D(c[9]), int.Parse(c[10]), D(c[11]), bool.Parse(c[12]), D(c[13]) * 1000,
                    int.Parse(c[14])), checker, section, axes, D(c[7]));
                Assert.AreEqual(r.Status, StatusFromFields(r, MomentCurvatureUnits.NewtonMillimetre), c[0]);
                Assert.IsTrue(r.YieldRefinement.Applied); Assert.AreEqual(12, r.YieldRefinement.Bisections); Assert.IsNull(r.InterruptedAtStep);
            }
        }

        /// <summary>
        /// Lazy limit point: with an end fraction below 1 the function is never evaluated (even if it throws); with the end fraction 1 it is evaluated once,
        /// inside the limit step, so an error there stops the curve at the last step with its message; the curve is the same as with the eager constructor.
        /// </summary>
        [TestMethod]
        public void LazyLimitPointIsEvaluatedOnlyAtTheLimitStep()
        {
            double M = DetailingContractTests.Mlim; int calls = 0;
            Func<double, double, double, MomentCurvatureStrains> response = DetailingContractTests.Strains;
            Func<double, double, double, MomentCurvatureLimit> throwing = (n, c, s) => new MomentCurvatureLimit(n, M * c, M * s,
                () => { calls++; throw new InvalidOperationException("limit state failed"); });
            var partial = MomentCurvatureAnalysis.Calculate(new MomentCurvatureRequest(0, 30, 20, .9), throwing, response, .002, new MomentCurvatureUnits("kN", "kNm"));
            Assert.AreEqual(0, calls); Assert.IsNull(partial.InterruptedAtStep); Assert.IsNull(partial.InterruptionMessage);
            Assert.IsFalse(partial.Points.Any(v => v.Limit)); Assert.IsTrue(partial.Points.Count >= 21); Assert.IsNull(partial.UltimateCurvature);
            StringAssert.Contains(partial.Status, " kN; residual = 0 kN.");
            var stopped = MomentCurvatureAnalysis.Calculate(new MomentCurvatureRequest(0, 30, 20), throwing, response, .002);
            Assert.AreEqual(1, calls); Assert.AreEqual(20, stopped.InterruptedAtStep); Assert.AreEqual("limit state failed", stopped.InterruptionMessage);
            Assert.IsNull(stopped.UltimateCurvature); StringAssert.Contains(stopped.Status, " Interrupted at step 20: limit state failed");
            Assert.IsFalse(stopped.Points.Any(v => v.Limit));
            var noState = MomentCurvatureAnalysis.Calculate(new MomentCurvatureRequest(0, 30, 20), (n, c, s) => new MomentCurvatureLimit(n, M * c, M * s, () => null), response, .002);
            Assert.AreEqual(20, noState.InterruptedAtStep); Assert.AreEqual("Response: strain state of the limit point not available.", noState.InterruptionMessage);

            int evaluations = 0;
            Func<double, double, double, MomentCurvatureLimit> lazy = (n, c, s) => new MomentCurvatureLimit(n, M * c, M * s,
                () => { evaluations++; return DetailingContractTests.Strains(n, 1.05 * M * c, 1.05 * M * s); });
            Func<double, double, double, MomentCurvatureLimit> eager = (n, c, s) => new MomentCurvatureLimit(n, M * c, M * s, DetailingContractTests.Strains(n, 1.05 * M * c, 1.05 * M * s));
            var a = MomentCurvatureAnalysis.Calculate(new MomentCurvatureRequest(-1e5, 135, 30), lazy, response, .002);
            var b = MomentCurvatureAnalysis.Calculate(new MomentCurvatureRequest(-1e5, 135, 30), eager, response, .002);
            Assert.AreEqual(1, evaluations); Assert.AreEqual(b.Status, a.Status); Assert.AreEqual(b.Points.Count, a.Points.Count);
            for (int i = 0; i < a.Points.Count; i++)
            {
                Assert.AreEqual(BitConverter.DoubleToInt64Bits(b.Points[i].Curvature), BitConverter.DoubleToInt64Bits(a.Points[i].Curvature), "point " + i);
                Assert.AreEqual(b.Points[i].Limit, a.Points[i].Limit);
            }
            Assert.AreEqual(b.UltimateCurvature, a.UltimateCurvature);
            var once = new MomentCurvatureLimit(0, 1, 0, () => { evaluations++; return DetailingContractTests.Strains(0, 1, 0); });
            Assert.AreSame(once.Strains, once.Strains); Assert.AreEqual(2, evaluations, "kept in the instance");
            Assert.AreEqual("strains", Assert.ThrowsException<ArgumentNullException>(() => new MomentCurvatureLimit(0, 1, 0, (Func<MomentCurvatureStrains>)null)).ParamName);
            Assert.AreEqual("strains", Assert.ThrowsException<ArgumentNullException>(() => new MomentCurvatureLimit(0, 1, 0, (MomentCurvatureStrains)null)).ParamName);
        }

        /// <summary>
        /// Rejections: the overload with units throws <see cref="MomentCurvatureException"/> with the reason and the values (caller units, NaN where not
        /// reached) and the message of 0.0.17.0 with its labels; the overloads of 0.0.17.0 keep the exact type <see cref="ArgumentException"/> and the message,
        /// with the reason in Exception.Data.
        /// </summary>
        [TestMethod]
        public void MomentCurvatureRejectionsAreTyped()
        {
            var kN = new MomentCurvatureUnits("kN", "kNm");
            Func<double, double, double, MomentCurvatureStrains> response = DetailingContractTests.Strains;
            void Check(MomentCurvatureRequest request, Func<double, double, double, MomentCurvatureLimit> limit, double yieldStrain, MomentCurvatureRejection reason,
                double limitN, double limitMoment, string messageN, string messageKn)
            {
                var plain = Assert.ThrowsException<ArgumentException>(() => MomentCurvatureAnalysis.Calculate(request, limit, response, yieldStrain), reason.ToString());
                Assert.AreEqual(messageN, plain.Message); Assert.AreEqual(reason, plain.Data[MomentCurvatureAnalysis.RejectionKey]);
                var typed = Assert.ThrowsException<MomentCurvatureException>(() => MomentCurvatureAnalysis.Calculate(request, limit, response, yieldStrain, kN), reason.ToString());
                Assert.AreEqual(messageKn, typed.Message); Assert.AreEqual(reason, typed.Reason); Assert.AreEqual(reason, typed.Data[MomentCurvatureAnalysis.RejectionKey]);
                Assert.AreEqual(request.AxialForce, typed.AxialForce); Assert.AreEqual(request.AxialTolerance, typed.AxialTolerance);
                Assert.AreEqual(limitN, typed.LimitAxialForce); Assert.AreEqual(limitMoment, typed.LimitMoment);
                Assert.IsInstanceOfType(typed, typeof(ArgumentException)); Assert.IsNull(typed.ParamName);
            }
            const string invalid = "Moment-curvature: 10-500 steps, end fraction in (0; 1], finite N and direction.";
            Check(new MomentCurvatureRequest(-12.5, 0, 9), DetailingContractTests.AnalyticLimit(0, 300), .002, MomentCurvatureRejection.InvalidRequest, double.NaN, double.NaN, invalid, invalid);
            Check(new MomentCurvatureRequest(-12.5, 0, 10), DetailingContractTests.AnalyticLimit(0, 300), 0, MomentCurvatureRejection.InvalidRequest, double.NaN, double.NaN, invalid, invalid);
            Check(new MomentCurvatureRequest(-12.5, 0, 10), (n, c, s) => null, .002, MomentCurvatureRejection.LimitPointNotAvailable, double.NaN, double.NaN,
                "Limit point not available at the assigned N.", "Limit point not available at the assigned N.");
            InCulture("", () =>
            {
                Check(new MomentCurvatureRequest(-123.456789, 30, 10, 1, true, 1.0005), DetailingContractTests.AnalyticLimit(1.2345678, 300), .002, MomentCurvatureRejection.AxialResidual,
                    -123.456789 + 1.2345678, double.NaN,
                    "Residual N at the limit point beyond the tolerance 1.0005 N: N = -123.456789 N, limit N = -122.222221 N.",
                    "Residual N at the limit point beyond the tolerance 1.0005 kN: N = -123.456789 kN, limit N = -122.222221 kN.");
                return 0;
            });
            InCulture("it-IT", () =>
            {
                Check(new MomentCurvatureRequest(25, 30, 10, 1, true, .01), DetailingContractTests.AnalyticLimit(-.01025, 300), .002, MomentCurvatureRejection.AxialResidual,
                    25 - .01025, double.NaN,
                    "Residual N at the limit point beyond the tolerance 0,01 N: N = 25 N, limit N = 24,98975 N.",
                    "Residual N at the limit point beyond the tolerance 0,01 kN: N = 25 kN, limit N = 24,98975 kN.");
                return 0;
            });
            Check(new MomentCurvatureRequest(-12.5, 0, 10), DetailingContractTests.AnalyticLimit(0, -300), .002, MomentCurvatureRejection.NonPositiveLimitMoment, -12.5, -300,
                "Non-positive limit moment in the requested direction.", "Non-positive limit moment in the requested direction.");
            Assert.AreEqual("units", Assert.ThrowsException<ArgumentNullException>(() => MomentCurvatureAnalysis.Calculate(new MomentCurvatureRequest(0, 0, 10),
                DetailingContractTests.AnalyticLimit(0, 300), response, .002, (MomentCurvatureUnits)null)).ParamName);
            Assert.ThrowsException<ArgumentNullException>(() => new MomentCurvatureUnits(null, "kNm"));
            Assert.AreEqual("N", MomentCurvatureUnits.NewtonMillimetre.Force); Assert.AreEqual("Nmm", MomentCurvatureUnits.NewtonMillimetre.Moment);
        }

        /// <summary>
        /// <see cref="MomentCurvatureException"/> crosses a serialization boundary (application domains of a .NET Framework host) with message, reason,
        /// values (NaN included) and Exception.Data.
        /// </summary>
        [TestMethod]
        public void MomentCurvatureExceptionIsSerializable()
        {
            Assert.IsTrue(typeof(MomentCurvatureException).IsSerializable);
            var kN = new MomentCurvatureUnits("kN", "kNm");
            Func<double, double, double, MomentCurvatureStrains> response = DetailingContractTests.Strains;
            var cases = new[]
            {
                new { Limit = DetailingContractTests.AnalyticLimit(0, -300), Reason = MomentCurvatureRejection.NonPositiveLimitMoment, LimitN = -12.5, LimitM = -300.0 },
                new { Limit = (Func<double, double, double, MomentCurvatureLimit>)((n, c, s) => null), Reason = MomentCurvatureRejection.LimitPointNotAvailable,
                    LimitN = double.NaN, LimitM = double.NaN }
            };
            foreach (var x in cases)
            {
                var original = Assert.ThrowsException<MomentCurvatureException>(() => MomentCurvatureAnalysis.Calculate(new MomentCurvatureRequest(-12.5, 0, 10, 1, true, 2.5),
                    x.Limit, response, .002, kN));
                MomentCurvatureException copy;
                using (var stream = new MemoryStream())
                {
                    var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    formatter.Serialize(stream, original); stream.Position = 0;
                    copy = (MomentCurvatureException)formatter.Deserialize(stream);
                }
                Assert.AreNotSame(original, copy); Assert.AreEqual(original.Message, copy.Message, x.Reason.ToString()); Assert.IsNull(copy.ParamName);
                Assert.AreEqual(x.Reason, copy.Reason); Assert.AreEqual(x.Reason, copy.Data[MomentCurvatureAnalysis.RejectionKey]);
                Assert.AreEqual(-12.5, copy.AxialForce); Assert.AreEqual(2.5, copy.AxialTolerance);
                Assert.AreEqual(x.LimitN, copy.LimitAxialForce); Assert.AreEqual(x.LimitM, copy.LimitMoment);
            }
        }

        /// <summary>
        /// Scale invariance: the analytic curves of the contract in (N; Nmm) and in (kN; kNm) give the same points to the conversion factors within 1e-12, the
        /// same steps, verdicts and rejections, with the labels of each overload in the Status.
        /// </summary>
        [TestMethod]
        public void MomentCurvatureIsScaleInvariant()
        {
            var kN = new MomentCurvatureUnits("kN", "kNm");
            void Close12(double expected, double actual, string what) => Assert.AreEqual(expected, actual, 1e-12 * Math.Max(1e-300, Math.Abs(expected)), what);
            var newton = DetailingContractTests.AnalyticCases().ToArray(); var kilo = DetailingContractTests.AnalyticCases().ToArray();
            int compared = 0;
            for (int k = 0; k < newton.Length; k++)
            {
                var a = newton[k]; var q = a.Request; var limitN = kilo[k].Limit; var responseN = kilo[k].Response;
                var request = new MomentCurvatureRequest(q.AxialForce / 1000, q.DirectionDegrees, q.Steps, q.EndFraction, q.QuadraticSampling, q.AxialTolerance / 1000, q.YieldRefinementSteps);
                Func<double, double, double, MomentCurvatureLimit> limit = (n, c, s) =>
                {
                    var l = limitN(n * 1000, c, s);
                    return l == null ? null : new MomentCurvatureLimit(l.AxialForce / 1000, l.Mx / 1e6, l.My / 1e6, () => l.Strains);
                };
                Func<double, double, double, MomentCurvatureStrains> response = (n, mx, my) => responseN(n * 1000, mx * 1e6, my * 1e6);
                if (Rejects(a))
                {
                    var plain = Assert.ThrowsException<ArgumentException>(() => MomentCurvatureAnalysis.Calculate(q, a.Limit, a.Response, a.YieldStrain), a.Name);
                    var typed = Assert.ThrowsException<MomentCurvatureException>(() => MomentCurvatureAnalysis.Calculate(request, limit, response, a.YieldStrain, kN), a.Name);
                    Assert.AreEqual(plain.Data[MomentCurvatureAnalysis.RejectionKey], typed.Reason, a.Name);
                    continue;
                }
                var rN = MomentCurvatureAnalysis.Calculate(q, a.Limit, a.Response, a.YieldStrain);
                var rK = MomentCurvatureAnalysis.Calculate(request, limit, response, a.YieldStrain, kN);
                Assert.AreEqual(rN.Points.Count, rK.Points.Count, a.Name); Assert.AreEqual(rN.InterruptedAtStep, rK.InterruptedAtStep, a.Name);
                Assert.AreEqual(rN.InterruptionMessage, rK.InterruptionMessage, a.Name);
                Close12(rN.LimitMoment, rK.LimitMoment * 1e6, a.Name + " limit moment"); Close12(rN.LimitAxialForce, rK.LimitAxialForce * 1000, a.Name + " limit N");
                Assert.AreEqual(rN.YieldCurvature.HasValue, rK.YieldCurvature.HasValue, a.Name);
                if (rN.YieldCurvature.HasValue) Close12(rN.YieldCurvature.Value, rK.YieldCurvature.Value, a.Name + " yield curvature");
                Assert.AreEqual(rN.UltimateCurvature.HasValue, rK.UltimateCurvature.HasValue, a.Name);
                if (rN.UltimateCurvature.HasValue) Close12(rN.UltimateCurvature.Value, rK.UltimateCurvature.Value, a.Name + " ultimate curvature");
                for (int i = 0; i < rN.Points.Count; i++)
                {
                    var n = rN.Points[i]; var m = rK.Points[i]; string what = a.Name + " point " + i;
                    Close12(n.Moment, m.Moment * 1e6, what + " M"); Close12(n.Mx, m.Mx * 1e6, what + " Mx"); Close12(n.My, m.My * 1e6, what + " My");
                    Close12(n.Curvature, m.Curvature, what + " χ"); Close12(n.SteelStrain, m.SteelStrain, what + " εs"); Close12(n.ReferenceStrain, m.ReferenceStrain, what + " ε0");
                    Assert.AreEqual(n.Yielded, m.Yielded, what); Assert.AreEqual(n.Limit, m.Limit, what);
                }
                Assert.AreEqual(rN.YieldRefinement == null, rK.YieldRefinement == null, a.Name);
                if (rN.YieldRefinement != null && rN.YieldRefinement.Applied) Close12(rN.YieldRefinement.Moment.Value, rK.YieldRefinement.Moment.Value * 1e6, a.Name + " My");
                Assert.AreEqual(rK.Status, StatusFromFields(rK, kN), a.Name);
                StringAssert.Contains(rK.Status, " kN; residual = ", a.Name); Assert.IsFalse(rK.Status.Contains(" Nmm"), a.Name);
                if (rK.YieldRefinement != null && rK.YieldRefinement.Applied) StringAssert.Contains(rK.Status, " kNm.", a.Name);
                compared++;
            }
            Assert.AreEqual(17, compared);
        }

        [TestMethod]
        public void LegacyMomentCurvatureIsReproduced()
        {
            var archive = Archive(); var rows = Rows("curvature-legacy.csv").Skip(1).ToArray();
            Assert.AreEqual(5, rows.Length);
            int points = 0;
            foreach (var row in rows)
            {
                var c = row.Split(';'); string id = "curve " + c[0] + " " + c[1] + " " + c[2];
                Assert.AreEqual("ok", c[15], id);
                var standard = ServiceabilityMigrationTests.Standard(c[2]);
                foreach (var pair in c[3].Split(',')) { var kv = pair.Split('='); typeof(StandardModelCode2010).GetProperty(kv[0]).SetValue(standard, D(kv[1])); }
                double[] P(string s) => s.Split(',').Select(D).ToArray();
                var o = P(c[4]); var v1 = P(c[5]); var v2 = P(c[6]);
                var axes = new CoordinateSystem(new Point3d(o[0], o[1], o[2]), new Vector3d(v1[0], v1[1], v1[2]), new Vector3d(v2[0], v2[1], v2[2]));
                var section = (ReinforcedConcreteSection)archive.BeamProperties[c[1]];
                var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(axes, SectionSolver.FailureAnalysisTypes.ConstantN, SectionSolver.FailureDomainTypes.Plastic,
                    SectionSolver.StressAnalysisTypes.NonLinear, 0, 0, false, 32);
                var checker = new SectionCheckerModelCode2010(new SectionCheckerAttribute(section, null, null), options, standard, false);
                var request = new MomentCurvatureRequest(D(c[8]) * 1000, D(c[9]), int.Parse(c[10]), D(c[11]), bool.Parse(c[12]), D(c[13]) * 1000, int.Parse(c[14]));
                var r = MomentCurvatureAnalysis.Calculate(request, checker, section, axes, D(c[7]));
                Close(D(c[16]) * 1e6, r.LimitMoment, id + " limit moment", 1e-7);
                Close(D(c[17]) / 1000, r.YieldCurvature.Value, id + " yield curvature", 1e-7); Close(D(c[18]) / 1000, r.UltimateCurvature.Value, id + " ultimate curvature", 1e-7);
                Close(D(c[19]) * 1000, r.LimitAxialForce, id + " limit N", 1e-7);
                var legacy = c[21].Split('|');
                Assert.AreEqual(legacy.Length, r.Points.Count, id + " points");
                for (int i = 0; i < legacy.Length; i++)
                {
                    var q = legacy[i].Split(':'); var p = r.Points[i]; string what = id + " point " + i;
                    Close(D(q[0]) * 1e6, p.Moment, what + " M", 1e-7); Close(D(q[1]) / 1000, p.Curvature, what + " χ", 1e-7);
                    Close(D(q[2]) / 1000, p.GradientX, what + " χx", 1e-7); Close(D(q[3]) / 1000, p.GradientY, what + " χy", 1e-7);
                    // At the limit point the strain plane comes from the domain search, converged on N within its tolerance (legacy residual ≈ 0.04 kN):
                    // with yielded steel and plastic concrete N is almost insensitive to a translation of the plane, so the translation is defined to
                    // ≈ 3e-4 only (the section read back from the archive gives a different converged plane). Moment, N and curvature agree to 1e-7.
                    double strains = bool.Parse(q[7]) ? 1e-3 : 1e-7;
                    Close(D(q[4]) / 1000, p.ReferenceStrain, what + " ε0", strains);
                    Close(D(q[5]) / 1000, p.ConcreteCompressionStrain, what + " εc", strains); Close(D(q[6]) / 1000, p.SteelStrain, what + " εs", strains);
                    Assert.AreEqual(bool.Parse(q[7]), p.Yielded, what + " yielded"); Assert.AreEqual(bool.Parse(q[8]), p.Limit, what + " limit");
                    points++;
                }
            }
            Assert.IsTrue(points >= 5 * 41, points + " points");
        }
    }
}
