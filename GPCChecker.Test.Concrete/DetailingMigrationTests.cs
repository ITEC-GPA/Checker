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
