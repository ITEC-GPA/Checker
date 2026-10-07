using GPC.Checkers.Concrete.Durability;
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
    /// Durability and covers moved from ANTHEA (Materiali.Durability, NtcCover, MinimumConcrete, AtecapMix; commit fe4652c).
    /// Fixtures/durability-legacy.csv: EC2 4.4N and NTC covers on 24 exposure combinations × 7 strengths × 6 option sets, UNI 11104 minimum classes
    /// and mix limits, captured on 7/10/2026 from ANTHEA refactoring/integrazione-d7b-d2 d2d3225 (supporto/test/CheckerMigration.Capture, mode tutte).
    /// </summary>
    [TestClass]
    public class DurabilityMigrationTests
    {
        private static double D(string s) => double.Parse(s, CultureInfo.InvariantCulture);
        private static double? N(string s) => s.Length == 0 ? (double?)null : D(s);
        private static string[] Rows() => File.ReadAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "durability-legacy.csv"), Encoding.UTF8).Where(l => !l.StartsWith("#")).ToArray();

        [TestMethod]
        public void LegacyCoversAndMixRequirementsAreReproduced()
        {
            int ec2 = 0, ntc = 0, errors = 0, mix = 0;
            foreach (var row in Rows())
            {
                var c = row.Split(';'); var codes = c[1].Split('+');
                if (c[0] == "MIX")
                {
                    if (c[2].StartsWith("error")) { Assert.ThrowsException<ArgumentException>(() => ExposureClasses.Uni11104MinimumStrength(codes), c[1]); errors++; continue; }
                    Assert.AreEqual((int)D(c[2]), ExposureClasses.Uni11104MinimumStrength(codes), c[1]);
                    var limits = ExposureClasses.Uni11104Mix(codes);
                    Assert.AreEqual(N(c[3]), limits.Item1, c[1] + " w/c"); Assert.AreEqual(c[4].Length == 0 ? (int?)null : int.Parse(c[4]), limits.Item2, c[1] + " cement");
                    Assert.AreEqual(N(c[5]), ExposureClasses.Uni11104Air(codes, 16), c[1]); Assert.AreEqual(N(c[6]), ExposureClasses.Uni11104Air(codes, 32), c[1]);
                    Assert.AreEqual(N(c[7]), ExposureClasses.Uni11104Air(codes, 8), c[1]);
                    mix++; continue;
                }
                bool isNtc = c[0] == "NTC";
                string[] q = isNtc ? c[14].Split(':') : new[] { "False" };
                var input = new CoverInput(codes, D(c[2]), int.Parse(c[3]), bool.Parse(c[4]), bool.Parse(c[5]), bool.Parse(c[6]), D(c[7]), D(c[8]), D(c[9]), bool.Parse(c[10]),
                    int.Parse(c[11]), int.Parse(c[12]), isNtc && bool.Parse(c[13]), isNtc && bool.Parse(q[0]), isNtc && q.Length > 1 ? D(q[1]) : (double?)null);
                var profile = isNtc ? DurabilityProfile.Ntc2018 : DurabilityProfile.EN1992p11; string id = row.Substring(0, Math.Min(80, row.Length));
                if (c[15] != "ok") { Assert.AreEqual("error:ArgumentException", c[15], id); Assert.ThrowsException<ArgumentException>(() => CoverRequirements.Calculate(profile, input), id); errors++; continue; }
                var r = CoverRequirements.Calculate(profile, input);
                Assert.AreEqual(D(c[16]), r.Bond, 1e-12, id); Assert.AreEqual(D(c[17]), r.Durability, 1e-12, id); Assert.AreEqual(D(c[18]), r.Minimum, 1e-12, id);
                Assert.AreEqual(D(c[19]), r.Nominal, 1e-12, id);
                if (isNtc)
                {
                    var t = c[20].Split('|');
                    Assert.AreEqual(int.Parse(t[1]), r.NtcEnvironment, id); Assert.AreEqual(D(t[2]), r.NtcCmin, id); Assert.AreEqual(D(t[3]), r.NtcC0, id); Assert.AreEqual(D(t[4]), r.NtcTable, id);
                    Assert.AreEqual(D(t[5]), r.NtcLifeExtra, id); Assert.AreEqual(D(t[6]), r.NtcLowStrengthExtra, id); Assert.AreEqual(D(t[7]), r.NtcQualityReduction, id);
                    ntc++;
                }
                else
                {
                    var lines = c[20].Split('|');
                    Assert.AreEqual(lines.Length, r.Lines.Count, id);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        var parts = lines[i].Split(':');
                        Assert.AreEqual(parts[0], r.Lines[i].Exposure, id); Assert.AreEqual(int.Parse(parts[1]), r.Lines[i].StructuralClass, id); Assert.AreEqual(D(parts[2]), r.Lines[i].Durability, id);
                    }
                    ec2++;
                }
            }
            Assert.AreEqual(588, ec2); Assert.AreEqual(1932, ntc); Assert.AreEqual(420 + 84 + 1, errors); Assert.AreEqual(23, mix);
        }

        /// <summary>Hand values: EN Table 4.4N, DK NA Tabel 4.4N NA (no structural classes) and the NTC Circolare table.</summary>
        [TestMethod]
        public void CoversMatchTheTablesOfEachStandard()
        {
            CoverInput Input(string exposure, double fck = 30, int life = 50, double deviation = 10, bool plate = false) => new CoverInput(new[] { exposure }, fck, life, false, false, false, 16, 20, deviation, plateElement: plate);
            // EN: XC3, S4 → 25 mm; 100 years → S6 → 35 mm; cnom = cmin + Δcdev.
            Assert.AreEqual(25, CoverRequirements.Calculate(DurabilityProfile.EN1992p11, Input("XC3")).Durability);
            Assert.AreEqual(4, CoverRequirements.Calculate(DurabilityProfile.EN1992p11, Input("XC3")).Lines.Single().StructuralClass);
            Assert.AreEqual(35, CoverRequirements.Calculate(DurabilityProfile.EN1992p11, Input("XC3", life: 100)).Durability);
            Assert.AreEqual(35, CoverRequirements.Calculate(DurabilityProfile.EN1992p11, Input("XC3")).Nominal);
            // UNI (DM 31/07/2012): recommended values, same as EN.
            Assert.AreEqual(CoverRequirements.Calculate(DurabilityProfile.EN1992p11, Input("XD1")).Nominal, CoverRequirements.Calculate(DurabilityProfile.UniEN1992p11, Input("XD1")).Nominal);
            // DK NA: XC3 20, XD1 30, XS3 40, X0 10 mm; Δcdev ≥ 5 mm; 100 years not implemented.
            Assert.AreEqual(20, CoverRequirements.Calculate(DurabilityProfile.DsEN1992p11, Input("XC3")).Durability);
            Assert.AreEqual(30, CoverRequirements.Calculate(DurabilityProfile.DsEN1992p11, Input("XD1")).Durability);
            Assert.AreEqual(40, CoverRequirements.Calculate(DurabilityProfile.DsEN1992p11, Input("XS3")).Durability);
            Assert.AreEqual(16, CoverRequirements.Calculate(DurabilityProfile.DsEN1992p11, Input("X0")).Minimum, "cmin,b = Ø governs");
            Assert.ThrowsException<ArgumentException>(() => CoverRequirements.Calculate(DurabilityProfile.DsEN1992p11, Input("XC3", deviation: 3)));
            Assert.ThrowsException<NotSupportedException>(() => CoverRequirements.Calculate(DurabilityProfile.DsEN1992p11, Input("XC3", life: 100)));
            // NTC (Circolare): ordinary environment, beam, C30 < C0 = 35 → 25 mm; plate 20 mm; C35 → 20 mm; aggressive XD1 → 35 mm; 100 years +10.
            Assert.AreEqual(25, CoverRequirements.Calculate(DurabilityProfile.Ntc2018, Input("XC3")).Durability);
            Assert.AreEqual(20, CoverRequirements.Calculate(DurabilityProfile.Ntc2018, Input("XC3", plate: true)).Durability);
            Assert.AreEqual(20, CoverRequirements.Calculate(DurabilityProfile.Ntc2018, Input("XC3", fck: 35)).Durability);
            Assert.AreEqual(35, CoverRequirements.Calculate(DurabilityProfile.Ntc2018, Input("XD1")).Durability);
            Assert.AreEqual(35, CoverRequirements.Calculate(DurabilityProfile.Ntc2018, Input("XC3", life: 100)).Durability);
            Assert.AreEqual(CoverRequirements.Calculate(DurabilityProfile.Ntc2018, Input("XD3")).Nominal, CoverRequirements.Calculate(DurabilityProfile.CnrDT200, Input("XD3")).Nominal);
            // Minimum strength classes: UNI 11104 (NTC/UNI) and EN 206 F.1.
            Assert.AreEqual(32, ExposureClasses.Uni11104MinimumStrength(new[] { "XC4", "XF2" })); Assert.AreEqual(30, ExposureClasses.En206MinimumStrength(new[] { "XC4", "XF2" }));
        }

        private sealed class CustomAnnex : StandardEN1992p11 { }

        [TestMethod]
        public void ProfilesAreResolvedByExactType()
        {
            Assert.IsFalse(DurabilityProfiles.TryResolve(new CustomAnnex(), out _));
            StringAssert.Contains(DurabilityProfiles.NotSupportedReason(new StandardDINEN1992p11()), "not implemented");
            StringAssert.Contains(DurabilityProfiles.NotSupportedReason(new StandardACI318p19()), "future implementation");
            Assert.IsNull(DurabilityProfiles.NotSupportedReason(new StandardUNIEN1992p11()));
        }
    }
}
