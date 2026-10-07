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
    }
}
