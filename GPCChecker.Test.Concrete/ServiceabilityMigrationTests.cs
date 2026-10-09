using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Checkers.Concrete.Serviceability;
using GPC.Geometry;
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
    /// Serviceability stress limits moved from ANTHEA (CheckerSection.DescribeStress, commit fe4652c) to StressLimitCheck.
    /// Fixtures/stress-legacy.csv: 2016 stress states (4 sections from Fixtures/stress-sections.xml, 9 standards, linear/non linear,
    /// creep, tension, thin casting; characteristic, quasi-permanent and frequent sets), captured on 7/10/2026 from ANTHEA
    /// refactoring/integrazione-d7b-d2 d2d3225 (supporto/test/CheckerMigration.Capture, mode tutte).
    /// </summary>
    [TestClass]
    public class ServiceabilityMigrationTests
    {
        internal static StandardModelCode2010 Standard(string name)
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
                case "CNR-DT 204/2006": return new StandardCNR204();
                case "CS-TR34": return new StandardCSTR34();
                case "CNR-DT 200 R1/2013": return new StandardCNR200();
                default: throw new ArgumentException(name);
            }
        }
        private static double D(string s) => double.Parse(s, CultureInfo.InvariantCulture);
        private static double? N(string s) => s.Length == 0 ? (double?)null : D(s);
        private static void Close(double expected, double actual, string what) => Assert.AreEqual(expected, actual, 1e-9 * Math.Max(1, Math.Abs(expected)), what);

        [TestMethod]
        public void LegacyStressStatesAreReproduced()
        {
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures");
            GPC.Model.Models.Model archive;
            using (var stream = File.OpenRead(Path.Combine(folder, "stress-sections.xml"))) archive = ModelArchive.Load(stream);
            var rows = File.ReadAllLines(Path.Combine(folder, "stress-legacy.csv"), Encoding.UTF8).Where(l => !l.StartsWith("#")).Skip(1).ToArray();
            Assert.AreEqual(2016, rows.Length);
            var checkers = new Dictionary<string, SectionCheckerModelCode2010>();
            int compared = 0;
            foreach (var row in rows)
            {
                var c = row.Split(';'); string id = "state " + c[0] + " " + c[1] + " " + c[2] + " " + c[18];
                Assert.AreEqual("ok", c[19], id);
                double[] P(string s) => s.Split(',').Select(D).ToArray();
                var o = P(c[9]); var v1 = P(c[10]); var v2 = P(c[11]);
                var axes = new CoordinateSystem(new Point3d(o[0], o[1], o[2]), new Vector3d(v1[0], v1[1], v1[2]), new Vector3d(v2[0], v2[1], v2[2]));
                bool linear = bool.Parse(c[4]), tension = bool.Parse(c[6]); double psi = D(c[5]);
                string key = string.Join("|", c[1], c[2], c[3], c[4], c[5], c[6], c[9], c[10], c[11]);
                if (!checkers.TryGetValue(key, out var checker))
                {
                    var standard = Standard(c[2]);
                    foreach (var pair in c[3].Split(','))
                    {
                        var kv = pair.Split('=');
                        typeof(StandardModelCode2010).GetProperty(kv[0]).SetValue(standard, D(kv[1]));
                    }
                    var section = (ReinforcedConcreteSection)archive.BeamProperties[c[1]];
                    var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(axes, SectionSolver.FailureAnalysisTypes.ConstantN,
                        SectionSolver.FailureDomainTypes.Plastic, linear ? SectionSolver.StressAnalysisTypes.Linear : SectionSolver.StressAnalysisTypes.NonLinear,
                        psi, 0, tension, int.Parse(c[7]));
                    checker = new SectionCheckerModelCode2010(new SectionCheckerAttribute(section, null, null), options, standard, tension);
                    checkers[key] = checker;
                }
                var force = new ResultBeamForces(D(c[12]), D(c[13]), D(c[14]), D(c[15]), D(c[16]), D(c[17]), axes);
                var combination = c[18] == "SLE" ? ServiceabilityCombination.Characteristic : c[18] == "SLE_QP" ? ServiceabilityCombination.QuasiPermanent
                    : ServiceabilityCombination.Frequent;
                var limits = StressLimitCheck.Evaluate(checker.GetTensionAnalysisResult(force), combination, D(c[8]));
                Close(D(c[20]), limits.ConcreteMinStress, id + " sigmaC");
                Close(D(c[21]), limits.SteelMaxStress, id + " sigmaS");
                var ratio = N(c[22]);
                Assert.AreEqual(ratio.HasValue, limits.Ratio.HasValue, id + " ratio defined");
                if (ratio.HasValue) Close(ratio.Value, limits.Ratio.Value, id + " ratio");
                // Legacy limits: concrete with the thin-casting factor, steel k3·fyk of the first bar.
                var limitC = N(c[23]);
                if (limitC.HasValue) Close(limitC.Value, limits.ConcreteLimit.Value, id + " concrete limit");
                if (combination == ServiceabilityCombination.Characteristic && limits.SteelPoints.All(p => p.Id.StartsWith("B")))
                    Close(D(c[24]), limits.SteelPoints[0].Limit, id + " steel limit");
                compared++;
            }
            Assert.AreEqual(2016, compared);
        }

        /// <summary>
        /// Every non-American concrete standard of Model: the limits are the coefficients of the class times fck / fyk; CS-TR34 does not
        /// set stress limits. Section 300x500 C30/37, 3Ø20 + 2Ø16 B450C, N = −300 kN, M = 120 kNm, linear analysis with φ = 2.
        /// </summary>
        [TestMethod]
        public void LimitsFollowTheCoefficientsOfEveryStandard()
        {
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures");
            GPC.Model.Models.Model archive;
            using (var stream = File.OpenRead(Path.Combine(folder, "stress-sections.xml"))) archive = ModelArchive.Load(stream);
            var section = (ReinforcedConcreteSection)archive.BeamProperties["R300x500"];
            var axes = new CoordinateSystem(section.Centroid, new Vector3d(1, 0, 0), new Vector3d(0, 1, 0));
            double fck = Math.Abs(((GPC.Model.Materials.ConcreteMaterialEuropeanCommon)section.ConcreteMaterial).Fck), fyk = Math.Abs(section.Rebars.First().RebarMaterial.Fyk);
            foreach (var name in new[] { "NTC 2018", "Model Code 2010", "EN 1992-1-1", "UNI EN 1992-1-1", "DIN EN 1992-1-1", "DS EN 1992-1-1", "NS EN 1992-1-1",
                "CNR-DT 204/2006", "CS-TR34", "CNR-DT 200 R1/2013" })
            {
                var standard = Standard(name);
                Assert.AreEqual(name == "CS-TR34", StressLimitCheck.NotApplicableReason(standard) != null, name);
                var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(axes, SectionSolver.FailureAnalysisTypes.ConstantN,
                    SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.Linear, 2, 0, false, 32);
                var checker = new SectionCheckerModelCode2010(new SectionCheckerAttribute(section, null, null), options, standard, false);
                var state = checker.GetTensionAnalysisResult(new ResultBeamForces(-300000, 0, 0, 0, 120e6, 0, axes));
                var characteristic = StressLimitCheck.Evaluate(state, ServiceabilityCombination.Characteristic);
                var quasiPermanent = StressLimitCheck.Evaluate(state, ServiceabilityCombination.QuasiPermanent);
                Close(standard.ServiceabilityStressConcreteCoefficientForCharacteristicCombination * fck, characteristic.ConcreteLimit.Value, name + " k1 fck");
                Close(standard.ServiceabilityStressConcreteCoefficientForQuasiPermanentCombination * fck, quasiPermanent.ConcreteLimit.Value, name + " k2 fck");
                Close(standard.ServiceabilityStressSteelCoefficientForCharacteristicCombination * fyk, characteristic.SteelGoverning.Limit, name + " k3 fyk");
                Assert.IsTrue(characteristic.Ratio > 0 && quasiPermanent.Ratio > 0 && quasiPermanent.SteelRatio == null, name);
                Assert.AreEqual(characteristic.ConcreteMinStress, quasiPermanent.ConcreteMinStress, 1e-12, name + " same stress state");
            }
        }

        /// <summary>
        /// Every state of Fixtures/stress-legacy.csv, frequent and quasi-permanent included:
        /// - SteelLimit with the standard of the analysis (custom coefficients) and the material of the first bar equals the legacy
        ///   limitS, k3·|fyk| of the first bar (column 24);
        /// - Satisfied follows the legacy status text (column 25): "Entro limiti tensionali" true, "Oltre limiti tensionali" false,
        ///   "Stato tensionale calcolato" null;
        /// - every legacy thin-casting reduction different from 1 (column 8) is ThinCasting.Factor of the standard, with the default
        ///   rule and with Ntc2018AndItalianAnnex (all of them are NTC 2018).
        /// The expected counts come from the fixture.
        /// </summary>
        [TestMethod]
        public void SteelLimitSatisfiedAndThinCastingFollowTheLegacyOnEveryState()
        {
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures");
            GPC.Model.Models.Model archive;
            using (var stream = File.OpenRead(Path.Combine(folder, "stress-sections.xml"))) archive = ModelArchive.Load(stream);
            var rows = File.ReadAllLines(Path.Combine(folder, "stress-legacy.csv"), Encoding.UTF8).Where(l => !l.StartsWith("#")).Skip(1).ToArray();
            Assert.AreEqual(2016, rows.Length);
            var checkers = new Dictionary<string, SectionCheckerModelCode2010>();
            int steel = 0, within = 0, beyond = 0, calculated = 0, thin = 0;
            foreach (var row in rows)
            {
                var c = row.Split(';'); string id = "state " + c[0] + " " + c[1] + " " + c[2] + " " + c[18];
                double[] P(string s) => s.Split(',').Select(D).ToArray();
                var o = P(c[9]); var v1 = P(c[10]); var v2 = P(c[11]);
                var axes = new CoordinateSystem(new Point3d(o[0], o[1], o[2]), new Vector3d(v1[0], v1[1], v1[2]), new Vector3d(v2[0], v2[1], v2[2]));
                bool linear = bool.Parse(c[4]), tension = bool.Parse(c[6]); double psi = D(c[5]);
                string key = string.Join("|", c[1], c[2], c[3], c[4], c[5], c[6], c[9], c[10], c[11]);
                var section = (ReinforcedConcreteSection)archive.BeamProperties[c[1]];
                if (!checkers.TryGetValue(key, out var checker))
                {
                    var custom = Standard(c[2]);
                    foreach (var pair in c[3].Split(','))
                    {
                        var kv = pair.Split('=');
                        typeof(StandardModelCode2010).GetProperty(kv[0]).SetValue(custom, D(kv[1]));
                    }
                    var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(axes, SectionSolver.FailureAnalysisTypes.ConstantN,
                        SectionSolver.FailureDomainTypes.Plastic, linear ? SectionSolver.StressAnalysisTypes.Linear : SectionSolver.StressAnalysisTypes.NonLinear,
                        psi, 0, tension, int.Parse(c[7]));
                    checker = new SectionCheckerModelCode2010(new SectionCheckerAttribute(section, null, null), options, custom, tension);
                    checkers[key] = checker;
                }
                var state = checker.GetTensionAnalysisResult(new ResultBeamForces(D(c[12]), D(c[13]), D(c[14]), D(c[15]), D(c[16]), D(c[17]), axes));
                var standard = (StandardModelCode2010)state.Standard;
                Assert.AreEqual(Standard(c[2]).GetType(), standard.GetType(), id + " standard of the analysis");
                Close(D(c[24]), StressLimitCheck.SteelLimit(standard, section.Rebars.First().RebarMaterial), id + " SteelLimit");
                steel++;

                double reduction = D(c[8]);
                if (reduction != 1)
                {
                    Assert.AreEqual(reduction, ThinCasting.Factor(standard), id + " thin casting");
                    Assert.AreEqual(reduction, ThinCasting.Factor(standard, ThinCastingRule.Ntc2018AndItalianAnnex), id + " thin casting, Italian annex rule");
                    thin++;
                }
                var combination = c[18] == "SLE" ? ServiceabilityCombination.Characteristic : c[18] == "SLE_QP" ? ServiceabilityCombination.QuasiPermanent
                    : ServiceabilityCombination.Frequent;
                var limits = StressLimitCheck.Evaluate(state, combination, reduction);
                switch (c[25])
                {
                    case "Entro limiti tensionali": Assert.AreEqual(true, limits.Satisfied, id + " within"); within++; break;
                    case "Oltre limiti tensionali": Assert.AreEqual(false, limits.Satisfied, id + " beyond"); beyond++; break;
                    case "Stato tensionale calcolato": Assert.IsNull(limits.Satisfied, id + " calculated"); calculated++; break;
                    default: Assert.Fail(id + " unknown legacy status " + c[25]); break;
                }
            }
            Assert.AreEqual(2016, steel);
            Assert.AreEqual(1250, within);
            Assert.AreEqual(94, beyond);
            Assert.AreEqual(672, calculated);
            Assert.AreEqual(72, thin);

            var material = ((ReinforcedConcreteSection)archive.BeamProperties["R300x500"]).Rebars.First().RebarMaterial;
            Assert.ThrowsException<ArgumentNullException>(() => StressLimitCheck.SteelLimit(null, material));
            Assert.ThrowsException<ArgumentNullException>(() => StressLimitCheck.SteelLimit(new StandardNTC2018Concrete(), null));
        }

        /// <summary>
        /// Linear analysis with φ = 2 of the 300×500 C30/37 with 3Ø20 + 2Ø16 B450C of Fixtures/stress-sections.xml, N = −300 kN,
        /// M = 120 kNm, under the given standard (the state of LimitsFollowTheCoefficientsOfEveryStandard).
        /// </summary>
        private static GPC.Checkers.Concrete.Results.StressAnalysisResult R300x500State(StandardModelCode2010 standard, out ReinforcedConcreteSection section)
        {
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures");
            GPC.Model.Models.Model archive;
            using (var stream = File.OpenRead(Path.Combine(folder, "stress-sections.xml"))) archive = ModelArchive.Load(stream);
            section = (ReinforcedConcreteSection)archive.BeamProperties["R300x500"];
            var axes = new CoordinateSystem(section.Centroid, new Vector3d(1, 0, 0), new Vector3d(0, 1, 0));
            var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(axes, SectionSolver.FailureAnalysisTypes.ConstantN,
                SectionSolver.FailureDomainTypes.Plastic, SectionSolver.StressAnalysisTypes.Linear, 2, 0, false, 32);
            var checker = new SectionCheckerModelCode2010(new SectionCheckerAttribute(section, null, null), options, standard, false);
            return checker.GetTensionAnalysisResult(new ResultBeamForces(-300000, 0, 0, 0, 120e6, 0, axes));
        }

        /// <summary>
        /// SteelLimit by hand with custom coefficients, which the fixture cannot tell apart (k3 = 0.8 and limitS = 360 MPa on all its
        /// 2016 rows). B450C, fyk = 450 MPa: k3 = 0.7 gives 315 MPa, k3 = 0.6 gives 270 MPa, k3 = 1 gives 450 MPa, the default
        /// k3 = 0.8 gives 360 MPa. Through the analysis, as the adapter of ANTHEA passes it (the standard of the stress result, with
        /// the custom coefficients), k3 = 0.7 gives 315 MPa, the limit of every bar of Evaluate in the characteristic combination.
        /// </summary>
        [TestMethod]
        public void SteelLimitUsesTheCoefficientOfTheGivenStandard()
        {
            var custom = new StandardNTC2018Concrete { ServiceabilityStressSteelCoefficientForCharacteristicCombination = 0.7 };
            var state = R300x500State(custom, out var section);
            var material = section.Rebars.First().RebarMaterial;
            Assert.AreEqual(450, Math.Abs(material.Fyk), "B450C");
            var cases = new (string Name, StandardModelCode2010 Standard, double Expected)[]
            {
                ("NTC 2018, k3 = 0.7", new StandardNTC2018Concrete { ServiceabilityStressSteelCoefficientForCharacteristicCombination = 0.7 }, 315),
                ("UNI EN 1992-1-1, k3 = 0.6", new StandardUNIEN1992p11 { ServiceabilityStressSteelCoefficientForCharacteristicCombination = 0.6 }, 270),
                ("EN 1992-1-1, k3 = 1", new StandardEN1992p11 { ServiceabilityStressSteelCoefficientForCharacteristicCombination = 1 }, 450),
                ("NTC 2018, default k3 = 0.8", new StandardNTC2018Concrete(), 360)
            };
            foreach (var c in cases) Close(c.Expected, StressLimitCheck.SteelLimit(c.Standard, material), c.Name);

            var standard = (StandardModelCode2010)state.Standard;
            Assert.AreEqual(0.7, standard.ServiceabilityStressSteelCoefficientForCharacteristicCombination, "custom coefficient of the analysis");
            Close(315, StressLimitCheck.SteelLimit(standard, material), "standard of the analysis");
            var characteristic = StressLimitCheck.Evaluate(state, ServiceabilityCombination.Characteristic);
            Assert.AreEqual(section.Rebars.Count(), characteristic.SteelPoints.Count, "one point per bar");
            foreach (var p in characteristic.SteelPoints) Close(315, p.Limit, "Evaluate, bar " + p.Id);
        }

        /// <summary>
        /// Satisfied at the border, which no row of the fixture reaches: the legacy status is "Entro limiti tensionali" for
        /// ratio ≤ 1 (aw-int2 X.Calculations/CheckerSection.cs:199). On the quasi-permanent state of R300x500State (NTC 2018) the
        /// ratio with factor 1 is r = |σc|/(k2 fck) &lt; 1. With concreteLimitFactor = r the governing ratio is r/r = 1 exactly and
        /// Satisfied is true; with the factor one ulp below r the ratio is above 1 and Satisfied is false.
        /// </summary>
        [TestMethod]
        public void SatisfiedIncludesTheLimit()
        {
            var state = R300x500State(new StandardNTC2018Concrete(), out _);
            var free = StressLimitCheck.Evaluate(state, ServiceabilityCombination.QuasiPermanent);
            double r = free.Ratio.Value;
            Assert.IsTrue(r > 0 && r < 1, "ratio with factor 1: " + r.ToString("R", CultureInfo.InvariantCulture));
            Assert.AreEqual(true, free.Satisfied, "below the limit");

            var atLimit = StressLimitCheck.Evaluate(state, ServiceabilityCombination.QuasiPermanent, r);
            Assert.AreEqual(1.0, atLimit.Ratio.Value, "ratio exactly 1");
            Assert.AreEqual(true, atLimit.Satisfied, "ratio = 1 is within the limits");

            double justBelow = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(r) - 1);
            var beyond = StressLimitCheck.Evaluate(state, ServiceabilityCombination.QuasiPermanent, justBelow);
            Assert.IsTrue(beyond.Ratio.Value > 1, "ratio one step beyond 1: " + beyond.Ratio.Value.ToString("R", CultureInfo.InvariantCulture));
            Assert.AreEqual(false, beyond.Satisfied, "ratio > 1 is beyond the limits");

            Assert.IsNull(StressLimitCheck.Evaluate(state, ServiceabilityCombination.Frequent).Satisfied, "frequent: no ratio");
        }

        /// <summary>
        /// Thin-casting factor of the 9 standards of ANTHEA, created as ANTHEA creates them (ConcreteStandards.Create), plus
        /// CNR-DT 200 (derived from NTC 2018) and ACI 318-19, under both rules. The expected values are written by hand:
        /// - Ntc2018Only: the legacy rule of ANTHEA (CheckerSection.cs:77, ConcreteMaterials.cs:11: 0.8 only when the standard
        ///   is "NTC 2018"), NTC 2018 §4.1.2.1.1.1 and §4.1.2.2.5.1;
        /// - Ntc2018AndItalianAnnex: the method page ca.sle-tensioni (docs/metodi/ca.sle-tensioni.md:143-144, :219), which adds
        ///   DM 31/07/2012 7.2, that is UNI EN 1992-1-1 with the Italian National Annex, and nothing else.
        /// The rule follows the exact class: custom coefficients (ConcreteStandards.Effective, "coefficienti_unitari") and a
        /// different name do not change it.
        /// </summary>
        [TestMethod]
        public void ThinCastingFactorOfEveryStandard()
        {
            var grid = new (string Name, Standard Standard, double Default, double WithAnnex)[]
            {
                ("NTC 2018", new StandardNTC2018Concrete(), 0.8, 0.8),
                ("Model Code 2010", new StandardModelCode2010(), 1, 1),
                ("EN 1992-1-1", new StandardEN1992p11(), 1, 1),
                ("UNI EN 1992-1-1", new StandardUNIEN1992p11 { AlphaCC = .85 }, 1, 0.8),
                ("DIN EN 1992-1-1", new StandardDINEN1992p11 { AlphaCT = .85 }, 1, 1),
                ("DS EN 1992-1-1", new StandardDSEN1992p11(), 1, 1),
                ("NS EN 1992-1-1", new StandardNSEN1992p11 { AlphaCT = .85, SteelCoefficientStrainTension = .4 }, 1, 1),
                ("CNR-DT 204/2006", new StandardCNR204(), 1, 1),
                ("CS-TR34", new StandardCSTR34(), 1, 1),
                ("CNR-DT 200 R1/2013", new StandardCNR200(), 1, 1),
                ("ACI 318-19", new StandardACI318p19(), 1, 1),
                ("NTC 2018, unit coefficients", new StandardNTC2018Concrete { AlphaCC = 1, GammaC = 1, GammaS = 1 }, 0.8, 0.8),
                ("UNI EN 1992-1-1, unit coefficients", new StandardUNIEN1992p11 { AlphaCC = 1, GammaC = 1, GammaS = 1 }, 1, 0.8),
                ("UNI EN 1992-1-1, custom SLS coefficients", new StandardUNIEN1992p11
                {
                    ServiceabilityStressConcreteCoefficientForCharacteristicCombination = .5,
                    ServiceabilityStressConcreteCoefficientForQuasiPermanentCombination = .4
                }, 1, 0.8),
                ("UNI EN 1992-1-1, other name", new StandardUNIEN1992p11("Progetto"), 1, 0.8)
            };
            var anthea = new[] { "NTC 2018", "Model Code 2010", "EN 1992-1-1", "UNI EN 1992-1-1", "DIN EN 1992-1-1", "DS EN 1992-1-1",
                "NS EN 1992-1-1", "CNR-DT 204/2006", "CS-TR34" };
            CollectionAssert.IsSubsetOf(anthea, grid.Select(g => g.Name).ToArray(), "the 9 standards of ANTHEA (ConcreteStandards.Names)");
            foreach (var g in grid)
            {
                Assert.AreEqual(g.Default, ThinCasting.Factor(g.Standard, ThinCastingRule.Ntc2018Only), g.Name + " NTC 2018 only rule");
                Assert.AreEqual(g.WithAnnex, ThinCasting.Factor(g.Standard, ThinCastingRule.Ntc2018AndItalianAnnex), g.Name + " NTC 2018 and Italian annex rule");
            }
            Assert.ThrowsException<ArgumentNullException>(() => ThinCasting.Factor(null, ThinCastingRule.Ntc2018Only));
            Assert.ThrowsException<ArgumentNullException>(() => ThinCasting.Factor(null, ThinCastingRule.Ntc2018AndItalianAnnex));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ThinCasting.Factor(new StandardNTC2018Concrete(), (ThinCastingRule)2));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ThinCasting.Factor(new StandardUNIEN1992p11(), (ThinCastingRule)(-1)));
        }

        /// <summary>
        /// The default does not change: the overload without a rule, the enum value 0 and default(ThinCastingRule) all mean
        /// Ntc2018Only, which gives the values of ANTHEA before F2.7 (0.8 only for NTC 2018, 1 for UNI EN 1992-1-1 and every
        /// other standard). The enum has exactly the two values 0 and 1.
        /// </summary>
        [TestMethod]
        public void ThinCastingDefaultRuleKeepsTheValuesBeforeF27()
        {
            Assert.AreEqual(ThinCastingRule.Ntc2018Only, default(ThinCastingRule));
            Assert.AreEqual(0, (int)ThinCastingRule.Ntc2018Only);
            Assert.AreEqual(1, (int)ThinCastingRule.Ntc2018AndItalianAnnex);
            CollectionAssert.AreEqual(new[] { "Ntc2018Only", "Ntc2018AndItalianAnnex" }, Enum.GetNames(typeof(ThinCastingRule)));

            var standards = new (string Name, Standard Standard, double Before)[]
            {
                ("NTC 2018", new StandardNTC2018Concrete(), 0.8),
                ("Model Code 2010", new StandardModelCode2010(), 1),
                ("EN 1992-1-1", new StandardEN1992p11(), 1),
                ("UNI EN 1992-1-1", new StandardUNIEN1992p11 { AlphaCC = .85 }, 1),
                ("DIN EN 1992-1-1", new StandardDINEN1992p11 { AlphaCT = .85 }, 1),
                ("DS EN 1992-1-1", new StandardDSEN1992p11(), 1),
                ("NS EN 1992-1-1", new StandardNSEN1992p11 { AlphaCT = .85, SteelCoefficientStrainTension = .4 }, 1),
                ("CNR-DT 204/2006", new StandardCNR204(), 1),
                ("CS-TR34", new StandardCSTR34(), 1),
                ("CNR-DT 200 R1/2013", new StandardCNR200(), 1),
                ("ACI 318-19", new StandardACI318p19(), 1)
            };
            foreach (var s in standards)
            {
                Assert.AreEqual(s.Before, ThinCasting.Factor(s.Standard), s.Name + " overload without a rule");
                Assert.AreEqual(s.Before, ThinCasting.Factor(s.Standard, default(ThinCastingRule)), s.Name + " default(ThinCastingRule)");
            }
            Assert.ThrowsException<ArgumentNullException>(() => ThinCasting.Factor(null));
        }

        // Homogenization: the forms of ANTHEA (branch aw-int2, commit 9bc4589), rewritten here as expected values with the
        // names and the order of the operations of the source. They are not calls of the library.

        /// <summary>X.Calculations/ConcreteSectionProperties.cs:44-53, Anthea.Calculations.Homogenization.Resolve (net8 double.IsFinite written out).</summary>
        private static (double Phi, double N) AntheaResolve(double steelModulus, double concreteModulus, bool fromN, double value)
        {
            if (!AntheaIsFinite(steelModulus) || steelModulus <= 0 || !AntheaIsFinite(concreteModulus) || concreteModulus <= 0 || !AntheaIsFinite(value))
                throw new ArgumentException("Omogeneizzazione: inserire moduli elastici finiti positivi e un valore numerico.");
            double phi = fromN ? value * concreteModulus / steelModulus - 1 : value;
            if (!AntheaIsFinite(phi) || phi < -1e-12) throw new ArgumentException("Inserire φ ≥ 0 oppure n ≥ Es/Ecm.");
            phi = Math.Max(0, phi); double n = steelModulus * (1 + phi) / concreteModulus;
            if (!AntheaIsFinite(n)) throw new ArgumentException("Omogeneizzazione fuori intervallo numerico.");
            return (phi, n);
        }
        private static bool AntheaIsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        /// <summary>X.Desktop/Wpf/ConcreteStress.cs:178 (stress tab, n given): phi = n * ec / modulus - 1.</summary>
        private static double AntheaStressTabPhi(double n, double ec, double modulus) => n * ec / modulus - 1;
        /// <summary>X.Desktop/Wpf/ConcreteStress.cs:183 (stress tab, φ given): n = modulus * (1 + φ) / ec.</summary>
        private static double AntheaStressTabN(double modulus, double phi, double ec) => modulus * (1 + phi) / ec;
        /// <summary>X.Core/ReportConcreteShort.cs:78: modularRatio = steel_modulus_mpa * (1 + phi) / Concrete(input).E.</summary>
        private static double AntheaShortReportN(double steelModulus, double phi, double e) => steelModulus * (1 + phi) / e;
        /// <summary>X.Calculations/Ntc2018Checks.cs:238 («n analisi» of the crack trace): es * (1 + (native.PsiRebar ?? 0)) / concrete.E.</summary>
        private static double AntheaCrackTraceN(double es, double? psiRebar, double e) => es * (1 + (psiRebar ?? 0)) / e;
        /// <summary>Rejection of the library that corresponds to each message of AntheaResolve.</summary>
        private static HomogenizationRejection AntheaRejection(string message)
        {
            switch (message)
            {
                case "Omogeneizzazione: inserire moduli elastici finiti positivi e un valore numerico.": return HomogenizationRejection.InvalidInput;
                case "Inserire φ ≥ 0 oppure n ≥ Es/Ecm.": return HomogenizationRejection.NegativeCreep;
                case "Omogeneizzazione fuori intervallo numerico.": return HomogenizationRejection.RatioOutOfRange;
                default: throw new AssertFailedException("Unknown ANTHEA message: " + message);
            }
        }
        private static void SameBits(double expected, double actual, string what)
            => Assert.AreEqual(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual), $"{what}: expected {expected:R}, actual {actual:R}");

        /// <summary>
        /// Resolve compared with AntheaResolve: same φ and n bit for bit, or the same rejection (exact type ArgumentException and
        /// the reason that corresponds to the message of ANTHEA). Returns true when the arguments are accepted.
        /// </summary>
        private static bool ResolveMatchesAnthea(double es, double ec, bool fromN, double value, string what)
        {
            (double Phi, double N) expected;
            try { expected = AntheaResolve(es, ec, fromN, value); }
            catch (ArgumentException e)
            {
                var reason = AntheaRejection(e.Message);
                var rejection = Assert.ThrowsException<ArgumentException>(() => Homogenization.Resolve(es, ec, fromN, value), what);
                Assert.AreEqual((object)reason, rejection.Data[Homogenization.RejectionKey], what + ": reason");
                return false;
            }
            var actual = Homogenization.Resolve(es, ec, fromN, value);
            SameBits(expected.Phi, actual.Phi, what + ": φ");
            SameBits(expected.N, actual.N, what + ": n");
            return true;
        }

        /// <summary>
        /// Grid φ ∈ {−0.5, −1e-13, 0, 0.5, 2, 15} on 3 steel moduli (200000, 210000 and 195000 MPa, the default Ep of ANTHEA)
        /// and 4 concrete moduli (Ecm = 22000·(fcm/10)^0.3 for fcm 28, 38 and 48 MPa, and 30000 MPa). ModularRatio and
        /// CreepFromModularRatio are bit for bit the expressions of ANTHEA (stress tab, short report, crack trace, Resolve), also
        /// with φ &lt; 0, which they accept as the stress tab does (F2.7-D6). Resolve from φ and from n = ModularRatio(φ) gives the φ
        /// and n of ANTHEA bit for bit or the same rejection: φ = −0.5 is rejected, φ = −1e-13 is within the tolerance and gives 0.
        /// </summary>
        [TestMethod]
        public void HomogenizationIsBitForBitTheArithmeticOfAnthea()
        {
            var phis = new[] { -0.5, -1e-13, 0, 0.5, 2, 15 };
            var steelModuli = new[] { 200000.0, 210000.0, 195000.0 };
            var concreteModuli = new[] { 22000 * Math.Pow(28 / 10.0, 0.3), 22000 * Math.Pow(38 / 10.0, 0.3), 22000 * Math.Pow(48 / 10.0, 0.3), 30000.0 };
            int cases = 0, accepted = 0, rejected = 0;
            foreach (double es in steelModuli)
                foreach (double ec in concreteModuli)
                {
                    SameBits(AntheaCrackTraceN(es, null, ec), Homogenization.ModularRatio(es, ec, 0), $"Es {es:R}, Ec {ec:R}: n without creep, Ntc2018Checks.cs:238");
                    foreach (double phi in phis)
                    {
                        string what = $"Es {es:R}, Ec {ec:R}, φ {phi:R}";
                        double n = Homogenization.ModularRatio(es, ec, phi);
                        SameBits(AntheaStressTabN(es, phi, ec), n, what + ": n, ConcreteStress.cs:183");
                        SameBits(AntheaShortReportN(es, phi, ec), n, what + ": n, ReportConcreteShort.cs:78");
                        SameBits(AntheaCrackTraceN(es, phi, ec), n, what + ": n, Ntc2018Checks.cs:238");
                        double back = Homogenization.CreepFromModularRatio(n, es, ec);
                        SameBits(AntheaStressTabPhi(n, ec, es), back, what + ": φ from n, ConcreteStress.cs:178");

                        if (ResolveMatchesAnthea(es, ec, false, phi, what + ", Resolve from φ")) accepted++; else rejected++;
                        if (ResolveMatchesAnthea(es, ec, true, n, what + ", Resolve from n")) accepted++; else rejected++;
                        cases += 2;

                        if (phi == -1e-13)
                        {
                            var fromPhi = Homogenization.Resolve(es, ec, false, phi);
                            Assert.AreEqual(0L, BitConverter.DoubleToInt64Bits(fromPhi.Phi), what + ": φ within the tolerance becomes +0");
                            SameBits(es / ec, fromPhi.N, what + ": n = Es/Ec");
                        }
                    }
                }
            Assert.AreEqual(144, cases);
            Assert.AreEqual(24, rejected, "φ = −0.5 from φ and from n, on the 12 pairs of moduli");
            Assert.AreEqual(120, accepted);
        }

        /// <summary>
        /// Rejections of Resolve, each compared with ANTHEA (ConcreteSectionProperties.cs:46-51), and hand calculations with values
        /// exact in binary (Es = 200000, Ec = 32000 MPa, Es/Ec = 6.25). φ below −1e-12 is rejected, also when it comes from n;
        /// −1e-12 itself is accepted and gives 0. ModularRatio and CreepFromModularRatio have no checks.
        /// </summary>
        [TestMethod]
        public void HomogenizationResolveRejectsAsAnthea()
        {
            Assert.AreEqual(1e-12, Homogenization.CreepTolerance);
            Assert.AreEqual("GPC.Checkers.Concrete.Serviceability.HomogenizationRejection", Homogenization.RejectionKey);
            CollectionAssert.AreEqual(new[] { "InvalidInput", "NegativeCreep", "RatioOutOfRange" }, Enum.GetNames(typeof(HomogenizationRejection)));
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, Enum.GetValues(typeof(HomogenizationRejection)).Cast<int>().ToArray());

            // Hand calculations: n = 200000·(1 + 2)/32000 = 18.75; φ = 18.75·32000/200000 − 1 = 2; n = Es/Ec = 6.25 gives φ = 0.
            var r = Homogenization.Resolve(200000, 32000, false, 2);
            Assert.AreEqual(2, r.Phi); Assert.AreEqual(18.75, r.N);
            r = Homogenization.Resolve(200000, 32000, true, 18.75);
            Assert.AreEqual(2, r.Phi); Assert.AreEqual(18.75, r.N);
            r = Homogenization.Resolve(200000, 32000, true, 6.25);
            Assert.AreEqual(0, r.Phi); Assert.AreEqual(6.25, r.N);
            r = Homogenization.Resolve(200000, 32000, false, -1e-12);
            Assert.AreEqual(0L, BitConverter.DoubleToInt64Bits(r.Phi), "φ = −1e-12 is accepted and becomes +0"); Assert.AreEqual(6.25, r.N);

            // No checks in the arithmetic: φ = −0.5 gives n = 3.125 < Es/Ec, and back (stress tab of ANTHEA, F2.7-D6).
            Assert.AreEqual(3.125, Homogenization.ModularRatio(200000, 32000, -0.5));
            Assert.AreEqual(-0.5, Homogenization.CreepFromModularRatio(3.125, 200000, 32000));
            Assert.IsTrue(double.IsNaN(Homogenization.ModularRatio(200000, 32000, double.NaN)));
            Assert.IsTrue(double.IsNaN(Homogenization.CreepFromModularRatio(double.NaN, 200000, 32000)));
            Assert.AreEqual(double.PositiveInfinity, Homogenization.ModularRatio(200000, 0, 0));

            double belowTolerance = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(-1e-12) + 1);
            Assert.IsTrue(belowTolerance < -1e-12);
            var rejections = new (string What, double Es, double Ec, bool FromN, double Value, HomogenizationRejection Reason)[]
            {
                ("φ = −0.5", 200000, 32000, false, -0.5, HomogenizationRejection.NegativeCreep),
                ("φ = −2e-12", 200000, 32000, false, -2e-12, HomogenizationRejection.NegativeCreep),
                ("φ one ulp below −1e-12", 200000, 32000, false, belowTolerance, HomogenizationRejection.NegativeCreep),
                ("n = 6 < Es/Ec, φ = −0.04", 200000, 32000, true, 6, HomogenizationRejection.NegativeCreep),
                ("n = 6.25·(1 − 1e-11), φ ≈ −1e-11", 200000, 32000, true, 6.25 * (1 - 1e-11), HomogenizationRejection.NegativeCreep),
                ("n = 0", 200000, 32000, true, 0, HomogenizationRejection.NegativeCreep),
                ("φ infinite from n", 1, 2, true, double.MaxValue, HomogenizationRejection.NegativeCreep),
                ("Es = 0", 0, 32000, false, 0, HomogenizationRejection.InvalidInput),
                ("Es < 0", -200000, 32000, false, 0, HomogenizationRejection.InvalidInput),
                ("Es NaN", double.NaN, 32000, false, 0, HomogenizationRejection.InvalidInput),
                ("Es infinite", double.PositiveInfinity, 32000, true, 6.25, HomogenizationRejection.InvalidInput),
                ("Ec = 0", 200000, 0, false, 0, HomogenizationRejection.InvalidInput),
                ("Ec < 0", 200000, -32000, true, 6.25, HomogenizationRejection.InvalidInput),
                ("Ec NaN", 200000, double.NaN, false, 0, HomogenizationRejection.InvalidInput),
                ("Ec infinite", 200000, double.NegativeInfinity, false, 0, HomogenizationRejection.InvalidInput),
                ("φ NaN", 200000, 32000, false, double.NaN, HomogenizationRejection.InvalidInput),
                ("φ infinite", 200000, 32000, false, double.PositiveInfinity, HomogenizationRejection.InvalidInput),
                ("n NaN", 200000, 32000, true, double.NaN, HomogenizationRejection.InvalidInput),
                ("n infinite", 200000, 32000, true, double.NegativeInfinity, HomogenizationRejection.InvalidInput),
                ("n infinite from φ", double.MaxValue, 0.5, false, 0, HomogenizationRejection.RatioOutOfRange),
                ("n infinite from a large φ", 1e10, 1, false, 1e300, HomogenizationRejection.RatioOutOfRange)
            };
            var messages = new Dictionary<HomogenizationRejection, string>
            {
                { HomogenizationRejection.InvalidInput, "Homogenization: the elastic moduli must be finite and positive and the value a finite number." },
                { HomogenizationRejection.NegativeCreep, "Homogenization: φ must be ≥ 0, that is n ≥ Es/Ec." },
                { HomogenizationRejection.RatioOutOfRange, "Homogenization: the modular ratio is outside the numeric range." }
            };
            foreach (var c in rejections)
            {
                Assert.IsFalse(ResolveMatchesAnthea(c.Es, c.Ec, c.FromN, c.Value, c.What), c.What + ": accepted");
                var e = Assert.ThrowsException<ArgumentException>(() => Homogenization.Resolve(c.Es, c.Ec, c.FromN, c.Value), c.What);
                Assert.AreEqual((object)c.Reason, e.Data[Homogenization.RejectionKey], c.What + ": reason");
                Assert.AreEqual(messages[c.Reason], e.Message, c.What + ": message");
                Assert.IsNull(e.ParamName, c.What + ": no parameter name");
            }
            // The thrown message equals the expected text above, which holds U+03C6 and U+2265: both sources were read as UTF-8.
            Assert.IsTrue(messages[HomogenizationRejection.NegativeCreep].Contains("φ must be ≥ 0"), "message decoded as UTF-8");
            foreach (HomogenizationRejection reason in Enum.GetValues(typeof(HomogenizationRejection)))
                Assert.IsTrue(rejections.Any(c => c.Reason == reason), reason + " covered");
        }
    }
}
