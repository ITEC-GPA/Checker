using GPC.Checkers.Concrete.Shear;
using GPC.Checkers.Concrete.Torsion;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ConcreteTests
{
    /// <summary>
    /// Torsion core moved from ANTHEA (ConcreteTorsionCalculator, commit fe4652c; captured at b5f2222 with unchanged sources) to GPCChecker.Concrete.
    /// Fixtures/torsion-legacy.csv freezes 986 NTC cases (grid, limits, seeded random), torsion-geometry-legacy.csv the resisting profile
    /// of 10 outlines. Legacy kN/kNm are converted to N/Nmm. The other standards are checked against hand calculations.
    /// </summary>
    [TestClass]
    public class TorsionMigrationTests
    {
        private static double D(string s) => double.Parse(s, CultureInfo.InvariantCulture);
        private static double? N(string s) => s.Length == 0 ? (double?)null : D(s);
        private static void Close(double expected, double actual, string what)
            => Assert.AreEqual(expected, actual, 1e-9 * Math.Max(1, Math.Abs(expected)), what);
        private static string[] Rows(string name)
            => File.ReadAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", name), Encoding.UTF8).Where(l => !l.StartsWith("#")).Skip(1).ToArray();

        [TestMethod]
        public void LegacyFixturesAreReproduced()
        {
            var rows = Rows("torsion-legacy.csv");
            Assert.AreEqual(986, rows.Length);
            int ok = 0, errors = 0, noLinks = 0;
            foreach (var row in rows)
            {
                var c = row.Split(';');
                string id = "case " + c[0];
                var input = new SectionTorsionInput(new StandardNTC2018Concrete(), D(c[1]) * 1e6, new TorsionGeometry(D(c[2]), D(c[3]), D(c[4])), 30, D(c[5]), 1.5,
                    D(c[6]), D(c[6]), D(c[7]), D(c[8]), D(c[9]), D(c[10]));
                var x = new TorsionShearComponent(D(c[11]) * 1000, D(c[13]) * 1000, D(c[14]) * 1000, D(c[15]));
                var y = new TorsionShearComponent(D(c[12]) * 1000, D(c[16]) * 1000, D(c[17]) * 1000, D(c[18]));
                if (c[19] != "ok")
                {
                    Assert.AreEqual("error:ArgumentException", c[19], id);
                    if (D(c[7]) == 0 && D(c[8]) > 0)
                    {
                        // Intentional difference: no closed links is a zero torsional resistance (NotSatisfied), not invalid data.
                        var none = SectionTorsionCalculator.Evaluate(input, x, y);
                        Assert.AreEqual(TorsionVerdict.NotSatisfied, none.Verdict, id); Assert.AreEqual(0, none.TRsd, id); Assert.IsNull(none.Ratio, id);
                        noLinks++; continue;
                    }
                    Assert.ThrowsException<ArgumentException>(() => SectionTorsionCalculator.Evaluate(input, x, y), id);
                    errors++; continue;
                }
                var r = SectionTorsionCalculator.Evaluate(input, x, y);
                Close(D(c[20]), r.TRcd / 1e6, id + " TRcd"); Close(D(c[21]), r.TRsd / 1e6, id + " TRsd"); Close(D(c[22]), r.TRld / 1e6, id + " TRld");
                Close(D(c[23]), r.TRd / 1e6, id + " TRd"); Close(D(c[27]), r.RequiredLongitudinalArea, id + " Al,req");
                foreach (var (legacy, actual, what) in new[] { (N(c[24]), r.TorsionRatio, "torsion"), (N(c[25]), r.ConcreteInteraction, "concrete"), (N(c[26]), r.LinkInteraction, "links") })
                {
                    Assert.AreEqual(legacy.HasValue, actual.HasValue, id + " " + what + " defined");
                    if (legacy.HasValue) Close(legacy.Value, actual.Value, id + " " + what);
                }
                Assert.AreEqual(bool.Parse(c[28]), r.Verdict == TorsionVerdict.Satisfied, id + " verdict " + c[29]);
                Assert.IsFalse(r.QuadraticInteraction, id);
                ok++;
            }
            Assert.AreEqual(936, ok); Assert.AreEqual(49, errors); Assert.AreEqual(1, noLinks);
        }

        [TestMethod]
        public void LegacyGeometryIsReproduced()
        {
            var rows = Rows("torsion-geometry-legacy.csv");
            Assert.AreEqual(10, rows.Length);
            foreach (var row in rows)
            {
                var c = row.Split(';');
                string id = c[1];
                if (c[2] == "A T") { Assert.AreEqual("error:ArgumentException", c[11], id); continue; } // only rectangles and circles are suggested
                bool hollow = bool.Parse(c[7]);
                Func<TorsionGeometry> build = () => c[2] == "Circolare"
                    ? TorsionGeometry.Circle(D(c[3]), D(c[5]), D(c[6]), hollow ? D(c[10]) : (double?)null)
                    : TorsionGeometry.Rectangle(D(c[3]), D(c[4]), D(c[5]), D(c[6]), hollow ? D(c[8]) : (double?)null, hollow ? D(c[9]) : (double?)null);
                if (c[11] != "ok") { Assert.ThrowsException<ArgumentException>(() => build(), id); continue; }
                var g = build();
                Close(D(c[12]), g.EnclosedArea, id + " A"); Close(D(c[13]), g.Perimeter, id + " P"); Close(D(c[14]), g.Thickness, id + " t");
            }
            // EN 1992-1-1 6.3.2(1) for hollow sections: A/u of the full outline, at most the wall. 1200x1000 box with 300 mm walls: 272.7 < 300.
            var en = TorsionGeometry.Rectangle(1200, 1000, 1200 * 1000 - 600 * 400, 60, 600, 400, TorsionThicknessRule.EquivalentLimitedByWall);
            Assert.AreEqual(1200.0 * 1000 / 4400, en.Thickness, 1e-9);
            Assert.AreEqual(300, TorsionGeometry.Rectangle(1200, 1000, 1200 * 1000 - 600 * 400, 60, 600, 400).Thickness, 1e-12);
        }

        // Rectangle 300x500 (Ak = 204 x 404, uk = 1216, tef = 96), fcd = 20 MPa, fck = 30 MPa, fyd = 500/1.15, Ø8/150 closed links, ΣAsl = 804 mm², cot θ = 1.5.
        private static SectionTorsionInput Input(Standard standard, double t = 30e6, bool box = false, double fck = 30, double fcd = 20, double cot = 1.5, double leg = 50.27)
            => new SectionTorsionInput(standard, t, new TorsionGeometry(82416, 1216, 96), fck, fcd, 1.5, 500 / 1.15, 500 / 1.15, leg, 150, 804, cot, 90, box);

        [TestMethod]
        public void Eurocode2ResistancesMatchHandCalculation()
        {
            // ν = 0.6 (1 − 30/250) = 0.528; TRd,max = 2 ν fcd Ak tef sinθ cosθ = 77.1231 kNm; TRd,s = 2 Ak (Ast/s) fyd cot θ = 36.0265 kNm;
            // ΣAsl fyd/uk = TEd cot θ/(2 Ak) → TRld = 31.5897 kNm.
            var r = SectionTorsionCalculator.Evaluate(Input(new StandardEN1992p11()), null, null);
            Assert.AreEqual(77123117.686153844, r.TRcd, 1e-3); Assert.AreEqual(36026541.913043484, r.TRsd, 1e-3); Assert.AreEqual(31589656.750572082, r.TRld, 1e-3);
            Assert.AreEqual(r.TRld, r.TRd); Assert.AreEqual(30e6 / r.TRld, r.TorsionRatio.Value, 1e-12);
            Assert.AreEqual(763.541, r.RequiredLongitudinalArea, 1e-3); Assert.AreEqual(.27907, r.RequiredLinkAreaPerLength, 1e-5);
            Assert.AreEqual(TorsionVerdict.Satisfied, r.Verdict); Assert.IsFalse(r.QuadraticInteraction);
            // NTC: f'cd = 0.5 fcd with the same truss; UNI (DM 31/07/2012): ν = 0.5 up to C70/85; DS: ν = 0.7 − fck/200 = 0.55.
            Assert.AreEqual(73.0333, SectionTorsionCalculator.Evaluate(Input(new StandardNTC2018Concrete()), null, null).TRcd / 1e6, 1e-4);
            Assert.AreEqual(73.0333, SectionTorsionCalculator.Evaluate(Input(new StandardUNIEN1992p11()), null, null).TRcd / 1e6, 1e-4);
            Assert.AreEqual(80.3366, SectionTorsionCalculator.Evaluate(Input(new StandardDSEN1992p11()), null, null).TRcd / 1e6, 1e-4);
            Assert.AreEqual(r.TRcd, SectionTorsionCalculator.Evaluate(Input(new StandardNSEN1992p11()), null, null).TRcd, 1e-6);
        }

        [TestMethod]
        public void DinAndModelCodeUseTheirStrutAndQuadraticInteractionForSolidSections()
        {
            // DIN NA: ν = 0.525 ν2 (compact), 0.75 ν2 (box with reinforcement on both faces), ν2 = 1.1 − fck/500 ≤ 1. Pure torsion: cot θ = 1 without shear data.
            Assert.ThrowsException<ArgumentException>(() => SectionTorsionCalculator.Evaluate(Input(new StandardDINEN1992p11()), null, null));
            var compact = SectionTorsionCalculator.Evaluate(Input(new StandardDINEN1992p11(), cot: 1), null, null);
            Assert.AreEqual(2 * 82416 * 96 * .525 * 20 * .5, compact.TRcd, 1e-6); Assert.IsTrue(compact.QuadraticInteraction);
            var box = SectionTorsionCalculator.Evaluate(Input(new StandardDINEN1992p11(), box: true, cot: 1), null, null);
            Assert.AreEqual(2 * 82416 * 96 * .75 * 20 * .5, box.TRcd, 1e-6); Assert.IsFalse(box.QuadraticInteraction);
            // MC2010: kc = min(0.65; 1/(1.2 + 55 ε1)) ηfc with ε1 = εx + (εx + 0.002) cot²θ; εx = 0 → kc = 0.65, TRcd = 94.9432 kNm.
            var mc = SectionTorsionCalculator.Evaluate(Input(new StandardModelCode2010()), null, null);
            Assert.AreEqual(94.9432, mc.TRcd / 1e6, 1e-4); Assert.IsTrue(mc.QuadraticInteraction);
            // A larger εx of the shear reduces the strut.
            var strained = SectionTorsionCalculator.Evaluate(Input(new StandardModelCode2010()), new TorsionShearComponent(1e5, 4e5, 9e5, 1.5, .001), null);
            Assert.IsTrue(strained.TRcd < mc.TRcd);
            // Quadratic: √[(T/TRcd)² + (V/VRcd)²] instead of the sum (same components).
            var v = new TorsionShearComponent(3e5, 6e5, 8e5, 1);
            var quadratic = SectionTorsionCalculator.Evaluate(Input(new StandardDINEN1992p11(), cot: 1), v, null);
            double tc = 30e6 / quadratic.TRcd, vc = 3e5 / 8e5;
            Assert.AreEqual(Math.Sqrt(tc * tc + vc * vc), quadratic.ConcreteInteraction.Value, 1e-12);
            Assert.AreEqual(30e6 / box.TRcd + vc, SectionTorsionCalculator.Evaluate(Input(new StandardDINEN1992p11(), box: true, cot: 1), v, null).ConcreteInteraction.Value, 1e-12);
        }

        [TestMethod]
        public void ShearOfBothDirectionsIsComputedWithTheSameCotTheta()
        {
            var standard = new StandardEN1992p11();
            // V1 along x: bw = 500, d = 250; V2 along y: bw = 300, d = 450; 2 legs Ø8/150.
            var axis1 = new SectionShearInput(standard, 0, 4e4, 0, 150000, 500, 250, 0, 30, 20, 500 / 1.15, 1.5, 200000, 2 * 50.27, 150);
            var axis2 = new SectionShearInput(standard, 0, -9e4, 0, 150000, 300, 450, 0, 30, 20, 500 / 1.15, 1.5, 200000, 2 * 50.27, 150);
            var r = SectionTorsionCalculator.Calculate(Input(standard, t: 1e7), axis1, axis2);
            Assert.AreEqual(1.5, r.Axis1Shear.CotTheta, 1e-12); Assert.AreEqual(1.5, r.Axis2Shear.CotTheta, 1e-12);
            var direct = SectionShearCalculator.Calculate(new SectionShearInput(standard, 0, -9e4, 0, 150000, 300, 450, 0, 30, 20, 500 / 1.15, 1.5, 200000, 2 * 50.27, 150, cotTheta: 1.5));
            Assert.AreEqual(direct.VRcd, r.Axis2Shear.VRcd, 1e-9); Assert.AreEqual(direct.VRsd, r.Axis2Shear.VRsd, 1e-9);
            double concrete = 1e7 / r.TRcd + 4e4 / r.Axis1Shear.VRcd + 9e4 / r.Axis2Shear.VRcd;
            double links = 1e7 / r.TRsd + Math.Max(4e4 / r.Axis1Shear.VRsd, 9e4 / r.Axis2Shear.VRsd);
            Assert.AreEqual(concrete, r.ConcreteInteraction.Value, 1e-12); Assert.AreEqual(links, r.LinkInteraction.Value, 1e-12);
            Assert.AreEqual(Math.Max(r.TorsionRatio.Value, Math.Max(concrete, links)), r.Ratio.Value, 1e-15);
            // Given components on another cot θ are rejected; a direction without shear may be missing, not one with shear.
            Assert.ThrowsException<ArgumentException>(() => SectionTorsionCalculator.Evaluate(Input(standard), new TorsionShearComponent(1e4, 1e5, 1e5, 2), null));
            var noLinks = new SectionShearInput(standard, 0, 4e4, 0, 150000, 500, 250, 402, 30, 20, 500 / 1.15, 1.5, 200000, 0, 1);
            Assert.ThrowsException<ArgumentException>(() => SectionTorsionCalculator.Calculate(Input(standard), noLinks, null));
        }

        [TestMethod]
        public void DinRangeOfCotThetaIncludesTheShearFlowOfTorsion()
        {
            var standard = new StandardDINEN1992p11();
            var axis = new SectionShearInput(standard, 0, 2e4, 0, 150000, 300, 450, 0, 30, 20, 500 / 1.15, 1.5, 200000, 2 * 50.27, 150, leverFactor: .85);
            // Small shear: the shear alone admits cot θ = 2.5; with the torsion shear flow VEd,T+V the range closes below 2.5.
            SectionShearCalculator.Calculate(axis.With(2e4, 2.5));
            Assert.ThrowsException<ArgumentException>(() => SectionTorsionCalculator.Calculate(Input(standard, t: 4e7, cot: 2.5), null, axis));
            var r = SectionTorsionCalculator.Calculate(Input(standard, t: 4e7, cot: 1), null, axis);
            Assert.IsTrue(r.Details.Any(d => d.Symbol.StartsWith("VEd,T+V")));
        }

        [TestMethod]
        public void ZeroResistanceAndLimitsAreExplicit()
        {
            var standard = new StandardNTC2018Concrete();
            // No closed links: zero resistance with a nonzero torque is NotSatisfied without ratio.
            var none = SectionTorsionCalculator.Calculate(Input(standard, leg: 0), null, null);
            Assert.AreEqual(TorsionVerdict.NotSatisfied, none.Verdict); Assert.IsNull(none.TorsionRatio); Assert.IsNull(none.Ratio); Assert.AreEqual(0, none.TRd);
            // Zero torque: ratio 0. Sign of T is irrelevant for the resistance.
            Assert.AreEqual(0, SectionTorsionCalculator.Evaluate(Input(standard, t: 0), null, null).Ratio.Value);
            Assert.AreEqual(SectionTorsionCalculator.Evaluate(Input(standard, t: 3e7), null, null).Ratio.Value,
                SectionTorsionCalculator.Evaluate(Input(standard, t: -3e7), null, null).Ratio.Value, 0);
            // Limits: cot θ, concrete class, inclined links.
            Assert.ThrowsException<ArgumentException>(() => SectionTorsionCalculator.Evaluate(Input(standard, cot: 2.6), null, null));
            Assert.ThrowsException<ArgumentException>(() => SectionTorsionCalculator.Evaluate(Input(new StandardDSEN1992p11(), cot: 2.2), null, null));
            Assert.ThrowsException<ArgumentException>(() => SectionTorsionCalculator.Evaluate(Input(standard, fck: 95, fcd: 50), null, null));
            var inclined = new SectionTorsionInput(standard, 3e7, new TorsionGeometry(82416, 1216, 96), 30, 20, 1.5, 435, 435, 50.27, 150, 804, 1.5, 60);
            Assert.ThrowsException<NotSupportedException>(() => SectionTorsionCalculator.Evaluate(inclined, null, null));
            // NS: classes above C60/75 are used with the C60 values.
            var ns90 = SectionTorsionCalculator.Evaluate(Input(new StandardNSEN1992p11(), fck: 90, fcd: 60), null, null);
            Assert.AreEqual(2 * 82416 * 96 * (.6 * (1 - 60.0 / 250)) * 40 * 1.5 / 3.25, ns90.TRcd, 1e-3);
        }

        private sealed class CustomAnnex : StandardEN1992p11 { }

        [TestMethod]
        public void ProfilesAreResolvedByExactTypeWithoutFallback()
        {
            Assert.IsFalse(TorsionProfiles.TryResolve(new CustomAnnex(), out _));
            Assert.ThrowsException<NotSupportedException>(() => TorsionProfiles.Resolve(new CustomAnnex()));
            StringAssert.Contains(TorsionProfiles.NotApplicableReason(new StandardCSTR34()), "CS-TR34");
            StringAssert.Contains(TorsionProfiles.NotSupportedReason(new StandardCNR204()), "CNR-DT 204");
            Assert.IsNull(TorsionProfiles.NotSupportedReason(new StandardEN1992p11()));
            StringAssert.Contains(Assert.ThrowsException<NotSupportedException>(() => TorsionProfiles.Resolve(new StandardACI318p19())).Message, "future implementation");
            Assert.IsTrue(TorsionProfiles.TryResolve(new StandardCNR200(), out var cnr) && cnr == TorsionProfile.CnrDT200);
            // CNR-DT 200 without FRP data: the NTC member, with TRd,f = 0 stated.
            var frp = SectionTorsionCalculator.Evaluate(Input(new StandardCNR200()), null, null);
            var ntc = SectionTorsionCalculator.Evaluate(Input(new StandardNTC2018Concrete()), null, null);
            Assert.AreEqual(ntc.TRd, frp.TRd); Assert.AreEqual(0, frp.Details.Single(d => d.Symbol == "TRd,f").Value);
            // DS: the DK NA rule for combined V, T, N and M is declared as not applied.
            Assert.IsTrue(SectionTorsionCalculator.Evaluate(Input(new StandardDSEN1992p11()), null, null).Limitations.Single().Contains("6.3.2(6)"));
        }
    }
}
