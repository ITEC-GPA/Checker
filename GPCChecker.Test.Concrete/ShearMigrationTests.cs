using GPC.Checkers.Concrete.Shear;
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
    /// Shear core moved from ANTHEA (ConcreteCodeChecks.Shear / Ntc2018Checks.Shear, commit fe4652c) to GPCChecker.Concrete.
    /// Fixtures/shear-legacy.csv freezes 2016 legacy cases (7 standards, grid + seeded random); hand calculations check the formulas.
    /// Legacy units kN/kNm are converted to N/Nmm; resistances are compared in kN.
    /// </summary>
    [TestClass]
    public class ShearMigrationTests
    {
        private static StandardModelCode2010 Standard(string name)
        {
            switch (name)
            {
                case "NTC 2018": return new StandardNTC2018Concrete();
                case "Model Code 2010": return new StandardModelCode2010();
                case "EN 1992-1-1": return new StandardEN1992p11();
                case "UNI EN 1992-1-1": return new StandardUNIEN1992p11();
                case "DIN EN 1992-1-1": return new StandardDINEN1992p11();
                case "DS EN 1992-1-1": return new StandardDSEN1992p11();
                case "NS EN 1992-1-1": return new StandardNSEN1992p11();
                default: throw new ArgumentException(name);
            }
        }
        private static double D(string s) => double.Parse(s, CultureInfo.InvariantCulture);
        private static double? N(string s) => s.Length == 0 ? (double?)null : D(s);
        private static void Close(double expected, double actual, string what)
            => Assert.AreEqual(expected, actual, 1e-9 * Math.Max(1, Math.Abs(expected)), what);

        [TestMethod]
        public void LegacyFixturesAreReproduced()
        {
            string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "shear-legacy.csv");
            var rows = File.ReadAllLines(file, Encoding.UTF8).Where(l => !l.StartsWith("#")).Skip(1).ToArray();
            Assert.AreEqual(2016, rows.Length);
            int ok = 0, errors = 0, detailsCompared = 0;
            foreach (var row in rows)
            {
                var c = row.Split(';');
                string id = "case " + c[0] + " " + c[1];
                var input = new SectionShearInput(Standard(c[1]), D(c[2]) * 1000, D(c[3]) * 1000, D(c[4]) * 1e6, D(c[5]), D(c[6]), D(c[7]), D(c[8]),
                    D(c[9]), D(c[10]), D(c[11]), D(c[12]), D(c[13]), D(c[14]), D(c[15]), D(c[16]), N(c[17]), D(c[18]), D(c[19]), D(c[20]));
                if (c[21] != "ok")
                {
                    Assert.AreEqual("error:ArgumentException", c[21], id);
                    Assert.ThrowsException<ArgumentException>(() => SectionShearCalculator.Calculate(input), id);
                    errors++; continue;
                }
                var r = SectionShearCalculator.Calculate(input);
                Close(D(c[22]), r.VRsd / 1000, id + " VRsd"); Close(D(c[23]), r.VRcd / 1000, id + " VRcd"); Close(D(c[24]), r.VRd / 1000, id + " VRd");
                Close(D(c[26]), r.CotTheta, id + " cot");
                var ratio = N(c[25]);
                Assert.AreEqual(ratio.HasValue, r.Ratio.HasValue, id + " ratio defined");
                if (ratio.HasValue) Close(ratio.Value, r.Ratio.Value, id + " ratio");
                string status = c[27];
                var expected = status.StartsWith("Trazione") ? ShearVerdict.NotEvaluated : status.Contains("insufficiente") ? ShearVerdict.NotSatisfied
                    : status.Contains("sufficiente") ? ShearVerdict.Satisfied : ratio.HasValue ? (ratio <= 1 ? ShearVerdict.Satisfied : ShearVerdict.NotSatisfied)
                    : r.Demand > 0 ? ShearVerdict.NotSatisfied : ShearVerdict.NotEvaluated;
                Assert.AreEqual(expected, r.Verdict, id + " verdict (" + status + ")");
                // Intermediate values of the Eurocode / Model Code path, with kN converted to N.
                foreach (var entry in c[28].Split('|').Where(e => e.Length > 0))
                {
                    var parts = entry.Split('=');
                    var detail = r.Details.FirstOrDefault(x => x.Symbol == parts[0]);
                    Assert.IsNotNull(detail, id + " detail " + parts[0]);
                    double legacy = D(parts[1]) * (parts[2] == "kN" ? 1000 : 1);
                    Close(legacy, detail.Value, id + " " + parts[0]); detailsCompared++;
                }
                ok++;
            }
            Assert.AreEqual(1840, ok); Assert.AreEqual(176, errors);
            Assert.IsTrue(detailsCompared > 5000, detailsCompared.ToString());
        }

        // Hand calculation: bw = 300 mm, d = 460 mm, Asl = 1256 mm², fck = 30 MPa, γc = 1.5, N = 0.
        // EC2 6.2.2: k = 1 + √(200/460) = 1.6594; ρl = 1256/(300·460) = 0.009101; 100 ρl fck = 27.30, ∛ = 3.0115;
        // VRd,c = 0.12·1.6594·3.0115·300·460 = 82.76 kN; vmin = 0.035·1.6594^1.5·√30 = 0.4098 MPa → 56.55 kN.
        [TestMethod]
        public void Eurocode2WithoutShearReinforcementMatchesHandCalculation()
        {
            var r = SectionShearCalculator.Calculate(new SectionShearInput(new StandardEN1992p11(), 0, 50000, 0, 150000, 300, 460, 1256, 30, 17, 391.3, 1.5, 200000, 0, 1));
            Assert.AreEqual(82.76, r.VRsd / 1000, .02); Assert.AreEqual(56.55, r.VRcd / 1000, .02); Assert.AreEqual(r.VRsd, r.VRd);
            Assert.AreEqual(ShearVerdict.Satisfied, r.Verdict); Assert.AreEqual(50000 / r.VRd, r.Ratio.Value, 1e-12);
        }

        // Hand calculation with Ø8 two legs (Asw = 100.53 mm²) at s = 150 mm, fyd = 391.3 MPa, z = 0.9 d = 414 mm, fcd = 17 MPa.
        // EC2 6.2.3: VRd,s(cot 2.5) = 0.6702·414·391.3·2.5 = 271.4 kN < VRd,max = 300·414·0.528·17/(2.5+0.4) = 384.4 kN → cot θ = 2.5.
        // NTC 4.1.2.3.5.2: αc = 1, cot θ = min(2.5; √(0.5·17·300/(0.6702·391.3) − 1) = 2.95) = 2.5; VRsd = 271.4 kN;
        // VRcd = 0.9·460·300·0.5·17·2.5/(1 + 6.25) = 364.0 kN.
        [TestMethod]
        public void TrussModelsMatchHandCalculation()
        {
            var ec2 = SectionShearCalculator.Calculate(new SectionShearInput(new StandardEN1992p11(), 0, 300000, 0, 150000, 300, 460, 1256, 30, 17, 391.3, 1.5, 200000, 100.53, 150));
            Assert.AreEqual(2.5, ec2.CotTheta, 1e-9); Assert.AreEqual(271.4, ec2.VRsd / 1000, .1); Assert.AreEqual(384.4, ec2.VRcd / 1000, .1);
            Assert.AreEqual(ShearVerdict.NotSatisfied, ec2.Verdict);
            var ntc = SectionShearCalculator.Calculate(new SectionShearInput(new StandardNTC2018Concrete(), 0, 200000, 0, 150000, 300, 460, 1256, 30, 17, 391.3, 1.5, 200000, 100.53, 150));
            Assert.AreEqual(2.5, ntc.CotTheta); Assert.AreEqual(271.4, ntc.VRsd / 1000, .1); Assert.AreEqual(364.0, ntc.VRcd / 1000, .1);
            Assert.AreEqual(ShearVerdict.Satisfied, ntc.Verdict); Assert.AreEqual(ShearProfile.Ntc2018, ntc.Profile);
        }

        [TestMethod]
        public void ProfilesAreResolvedByExactTypeWithoutFallback()
        {
            // CNR-DT 200 derives from NTC 2018 but has its own profile; CS-TR34 does not define beam shear; American standards are future work.
            var expected = new List<Tuple<Standard, ShearProfile?>>
            {
                Tuple.Create<Standard, ShearProfile?>(new StandardNTC2018Concrete(), ShearProfile.Ntc2018),
                Tuple.Create<Standard, ShearProfile?>(new StandardModelCode2010(), ShearProfile.ModelCode2010),
                Tuple.Create<Standard, ShearProfile?>(new StandardEN1992p11(), ShearProfile.EN1992p11),
                Tuple.Create<Standard, ShearProfile?>(new StandardUNIEN1992p11(), ShearProfile.UniEN1992p11),
                Tuple.Create<Standard, ShearProfile?>(new StandardDINEN1992p11(), ShearProfile.DinEN1992p11),
                Tuple.Create<Standard, ShearProfile?>(new StandardDSEN1992p11(), ShearProfile.DsEN1992p11),
                Tuple.Create<Standard, ShearProfile?>(new StandardNSEN1992p11(), ShearProfile.NsEN1992p11),
                Tuple.Create<Standard, ShearProfile?>(new StandardCNR204(), ShearProfile.CnrDT204),
                Tuple.Create<Standard, ShearProfile?>(new StandardCNR200(), ShearProfile.CnrDT200),
                Tuple.Create<Standard, ShearProfile?>(new StandardCSTR34(), null)
            };
            foreach (var pair in expected)
            {
                string name = pair.Item1.GetType().Name;
                Assert.AreEqual(pair.Item2.HasValue, ShearProfiles.TryResolve(pair.Item1, out var profile), name);
                if (pair.Item2.HasValue) Assert.AreEqual(pair.Item2.Value, profile, name);
                Assert.AreEqual(!pair.Item2.HasValue, ShearProfiles.NotApplicableReason(pair.Item1) != null, name);
            }
            var tr34 = Assert.ThrowsException<NotSupportedException>(() => SectionShearCalculator.Calculate(
                new SectionShearInput(new StandardCSTR34(), 0, 1000, 0, 150000, 300, 460, 1256, 30, 17, 391.3, 1.5, 200000, 0, 1)));
            StringAssert.Contains(tr34.Message, "CS-TR34");
            var aci = Assert.ThrowsException<NotSupportedException>(() => ShearProfiles.Resolve(new StandardACI318p19()));
            StringAssert.Contains(aci.Message, "future implementation");
        }

        // CNR-DT 204, no shear reinforcement: bw = 300, d = 460, Asl = 1256 mm², fck = 30, γc = 1.5, fFtuk = 1.5 MPa, fctk = 2.028 MPa.
        // Fibre term 1 + 7.5·1.5/2.028 = 6.548; (100·0.009101·6.548·30)^(1/3) = 5.632; VRd,F = 0.12·1.6594·5.632·300·460 = 154.8 kN.
        // With fFtuk = 0 the formula is the EC2 one (82.76 kN); fibres combined with stirrups are not implemented.
        [TestMethod]
        public void FibreReinforcedConcreteWithoutStirrupsMatchesHandCalculation()
        {
            var frc = SectionShearCalculator.Calculate(new SectionShearInput(new StandardCNR204(), 0, 100000, 0, 150000, 300, 460, 1256, 30, 17, 391.3, 1.5, 200000, 0, 1,
                residualTensileStrength: 1.5, matrixTensileStrength: 2.028));
            Assert.AreEqual(154.8, frc.VRd / 1000, .3); Assert.AreEqual(ShearProfile.CnrDT204, frc.Profile); Assert.AreEqual(ShearVerdict.Satisfied, frc.Verdict);
            var plain = SectionShearCalculator.Calculate(new SectionShearInput(new StandardCNR204(), 0, 100000, 0, 150000, 300, 460, 1256, 30, 17, 391.3, 1.5, 200000, 0, 1,
                matrixTensileStrength: 2.028));
            var ec2 = SectionShearCalculator.Calculate(new SectionShearInput(new StandardEN1992p11(), 0, 100000, 0, 150000, 300, 460, 1256, 30, 17, 391.3, 1.5, 200000, 0, 1));
            Assert.AreEqual(ec2.VRd, plain.VRd, 1e-9 * ec2.VRd);
            Assert.ThrowsException<NotSupportedException>(() => SectionShearCalculator.Calculate(new SectionShearInput(new StandardCNR204(), 0, 100000, 0, 150000, 300, 460, 1256,
                30, 17, 391.3, 1.5, 200000, 100.53, 150, residualTensileStrength: 1.5, matrixTensileStrength: 2.028)));
            Assert.ThrowsException<ArgumentException>(() => SectionShearCalculator.Calculate(new SectionShearInput(new StandardCNR204(), 0, 100000, 0, 150000, 300, 460, 1256,
                30, 17, 391.3, 1.5, 200000, 0, 1, residualTensileStrength: 1.5)));
        }

        // CNR-DT 200: no FRP data in the section, VRd,f = 0 and the NTC resistance of the member, stated in the details.
        [TestMethod]
        public void FrpStandardWithoutFrpGivesTheNtcResistanceAndStatesIt()
        {
            var ntc = SectionShearCalculator.Calculate(new SectionShearInput(new StandardNTC2018Concrete(), -300000, 200000, 0, 150000, 300, 460, 1256, 30, 17, 391.3, 1.5, 200000, 100.53, 150));
            var frp = SectionShearCalculator.Calculate(new SectionShearInput(new StandardCNR200(), -300000, 200000, 0, 150000, 300, 460, 1256, 30, 17, 391.3, 1.5, 200000, 100.53, 150));
            Assert.AreEqual(ntc.VRd, frp.VRd); Assert.AreEqual(ntc.CotTheta, frp.CotTheta); Assert.AreEqual(ShearProfile.CnrDT200, frp.Profile);
            Assert.AreEqual(0, frp.Details.Single(d => d.Symbol == "VRd,f").Value);
            StringAssert.Contains(frp.Reference, "CNR-DT 200");
        }

        [TestMethod]
        public void ZeroResistanceWithDemandIsNotSatisfiedAndTensionWithoutStirrupsIsNotEvaluated()
        {
            // NTC: σcp ≥ fcd gives αc = 0 → VRcd = 0, no defined ratio.
            var crushed = SectionShearCalculator.Calculate(new SectionShearInput(new StandardNTC2018Concrete(), -2600000, 1000, 0, 150000, 300, 460, 1256, 30, 17, 391.3, 1.5, 200000, 100.53, 150));
            Assert.AreEqual(0, crushed.VRd); Assert.IsNull(crushed.Ratio); Assert.AreEqual(ShearVerdict.NotSatisfied, crushed.Verdict);
            var tension = SectionShearCalculator.Calculate(new SectionShearInput(new StandardNTC2018Concrete(), 1000, 1000, 0, 150000, 300, 460, 1256, 30, 17, 391.3, 1.5, 200000, 0, 1));
            Assert.AreEqual(ShearVerdict.NotEvaluated, tension.Verdict); Assert.IsNull(tension.Ratio);
            // The sign of V is irrelevant for the resistance; the demand is |V|.
            var negative = SectionShearCalculator.Calculate(new SectionShearInput(new StandardEN1992p11(), 0, -50000, 0, 150000, 300, 460, 1256, 30, 17, 391.3, 1.5, 200000, 0, 1));
            Assert.AreEqual(50000, negative.Demand); Assert.AreEqual(50000 / negative.VRd, negative.Ratio.Value, 1e-12);
        }
    }
}
